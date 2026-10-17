using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
namespace RainyApartmentVR
{
    // 仅接入现有公寓电视，播放器不改动角色、语音、玩家或地图结构。
    public sealed partial class LocalMediaCenter : MonoBehaviour
    {
        public static string MediaRootOverride, HistoryPathOverride;
        public Renderer originalScreen;
        public string MediaRoot { get; private set; }
        public LocalMediaEntry Current { get; private set; }
        public IReadOnlyList<LocalMediaEntry> Entries => entries;
        public string PlaybackState { get; private set; } = "未播放";
        public string Message { get; private set; } = "把媒体文件放入 VR_Media 后即可选择。";
        public bool PowerOn { get; private set; } = true;
        public bool RainMuted => history.rainMuted;
        public float Volume => history.volume;
        public double PlaybackTime => Current == null ? 0 : Current.kind == LocalMediaKind.Music ? musicStarted ? audioSource.time : unstartedMusicPosition : IsVideo ? seeking ? pendingSeekPosition : video.time : 0;
        public double Duration => Current == null ? 0 : Current.kind == LocalMediaKind.Music ? audioSource.clip ? audioSource.clip.length : 0 : IsVideo && video.isPrepared ? video.length : 0;
        private bool IsVideo => Current != null && (Current.kind == LocalMediaKind.Drama || Current.kind == LocalMediaKind.Movies);
        private List<LocalMediaEntry> entries = new List<LocalMediaEntry>();
        private LocalMediaHistory history;
        private string historyPath;
        private AudioSource audioSource;
        private AudioSource[] rainSources;
        private VideoPlayer video;
        private VideoPlayer.EventHandler prepared;
        private Texture2D photo;
        private UnityWebRequest audioRequest;
        private int generation;
        private bool wantsPlayback, musicStarted, seeking;
        private double pendingSeekPosition, unstartedMusicPosition;
        private float nextScan, nextSave, nextRefresh, preferenceSaveAt;
        private bool preferencesDirty;
        private Task<List<LocalMediaEntry>> scanning;
        private bool StorageEnabled => !Application.isBatchMode || !Application.isEditor || HistoryPathOverride != null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);
        public static LocalMediaCenter Install(Scene scene)
        {
            if (scene.name != "ApartmentDesignV2") return null;
            var screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "电视屏幕");
            if (!screen) return null;
            var existing = screen.GetComponentInChildren<LocalMediaCenter>(true);
            if (existing) return existing;
            var node = new GameObject("电视_本地媒体中心"); node.SetActive(false);
            node.transform.SetParent(screen, false);
            node.transform.position = screen.GetComponent<Renderer>().bounds.center;
            node.transform.rotation = screen.rotation;
            Vector3 scale = screen.lossyScale;
            node.transform.localScale = new Vector3(1f/Mathf.Max(.001f,Mathf.Abs(scale.x)), 1f/Mathf.Max(.001f,Mathf.Abs(scale.y)), 1f/Mathf.Max(.001f,Mathf.Abs(scale.z)));
            var center = node.AddComponent<LocalMediaCenter>(); center.originalScreen = screen.GetComponent<Renderer>();
            node.SetActive(true); return center;
        }
        private void Awake()
        {
            MediaRoot = MediaRootOverride ?? Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "VR_Media");
            historyPath = HistoryPathOverride ?? Path.Combine(Application.persistentDataPath, "电视播放记录.json");
            history = StorageEnabled ? LocalMediaHistory.Load(historyPath) : new LocalMediaHistory();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = .8f;
            audioSource.minDistance = 1.2f; audioSource.maxDistance = 12f; audioSource.volume = history.volume;
            video = gameObject.AddComponent<VideoPlayer>(); video.playOnAwake = false; video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.APIOnly; video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.controlledAudioTrackCount = 1; video.SetTargetAudioSource(0,audioSource); video.EnableAudioTrack(0,true); video.skipOnDrop = true;
            video.waitForFirstFrame = true; video.loopPointReached += OnVideoEnd; video.errorReceived += OnVideoError; video.seekCompleted += OnSeekComplete;
            rainSources = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioSource>(true))
                .Where(a => a.name == "RainAudioSource").ToArray();
            ApplyRainMute(); BuildDisplay(); RefreshLibrary();
        }
        public void RefreshLibrary()
        {
            if (scanning != null && !scanning.IsCompleted) return;
            try { LocalMediaCatalog.EnsureFolders(MediaRoot); scanning = Task.Run(() => LocalMediaCatalog.Scan(MediaRoot)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Message = "媒体文件夹无法读取，请检查文件夹位置。"; Debug.LogWarning(Message); }
            nextScan = Time.unscaledTime + 5f;
        }
        private void Update()
        {
            if (scanning != null && scanning.IsCompleted)
            {
                if (!scanning.IsFaulted && !scanning.IsCanceled) { entries = scanning.Result; ClampPage(); RefreshDisplay(); }
                else { Message = "媒体扫描失败，可以按刷新重试。"; Debug.LogWarning(Message); }
                scanning = null;
            }
            if (Time.unscaledTime >= nextScan) RefreshLibrary();
            if (preferencesDirty && Time.unscaledTime >= preferenceSaveAt) SaveHistory();
            if (IsVideo && video.isPrepared && video.texture) ShowTexture(video.texture, video.width, video.height);
            if (Current != null && Current.kind == LocalMediaKind.Music && musicStarted && PlaybackState == "播放中" && !audioSource.isPlaying)
            {
                history.Remember(Current.id,Duration); SaveHistory(); unstartedMusicPosition=Duration; musicStarted = false; PlaybackState = "已播完"; wantsPlayback = false; Next(1);
            }
            if (Current != null && PlaybackState == "播放中" && Time.unscaledTime >= nextSave)
            {
                RememberCurrent(); SaveHistory(); nextSave = Time.unscaledTime + 5f;

            }
            if (Time.unscaledTime >= nextRefresh) { RefreshDisplay(); nextRefresh = Time.unscaledTime + .25f; }
        }
        public void Play(LocalMediaEntry item, bool resume = true)
        {
            if (item == null || !entries.Any(e => string.Equals(e.id,item.id,StringComparison.OrdinalIgnoreCase))) return;
            if (!File.Exists(item.path)) { Message = "文件已经移走，请刷新列表。"; return; }
            RememberCurrent(); CancelPlayback(); Current = item; PowerOn = true; libraryVisible = false;
            wantsPlayback = true; PlaybackState = "准备中"; Message = LocalMediaCatalog.DisplayTitle(item); RefreshDisplay();
            double position = resume ? history.Position(item.id) : 0; int token = generation;
            if (IsVideo)
            {
                string uri = new Uri(item.path).AbsoluteUri; video.url = uri;
                prepared = player =>
                {
                    if (!this || token != generation || Current != item) return;
                    double length = player.length;
                    SetVideoPosition(length > 1 && position < length-.5 ? Math.Max(0,position) : 0);
                    if (PowerOn && wantsPlayback) { player.Play(); PlaybackState = "播放中"; }
                    else PlaybackState = "已暂停";
                    RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
                };
                video.prepareCompleted += prepared; video.Prepare();
            }
            else if (item.kind == LocalMediaKind.Music) StartCoroutine(LoadMusic(item,position,token));
            else LoadPhoto(item,token);
            nextSave = Time.unscaledTime + 5f;
        }
        private IEnumerator LoadMusic(LocalMediaEntry item, double position, int token)
        {
            string extension = Path.GetExtension(item.path).ToLowerInvariant();
            AudioType type = extension == ".mp3" ? AudioType.MPEG : extension == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(item.path).AbsoluteUri,type))
            {
                audioRequest = request;
                ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
                yield return request.SendWebRequest();
                if (audioRequest == request) audioRequest = null;
                if (!this || token != generation) yield break;
                if (request.result != UnityWebRequest.Result.Success) { Fail("音乐读取失败，请检查格式和文件是否复制完成。"); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (!clip) { Fail("音乐没有可播放的声音轨道。"); yield break; }
                audioSource.clip = clip;
                unstartedMusicPosition = clip.length > .5f && position < clip.length-.5 ? Math.Max(0,position) : 0;
                audioSource.time=(float)unstartedMusicPosition;
                if (PowerOn && wantsPlayback) { audioSource.Play(); audioSource.time=(float)unstartedMusicPosition; musicStarted = true; PlaybackState = "播放中"; } else PlaybackState = "已暂停";
                RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
            }
        }
        private void LoadPhoto(LocalMediaEntry item,int token)
        {
            try
            {
                if (new FileInfo(item.path).Length > 64*1024*1024) { Fail("照片文件过大，请使用较小的图片。"); return; }
                photo = new Texture2D(2,2,TextureFormat.RGBA32,false);
                if (!photo.LoadImage(File.ReadAllBytes(item.path),true)) { Destroy(photo); photo = null; Fail("照片无法打开，请检查图片格式。"); return; }
                if (token != generation) return;
                ShowTexture(photo,(uint)photo.width,(uint)photo.height); PlaybackState = "展示中";
                RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Fail("照片读取失败，文件可能正在复制。"); }
        }
        public void TogglePause()
        {
            if (Current == null) { ContinueLast(); return; }
            if (!PowerOn) { SetPower(true); return; }
            if (Current.kind == LocalMediaKind.Photos) return;
            if (PlaybackState == "播放中" || PlaybackState == "准备中")
            {
                wantsPlayback = false; if (IsVideo) video.Pause(); else audioSource.Pause(); PlaybackState = "已暂停";
            }
            else if (PlaybackState == "已播完") { Play(Current,false); return; }
            else if (PlaybackState != "读取失败")
            {
                wantsPlayback = true;
                if (IsVideo && video.isPrepared) video.Play(); else if (audioSource.clip) { if(musicStarted)audioSource.UnPause();else{audioSource.Play();audioSource.time=(float)unstartedMusicPosition;} musicStarted = true; }
                else { Play(Current); return; }
                PlaybackState = "播放中";
            }
            RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
        }
        public void SetPower(bool on)
        {
            if (PowerOn == on) return;
            PowerOn = on;
            if (!on)
            {
                wantsPlayback = false; if (IsVideo) video.Pause(); else audioSource.Pause();
                if (Current != null && PlaybackState != "读取失败") PlaybackState = Current.kind == LocalMediaKind.Photos ? "展示中" : "已暂停";
            }
            RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
        }
        public void ContinueLast()
        {
            var item = entries.Find(e => string.Equals(e.id,history.lastMedia,StringComparison.OrdinalIgnoreCase));
            if (item != null) Play(item); else { Message = "还没有播放记录，先在媒体库选择一个文件。"; libraryVisible = true; PowerOn = true; RefreshDisplay(); }
        }
        public void Next(int direction)
        {
            if (Current == null) return;
            var candidates = entries.Where(e => e.kind == Current.kind && (Current.kind != LocalMediaKind.Drama || e.series == Current.series)).ToList();
            int index = candidates.FindIndex(e => e.id == Current.id); int next = index+direction;
            if (index < 0 || next < 0 || next >= candidates.Count) { Message = direction > 0 ? "已经是最后一个文件。" : "已经是第一个文件。"; return; }
            Play(candidates[next]);
        }
        public void Seek(double seconds)
        {
            if (Duration <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            seconds = Math.Max(0,Math.Min(Duration-.1,seconds));
            if (IsVideo && video.canSetTime) SetVideoPosition(seconds);
            else if (Current.kind == LocalMediaKind.Music && audioSource.clip) { audioSource.time = (float)seconds; if(!musicStarted)unstartedMusicPosition=seconds; }
            RememberCurrent(); SaveHistory(); PublishMedia(); RefreshDisplay();
        }
        public void SetVolume(float amount)
        {
            if (float.IsNaN(amount)) return;
            history.volume = Mathf.Clamp01(amount); audioSource.volume = history.volume; preferencesDirty = true; preferenceSaveAt = Time.unscaledTime+.35f; RefreshDisplay();
        }
        public void SetRainMuted(bool muted)
        {
            history.rainMuted = muted; ApplyRainMute(); SaveHistory(); RefreshDisplay();
        }
        private void ApplyRainMute() { foreach (var source in rainSources) if (source) source.mute = history.rainMuted; }
        private void SetVideoPosition(double seconds)
        {
            pendingSeekPosition=seconds;seeking=Math.Abs(video.time-seconds)>.01;video.time=seconds;
        }
        private void OnSeekComplete(VideoPlayer source)
        {
            if(seeking&&Math.Abs(source.time-pendingSeekPosition)<.5)seeking=false;
        }
        private void OnVideoEnd(VideoPlayer source)
        {
            if (!PowerOn || !wantsPlayback) return;
            history.Remember(Current.id,Duration); SaveHistory(); PlaybackState = "已播完"; wantsPlayback = false; Next(1);
        }
        private void OnVideoError(VideoPlayer source,string error) { Fail("视频无法播放，请使用可播放的本地视频，或等待文件复制完成后重试。"); Debug.LogWarning("电视视频读取失败："+error); }
        private void Fail(string message) { video.Pause(); audioSource.Pause(); wantsPlayback = false; PlaybackState = "读取失败"; Message = message; PublishMedia(); RefreshDisplay(); }
        private void RememberCurrent() { if (Current != null && (!IsVideo || video.isPrepared) && (Current.kind != LocalMediaKind.Music || audioSource.clip) && PlaybackState != "准备中" && PlaybackState != "读取失败" && PlaybackState != "已播完") history.Remember(Current.id,PlaybackTime); }
        private void SaveHistory()
        {
            preferencesDirty = false;
            if (!StorageEnabled) return;
            try { history.Save(historyPath); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Debug.LogWarning("电视播放记录暂时无法保存。"); }
        }
        private void PublishMedia()
        {
            var value = new MediaEvent { mediaType = Current?.kind.ToString() ?? "", title = Current == null ? "" : LocalMediaCatalog.DisplayTitle(Current),
                episode = Current?.title ?? "", state = PowerOn ? PlaybackState : "电视关闭", playbackTime = PlaybackTime };
            foreach (var hub in FindObjectsByType<PerceptionEventHub>(FindObjectsSortMode.None)) hub.Publish(value);
        }
        private void CancelPlayback()
        {
            generation++; musicStarted = seeking = false; unstartedMusicPosition = 0; if (audioRequest != null) { audioRequest.Abort(); audioRequest = null; }
            if (prepared != null) { video.prepareCompleted -= prepared; prepared = null; }
            video.Stop(); audioSource.Stop();
            if (audioSource.clip) { var old = audioSource.clip; audioSource.clip = null; Destroy(old); }
            if (photo) { Destroy(photo); photo = null; }
            ClearTexture();
        }
        private void OnApplicationQuit() { RememberCurrent(); SaveHistory(); }
        private void OnDestroy()
        {
            if (history == null) return;
            RememberCurrent(); SaveHistory(); CancelPlayback();
            video.loopPointReached -= OnVideoEnd; video.errorReceived -= OnVideoError; video.seekCompleted -= OnSeekComplete;
            DisposeDisplay();
        }
    }
}

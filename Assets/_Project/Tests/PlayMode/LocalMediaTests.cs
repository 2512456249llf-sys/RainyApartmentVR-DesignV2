using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;
namespace RainyApartmentVR.Tests
{
    public sealed class LocalMediaTests
    {
        private string root, history;
        [SetUp] public void 准备隔离媒体目录()
        {
            root = Path.Combine(Application.temporaryCachePath,"电视测试_"+Guid.NewGuid().ToString("N")); history = Path.Combine(root,"播放历史.json");
            LocalMediaCatalog.EnsureFolders(root); LocalMediaCenter.MediaRootOverride = root; LocalMediaCenter.HistoryPathOverride = history;
        }
        [TearDown] public void 清理测试目录()
        {
            foreach (var center in Object.FindObjectsByType<LocalMediaCenter>(FindObjectsSortMode.None)) Object.DestroyImmediate(center.gameObject);
            LocalMediaCenter.MediaRootOverride = LocalMediaCenter.HistoryPathOverride = null;
            if (Directory.Exists(root)) Directory.Delete(root,true);
        }
        private void Fake(string relative)
        {
            string path = Path.Combine(root,relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,new byte[]{1});
        }
        [Test] public void 分类扫描中文路径和数字集数排序()
        {
            Fake("Drama/雨夜故事/10.mp4"); Fake("Drama/雨夜故事/2.mp4"); Fake("Drama/雨夜故事/01.MP4"); Fake("Drama/雨夜故事/cover.jpg");
            Fake("Music/歌曲.MP3"); Fake("Photos/旅行.png"); Fake("直接拖入.mp4"); Fake("Movies/未完成.mp4.part");
            var entries = LocalMediaCatalog.Scan(root); Assert.AreEqual(6,entries.Count);
            CollectionAssert.AreEqual(new[]{"01","2","10"},entries.Where(e=>e.kind==LocalMediaKind.Drama).Select(e=>e.title).ToArray());
            Assert.IsTrue(entries.Any(e=>e.id=="直接拖入.mp4")); Assert.IsFalse(entries.Any(e=>e.title=="cover"));
        }
        [Test] public void 播放历史音量雨声持久保存并可恢复备份()
        {
            var value = new LocalMediaHistory { volume=.3f, rainMuted=true }; value.Remember("Drama/剧名/23.mp4",766); value.Save(history);
            value.Remember("Drama/剧名/23.mp4",800); value.Save(history);
            var loaded = LocalMediaHistory.Load(history); Assert.AreEqual(800,loaded.Position("Drama/剧名/23.mp4")); Assert.AreEqual(.3f,loaded.volume); Assert.IsTrue(loaded.rainMuted);
            File.WriteAllText(history,"损坏内容"); loaded = LocalMediaHistory.Load(history); Assert.AreEqual(766,loaded.Position("Drama/剧名/23.mp4"));
        }
        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            LocalMediaCenter.Install(SceneManager.GetActiveScene());
            var center = Object.FindFirstObjectByType<LocalMediaCenter>(); Assert.IsNotNull(center);
            float until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until && center.Entries.Count==0) yield return null;
        }
        [UnityTest] public IEnumerator 雨声按钮真实交互不影响角色与电视音源()
        {
            Fake("Movies/占位.mp4"); yield return LoadScene();
            var center = Object.FindFirstObjectByType<LocalMediaCenter>(); var rain = GameObject.Find("RainAudioSource").GetComponent<AudioSource>();
            var voice = Object.FindFirstObjectByType<VoiceDialogue>(); float volume=voice.characterAudio.volume;
            var button = GameObject.Find("电视旁雨声开关").GetComponent<XRSimpleInteractable>();
            var controller = new GameObject("电视按钮测试手"); var collider=controller.AddComponent<SphereCollider>(); collider.isTrigger=true;
            var interactor=controller.AddComponent<XRDirectInteractor>(); interactor.interactionManager=Object.FindFirstObjectByType<XRInteractionManager>();
            yield return null; interactor.StartManualInteraction((IXRSelectInteractable)button); yield return null;
            Assert.IsTrue(rain.mute); Assert.IsTrue(center.RainMuted); Assert.AreEqual(volume,voice.characterAudio.volume);
            Assert.IsFalse(center.GetComponent<AudioSource>().mute);
            interactor.EndManualInteraction(); Object.Destroy(controller);
            center.SetRainMuted(false); Assert.IsFalse(rain.mute);
        }
        [UnityTest] public IEnumerator 本地视频真实准备播放暂停跳转与续播()
        {
            string fixture=Path.Combine(Application.dataPath,"../BuildLogs/电视测试素材/本地解码样片.mp4");
            Assert.IsTrue(File.Exists(fixture)); File.Copy(fixture,Path.Combine(root,"Movies/样片.mp4"));
            yield return LoadScene(); var center=Object.FindFirstObjectByType<LocalMediaCenter>(); center.Play(center.Entries.Single());
            float until=Time.realtimeSinceStartup+15; while(Time.realtimeSinceStartup<until && center.PlaybackState=="准备中")yield return null;
            Assert.AreEqual("播放中",center.PlaybackState,center.Message); Assert.Greater(center.Duration,1);
            var player=center.GetComponent<VideoPlayer>(); Assert.IsTrue(player.isPrepared); yield return new WaitForSeconds(.6f);
            Assert.IsNotNull(player.texture); Assert.Greater(center.PlaybackTime,0);
            center.TogglePause(); Assert.AreEqual("已暂停",center.PlaybackState); double paused=center.PlaybackTime;
            yield return new WaitForSeconds(.3f); Assert.AreEqual(paused,center.PlaybackTime,.2);
            center.Seek(Math.Min(2,center.Duration*.25)); yield return new WaitForSeconds(.4f); Assert.Greater(center.PlaybackTime,.2);
            double saved=center.PlaybackTime; center.SetPower(false); Assert.IsFalse(player.isPlaying);
            var loaded=LocalMediaHistory.Load(history); Assert.AreEqual(saved,loaded.Position(center.Current.id),.3);
            center.ContinueLast(); until=Time.realtimeSinceStartup+10; while(Time.realtimeSinceStartup<until && center.PlaybackState=="准备中")yield return null;
            yield return new WaitForSeconds(.5f); Assert.IsTrue(center.PowerOn); Assert.Greater(center.PlaybackTime,saved-.5);
            Capture(center,"电视播放器应用实际画面.png");
            center.SetFullScreen(true); Assert.IsTrue(center.IsFullScreen); yield return null;
            var videoImage=center.GetComponentsInChildren<RawImage>(true).First(t=>t.name=="电视播放画面"); Assert.Greater(videoImage.rectTransform.rect.width,1800);
            typeof(LocalMediaCenter).GetField("controlsUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(center,Time.unscaledTime-1);
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(center.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="应用播放控制栏").gameObject.activeSelf);
            Capture(center,"电视本地视频实际画面.png");
        }
        [UnityTest] public IEnumerator 本地音乐读取和图片保留比例()
        {
            string wav=Path.Combine(root,"Music/测试声音.wav");
            using(var writer=new BinaryWriter(File.Create(wav)))
            {
                int samples=44100*8; writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+samples*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples*2); for(int i=0;i<samples;i++)writer.Write((short)(Math.Sin(i*440*Math.PI*2/44100)*1500));
            }
            var texture=new Texture2D(200,100); var pixels=Enumerable.Repeat(new Color(.1f,.55f,.8f),200*100).ToArray(); texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(Path.Combine(root,"Photos/宽照片.png"),texture.EncodeToPNG()); Object.Destroy(texture);
            yield return LoadScene(); var center=Object.FindFirstObjectByType<LocalMediaCenter>(); center.Play(center.Entries.First(e=>e.kind==LocalMediaKind.Music));
            float until=Time.realtimeSinceStartup+10; while(Time.realtimeSinceStartup<until && center.PlaybackState=="准备中")yield return null;
            Assert.AreEqual("播放中",center.PlaybackState); Assert.Greater(center.Duration,7);
            center.Seek(2.5); center.TogglePause(); Assert.AreEqual("已暂停",center.PlaybackState);
            center.Play(center.Current); until=Time.realtimeSinceStartup+10;while(Time.realtimeSinceStartup<until&&center.PlaybackState=="准备中")yield return null;
            yield return new WaitForSeconds(.2f);Assert.Greater(center.GetComponent<AudioSource>().time,2.4f,"音乐续播位置丢失");
            center.Play(center.Entries.First(e=>e.kind==LocalMediaKind.Photos)); Assert.AreEqual("展示中",center.PlaybackState);
            Assert.IsFalse(center.GetComponent<AudioSource>().isPlaying); Assert.IsFalse(center.GetComponent<VideoPlayer>().isPlaying);
            var picture=center.GetComponentsInChildren<RawImage>(true).First(t=>t.name=="电视播放画面"); Assert.AreEqual(2f,picture.rectTransform.rect.width/picture.rectTransform.rect.height,.01);
        }
        [UnityTest] public IEnumerator 空目录可操作刷新且重复安装不重复按钮()
        {
            yield return LoadScene(); var center=Object.FindFirstObjectByType<LocalMediaCenter>(); Assert.AreEqual(0,center.Entries.Count);
            Assert.AreEqual(center,LocalMediaCenter.Install(SceneManager.GetActiveScene())); Assert.AreEqual(1,Object.FindObjectsByType<LocalMediaCenter>(FindObjectsSortMode.None).Length);
            Fake("Music/刚拖入.mp3"); center.RefreshLibrary(); float until=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<until && center.Entries.Count==0)yield return null;
            Assert.AreEqual(1,center.Entries.Count);
            GameObject.Find("媒体分类_音乐").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.IsNotNull(GameObject.Find("电视应用画布").GetComponent<Canvas>());
            Assert.Less(GameObject.Find("电视媒体标题").GetComponent<RectTransform>().rect.width,1920f);
            Assert.Less(GameObject.Find("电视旁雨声开关_文字").GetComponent<Renderer>().bounds.size.x,.46f);
            Capture(center,"电视媒体列表与雨声按钮.png");
        }
        [UnityTest] public IEnumerator 屏幕应用按钮射线命中且不截获空格语音焦点()
        {
            Fake("Movies/占位.mp4");yield return LoadScene();var center=Object.FindFirstObjectByType<LocalMediaCenter>();
            Canvas.ForceUpdateCanvases();var button=GameObject.Find("媒体分类_音乐").GetComponent<RectTransform>();
            Vector3 point=button.TransformPoint(button.rect.center);Assert.IsTrue(center.ClickScreen(point));yield return null;
            StringAssert.Contains("音乐",GameObject.Find("电视媒体标题").GetComponent<Text>().text);
            Assert.IsNull(EventSystem.current.currentSelectedGameObject);
            var canvas=center.GetComponentInChildren<Canvas>();Assert.AreEqual(RenderMode.WorldSpace,canvas.renderMode);
            Assert.IsNotNull(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>());
            var footer=GameObject.Find("应用播放控制栏").GetComponent<RectTransform>();
            Vector3[] corners=new Vector3[4];footer.GetWorldCorners(corners);var area=canvas.GetComponent<RectTransform>();
            foreach(var corner in corners){Vector3 local=area.InverseTransformPoint(corner);Assert.IsTrue(new Rect(area.rect.xMin-.5f,area.rect.yMin-.5f,area.rect.width+1,area.rect.height+1).Contains(local));}
        }
        private static void Capture(LocalMediaCenter center,string name)
        {
            var node=new GameObject("电视验收相机"); var camera=node.AddComponent<Camera>(); camera.fieldOfView=48; camera.nearClipPlane=.05f;
            node.transform.position=center.transform.position+new Vector3(.2f,-.2f,-3.1f); node.transform.LookAt(center.transform.position+new Vector3(.1f,-.25f,0));
            var rt=new RenderTexture(1400,1000,24); var image=new Texture2D(1400,1000,TextureFormat.RGB24,false); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1400,1000),0,0); image.Apply(); Directory.CreateDirectory("BuildLogs"); File.WriteAllBytes("BuildLogs/"+name,image.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=null; Object.Destroy(node); Object.Destroy(rt); Object.Destroy(image);
        }
    }
}

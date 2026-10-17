using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace RainyApartmentVR
{
    // 一个持续会话同时处理听、想、说；不再启动外部识别和配音进程。
    // 后端：通义千问 Omni 实时（DashScope Realtime，协议与 OpenAI Realtime 兼容）。
    public sealed partial class VoiceDialogue : MonoBehaviour
    {
        [Serializable] private sealed class Settings
        {
            public string DashScopeApiKey;
            public string QwenRealtimeModel;
            public string QwenVoice;
            public string QwenRealtimeUrl;
        }

        [Serializable] private sealed class Event
        {
            public string type;
            public string delta;
            public string transcript;
            public string name;
            public string call_id;
            public string arguments;
            public ErrorDetail error;
        }

        [Serializable] private sealed class ErrorDetail { public string message; }
        [Serializable] private sealed class ActionArgument { public string action; }

        // 兜底动作池：说话时看起来自然、不依赖走位的动作。
        // 未知语境时只补中性动作；生气、难过、鼓掌等由模型按内容选择，不能随机乱演。
        private static readonly string[] SpeechFallbackActions =
        {
            // 程序化头部/面部小动作（走路、坐下、牵手时也能用）
            "nod", "shake", "smile", "look_away", "look_player", "tilt_head",
            "hand_chest", "lean_in",
            // 交谈与视线
            "talk_soft", "talk_animated", "talking", "talking_2", "look_around", "look_around_2",
            "look_away_gesture",
            // 头部姿态变体
            "nod_yes", "nod_long", "nod_thoughtful",
            "breathing",
            // 手势
            "weight_shift_gesture"
        };

        private const string DefaultUrl = "wss://dashscope.aliyuncs.com/api-ws/v1/realtime";
        private const int InputRate = 16000;
        private const int OutputRate = 24000;

        public AudioSource characterAudio;
        public CharacterVoicePerformance performance;
        public CharacterBehaviour behaviour;
        public CharacterActionLibrary actions;

        [Tooltip("模型本轮没选动作时，开口说话自动补一个自然小动作")]
        public bool speechActionFallback = true;

        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private readonly Queue<float> audioQueue = new Queue<float>();
        private readonly object audioLock = new object();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private readonly List<string> pendingCalls = new List<string>();
        private readonly Dictionary<string, bool> callResults = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> callActionIds = new Dictionary<string, string>();
        private ClientWebSocket socket;
        private CancellationTokenSource lifetime;
        private AudioClip microphoneClip;
        private AudioClip outputClip;
        private TextMesh statusText;
        private string microphoneName;
        private string transcript = "";
        private string status = "正在准备实时语音…";
        private VoiceDialogueState state = VoiceDialogueState.Idle;
        private int microphoneReadPosition;
        private bool previousButton;
        private bool connecting;
        private bool ready;
        private bool turnActive;
        private bool actionCalledThisTurn;
        private bool fallbackFired;
        private bool responseComplete;
        private bool playbackStarted;
        private float firstAudioTime;
        private float lastAudioTime;
        private float lastPlayedSampleAt;
        private float lastActionAt;
        public VoiceDialogueState CurrentState => state;
        public string Status => status;

        private void Awake()
        {
            if (!performance) performance = GetComponent<CharacterVoicePerformance>();
            if (!behaviour) behaviour = GetComponent<CharacterBehaviour>();
            if (!actions) actions = GetComponent<CharacterActionLibrary>();
            if (!characterAudio)
            {
                var animator = GetComponentInChildren<Animator>(true);
                var head = animator && animator.avatar && animator.avatar.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : transform;
                var holder = new GameObject("角色声音_运行时");
                holder.transform.SetParent(head ? head : transform, false);
                characterAudio = holder.AddComponent<AudioSource>();
            }
            characterAudio.playOnAwake = false;
            characterAudio.spatialBlend = 1f;
            characterAudio.minDistance = .75f;
            characterAudio.maxDistance = 9f;
            if (performance) performance.speechAudio = characterAudio;
            CreateStatusText();
            InitializeTouchAutonomy();
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                SetStatus("这个试玩版目前只支持电脑端语音。", VoiceDialogueState.Idle);
            else if (string.IsNullOrWhiteSpace(ReadSettings()?.DashScopeApiKey))
                SetStatus("请在项目根目录 qwen_secrets.json 填写 DashScopeApiKey。", VoiceDialogueState.Idle);
            else if (!Application.isBatchMode) _ = ConnectAsync();
        }

        private void Update()
        {
            for (int i = 0; i < 64 && messages.TryDequeue(out var json); i++) Handle(json);
            bool button = ButtonHeld();
            if (button && !previousButton)
            {
                if (!ready && !connecting) _ = ConnectAsync();
                else if (ready && !turnActive) StartMicrophone();
            }
            if (turnActive && microphoneClip) SendMicrophoneChunks();
            if (!button && previousButton && microphoneClip) StopMicrophone();
            previousButton = button;

            if (turnActive && !playbackStarted && firstAudioTime > 0f)
            {
                int buffered;
                lock (audioLock) buffered = audioQueue.Count;
                if (buffered >= OutputRate / 4 || Time.realtimeSinceStartup - firstAudioTime > .35f) StartPlayback();
            }
            if (turnActive && playbackStarted)
            {
                int queuedNow;
                lock (audioLock) queuedNow = audioQueue.Count;
                if (queuedNow > 0) lastPlayedSampleAt = Time.realtimeSinceStartup;
            }
            // 看门狗：松开按钮后服务器 15 秒没动静（没回音频、没回执），说明这轮已经死透了，强制复位，保证按键永远能开新回合。
            if (turnActive && !microphoneClip && pendingCalls.Count == 0 &&
                Time.realtimeSinceStartup - lastAudioTime > 15f &&
                Time.realtimeSinceStartup - lastPlayedSampleAt > 15f)
                ForceResetTurn("回合超时");
            if (turnActive && responseComplete)
            {
                // 以播放端为准：队列读空后声卡缓冲里还有最后一点声音，等它真正播完再结束回合，否则每句最后一个字都会被切掉。
                int buffered;
                lock (audioLock) buffered = audioQueue.Count;
                if (buffered > 0) lastPlayedSampleAt = Time.realtimeSinceStartup;
                if (pendingCalls.Count == 0 && Time.realtimeSinceStartup - lastPlayedSampleAt > .9f) EndTurn();
            }
            TickTouchAutonomy();
            if (!IsAutonomousTurn && speechActionFallback && turnActive && playbackStarted &&
                     Time.realtimeSinceStartup - lastActionAt > 6f)
            {
                // 说话过程中若模型和兜底都超过 6 秒没动作，补一个自然小动作。
                TrySpeechFallbackAction();
            }
            if (statusText && Camera.main)
                statusText.transform.rotation = Quaternion.LookRotation(statusText.transform.position - Camera.main.transform.position);
        }

        private static bool ButtonHeld()
        {
            bool keyboard = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            return keyboard || hand.isValid && hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed;
        }

        private async Task ConnectAsync()
        {
            if (connecting || ready) return;
            var config = ReadSettings();
            if (config == null || string.IsNullOrWhiteSpace(config.DashScopeApiKey))
            {
                SetStatus("缺少千问接口密钥，请填写 qwen_secrets.json。", VoiceDialogueState.Idle);
                return;
            }
            connecting = true;
            SetStatus("正在连接实时语音…", VoiceDialogueState.Thinking);
            try
            {
                lifetime?.Cancel();
                socket?.Dispose();
                socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("Authorization", "Bearer " + config.DashScopeApiKey.Trim());
                socket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");
                lifetime = new CancellationTokenSource();
                string model = string.IsNullOrWhiteSpace(config.QwenRealtimeModel) ? "qwen3.8-omni-flash-realtime" : config.QwenRealtimeModel.Trim();
                string baseUrl = string.IsNullOrWhiteSpace(config.QwenRealtimeUrl) ? DefaultUrl : config.QwenRealtimeUrl.Trim();
                var uri = new Uri(baseUrl + "?model=" + Uri.EscapeDataString(model));
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    await socket.ConnectAsync(uri, timeout.Token);
                _ = Task.Run(() => Receive(lifetime.Token));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("实时语音连接失败：" + ex.GetType().Name);
                connecting = false;
                SetStatus("连接失败，请检查接口密钥、网络和模型权限。", VoiceDialogueState.Idle);
            }
        }

        private async Task Receive(CancellationToken token)
        {
            var buffer = new byte[65536];
            using (var stream = new MemoryStream())
            {
                try
                {
                    while (!token.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open)
                    {
                        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        stream.Write(buffer, 0, result.Count);
                        if (!result.EndOfMessage) continue;
                        if (result.MessageType == WebSocketMessageType.Text)
                            messages.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
                        stream.SetLength(0);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Debug.LogWarning("实时语音接收中断：" + ex.GetType().Name); }
            }
            messages.Enqueue("{\"type\":\"goAway\"}");
        }

        private async Task Send(string json)
        {
            await sendLock.WaitAsync();
            try
            {
                if (TransportOverride != null) { await TransportOverride(json); return; }
                if (socket == null || socket.State != WebSocketState.Open) return;
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
            }
            catch (Exception ex) { Debug.LogWarning("实时语音发送失败：" + ex.GetType().Name); }
            finally { sendLock.Release(); }
        }

        private string BuildInstruction()
        {
            return
                "你现在有一具可活动的虚拟身体，正在雨夜公寓里和玩家面对面交谈。" +
                "用自然、简短的中文口语回答，像真人邻居聊天，不要朗读，不要客套套话。" +
                "重要规则：每次开口说话前必须先调用一次 perform_action，为身体选一个贴合此刻要说内容和情绪的动作（必须，不是可选，先调动作再开口）；" +
                "说话较长时中途可以再补一两个动作；动作由你自主判断，不是听到固定词才执行。动作库里有" + CharacterActionLibrary.ActionIds.Length + "个动作，根据内容在点头摇头、视线、手势、情绪、惊讶、思考、耸肩、鼓掌、鞠躬、笑、难过、兴奋等不同类型之间换着选，每轮尽量挑和前几轮不同类别的动作；最近几轮用过的动作先换同义变体，不要总是用点头、微笑、说话手势那几样。" +
                "玩家明确让你靠近（比如“过来”“来我这儿”）时，这一轮动作必须选 approach_player；让你坐下、蹲下、站起来、停下时分别对应选 sit、crouch、stand、stop。" +
                "动作 id 只能使用清单里列出的值，绝对不要编造清单外的新 id；跪下、膜拜、祈祷使用 kneel_pray；起身使用 stand；不能将跪下映射为坐下或蹲下。没有趴下动作，说明目前做不到即可。" +
                "如果动作执行结果返回失败，不要假装成功：可换一个符合当前语境的轻动作，或自然地告诉玩家稍等一下，不要机械重复同一种动作。" +
                "不要捏造你与玩家未发生过的共同经历。普通语音轮次不要使用respond_to_touch，它仅用于明确收到身体感知事件的轮次。" +
                "走动类动作（approach_player 走近一点、walk_around 换个位置、sit 坐下、crouch 蹲下、stand 起立、stop 停步）要克制、看情境，偶尔为之，不要一直跟着玩家。" +
                "可用动作：" + CharacterActionLibrary.PromptCatalog;
        }

        private string BuildSetup()
        {
            var config = ReadSettings();
            string voice = config != null && !string.IsNullOrWhiteSpace(config.QwenVoice) ? config.QwenVoice.Trim() : "Evan";
            string instruction = BuildInstruction();
            string actionParams =
                "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"" + Escape("动作 id，只能从以下值选一个：" + string.Join("、", CharacterActionLibrary.ActionIds)) + "\"}},\"required\":[\"action\"]}";
            return "{\"type\":\"session.update\",\"session\":{" +
                "\"modalities\":[\"audio\",\"text\"]," +
                "\"voice\":\"" + Escape(voice) + "\"," +
                "\"instructions\":\"" + Escape(instruction) + "\"," +
                "\"input_audio_format\":\"pcm16\"," +
                "\"output_audio_format\":\"pcm16\"," +
                "\"turn_detection\":null,\"tool_choice\":\"auto\"," +
                "\"tools\":[{\"type\":\"function\",\"function\":{\"name\":\"perform_action\"," +
                "\"description\":\"" + Escape("每次开口说话前必须调用一次：根据要说的内容和情绪，为身体挑一个动作同步执行。玩家要求靠近时传 approach_player，蹲下传 crouch，坐下传 sit，站起传 stand，停下传 stop。") + "\"," +
                "\"parameters\":" + actionParams + "}}," + TouchTool + "]}}";
        }

        private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n");

        private void StartMicrophone()
        {
            touchQueue.Clear();
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                SetStatus("未找到麦克风，请检查头显麦克风和录音权限。", VoiceDialogueState.Idle);
                return;
            }
            microphoneName = Microphone.devices.FirstOrDefault(name => name.Contains("Steam") || name.Contains("Quest") || name.Contains("Oculus")) ?? Microphone.devices[0];
            try { microphoneClip = Microphone.Start(microphoneName, true, 60, InputRate); }
            catch (Exception ex) { Debug.LogWarning("麦克风启动失败：" + ex.GetType().Name); }
            if (!microphoneClip)
            {
                SetStatus("麦克风无法启动。", VoiceDialogueState.Idle);
                return;
            }
            microphoneReadPosition = 0;
            turnActive = true;
            responseComplete = false;
            playbackStarted = false;
            firstAudioTime = 0f;
            lastAudioTime = Time.realtimeSinceStartup;
            transcript = "";
            pendingCalls.Clear();
            actionCalledThisTurn = false;
            fallbackFired = false;
            lastPlayedSampleAt = 0f;
            lastActionAt = Time.realtimeSinceStartup;
            callResults.Clear();
            callActionIds.Clear();
            lock (audioLock) audioQueue.Clear();
            characterAudio.Stop();
            characterAudio.clip = null;
            if (outputClip) Destroy(outputClip);
            outputClip = null;
            _ = Send("{\"type\":\"input_audio_buffer.clear\"}");
            SetStatus("我在听，松开按钮后回答。", VoiceDialogueState.Listen);
        }

        private void SendMicrophoneChunks()
        {
            int current = Microphone.GetPosition(microphoneName);
            if (current < 0) return;
            int available = current - microphoneReadPosition;
            if (available < 0) available += microphoneClip.samples;
            while (available >= 1024)
            {
                int count = Math.Min(2048, available);
                var values = new float[count * microphoneClip.channels];
                if (!microphoneClip.GetData(values, microphoneReadPosition)) break;
                var pcm = new byte[count * 2];
                for (int i = 0; i < count; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < microphoneClip.channels; c++) sum += values[i * microphoneClip.channels + c];
                    short sample = (short)(Mathf.Clamp(sum / microphoneClip.channels, -1f, 1f) * 32767f);
                    pcm[i * 2] = (byte)sample;
                    pcm[i * 2 + 1] = (byte)(sample >> 8);
                }
                _ = Send("{\"type\":\"input_audio_buffer.append\",\"audio\":\"" + Convert.ToBase64String(pcm) + "\"}");
                microphoneReadPosition = (microphoneReadPosition + count) % microphoneClip.samples;
                available -= count;
            }
        }

        private void StopMicrophone()
        {
            SendMicrophoneChunks();
            Microphone.End(microphoneName);
            Destroy(microphoneClip);
            microphoneClip = null;
            _ = Send("{\"type\":\"input_audio_buffer.commit\"}");
            _ = Send("{\"type\":\"response.create\"}");
            lastAudioTime = Time.realtimeSinceStartup;
            lastPlayedSampleAt = Time.realtimeSinceStartup;
            SetStatus("正在听取角色回答…", VoiceDialogueState.Thinking);
        }

        internal void Handle(string json)
        {
            Event message;
            try { message = JsonUtility.FromJson<Event>(json); }
            catch (Exception) { return; }
            if (message == null || string.IsNullOrEmpty(message.type)) return;
            switch (message.type)
            {
                case "session.created":
                    _ = Send(BuildSetup());
                    break;
                case "session.updated":
                    connecting = false;
                    ready = true;
                    SetStatus("按住左手柄主按钮说话，松开后回答；电脑测试按空格。", VoiceDialogueState.Idle);
                    break;
                case "conversation.item.input_audio_transcription.completed":
                    if (!string.IsNullOrWhiteSpace(message.transcript))
                        SetStatus("听到：「" + Shorten(message.transcript, 38) + "」", VoiceDialogueState.Thinking);
                    break;
                case "response.audio_transcript.delta":
                    if (IsAutonomousTurn && touchPhase != TouchTurnPhase.Speaking) break;
                    if (!string.IsNullOrEmpty(message.delta))
                    {
                        transcript += message.delta;
                        SetStatus("角色：「" + Shorten(transcript, 44) + "」", VoiceDialogueState.TalkNormal);
                    }
                    break;
                case "response.audio.delta":
                    if (IsAutonomousTurn && touchPhase != TouchTurnPhase.Speaking) break;
                    if (!string.IsNullOrEmpty(message.delta)) AddAudio(message.delta);
                    break;
                case "response.function_call_arguments.done":
                    HandleToolCall(message);
                    break;
                case "goAway":
                {
                    // Receive 循环结束（连接断开/被关闭/异常）都会投递这条消息。此前没有处理它，
                    // 断线后 ready 永远是 true、Send 静默丢弃，按键就再也没反应了。
                    ready = false;
                    connecting = false;
                    ForceResetTurn("连接中断");
                    SetStatus("语音连接断开了，正在自动重连…", VoiceDialogueState.Thinking);
                    _ = ConnectAsync();
                    break;
                }
                case "response.done":
                    if (CompleteTouchResponse()) break;
                    if (pendingCalls.Count > 0)
                    {
                        var calls = pendingCalls.ToArray();
                        pendingCalls.Clear();
                        _ = SendFunctionOutputs(calls);
                    }
                    else
                    {
                        if (!actionCalledThisTurn)
                            Debug.Log("角色自主动作：本轮回答模型没有调用 perform_action" + (fallbackFired ? "，已自动补一个自然动作。" : "（纯语音回复）。"));
                        responseComplete = true;
                    }
                    break;
                case "error":
                {
                    Debug.LogWarning("实时语音事件错误：" + json);
                    string errorText = message.error?.message ?? "";
                    ForceResetTurn("服务端错误");
                    if (errorText.Contains("50002") || errorText.Contains("InternalError"))
                        SetStatus("语音服务刚才开小差了（50002），请按住按钮再说一次。", VoiceDialogueState.Idle);
                    else
                        SetStatus("语音服务拒绝了请求：" + Shorten(errorText.Length > 0 ? errorText : "请查看运行日志。", 52), VoiceDialogueState.Idle);
                    if ((socket == null || socket.State != WebSocketState.Open) && !connecting) _ = ConnectAsync();
                    break;
                }
            }
        }

        private void HandleToolCall(Event message)
        {
            if (IsAutonomousTurn) { HandleTouchTool(message); return; }
            bool ok = false;
            if (message.name == "perform_action")
            {
                if (!actions) Debug.LogWarning("角色自主动作失败：动作组件未接线（actions 为空）。");
                else
                {
                    try
                    {
                        var argument = string.IsNullOrEmpty(message.arguments) ? null : JsonUtility.FromJson<ActionArgument>(message.arguments);
                        ok = actions.TryPerform(argument?.action);
                    }
                    catch (Exception ex) { Debug.LogWarning("角色自主动作参数无效：" + ex.GetType().Name); }
                }
                if (ok) actionCalledThisTurn = true;
            }
            if (!string.IsNullOrEmpty(message.call_id))
            {
                pendingCalls.Add(message.call_id);
                callResults[message.call_id] = ok;
                if (ok && actions) callActionIds[message.call_id] = actions.LastPerformedId;
            }
            if (ok) lastActionAt = Time.realtimeSinceStartup;
            else if (message.name == "perform_action" && playbackStarted && !fallbackFired)
                TrySpeechFallbackAction();
            Debug.Log("角色自主动作：" + (message.arguments ?? "") + " -> " + (ok ? "已执行" : "无法执行"));
        }

        // 模型本轮没主动选动作时，在开口瞬间补一个自然小动作，保证说话带动作。
        private void TrySpeechFallbackAction()
        {
            if (!speechActionFallback || !actions) return;
            lastActionAt = Time.realtimeSinceStartup;  // 无论成败都按 6 秒间隔，避免连续重试刷屏
            if (actions.IsPlayingClip) return;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string pick = actions.PickSpeechFallback(SpeechFallbackActions);
                if (actions.TryPerform(pick))
                {
                    fallbackFired = true;
                    lastActionAt = Time.realtimeSinceStartup;
                    Debug.Log("角色自主动作：说话时补充了 " + actions.LastPerformedId + "。");
                    return;
                }
            }
            Debug.Log("角色自主动作：兜底动作也未能执行（可能正在走位或坐下）。");
        }

        private async Task SendFunctionOutputs(string[] calls)
        {
            await sendLock.WaitAsync();
            try
            {
                if (socket == null || socket.State != WebSocketState.Open) return;
                foreach (var id in calls)
                {
                    bool executed = callResults.TryGetValue(id, out var result) && result;
                    string outcome = executed
                        ? "动作已安排（已有动作时将顺序播放）：" + (callActionIds.TryGetValue(id, out var performed) ? performed : "已选动作") +
                          "。最近动作：" + (actions ? actions.RecentActionSummary : "暂无") + "。接着自然回答，下一轮请换符合内容的动作。"
                        : "动作没有执行成功（可能在冷却、正在走动或坐着）。请按当前语境选另一种轻动作，避免反复点头或微笑；也可以直接答话。";
                    string item = "{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"function_call_output\",\"call_id\":\"" +
                        Escape(id) + "\",\"output\":\"" + Escape(outcome) + "\"}}";
                    byte[] bytes = Encoding.UTF8.GetBytes(item);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
                }
                byte[] create = Encoding.UTF8.GetBytes("{\"type\":\"response.create\"}");
                await socket.SendAsync(new ArraySegment<byte>(create), WebSocketMessageType.Text, true, lifetime.Token);
            }
            catch (Exception ex) { Debug.LogWarning("动作回执发送失败：" + ex.GetType().Name); }
            finally { sendLock.Release(); }
        }

        private void AddAudio(string encoded)
        {
            byte[] pcm;
            try { pcm = Convert.FromBase64String(encoded); }
            catch (FormatException) { return; }
            lock (audioLock)
                for (int i = 0; i + 1 < pcm.Length; i += 2)
                    audioQueue.Enqueue((short)(pcm[i] | pcm[i + 1] << 8) / 32768f);
            if (firstAudioTime == 0f) firstAudioTime = Time.realtimeSinceStartup;
            lastAudioTime = Time.realtimeSinceStartup;
        }

        private void StartPlayback()
        {
            if (playbackStarted) return;
            playbackStarted = true;
            lastPlayedSampleAt = Time.realtimeSinceStartup;
            if (!IsAutonomousTurn && !actionCalledThisTurn) TrySpeechFallbackAction();
            outputClip = AudioClip.Create("角色实时声音", OutputRate * 180, 1, OutputRate, true, FillAudio);
            characterAudio.clip = outputClip;
            characterAudio.Play();
            if (behaviour) behaviour.SetSpeaking(true);
            SetStatus("角色正在回答…", VoiceDialogueState.TalkNormal);
        }

        private void FillAudio(float[] data)
        {
            lock (audioLock)
                for (int i = 0; i < data.Length; i++) data[i] = audioQueue.Count > 0 ? audioQueue.Dequeue() : 0f;
        }

        private void EndTurn()
        {
            bool wasAutonomous = IsAutonomousTurn;
            ResetTouchTurn();
            if (microphoneClip)
            {
                Microphone.End(microphoneName);
                Destroy(microphoneClip);
                microphoneClip = null;
            }
            turnActive = false;
            responseComplete = false;
            firstAudioTime = 0f;
            characterAudio.Stop();
            characterAudio.clip = null;
            if (outputClip) Destroy(outputClip);
            outputClip = null;
            if (behaviour) behaviour.SetSpeaking(false);
            SetStatus("按住左手柄主按钮继续说话。", VoiceDialogueState.Idle);
            if (wasAutonomous && ready && ButtonHeld()) StartMicrophone();
        }

        // 服务端报错、连接中断或回合超时后，强制结束本轮并复位全部回合状态。
        // 不复位的话 turnActive 会一直是 true，而按键要求 !turnActive 才能开新回合，于是怎么按都没反应。
        private void ForceResetTurn(string reason)
        {
            touchQueue.Clear(); ResetTouchTurn();
            if (microphoneClip)
            {
                Microphone.End(microphoneName);
                Destroy(microphoneClip);
                microphoneClip = null;
            }
            lock (audioLock) audioQueue.Clear();
            if (characterAudio) characterAudio.Stop();
            if (characterAudio) characterAudio.clip = null;
            if (outputClip) Destroy(outputClip);
            outputClip = null;
            pendingCalls.Clear();
            callResults.Clear();
            callActionIds.Clear();
            turnActive = false;
            responseComplete = false;
            playbackStarted = false;
            actionCalledThisTurn = false;
            fallbackFired = false;
            firstAudioTime = 0f;
            lastPlayedSampleAt = 0f;
            if (behaviour) behaviour.SetSpeaking(false);
            if (performance) performance.SetState(VoiceDialogueState.Idle);
            Debug.Log("对话回合已强制复位（" + reason + "），按住按钮可以重新说话。");
        }

        private static string Shorten(string value, int length) => value.Length <= length ? value : value.Substring(0, length) + "…";

        private static Settings ReadSettings()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (var path in new[] { Path.Combine(project, "qwen_secrets.json"),
                         Path.GetFullPath(Path.Combine(project, "..", "..", "qwen_secrets.json")),
                         Path.Combine(project, "secrets.json"),
                         Path.GetFullPath(Path.Combine(project, "..", "..", "secrets.json")) })
            {
                if (!File.Exists(path)) continue;
                try { return JsonUtility.FromJson<Settings>(File.ReadAllText(path, Encoding.UTF8)); }
                catch (Exception ex) { Debug.LogWarning("密钥配置读取失败：" + ex.GetType().Name); }
            }
            return null;
        }

        private void SetStatus(string text, VoiceDialogueState next)
        {
            status = text;
            state = next;
            if (statusText) statusText.text = text;
            if (performance) performance.SetState(next);
        }

        private void CreateStatusText()
        {
            var display = new GameObject("语音状态提示_运行时");
            display.transform.SetParent(transform, false);
            display.transform.localPosition = new Vector3(0f, 2.27f, 0f);
            statusText = display.AddComponent<TextMesh>();
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.fontSize = 34;
            statusText.characterSize = .025f;
            statusText.color = new Color(.86f, .94f, 1f);
            var font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 34);
            if (font)
            {
                statusText.font = font;
                display.GetComponent<MeshRenderer>().material = font.material;
            }
            statusText.text = status;
        }

        private void OnDestroy()
        {
            DisposeTouchAutonomy();
            lifetime?.Cancel();
            socket?.Dispose();
            if (microphoneClip)
            {
                Microphone.End(microphoneName);
                Destroy(microphoneClip);
            }
            if (characterAudio) { characterAudio.Stop(); characterAudio.clip = null; }
            if (outputClip) Destroy(outputClip);
            lock (audioLock) audioQueue.Clear();
        }
    }
}


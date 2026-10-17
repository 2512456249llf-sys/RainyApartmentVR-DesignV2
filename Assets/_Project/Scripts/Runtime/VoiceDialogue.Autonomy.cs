using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace RainyApartmentVR
{
    public sealed partial class VoiceDialogue
    {
        [Tooltip("身体接触可以触发千问自主决策；模型允许保持安静")]
        public bool touchAutonomyEnabled = true;
        private enum TouchTurnPhase { None, Deciding, Completing, Speaking }
        private TouchTurnPhase touchPhase;
        private readonly TouchAutonomyQueue touchQueue = new TouchAutonomyQueue();
        private readonly Dictionary<string, string> touchOutputs = new Dictionary<string, string>();
        private readonly HashSet<string> touchCalls = new HashSet<string>();
        private TouchReplyPlan touchPlan;
        private string acceptedTouchCall;
        private TouchEvent currentTouch;
        private PerceptionEventHub perceptionHub;
        private int touchGeneration;
        private float touchStarted;
        internal Func<string, Task> TransportOverride;
        public bool IsAutonomousTurn => touchPhase != TouchTurnPhase.None;
        public int AutonomousRequestCount { get; private set; }
        [Serializable] private sealed class Request { public string type = "response.create"; public RequestBody response; }
        [Serializable] private sealed class RequestBody { public string instructions; public string[] modalities; }
        [Serializable] private sealed class TouchReceipt
        {
            public string eventType = "TouchEvent";
            public TouchEvent touch;
            public bool shouldSpeak;
            public string utterance;
            public string action;
            public bool actionApplied;
        }
        internal const string TouchTool = "{\"type\":\"function\",\"function\":{\"name\":\"respond_to_touch\",\"description\":\"仅用于真实身体触摸事件的自主回应。可以保持安静或只做小动作，不应调用走动、坐下等指令动作。\",\"parameters\":{\"type\":\"object\",\"properties\":{\"shouldSpeak\":{\"type\":\"boolean\"},\"utterance\":{\"type\":\"string\",\"description\":\"自行选择的中文口语回应，不说话时为空，最多140字\"},\"action\":{\"type\":\"string\",\"enum\":[\"none\",\"nod\",\"shake\",\"smile\",\"look_player\",\"look_touch\",\"tilt_head\",\"look_away\"]}},\"required\":[\"shouldSpeak\",\"utterance\",\"action\"]}}}";
        private void InitializeTouchAutonomy()
        {
            perceptionHub = GetComponent<PerceptionEventHub>();
            if (perceptionHub) perceptionHub.Published += OnPerception;
        }
        private void OnPerception(PerceptionEvent value)
        {
            if (touchAutonomyEnabled && value != null && value.eventType == "TouchEvent")
                touchQueue.Enqueue(value.touch, Time.unscaledTime);
        }
        private bool CanStartTouchTurn => ready && !connecting && !turnActive && !microphoneClip && !ButtonHeld() &&
            (TransportOverride != null || (socket != null && socket.State == System.Net.WebSockets.WebSocketState.Open)) && CharacterPoseGate.CanTouch(gameObject);
        private void TickTouchAutonomy()
        {
            if (IsAutonomousTurn && touchPhase != TouchTurnPhase.Speaking && !responseComplete && Time.unscaledTime - touchStarted > 15f)
                ForceResetTurn("触摸决策超时");
            if (!touchAutonomyEnabled) { touchQueue.Clear(); return; }
            var touch = touchQueue.Take(Time.unscaledTime, CanStartTouchTurn);
            if (touch == null) return;
            currentTouch = touch; touchPlan = null; acceptedTouchCall = null; touchOutputs.Clear(); touchCalls.Clear();
            touchPhase = TouchTurnPhase.Deciding; touchStarted = Time.unscaledTime; touchGeneration++; AutonomousRequestCount++;
            turnActive = true; responseComplete = playbackStarted = actionCalledThisTurn = fallbackFired = false;
            firstAudioTime = 0; lastAudioTime = lastPlayedSampleAt = lastActionAt = Time.realtimeSinceStartup;
            transcript = ""; pendingCalls.Clear(); callResults.Clear(); callActionIds.Clear();
            lock (audioLock) audioQueue.Clear();
            SetStatus("角色感知到了身体接触…", VoiceDialogueState.Thinking);
            Debug.Log("千问触摸文本交接：" + TouchAutonomyPolicy.Describe(touch));
            _ = Send(BuildTouchRequest(touch));
        }
        internal string BuildTouchRequest(TouchEvent touch)
        {
            string instruction = BuildInstruction() + "\n本次是身体感知轮次，覆盖普通语音轮次必须开口的规则。" +
                "玩家当前没有说话，结合当前会话、性格与身体接触自主判断。必须仅调用一次respond_to_touch，不能调用perform_action。" +
                "shouldSpeak=false是完全合法的结果；action=none也合法，不要为了触摸而强行回应，不要固定部位对应固定台词。" +
                "动作只允许工具所列的小动作；look_touch表示看向接触位置。不要直接输出普通文本或音频。\n" +
                TouchAutonomyPolicy.Describe(touch) + "\n结构化游戏事件：" + JsonUtility.ToJson(touch);
            return JsonUtility.ToJson(new Request { response = new RequestBody { instructions = instruction, modalities = new[] { "text" } } });
        }
        private void HandleTouchTool(Event message)
        {
            if (string.IsNullOrEmpty(message.call_id) || !touchCalls.Add(message.call_id)) return;
            pendingCalls.Add(message.call_id);
            touchOutputs[message.call_id] = "当前不是合法触摸决策，未执行身体动作。";
            if (touchPhase != TouchTurnPhase.Deciding || message.name != "respond_to_touch" || touchPlan != null) return;
            try
            {
                var plan = JsonUtility.FromJson<TouchReplyPlan>(message.arguments ?? "");
                if (!TouchAutonomyPolicy.IsValid(plan)) return;
                touchPlan = plan; acceptedTouchCall = message.call_id;
                touchOutputs[message.call_id] = "触摸自主决策已接收，等待本轮结束后按当前人物状态执行。";
            }
            catch (Exception) { Debug.LogWarning("触摸决策格式无效，保持安静。"); }
        }
        private bool CompleteTouchResponse()
        {
            if (!IsAutonomousTurn) return false;
            if (touchPhase == TouchTurnPhase.Completing) return true;
            var calls = pendingCalls.ToArray(); pendingCalls.Clear();
            if (touchPhase == TouchTurnPhase.Deciding)
            {
                touchPhase = TouchTurnPhase.Completing;
                _ = ApplyTouchDecision(calls, touchGeneration);
            }
            else
            {
                // 触摸说话轮次不允许重复排动作或无限追加工具回合。
                _ = FinishTouchToolOutputs(calls, touchGeneration);
            }
            return true;
        }
        private bool ExecuteTouchAction(string action)
        {
            if (action == "none") return true;
            var face = GetComponent<CharacterFaceTouch>();
            if (!CharacterPoseGate.CanTouch(gameObject) || (face && face.IsHolding) || !performance) return false;
            if (action == "look_touch") performance.LookAtTouch(currentTouch.contactPosition);
            else performance.EnqueueTouchAction(action);
            return true;
        }
        private async Task ApplyTouchDecision(string[] calls, int generation)
        {
            bool eligible = CharacterPoseGate.CanTouch(gameObject);
            bool speak = eligible && touchPlan != null && touchPlan.shouldSpeak;
            bool acted = eligible && touchPlan != null && ExecuteTouchAction(touchPlan.action);
            var receipt = new TouchReceipt
            {
                touch = currentTouch, shouldSpeak = speak, utterance = speak ? touchPlan.utterance : "",
                action = touchPlan != null ? touchPlan.action : "none", actionApplied = acted
            };
            string result = JsonUtility.ToJson(receipt);
            foreach (string id in calls)
            {
                if (generation != touchGeneration || !IsAutonomousTurn) return;
                await Send("{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"function_call_output\",\"call_id\":\"" + Escape(id) + "\",\"output\":\"" + Escape(id == acceptedTouchCall ? result : touchOutputs[id]) + "\"}}");
            }
            if (generation != touchGeneration || !IsAutonomousTurn) return;
            Debug.Log("千问触摸自主决策：" + result);
            actionCalledThisTurn = acted && receipt.action != "none";
            if (!speak)
            {
                responseComplete = true; lastPlayedSampleAt = Time.realtimeSinceStartup;
                SetStatus("角色已感知接触。", VoiceDialogueState.Idle);
                return;
            }
            touchPhase = TouchTurnPhase.Speaking;
            string instruction = "沿用雨夜公寓角色和当前对话。你刚刚自主决定回应这次身体触摸，身体小动作已经执行或因忙碌被跳过。" +
                "本轮不要再调用任何工具，使用自然中文说出你自己选择的这句回应，不附加说明：" + touchPlan.utterance;
            await Send(JsonUtility.ToJson(new Request { response = new RequestBody { instructions = instruction, modalities = new[] { "text", "audio" } } }));
        }
        private async Task FinishTouchToolOutputs(string[] calls, int generation)
        {
            foreach (string id in calls)
            {
                if (generation != touchGeneration || !IsAutonomousTurn) return;
                await Send("{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"function_call_output\",\"call_id\":\"" + Escape(id) + "\",\"output\":\"本轮触摸反馈不再执行额外指令动作。\"}}");
            }
            if (generation == touchGeneration) responseComplete = true;
        }
        private void ResetTouchTurn()
        {
            touchGeneration++; touchPhase = TouchTurnPhase.None; currentTouch = null; touchPlan = null; touchCalls.Clear(); touchOutputs.Clear();
        }
        private void DisposeTouchAutonomy()
        {
            if (perceptionHub) perceptionHub.Published -= OnPerception;
            touchQueue.Clear(); ResetTouchTurn();
        }
    }
}

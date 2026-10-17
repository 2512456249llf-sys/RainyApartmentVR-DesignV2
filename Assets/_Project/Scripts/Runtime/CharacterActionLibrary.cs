using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace RainyApartmentVR
{
    // 模型只挑选动作名称，骨骼和动画由本地验证过的清单执行。
    public sealed class CharacterActionLibrary : MonoBehaviour
    {
        public static readonly string[] ActionIds =
        {
            "nod", "shake", "smile", "look_away", "look_player", "tilt_head", "head_down",
            "wave", "hand_chest", "lean_in", "thinking", "relieved", "surprised",
            "shrug", "greeting", "laugh", "agree", "weight_shift", "thumbs_up",
            "talk_soft", "talk_animated",
            "nod_hard", "nod_yes", "nod_long", "nod_thoughtful", "shake_annoyed", "shake_thoughtful",
            "look_around", "look_around_2", "look_around_nervous", "look_away_gesture", "agree_soft",
            "talking", "talking_2", "point", "point_2", "point_gesture", "point_gesture_2",
            "point_forward", "point_angry", "clap", "clap_2", "clap_stand", "clap_stand_2",
            "bored", "excited", "bow_formal", "bow_informal", "sad", "sad_2",
            "breathing", "happy", "happy_2", "weight_shift_gesture",
            "approach_player", "walk_around", "sit", "crouch", "kneel_pray", "stand", "stop"
        };

        public const string PromptCatalog =
            "nod点头；shake摇头；smile微笑；look_away短暂移开目光；look_player看向玩家；" +
            "tilt_head轻微歪头；head_down稍微低头；wave挥手；hand_chest手扶胸前；lean_in轻微前倾；" +
            "thinking思考；relieved松口气；surprised惊讶；shrug耸肩；greeting招呼；laugh轻笑；" +
            "agree赞同；weight_shift换重心；thumbs_up竖拇指；talk_soft柔和说话手势；talk_animated活泼说话手势；" +
            "nod_hard用力点头；nod_yes点头答应；nod_long慢慢点头；nod_thoughtful若有所思地点头；" +
            "shake_annoyed不耐烦地摇头；shake_thoughtful若有所思地摇头；" +
            "look_around环顾四周；look_around_2环顾四周（另一版）；look_around_nervous紧张地张望；look_away_gesture移开视线；" +
            "agree_soft温和地认同；talking边说边比划；talking_2边说边比划（另一版）；" +
            "point指一下；point_2指一下（另一版）；point_gesture用手势示意；point_gesture_2用手势示意（另一版）；" +
            "point_forward指向前面；point_angry生气地指；clap鼓掌；clap_2鼓掌（另一版）；clap_stand站着鼓掌；clap_stand_2站着鼓掌（另一版）；" +
            "bored无聊；excited兴奋；bow_formal正式鞠躬；bow_informal随意点头致意；sad情绪低落；sad_2情绪低落（另一版）；" +
            "breathing深呼吸；happy开心；happy_2开心（另一版）；weight_shift_gesture换重心并带手势；" +
            "approach_player走近玩家；walk_around在原位附近随意走走；sit坐下；crouch屈膝蹲下；kneel_pray跪下膜拜或跪下祈祷；stand起立或站直；stop停步。";

        private static readonly string[] ClipIds =
        {
            "thinking", "relieved", "surprised", "shrug", "greeting", "laugh", "agree",
            "weight_shift", "thumbs_up", "talk_soft", "talk_animated",
            "nod_hard", "nod_yes", "nod_long", "nod_thoughtful", "shake_annoyed", "shake_thoughtful",
            "look_around", "look_around_2", "look_around_nervous", "look_away_gesture", "agree_soft",
            "talking", "talking_2", "point", "point_2", "point_gesture", "point_gesture_2",
            "point_forward", "point_angry", "clap", "clap_2", "clap_stand", "clap_stand_2",
            "bored", "excited", "bow_formal", "bow_informal", "sad", "sad_2",
            "breathing", "happy", "happy_2", "weight_shift_gesture"
        };

        // 常见动作交给本地在同义片段间轮换；明确带情绪的动作仍按模型指定的片段执行。
        private static readonly Dictionary<string, string[]> Variants = new Dictionary<string, string[]>
        {
            { "nod", new[] { "nod_yes", "nod_long", "nod_thoughtful", "nod" } },
            { "shake", new[] { "shake", "shake_thoughtful" } },
            { "smile", new[] { "smile", "happy", "happy_2" } },
            { "look_away", new[] { "look_away_gesture", "look_away" } },
            { "look_around", new[] { "look_around", "look_around_2" } },
            { "wave", new[] { "greeting", "wave" } },
            { "agree", new[] { "agree", "agree_soft" } },
            { "talk_soft", new[] { "talk_soft", "talking", "talking_2" } },
            { "talk_animated", new[] { "talk_animated", "talking_2" } },
            { "talking", new[] { "talking", "talking_2", "talk_soft" } },
            { "point", new[] { "point", "point_2", "point_gesture", "point_gesture_2", "point_forward" } },
            { "clap", new[] { "clap", "clap_2", "clap_stand", "clap_stand_2" } },
            { "happy", new[] { "happy", "happy_2" } },
            { "sad", new[] { "sad", "sad_2" } },
            { "weight_shift", new[] { "weight_shift_gesture", "weight_shift" } }
        };

        // 模型可能说出的近义叫法 -> 清单内动作 id（含中文，防止编造的 id 落空）
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "跪下", "kneel_pray" }, { "膜拜", "kneel_pray" }, { "kneel", "kneel_pray" }, { "praying", "kneel_pray" }, { "跪下膜拜", "kneel_pray" },
            { "squat", "crouch" }, { "squat_down", "crouch" }, { "crouch_down", "crouch" },
            { "sit_down", "sit" }, { "蹲下", "crouch" }, { "下蹲", "crouch" }, { "坐下", "sit" },
            { "stand_up", "stand" }, { "get_up", "stand" }, { "rise", "stand" },
            { "起来", "stand" }, { "站起来", "stand" }, { "起身", "stand" },
            { "come", "approach_player" }, { "come_here", "approach_player" }, { "come_over", "approach_player" },
            { "follow", "approach_player" }, { "approach", "approach_player" }, { "walk_to_player", "approach_player" },
            { "过来", "approach_player" }, { "走近", "approach_player" }, { "走过来", "approach_player" },
            { "walk", "walk_around" }, { "move", "walk_around" }, { "move_around", "walk_around" }, { "wander", "walk_around" },
            { "走走", "walk_around" }, { "走动", "walk_around" }, { "逛", "walk_around" },
            { "stop_moving", "stop" }, { "stay", "stop" }, { "halt", "stop" }, { "freeze", "stop" },
            { "停下", "stop" }, { "站住", "stop" }, { "别动", "stop" },
        };

        public Animator animator;
        public CharacterBehaviour behaviour;
        public CharacterVoicePerformance performance;
        private Coroutine playing;
        private readonly Queue<string> pendingClips = new Queue<string>();
        public bool IsPlayingClip => playing != null;
        private int layer = -1;
        private readonly Queue<string> recentActions = new Queue<string>();
        private readonly Dictionary<string, int> variantCursors = new Dictionary<string, int>();

        public string LastPerformedId { get; private set; }
        public string RecentActionSummary
        {
            get
            {
                var all = recentActions.ToArray();
                int begin = Math.Max(0, all.Length - 5);
                return all.Length == 0 ? "暂无" : string.Join("、", all, begin, all.Length - begin);
            }
        }

        public string PickSpeechFallback(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return null;
            bool busy = behaviour && (behaviour.IsWalking || behaviour.IsPostureLocked || behaviour.IsHandHeld);
            // 只避开最近 4 个（排除窗口过大会把候选排光，退化成固定几个动作死循环）；随机挑选打散固定顺序。
            var all = recentActions.ToArray();
            int begin = Math.Max(0, all.Length - 4);
            var recent = new List<string>();
            for (int i = begin; i < all.Length; i++) recent.Add(all[i]);
            var pool = new List<string>();
            foreach (var candidate in candidates)
            {
                if (recent.Contains(candidate)) continue;
                if (busy && Array.IndexOf(ClipIds, candidate) >= 0) continue;
                pool.Add(candidate);
            }
            if (pool.Count == 0)
                foreach (var candidate in candidates)
                {
                    if (busy && Array.IndexOf(ClipIds, candidate) >= 0) continue;
                    pool.Add(candidate);
                }
            if (pool.Count == 0) return busy ? "tilt_head" : candidates[UnityEngine.Random.Range(0, candidates.Length)];
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private string ResolveVariant(string id)
        {
            if (!Variants.TryGetValue(id, out var choices)) return id;
            // 正在走路、坐着或牵手时，保留可叠加的头部和面部动作。
            if (behaviour && (behaviour.IsWalking || behaviour.IsPostureLocked || behaviour.IsHandHeld) &&
                Array.IndexOf(ClipIds, id) < 0) return id;
            int start = variantCursors.TryGetValue(id, out int value) ? value : 0;
            int selected = start % choices.Length;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = (start + i) % choices.Length;
                if (recentActions.Contains(choices[index])) continue;
                selected = index;
                break;
            }
            variantCursors[id] = (selected + 1) % choices.Length;
            return choices[selected];
        }

        private void Remember(string id)
        {
            LastPerformedId = id;
            recentActions.Enqueue(id);
            while (recentActions.Count > 10) recentActions.Dequeue();
        }

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (!behaviour) behaviour = GetComponent<CharacterBehaviour>();
            if (!performance) performance = GetComponent<CharacterVoicePerformance>();
            if (animator)
            {
                layer = animator.GetLayerIndex("自然动作");
                // 空动作层不应覆盖基础待机姿势，否则上下半身会在腰部错位。
                if (layer >= 0) animator.SetLayerWeight(layer, 0f);
            }
        }

        public bool TryPerform(string requested)
        {
            string id = (requested ?? "").Trim().ToLowerInvariant();
            if (Aliases.TryGetValue(id, out var alias))
            {
                Debug.Log("动作别名映射：" + id + " -> " + alias);
                id = alias;
            }
            if (Array.IndexOf(ActionIds, id) < 0)
            {
                Debug.LogWarning("动作未执行：[" + id + "] 不在动作清单内。");
                return false;
            }
            string requestedBase = id;
            id = ResolveVariant(id);
            if (id != requestedBase) Debug.Log("动作变体：" + requestedBase + " -> " + id);
            switch (id)
            {
                case "approach_player": if (behaviour && behaviour.ApproachPlayer()) { Remember(id); return true; } return false;
                case "walk_around": if (behaviour && behaviour.WalkAround()) { Remember(id); return true; } return false;
                case "kneel_pray": var prayer = GetComponent<CharacterPrayer>(); if (prayer && prayer.Begin()) { Remember(id); return true; } return false;
                case "sit": if (behaviour && !behaviour.IsHandHeld && !behaviour.IsSitting) { behaviour.Sit(); Remember(id); return true; } return false;
                case "crouch": if (behaviour && behaviour.Crouch()) { Remember(id); return true; } return false;
                case "stand": if (behaviour && behaviour.IsPostureLocked) { behaviour.Stand(); Remember(id); return true; } return false;
                case "stop": if (behaviour) { behaviour.Stop(); Remember(id); return true; } return false;
            }
            if (Array.IndexOf(ClipIds, id) >= 0)
            {
                if (behaviour && behaviour.IsHandHeld) return false;
                if (!animator) { Debug.LogWarning("动作未执行：" + id + "，Animator 未接线。"); return false; }
                if (layer < 0) { Debug.LogWarning("动作未执行：" + id + "，动画控制器缺少[自然动作]层（GetLayerIndex=-1）。"); return false; }
                if (behaviour && behaviour.IsPostureLocked) { Debug.Log("动作暂缓：" + id + "，角色正坐着或蹲着。"); return false; }
                if (behaviour && behaviour.IsWalking) { Debug.Log("动作暂缓：" + id + "，角色正在走位。"); return false; }
                if (!animator.HasState(layer, Animator.StringToHash("自然动作.Action_" + id)))
                {
                    Debug.LogWarning("动作未执行：" + id + "，[自然动作]层缺少状态 Action_" + id + "。");
                    return false;
                }
                // 连续工具调用不再把前一段动作截断，最多暂存两段，避免排成长队。
                if (pendingClips.Count >= 2) return false;
                pendingClips.Enqueue(id);
                if (playing == null) playing = StartCoroutine(PlayPendingClips());
                Remember(id);
                return true;
            }
            if (!performance) { Debug.LogWarning("动作未执行：" + id + "，表演组件未接线。"); return false; }
            if (!performance.enabled) { Debug.LogWarning("动作未执行：" + id + "，表演组件已自禁用（人形骨架或 VRM 无效）。"); return false; }
            performance.EnqueueActions(new[] { id });
            Remember(id);
            return true;
        }

        private IEnumerator PlayPendingClips()
        {
            while (pendingClips.Count > 0)
            {
                yield return PlayClip(pendingClips.Dequeue());
            }
            playing = null;
        }

        private IEnumerator PlayClip(string id)
        {
            string state = "自然动作.Action_" + id;
            int stateHash = Animator.StringToHash(state);
            animator.CrossFadeInFixedTime(state, .12f, layer, 0f);
            float enterDeadline = Time.time + 1f;
            while (animator && Time.time < enterDeadline &&
                   animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != stateHash)
                yield return null;
            if (!animator || animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != stateHash)
            {
                Debug.LogWarning("人物动作未进入动画状态：" + id);
                if (animator) animator.SetLayerWeight(layer, 0f);
                yield break;
            }

            float fade = animator.GetLayerWeight(layer);
            while (animator && fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.deltaTime / .16f);
                animator.SetLayerWeight(layer, fade);
                yield return null;
            }
            float finishDeadline = Time.time + 15f;
            while (animator && Time.time < finishDeadline)
            {
                var current = animator.GetCurrentAnimatorStateInfo(layer);
                if (behaviour && (behaviour.IsWalking || behaviour.IsPostureLocked || behaviour.IsHandHeld))
                {
                    pendingClips.Clear();
                    break;
                }
                if (current.fullPathHash != stateHash || current.normalizedTime >= 1f) break;
                yield return null;
            }
            fade = animator ? animator.GetLayerWeight(layer) : 0f;
            while (animator && fade > 0f)
            {
                fade = Mathf.Max(0f, fade - Time.deltaTime / .2f);
                animator.SetLayerWeight(layer, fade);
                yield return null;
            }
            if (animator) animator.CrossFadeInFixedTime("自然动作.OverlayEmpty", 0f, layer);
        }

        private void OnDisable()
        {
            if (playing != null) StopCoroutine(playing);
            playing = null;
            pendingClips.Clear();
            if (animator && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}


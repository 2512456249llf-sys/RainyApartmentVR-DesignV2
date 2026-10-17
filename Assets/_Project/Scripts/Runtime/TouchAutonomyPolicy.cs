using System;
using System.Globalization;
using UnityEngine;
namespace RainyApartmentVR
{
    [Serializable] public sealed class TouchReplyPlan
    {
        public bool shouldSpeak;
        public string utterance;
        public string action;
    }
    // 接触描述不伪装成玩家发言，也不把物理距离当成压力。
    public static class TouchAutonomyPolicy
    {
        public static readonly string[] SafeActions = { "none", "nod", "shake", "smile", "look_player", "look_touch", "tilt_head", "look_away" };
        public static bool IsValid(TouchReplyPlan plan)
        {
            return plan != null && Array.IndexOf(SafeActions, plan.action) >= 0 &&
                (!plan.shouldSpeak || (!string.IsNullOrWhiteSpace(plan.utterance) && plan.utterance.Length <= 140));
        }
        public static string Describe(TouchEvent touch)
        {
            string part;
            switch (touch.bodyPart)
            {
                case "Hand": part = "手部"; break;
                case "Arm": part = "手臂"; break;
                case "Shoulder": part = "肩膀"; break;
                case "Head": part = "头部或头发"; break;
                case "Torso": part = "躯干"; break;
                default: part = "身体"; break;
            }
            string contact;
            switch (touch.contactType)
            {
                case "BriefContact": contact = "短暂碰触"; break;
                case "Stroke": contact = "抚摸"; break;
                case "SustainedContact": contact = "保持持续接触"; break;
                default: contact = "触摸"; break;
            }
            return "真实身体感知：玩家用" + (touch.playerHand == "Left" ? "左手" : "右手") + contact + "了你的" + part +
                "，持续" + Mathf.Max(0, touch.duration).ToString("0.00", CultureInfo.InvariantCulture) + "秒。" +
                (touch.intensityReliable ? "接触强度归一化值为" + touch.intensity.ToString("0.00", CultureInfo.InvariantCulture) + "。" : "接触强度未知。") +
                "这是游戏检测到的身体行为，不是玩家说的话。";
        }
    }
    // 只保留最新事件；语音忙碌时合并等待，过期事件不追着补回应。
    public sealed class TouchAutonomyQueue
    {
        public float cooldown = 8f, maximumAge = 8f;
        private TouchEvent pending;
        private float receivedAt, nextAllowed;
        public bool HasPending => pending != null;
        public void Enqueue(TouchEvent touch, float now)
        {
            if (touch == null || touch.duration < 0 || float.IsNaN(touch.duration) || float.IsInfinity(touch.duration) ||
                touch.bodyPart == "None" || string.IsNullOrEmpty(touch.bodyPart)) return;
            pending = touch; receivedAt = now;
        }
        public TouchEvent Take(float now, bool idle)
        {
            if (pending == null) return null;
            if (now - receivedAt > maximumAge) { pending = null; return null; }
            if (!idle || now < nextAllowed) return null;
            var result = pending; pending = null; nextAllowed = now + cooldown; return result;
        }
        public void Clear() { pending = null; }
    }
}

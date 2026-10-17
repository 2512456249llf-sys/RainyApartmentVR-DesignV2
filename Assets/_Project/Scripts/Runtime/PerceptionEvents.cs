using System;
using UnityEngine;
namespace RainyApartmentVR
{
    public enum TouchBodyPart { None, Hand, Arm, Shoulder, Head, Torso }
    public enum TouchContactType { BriefContact, Touch, Stroke, SustainedContact }
    [Serializable] public sealed class TouchEvent
    {
        public string bodyPart;
        public string contactType;
        public float duration;
        public bool intensityReliable;
        public float intensity;
        public string playerHand;
        public string timestamp;
        public Vector3 contactPosition;
    }
    [Serializable] public sealed class MediaEvent
    {
        public string mediaType, title, episode, state;
        public double playbackTime;
    }
    [Serializable] public sealed class PerceptionEvent
    {
        public string eventType;
        public TouchEvent touch;
        public MediaEvent media;
    }
    // 合并连续样本：接触确认、离开防抖、部位切换防抖、冷却和持续接触节流。
    public sealed class TouchContactAccumulator
    {
        public float debounce = .12f, releaseDelay = .18f, cooldown = 2f, sustainedInterval = 5f;
        private TouchBodyPart current, candidate;
        private float candidateSince, started, lastContact, nextAllowed, lastReport;
        private float travel;
        private bool reported;
        private Vector3 position;
        public event Action<TouchEvent> Emitted;
        public void Sample(TouchBodyPart part, Vector3 point, Vector3 tangentMovement, float now, bool left)
        {
            if (part == TouchBodyPart.None)
            {
                candidate = TouchBodyPart.None;
                if (current != TouchBodyPart.None && now - lastContact >= releaseDelay) Finish(lastContact, now, left);
                return;
            }
            if (part != candidate) { candidate = part; candidateSince = now; }
            if (current != TouchBodyPart.None && part != current)
            {
                if (now - candidateSince < debounce) { lastContact = now; return; }
                Finish(lastContact, now, left);
            }
            if (current == TouchBodyPart.None)
            {
                if (now - candidateSince < debounce) return;
                current = part; started = candidateSince; lastReport = started; reported = false; travel = 0;
            }
            position = point; lastContact = now; travel += tangentMovement.magnitude;
            float duration = now - started;
            if (duration >= 1f && !reported && now >= nextAllowed) Emit(duration, now, left);
            else if (reported && now - lastReport >= sustainedInterval && now >= nextAllowed) Emit(duration, now, left);
        }
        private void Finish(float ended, float now, bool left)
        {
            if (!reported && ended - started >= debounce && now >= nextAllowed) Emit(ended - started, now, left);
            current = TouchBodyPart.None; travel = 0; reported = false;
        }
        private void Emit(float duration, float now, bool left)
        {
            var type = duration < .5f ? TouchContactType.BriefContact : travel >= .06f ? TouchContactType.Stroke :
                duration >= 2f ? TouchContactType.SustainedContact : TouchContactType.Touch;
            var result = new TouchEvent
            {
                bodyPart = current.ToString(), contactType = type.ToString(), duration = Mathf.Max(0, duration),
                playerHand = left ? "Left" : "Right", timestamp = DateTimeOffset.UtcNow.ToString("O"),
                contactPosition = position, intensityReliable = false
            };
            reported = true; lastReport = now; nextAllowed = now + cooldown; travel = 0;
            Emitted?.Invoke(result);
        }
        public void Reset() { current = candidate = TouchBodyPart.None; travel = 0; reported = false; }
    }
}


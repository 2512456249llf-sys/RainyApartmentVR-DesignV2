using System;
using UnityEngine;
namespace RainyApartmentVR {
    // 第一阶段只发布本地结构化感知，后续模型入口订阅同一事件。
    public sealed class PerceptionEventHub : MonoBehaviour
    {
        public bool logEvents = true;
        public event Action<PerceptionEvent> Published;
        public int PublishedCount { get; private set; }
        // 媒体状态沿用同一感知总线；本阶段只发布，不发起模型请求。
        public void Publish(MediaEvent media)
        {
            PublishedCount++;
            Published?.Invoke(new PerceptionEvent { eventType = "MediaEvent", media = media });
        }
        public void Publish(TouchEvent touch)
        {
            var perception = new PerceptionEvent { eventType = "TouchEvent", touch = touch };
            PublishedCount++;
            if (logEvents) Debug.Log("角色触摸感知：" + JsonUtility.ToJson(perception));
            Published?.Invoke(perception);
        }
    }
}

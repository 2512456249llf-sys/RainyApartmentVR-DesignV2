using System.Text;
using UnityEngine;

namespace RainyApartmentVR
{
    // VR 卡顿记录仪：帧时间超时就写日志，并定期汇报平均帧率与最差帧。
    public sealed class PerformanceWatchdog : MonoBehaviour
    {
        public float warnThreshold = 0.018f;
        public float summaryEvery = 10f;
        public bool showOnScreen = true;

        private TextMesh display;
        private float windowStart;
        private int frames;
        private float worst;
        private float lastWarn = -10f;
        private float nextSummary;

        private void Awake()
        {
            windowStart = Time.realtimeSinceStartup;
            nextSummary = summaryEvery;
            if (!showOnScreen) return;
            var holder = new GameObject("性能显示_运行时");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, 2.1f, 0.75f);
            display = holder.AddComponent<TextMesh>();
            display.anchor = TextAnchor.MiddleCenter;
            display.alignment = TextAlignment.Center;
            display.fontSize = 40;
            display.characterSize = .022f;
            display.color = new Color(.8f, 1f, .8f, .95f);
            var font = Font.CreateDynamicFontFromOSFont("Consolas", 40);
            if (font)
            {
                display.font = font;
                holder.GetComponent<MeshRenderer>().material = font.material;
            }
        }

        private void Update()
        {
            frames++;
            float dt = Time.unscaledDeltaTime;
            if (dt > worst) worst = dt;
            if (dt >= warnThreshold && Time.realtimeSinceStartup - lastWarn > 1f)
            {
                lastWarn = Time.realtimeSinceStartup;
                Debug.LogWarning(string.Format("卡顿帧：{0:0.0} ms（目标 ≤ {1:0.0} ms），帧号 {2}，VR={3}",
                    dt * 1000f, warnThreshold * 1000f, Time.frameCount, UnityEngine.XR.XRSettings.isDeviceActive));
            }

            float elapsed = Time.realtimeSinceStartup - windowStart;
            if (elapsed >= summaryEvery)
            {
                float fps = frames / elapsed;
                Debug.Log(string.Format("性能汇总：平均 {0:0.0} fps，最差一帧 {1:0.0} ms（{2} 帧/{3:0}s）",
                    fps, worst * 1000f, frames, elapsed.ToString("0")));
                windowStart = Time.realtimeSinceStartup;
                frames = 0;
                worst = 0f;
            }
            if (display)
            {
                display.text = string.Format("{0:0.0} fps\n最差 {1:0.0} ms", frames / Mathf.Max(.001f, elapsed), worst * 1000f);
            }
        }
    }
}

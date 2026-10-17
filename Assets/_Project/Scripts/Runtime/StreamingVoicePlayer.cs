using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RainyApartmentVR
{
    // 把 GPT-SoVITS 的流式返回（WAV 头 + 16bit PCM）边收边播：
    // 收到约 0.2 秒的音频就先开始播放，后面边下边补进音频缓冲。
    // 模型常驻时第一段声音约 2 秒就能开口，且与句子长短无关。
    public static class StreamingVoicePlayer
    {
        private const float CapacitySeconds = 60f;
        private const float PreRollSeconds = 0.2f;

        public static IEnumerator Play(
            string apiUrl,
            string requestJson,
            AudioSource source,
            Action<bool> completed,
            Action onFirstAudio)
        {
            if (source == null || string.IsNullOrWhiteSpace(apiUrl))
            {
                completed(false);
                yield break;
            }

            string url = apiUrl.TrimEnd('/') + "/tts";
            var handler = new WavStreamHandler();
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
            request.downloadHandler = handler;
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 120;

            AudioClip clip = null;
            var buffer = new float[8192];
            int written = 0;
            bool started = false;
            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request.responseCode == 200)
                        Pump(handler, ref clip, buffer, ref written, ref started, source, onFirstAudio);
                    yield return null;
                }
                handler.Finished = true;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("流式配音请求失败：" + request.responseCode + " " + request.error);
                    if (!started && handler.Received <= 0)
                    {
                        completed(false);
                        yield break;
                    }
                }
                else
                {
                    Pump(handler, ref clip, buffer, ref written, ref started, source, onFirstAudio);
                }

                if (!started)
                {
                    if (written > 0 && clip != null)
                    {
                        source.clip = clip;
                        source.loop = false;
                        source.Play();
                        started = true;
                        onFirstAudio?.Invoke();
                    }
                    else
                    {
                        completed(false);
                        yield break;
                    }
                }

                // 等播放头追到已写入的位置（播完）。
                float deadline = Time.realtimeSinceStartup + 180f;
                while (source.timeSamples < written - 16 && Time.realtimeSinceStartup < deadline)
                    yield return null;

                yield return new WaitForSecondsRealtime(0.1f);
                if (source.isPlaying) source.Stop();
                completed(true);
            }
            finally
            {
                request.Dispose();
                if (clip != null) UnityEngine.Object.Destroy(clip);
                if (source != null) source.clip = null;
            }
        }

        private static void Pump(
            WavStreamHandler handler,
            ref AudioClip clip,
            float[] buffer,
            ref int written,
            ref bool started,
            AudioSource source,
            Action onFirstAudio)
        {
            if (!handler.HeaderReady) return;
            if (clip == null)
            {
                int capacity = Mathf.Max(handler.SampleRate, Mathf.CeilToInt(handler.SampleRate * CapacitySeconds));
                clip = AudioClip.Create("gpt_sovits_stream", capacity, 1, handler.SampleRate, false);
            }

            while (true)
            {
                int drained = handler.Drain(buffer);
                if (drained <= 0) break;
                int count = Mathf.Min(drained, clip.samples - written);
                if (count <= 0) break;
                if (count == buffer.Length)
                {
                    clip.SetData(buffer, written);
                }
                else
                {
                    var slice = new float[count];
                    Array.Copy(buffer, slice, count);
                    clip.SetData(slice, written);
                }
                written += count;
                if (drained < buffer.Length || count < drained) break;
            }

            if (!started && written >= Mathf.Max(1600, Mathf.RoundToInt(handler.SampleRate * PreRollSeconds)))
            {
                source.clip = clip;
                source.loop = false;
                source.Play();
                started = true;
                onFirstAudio?.Invoke();
            }
        }

        // 解析 WAV 头后，把 16bit 小端 PCM 转成 float 样本放进队列。
        private sealed class WavStreamHandler : DownloadHandlerScript
        {
            private readonly object gate = new object();
            private readonly Queue<float> pending = new Queue<float>();
            private readonly byte[] header = new byte[44];
            private int headerCount;
            private int carry = -1;

            public int SampleRate { get; private set; } = 32000;
            public int Channels { get; private set; } = 1;
            public bool HeaderReady { get; private set; }
            public bool Finished { get; set; }
            public long Received { get; private set; }

            public WavStreamHandler() : base(new byte[16384]) { }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0) return true;
                int index = 0;
                if (!HeaderReady)
                {
                    while (index < dataLength && headerCount < 44) header[headerCount++] = data[index++];
                    if (headerCount < 44) return true;
                    Channels = BitConverter.ToInt16(header, 22);
                    SampleRate = BitConverter.ToInt32(header, 24);
                    if (Channels < 1) Channels = 1;
                    if (SampleRate < 8000 || SampleRate > 192000) SampleRate = 32000;
                    HeaderReady = true;
                }

                lock (gate)
                {
                    if (carry >= 0 && index < dataLength)
                    {
                        short merged = (short)(carry | (data[index] << 8));
                        pending.Enqueue(merged / 32768f);
                        Received++;
                        index += 1;
                        carry = -1;
                    }
                    for (; index + 1 < dataLength; index += 2)
                    {
                        short value = (short)(data[index] | (data[index + 1] << 8));
                        pending.Enqueue(value / 32768f);
                        Received++;
                    }
                    if (index < dataLength) carry = data[index];
                }
                return true;
            }

            public int Drain(float[] into)
            {
                lock (gate)
                {
                    int count = 0;
                    while (count < into.Length && pending.Count > 0) into[count++] = pending.Dequeue();
                    return count;
                }
            }
        }
    }
}

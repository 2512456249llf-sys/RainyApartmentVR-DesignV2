using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;
namespace RainyApartmentVR.Tests
{
    public sealed class TouchStageOneTests
    {
        private string save;
        [UnityTest] public IEnumerator 方巾真实抓取后擦除且手部占用识别()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var towel = GameObject.Find("方巾_擦除口红"); var grab = towel.GetComponent<XRGrabInteractable>();
            var controller = new GameObject("方巾抓取测试控制器"); controller.transform.position = towel.transform.position;
            var trigger = controller.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = .05f;
            var interactor = controller.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = Object.FindFirstObjectByType<XRInteractionManager>();
            var handNode = new GameObject("方巾测试手"); handNode.transform.SetParent(controller.transform, false);
            var hand = handNode.AddComponent<VRHandPose>(); yield return new WaitForSeconds(.15f);
            interactor.StartManualInteraction((IXRSelectInteractable)grab); yield return new WaitForSeconds(.2f);
            Assert.IsTrue(grab.isSelected); Assert.IsTrue(hand.IsHoldingObject);
            Vector3 destination = towel.transform.position + Vector3.up * .3f;
            var board = new GameObject("方巾实际擦拭测试板"); var surface = board.AddComponent<DrawingSurface>(); surface.stableId = "方巾抓取擦拭板";
            var memory = LipstickDrawingMemory.Instance; var stroke = memory.BeginStroke(surface);
            memory.AddPoint(stroke, surface, destination); memory.AddPoint(stroke, surface, destination + Vector3.right * .015f);
            controller.transform.position += Vector3.up * .3f; yield return new WaitForSeconds(.4f);
            Assert.AreEqual(0, memory.StrokeCount, "方巾抓取后接触笔迹没有实际擦除");
            interactor.EndManualInteraction(); yield return null; Assert.IsFalse(hand.IsHoldingObject);
            Object.Destroy(controller); Object.Destroy(board);
        }
        private static void Capture(Vector3 target, Vector3 front, string name)
        {
            var node = new GameObject("身体绘画验收相机"); var camera = node.AddComponent<Camera>();
            camera.fieldOfView = 42; camera.nearClipPlane = .01f;
            node.transform.position = target + front * .7f; node.transform.LookAt(target);
            var texture = new Texture2D(640,640,TextureFormat.RGB24,false); var rt = new RenderTexture(640,640,24);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,640,640),0,0); texture.Apply();
            Directory.CreateDirectory("BuildLogs"); File.WriteAllBytes("BuildLogs/" + name + ".png",texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.Destroy(node); Object.Destroy(rt); Object.Destroy(texture);
        }
        [SetUp] public void Setup()
        {
            save = Path.GetFullPath("TestResults/触摸方巾隔离存档_" + Guid.NewGuid().ToString("N") + "/公寓摆放记忆.json");
            ScenePlacementMemory.StoragePathOverride = save;
        }
        [TearDown] public void Teardown() { ScenePlacementMemory.StoragePathOverride = null; }
        [Test] public void 接触防抖分类冷却与持续接触节流()
        {
            var events = new List<TouchEvent>(); var accumulator = new TouchContactAccumulator(); accumulator.Emitted += events.Add;
            void Feed(TouchBodyPart part, float time, float slide = 0) => accumulator.Sample(part, Vector3.one, Vector3.right * slide, time, true);
            Feed(TouchBodyPart.Hand, 0); Feed(TouchBodyPart.None, .03f); Assert.IsEmpty(events, "瞬间噪声不能成为触摸事件");
            Feed(TouchBodyPart.Hand, .2f); Feed(TouchBodyPart.Hand, .34f); Feed(TouchBodyPart.Hand, .45f); Feed(TouchBodyPart.None, .66f);
            Assert.AreEqual(1, events.Count); Assert.AreEqual("BriefContact", events[0].contactType); Assert.AreEqual("Left", events[0].playerHand);
            Assert.IsFalse(events[0].intensityReliable, "手柄距离不能伪装成真实压力"); Assert.IsNotEmpty(events[0].timestamp);
            Feed(TouchBodyPart.Hand, .7f); Feed(TouchBodyPart.Hand, .85f); Feed(TouchBodyPart.None, 1.1f); Assert.AreEqual(1, events.Count, "冷却期间重复接触不能刷事件");
            Feed(TouchBodyPart.Arm, 3); for (float t = 3.15f; t < 4.3f; t += .05f) Feed(TouchBodyPart.Arm, t, .005f);
            Assert.AreEqual("Stroke", events.Last().contactType);
            Feed(TouchBodyPart.None, 4.6f); Feed(TouchBodyPart.Torso, 7);
            for (float t = 7.15f; t <= 15; t += .05f) Feed(TouchBodyPart.Torso, t);
            Assert.AreEqual("SustainedContact", events.Last().contactType);
            Assert.LessOrEqual(events.Count, 4, "大量采样必须合并为少量事件");
        }
        [UnityTest] public IEnumerator 五类部位覆盖且跪拜期间触摸不抢骨骼()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return new WaitForSeconds(.5f);
            var surfaces = Object.FindFirstObjectByType<CharacterTouchSurfaces>();
            foreach (var part in new[] { TouchBodyPart.Hand, TouchBodyPart.Arm, TouchBodyPart.Shoulder, TouchBodyPart.Head, TouchBodyPart.Torso })
                Assert.IsTrue(surfaces.Surfaces.Any(c => surfaces.BodyPart(c) == part), "缺少触摸部位：" + part);
            var eventHub = surfaces.GetComponent<PerceptionEventHub>();
            var eventSource = surfaces.GetComponent<CharacterTouchPerception>();
            var received = new List<PerceptionEvent>(); eventHub.Published += received.Add;
            float sampleTime = 1;
            foreach (var part in new[] { TouchBodyPart.Hand, TouchBodyPart.Arm, TouchBodyPart.Shoulder, TouchBodyPart.Head, TouchBodyPart.Torso })
            {
                var contact = surfaces.Surfaces.First(c => surfaces.BodyPart(c) == part);
                eventSource.Report(true, contact, contact.transform.position, Vector3.zero, Vector3.up, sampleTime);
                eventSource.Report(true, contact, contact.transform.position, Vector3.zero, Vector3.up, sampleTime + .2f);
                eventSource.Report(true, contact, contact.transform.position, Vector3.zero, Vector3.up, sampleTime + .35f);
                eventSource.Report(true, null, Vector3.zero, Vector3.zero, Vector3.up, sampleTime + .6f);
                sampleTime += 3;
            }
            Assert.AreEqual(5, received.Count, "五类真实触摸壳必须通过统一事件入口发布");
            Assert.IsTrue(received.All(e => e.eventType == "TouchEvent"));
            Assert.AreEqual(5, received.Select(e => e.touch.bodyPart).Distinct().Count());
            var prayer = surfaces.GetComponent<CharacterPrayer>(); var face = surfaces.GetComponent<CharacterFaceTouch>();
            Assert.IsTrue(CharacterPoseGate.CanTouch(surfaces.gameObject)); Assert.IsTrue(prayer.Begin()); yield return new WaitForSeconds(.3f);
            Assert.IsFalse(CharacterPoseGate.CanTouch(surfaces.gameObject));
            var hand = new GameObject("跪拜触摸测试手").transform; hand.position = face.FaceCenter;
            Assert.IsFalse(face.BeginHold(hand));
            var hub = surfaces.GetComponent<PerceptionEventHub>(); int baseline = hub.PublishedCount;
            var perception = surfaces.GetComponent<CharacterTouchPerception>(); var surface = surfaces.Surfaces.First();
            for (int i = 0; i < 30; i++) perception.Report(true, surface, surface.transform.position, Vector3.right * .01f, Vector3.up, i * .1f);
            Assert.AreEqual(baseline, hub.PublishedCount); Object.Destroy(hand.gameObject);
        }
        [UnityTest] public IEnumerator 身体实际网格绘画随动作附着并由方巾局部擦除且重载不复现()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var painter = Object.FindFirstObjectByType<LipstickPainter>(); var memory = LipstickDrawingMemory.Instance;
            var character = Object.FindFirstObjectByType<CharacterBehaviour>();
            character.GetComponent<CharacterVoicePerformance>().enabled = false;
            var a = character.animator; a.speed = 0; a.enabled = false;
            var chest = a.GetBoneTransform(HumanBodyBones.Chest); var face = character.GetComponent<CharacterFaceTouch>();
            Vector3 front = face.FaceForward, right = Vector3.Cross(Vector3.up, front).normalized;
            SkinnedPaintSurface surface = null; SkinnedPaintSurface.Binding first = null; Vector3 firstPoint = Vector3.zero;
            foreach (var skin in Object.FindObjectsByType<SkinnedPaintSurface>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < 25; i++)
                {
                    Vector3 target = chest.position + right * (i - 12) * .003f;
                    if (!skin.Raycast(new Ray(target + front * .7f, -front), 1f, out var point, out var normal, out var binding)) continue;
                    if (surface == null) { surface = skin; first = binding; firstPoint = point + normal * .0018f; }
                    if (surface == skin) Assert.IsTrue(painter.DrawAt(skin.Surface, point + normal * .0018f, binding));
                }
                if (surface) break;
            }
            Assert.IsNotNull(surface, "不能命中躯干实际衣物网格"); painter.EndDrawing();
            Assert.AreEqual(1, memory.StrokeCount);
            yield return null; Capture(chest.position, front, "身体口红笔迹实际效果");
            var line = memory.GetComponentInChildren<LineRenderer>(); Assert.Greater(line.positionCount, 0);
            var probe = character.gameObject.AddComponent<SkinPaintProbe>(); probe.surface = surface; probe.binding = first; probe.line = line;
            chest.localRotation *= Quaternion.Euler(12, 8, 0); yield return new WaitForSeconds(.2f);
            Assert.Less(probe.error, .002f, "身体笔迹没有跟随皮肤变形");
            surface.TryPoint(first, out var moved); Assert.Greater(Vector3.Distance(moved, firstPoint), .005f);
            var towel = GameObject.Find("方巾_擦除口红"); Assert.IsNotNull(towel); Assert.IsNotNull(towel.GetComponent<LipstickEraser>());
            Assert.Greater(memory.EraseNear(moved, .014f), 0);
            memory.SaveNow(); Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(save), "口红笔迹.json")));
            int remaining = memory.StrokeCount;
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            Assert.AreEqual(remaining, LipstickDrawingMemory.Instance.StrokeCount, "擦除后重新进入不应恢复旧笔迹");
        }
        [UnityTest] public IEnumerator 擦掉中间笔迹保留两端且不跨空白连接()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var memory = LipstickDrawingMemory.Instance;
            var anchor = new GameObject("局部擦拭测试板"); var surface = anchor.AddComponent<DrawingSurface>(); surface.stableId = "测试擦拭板";
            var stroke = memory.BeginStroke(surface);
            for (int i = 0; i <= 40; i++) Assert.IsTrue(memory.AddPoint(stroke, surface, new Vector3(i * .005f, 0, 0)));
            Assert.Greater(memory.EraseNear(new Vector3(.1f, 0, 0), .025f), 0); yield return null;
            Assert.AreEqual(2, memory.StrokeCount);
            foreach (var line in memory.GetComponentsInChildren<LineRenderer>())
                for (int i = 1; i < line.positionCount; i++) Assert.Less(Vector3.Distance(line.GetPosition(i - 1), line.GetPosition(i)), .011f);
            Object.Destroy(anchor);
        }
    }
    [DefaultExecutionOrder(14000)] public sealed class SkinPaintProbe : MonoBehaviour
    {
        public SkinnedPaintSurface surface; public SkinnedPaintSurface.Binding binding; public LineRenderer line; public float error = 99;
        private void LateUpdate() { if (surface && line && line.positionCount > 0 && surface.TryPoint(binding, out var p)) error = Vector3.Distance(p, line.GetPosition(0)); }
    }
}





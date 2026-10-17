using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Tests
{
    public sealed class ContinuationRepairTests
    {
        [UnityTest]
        public IEnumerator 十三相框保留且照片正面受光()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var frames = Object.FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("相框_")).ToArray();
            Assert.AreEqual(13, frames.Length);
            var original = GameObject.Find("相框_可拿取");
            Assert.IsNotNull(original);
            Assert.That(original.transform.position.x, Is.EqualTo(-.70f).Within(.005f));
            Assert.That(original.transform.position.z, Is.EqualTo(.20f).Within(.005f));
            var renderer = GameObject.Find("相框_01").GetComponentInChildren<MeshRenderer>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) r.enabled = r == renderer;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0;
            RenderSettings.reflectionIntensity = 0;
            var cameraNode = new GameObject("照片受光验收相机");
            var camera = cameraNode.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 35;
            camera.nearClipPlane = .01f;
            cameraNode.transform.position = renderer.bounds.center + Vector3.back * .45f;
            cameraNode.transform.LookAt(renderer.bounds.center);
            var lightNode = new GameObject("照片单独受光验收");
            var testLight = lightNode.AddComponent<Light>();
            testLight.type = LightType.Directional;
            testLight.color = Color.white;
            testLight.intensity = 1;
            lightNode.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            float front = Capture(camera, "照片正面受光");
            lightNode.transform.rotation = Quaternion.LookRotation(Vector3.back);
            float back = Capture(camera, "照片背面受光");
            Assert.Greater(front, .06f, "正面受光仍然没有可见照片");
            Assert.Greater(front, back * 1.6f, "照片仍然只能依赖背面光源");
            Debug.Log("照片受光验收：正面亮度=" + front + " 背面亮度=" + back);
            Object.Destroy(cameraNode); Object.Destroy(lightNode);
        }

        [UnityTest]
        public IEnumerator 摆放与口红笔迹重新加载后恢复且附着物品()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null; yield return null;
            string directory = Path.GetFullPath("TestResults/摆放记忆验证_" + Guid.NewGuid().ToString("N"));
            ScenePlacementMemory.StoragePathOverride = Path.Combine(directory, "公寓摆放记忆.json");
            try
            {
                var frame = GameObject.Find("相框_01");
                var sofa = Object.FindFirstObjectByType<MovableFurniture>();
                frame.transform.position += new Vector3(.25f, .3f, -.1f);
                frame.transform.rotation *= Quaternion.Euler(12, 28, 0);
                sofa.transform.position += Vector3.up * .4f;
                sofa.GetComponent<Rigidbody>().position = sofa.transform.position;
                Vector3 expectedFrame = frame.transform.position, expectedSofa = sofa.transform.position;
                Quaternion expectedRotation = frame.transform.rotation;
                var surface = sofa.GetComponentInChildren<DrawingSurface>();
                Assert.IsNotNull(surface);
                var painter = Object.FindFirstObjectByType<LipstickPainter>();
                Vector3 local = new Vector3(.01f, .03f, .02f);
                Assert.IsTrue(painter.DrawAt(surface, surface.transform.TransformPoint(local)));
                Assert.IsTrue(painter.DrawAt(surface, surface.transform.TransformPoint(local + Vector3.right * .015f)));
                painter.EndDrawing();
                ScenePlacementMemory.Instance.SaveNow();
                Assert.IsTrue(File.Exists(ScenePlacementMemory.StoragePathOverride));
                Assert.IsTrue(File.Exists(Path.Combine(directory, "口红笔迹.json")));
                SceneManager.LoadScene("ApartmentDesignV2");
                yield return null; yield return null;
                frame = GameObject.Find("相框_01");
                sofa = Object.FindFirstObjectByType<MovableFurniture>();
                Assert.Less(Vector3.Distance(expectedFrame, frame.transform.position), .001f);
                Assert.Less(Quaternion.Angle(expectedRotation, frame.transform.rotation), .1f);
                Assert.Less(Vector3.Distance(expectedSofa, sofa.transform.position), .001f);
                Assert.IsTrue(sofa.GetComponent<Rigidbody>().isKinematic);
                Assert.AreEqual(1, LipstickDrawingMemory.Instance.StrokeCount);
                var line = LipstickDrawingMemory.Instance.GetComponentInChildren<LineRenderer>();
                Assert.IsNotNull(line); Assert.AreEqual(2, line.positionCount);
                Vector3 before = line.GetPosition(0);
                sofa.transform.position += Vector3.right * .1f;
                sofa.GetComponent<Rigidbody>().position = sofa.transform.position;
                yield return null; yield return null;
                Assert.Less(Vector3.Distance(before + Vector3.right * .1f, line.GetPosition(0)), .002f, "笔迹没有跟随物品");
                Debug.Log("摆放和笔迹跨场景恢复验收通过；独立测试存档=" + directory);
            }
            finally { ScenePlacementMemory.StoragePathOverride = null; }
        }

        [UnityTest]
        public IEnumerator 脸部握持带动头部且松手自然恢复()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var face = Object.FindFirstObjectByType<CharacterFaceTouch>();
            Assert.IsNotNull(face);
            var animator = face.GetComponentInChildren<Animator>();
            var performance = face.GetComponent<CharacterVoicePerformance>();
            performance.enabled = false;
            animator.speed = 0;
            var probe = face.gameObject.AddComponent<ContinuationPoseProbe>();
            probe.animator = animator;
            yield return null; yield return null;
            var head = face.Head;
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            float length = Vector3.Distance(head.position, neck.position);
            Quaternion baseline = probe.headRotation;
            var hand = new GameObject("脸部握持测试手").transform;
            hand.position = face.FaceCenter;
            Assert.IsTrue(face.BeginHold(hand));
            hand.position += face.transform.right * .13f + Vector3.up * .065f;
            yield return new WaitForSeconds(.7f);
            Assert.IsTrue(face.IsHolding);
            Assert.Greater(face.CurrentAngles.magnitude, 10f);
            Assert.Greater(Quaternion.Angle(probe.headRotation, baseline), 8f);
            Assert.AreEqual(length, probe.neckLength, .003f, "牵动脸部拉长了颈部");
            hand.position += face.transform.right * .08f;
            yield return new WaitForSeconds(.4f);
            Assert.LessOrEqual(Mathf.Abs(face.CurrentAngles.y), face.maximumYaw + .05f);
            face.Release();
            yield return new WaitForSeconds(1.3f);
            Assert.Less(face.CurrentAngles.magnitude, .1f);
            Assert.Less(Quaternion.Angle(probe.headRotation, baseline), .5f, "松手后头部偏移没有恢复");
            Object.Destroy(hand.gameObject);
        }

        [UnityTest]
        public IEnumerator 口红绘画按下启用松开停止并能命中实际脸部皮肤()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var painter = Object.FindFirstObjectByType<LipstickPainter>();
            Assert.IsNotNull(painter); Assert.IsNotNull(painter.tip);
            var face = Object.FindFirstObjectByType<FacePaintSurface>();
            Assert.IsNotNull(face); Assert.IsNotNull(face.skin);
            var head = face.transform;
            var touch = Object.FindFirstObjectByType<CharacterFaceTouch>();
            var ray = new Ray(touch.FaceCenter + touch.FaceForward * .5f, -touch.FaceForward);
            Assert.IsTrue(face.Raycast(ray, 1f, out var point, out var normal), "口红不能命中真实脸部网格");
            Assert.IsTrue(painter.DrawAt(face.Surface, point + normal * .0018f));
            Assert.IsTrue(painter.DrawAt(face.Surface, point + normal * .0018f + head.right * .015f));
            Assert.IsTrue(painter.IsDrawing);
            painter.EndDrawing(); Assert.IsFalse(painter.IsDrawing);
            Assert.AreEqual(1, LipstickDrawingMemory.Instance.StrokeCount);
            var probe = face.transform.root.gameObject.AddComponent<ContinuationPoseProbe>();
            probe.animator = face.GetComponentInParent<Animator>();
            probe.paintedAnchor = head;
            probe.paintedLine = LipstickDrawingMemory.Instance.GetComponentInChildren<LineRenderer>();
            probe.paintedLocalPoint = head.InverseTransformPoint(point + normal * .0018f);
            var hand = new GameObject("绘画后头部牵动验收手").transform;
            hand.position = touch.FaceCenter;
            Assert.IsTrue(touch.BeginHold(hand));
            hand.position += touch.transform.right * .1f;
            yield return new WaitForSeconds(.6f);
            Assert.Less(probe.attachmentError, .002f, "脸上的笔迹没有跟随最终头部姿态");
            touch.Release(); Object.Destroy(hand.gameObject);
            yield return new WaitForSeconds(1.2f);
            var cameraNode = new GameObject("口红脸部绘画验收相机");
            var camera = cameraNode.AddComponent<Camera>();
            camera.fieldOfView = 35; camera.nearClipPlane = .01f;
            cameraNode.transform.position = touch.FaceCenter + touch.FaceForward * .55f;
            cameraNode.transform.LookAt(touch.FaceCenter);
            Capture(camera, "口红脸部绘画实际效果");
            Object.Destroy(cameraNode);
        }

        private static float Capture(Camera camera, string name)
        {
            var rt = new RenderTexture(640, 640, 24);
            var texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); texture.Apply();
            Directory.CreateDirectory("BuildLogs"); File.WriteAllBytes("BuildLogs/" + name + ".png", texture.EncodeToPNG());
            float luminance = 0; int samples = 0;
            for (int x = 230; x < 410; x += 4) for (int y = 230; y < 410; y += 4)
            { var pixel = texture.GetPixel(x, y); luminance += (pixel.r + pixel.g + pixel.b) / 3; samples++; }
            camera.targetTexture = null; RenderTexture.active = null;
            Object.Destroy(rt); Object.Destroy(texture);
            return luminance / samples;
        }
    }
}

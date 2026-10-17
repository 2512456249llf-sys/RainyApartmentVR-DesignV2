using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    // 不依赖外部模型；在 Unity 内生成圆滑手掌、关节与袖口。
    public static class DesignVRHands
    {
        private const string Art = "Assets/_Project/DesignV2";
        private const string HandName = "可见虚拟手";

        [MenuItem("雨夜公寓/为新版场景安装双手")]
        public static void InstallIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var rig = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "XROrigin_PCVR");
            if (!rig) throw new InvalidOperationException("找不到新版场景的头显玩家。");
            Install(rig);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("新版场景已安装左右虚拟手。");
        }

        public static void InstallAndVerify()
        {
            InstallIntoScene();
            VerifyAndPreview();
        }

        public static void VerifyAndPreview()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var rig = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "XROrigin_PCVR");
            if (!rig) throw new InvalidOperationException("缺少头显玩家。");
            var hands = rig.GetComponentsInChildren<VRHandPose>(true);
            if (hands.Length != 2) throw new InvalidOperationException("左右手数量不是两只：" + hands.Length);
            foreach (var hand in hands)
            {
                if (hand.GetComponentsInChildren<MeshRenderer>(true).Length < 18)
                    throw new InvalidOperationException("手部网格不完整：" + hand.name);
                var touch = hand.GetComponentsInChildren<SphereCollider>(true)
                    .FirstOrDefault(c => c.name == "虚拟手触摸探测");
                if (!touch || !touch.isTrigger)
                    throw new InvalidOperationException("虚拟手触摸探测体没有正确安装。");
            }
            foreach (var oldCamera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) oldCamera.enabled = false;
            rig.SetActive(true);
            rig.transform.position = new Vector3(0f, 100f, 0f);
            rig.transform.rotation = Quaternion.identity;
            var left = hands.First(h => h.transform.parent.name == "Left Controller").transform.parent;
            var right = hands.First(h => h.transform.parent.name == "Right Controller").transform.parent;
            left.localPosition = new Vector3(-.19f, 0f, .48f);
            right.localPosition = new Vector3(.19f, 0f, .48f);
            left.localRotation = Quaternion.Euler(-18f, -10f, 0f);
            right.localRotation = Quaternion.Euler(-18f, 10f, 0f);
            var middle = (left.position + right.position) * .5f + Vector3.forward * .1f;
            var lightObject = new GameObject("临时手部预览照明");
            lightObject.transform.position = middle + new Vector3(0f, .65f, -.45f);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.intensity = 1.3f; light.range = 3f;
            var cameraObject = new GameObject("临时手部预览相机");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .051f, .07f);
            camera.fieldOfView = 55f;
            camera.transform.position = middle + new Vector3(0f, .12f, -.48f);
            camera.transform.LookAt(middle);
            var rt = new RenderTexture(1200, 750, 24);
            var image = new Texture2D(1200, 750, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); image.Apply();
            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/06_双手预览.png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
            Debug.Log("左右手及射线兼容检查通过，已生成双手预览。");
        }

        public static void Install(GameObject rig)
        {
            var skin = Material("手部暖色肌肤", new Color(.79f, .58f, .49f), .34f);
            var nail = Material("手部自然指甲", new Color(.78f, .64f, .58f), .38f);
            var sleeve = Material("手部黑色袖口", new Color(.034f, .042f, .055f), .27f);
            var sphere = SphereMesh();
            foreach (bool left in new[] { true, false })
            {
                string controllerName = left ? "Left Controller" : "Right Controller";
                var controller = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == controllerName);
                if (!controller) throw new InvalidOperationException("找不到手柄节点：" + controllerName);
                var old = controller.Find(HandName);
                if (old) Object.DestroyImmediate(old.gameObject);
                // 官方手柄模型使用示例材质；仅隐藏外壳，保留追踪、交互器和射线。
                foreach (var renderer in controller.GetComponentsInChildren<Renderer>(true))
                    if (!(renderer is LineRenderer)) renderer.enabled = false;

                var hand = Child(HandName, controller);
                hand.localPosition = new Vector3(left ? -.018f : .018f, -.035f, .055f);
                hand.localRotation = Quaternion.Euler(-12f, 0f, left ? -7f : 7f);
                Ellipsoid("掌心", hand, sphere, skin, new Vector3(0f, 0f, .042f), new Vector3(.049f, .022f, .068f));
                Ellipsoid("掌背", hand, sphere, skin, new Vector3(0f, .006f, .038f), new Vector3(.046f, .019f, .061f));
                Ellipsoid("手腕", hand, sphere, skin, new Vector3(0f, 0f, -.033f), new Vector3(.035f, .028f, .041f));
                Ellipsoid("袖口", hand, sphere, sleeve, new Vector3(0f, 0f, -.084f), new Vector3(.041f, .032f, .052f));

                var roots = new Transform[5];
                var tips = new Transform[5];
                for (int i = 0; i < 5; i++)
                {
                    bool thumb = i == 0;
                    float x = thumb ? (left ? .052f : -.052f) : (i - 2.5f) * .022f;
                    float z = thumb ? .006f : .091f - Mathf.Abs(i - 2.5f) * .004f;
                    float firstLength = thumb ? .044f : i == 2 ? .062f : i == 3 ? .061f : .054f;
                    float secondLength = thumb ? .033f : i == 2 ? .050f : i == 3 ? .047f : .042f;
                    float radius = thumb ? .013f : .0105f;
                    var root = Child(thumb ? "拇指" : "手指" + i, hand);
                    root.localPosition = new Vector3(x, -.001f, z);
                    root.localRotation = thumb
                        ? Quaternion.Euler(4f, left ? 44f : -44f, left ? -22f : 22f)
                        : Quaternion.Euler(0f, (i - 2.5f) * 4f, 0f);
                    Ellipsoid("近节", root, sphere, skin, new Vector3(0f, 0f, firstLength * .5f), new Vector3(radius, radius * .91f, firstLength * .57f));
                    Ellipsoid("指关节", root, sphere, skin, new Vector3(0f, 0f, firstLength), Vector3.one * radius * .94f);
                    var tip = Child("末节关节", root);
                    tip.localPosition = new Vector3(0f, 0f, firstLength);
                    Ellipsoid("末节", tip, sphere, skin, new Vector3(0f, 0f, secondLength * .5f), new Vector3(radius * .86f, radius * .82f, secondLength * .57f));
                    Ellipsoid("指甲", tip, sphere, nail, new Vector3(0f, radius * .73f, secondLength * .82f), new Vector3(radius * .56f, .0018f, secondLength * .17f));
                    roots[i] = root;
                    tips[i] = tip;
                }
                hand.gameObject.AddComponent<VRHandPose>().Configure(left, roots, tips);
            }
        }

        private static Transform Child(string name, Transform parent)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            return item;
        }

        private static void Ellipsoid(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = Child(name, parent);
            item.localPosition = position;
            item.localScale = scale;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material Material(string name, Color color, float smoothness)
        {
            string path = Art + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SphereMesh()
        {
            const string path = Art + "/Meshes/虚拟手圆滑部件.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            const int rows = 14, columns = 20;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float v = (float)y / rows;
                float u = (float)x / columns;
                float angle = u * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Sin(v * Mathf.PI) * Mathf.Cos(angle), Mathf.Cos(v * Mathf.PI), Mathf.Sin(v * Mathf.PI) * Mathf.Sin(angle));
                vertices.Add(normal); normals.Add(normal); uv.Add(new Vector2(u, v));
            }
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x;
                int b = a + columns + 1;
                triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
            return DesignMesh.Save("虚拟手圆滑部件", vertices, triangles, uv, normals);
        }
    }
}

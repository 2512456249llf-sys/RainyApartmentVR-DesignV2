using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class PhotoFrameInstaller
    {
        public const string Folder = "Assets/_Project/Props/相框";
        public const string ModelPath = Folder + "/相框_成品.fbx";
        public const string ObjectName = "相框_可拿取";

        [MenuItem("雨夜公寓/把相框放进地图")]
        public static void Install()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("相框模型不在工程里：" + ModelPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();

            var frameMaterial = MakeMaterial("M_Frame_场景", null, new Color(.905f, .905f, .895f), .30f);
            var photoMaterial = MakeMaterial("M_Photo_场景", Folder + "/front_texture.png", Color.white, .40f);
            var backMaterial = MakeMaterial("M_Back_场景", Folder + "/back_texture.png", Color.white, .85f);
            var woodMaterial = MakeMaterial("M_Wood_场景", null, new Color(.68f, .49f, .30f), .55f);

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "男主_雨夜公寓");
            var anchor = character ? character.position : new Vector3(-1.05f, .01f, 1.35f);

            var previous = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == ObjectName);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("加载不到相框模型");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = ObjectName;
            var renderer = instance.GetComponentInChildren<Renderer>(true);
            if (!renderer) throw new InvalidOperationException("相框没有渲染组件");
            renderer.sharedMaterials = new[] { frameMaterial, photoMaterial, backMaterial, woodMaterial };
            SeparateSupport(instance, woodMaterial);

            var probe = anchor + new Vector3(.35f, 0f, -1.15f);
            float surface = .02f;
            if (Physics.Raycast(new Vector3(probe.x, 2.4f, probe.z), Vector3.down, out var hit, 8f))
                surface = hit.point.y + 0.02f;
            instance.transform.position = new Vector3(probe.x, surface + .095f, probe.z);
            instance.transform.rotation = Quaternion.LookRotation(anchor - instance.transform.position, Vector3.up)
                * Quaternion.Euler(-68f, 180f, 0f);

            var bounds = renderer.localBounds;
            if (bounds.size.sqrMagnitude < 1e-8f) bounds = new Bounds(renderer.localBounds.center, renderer.bounds.size / goScale(instance));
            var box = instance.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = new Vector3(Mathf.Abs(bounds.size.x), Mathf.Abs(bounds.size.y), Mathf.Abs(bounds.size.z));

            var body = instance.AddComponent<Rigidbody>();
            body.mass = .6f;
            body.drag = .05f;
            body.angularDrag = .05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;

            instance.AddComponent<GrabbableProp>();

            var grabType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit")
                ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
            if (grabType != null)
            {
                var grab = instance.AddComponent(grabType);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                grabType.GetProperty("throwOnDetach", flags)?.SetValue(grab, true);
                grabType.GetProperty("useDynamicAttach", flags)?.SetValue(grab, true);
                Debug.Log("已添加手抓组件：" + grabType.FullName);
            }
            else
            {
                Debug.LogWarning("没找到 XRGrabInteractable，只用 E 键 / 手柄射线交互。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("相框已放入地图，位置：" + instance.transform.position + "，材质 4 个，尺寸=" + bounds.size);
        }

        [MenuItem("雨夜公寓/检查并修正相框碰撞盒")]
        public static void DiagnoseAndFix()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var go = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == ObjectName)?.gameObject;
            if (!go) throw new InvalidOperationException("场景里没有相框");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/M_Wood_场景.mat");
            SeparateSupport(go, wood);
            // 模型正面位于本地 XZ 平面。原先零倾角会让相框平躺，支架压在照片背面。
            var character = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "男主_雨夜公寓");
            var oldPosition = go.transform.position;
            go.transform.position = new Vector3(oldPosition.x, .115f, oldPosition.z);
            if (character)
                go.transform.rotation = Quaternion.LookRotation(character.position - go.transform.position, Vector3.up)
                    * Quaternion.Euler(-68f, 180f, 0f);
            var body = go.GetComponent<Rigidbody>();
            if (body)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            Debug.Log("相框缩放 lossyScale=" + go.transform.lossyScale);
            var fallback = new Bounds(go.transform.position, Vector3.one * .2f);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                if (!mesh && renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                Debug.Log("渲染组件：" + renderer.GetType().Name + " / " + renderer.name +
                          "，网格=" + (mesh ? mesh.name : "无") +
                          "，顶点=" + (mesh ? mesh.vertexCount : -1) +
                          "，localBounds=" + renderer.localBounds +
                          "，worldBounds=" + renderer.bounds);
                if (mesh && mesh.vertexCount > 0) fallback = renderer.bounds;
            }
            var box = go.GetComponent<BoxCollider>();
            if (box)
            {
                var renderer = go.GetComponentInChildren<Renderer>(true);
                box.center = renderer ? renderer.localBounds.center : Vector3.zero;
                var s = renderer ? renderer.localBounds.size : fallback.size;
                box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                Debug.Log("碰撞盒已修正：center=" + box.center + " size=" + box.size);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SeparateSupport(GameObject instance, Material wood)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)?.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            var filter = instance.GetComponentInChildren<MeshFilter>(true);
            if (!source || !filter || source.subMeshCount < 4)
                throw new InvalidOperationException("相框原始网格缺少可分离的支架部分。");

            // 原网格第 4 子网格穿过照片。保留相框和照片，只移除这部分三角面。
            var bodyPath = Folder + "/相框_去除穿插支架.asset";
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(bodyPath);
            if (!body)
            {
                body = UnityEngine.Object.Instantiate(source);
                body.name = "相框_去除穿插支架";
                body.SetTriangles(Array.Empty<int>(), 3);
                AssetDatabase.CreateAsset(body, bodyPath);
            }
            filter.sharedMesh = body;

            var standPath = Folder + "/相框_背面斜支架.asset";
            var standMesh = AssetDatabase.LoadAssetAtPath<Mesh>(standPath);
            if (!standMesh)
            {
                standMesh = MakeStandMesh();
                AssetDatabase.CreateAsset(standMesh, standPath);
            }
            else
            {
                var updated = MakeStandMesh();
                EditorUtility.CopySerialized(updated, standMesh);
                UnityEngine.Object.DestroyImmediate(updated);
                EditorUtility.SetDirty(standMesh);
            }
            var stand = instance.transform.Find("相框背面支架_修复");
            if (!stand)
            {
                stand = new GameObject("相框背面支架_修复").transform;
                stand.SetParent(instance.transform, false);
            }
            stand.localPosition = Vector3.zero;
            stand.localRotation = Quaternion.identity;
            stand.localScale = Vector3.one;
            var standFilter = stand.GetComponent<MeshFilter>();
            if (!standFilter) standFilter = stand.gameObject.AddComponent<MeshFilter>();
            standFilter.sharedMesh = standMesh;
            var standRenderer = stand.GetComponent<MeshRenderer>();
            if (!standRenderer) standRenderer = stand.gameObject.AddComponent<MeshRenderer>();
            standRenderer.sharedMaterial = wood;
            var standCollider = stand.GetComponent<MeshCollider>();
            if (!standCollider) standCollider = stand.gameObject.AddComponent<MeshCollider>();
            standCollider.sharedMesh = standMesh;
            standCollider.convex = true;
            Debug.Log("相框支架已从照片网格移除，并作为独立的斜腿安装在背面。");
        }

        private static Mesh MakeStandMesh()
        {
            // 本地 +Y 是照片正面，支架全部位于 -Y 背面；顶部靠背板，脚落到地面。
            var vertices = new[]
            {
                new Vector3(-.014f, -.021f, .065f), new Vector3(.014f, -.021f, .065f),
                new Vector3(-.014f, -.037f, .065f), new Vector3(.014f, -.037f, .065f),
                new Vector3(-.011f, -.081f, -.090f), new Vector3(.011f, -.081f, -.090f),
                new Vector3(-.011f, -.094f, -.090f), new Vector3(.011f, -.094f, -.090f)
            };
            var triangles = new[]
            {
                0, 1, 2, 1, 3, 2,
                4, 6, 5, 5, 6, 7,
                0, 4, 1, 1, 4, 5,
                2, 3, 6, 3, 7, 6,
                0, 2, 4, 2, 6, 4,
                1, 5, 3, 3, 5, 7
            };
            var mesh = new Mesh { name = "相框_背面斜支架" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float goScale(GameObject go) => Mathf.Max(1e-6f, go.transform.lossyScale.x);

        private static Material MakeMaterial(string name, string imagePath, Color color, float smoothness)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (!string.IsNullOrEmpty(imagePath))
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

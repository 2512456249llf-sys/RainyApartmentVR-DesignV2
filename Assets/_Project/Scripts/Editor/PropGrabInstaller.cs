using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    // 把 12 个相框 + 洗衣粉放进地图，做成"拿起不掉、浮空任意摆放"的装饰物。
    public static class PropGrabInstaller
    {
        public const string FrameFolder = "Assets/_Project/Props/相框12";
        public const string SoapPath = "Assets/_Project/Props/洗衣粉/洗衣粉_成品.fbx";
        public const string FrameGroupName = "相框_刷新点";
        public const string SoapName = "洗衣粉";

        // 相框在电视右前方的房间空地上，按桌面高度摆成 6×2。
        private static readonly Vector3 FrameGroupCenter = new Vector3(1.2f, 0.75f, 4.6f);
        // 洗衣粉放在电视柜右端柜面上。
        private static readonly Vector3 SoapPosition = new Vector3(-1.1f, 0.56f, 5.5f);

        [MenuItem("雨夜公寓/放置相框与洗衣粉")]
        public static void Install()
        {
            var framePaths = AssetDatabase.FindAssets("t:Model", new[] { FrameFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            if (framePaths.Length == 0) throw new InvalidOperationException("相框12 目录里没有 fbx。");

            foreach (var p in framePaths) PrepareImporter(p);
            PrepareImporter(SoapPath);

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            RemoveIfExists(scene, FrameGroupName);
            RemoveIfExists(scene, SoapName);
            // 原相框属于用户现有摆放，安装新增十二个时必须保留。

            var group = new GameObject(FrameGroupName).transform;
            group.position = FrameGroupCenter;

            for (int i = 0; i < framePaths.Length; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(framePaths[i]);
                if (!model) { Debug.LogWarning("加载不到：" + framePaths[i]); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "相框_" + (i + 1).ToString("00");
                inst.transform.SetParent(group, false);
                int col = i % 6, row = i / 6;
                inst.transform.localPosition = new Vector3((col - 2.5f) * 0.5f, 0f, row * 0.55f);
                // Unity 导入后相框平躺；转成竖立，并翻 180° 让画面朝房间（-Z）。
                inst.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
                Decorate(inst, 0.6f, "相框");
            }

            var soapModel = AssetDatabase.LoadAssetAtPath<GameObject>(SoapPath);
            if (!soapModel) throw new InvalidOperationException("加载不到洗衣粉模型：" + SoapPath);
            var soap = (GameObject)PrefabUtility.InstantiatePrefab(soapModel);
            soap.name = SoapName;
            soap.transform.position = SoapPosition;
            soap.transform.rotation = Quaternion.identity;
            Decorate(soap, 0.5f, "洗衣粉");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("已放入 " + framePaths.Length + " 个相框 + 1 个洗衣粉。相框中心=" + FrameGroupCenter + "，洗衣粉=" + SoapPosition);
        }

        [MenuItem("雨夜公寓/诊断相框材质")]
        public static void Dump()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var go = GameObject.Find("相框_01");
            if (!go) { Debug.Log("DIAG no 相框_01"); return; }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
                Debug.Log("DIAG renderer=" + r.name + " submeshes=" + (mesh ? mesh.subMeshCount : -1));
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    Debug.Log("DIAG   mat[" + i + "]=" + (mats[i] ? mats[i].name : "null") +
                              " shader=" + (mats[i] ? mats[i].shader.name : "-") +
                              " tex=" + (mats[i] && mats[i].mainTexture ? mats[i].mainTexture.name : "null"));
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath(FrameFolder + "/photo_01_vr_guy.fbx");
            foreach (var a in assets)
            {
                if (a is Material m)
                    Debug.Log("DIAG fbxMat=" + m.name + " shader=" + m.shader.name + " tex=" + (m.mainTexture ? m.mainTexture.name : "null") +
                              " baseColor=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-"));
                else if (a is Texture t)
                    Debug.Log("DIAG fbxTex=" + t.name);
            }

            // 各子网格在世界空间里的平均法线朝向
            var mf = go.GetComponentInChildren<MeshFilter>(true);
            if (mf && mf.sharedMesh)
            {
                var mesh = mf.sharedMesh;
                var normals = mesh.normals;
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var tris = mesh.GetTriangles(sm);
                    Vector3 acc = Vector3.zero;
                    for (int i = 0; i < tris.Length; i++) acc += normals[tris[i]];
                    if (acc.sqrMagnitude > 1e-9f)
                        Debug.Log("DIAG submesh[" + sm + "] worldNormal=" + go.transform.TransformDirection(acc.normalized) + " tris=" + (tris.Length / 3));
                }
            }
            Debug.Log("DIAG 相框_01 worldPos=" + go.transform.position + " euler=" + go.transform.eulerAngles);
        }

        [MenuItem("雨夜公寓/渲染检查_相框与洗衣粉")]
        public static void RenderCheck()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var camGo = new GameObject("PropCheckCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            var urpCam = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCam != null && camGo.GetComponent(urpCam) == null) camGo.AddComponent(urpCam);
            camGo.transform.position = new Vector3(2.0f, 1.7f, 1.2f);
            camGo.transform.LookAt(new Vector3(-0.5f, 0.85f, 5.4f));
            var lightGo = new GameObject("PropCheckLight");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 2.2f;
            l.range = 12f;

            var rt = new RenderTexture(1280, 800, 24);
            lightGo.transform.position = camGo.transform.position;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..\\PropCheck.png"));
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("渲染完成：" + path);

            // 近景：正对第 1 个相框，确认画面没颠倒
            camGo.transform.position = new Vector3(-0.05f, 0.85f, 3.5f);
            camGo.transform.LookAt(new Vector3(-0.05f, 0.78f, 4.9f));
            lightGo.transform.position = camGo.transform.position;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var path2 = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..\\PropCheckClose.png"));
            System.IO.File.WriteAllBytes(path2, tex.EncodeToPNG());
            Debug.Log("渲染完成：" + path2);

            // 从相框背面看，判断照片到底朝哪边
            camGo.transform.position = new Vector3(-0.05f, 0.85f, 5.9f);
            camGo.transform.LookAt(new Vector3(-0.05f, 0.78f, 4.4f));
            lightGo.transform.position = camGo.transform.position;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var path3 = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..\\PropCheckBack.png"));
            System.IO.File.WriteAllBytes(path3, tex.EncodeToPNG());
            Debug.Log("渲染完成：" + path3);

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
        }

        [MenuItem("雨夜公寓/验收相框与洗衣粉")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var frames = Object.FindObjectsByType<GrabbableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => System.Text.RegularExpressions.Regex.IsMatch(g.name, "^相框_[0-9]{2}$")).ToArray();
            if (frames.Length != 12) throw new InvalidOperationException("相框数量不是 12，而是 " + frames.Length);
            var soap = Object.FindObjectsByType<GrabbableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(g => g.name == SoapName);
            if (soap == null) throw new InvalidOperationException("场景里没有洗衣粉。");
            foreach (var g in frames.Concat(new[] { soap }))
            {
                if (!g.GetComponent<BoxCollider>()) throw new InvalidOperationException(g.name + " 没有碰撞盒");
                var body = g.GetComponent<Rigidbody>();
                if (!body || !body.isKinematic || body.useGravity) throw new InvalidOperationException(g.name + " 刚体状态不对");
                if (g.GetComponentInChildren<Renderer>(true) == null) throw new InvalidOperationException(g.name + " 没有渲染器");
            }
            Debug.Log("验收通过：12 个相框 + 洗衣粉，均有碰撞盒、运动学刚体、可拿取、松手浮空。");
        }

        private static void RemoveIfExists(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var old = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == name);
            if (old && old.gameObject != null) Object.DestroyImmediate(old.gameObject);
        }

        private static void PrepareImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
            // 把 FBX 内嵌的贴图提取成独立文件，Unity 才能给材质用上。
            try { importer.ExtractTextures(System.IO.Path.GetDirectoryName(path)); }
            catch (Exception e) { Debug.LogWarning("提取内嵌贴图跳过：" + path + " / " + e.GetType().Name); }
        }

        private static void Decorate(GameObject go, float mass, string label)
        {
            FixMaterials(go);

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(label + " 没有渲染组件。");
            var bounds = renderers[0].localBounds;
            foreach (var r in renderers) bounds.Encapsulate(r.localBounds);

            var box = go.GetComponent<BoxCollider>();
            if (!box) box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = new Vector3(Mathf.Max(0.005f, Mathf.Abs(bounds.size.x)),
                                   Mathf.Max(0.005f, Mathf.Abs(bounds.size.y)),
                                   Mathf.Max(0.005f, Mathf.Abs(bounds.size.z)));

            var body = go.GetComponent<Rigidbody>();
            if (!body) body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.drag = 0.05f;
            body.angularDrag = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;

            var grab = go.GetComponent<GrabbableProp>();
            if (!grab) grab = go.AddComponent<GrabbableProp>();
            grab.stayWhereReleased = true;
            grab.faceHolder = false;

            var grabType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit")
                ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
            if (grabType != null && go.GetComponent(grabType) == null)
            {
                var xr = go.AddComponent(grabType);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                grabType.GetProperty("throwOnDetach", flags)?.SetValue(xr, false);
                grabType.GetProperty("useDynamicAttach", flags)?.SetValue(xr, true);
            }
        }

        private static void FixMaterials(GameObject go)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var fixedMat = ToUrpLit(mats[i], urpLit);
                    if (!ReferenceEquals(fixedMat, mats[i])) { mats[i] = fixedMat; changed = true; }
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        private static Material ToUrpLit(Material source, Shader urpLit)
        {
            if (source == null) return null;
            var baseTex = source.mainTexture;
            var origColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;

            if (source.shader != urpLit) source.shader = urpLit;
            if (source.HasProperty("_BaseMap")) source.SetTexture("_BaseMap", baseTex);
            if (source.HasProperty("_BaseColor"))
            {
                // 有贴图：基础色必须为白（URP = 基础色 × 贴图，黑基础色会变全黑）
                source.SetColor("_BaseColor", baseTex != null ? Color.white : origColor);
            }
            if (normal != null && source.HasProperty("_NormalMap"))
            {
                source.SetTexture("_NormalMap", normal);
                source.SetFloat("_BumpScale", 1f);
                source.EnableKeyword("_NORMALMAP");
            }
            if (baseTex != null) source.EnableKeyword("_BASEMAP");
            source.SetFloat("_Smoothness", 0.35f);
            source.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(source);
            return source;
        }
    }
}

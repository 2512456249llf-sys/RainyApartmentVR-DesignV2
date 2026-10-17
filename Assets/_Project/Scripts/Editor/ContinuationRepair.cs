using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR.Editor
{
    public static class ContinuationRepair
    {
        private const string MeshFolder = "Assets/_Project/Props/修复网格";
        private const string LipstickFolder = "Assets/_Project/Props/口红";
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            RestoreOriginalFrame(scene);
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(LipstickFolder);
            AssetDatabase.Refresh();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None)
                .Where(g => Regex.IsMatch(g.name, "^相框_[0-9]{2}$") || g.name == PhotoFrameInstaller.ObjectName))
                CorrectPhotoNormals(prop.gameObject);
            var npc = GameObject.Find(CharacterAnimationSetup.CharacterName);
            ConfigurePrayer(npc.GetComponentInChildren<Animator>());
            if (!npc.GetComponent<CharacterFaceTouch>()) npc.AddComponent<CharacterFaceTouch>();
            var head = npc.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var faceSurface = head.GetComponent<DrawingSurface>();
            if (!faceSurface) faceSurface = head.gameObject.AddComponent<DrawingSurface>();
            if (string.IsNullOrEmpty(faceSurface.stableId)) faceSurface.stableId = Guid.NewGuid().ToString("N");
            var facePaint = head.GetComponent<FacePaintSurface>();
            if (!facePaint) facePaint = head.gameObject.AddComponent<FacePaintSurface>();
            facePaint.skin = npc.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh && r.sharedMesh.blendShapeCount > 0).OrderByDescending(r => r.sharedMesh.blendShapeCount).First();
            var sofa = GameObject.Find(PlayerAssetsInstaller.SofaName);
            if (!sofa) throw new Exception("猪沙发不在当前场景中");
            sofa.GetComponent<MovableFurniture>().stayWhereReleased = true;
            var body = sofa.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            sofa.GetComponent<XRGrabInteractable>().throwOnDetach = false;
            AddLipstick();
            var manager = UnityEngine.Object.FindFirstObjectByType<ScenePlacementMemory>();
            if (!manager) manager = new GameObject("物品摆放记忆").AddComponent<ScenePlacementMemory>();
            if (!manager.GetComponent<LipstickDrawingMemory>()) manager.gameObject.AddComponent<LipstickDrawingMemory>();
            const string strokePath = LipstickFolder + "/口红笔迹材质.mat";
            var strokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(strokePath);
            if (!strokeMaterial) { strokeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(strokeMaterial, strokePath); }
            strokeMaterial.SetColor("_BaseColor", new Color(.72f, .025f, .09f, 1));
            EditorUtility.SetDirty(strokeMaterial);
            manager.GetComponent<LipstickDrawingMemory>().strokeMaterial = strokeMaterial;
            GameObject.Find("口红_可绘画").transform.position = new Vector3(-.33f, .75f, 4.23f);
            var movable = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.GetComponent<GrabbableProp>() || t.GetComponent<MovableFurniture>() || t.GetComponent<XRGrabInteractable>() || t.name == "电视右侧_长方形展示桌");
            foreach (var t in movable)
            {
                var memory = t.GetComponent<PersistentPlacement>();
                if (!memory) memory = t.gameObject.AddComponent<PersistentPlacement>();
                if (string.IsNullOrEmpty(memory.stableId)) memory.stableId = Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(memory);
            }
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (collider.isTrigger || collider.transform.IsChildOf(npc.transform) || collider.GetComponentInParent<VRPlayerBody>() ||
                    collider.GetComponentInParent<DesktopPlayerController>() || collider.GetComponentInParent<VRHandPose>() || collider.GetComponentInParent<LipstickPainter>()) continue;
                var surface = collider.GetComponent<DrawingSurface>();
                if (!surface) surface = collider.gameObject.AddComponent<DrawingSurface>();
                if (string.IsNullOrEmpty(surface.stableId)) surface.stableId = Guid.NewGuid().ToString("N");
            }
            var ids = UnityEngine.Object.FindObjectsByType<PersistentPlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(m => m.stableId).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new Exception("摆放记忆编号重复，停止保存");
            int frames = UnityEngine.Object.FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None).Count(g => g.name.StartsWith("相框_"));
            if (frames != 13) throw new Exception("相框总数应为十三个，实际=" + frames);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("七项修复安装完成：十三相框、照片法线、原片段反向起身、沙发悬停、摆放记忆物品=" + ids.Length + "、脸部牵动、口红表面绘画");
            InteriorFinish.Capture();
        }
        private static void RestoreOriginalFrame(Scene scene)
        {
            if (GameObject.Find(PhotoFrameInstaller.ObjectName)) return;
            const string reference = "Assets/_Project/Scenes/临时原相框恢复参考.unity";
            File.Copy("旧版备份/2026-09-28_试玩姿态修复前/ApartmentDesignV2.unity", reference, true);
            AssetDatabase.ImportAsset(reference);
            var old = EditorSceneManager.OpenScene(reference, OpenSceneMode.Additive);
            try
            {
                var original = old.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == PhotoFrameInstaller.ObjectName);
                if (!original) throw new Exception("旧场景中未找到原相框，停止恢复");
                var restored = UnityEngine.Object.Instantiate(original.gameObject);
                restored.name = original.name;
                restored.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(restored, scene);
                Debug.Log("从旧场景恢复原相框：位置=" + restored.transform.position + " 比例=" + restored.transform.localScale);
            }
            finally { EditorSceneManager.CloseScene(old, true); AssetDatabase.DeleteAsset(reference); SceneManager.SetActiveScene(scene); }
        }
        public static void CorrectPhotoNormals(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var source = filter.sharedMesh;
                var mesh = UnityEngine.Object.Instantiate(source);
                var normals = mesh.normals;
                bool changed = false;
                for (int sub = 0; sub < mesh.subMeshCount && sub < r.sharedMaterials.Length; sub++)
                {
                    var mat = r.sharedMaterials[sub];
                    if (!mat || !(mat.name.StartsWith("照片_") || mat.name.Contains("Photo"))) continue;
                    var triangles = mesh.GetTriangles(sub);
                    // 模型的本地正面是正向纵轴，原照片面的法线却朝反向。
                    foreach (int index in triangles.Distinct()) normals[index] = Vector3.up;
                    var vertices = mesh.vertices;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var n = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                        if (Vector3.Dot(n, Vector3.up) < 0) { int tmp = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = tmp; }
                    }
                    mesh.SetTriangles(triangles, sub);
                    mat.SetFloat("_Cull", 2);
                    mat.SetFloat("_Smoothness", .12f);
                    mat.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(mat);
                    changed = true;
                    Debug.Log("照片正面受光修正：" + go.name + " 世界法线=" + r.transform.TransformDirection(Vector3.up));
                }
                if (!changed) { UnityEngine.Object.DestroyImmediate(mesh); continue; }
                mesh.normals = normals;
                mesh.RecalculateTangents();
                var path = MeshFolder + "/" + go.name + "_正面法线.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); filter.sharedMesh = existing; EditorUtility.SetDirty(existing); }
                else { mesh.name = go.name + "_正面法线"; AssetDatabase.CreateAsset(mesh, path); filter.sharedMesh = mesh; }
            }
        }
        public static void ConfigurePrayer(Animator animator)
        {
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var machine = controller.layers[0].stateMachine;
            var prayer = machine.states.First(s => s.state.name == "Prayer").state;
            var rise = machine.states.First(s => s.state.name == "PrayerRise").state;
            rise.motion = prayer.motion;
            rise.speed = 1f;
            foreach (var transition in rise.transitions.ToArray()) rise.RemoveTransition(transition);
            EditorUtility.SetDirty(controller);
            Debug.Log("起身沿用原片段的顺向起身段：" + prayer.motion.name);
        }
        private static Material Material(string name, Color color, float metallic)
        {
            var path = LipstickFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", .4f);
            return mat;
        }
        private static void Cylinder(Transform parent, string name, float y, float radius, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * y;
            go.transform.localScale = new Vector3(radius * 2, height * .5f, radius * 2);
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        private static void AddLipstick()
        {
            if (GameObject.Find("口红_可绘画")) return;
            var go = new GameObject("口红_可绘画");
            go.transform.position = new Vector3(-.33f, .75f, 4.23f);
            Cylinder(go.transform, "黑色外壳", .03f, .012f, .06f, Material("口红外壳", new Color(.025f, .025f, .03f), .3f));
            Cylinder(go.transform, "金色套管", .068f, .0105f, .026f, Material("口红金属套管", new Color(.7f, .44f, .15f), .75f));
            Cylinder(go.transform, "红色膏体", .094f, .008f, .03f, Material("口红膏体", new Color(.7f, .025f, .08f), 0));
            var tip = new GameObject("口红绘画笔尖").transform;
            tip.SetParent(go.transform, false);
            tip.localPosition = Vector3.up * .109f;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = .013f;
            collider.height = .112f;
            collider.center = Vector3.up * .056f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = .04f;
            body.isKinematic = true;
            body.useGravity = false;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            go.AddComponent<GrabbableProp>();
            go.AddComponent<LipstickPainter>().tip = tip;
        }
    }
}

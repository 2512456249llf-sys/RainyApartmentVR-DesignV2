using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    public static class CharacterInstaller
    {
        public const string ModelPath = "Assets/_Project/Characters/A.vrm";

        [MenuItem("雨夜公寓/用新 VRM 人物替换旧人物")]
        public static void InstallIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            Install();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("新 VRM 人物已替换旧人物。");
        }

        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var apartment = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "雨夜复式公寓_新版");
            if (!apartment) throw new InvalidOperationException("新版地图根节点缺失。");
            var group = apartment.transform.Find("人物");
            if (!group) { group = new GameObject("人物").transform; group.SetParent(apartment.transform, false); }
            var prior = group.Find("男主_雨夜公寓");
            if (prior) Object.DestroyImmediate(prior.gameObject);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("新 VRM 模型没有导入成功。");
            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (!instance) throw new InvalidOperationException("新 VRM 模型无法实例化。");
            instance.name = "男主_雨夜公寓";
            instance.transform.SetParent(group, false);
            instance.transform.position = new Vector3(-1.05f, .01f, 1.35f);
            instance.transform.rotation = Quaternion.Euler(0f, 165f, 0f);

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("新 VRM 人形骨架无效。");
            animator.runtimeAnimatorController = null;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length < 3) throw new InvalidOperationException("新 VRM 人物网格不完整。");
            foreach (var renderer in renderers)
            {
                renderer.updateWhenOffscreen = true;
                if (renderer.sharedMaterials.Any(material => !material || !material.shader || material.shader.name != "VRM10/Universal Render Pipeline/MToon10"))
                    throw new InvalidOperationException("新 VRM 材质缺失：" + renderer.name);
            }
            if (!instance.GetComponent<CharacterInteraction>()) instance.AddComponent<CharacterInteraction>();
            if (!instance.GetComponent<CharacterTouchSurfaces>()) instance.AddComponent<CharacterTouchSurfaces>();
            var collider = instance.GetComponent<CapsuleCollider>();
            if (!collider) collider = instance.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.0f, 0f);
            collider.height = 2.02f;
            collider.radius = .27f;
            Debug.Log("新 VRM 角色站位：" + instance.transform.position + "，网格=" + renderers.Length + "，人形骨架有效。");
        }

        public static void VerifyAndPreview()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "男主_雨夜公寓");
            if (!character) throw new InvalidOperationException("地图里找不到新人物。");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(character.gameObject);
            if (!source || AssetDatabase.GetAssetPath(source) != ModelPath)
                throw new InvalidOperationException("地图人物不是新的 VRM 模型。");
            var animator = character.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("新人物骨架没有通过检查。");
            var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length < 3 || renderers.Any(r => r.sharedMaterials.Any(m => !m || !m.shader)))
                throw new InvalidOperationException("新人物网格或材质不完整。");
            var interaction = character.GetComponent<CharacterInteraction>();
            if (!interaction) throw new InvalidOperationException("拥抱交互没有安装。");
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) camera.enabled = false;
            var cameraObject = new GameObject("临时人物验收相机");
            var preview = cameraObject.AddComponent<Camera>();
            preview.clearFlags = CameraClearFlags.SolidColor;
            preview.backgroundColor = new Color(.016f, .03f, .045f);
            preview.fieldOfView = 48f;
            preview.nearClipPlane = .05f;
            preview.farClipPlane = 100f;
            preview.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            interaction.PreparePose();
            interaction.EvaluatePose(0f);
            Debug.Log("新 VRM 站姿骨骼：" + interaction.PoseDiagnostics());
            Capture(preview, "12_新VRM人物站姿", character.position + character.forward * 3.2f + Vector3.up * 1.55f, character.position + Vector3.up * 1.15f);
            Capture(preview, "13_新VRM人物近景", character.position + character.forward * 2.4f + Vector3.up * 1.65f, character.position + Vector3.up * 1.52f);
            interaction.EvaluatePose(1f);
            Debug.Log("新 VRM 拥抱骨骼：" + interaction.PoseDiagnostics());
            Capture(preview, "14_新VRM人物拥抱", character.position + character.forward * 2.8f + Vector3.up * 1.6f, character.position + Vector3.up * 1.35f);
            Object.DestroyImmediate(cameraObject);
            Debug.Log("新 VRM 人物的骨架、网格、材质和拥抱姿势已检查。");
        }

        private static void Capture(Camera camera, string name, Vector3 from, Vector3 target)
        {
            camera.transform.position = from;
            camera.transform.LookAt(target);
            var rt = new RenderTexture(1200, 800, 24);
            var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            camera.Render(); camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
        }

        public static void InstallAndVerify()
        {
            InstallIntoScene();
            VerifyAndPreview();
        }
    }
}

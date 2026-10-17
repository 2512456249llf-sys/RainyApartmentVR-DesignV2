using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RainyApartmentVR.Editor
{
    public static class TouchStageOneInstaller
    {
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            const string prayerPath = "Assets/_Project/Characters/跪拜/跪下膜拜.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(prayerPath);
            var clips = importer.clipAnimations;
            foreach (var clip in clips) { clip.lockRootHeightY = true; clip.keepOriginalPositionY = true; clip.loopTime = false; }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var npc = GameObject.Find(CharacterAnimationSetup.CharacterName);
            if (!npc) throw new Exception("当前场景找不到原人物");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(npc);
            if (!npc.GetComponent<PerceptionEventHub>()) npc.AddComponent<PerceptionEventHub>();
            if (!npc.GetComponent<CharacterTouchPerception>()) npc.AddComponent<CharacterTouchPerception>();
            foreach (var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh))
            {
                var surface = skin.GetComponent<DrawingSurface>();
                if (!surface) surface = skin.gameObject.AddComponent<DrawingSurface>();
                if (string.IsNullOrEmpty(surface.stableId)) surface.stableId = Guid.NewGuid().ToString("N");
                var paint = skin.GetComponent<SkinnedPaintSurface>();
                if (!paint) paint = skin.gameObject.AddComponent<SkinnedPaintSurface>();
                paint.skin = skin;
            }
            var towel = GameObject.Find("方巾_擦除口红");
            if (!towel)
            {
                towel = GameObject.CreatePrimitive(PrimitiveType.Cube); towel.name = "方巾_擦除口红";
                towel.transform.position = new Vector3(-.13f, .77f, 4.23f);
                towel.transform.localScale = new Vector3(.16f, .008f, .16f);
                const string path = "Assets/_Project/Props/口红/擦拭方巾材质.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor", new Color(.82f, .91f, .94f));
                    mat.SetFloat("_Smoothness", .08f); AssetDatabase.CreateAsset(mat, path);
                }
                towel.GetComponent<Renderer>().sharedMaterial = mat;
                var body = towel.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false; body.mass = .03f;
                var grab = towel.AddComponent<XRGrabInteractable>(); grab.throwOnDetach = false; grab.useDynamicAttach = true;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                towel.AddComponent<GrabbableProp>(); towel.AddComponent<LipstickEraser>();
                towel.AddComponent<PersistentPlacement>().stableId = Guid.NewGuid().ToString("N");
            }
            ContinuationRepair.ConfigurePrayer(npc.GetComponentInChildren<Animator>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("跪拜与绘画修复、方巾和触摸阶段一已保存；地图及人物保留");
        }
    }
}

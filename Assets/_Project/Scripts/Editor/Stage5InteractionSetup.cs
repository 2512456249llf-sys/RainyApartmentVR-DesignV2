using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR.Editor
{
    public static class Stage5InteractionSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [MenuItem("Rainy Apartment/配置门与灯交互")]
        public static void ConfigureInteractions()
        {
            EditorSceneManager.OpenScene(ScenePath);

            GameObject oldRoot = GameObject.Find("InteractionSystem");
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            Material darkMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Greybox_Stair.mat");
            Material markerMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Greybox_Marker.mat");
            markerMaterial.SetColor("_BaseColor", new Color(0.7f, 0.55f, 0.22f));
            EditorUtility.SetDirty(markerMaterial);

            GameObject root = new GameObject("InteractionSystem");
            CreateBathroomDoor(root.transform, darkMaterial);
            CreateLivingRoomLightSwitch(root.transform, markerMaterial);
            ConfigureDesktopInteraction();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath))
            {
                throw new System.InvalidOperationException("门与灯交互场景保存失败。");
            }

            Debug.Log("阶段 5：浴室门和客厅灯开关已配置，桌面按 E、VR 使用选择键交互。");
        }

        public static void ConfigureFromCommandLine()
        {
            ConfigureInteractions();
        }

        private static void CreateBathroomDoor(Transform parent, Material material)
        {
            GameObject door = new GameObject("BathroomDoorInteractable");
            door.transform.SetParent(parent);
            door.transform.position = new Vector3(2f, 0f, -3.55f);

            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.025f, 0.44f);
            collider.size = new Vector3(0.08f, 2.05f, 0.88f);
            door.AddComponent<XRSimpleInteractable>();
            door.AddComponent<DoorInteractable>();

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(door.transform);
            panel.transform.localPosition = new Vector3(0f, 1.025f, 0.44f);
            panel.transform.localScale = new Vector3(0.08f, 2.05f, 0.88f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(panel.GetComponent<BoxCollider>());

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "DoorHandle";
            handle.transform.SetParent(door.transform);
            handle.transform.localPosition = new Vector3(-0.08f, 1.05f, 0.78f);
            handle.transform.localScale = Vector3.one * 0.09f;
            handle.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(handle.GetComponent<SphereCollider>());
        }

        private static void CreateLivingRoomLightSwitch(Transform parent, Material material)
        {
            GameObject lightObject = new GameObject("LivingRoomInteractiveLight");
            lightObject.transform.SetParent(parent);
            lightObject.transform.position = new Vector3(-1.1f, 2.55f, 0.65f);
            Light roomLight = lightObject.AddComponent<Light>();
            roomLight.type = LightType.Point;
            roomLight.range = 7f;
            roomLight.intensity = 140f;
            roomLight.color = new Color(1f, 0.67f, 0.38f);
            roomLight.shadows = LightShadows.Soft;

            GameObject switchObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            switchObject.name = "LivingRoomLightSwitch";
            switchObject.transform.SetParent(parent);
            switchObject.transform.position = new Vector3(-0.55f, 1.25f, -2.08f);
            switchObject.transform.localScale = new Vector3(0.15f, 0.24f, 0.06f);
            Renderer switchRenderer = switchObject.GetComponent<MeshRenderer>();
            switchRenderer.sharedMaterial = material;

            switchObject.AddComponent<XRSimpleInteractable>();
            LightSwitch lightSwitch = switchObject.AddComponent<LightSwitch>();
            lightSwitch.Configure(roomLight, switchRenderer, true);
        }

        private static void ConfigureDesktopInteraction()
        {
            GameObject cameraObject = GameObject.Find("DesktopCamera");
            if (cameraObject == null)
            {
                throw new System.InvalidOperationException("找不到 DesktopCamera。");
            }

            if (cameraObject.GetComponent<DesktopInteractionRaycaster>() == null)
            {
                cameraObject.AddComponent<DesktopInteractionRaycaster>();
            }
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class Stage3DesktopSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [MenuItem("Rainy Apartment/配置桌面第一人称玩家")]
        public static void ConfigureDesktopPlayer()
        {
            ApartmentBuilder.BuildApartment();

            GameObject spawn = GameObject.Find("PlayerSpawn");
            if (spawn == null)
            {
                throw new System.InvalidOperationException("场景中缺少 PlayerSpawn。");
            }

            GameObject previewCamera = GameObject.Find("Scene Preview Camera");
            if (previewCamera != null)
            {
                Object.DestroyImmediate(previewCamera);
            }

            GameObject oldPlayer = GameObject.Find("DesktopPlayer");
            if (oldPlayer != null)
            {
                Object.DestroyImmediate(oldPlayer);
            }

            GameObject player = new GameObject("DesktopPlayer");
            player.transform.position = spawn.transform.position;
            player.transform.rotation = spawn.transform.rotation;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            GameObject cameraObject = new GameObject("DesktopCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();

            DesktopPlayerController movement = player.AddComponent<DesktopPlayerController>();
            movement.Configure(cameraObject.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath))
            {
                throw new System.InvalidOperationException("桌面玩家配置保存失败。");
            }

            Debug.Log("阶段 3：桌面第一人称玩家已配置。支持 WASD、鼠标视角、重力与楼梯碰撞。");
        }

        public static void ConfigureFromCommandLine()
        {
            ConfigureDesktopPlayer();
        }
    }
}

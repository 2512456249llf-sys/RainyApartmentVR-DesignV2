using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace RainyApartmentVR.Editor
{
    public static class PrototypeValidator
    {
        public const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [MenuItem("Rainy Apartment/运行原型总体验收")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateFromCommandLine()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            string[] issues = CollectIssues();
            if (issues.Length > 0)
            {
                string report = "原型总体验收失败：\n- " + string.Join("\n- ", issues);
                Debug.LogError(report);
                throw new System.InvalidOperationException(report);
            }

            Debug.Log("VR APARTMENT PROTOTYPE\nVALIDATION PASSED");
        }

        public static string[] CollectIssues()
        {
            var issues = new List<string>();
            if (!System.IO.File.Exists(ScenePath))
            {
                issues.Add("缺少主场景 Apartment.unity。");
                return issues.ToArray();
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            RequireObject("Apartment", issues);
            RequireObject("GroundFloor", issues);
            RequireObject("Stairs", issues);
            RequireObject("DesktopPlayer", issues);
            RequireObject("XROrigin_PCVR", issues);
            RequireObject("RainyNightAtmosphere", issues);
            RequireObject("RainFX", issues);
            RequireObject("PlaceholderFurniture", issues);

            string[] rooms = { "Entry", "LivingRoom", "Kitchen", "Bathroom", "Landing", "Bedroom1", "Bedroom2" };
            foreach (string room in rooms)
            {
                RequireObject(room, issues);
            }

            GameObject desktopPlayer = GameObject.Find("DesktopPlayer");
            if (desktopPlayer != null && desktopPlayer.GetComponent<CharacterController>() == null)
            {
                issues.Add("桌面玩家缺少 CharacterController。");
            }

            GameObject xrRig = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "XROrigin_PCVR");
            if (xrRig != null)
            {
                Transform[] xrChildren = xrRig.GetComponentsInChildren<Transform>(true);
                foreach (string child in new[] { "Main Camera", "Left Controller", "Right Controller", "Move", "Turn" })
                {
                    if (!xrChildren.Any(item => item.name == child))
                    {
                        issues.Add($"VR 玩家缺少 {child}。");
                    }
                }
            }

            if (Object.FindObjectsByType<PlayerModeBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                issues.Add("缺少桌面与 VR 自动切换组件。");
            }

            DoorInteractable[] doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!doors.Any(door => door.GetComponent<XRSimpleInteractable>() != null))
            {
                issues.Add("缺少可由 VR 手柄操作的门。");
            }

            LightSwitch[] switches = Object.FindObjectsByType<LightSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!switches.Any(item => item.GetComponent<XRSimpleInteractable>() != null))
            {
                issues.Add("缺少可由 VR 手柄操作的灯开关。");
            }

            GameObject rain = GameObject.Find("RainFX");
            if (rain != null && rain.GetComponent<ParticleSystem>() == null)
            {
                issues.Add("雨景对象缺少粒子系统。");
            }

            GameObject furniture = GameObject.Find("PlaceholderFurniture");
            if (furniture != null && furniture.GetComponentsInChildren<Renderer>(true).Length < 70)
            {
                issues.Add("占位家具数量不足。");
            }

            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (generalSettings == null || !generalSettings.Manager.activeLoaders.Any(loader =>
                    loader != null && loader.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader"))
            {
                issues.Add("Windows 未配置 OpenXR 加载器。");
            }

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            OculusTouchControllerProfile touch = openXr?.GetFeature<OculusTouchControllerProfile>();
            if (touch == null || !touch.enabled)
            {
                issues.Add("OpenXR 未启用 Meta Quest Touch 手柄配置。");
            }

            if (!EditorBuildSettings.scenes.Any(item => item.enabled && item.path == ScenePath))
            {
                issues.Add("主场景未加入 Windows 构建列表。");
            }

            foreach (GameObject item in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item);
                if (missingCount > 0)
                {
                    issues.Add($"对象 {item.name} 存在 {missingCount} 个丢失脚本。");
                }
            }

            return issues.Distinct().ToArray();
        }

        private static void RequireObject(string name, ICollection<string> issues)
        {
            bool exists = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Any(item => item.name == name);
            if (!exists)
            {
                issues.Add($"场景缺少对象：{name}。");
            }
        }
    }
}

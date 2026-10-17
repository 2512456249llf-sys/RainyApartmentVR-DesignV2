using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace RainyApartmentVR.Editor
{
    public static class Stage4PCVRSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        private const string RigPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string LoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string XrSettingsAssetPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem("Rainy Apartment/配置 PC VR OpenXR")]
        public static void ConfigurePcVr()
        {
            ConfigureOpenXrForWindows();
            ConfigureSceneRig();
            ApplyAutomaticValidationFixes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("阶段 4：OpenXR PC VR、双手柄、连续移动和转向已配置，桌面模式保留。");
        }

        public static void ConfigureFromCommandLine()
        {
            ConfigurePcVr();
        }

        private static void ConfigureOpenXrForWindows()
        {
            EnsureFolder("Assets/XR");
            EnsureFolder("Assets/XR/Settings");

            XRGeneralSettingsPerBuildTarget perBuildTarget =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsAssetPath);
            if (perBuildTarget == null)
            {
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuildTarget, XrSettingsAssetPath);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTarget, true);
            }

            const BuildTargetGroup group = BuildTargetGroup.Standalone;
            if (!perBuildTarget.HasSettingsForBuildTarget(group))
            {
                perBuildTarget.CreateDefaultSettingsForBuildTarget(group);
            }

            if (!perBuildTarget.HasManagerSettingsForBuildTarget(group))
            {
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            XRGeneralSettings generalSettings = perBuildTarget.SettingsForBuildTarget(group);
            generalSettings.InitManagerOnStart = true;
            EditorUtility.SetDirty(generalSettings);

            if (!XRPackageMetadataStore.IsLoaderAssigned(LoaderTypeName, group))
            {
                bool assigned = XRPackageMetadataStore.AssignLoader(generalSettings.Manager, LoaderTypeName, group);
                if (!assigned)
                {
                    throw new System.InvalidOperationException("无法为 Windows 分配 OpenXR Loader。");
                }
            }

            FeatureHelpers.RefreshFeatures(group);
            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (openXrSettings == null)
            {
                throw new System.InvalidOperationException("无法创建 Windows OpenXR 设置。");
            }

            EnableFeature<OculusTouchControllerProfile>(openXrSettings);
            EnableFeature<ValveIndexControllerProfile>(openXrSettings);
            EnableFeature<HTCViveControllerProfile>(openXrSettings);
            EditorUtility.SetDirty(openXrSettings);
        }

        private static void ConfigureSceneRig()
        {
            EditorSceneManager.OpenScene(ScenePath);

            GameObject spawn = GameObject.Find("PlayerSpawn");
            GameObject desktopPlayer = GameObject.Find("DesktopPlayer");
            if (spawn == null || desktopPlayer == null)
            {
                throw new System.InvalidOperationException("场景中缺少 PlayerSpawn 或 DesktopPlayer。");
            }

            GameObject previousRig = GameObject.Find("XROrigin_PCVR");
            if (previousRig != null)
            {
                Object.DestroyImmediate(previousRig);
            }

            GameObject previousManager = GameObject.Find("PlayerModeManager");
            if (previousManager != null)
            {
                Object.DestroyImmediate(previousManager);
            }

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null)
            {
                throw new System.InvalidOperationException("找不到官方 XR Origin 起步预制体。");
            }

            GameObject rig = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
            if (rig == null)
            {
                throw new System.InvalidOperationException("XR Origin 实例化失败。");
            }

            rig.name = "XROrigin_PCVR";
            rig.transform.position = spawn.transform.position;
            rig.transform.rotation = spawn.transform.rotation;

            DisableOptionalLocomotion(rig, "Teleportation", "Climb", "Climb Teleport", "Grab Move", "Jump");
            DisableContinuousTurning(rig);

            GameObject managerObject = new GameObject("PlayerModeManager");
            PlayerModeBootstrap bootstrap = managerObject.AddComponent<PlayerModeBootstrap>();
            bootstrap.Configure(desktopPlayer, rig);

            // 场景文件默认保留桌面入口；运行时检测到 OpenXR 后再自动切换。
            rig.SetActive(false);
            desktopPlayer.SetActive(true);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath))
            {
                throw new System.InvalidOperationException("PC VR 场景配置保存失败。");
            }
        }

        private static void DisableOptionalLocomotion(GameObject rig, params string[] names)
        {
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (names.Contains(child.name))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void DisableContinuousTurning(GameObject rig)
        {
            foreach (Behaviour behaviour in rig.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour.GetType().Name.Contains("ContinuousTurnProvider"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void ApplyAutomaticValidationFixes()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Standalone);

            foreach (OpenXRFeature.ValidationRule issue in issues)
            {
                if (issue.fixItAutomatic && issue.fixIt != null)
                {
                    issue.fixIt.Invoke();
                }
            }

            issues.Clear();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Standalone);
            foreach (OpenXRFeature.ValidationRule issue in issues.Where(item => item.error))
            {
                Debug.LogError("OpenXR 验证未通过：" + issue.message);
            }
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            T feature = settings.GetFeature<T>();
            if (feature == null)
            {
                throw new System.InvalidOperationException($"找不到 OpenXR 功能：{typeof(T).Name}。");
            }

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string folder = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

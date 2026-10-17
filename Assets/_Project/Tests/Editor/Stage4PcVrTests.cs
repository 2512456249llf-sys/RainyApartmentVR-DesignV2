using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage4PcVrTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [OneTimeSetUp]
        public void 打开已配置的公寓场景()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void 官方正式版组件已经安装()
        {
            string manifest = File.ReadAllText("Packages/manifest.json");
            StringAssert.Contains("\"com.unity.xr.interaction.toolkit\": \"3.3.0\"", manifest);
            StringAssert.Contains("\"com.unity.xr.management\": \"4.5.3\"", manifest);
            StringAssert.Contains("\"com.unity.xr.openxr\": \"1.16.1\"", manifest);

            Assert.That(File.Exists(
                "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/XRI Default Input Actions.inputactions"),
                Is.True);
        }

        [Test]
        public void Windows已经分配OpenXR加载器()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.InitManagerOnStart, Is.True);
            Assert.That(settings.Manager.activeLoaders.Any(loader =>
                loader != null && loader.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader"), Is.True);

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            Assert.That(openXr, Is.Not.Null);
            OculusTouchControllerProfile touch = openXr.GetFeature<OculusTouchControllerProfile>();
            Assert.That(touch, Is.Not.Null);
            Assert.That(touch.enabled, Is.True);
        }

        [Test]
        public void 场景包含双手柄移动和瞬移转向配置()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject rig = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "XROrigin_PCVR");
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.activeSelf, Is.False, "场景默认应保留桌面入口，由运行时检测 VR 后启用。");

            Transform[] transforms = rig.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.Any(item => item.name == "Main Camera"), Is.True);
            Assert.That(transforms.Any(item => item.name == "Left Controller"), Is.True);
            Assert.That(transforms.Any(item => item.name == "Right Controller"), Is.True);
            Assert.That(transforms.Any(item => item.name == "Move"), Is.True);
            Assert.That(transforms.Any(item => item.name == "Turn"), Is.True);

            Behaviour[] behaviours = rig.GetComponentsInChildren<Behaviour>(true);
            Assert.That(behaviours.Any(item => HasTypeInHierarchy(item.GetType(), "ContinuousMoveProvider")), Is.True);
            Assert.That(behaviours.Any(item => item.GetType().Name == "SnapTurnProvider" && item.enabled), Is.True);
            Assert.That(behaviours.Any(item => item.GetType().Name == "ContinuousTurnProvider" && item.enabled), Is.False);
        }

        [Test]
        public void 高级移动功能保持关闭()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject rig = scene.GetRootGameObjects().First(item => item.name == "XROrigin_PCVR");
            string[] disabledNames = { "Teleportation", "Climb", "Climb Teleport", "Grab Move", "Jump" };

            foreach (string objectName in disabledNames)
            {
                Transform item = rig.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child => child.name == objectName);
                Assert.That(item, Is.Not.Null, $"官方预制体中缺少 {objectName}。");
                Assert.That(item.gameObject.activeSelf, Is.False, $"{objectName} 第一版必须关闭。");
            }
        }

        private static bool HasTypeInHierarchy(System.Type type, string typeName)
        {
            while (type != null)
            {
                if (type.Name == typeName)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
    }
}

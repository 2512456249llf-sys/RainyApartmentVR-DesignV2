using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class WindowsPrototypeBuilder
    {
        private const string OutputFolder = "Builds/Windows";
        private const string ExecutablePath = OutputFolder + "/RainyApartmentVR.exe";

        [MenuItem("Rainy Apartment/构建 Windows PC VR 原型")]
        public static void BuildFromMenu()
        {
            BuildWindowsPrototype();
        }

        public static void BuildFromCommandLine()
        {
            BuildWindowsPrototype();
        }

        private static void BuildWindowsPrototype()
        {
            PrototypeValidator.ValidateOrThrow();
            EditorSceneManager.SaveOpenScenes();

            Directory.CreateDirectory(OutputFolder);
            PlayerSettings.companyName = "Rainy Apartment Prototype";
            PlayerSettings.productName = "Rainy Apartment VR";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeValidator.ScenePath },
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Windows 原型构建失败：{report.summary.result}，错误数 {report.summary.totalErrors}。");
            }

            if (!File.Exists(ExecutablePath) || !Directory.Exists(OutputFolder + "/RainyApartmentVR_Data"))
            {
                throw new BuildFailedException("构建过程结束，但没有找到完整的 Windows 游戏文件。");
            }

            File.WriteAllText(OutputFolder + "/使用说明.txt",
                "雨夜复式公寓 PC VR 原型\r\n\r\n" +
                "桌面测试：双击 RainyApartmentVR.exe；WASD 移动，鼠标观察，E 键操作门和灯。\r\n\r\n" +
                "Quest 3 + Steam Link：\r\n" +
                "1. 在电脑启动 Steam 和 SteamVR。\r\n" +
                "2. 在 Quest 3 打开 Steam Link 并连接这台电脑。\r\n" +
                "3. 双击 RainyApartmentVR.exe。\r\n" +
                "4. 左摇杆连续移动，右摇杆分段转向；手柄射线可操作浴室门和客厅灯。\r\n\r\n" +
                "若没有连接 VR 设备，游戏会自动进入桌面测试模式。\r\n");

            Debug.Log($"阶段 8：Windows x64 原型构建成功。大小：{report.summary.totalSize / (1024f * 1024f):F1} MB。\n{ExecutablePath}");
        }
    }
}

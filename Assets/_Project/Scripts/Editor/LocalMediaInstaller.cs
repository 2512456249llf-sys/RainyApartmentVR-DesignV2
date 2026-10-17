using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RainyApartmentVR.Editor
{
    public static class LocalMediaInstaller
    {
        [MenuItem("雨夜公寓/检查本地电视与媒体目录")]
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            if (!all.Any(t => t.name == "电视屏幕" && t.GetComponent<Renderer>())) throw new InvalidOperationException("现有电视屏幕缺失。");
            if (!all.Any(t => t.name == "RainAudioSource" && t.GetComponent<AudioSource>())) throw new InvalidOperationException("独立雨声音源缺失。");
            if (!Resources.Load<Shader>("电视屏幕")) throw new InvalidOperationException("电视无光照显示材质缺失。");
            LocalMediaCatalog.EnsureFolders(Path.GetFullPath("VR_Media"));
            Debug.Log("本地电视接入检查通过；运行时在原电视上安装播放器，不重建场景。");
        }
        [MenuItem("雨夜公寓/构建本地电视试玩版")]
        public static void Build()
        {
            Verify();
            NaturalActionInstaller.BuildRealtimeDemo();
            string root = Path.GetFullPath("Builds/DesignV2_Realtime/VR_Media");
            LocalMediaCatalog.EnsureFolders(root);
            File.WriteAllText(Path.Combine(root,"放入媒体文件说明.txt"),
                "把文件拖进这里，进入游戏后电视会自动更新，也可以按电视的刷新按钮。\r\n"+
                "视频也可直接放在此目录；电影放 Movies；音乐放 Music；照片放 Photos。\r\n"+
                "短剧放 Drama/剧名/01.mp4、02.mp4，按数字集数排列。\r\n"+
                "瞄准电视按钮按 E；头显使用现有手柄射线选择按钮。\r\n"+
                "雨声开关位于电视右侧，只控制背景雨声。\r\n"+
                "播放记录、音量和雨声状态保存到本机，继续上次可恢复播放位置。\r\n");
            Debug.Log("电视本地媒体目录已创建："+root);
        }
    }
}

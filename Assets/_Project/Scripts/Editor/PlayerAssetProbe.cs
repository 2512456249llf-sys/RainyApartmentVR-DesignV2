using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class PlayerAssetProbe
    {
        public static void Inspect()
        {
            AssetDatabase.Refresh();
            foreach (string path in new[] { "Assets/_Project/PlayerAvatar/群青路.vrm", "Assets/_Project/Props/粉色猪沙发/粉色猪懒人沙发.glb" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidOperationException("模型导入失败：" + path);
                var instance = UnityEngine.Object.Instantiate(prefab);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Debug.Log("导入模型：" + path + "；尺寸=" + bounds.size + "；中心=" + bounds.center + "；渲染网格=" + renderers.Length);
                foreach (var renderer in renderers) Debug.Log("模型部件：" + renderer.name + "；尺寸=" + renderer.bounds.size + "；材质=" + string.Join("、", renderer.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name : "缺失")));
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator) Debug.Log("玩家骨架：人形=" + animator.isHuman + "；左手=" + animator.GetBoneTransform(HumanBodyBones.LeftHand).position + "；头=" + animator.GetBoneTransform(HumanBodyBones.Head).position);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name.Contains("沙发") || node.name.Contains("茶几") || node.name.Contains("大厅") || node.name.Contains("Camera") || node.name.Contains("Controller") || node.name == "XROrigin_PCVR")
                        Debug.Log("现有场景节点：" + node.name + "；位置=" + node.position + "；朝向=" + node.eulerAngles + "；启用=" + node.gameObject.activeSelf);
        }
    }
}

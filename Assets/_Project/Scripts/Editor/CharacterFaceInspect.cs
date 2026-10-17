using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class CharacterFaceInspect
    {
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "男主_雨夜公寓");
            if (!character) throw new System.InvalidOperationException("找不到男主");
            int total = 0;
            foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (!mesh || mesh.blendShapeCount == 0) continue;
                total += mesh.blendShapeCount;
                var names = new List<string>();
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    names.Add(mesh.GetBlendShapeName(i));
                Debug.Log("面部网格【" + renderer.name + "】共 " + mesh.blendShapeCount + " 个表情键：\n" + string.Join("\n", names));
            }
            Debug.Log("面部表情键合计：" + total);
        }
    }
}

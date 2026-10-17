using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RainyApartmentVR.Editor
{
    public static class PerformanceTuner
    {
        [MenuItem("雨夜公寓/给 VR 降负载并装卡顿记录仪")]
        public static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (!asset) throw new System.InvalidOperationException("找不到 URP 管线资源 PC_RPAsset.asset");
            var so = new SerializedObject(asset);
            Set(so, "m_RenderScale", 0.9f);
            Set(so, "m_ShadowDistance", 18f);
            Set(so, "m_ShadowCascadeCount", 1);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_MainLightShadowmapResolution", 1024);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log("URP 已降负载：渲染比例 0.9，阴影距离 18m，软阴影关，阴影图 1024。");

            QualitySettings.shadowDistance = 18f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.antiAliasing = 0;
            QualitySettings.pixelLightCount = 2;

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var existing = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<PerformanceWatchdog>(true))
                .FirstOrDefault();
            if (!existing)
            {
                var go = new GameObject("性能监视");
                go.AddComponent<PerformanceWatchdog>();
                Debug.Log("卡顿记录仪已加入场景。");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("VR 降负载完成，场景已保存。");
        }

        private static void Set(SerializedObject so, string name, float value)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("URP 资源里没有字段：" + name);
                return;
            }
            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
            else property.intValue = Mathf.RoundToInt(value);
        }

        private static void Set(SerializedObject so, string name, bool value)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("URP 资源里没有字段：" + name);
                return;
            }
            property.boolValue = value;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class HandInspect
    {
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var hands = Object.FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log("虚拟手组件数量：" + hands.Length);
            foreach (var hand in hands)
            {
                var t = hand.transform;
                var chain = new List<string>();
                var walk = t;
                while (walk != null)
                {
                    chain.Add(walk.name);
                    walk = walk.parent;
                }
                Debug.Log("虚拟手【" + t.name + "】层级=" + string.Join(" < ", chain) +
                          " || localPos=" + t.localPosition.ToString("F4") +
                          " localRot=" + t.localEulerAngles.ToString("F1") +
                          " localScale=" + t.localScale.ToString("F3") +
                          " worldPos=" + t.position.ToString("F3"));
                foreach (var r in hand.GetComponentsInChildren<Renderer>(true))
                {
                    Debug.Log("  手部网格：" + r.name + " localBounds=" + r.localBounds.ToString("F4") +
                              " worldBounds=" + r.bounds.ToString("F4"));
                }
            }
        }
    }
}

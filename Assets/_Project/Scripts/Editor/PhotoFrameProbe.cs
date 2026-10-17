using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class PhotoFrameProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var item = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == PhotoFrameInstaller.ObjectName);
            if (!item) throw new InvalidOperationException("没有找到相框。");
            Debug.Log("相框位置=" + item.position + " 旋转=" + item.eulerAngles + " 缩放=" + item.lossyScale);
            var filter = item.GetComponentInChildren<MeshFilter>(true);
            var mesh = filter ? filter.sharedMesh : null;
            if (!mesh) throw new InvalidOperationException("相框网格缺失。");
            var vertices = mesh.vertices;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                if (triangles.Length == 0) { Debug.Log("相框子网格 " + sub + " 已移除穿插支架"); continue; }
                var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
                foreach (var index in triangles) bounds.Encapsulate(vertices[index]);
                var normal = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]], vertices[triangles[2]] - vertices[triangles[0]]).normalized;
                Debug.Log("相框子网格 " + sub + " 顶点=" + triangles.Distinct().Count() + " 范围=" + bounds + " 首面法线=" + normal);
            }
            var cameraObject = new GameObject("临时相框验收相机");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .16f, .18f);
            Capture(camera, "相框_正面", item.position + item.up * .48f + Vector3.up * .05f, item.position);
            Capture(camera, "相框_背面", item.position - item.up * .48f + Vector3.up * .05f, item.position);
            Capture(camera, "相框_侧面", item.position + item.right * .48f + Vector3.up * .05f, item.position);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        private static void Capture(Camera camera, string name, Vector3 position, Vector3 target)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var render = new RenderTexture(900, 900, 24);
            var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/" + name + ".png", texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(render);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

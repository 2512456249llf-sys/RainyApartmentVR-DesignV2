using System.Collections.Generic;
using UnityEngine;

namespace RainyApartmentVR
{
    // 只在绘画时烘焙脸部网格并检查皮肤三角面，不用浮在皮肤外的触摸球代替脸。
    public sealed class FacePaintSurface : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        private Mesh baked;
        private Vector3[] vertices;
        private readonly List<int> faceTriangles = new List<int>();
        private float nextBake;
        public DrawingSurface Surface => GetComponent<DrawingSurface>();
        private void Bake()
        {
            if (!skin || !skin.sharedMesh) return;
            if (!baked) baked = new Mesh { name = "临时脸部绘画网格" };
            skin.BakeMesh(baked);
            vertices = baked.vertices;
            if (faceTriangles.Count == 0)
            {
                var triangles = baked.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var center = skin.transform.TransformPoint((vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3);
                    if (Vector3.Distance(center, transform.position + Vector3.up * .05f) < .24f)
                    { faceTriangles.Add(triangles[i]); faceTriangles.Add(triangles[i + 1]); faceTriangles.Add(triangles[i + 2]); }
                }
            }
        }
        public bool Raycast(Ray ray, float distance, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            if (!skin || Vector3.Cross(ray.direction, transform.position - ray.origin).magnitude > .3f) return false;
            if (!baked || Time.unscaledTime >= nextBake) { Bake(); nextBake = Time.unscaledTime + .06f; }
            if (vertices == null) return false;
            var origin = skin.transform.InverseTransformPoint(ray.origin);
            var direction = skin.transform.InverseTransformVector(ray.direction);
            bool found = false;
            float best = distance;
            for (int i = 0; i < faceTriangles.Count; i += 3)
            {
                var a = vertices[faceTriangles[i]];
                var edge1 = vertices[faceTriangles[i + 1]] - a;
                var edge2 = vertices[faceTriangles[i + 2]] - a;
                var p = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < .00000001f) continue;
                var s = origin - a;
                float u = Vector3.Dot(s, p) / determinant;
                if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(s, edge1);
                float v = Vector3.Dot(direction, q) / determinant;
                if (v < 0 || u + v > 1) continue;
                float t = Vector3.Dot(edge2, q) / determinant;
                if (t < 0 || t >= best) continue;
                best = t;
                point = ray.GetPoint(t);
                normal = skin.transform.TransformDirection(Vector3.Cross(edge1, edge2).normalized).normalized;
                if (Vector3.Dot(normal, ray.direction) > 0) normal = -normal;
                found = true;
            }
            return found;
        }
        private void OnDestroy() { if (baked) Destroy(baked); }
    }
}

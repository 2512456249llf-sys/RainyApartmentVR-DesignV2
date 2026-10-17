using System;
using UnityEngine;

namespace RainyApartmentVR
{
    // 笔迹绑定真实三角面和重心坐标，身体弯曲时重新计算皮肤位置。
    public sealed class SkinnedPaintSurface : MonoBehaviour
    {
        [Serializable] public sealed class Binding
        {
            public int a, b, c;
            public Vector3 barycentric;
            public float offset = .0018f;
        }
        public SkinnedMeshRenderer skin;
        private Mesh baked;
        private Vector3[] vertices;
        private int[] triangles;
        private int bakedFrame = -1, finalFrame = -1;
        public DrawingSurface Surface => GetComponent<DrawingSurface>();
        private bool Bake(bool final)
        {
            if (!skin || !skin.sharedMesh) return false;
            if (bakedFrame == Time.frameCount && (!final || finalFrame == Time.frameCount)) return vertices != null;
            if (!baked) baked = new Mesh { name = "身体绘画临时网格" };
            skin.BakeMesh(baked);
            vertices = baked.vertices;
            if (triangles == null) triangles = baked.triangles;
            bakedFrame = Time.frameCount;
            if (final) finalFrame = Time.frameCount;
            return vertices != null;
        }
        public bool Raycast(Ray ray, float distance, out Vector3 point, out Vector3 normal, out Binding binding)
        {
            point = normal = Vector3.zero; binding = null;
            if (!skin) return false;
            var bounds = skin.bounds; bounds.Expand(.08f);
            if (!bounds.Contains(ray.origin) && (!bounds.IntersectRay(ray, out float entry) || entry > distance)) return false;
            if (!Bake(false)) return false;
            Vector3 origin = skin.transform.InverseTransformPoint(ray.origin);
            Vector3 direction = skin.transform.InverseTransformVector(ray.direction);
            float best = distance;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 edge1 = vertices[b] - vertices[a], edge2 = vertices[c] - vertices[a];
                Vector3 p = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < .00000001f) continue;
                Vector3 s = origin - vertices[a]; float u = Vector3.Dot(s, p) / determinant;
                if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(s, edge1); float v = Vector3.Dot(direction, q) / determinant;
                if (v < 0 || u + v > 1) continue;
                float t = Vector3.Dot(edge2, q) / determinant;
                if (t < 0 || t >= best) continue;
                best = t; point = ray.GetPoint(t);
                normal = skin.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(Vector3.Cross(edge1, edge2)).normalized;
                float sign = Vector3.Dot(normal, ray.direction) > 0 ? -1 : 1;
                normal *= sign;
                binding = new Binding { a = a, b = b, c = c, barycentric = new Vector3(1 - u - v, u, v), offset = sign * .0018f };
            }
            return binding != null;
        }
        public bool TryPoint(Binding binding, out Vector3 point)
        {
            point = Vector3.zero;
            if (binding == null || !Bake(true) || binding.a < 0 || binding.b < 0 || binding.c < 0 ||
                binding.a >= vertices.Length || binding.b >= vertices.Length || binding.c >= vertices.Length) return false;
            Vector3 a = vertices[binding.a], b = vertices[binding.b], c = vertices[binding.c];
            Vector3 normal = skin.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(Vector3.Cross(b - a, c - a)).normalized;
            point = skin.transform.TransformPoint(a * binding.barycentric.x + b * binding.barycentric.y + c * binding.barycentric.z) + normal * binding.offset;
            return true;
        }
        private void OnDestroy() { if (baked) Destroy(baked); }
    }
}

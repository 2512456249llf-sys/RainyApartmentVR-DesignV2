using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RainyApartmentVR.Editor
{
    // 在 Unity 内直接生成带倒角、贴图坐标和法线的可持久化网格。
    public static class DesignMesh
    {
        public const string Folder = "Assets/_Project/DesignV2/Meshes";
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        public static void ClearCache() => Cache.Clear();
        public static Mesh Save(string key, List<Vector3> v, List<int> t, List<Vector2> uv, List<Vector3> n = null)
        {
            string path = Folder + "/" + key + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = key;
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetUVs(0, uv);
            if(n == null) mesh.RecalculateNormals(); else mesh.SetNormals(n);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh); Cache[key] = mesh; return mesh;
        }
        public static Mesh Rounded(Vector3 size, float radius)
        {
            string key = $"倒角_{size.x:F3}_{size.y:F3}_{size.z:F3}_{radius:F3}";
            if(Cache.TryGetValue(key, out Mesh cached)) return cached;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>(); var n = new List<Vector3>();
            Vector3 half = size * .5f;
            float r = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .95f);
            Vector3 inner = half - Vector3.one * r;
            Vector3[] normals = {Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back};
            foreach(Vector3 normal in normals)
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(normal, u);
                float hu = Vector3.Dot(half, new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                float hw = Vector3.Dot(half, new Vector3(Mathf.Abs(w.x),Mathf.Abs(w.y),Mathf.Abs(w.z)));
                float hn = Mathf.Abs(Vector3.Dot(half, normal));
                // 贴近边缘分段，保持平面部分平整，边缘呈圆弧。
                float[] a = {-1,-1+r/hu*.29f,-1+r/hu,0,1-r/hu,1-r/hu*.29f,1};
                float[] b = {-1,-1+r/hw*.29f,-1+r/hw,0,1-r/hw,1-r/hw*.29f,1};
                int start = v.Count;
                for(int y=0;y<7;y++) for(int x=0;x<7;x++)
                {
                    Vector3 p = normal*hn + u*a[x]*hu + w*b[y]*hw;
                    Vector3 q = new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 outward = (p-q).normalized;
                    v.Add(q+outward*r); n.Add(outward); uv.Add(new Vector2(a[x]*hu,b[y]*hw));
                }
                for(int y=0;y<6;y++) for(int x=0;x<6;x++)
                {int i=start+y*7+x; t.AddRange(new[]{i,i+1,i+8,i,i+8,i+7});}
            }
            return Save(key,v,t,uv,n);
        }
        public static Mesh Lathe(string key, Vector2[] profile, int steps = 32)
        {
            if(Cache.TryGetValue(key,out Mesh cached)) return cached;
            var v=new List<Vector3>(); var t=new List<int>(); var uv=new List<Vector2>();
            for(int y=0;y<profile.Length;y++) for(int x=0;x<=steps;x++)
            {float a=x*Mathf.PI*2/steps; v.Add(new Vector3(Mathf.Cos(a)*profile[y].x,profile[y].y,Mathf.Sin(a)*profile[y].x)); uv.Add(new Vector2((float)x/steps,profile[y].y));}
            for(int y=0;y<profile.Length-1;y++) for(int x=0;x<steps;x++)
            {int i=y*(steps+1)+x; t.AddRange(new[]{i,i+steps+1,i+1,i+1,i+steps+1,i+steps+2});}
            return Save(key,v,t,uv);
        }
        public static Mesh Curtain(string key,float width,float height)
        {
            var v=new List<Vector3>(); var t=new List<int>(); var uv=new List<Vector2>();
            const int nx=96,ny=16;
            for(int y=0;y<=ny;y++) for(int x=0;x<=nx;x++)
            {
                float u=(float)x/nx, a=(float)y/ny;
                float ripple=Mathf.Sin(u*Mathf.PI*22)*(.08f+.025f*(1-a));
                v.Add(new Vector3((u-.5f)*width,height*a + .025f*Mathf.Sin(u*69)*(1-a),ripple)); uv.Add(new Vector2(u*width,a*height));
            }
            for(int y=0;y<ny;y++) for(int x=0;x<nx;x++)
            {int i=y*(nx+1)+x; t.AddRange(new[]{i,i+1,i+nx+2,i,i+nx+2,i+nx+1});}
            return Save(key,v,t,uv);
        }
        public static Mesh Leaf(string key)
        {
            var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=12;i++)
            {float s=i/12f;float w=Mathf.Sin(s*Mathf.PI)*.11f;float y=Mathf.Sin(s*Mathf.PI)*.12f;
                v.Add(new Vector3(-w,y,s*.65f));v.Add(new Vector3(0,y+.025f,s*.65f));v.Add(new Vector3(w,y,s*.65f));
                uv.Add(new Vector2(0,s));uv.Add(new Vector2(.5f,s));uv.Add(new Vector2(1,s));}
            for(int i=0;i<12;i++)for(int j=0;j<2;j++){int a=i*3+j;t.AddRange(new[]{a,a+3,a+1,a+1,a+3,a+4});}
            return Save(key,v,t,uv);
        }
    }
}

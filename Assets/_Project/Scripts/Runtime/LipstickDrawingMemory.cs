using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RainyApartmentVR
{
    // 在人物骨骼最终姿态之后更新笔迹，避免头部牵动时笔迹留在旧位置。
    [DefaultExecutionOrder(13000)]
    public sealed class LipstickDrawingMemory : MonoBehaviour
    {
        [Serializable] public sealed class Stroke
        {
            public string surfaceId;
            public Color color = new Color(.72f, .025f, .09f, 1);
            public float width = .006f;
            public List<Vector3> points = new List<Vector3>();
            public List<SkinnedPaintSurface.Binding> bindings = new List<SkinnedPaintSurface.Binding>();
        }
        [Serializable] private sealed class Document { public int version = 1; public List<Stroke> strokes = new List<Stroke>(); }
        private sealed class VisibleStroke { public Stroke data; public Transform anchor; public LineRenderer line; public SkinnedPaintSurface skin; }
        public static LipstickDrawingMemory Instance { get; private set; }
        public Material strokeMaterial;
        private readonly List<Stroke> strokes = new List<Stroke>();
        private readonly List<VisibleStroke> visible = new List<VisibleStroke>();
        private readonly Dictionary<string, DrawingSurface> surfaces = new Dictionary<string, DrawingSurface>();
        private Material material;
        private bool dirty;
        private float nextSave;
        private string StoragePath => Path.Combine(Path.GetDirectoryName(ScenePlacementMemory.Instance.StoragePath), "口红笔迹.json");
        public int StrokeCount => strokes.Count;
        private bool StorageEnabled => ScenePlacementMemory.Instance && ScenePlacementMemory.Instance.StorageEnabled;
        private void Awake() { Instance = this; }
        private void Start()
        {
            foreach (var surface in FindObjectsByType<DrawingSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(surface.stableId)) surfaces[surface.stableId] = surface;
            material = strokeMaterial ? strokeMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (!strokeMaterial) material.SetColor("_BaseColor", new Color(.72f, .025f, .09f, 1));
            if (StorageEnabled)
            {
                try
                {
                    var path = File.Exists(StoragePath) ? StoragePath : StoragePath + ".bak";
                    if (File.Exists(path))
                    {
                        var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                        if (document != null && document.version == 1 && document.strokes != null)
                            foreach (var stroke in document.strokes)
                                if (stroke != null && stroke.points != null && stroke.points.Count > 0 && stroke.points.Count <= 2048)
                                { strokes.Add(stroke); if (surfaces.TryGetValue(stroke.surfaceId, out var surface)) CreateVisible(stroke, surface.Anchor); }
                    }
                }
                catch (Exception error) { Debug.LogWarning("口红笔迹读取失败，保留原文件：" + error.GetType().Name); }
                if (strokes.Count > 0) Debug.Log("口红笔迹恢复：" + strokes.Count + " 段");
            }
        }
        private void CreateVisible(Stroke data, Transform anchor)
        {
            var go = new GameObject("口红笔迹");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = data.width;
            line.startColor = line.endColor = data.color;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visible.Add(new VisibleStroke { data = data, anchor = anchor, line = line, skin = anchor.GetComponent<SkinnedPaintSurface>() });
        }
        public Stroke BeginStroke(DrawingSurface surface)
        {
            if (!surface || string.IsNullOrEmpty(surface.stableId) || strokes.Count >= 512) return null;
            var stroke = new Stroke { surfaceId = surface.stableId };
            strokes.Add(stroke);
            CreateVisible(stroke, surface.Anchor);
            dirty = true;
            return stroke;
        }
        public bool AddPoint(Stroke stroke, DrawingSurface surface, Vector3 point, SkinnedPaintSurface.Binding binding = null)
        {
            if (stroke == null || !strokes.Contains(stroke) || !surface || stroke.surfaceId != surface.stableId || stroke.points.Count >= 2048) return false;
            Vector3 local = surface.Anchor.InverseTransformPoint(point);
            if (stroke.points.Count > 0 && Vector3.Distance(surface.Anchor.TransformPoint(stroke.points[stroke.points.Count - 1]), point) < .002f) return true;
            stroke.points.Add(local);
            if (stroke.bindings == null) stroke.bindings = new List<SkinnedPaintSurface.Binding>();
            while (stroke.bindings.Count < stroke.points.Count - 1) stroke.bindings.Add(null);
            stroke.bindings.Add(binding);
            dirty = true;
            return true;
        }
        private void LateUpdate()
        {
            foreach (var item in visible)
            {
                if (!item.anchor || !item.line) continue;
                item.line.positionCount = item.data.points.Count;
                for (int i = 0; i < item.data.points.Count; i++) item.line.SetPosition(i, WorldPoint(item, i));
            }
            if (Time.unscaledTime > nextSave) { nextSave = Time.unscaledTime + 2f; SaveNow(); }
        }
        private Vector3 WorldPoint(VisibleStroke item, int index)
        {
            if (item.skin && item.data.bindings != null && index < item.data.bindings.Count && item.skin.TryPoint(item.data.bindings[index], out var point)) return point;
            return item.anchor.TransformPoint(item.data.points[index]);
        }
        public int EraseNear(Vector3 center, float radius)
        {
            if (radius <= 0 || radius > .25f) return 0;
            int erased = 0;
            for (int s = visible.Count - 1; s >= 0; s--)
            {
                var item = visible[s];
                if (!item.anchor || item.data.points.Count == 0) continue;
                int count = item.data.points.Count; var remove = new bool[count];
                Vector3 previous = WorldPoint(item, 0);
                remove[0] = Vector3.Distance(previous, center) <= radius + item.data.width * .5f;
                for (int i = 1; i < count; i++)
                {
                    Vector3 current = WorldPoint(item, i), edge = current - previous;
                    float t = edge.sqrMagnitude > .00000001f ? Mathf.Clamp01(Vector3.Dot(center - previous, edge) / edge.sqrMagnitude) : 0;
                    if (Vector3.Distance(previous + edge * t, center) <= radius + item.data.width * .5f) remove[i - 1] = remove[i] = true;
                    previous = current;
                }
                bool changed = false; foreach (bool cut in remove) if (cut) { changed = true; erased++; }
                if (!changed) continue;
                visible.RemoveAt(s); strokes.Remove(item.data); Destroy(item.line.gameObject);
                Stroke run = null;
                for (int i = 0; i < count; i++)
                {
                    if (remove[i]) { run = null; continue; }
                    if (run == null)
                    {
                        run = new Stroke { surfaceId = item.data.surfaceId, width = item.data.width, color = item.data.color };
                        strokes.Add(run); CreateVisible(run, item.anchor);
                    }
                    run.points.Add(item.data.points[i]);
                    run.bindings.Add(item.data.bindings != null && i < item.data.bindings.Count ? item.data.bindings[i] : null);
                }
                dirty = true;
            }
            return erased;
        }
        public void SaveNow()
        {
            if (!dirty || !StorageEnabled) return;
            try
            {
                var document = new Document { strokes = strokes.FindAll(s => s.points.Count > 0) };
                Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
                File.WriteAllText(StoragePath + ".tmp", JsonUtility.ToJson(document));
                if (File.Exists(StoragePath)) File.Replace(StoragePath + ".tmp", StoragePath, StoragePath + ".bak");
                else File.Move(StoragePath + ".tmp", StoragePath);
                dirty = false;
            }
            catch (Exception error) { Debug.LogWarning("口红笔迹保存失败：" + error.GetType().Name); }
        }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnDestroy() { SaveNow(); if (Instance == this) Instance = null; if (material && !strokeMaterial) Destroy(material); }
    }
}

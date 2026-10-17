using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RainyApartmentVR
{
    // 保存可移动物品的稳定编号和姿态，不改写场景文件或玩家的位置。
    [DefaultExecutionOrder(-500)]
    public sealed class ScenePlacementMemory : MonoBehaviour
    {
        [Serializable] public sealed class Item
        {
            public string id;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable] private sealed class Document
        {
            public int version = 1;
            public List<Item> items = new List<Item>();
        }
        public static string StoragePathOverride;
        public static ScenePlacementMemory Instance { get; private set; }
        private readonly Dictionary<string, Item> stored = new Dictionary<string, Item>();
        private readonly Dictionary<string, PersistentPlacement> live = new Dictionary<string, PersistentPlacement>();
        private float nextSave;
        private bool dirty;
        private string commandLinePath;
        public string StoragePath => StoragePathOverride ?? commandLinePath ?? Path.Combine(Application.persistentDataPath, "公寓摆放记忆.json");
        public bool StorageEnabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-noPlacementMemory") < 0
            && (!(Application.isEditor && Application.isBatchMode) || StoragePathOverride != null);

        private void Awake()
        {
            Instance = this;
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-placementMemoryPath");
            if (index >= 0 && index + 1 < arguments.Length) commandLinePath = Path.GetFullPath(arguments[index + 1]);
            if (StorageEnabled) Load();
        }
        private bool Read(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                if (document == null || document.version != 1 || document.items == null) return false;
                foreach (var item in document.items)
                    if (item != null && !string.IsNullOrEmpty(item.id) && Valid(item)) stored[item.id] = item;
                return true;
            }
            catch (Exception error) { Debug.LogWarning("摆放记忆读取失败，保留原文件：" + error.GetType().Name); return false; }
        }
        private void Load()
        {
            if (!Read(StoragePath)) Read(StoragePath + ".bak");
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Valid(Item i) => Finite(i.position.x) && Finite(i.position.y) && Finite(i.position.z)
            && Finite(i.rotation.x) && Finite(i.rotation.y) && Finite(i.rotation.z) && Finite(i.rotation.w)
            && Quaternion.Dot(i.rotation, i.rotation) > .5f && Quaternion.Dot(i.rotation, i.rotation) < 1.5f
            && Finite(i.scale.x) && Finite(i.scale.y) && Finite(i.scale.z)
            && i.scale.x > 0 && i.scale.y > 0 && i.scale.z > 0;

        public void Register(PersistentPlacement placement)
        {
            if (string.IsNullOrEmpty(placement.stableId)) { Debug.LogWarning("物品没有稳定编号，未启用记忆：" + placement.name); return; }
            if (live.TryGetValue(placement.stableId, out var old) && old && old != placement)
            { Debug.LogError("物品记忆编号重复：" + placement.name); return; }
            live[placement.stableId] = placement;
            if (stored.TryGetValue(placement.stableId, out var item))
            {
                placement.transform.SetPositionAndRotation(item.position, item.rotation.normalized);
                placement.transform.localScale = item.scale;
                var body = placement.GetComponent<Rigidbody>();
                if (body) { body.position = item.position; body.rotation = item.rotation.normalized; }
                if (commandLinePath != null) Debug.Log("跨进程摆放恢复：" + placement.name + " 位置=" + item.position + " 朝向=" + item.rotation.eulerAngles);
            }
            placement.RememberCurrent();
        }
        public void MarkChanged() { dirty = true; }
        private void Update()
        {
            if (Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 2f;
            SaveNow();
        }
        public void SaveNow()
        {
            if (!StorageEnabled) return;
            foreach (var pair in live)
            {
                var p = pair.Value;
                if (!p) continue;
                if (!p.HasChanged && stored.ContainsKey(pair.Key)) continue;
                var item = new Item { id = pair.Key, position = p.transform.position, rotation = p.transform.rotation, scale = p.transform.localScale };
                if (!Valid(item)) continue;
                stored[pair.Key] = item;
                p.RememberCurrent();
                dirty = true;
            }
            if (!dirty) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StoragePath));
                string temporary = StoragePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(new Document { items = new List<Item>(stored.Values) }, true));
                if (File.Exists(StoragePath)) File.Replace(temporary, StoragePath, StoragePath + ".bak");
                else File.Move(temporary, StoragePath);
                dirty = false;
            }
            catch (Exception error) { Debug.LogWarning("摆放记忆保存失败：" + error.GetType().Name); }
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnDestroy() { if (Instance == this) { SaveNow(); Instance = null; } }
    }
}

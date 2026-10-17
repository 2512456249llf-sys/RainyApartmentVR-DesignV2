using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace RainyApartmentVR
{
    [Serializable] public sealed class LocalMediaRecord
    {
        public string id;
        public double time;
        public long lastSeen;
    }
    [Serializable] public sealed class LocalMediaHistory
    {
        public int version = 1;
        public float volume = .55f;
        public bool rainMuted;
        public string lastMedia;
        public List<LocalMediaRecord> records = new List<LocalMediaRecord>();
        public double Position(string id) => records.Find(r => r.id == id)?.time ?? 0;
        public void Remember(string id, double seconds)
        {
            if (string.IsNullOrEmpty(id)) return;
            var record = records.Find(r => r.id == id);
            if (record == null) { record = new LocalMediaRecord { id = id }; records.Add(record); }
            record.time = double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : Math.Max(0, seconds);
            record.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); lastMedia = id;
            if (records.Count > 256) { records.Sort((a,b) => b.lastSeen.CompareTo(a.lastSeen)); records.RemoveRange(256, records.Count-256); }
        }
        public static LocalMediaHistory Load(string path)
        {
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    var value = JsonUtility.FromJson<LocalMediaHistory>(File.ReadAllText(candidate));
                    if (value != null && value.version == 1 && value.records != null)
                    {
                        value.records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id));
                        value.volume = float.IsNaN(value.volume) ? .55f : Mathf.Clamp01(value.volume);
                        return value;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { }
            }
            return new LocalMediaHistory();
        }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(this, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}

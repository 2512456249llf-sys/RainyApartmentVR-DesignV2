using System;
using System.Collections.Generic;
using System.IO;
namespace RainyApartmentVR
{
    public enum LocalMediaKind { Drama, Movies, Music, Photos }
    public sealed class LocalMediaEntry
    {
        public string id, path, title, series, coverPath;
        public LocalMediaKind kind;
    }
    // 目录扫描与播放解耦；编号相对于媒体根目录，移动整个游戏目录不丢历史。
    public static class LocalMediaCatalog
    {
        public static readonly string[] Folders = { "Drama", "Movies", "Music", "Photos" };
        public static void EnsureFolders(string root)
        {
            Directory.CreateDirectory(root);
            foreach (string folder in Folders) Directory.CreateDirectory(Path.Combine(root, folder));
        }
        public static List<LocalMediaEntry> Scan(string root)
        {
            var result = new List<LocalMediaEntry>();
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root)) return result;
            AddDirectory(root, root, null, 0, result, false);
            for (int i = 0; i < Folders.Length; i++)
                AddDirectory(root, Path.Combine(root, Folders[i]), (LocalMediaKind)i, 0, result, true);
            result.Sort((a,b) => { int kind = a.kind.CompareTo(b.kind); return kind != 0 ? kind : NaturalCompare(a.id, b.id); });
            return result;
        }
        private static void AddDirectory(string root, string directory, LocalMediaKind? category, int depth, List<LocalMediaEntry> result, bool recurse)
        {
            if (depth > 8 || result.Count >= 3000 || !Directory.Exists(directory)) return;
            try
            {
                // 避免目录联接形成循环或把媒体目录外的大量文件纳入列表。
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                foreach (string file in Directory.GetFiles(directory))
                {
                    if (result.Count >= 3000) break;
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file).Length == 0) continue;
                    var kind = Classify(Path.GetExtension(file));
                    if (kind == null || (category.HasValue && kind != category && !(kind == LocalMediaKind.Movies && category == LocalMediaKind.Drama))) continue;
                    string id = file.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                    var actualKind = category ?? kind.Value;
                    result.Add(new LocalMediaEntry { id = id, path = file, kind = actualKind, title = Path.GetFileNameWithoutExtension(file),
                        coverPath = FindCover(file,actualKind), series = actualKind == LocalMediaKind.Drama ? Path.GetFileName(Path.GetDirectoryName(file)) : "" });
                }
                if (recurse) foreach (string child in Directory.GetDirectories(directory)) AddDirectory(root, child, category, depth + 1, result, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        private static string FindCover(string path,LocalMediaKind kind)
        {
            if (kind == LocalMediaKind.Photos) return path;
            string folder = Path.GetDirectoryName(path), stem = Path.GetFileNameWithoutExtension(path);
            foreach (string name in new[] { stem+".jpg", stem+".png", "cover.jpg", "cover.png" })
            { string candidate = Path.Combine(folder,name); if (File.Exists(candidate)) return candidate; }
            return "";
        }
        public static LocalMediaKind? Classify(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".mp4": case ".m4v": case ".mov": case ".webm": case ".wmv": return LocalMediaKind.Movies;
                case ".mp3": case ".wav": case ".ogg": return LocalMediaKind.Music;
                case ".jpg": case ".jpeg": case ".png": return LocalMediaKind.Photos;
                default: return null;
            }
        }
        public static int NaturalCompare(string a, string b)
        {
            int x = 0, y = 0;
            while (x < a.Length && y < b.Length)
            {
                if (char.IsDigit(a[x]) && char.IsDigit(b[y]))
                {
                    int ax = x, by = y;
                    while (x < a.Length && char.IsDigit(a[x])) x++;
                    while (y < b.Length && char.IsDigit(b[y])) y++;
                    string left = a.Substring(ax, x-ax).TrimStart('0'), right = b.Substring(by, y-by).TrimStart('0');
                    int order = left.Length.CompareTo(right.Length);
                    if (order == 0) order = string.Compare(left, right, StringComparison.Ordinal);
                    if (order != 0) return order;
                }
                else
                {
                    int order = char.ToUpperInvariant(a[x++]).CompareTo(char.ToUpperInvariant(b[y++]));
                    if (order != 0) return order;
                }
            }
            int remaining = (a.Length-x).CompareTo(b.Length-y);
            return remaining != 0 ? remaining : string.Compare(a,b,StringComparison.OrdinalIgnoreCase);
        }
        public static string DisplayTitle(LocalMediaEntry item) => item.kind == LocalMediaKind.Drama ? item.series + " · " + item.title : item.title;
    }
}

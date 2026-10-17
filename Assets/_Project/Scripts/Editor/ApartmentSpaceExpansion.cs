using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    // 对当前场景做定点拓建，保留后来添加的人物、相框、语音和动作配置。
    public static class ApartmentSpaceExpansion
    {
        private const string ScenePath = DesignApartmentBuilder.ScenePath;
        private static Transform root;
        private static Transform shell;
        private static Transform furniture;
        private static Transform detail;
        private static Material plaster;
        private static Material metal;
        private static Material glass;
        private static Material curtain;

        [MenuItem("雨夜公寓/拓建三个房间并安装互动")]
        public static void ApplyToCurrentScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            root = scene.GetRootGameObjects().Select(go => go.transform)
                .FirstOrDefault(t => t.name == "雨夜复式公寓_新版");
            if (!root) throw new InvalidOperationException("没有找到现有复式公寓场景，已停止，未重建地图。");
            shell = root.Find("建筑与结构");
            furniture = root.Find("室内家具");
            detail = root.Find("收边与装饰");
            if (!shell || !furniture || !detail) throw new InvalidOperationException("现有场景结构不完整，已停止。");
            plaster = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/DesignV2/Materials/蓝灰细砂墙.mat");
            metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/DesignV2/Materials/黑钛金属.mat");
            glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/DesignV2/Materials/低反射通透玻璃.mat");
            curtain = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/DesignV2/Materials/垂落蓝灰窗帘.mat");
            if (!plaster || !metal || !glass || !curtain) throw new InvalidOperationException("地图原有材质不完整，已停止。");

            if (!root.Find("四倍房间拓建标记"))
            {
                ExpandUpperRooms();
                new GameObject("四倍房间拓建标记").transform.SetParent(root, false);
            }
            FaceSofas();
            InstallCurtains();
            InstallHandHold();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("现有场景拓建完成：主卧、卫浴、次卧各四倍地面面积；沙发、窗帘和牵手交互已安装。");
        }

        public static void ApplyAndBuild()
        {
            ApplyToCurrentScene();
            NaturalActionInstaller.BuildRealtimeDemo();
        }

        private static Transform Find(string name, Transform parent = null)
        {
            return (parent ? parent : root).GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == name);
        }

        private static void Resize(string name, Vector3 center, Vector3 size, Transform parent = null)
        {
            Transform item = Find(name, parent);
            if (!item) throw new InvalidOperationException("缺少现有结构：" + name);
            item.position = center;
            var mesh = DesignMesh.Rounded(size, .008f);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = item.GetComponent<BoxCollider>();
            if (collider) { collider.center = mesh.bounds.center; collider.size = mesh.bounds.size; }
        }

        private static GameObject Solid(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collision = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mesh = DesignMesh.Rounded(size, .008f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collision)
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
            }
            return go;
        }

        private static void Move(string name, Vector3 delta, Transform parent = null)
        {
            Transform item = Find(name, parent);
            if (!item) throw new InvalidOperationException("缺少要移动的家具：" + name);
            item.position += delta;
        }

        private static void ExpandUpperRooms()
        {
            // 三间房长宽各扩大两倍：主卧 8.9×8.4，卫浴 5.1×8.4，次卧 8.4×8.4 米。
            Resize("二楼北侧楼板", new Vector3(1.15f, 3.14f, 5.6f), new Vector3(22.4f, .32f, 9.8f), shell);
            Resize("北外墙", new Vector3(0, 1.65f, 6.4f), new Vector3(11.6f, 3.3f, .2f), shell);
            Solid("拓建上层北墙", shell, new Vector3(1.15f, 4.95f, 10.6f), new Vector3(22.6f, 3.3f, .2f), plaster);
            Resize("东外墙", new Vector3(5.7f, 1.65f, 0), new Vector3(.2f, 3.3f, 12.8f), shell);
            Solid("上层原东侧回廊墙", shell, new Vector3(5.7f, 4.95f, -2.1f), new Vector3(.2f, 3.3f, 8.4f), plaster);
            Solid("拓建上层东墙", shell, new Vector3(12.45f, 4.95f, 6.35f), new Vector3(.2f, 3.3f, 8.5f), plaster);
            Resize("主卧隔墙", new Vector3(-1.15f, 4.95f, 6.3f), new Vector3(.15f, 3.3f, 8.4f), shell);
            Resize("书房隔墙", new Vector3(3.95f, 4.95f, 6.3f), new Vector3(.15f, 3.3f, 8.4f), shell);
            Resize("通高顶板", new Vector3(0, 6.68f, -2.2f), new Vector3(11.6f, .16f, 8.6f), shell);
            Solid("拓建上层顶板", shell, new Vector3(1.15f, 6.68f, 6.35f), new Vector3(22.6f, .16f, 8.5f), plaster);
            Solid("主卧拓建前墙", shell, new Vector3(-7.825f, 4.95f, 2.1f), new Vector3(4.45f, 3.3f, .18f), plaster);
            Solid("卫浴拓建前墙", shell, new Vector3(2.675f, 4.95f, 2.1f), new Vector3(2.55f, 3.3f, .18f), plaster);

            foreach (var t in shell.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("书房入口_")).ToArray()) Object.DestroyImmediate(t.gameObject);
            float door = 5.15f, width = 1.05f;
            Solid("书房入口_左段", shell, new Vector3((3.95f + door - width / 2f) / 2f, 4.95f, 2.1f),
                new Vector3(door - width / 2f - 3.95f, 3.3f, .18f), plaster);
            Solid("书房入口_右段", shell, new Vector3((door + width / 2f + 12.35f) / 2f, 4.95f, 2.1f),
                new Vector3(12.35f - door - width / 2f, 3.3f, .18f), plaster);
            Solid("书房入口_门楣", shell, new Vector3(door, 5.95f, 2.1f), new Vector3(width, 1.3f, .18f), plaster);
            Move("书房门", new Vector3(2.15f, 0, 0), root);
            foreach (var frame in detail.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("书房门门框"))) frame.position += new Vector3(2.15f, 0, 0);

            // 原窗格的下层不变；上层西侧移到拓建后外墙，避免窗玻璃横穿主卧。
            var windows = Find("通高落地窗", shell);
            foreach (var item in windows.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (item.name == "西侧窗玻璃" && item.position.y > 3.3f && item.position.z > 2.1f)
                    Object.DestroyImmediate(item.gameObject);
                else if (item.name == "西侧竖框" && item.position.z > 2.1f)
                    ResizeFrame(item, new Vector3(item.position.x, 1.65f, item.position.z), new Vector3(.085f, 3.3f, .085f));
            }
            for (int i = 0; i <= 6; i++)
            {
                float z = 2.1f + i * 1.4f;
                Solid("拓建西侧窗竖框", windows, new Vector3(-10.02f, 4.95f, z), new Vector3(.085f, 3.3f, .085f), metal);
                if (i < 6) Solid("拓建西侧窗玻璃", windows, new Vector3(-10.06f, 4.95f, z + .7f),
                    new Vector3(.025f, 3.17f, 1.31f), glass);
            }
            foreach (float y in new[] { 3.32f, 6.58f })
                Solid("拓建西侧窗横框", windows, new Vector3(-10.02f, y, 6.3f), new Vector3(.11f, .09f, 8.4f), metal);
            foreach (var item in windows.GetComponentsInChildren<Transform>(true).ToArray())
                if (item.name == "西侧横框" && item.position.y > 3.3f)
                    ResizeFrame(item, new Vector3(-5.55f, item.position.y, -2.2f), new Vector3(.11f, .09f, 8.2f));

            Move("主卧", new Vector3(-2.2f, 0, 2.05f), furniture);
            Move("次卧书房", new Vector3(4.35f, 0, 1.85f), furniture);
            Move("主卧衣柜", new Vector3(0, 0, 2.1f), furniture);
            Move("次卧书房衣柜", new Vector3(2.55f, 0, 2.1f), furniture);
            foreach (var strip in detail.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name == "衣柜细格栅"))
                strip.position += strip.position.x < 0 ? new Vector3(0, 0, 2.1f) : new Vector3(2.55f, 0, 2.1f);
            foreach (var item in furniture.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name == "书桌" || t.name == "书桌侧脚" || t.name == "叠放书籍" && t.position.y > 3.3f ||
                                     t.name == "曲面餐椅" && t.position.y > 3.3f))
                item.position += new Vector3(4.35f, 0, 2.1f);
            Move("主卧暖光", new Vector3(-2.2f, 0, 2.05f), root);
            Move("次卧书房暖光", new Vector3(4.35f, 0, 1.85f), root);

            Transform bath = Find("二楼卫生间", furniture);
            foreach (Transform child in bath) child.position += new Vector3(1.3f, 0, 2.1f);
            Resize("浴室地砖", new Vector3(1.4f, 3.325f, 6.3f), new Vector3(4.92f, .04f, 8.2f), bath);
            Move("浴室灯光", new Vector3(1.3f, 0, 2.1f), root);
            var probe = Find("室内静态反射探针", root)?.GetComponent<ReflectionProbe>();
            if (probe) { probe.transform.position = new Vector3(1.15f, 3.3f, 2.1f); probe.size = new Vector3(23f, 7f, 18f); }
        }

        private static void ResizeFrame(Transform item, Vector3 center, Vector3 size)
        {
            item.position = center;
            var mesh = DesignMesh.Rounded(size, .008f);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = item.GetComponent<BoxCollider>();
            if (collider) { collider.center = mesh.bounds.center; collider.size = mesh.bounds.size; }
        }

        private static void FaceSofas()
        {
            Transform left = Find("窗边三人沙发", furniture);
            Transform right = Find("对坐三人沙发", furniture);
            if (!left || !right) throw new InvalidOperationException("未找到客厅沙发。");
            left.rotation = Quaternion.Euler(0, -90f, 0);
            right.rotation = Quaternion.Euler(0, 90f, 0);
        }

        private static void InstallCurtains()
        {
            var panels = detail.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "通高褶皱窗帘").OrderBy(t => t.position.z).ToArray();
            if (panels.Length < 3) throw new InvalidOperationException("未找到原有三片窗帘。");
            // 第三片改为二楼主卧外窗；原来通高的位置会穿过扩建后的楼板。
            panels[2].position = new Vector3(-9.82f, 3.33f, 6.3f);
            panels[2].GetComponent<MeshFilter>().sharedMesh = DesignMesh.Curtain("拓建上层窗帘", 1.6f, 3.15f);
            for (int i = 0; i < panels.Length; i++)
                EnsureCurtainInteraction(panels[i], i == 2 ? 1.35f : 1.45f);
            foreach (float z in new[] { 2.9f, 4.6f, 8f, 9.7f })
            {
                string name = "拓建上层可拉窗帘_" + z.ToString("F1");
                Transform panel = detail.Find(name);
                if (!panel)
                {
                    var go = new GameObject(name);
                    panel = go.transform;
                    panel.SetParent(detail, false);
                    panel.position = new Vector3(-9.82f, 3.33f, z);
                    panel.localEulerAngles = new Vector3(0, 90f, 0);
                    go.AddComponent<MeshFilter>().sharedMesh = DesignMesh.Curtain("拓建上层窗帘", 1.6f, 3.15f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = curtain;
                }
                EnsureCurtainInteraction(panel, 1.35f);
            }
            if (!detail.Find("拓建上层窗帘轨道"))
                Solid("拓建上层窗帘轨道", detail, new Vector3(-9.81f, 6.46f, 6.3f), new Vector3(.035f, .035f, 8.4f), metal, false);
        }

        private static void EnsureCurtainInteraction(Transform panel, float handHeight)
        {
            var interactive = panel.GetComponent<InteractiveCurtain>();
            if (!interactive) interactive = panel.gameObject.AddComponent<InteractiveCurtain>();
            interactive.grabHeight = handHeight;
            if (interactive.grabPoint) return;
            var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = "窗帘抓握点";
            point.transform.SetParent(panel, false);
            point.transform.localScale = Vector3.one * .055f;
            point.GetComponent<Renderer>().sharedMaterial = metal;
            point.GetComponent<SphereCollider>().isTrigger = true;
            interactive.grabPoint = point.transform;
        }

        private static void InstallHandHold()
        {
            var character = Find(CharacterAnimationSetup.CharacterName, root);
            if (!character) throw new InvalidOperationException("没有找到现有人物，不能安装牵手。");
            if (!character.GetComponent<CharacterHandHold>()) character.gameObject.AddComponent<CharacterHandHold>();
        }

        private static void Verify()
        {
            float x = Find("主卧隔墙", shell).position.x;
            float right = Find("书房隔墙", shell).position.x;
            var slab = Find("二楼北侧楼板", shell).GetComponent<BoxCollider>();
            if (Mathf.Abs(x + 1.15f) > .01f || Mathf.Abs(right - 3.95f) > .01f || slab.size.x < 22f)
                throw new InvalidOperationException("房间拓建校验失败。");
            if (detail.GetComponentsInChildren<InteractiveCurtain>(true).Length < 3)
                throw new InvalidOperationException("窗帘交互未安装齐全。");
            if (!Find(CharacterAnimationSetup.CharacterName, root).GetComponent<CharacterHandHold>())
                throw new InvalidOperationException("人物牵手未安装。");
        }
    }
}

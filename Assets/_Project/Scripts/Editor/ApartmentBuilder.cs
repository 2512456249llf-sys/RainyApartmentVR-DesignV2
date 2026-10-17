using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RainyApartmentVR.Editor
{
    public static class ApartmentBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        private const string MaterialFolder = "Assets/_Project/Materials";

        private static class Config
        {
            public const float Width = 12f;
            public const float Depth = 9f;
            public const float FloorHeight = 3f;
            public const float WallThickness = 0.18f;
            public const float FloorThickness = 0.2f;
            public const float DoorWidth = 0.9f;
            public const float DoorHeight = 2.1f;
            public const float StairWidth = 1.1f;
            public const int StairCount = 17;
            public const float StairTread = 0.25f;
        }

        private sealed class Palette
        {
            public Material Wall;
            public Material Floor;
            public Material Ceiling;
            public Material Stair;
            public Material Glass;
            public Material Marker;
        }

        [MenuItem("Rainy Apartment/生成复式公寓灰盒")]
        public static void BuildApartment()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(MaterialFolder);

            Palette palette = CreatePalette();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject apartment = new GameObject("Apartment");
            CreateRoomMarkers(apartment.transform);
            CreateFirstFloor(apartment.transform, palette);
            CreateSecondFloor(apartment.transform, palette);
            CreateStairs(apartment.transform, palette);
            CreatePreviewLighting(apartment.transform);
            CreatePlayerSpawn(apartment.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new System.InvalidOperationException("Apartment 场景保存失败。");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("阶段 2：Apartment.unity 已生成。尺寸为 12m × 9m，层高为 3m。");
        }

        public static void BuildFromCommandLine()
        {
            BuildApartment();
        }

        private static void CreateRoomMarkers(Transform root)
        {
            GameObject rooms = CreateGroup("Rooms", root);
            CreateMarker("Entry", rooms.transform, new Vector3(-1.3f, 0f, -3.3f));
            CreateMarker("LivingRoom", rooms.transform, new Vector3(-1.1f, 0f, 0.8f));
            CreateMarker("Kitchen", rooms.transform, new Vector3(4f, 0f, 1.2f));
            CreateMarker("Bathroom", rooms.transform, new Vector3(4f, 0f, -3f));
            CreateMarker("Landing", rooms.transform, new Vector3(-1f, Config.FloorHeight, 3.1f));
            CreateMarker("Bedroom1", rooms.transform, new Vector3(-2f, Config.FloorHeight, -1.1f));
            CreateMarker("Bedroom2", rooms.transform, new Vector3(3.2f, Config.FloorHeight, -1.1f));
        }

        private static void CreateFirstFloor(Transform root, Palette palette)
        {
            GameObject floor = CreateGroup("FirstFloor", root);
            CreateCube("GroundFloor", floor.transform,
                new Vector3(0f, -Config.FloorThickness * 0.5f, 0f),
                new Vector3(Config.Width, Config.FloorThickness, Config.Depth), palette.Floor);

            GameObject walls = CreateGroup("Walls", floor.transform);
            float halfWidth = Config.Width * 0.5f;
            float halfDepth = Config.Depth * 0.5f;

            CreateWallXWithDoor(walls.transform, "SouthWall", -halfWidth, halfWidth, -halfDepth,
                -1.3f, 1.05f, 0f, palette.Wall);
            CreateFloorToCeilingWindow(walls.transform, palette, -halfDepth + Config.Depth);
            CreateWallZ(walls.transform, "WestWall", -halfWidth, 0f, 0f, Config.Depth, Config.FloorHeight, palette.Wall);
            CreateWallZ(walls.transform, "EastWall", halfWidth, 0f, 0f, Config.Depth, Config.FloorHeight, palette.Wall);

            // 东侧服务区：两个门洞分别连接厨房与浴室。
            CreateWallZSegment(walls.transform, "ServiceWall_A", 2f, -4.5f, -3.55f, 0f, palette.Wall);
            CreateDoorHeaderZ(walls.transform, "BathroomDoorHeader", 2f, -3.1f, 0f, palette.Wall);
            CreateWallZSegment(walls.transform, "ServiceWall_B", 2f, -2.65f, -0.45f, 0f, palette.Wall);
            CreateDoorHeaderZ(walls.transform, "KitchenDoorHeader", 2f, 0f, 0f, palette.Wall);
            CreateWallZSegment(walls.transform, "ServiceWall_C", 2f, 0.45f, 4.5f, 0f, palette.Wall);
            CreateWallX(walls.transform, "KitchenBathroomDivider", 4f, -1.7f, 0f, 4f, Config.FloorHeight, palette.Wall);

            // 玄关与客厅保持宽敞联系，同时给楼梯保留通道。
            CreateWallXSegment(walls.transform, "EntryDivider_A", -4.2f, -1.85f, -2.2f, 0f, palette.Wall);
            CreateDoorHeaderX(walls.transform, "EntryOpeningHeader", -1.1f, -2.2f, 0f, 1.5f, palette.Wall);
            CreateWallXSegment(walls.transform, "EntryDivider_B", -0.35f, 2f, -2.2f, 0f, palette.Wall);

            CreateCube("EntryStep", floor.transform, new Vector3(-1.3f, -0.03f, -4.75f),
                new Vector3(1.5f, 0.14f, 0.5f), palette.Floor);
        }

        private static void CreateSecondFloor(Transform root, Palette palette)
        {
            GameObject floor = CreateGroup("SecondFloor", root);
            GameObject slabs = CreateGroup("FloorSlabs", floor.transform);

            float floorY = Config.FloorHeight - Config.FloorThickness * 0.5f;
            CreateCube("MainSlab", slabs.transform, new Vector3(0.875f, floorY, 0f),
                new Vector3(10.25f, Config.FloorThickness, Config.Depth), palette.Floor);
            CreateCube("WestEdgeSlab", slabs.transform, new Vector3(-5.825f, floorY, 0f),
                new Vector3(0.35f, Config.FloorThickness, Config.Depth), palette.Floor);
            CreateCube("StairFrontSlab", slabs.transform, new Vector3(-4.95f, floorY, -3.75f),
                new Vector3(1.4f, Config.FloorThickness, 1.5f), palette.Floor);
            CreateCube("StairRearLanding", slabs.transform, new Vector3(-4.95f, floorY, 3.15f),
                new Vector3(1.4f, Config.FloorThickness, 2.7f), palette.Floor);

            GameObject walls = CreateGroup("Walls", floor.transform);
            float baseY = Config.FloorHeight;
            float halfWidth = Config.Width * 0.5f;
            float halfDepth = Config.Depth * 0.5f;

            CreateSecondFloorSouthWindows(walls.transform, palette);
            CreateWallX(walls.transform, "NorthWall", 0f, halfDepth, baseY, Config.Width, Config.FloorHeight, palette.Wall);
            CreateWallZ(walls.transform, "WestWall", -halfWidth, 0f, baseY, Config.Depth, Config.FloorHeight, palette.Wall);
            CreateWallZ(walls.transform, "EastWall", halfWidth, 0f, baseY, Config.Depth, Config.FloorHeight, palette.Wall);

            CreateWallZ(walls.transform, "BedroomDivider", 0.3f, -1.35f, baseY, 6.3f, Config.FloorHeight, palette.Wall);

            // 卧室门均朝向公共平台，两个卧室互相独立。
            CreateWallXSegment(walls.transform, "BedroomFront_A", -4.25f, -2.25f, 1.8f, baseY, palette.Wall);
            CreateDoorHeaderX(walls.transform, "Bedroom1DoorHeader", -1.8f, 1.8f, baseY, palette.Wall);
            CreateWallXSegment(walls.transform, "BedroomFront_B", -1.35f, 2.55f, 1.8f, baseY, palette.Wall);
            CreateDoorHeaderX(walls.transform, "Bedroom2DoorHeader", 3f, 1.8f, baseY, palette.Wall);
            CreateWallXSegment(walls.transform, "BedroomFront_C", 3.45f, 6f, 1.8f, baseY, palette.Wall);

            CreateCube("StairGuardRail", floor.transform, new Vector3(-4.18f, 3.55f, -0.6f),
                new Vector3(0.12f, 1.1f, 4.8f), palette.Stair);
            CreateCube("Ceiling", floor.transform, new Vector3(0f, 6.05f, 0f),
                new Vector3(Config.Width, 0.1f, Config.Depth), palette.Ceiling);
        }

        private static void CreateFloorToCeilingWindow(Transform parent, Palette palette, float northZ)
        {
            GameObject window = CreateGroup("FloorToCeilingWindow", parent);
            float minX = -3.9f;
            float maxX = 1.7f;

            CreateWallXSegment(parent, "NorthWall_Left", -6f, minX, northZ, 0f, palette.Wall);
            CreateWallXSegment(parent, "NorthWall_Right", maxX, 6f, northZ, 0f, palette.Wall);
            CreateCube("WindowHeader", window.transform, new Vector3((minX + maxX) * 0.5f, 2.9f, northZ),
                new Vector3(maxX - minX, 0.2f, Config.WallThickness), palette.Wall);
            CreateCube("Glass", window.transform, new Vector3((minX + maxX) * 0.5f, 1.45f, northZ),
                new Vector3(maxX - minX, 2.7f, 0.05f), palette.Glass);

            for (int index = 1; index < 4; index++)
            {
                float x = Mathf.Lerp(minX, maxX, index / 4f);
                CreateCube($"Mullion_{index}", window.transform, new Vector3(x, 1.45f, northZ - 0.015f),
                    new Vector3(0.06f, 2.7f, 0.08f), palette.Stair);
            }
        }

        private static void CreateSecondFloorSouthWindows(Transform parent, Palette palette)
        {
            float z = -Config.Depth * 0.5f;
            float baseY = Config.FloorHeight;
            float[] edges = { -6f, -3.2f, -1f, 1.8f, 4f, 6f };

            CreateWallXSegment(parent, "SouthWall_A", edges[0], edges[1], z, baseY, palette.Wall);
            CreateWindowOpening(parent, "Bedroom1Window", edges[1], edges[2], z, baseY, palette);
            CreateWallXSegment(parent, "SouthWall_B", edges[2], edges[3], z, baseY, palette.Wall);
            CreateWindowOpening(parent, "Bedroom2Window", edges[3], edges[4], z, baseY, palette);
            CreateWallXSegment(parent, "SouthWall_C", edges[4], edges[5], z, baseY, palette.Wall);
        }

        private static void CreateWindowOpening(Transform parent, string name, float minX, float maxX,
            float z, float baseY, Palette palette)
        {
            GameObject window = CreateGroup(name, parent);
            float width = maxX - minX;
            float centerX = (minX + maxX) * 0.5f;
            CreateCube("Sill", window.transform, new Vector3(centerX, baseY + 0.45f, z),
                new Vector3(width, 0.9f, Config.WallThickness), palette.Wall);
            CreateCube("Header", window.transform, new Vector3(centerX, baseY + 2.6f, z),
                new Vector3(width, 0.8f, Config.WallThickness), palette.Wall);
            CreateCube("Glass", window.transform, new Vector3(centerX, baseY + 1.55f, z),
                new Vector3(width, 1.3f, 0.05f), palette.Glass);
        }

        private static void CreateStairs(Transform root, Palette palette)
        {
            GameObject stairs = CreateGroup("Stairs", root);
            float rise = Config.FloorHeight / Config.StairCount;
            // 末级踏步与二楼平台之间只保留很小的施工缝，避免角色卡在顶部。
            float startZ = -2.4f;

            for (int index = 0; index < Config.StairCount; index++)
            {
                float stepHeight = rise * (index + 1);
                float z = startZ + Config.StairTread * index;
                CreateCube($"Step_{index + 1:00}", stairs.transform,
                    new Vector3(-4.95f, stepHeight * 0.5f, z),
                    new Vector3(Config.StairWidth, stepHeight, Config.StairTread + 0.01f), palette.Stair);
            }

            CreateCube("OuterHandrail", stairs.transform, new Vector3(-5.58f, 2.05f, -0.85f),
                new Vector3(0.08f, 1.1f, 4.4f), palette.Stair);
        }

        private static void CreatePlayerSpawn(Transform root)
        {
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(root);
            spawn.transform.position = new Vector3(-1.3f, 0.05f, -3.45f);
            spawn.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }

        private static void CreatePreviewLighting(Transform root)
        {
            GameObject lighting = CreateGroup("PreviewLighting", root);
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(lighting.transform);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.color = new Color(0.73f, 0.82f, 1f);

            GameObject cameraObject = new GameObject("Scene Preview Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(lighting.transform);
            cameraObject.transform.position = new Vector3(-1.3f, 1.65f, -3.5f);
            cameraObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateWallXWithDoor(Transform parent, string name, float minX, float maxX,
            float z, float doorCenterX, float doorWidth, float baseY, Material material)
        {
            CreateWallXSegment(parent, name + "_Left", minX, doorCenterX - doorWidth * 0.5f, z, baseY, material);
            CreateWallXSegment(parent, name + "_Right", doorCenterX + doorWidth * 0.5f, maxX, z, baseY, material);
            CreateDoorHeaderX(parent, name + "_Header", doorCenterX, z, baseY, doorWidth, material);
        }

        private static void CreateWallXSegment(Transform parent, string name, float minX, float maxX,
            float z, float baseY, Material material)
        {
            float width = maxX - minX;
            if (width <= 0f)
            {
                return;
            }

            CreateWallX(parent, name, (minX + maxX) * 0.5f, z, baseY, width, Config.FloorHeight, material);
        }

        private static void CreateWallZSegment(Transform parent, string name, float x, float minZ, float maxZ,
            float baseY, Material material)
        {
            float depth = maxZ - minZ;
            if (depth <= 0f)
            {
                return;
            }

            CreateWallZ(parent, name, x, (minZ + maxZ) * 0.5f, baseY, depth, Config.FloorHeight, material);
        }

        private static void CreateDoorHeaderX(Transform parent, string name, float centerX, float z,
            float baseY, Material material)
        {
            CreateDoorHeaderX(parent, name, centerX, z, baseY, Config.DoorWidth, material);
        }

        private static void CreateDoorHeaderX(Transform parent, string name, float centerX, float z,
            float baseY, float width, Material material)
        {
            float height = Config.FloorHeight - Config.DoorHeight;
            CreateCube(name, parent,
                new Vector3(centerX, baseY + Config.DoorHeight + height * 0.5f, z),
                new Vector3(width, height, Config.WallThickness), material);
        }

        private static void CreateDoorHeaderZ(Transform parent, string name, float x, float centerZ,
            float baseY, Material material)
        {
            float height = Config.FloorHeight - Config.DoorHeight;
            CreateCube(name, parent,
                new Vector3(x, baseY + Config.DoorHeight + height * 0.5f, centerZ),
                new Vector3(Config.WallThickness, height, Config.DoorWidth), material);
        }

        private static void CreateWallX(Transform parent, string name, float centerX, float z, float baseY,
            float width, float height, Material material)
        {
            CreateCube(name, parent, new Vector3(centerX, baseY + height * 0.5f, z),
                new Vector3(width, height, Config.WallThickness), material);
        }

        private static void CreateWallZ(Transform parent, string name, float x, float centerZ, float baseY,
            float depth, float height, Material material)
        {
            CreateCube(name, parent, new Vector3(x, baseY + height * 0.5f, centerZ),
                new Vector3(Config.WallThickness, height, depth), material);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent);
            return group;
        }

        private static void CreateMarker(string name, Transform parent, Vector3 position)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.position = position;
        }

        private static Palette CreatePalette()
        {
            return new Palette
            {
                Wall = CreateOrUpdateMaterial("Greybox_Wall", new Color(0.52f, 0.56f, 0.62f), 0.1f, false),
                Floor = CreateOrUpdateMaterial("Greybox_Floor", new Color(0.22f, 0.25f, 0.3f), 0.15f, false),
                Ceiling = CreateOrUpdateMaterial("Greybox_Ceiling", new Color(0.36f, 0.39f, 0.44f), 0.05f, false),
                Stair = CreateOrUpdateMaterial("Greybox_Stair", new Color(0.15f, 0.18f, 0.23f), 0.25f, false),
                Glass = CreateOrUpdateMaterial("Greybox_Glass", new Color(0.2f, 0.45f, 0.65f, 0.34f), 0.8f, true),
                Marker = CreateOrUpdateMaterial("Greybox_Marker", new Color(0.7f, 0.55f, 0.22f), 0.1f, false)
            };
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float smoothness, bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new System.InvalidOperationException("找不到 URP/Lit 着色器。");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.renderQueue = transparent ? 3000 : -1;
            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string folder = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class Stage7FurnitureSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        private const string MaterialFolder = "Assets/_Project/Materials";

        [MenuItem("Rainy Apartment/摆放占位家具")]
        public static void ConfigureFurniture()
        {
            EditorSceneManager.OpenScene(ScenePath);

            GameObject oldRoot = GameObject.Find("PlaceholderFurniture");
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            Material dark = CreateOrUpdateMaterial("Furniture_Dark", new Color(0.055f, 0.065f, 0.085f), 0.55f, 0.08f);
            Material fabric = CreateOrUpdateMaterial("Furniture_FabricBlue", new Color(0.09f, 0.14f, 0.2f), 0.8f, 0f);
            Material wood = CreateOrUpdateMaterial("Furniture_WarmWood", new Color(0.23f, 0.12f, 0.065f), 0.5f, 0f);
            Material metal = CreateOrUpdateMaterial("Furniture_Metal", new Color(0.18f, 0.2f, 0.23f), 0.24f, 0.7f);
            Material porcelain = CreateOrUpdateMaterial("Furniture_Porcelain", new Color(0.62f, 0.69f, 0.73f), 0.18f, 0.05f);
            Material green = CreateOrUpdateMaterial("Furniture_Plant", new Color(0.08f, 0.24f, 0.12f), 0.72f, 0f);
            Material accent = CreateOrUpdateMaterial("Furniture_Accent", new Color(0.37f, 0.16f, 0.12f), 0.62f, 0f);

            Transform root = new GameObject("PlaceholderFurniture").transform;
            CreateLivingRoom(root, dark, fabric, wood, metal, green, accent);
            CreateKitchen(root, dark, wood, metal, porcelain, green);
            CreateBathroom(root, dark, metal, porcelain);
            CreateBedroom(root, "Bedroom1Furniture", new Vector3(-3.75f, 3f, -2.55f), -1f,
                dark, fabric, wood, metal, green, accent);
            CreateBedroom(root, "Bedroom2Furniture", new Vector3(4.65f, 3f, -2.55f), 1f,
                dark, fabric, wood, metal, green, accent);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath))
            {
                throw new System.InvalidOperationException("占位家具场景保存失败。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("阶段 7：各房间占位家具与生活物件已摆放，并保留主要漫游路线。");
        }

        public static void ConfigureFromCommandLine()
        {
            ConfigureFurniture();
        }

        private static void CreateLivingRoom(Transform root, Material dark, Material fabric,
            Material wood, Material metal, Material green, Material accent)
        {
            Transform room = Group("LivingRoomFurniture", root);

            Transform sofa = Group("Furniture_Living_Sofa", room);
            Cube("Base", sofa, new Vector3(-1.8f, 0.42f, 3.15f), new Vector3(2.5f, 0.45f, 0.78f), fabric);
            Cube("Back", sofa, new Vector3(-1.8f, 0.92f, 3.48f), new Vector3(2.5f, 0.85f, 0.18f), fabric);
            Cube("ArmLeft", sofa, new Vector3(-3.12f, 0.7f, 3.15f), new Vector3(0.18f, 0.68f, 0.8f), fabric);
            Cube("ArmRight", sofa, new Vector3(-0.48f, 0.7f, 3.15f), new Vector3(0.18f, 0.68f, 0.8f), fabric);

            Transform table = Group("Furniture_Living_CoffeeTable", room);
            Cube("Top", table, new Vector3(-1.8f, 0.46f, 1.95f), new Vector3(1.35f, 0.12f, 0.72f), wood);
            FourLegs(table, new Vector3(-1.8f, 0.23f, 1.95f), 1.05f, 0.48f, 0.4f, dark);
            Cube("Book_A", table, new Vector3(-1.95f, 0.55f, 1.95f), new Vector3(0.34f, 0.05f, 0.25f), accent, false);
            Cube("Book_B", table, new Vector3(-1.93f, 0.6f, 1.95f), new Vector3(0.3f, 0.05f, 0.22f), dark, false);
            Cylinder("Cup", table, new Vector3(-1.48f, 0.62f, 1.92f), new Vector3(0.09f, 0.12f, 0.09f), porcelain: metal);

            Transform tvStand = Group("Furniture_Living_TVStand", room);
            Cube("Cabinet", tvStand, new Vector3(1.58f, 0.38f, 2.85f), new Vector3(0.5f, 0.72f, 1.65f), dark);
            Cube("TV", tvStand, new Vector3(1.48f, 1.25f, 2.85f), new Vector3(0.1f, 1.05f, 1.72f), metal);

            Cube("Furniture_Living_Rug", room, new Vector3(-1.8f, 0.025f, 2.35f), new Vector3(3.35f, 0.035f, 2.1f), accent, false);
            CreateFloorLamp(room, new Vector3(-3.7f, 0f, 3.7f), metal, accent);

            Transform shelf = Group("Furniture_Living_Bookshelf", room);
            Cube("Frame", shelf, new Vector3(-5.55f, 1.05f, 3.4f), new Vector3(0.35f, 2.1f, 1.35f), dark);
            for (int index = 0; index < 4; index++)
            {
                Cube($"Shelf_{index + 1}", shelf, new Vector3(-5.34f, 0.25f + index * 0.5f, 3.4f),
                    new Vector3(0.28f, 0.08f, 1.3f), wood);
            }

            CreatePlant(room, "Furniture_Living_Plant", new Vector3(0.85f, 0f, 3.85f), wood, green);
        }

        private static void CreateKitchen(Transform root, Material dark, Material wood,
            Material metal, Material porcelain, Material green)
        {
            Transform room = Group("KitchenFurniture", root);
            Cube("Furniture_Kitchen_Fridge", room, new Vector3(5.42f, 1.05f, 3.55f), new Vector3(0.9f, 2.1f, 0.85f), metal);

            Transform counter = Group("Furniture_Kitchen_Counter", room);
            Cube("Cabinet", counter, new Vector3(5.48f, 0.46f, 1.7f), new Vector3(0.82f, 0.92f, 2.5f), dark);
            Cube("Worktop", counter, new Vector3(5.45f, 0.95f, 1.7f), new Vector3(0.9f, 0.1f, 2.58f), wood);
            Cube("Sink", counter, new Vector3(5.39f, 1.02f, 2.2f), new Vector3(0.48f, 0.06f, 0.65f), metal, false);
            Cube("Stove", counter, new Vector3(5.39f, 1.02f, 1.1f), new Vector3(0.48f, 0.06f, 0.65f), metal, false);

            Transform dining = Group("Furniture_Kitchen_DiningTable", room);
            Cube("Top", dining, new Vector3(3.45f, 0.78f, 3.05f), new Vector3(1.5f, 0.12f, 1.05f), wood);
            FourLegs(dining, new Vector3(3.45f, 0.4f, 3.05f), 1.15f, 0.7f, 0.72f, dark);
            CreateChair(room, "Furniture_Kitchen_Chair_A", new Vector3(2.55f, 0f, 3.05f), 90f, wood, dark);
            CreateChair(room, "Furniture_Kitchen_Chair_B", new Vector3(4.35f, 0f, 3.05f), -90f, wood, dark);
            Cylinder("LifeItem_Kitchen_Cup", room, new Vector3(3.3f, 0.94f, 3.05f), new Vector3(0.09f, 0.13f, 0.09f), porcelain: porcelain);
            CreatePlant(room, "LifeItem_Kitchen_Plant", new Vector3(5.1f, 0.98f, 0.15f), wood, green, 0.55f);
            Cylinder("LifeItem_Kitchen_TrashBin", room, new Vector3(2.48f, 0.36f, 1.25f), new Vector3(0.3f, 0.36f, 0.3f), porcelain: metal);
        }

        private static void CreateBathroom(Transform root, Material dark, Material metal, Material porcelain)
        {
            Transform room = Group("BathroomFurniture", root);
            Transform toilet = Group("Furniture_Bathroom_Toilet", room);
            Cube("Tank", toilet, new Vector3(5.35f, 0.68f, -3.95f), new Vector3(0.55f, 0.75f, 0.3f), porcelain);
            Cylinder("Bowl", toilet, new Vector3(5.35f, 0.34f, -3.62f), new Vector3(0.36f, 0.22f, 0.48f), porcelain: porcelain);

            Transform sink = Group("Furniture_Bathroom_Sink", room);
            Cube("Cabinet", sink, new Vector3(5.45f, 0.42f, -2.15f), new Vector3(0.75f, 0.84f, 0.72f), dark);
            Cube("Basin", sink, new Vector3(5.45f, 0.89f, -2.15f), new Vector3(0.82f, 0.12f, 0.76f), porcelain);
            Cube("Mirror", sink, new Vector3(5.86f, 1.65f, -2.15f), new Vector3(0.04f, 1.1f, 0.78f), metal, false);

            Transform shower = Group("Furniture_Bathroom_Shower", room);
            Cube("Tray", shower, new Vector3(2.72f, 0.08f, -4f), new Vector3(1.05f, 0.16f, 0.9f), porcelain);
            Cube("GlassSide", shower, new Vector3(3.23f, 1.05f, -4f), new Vector3(0.04f, 2.05f, 0.9f), metal, false);
            Cylinder("ShowerHead", shower, new Vector3(2.7f, 2.05f, -4.35f), new Vector3(0.13f, 0.06f, 0.13f), porcelain: metal, collider: false);
        }

        private static void CreateBedroom(Transform root, string groupName, Vector3 bedCenter, float side,
            Material dark, Material fabric, Material wood, Material metal, Material green, Material accent)
        {
            Transform room = Group(groupName, root);
            string prefix = side < 0f ? "Furniture_Bedroom1_" : "Furniture_Bedroom2_";

            Transform bed = Group(prefix + "Bed", room);
            Cube("Frame", bed, bedCenter + new Vector3(0f, 0.28f, 0f), new Vector3(1.75f, 0.32f, 2.15f), wood);
            Cube("Mattress", bed, bedCenter + new Vector3(0f, 0.52f, 0f), new Vector3(1.65f, 0.25f, 2.02f), fabric);
            Cube("Pillow", bed, bedCenter + new Vector3(0f, 0.73f, -0.72f), new Vector3(1.2f, 0.16f, 0.48f), accent, false);

            float nightstandX = bedCenter.x + side * 1.18f;
            Cube(prefix + "Nightstand", room, new Vector3(nightstandX, 3.36f, -2.5f), new Vector3(0.58f, 0.72f, 0.55f), dark);
            Cylinder(prefix + "BedsideLamp", room, new Vector3(nightstandX, 3.85f, -2.5f), new Vector3(0.16f, 0.3f, 0.16f), porcelain: metal);

            float wardrobeX = side < 0f ? -5.45f : 5.45f;
            Cube(prefix + "Wardrobe", room, new Vector3(wardrobeX, 4.05f, 0.55f), new Vector3(0.75f, 2.05f, 1.5f), dark);

            float deskX = side < 0f ? -0.55f : 0.92f;
            Transform desk = Group(prefix + "Desk", room);
            Cube("Top", desk, new Vector3(deskX, 3.78f, -3.75f), new Vector3(1.2f, 0.1f, 0.62f), wood);
            FourLegs(desk, new Vector3(deskX, 3.39f, -3.75f), 0.95f, 0.42f, 0.73f, dark);
            CreateChair(room, prefix + "Chair", new Vector3(deskX, 3f, -3.05f), 180f, wood, dark);
            Cube(prefix + "Books", room, new Vector3(deskX - 0.25f, 3.87f, -3.72f), new Vector3(0.35f, 0.12f, 0.25f), accent, false);
            CreatePlant(room, prefix + "Plant", new Vector3(deskX + 0.38f, 3.83f, -3.72f), wood, green, 0.42f);
        }

        private static void CreateChair(Transform parent, string name, Vector3 position, float yaw,
            Material wood, Material dark)
        {
            Transform chair = Group(name, parent);
            chair.position = position;
            chair.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cube("Seat", chair, new Vector3(0f, 0.48f, 0f), new Vector3(0.52f, 0.12f, 0.52f), wood, true, true);
            Cube("Back", chair, new Vector3(0f, 0.9f, 0.22f), new Vector3(0.52f, 0.72f, 0.1f), wood, true, true);
            FourLegs(chair, new Vector3(0f, 0.24f, 0f), 0.36f, 0.36f, 0.42f, dark, true);
        }

        private static void CreateFloorLamp(Transform parent, Vector3 position, Material metal, Material shade)
        {
            Transform lamp = Group("Furniture_Living_FloorLamp", parent);
            Cylinder("Base", lamp, position + new Vector3(0f, 0.06f, 0f), new Vector3(0.28f, 0.06f, 0.28f), porcelain: metal);
            Cylinder("Stem", lamp, position + new Vector3(0f, 0.9f, 0f), new Vector3(0.035f, 0.85f, 0.035f), porcelain: metal);
            Cylinder("Shade", lamp, position + new Vector3(0f, 1.72f, 0f), new Vector3(0.34f, 0.3f, 0.34f), porcelain: shade, collider: false);
        }

        private static void CreatePlant(Transform parent, string name, Vector3 position,
            Material pot, Material leaves, float scale = 1f)
        {
            Transform plant = Group(name, parent);
            Cylinder("Pot", plant, position + new Vector3(0f, 0.22f * scale, 0f),
                new Vector3(0.26f * scale, 0.22f * scale, 0.26f * scale), porcelain: pot);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * 72f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.13f * scale;
                Cube($"Leaf_{index + 1}", plant, position + offset + new Vector3(0f, 0.62f * scale, 0f),
                    new Vector3(0.16f, 0.72f, 0.1f) * scale, leaves, false);
            }
        }

        private static void FourLegs(Transform parent, Vector3 center, float width, float depth,
            float height, Material material, bool local = false)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 position = center + new Vector3(x * width * 0.5f, 0f, z * depth * 0.5f);
                    Cube($"Leg_{x}_{z}", parent, position, new Vector3(0.08f, height, 0.08f), material, true, local);
                }
            }
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = true, bool local = false)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            if (local)
            {
                item.transform.localPosition = position;
            }
            else
            {
                item.transform.position = position;
            }
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
            {
                Object.DestroyImmediate(item.GetComponent<Collider>());
            }
            return item;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale,
            Material porcelain, bool collider = true)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = porcelain;
            if (!collider)
            {
                Object.DestroyImmediate(item.GetComponent<Collider>());
            }
            return item;
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

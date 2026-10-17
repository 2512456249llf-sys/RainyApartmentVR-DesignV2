using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage7FurnitureTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [OneTimeSetUp]
        public void 打开公寓场景()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void 每个房间都有独立的家具分组()
        {
            GameObject root = GameObject.Find("PlaceholderFurniture");
            Assert.That(root, Is.Not.Null);
            string[] groups =
            {
                "LivingRoomFurniture", "KitchenFurniture", "BathroomFurniture",
                "Bedroom1Furniture", "Bedroom2Furniture"
            };
            foreach (string group in groups)
            {
                Assert.That(GameObject.Find(group), Is.Not.Null, $"缺少家具分组：{group}");
            }
        }

        [Test]
        public void 主要家具种类齐全()
        {
            string[] required =
            {
                "Furniture_Living_Sofa", "Furniture_Living_CoffeeTable", "Furniture_Living_TVStand",
                "Furniture_Living_Bookshelf", "Furniture_Kitchen_Fridge", "Furniture_Kitchen_Counter",
                "Furniture_Kitchen_DiningTable", "Furniture_Bathroom_Toilet", "Furniture_Bathroom_Sink",
                "Furniture_Bathroom_Shower", "Furniture_Bedroom1_Bed", "Furniture_Bedroom1_Wardrobe",
                "Furniture_Bedroom1_Desk", "Furniture_Bedroom2_Bed", "Furniture_Bedroom2_Wardrobe",
                "Furniture_Bedroom2_Desk"
            };
            foreach (string item in required)
            {
                Assert.That(GameObject.Find(item), Is.Not.Null, $"缺少占位家具：{item}");
            }
        }

        [Test]
        public void 家具为轻量静态占位且数量合理()
        {
            GameObject root = GameObject.Find("PlaceholderFurniture");
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.InRange(70, 180));
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(0));
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThan(35));
        }

        [Test]
        public void 场景包含基础生活物件()
        {
            GameObject root = GameObject.Find("PlaceholderFurniture");
            string[] names = root.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
            Assert.That(names.Any(name => name.Contains("Book") || name.Contains("Books")), Is.True);
            Assert.That(names.Any(name => name.Contains("Cup")), Is.True);
            Assert.That(names.Count(name => name.Contains("Plant")), Is.GreaterThanOrEqualTo(3));
            Assert.That(names.Any(name => name.Contains("TrashBin")), Is.True);
        }
    }
}

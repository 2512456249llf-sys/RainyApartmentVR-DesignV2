using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage2ApartmentTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [OneTimeSetUp]
        public void 打开公寓场景()
        {
            Assert.That(File.Exists(ScenePath), Is.True, "找不到 Apartment.unity。");
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void 所有规定房间都存在()
        {
            string[] requiredNames =
            {
                "Entry", "LivingRoom", "Kitchen", "Bathroom",
                "Landing", "Bedroom1", "Bedroom2", "Stairs"
            };

            foreach (string requiredName in requiredNames)
            {
                Assert.That(GameObject.Find(requiredName), Is.Not.Null, $"缺少 {requiredName}。");
            }
        }

        [Test]
        public void 地板墙体楼梯均具有碰撞体()
        {
            GameObject groundFloor = GameObject.Find("GroundFloor");
            Assert.That(groundFloor, Is.Not.Null);
            Assert.That(groundFloor.GetComponent<BoxCollider>(), Is.Not.Null);

            GameObject firstFloorWalls = GameObject.Find("Apartment/FirstFloor/Walls");
            Assert.That(firstFloorWalls, Is.Not.Null);
            Assert.That(firstFloorWalls.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThan(10));

            GameObject stairs = GameObject.Find("Stairs");
            Assert.That(stairs, Is.Not.Null);
            Assert.That(stairs.transform.childCount, Is.GreaterThanOrEqualTo(17));
            Assert.That(stairs.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThanOrEqualTo(17));
        }

        [Test]
        public void 房间尺度与楼层高度符合设计()
        {
            GameObject westWall = GameObject.Find("Apartment/FirstFloor/Walls/WestWall");
            GameObject eastWall = GameObject.Find("Apartment/FirstFloor/Walls/EastWall");
            GameObject secondFloor = GameObject.Find("Apartment/SecondFloor");

            Assert.That(westWall.transform.position.x, Is.EqualTo(-6f).Within(0.01f));
            Assert.That(eastWall.transform.position.x, Is.EqualTo(6f).Within(0.01f));
            Assert.That(secondFloor, Is.Not.Null);

            GameObject mainSlab = GameObject.Find("Apartment/SecondFloor/FloorSlabs/MainSlab");
            Assert.That(mainSlab.transform.position.y + 0.1f, Is.EqualTo(3f).Within(0.01f));
        }

        [Test]
        public void 客厅落地窗与玩家出生点存在()
        {
            GameObject window = GameObject.Find("FloorToCeilingWindow");
            GameObject spawn = GameObject.Find("PlayerSpawn");

            Assert.That(window, Is.Not.Null);
            Assert.That(window.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThan(0));
            Assert.That(spawn, Is.Not.Null);
            Assert.That(spawn.transform.position.y, Is.GreaterThanOrEqualTo(0f));
        }
    }
}

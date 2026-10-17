using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage5InteractionTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [OneTimeSetUp]
        public void 打开公寓场景()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void 至少一扇门支持桌面和VR交互()
        {
            DoorInteractable[] doors = Object.FindObjectsByType<DoorInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(doors.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(doors[0].GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(doors[0].GetComponent<XRSimpleInteractable>(), Is.Not.Null);

            doors[0].SetOpenImmediate(true);
            Assert.That(doors[0].IsOpen, Is.True);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(doors[0].transform.localEulerAngles.y, 90f)), Is.LessThan(0.1f));
            doors[0].SetOpenImmediate(false);
        }

        [Test]
        public void 至少一个灯开关可以切换灯光()
        {
            LightSwitch[] switches = Object.FindObjectsByType<LightSwitch>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(switches.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(switches[0].GetComponent<XRSimpleInteractable>(), Is.Not.Null);

            switches[0].SetState(true);
            Assert.That(switches[0].IsOn, Is.True);
            switches[0].Interact();
            Assert.That(switches[0].IsOn, Is.False);
            switches[0].SetState(true);
        }

        [Test]
        public void 桌面相机具有交互射线()
        {
            DesktopInteractionRaycaster raycaster = Object.FindObjectsByType<DesktopInteractionRaycaster>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            Assert.That(raycaster, Is.Not.Null);
        }
    }
}

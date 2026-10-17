using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainyApartmentVR.Tests
{
    public sealed class Stage6AtmosphereTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";

        [OneTimeSetUp]
        public void 打开公寓场景()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void 全局后期只使用舒适的颜色和轻微泛光()
        {
            Volume volume = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.name == "RainyNightGlobalVolume");
            Assert.That(volume, Is.Not.Null);
            Assert.That(volume.isGlobal, Is.True);
            Assert.That(volume.sharedProfile.TryGet(out ColorAdjustments color), Is.True);
            Assert.That(volume.sharedProfile.TryGet(out Bloom bloom), Is.True);
            Assert.That(color.saturation.value, Is.LessThan(0f));
            Assert.That(bloom.intensity.value, Is.LessThanOrEqualTo(0.25f));
            Assert.That(volume.sharedProfile.components.Any(component => component.GetType().Name == "MotionBlur"), Is.False);
        }

        [Test]
        public void 窗外城市为低成本程序化占位建筑()
        {
            GameObject city = GameObject.Find("ProceduralCity");
            Assert.That(city, Is.Not.Null);
            Assert.That(city.transform.childCount, Is.EqualTo(28));
            Assert.That(city.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(0));
            Assert.That(city.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(45));
        }

        [Test]
        public void 雨只在窗外且粒子数量受控()
        {
            GameObject rain = GameObject.Find("RainFX");
            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.transform.position.z, Is.GreaterThan(4.5f));
            ParticleSystem particles = rain.GetComponent<ParticleSystem>();
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.maxParticles, Is.LessThanOrEqualTo(1200));
        }

        [Test]
        public void 落地窗玻璃和环境声音源均已配置()
        {
            Material glass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Greybox_Glass.mat");
            Assert.That(glass, Is.Not.Null);
            Assert.That(glass.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(glass.GetColor("_BaseColor").a, Is.LessThan(0.5f));

            GameObject rainAudio = GameObject.Find("RainAudioSource");
            GameObject cityAudio = GameObject.Find("CityAmbienceAudioSource");
            Assert.That(rainAudio?.GetComponent<AudioSource>(), Is.Not.Null);
            Assert.That(cityAudio?.GetComponent<AudioSource>(), Is.Not.Null);
        }
    }
}

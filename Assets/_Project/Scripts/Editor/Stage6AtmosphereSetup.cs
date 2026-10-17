using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainyApartmentVR.Editor
{
    public static class Stage6AtmosphereSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Apartment.unity";
        private const string MaterialFolder = "Assets/_Project/Materials";
        private const string VolumeProfilePath = "Assets/_Project/Settings/RainyNightVolume.asset";

        [MenuItem("Rainy Apartment/配置蓝调雨夜氛围")]
        public static void ConfigureAtmosphere()
        {
            EditorSceneManager.OpenScene(ScenePath);

            GameObject oldRoot = GameObject.Find("RainyNightAtmosphere");
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject("RainyNightAtmosphere");
            ConfigureRenderSettings();
            ConfigureVolume(root.transform);
            ConfigureCameras();
            ConfigureExistingLighting();
            ConfigureGlass();
            CreateProceduralCity(root.transform);
            CreateRain(root.transform);
            CreateAudioPlaceholders(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath))
            {
                throw new System.InvalidOperationException("雨夜氛围场景保存失败。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("阶段 6：冷蓝雨夜、程序化城市、窗外降雨与环境声音源结构已配置。");
        }

        public static void ConfigureFromCommandLine()
        {
            ConfigureAtmosphere();
        }

        public static void RenderPreviewFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Camera camera = GameObject.Find("DesktopCamera")?.GetComponent<Camera>();
            if (camera == null)
            {
                throw new System.InvalidOperationException("找不到 DesktopCamera，无法渲染预览。");
            }

            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            bool oldEnabled = camera.enabled;
            camera.gameObject.SetActive(true);
            camera.enabled = true;
            camera.transform.position = new Vector3(-1.1f, 1.65f, 1.5f);
            camera.transform.LookAt(new Vector3(-1.1f, 1.7f, 8f));

            ParticleSystem rain = GameObject.Find("RainFX")?.GetComponent<ParticleSystem>();
            if (rain != null)
            {
                rain.Simulate(1.4f, true, true, true);
            }

            var renderTexture = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();

            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/stage6-preview.png", image.EncodeToPNG());

            camera.targetTexture = null;
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.enabled = oldEnabled;
            RenderTexture.active = null;
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(image);
            Debug.Log("阶段 6 预览图已生成。" );
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.13f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.055f, 0.09f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.018f, 0.03f);
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.025f, 0.06f, 0.11f);

            Material sky = CreateOrUpdateMaterial("RainyNightSky", Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.025f, 0.06f, 0.12f));
            sky.SetColor("_GroundColor", new Color(0.008f, 0.012f, 0.02f));
            sky.SetFloat("_Exposure", 0.16f);
            sky.SetFloat("_AtmosphereThickness", 0.45f);
            RenderSettings.skybox = sky;
        }

        private static void ConfigureVolume(Transform parent)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            foreach (VolumeComponent component in profile.components.Where(item => item != null).ToArray())
            {
                profile.Remove(component.GetType());
                Object.DestroyImmediate(component, true);
            }

            ColorAdjustments color = profile.Add<ColorAdjustments>();
            AssetDatabase.AddObjectToAsset(color, profile);
            color.active = true;
            color.postExposure.Override(-0.9f);
            color.contrast.Override(10f);
            color.saturation.Override(-34f);
            color.colorFilter.Override(new Color(0.58f, 0.72f, 1f));

            Bloom bloom = profile.Add<Bloom>();
            AssetDatabase.AddObjectToAsset(bloom, profile);
            bloom.active = true;
            bloom.intensity.Override(0.16f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            GameObject volumeObject = new GameObject("RainyNightGlobalVolume");
            volumeObject.transform.SetParent(parent);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static void ConfigureCameras()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = true;
            }
        }

        private static void ConfigureExistingLighting()
        {
            GameObject directionalObject = GameObject.Find("Directional Light");
            if (directionalObject != null)
            {
                Light directional = directionalObject.GetComponent<Light>();
                directional.intensity = 0.22f;
                directional.color = new Color(0.45f, 0.62f, 0.9f);
                directional.shadows = LightShadows.Soft;
            }

            GameObject warmLightObject = GameObject.Find("LivingRoomInteractiveLight");
            if (warmLightObject != null)
            {
                warmLightObject.GetComponent<Light>().intensity = 140f;
            }
        }

        private static void ConfigureGlass()
        {
            Material glass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Greybox_Glass.mat");
            if (glass == null)
            {
                return;
            }

            glass.SetColor("_BaseColor", new Color(0.16f, 0.34f, 0.52f, 0.07f));
            glass.SetFloat("_Smoothness", 0.92f);
            ConfigureTransparentMaterial(glass);
            EditorUtility.SetDirty(glass);
        }

        private static void CreateProceduralCity(Transform parent)
        {
            GameObject city = new GameObject("ProceduralCity");
            city.transform.SetParent(parent);

            Material buildingMaterial = CreateLitMaterial(
                "CityBuilding", new Color(0.018f, 0.028f, 0.05f), 0.12f);
            Material windowMaterial = CreateLitMaterial(
                "CityWindowEmission", new Color(0.12f, 0.16f, 0.22f), 0.35f);
            windowMaterial.EnableKeyword("_EMISSION");
            windowMaterial.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.18f) * 2.2f);

            var random = new System.Random(61427);
            for (int index = 0; index < 28; index++)
            {
                float width = Mathf.Lerp(2.8f, 6.5f, (float)random.NextDouble());
                float depth = Mathf.Lerp(2.8f, 6.2f, (float)random.NextDouble());
                float height = Mathf.Lerp(9f, 28f, (float)random.NextDouble());
                float x = Mathf.Lerp(-30f, 30f, (float)random.NextDouble());
                float z = Mathf.Lerp(12f, 46f, (float)random.NextDouble());
                float baseHeight = -8f;

                GameObject building = new GameObject($"CityBuilding_{index + 1:00}");
                building.transform.SetParent(city.transform);
                CreateCube("BuildingBody", building.transform,
                    new Vector3(x, baseHeight + height * 0.5f, z),
                    new Vector3(width, height, depth), buildingMaterial, false);

                int lightCount = 1 + random.Next(0, 3);
                for (int lightIndex = 0; lightIndex < lightCount; lightIndex++)
                {
                    float localX = Mathf.Lerp(-width * 0.3f, width * 0.3f, (float)random.NextDouble());
                    float localY = Mathf.Lerp(-height * 0.15f, height * 0.35f, (float)random.NextDouble());
                    CreateCube($"Window_{lightIndex + 1}", building.transform,
                        new Vector3(x + localX, baseHeight + height * 0.5f + localY, z - depth * 0.5f - 0.02f),
                        new Vector3(0.35f, 0.45f, 0.04f), windowMaterial, false);
                }
            }
        }

        private static void CreateRain(Transform parent)
        {
            Material rainMaterial = CreateOrUpdateMaterial(
                "RainParticle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            rainMaterial.SetColor("_BaseColor", new Color(0.48f, 0.7f, 1f, 0.48f));
            ConfigureTransparentMaterial(rainMaterial);

            GameObject rainObject = new GameObject("RainFX");
            rainObject.transform.SetParent(parent);
            rainObject.transform.position = new Vector3(-1f, 7f, 13f);

            ParticleSystem particles = rainObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 2.1f;
            main.startSpeed = 0f;
            main.startSize = 0.035f;
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.65f, 0.8f, 1f, 0.62f);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 360f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 0.2f, 20f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-12f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.7f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4.5f;
            renderer.velocityScale = 0.08f;
            renderer.sharedMaterial = rainMaterial;
        }

        private static void CreateAudioPlaceholders(Transform parent)
        {
            CreateAudioSource("RainAudioSource", parent, 0.55f);
            CreateAudioSource("CityAmbienceAudioSource", parent, 0.3f);
        }

        private static void CreateAudioSource(string name, Transform parent, float volume)
        {
            GameObject audioObject = new GameObject(name);
            audioObject.transform.SetParent(parent);
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.volume = volume;
            source.spatialBlend = 0f;
        }

        private static Material CreateLitMaterial(string name, Color color, float smoothness)
        {
            Material material = CreateOrUpdateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateMaterial(string name, Shader shader)
        {
            if (shader == null)
            {
                throw new System.InvalidOperationException($"找不到用于 {name} 的着色器。");
            }

            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            }

            return cube;
        }
    }
}

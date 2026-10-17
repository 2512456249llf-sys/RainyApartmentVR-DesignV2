using System;
using System.IO;
using System.Linq;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    public static class PlayerAssetsInstaller
    {
        public const string AvatarPath = "Assets/_Project/PlayerAvatar/群青路.vrm";
        public const string SofaPath = "Assets/_Project/Props/粉色猪沙发/粉色猪懒人沙发.glb";
        public const string AvatarName = "玩家身体_群青路";
        public const string SofaName = "粉色猪懒人沙发_可移动";

        [MenuItem("雨夜公寓/加入玩家身体和可移动猪沙发")]
        public static void Install()
        {
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            var sofaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SofaPath);
            if (!avatarPrefab || !sofaPrefab) throw new InvalidOperationException("两份用户模型尚未完成导入。");
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var rig = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "XROrigin_PCVR");
            if (!rig) throw new InvalidOperationException("缺少现有头显追踪节点，不能另建相机代替它。");
            var camera = rig.GetComponentInChildren<Camera>(true);
            var hands = rig.GetComponentsInChildren<VRHandPose>(true);
            if (!camera || hands.Length != 2) throw new InvalidOperationException("原玩家相机或两只手未正确安装。");
            if (rig.transform.Find(AvatarName) || Find(SofaName))
                throw new InvalidOperationException("玩家模型或猪沙发已经存在，请先检查，不覆盖已有摆放。");

            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
            avatar.name = AvatarName;
            avatar.transform.SetParent(rig.transform, false);
            var animator = avatar.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.isHuman) throw new InvalidOperationException("玩家模型没有可用人形骨架。");
            animator.runtimeAnimatorController = null;
            int headLayer = EnsureHeadLayer();
            PrepareFirstPersonMeshes(avatar, animator.GetBoneTransform(HumanBodyBones.Head), headLayer);
            camera.cullingMask &= ~(1 << headLayer);
            camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .03f);
            var body = avatar.AddComponent<VRPlayerBody>();
            body.avatarAnimator = animator;
            body.headTarget = camera.transform;
            body.trackingOrigin = rig.transform;
            body.leftTouchHand = hands.First(h => h.IsLeftHand);
            body.rightTouchHand = hands.First(h => !h.IsLeftHand);
            body.leftHandTarget = body.leftTouchHand.transform;
            body.rightHandTarget = body.rightTouchHand.transform;
            foreach (var hand in hands)
                foreach (var renderer in hand.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            // 保留旧手的追踪、触摸壳和握把数值，只替换可见的皮肤和衣袖。

            var sofa = (GameObject)PrefabUtility.InstantiatePrefab(sofaPrefab);
            sofa.name = SofaName;
            var raw = BoundsOf(sofa);
            sofa.transform.localScale = Vector3.one * (1.3f / Mathf.Max(.01f, raw.size.x));
            sofa.transform.rotation = Quaternion.identity;
            var scaled = BoundsOf(sofa);
            sofa.transform.position = new Vector3(-2.4f, .025f - scaled.min.y, .45f);
            foreach (var filter in sofa.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.Contains("Arm") && !filter.name.Contains("Back") &&
                    !filter.name.Contains("BaseRing") && !filter.name.Contains("Seat")) continue;
                var collider = filter.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            var rigidbody = sofa.AddComponent<Rigidbody>();
            rigidbody.mass = 8f;
            rigidbody.useGravity = true;
            rigidbody.isKinematic = false;
            rigidbody.linearDamping = .35f;
            rigidbody.angularDamping = 3f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var grab = sofa.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.attachEaseInTime = .15f;
            grab.colliders.Clear();
            grab.colliders.AddRange(sofa.GetComponentsInChildren<Collider>());
            sofa.AddComponent<MovableFurniture>();
            Verify(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("安装完成：玩家身体双手复用原手柄，男主未替换；猪沙发尺寸=" + BoundsOf(sofa).size + "，已安装手柄抓取和落地物理。");
        }

        private static Transform Find(string name) => EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);

        private static int EnsureHeadLayer()
        {
            const string name = "玩家自身头部";
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 30; i >= 8; i--)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    manager.ApplyModifiedProperties();
                    return i;
                }
            throw new InvalidOperationException("没有空闲图层隐藏玩家自身头部。");
        }

        private static void PrepareFirstPersonMeshes(GameObject avatar, Transform head, int headLayer)
        {
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
                var eraseBones = Enumerable.Range(0, renderer.bones.Length)
                    .Where(i => renderer.bones[i] && (renderer.bones[i] == head || renderer.bones[i].IsChildOf(head))).ToArray();
                if (eraseBones.Length == 0) continue;
                var erased = UniGLTF.MeshUtility.BoneMeshEraser.CreateErasedMesh(renderer.sharedMesh, eraseBones);
                renderer.gameObject.layer = headLayer;
                if (Enumerable.Range(0, erased.subMeshCount).Sum(i => (long)erased.GetIndexCount(i)) == 0)
                {
                    Object.DestroyImmediate(erased);
                    continue;
                }
                string path = "Assets/_Project/PlayerAvatar/第一人称_" + renderer.name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) throw new InvalidOperationException("第一人称网格已经存在，不覆盖：" + path);
                AssetDatabase.CreateAsset(erased, path);
                var firstPerson = new GameObject("第一人称身体_" + renderer.name);
                firstPerson.transform.SetParent(renderer.transform, false);
                var copy = firstPerson.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = erased;
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.bones = renderer.bones;
                copy.rootBone = renderer.rootBone;
                copy.localBounds = renderer.localBounds;
                copy.updateWhenOffscreen = true;
                Debug.Log("第一人称处理：" + renderer.name + "，保留身体网格，去除头部相关三角面。");
            }
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Verify(UnityEngine.SceneManagement.Scene scene)
        {
            var avatar = Find(AvatarName);
            var sofa = Find(SofaName);
            var npc = Find(CharacterAnimationSetup.CharacterName);
            if (!npc || !npc.GetComponent<VoiceDialogue>()) throw new InvalidOperationException("原千问男主组件丢失。");
            var body = avatar ? avatar.GetComponent<VRPlayerBody>() : null;
            if (!body || !body.headTarget || !body.leftHandTarget || !body.rightHandTarget)
                throw new InvalidOperationException("玩家追踪未接好。");
            if (!sofa || !sofa.GetComponent<XRGrabInteractable>() || !sofa.GetComponent<MovableFurniture>() ||
                sofa.GetComponentsInChildren<Collider>().Length < 4 || sofa.GetComponent<Rigidbody>().isKinematic)
                throw new InvalidOperationException("沙发抓取或物理未接好。");
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true).Concat(sofa.GetComponentsInChildren<Renderer>(true)))
                if (renderer.sharedMaterials.Any(m => !m || !m.shader || !m.shader.isSupported))
                    throw new InvalidOperationException("新增模型存在缺失或不支持的材质：" + renderer.name);
        }

        public static void Preview()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var sofa = Find(SofaName);
            if (!sofa) throw new InvalidOperationException("猪沙发尚未安装。");
            var cameraObject = new GameObject("临时模型验收相机");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            cameraObject.transform.position = sofa.position + new Vector3(1.6f, 1.3f, -2f);
            cameraObject.transform.LookAt(sofa.position + Vector3.up * .42f);
            var lightObject = new GameObject("临时模型验收补光");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 1.1f;
            light.transform.position = sofa.position + new Vector3(.5f, 2f, -1f);
            Capture(camera, "猪沙发场景验收");
            var avatar = Find(AvatarName);
            avatar.parent.gameObject.SetActive(true);
            var body = avatar.GetComponent<VRPlayerBody>();
            var floor = new GameObject("临时玩家预览原点");
            floor.transform.position = new Vector3(0f, 100f, 0f);
            var head = new GameObject("临时玩家预览头显");
            head.transform.position = floor.transform.position + Vector3.up * 1.65f;
            var left = new GameObject("临时玩家预览左手");
            var right = new GameObject("临时玩家预览右手");
            left.transform.position = head.transform.position + new Vector3(-.28f, -.42f, .35f);
            right.transform.position = head.transform.position + new Vector3(.28f, -.42f, .35f);
            body.trackingOrigin = floor.transform;
            body.headTarget = head.transform;
            body.leftHandTarget = left.transform;
            body.rightHandTarget = right.transform;
            body.ApplyTracking(10f);
            var firstPersonRenderers = avatar.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("第一人称身体_")).ToArray();
            foreach (var renderer in firstPersonRenderers) renderer.enabled = false;
            light.transform.position = floor.transform.position + new Vector3(.7f, 2f, 1.8f);
            light.intensity = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .17f, .19f);
            camera.transform.position = floor.transform.position + new Vector3(0f, 1.05f, 3f);
            camera.transform.LookAt(floor.transform.position + Vector3.up * .95f);
            Capture(camera, "玩家身体双手姿势验收");
            foreach (var renderer in firstPersonRenderers) renderer.enabled = true;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("玩家自身头部"));
            head.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            body.ApplyTracking(10f);
            camera.transform.SetPositionAndRotation(head.transform.position, head.transform.rotation);
            camera.nearClipPlane = .025f;
            camera.fieldOfView = 80f;
            Capture(camera, "玩家第一人称双手验收");
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(head);
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            // 预览不保存场景，不修改灯光或玩家起点。
        }

        private static void Capture(Camera camera, string name)
        {
            var texture = new RenderTexture(1280, 960, 24);
            var image = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
            image.Apply();
            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/" + name + ".png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }

        public static void PreviewAndBuildCurrent()
        {
            Preview();
            // 构建方法会重新打开已保存的场景，不带入临时预览姿势。
            NaturalActionInstaller.BuildRealtimeDemo();
        }
    }
}

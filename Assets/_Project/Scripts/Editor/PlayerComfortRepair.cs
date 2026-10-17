using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    public static class PlayerComfortRepair
    {
        public static void PreviewAndBuild()
        {
            Preview();
            // 构建入口重新打开已保存场景，临时预览姿态不会带入游戏。
            NaturalActionInstaller.BuildRealtimeDemo();
        }

        public static void Preview()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var body = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<VRPlayerBody>(true)).Single();
            body.trackingOrigin.gameObject.SetActive(true);
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) existing.enabled = false;
            var floor = new GameObject("临时预览原点");
            floor.transform.position = Vector3.up * 100f;
            var head = new GameObject("临时预览头显");
            var left = new GameObject("临时预览左握持点");
            var right = new GameObject("临时预览右握持点");
            var cameraObject = new GameObject("临时预览相机");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.17f, .19f, .21f);
            camera.fieldOfView = 85f;
            camera.nearClipPlane = .025f;
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("玩家自身头部"));
            var lightObject = new GameObject("临时预览补光");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.intensity = 5f; light.range = 6f;
            lightObject.transform.position = floor.transform.position + new Vector3(0f, 2f, 1.5f);
            body.headTarget = head.transform;
            body.leftHandTarget = left.transform;
            body.rightHandTarget = right.transform;
            body.trackingOrigin = floor.transform;
            head.transform.position = floor.transform.position + Vector3.up * 1.65f;
            left.transform.position = head.transform.position + new Vector3(-.27f, -.35f, .34f);
            right.transform.position = head.transform.position + new Vector3(.27f, -.35f, .34f);
            head.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            body.ApplyTracking(1f);
            camera.transform.SetPositionAndRotation(head.transform.position, head.transform.rotation);
            Capture(camera, "姿态修复_站立低头");
            head.transform.position -= Vector3.up * .75f;
            left.transform.position -= Vector3.up * .75f;
            right.transform.position -= Vector3.up * .75f;
            for (int i = 0; i < 10; i++) body.ApplyTracking(.1f);
            camera.transform.SetPositionAndRotation(head.transform.position, head.transform.rotation);
            Capture(camera, "姿态修复_蹲身低头");
            head.transform.position += Vector3.up * .75f;
            head.transform.rotation = Quaternion.identity;
            for (int i = 0; i < 10; i++) body.ApplyTracking(.1f);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.layer == LayerMask.NameToLayer("玩家自身头部")) renderer.enabled = true;
                else if (renderer.name.StartsWith("第一人称")) renderer.enabled = false;
            }
            camera.cullingMask = -1;
            camera.transform.position = floor.transform.position + new Vector3(.5f, .95f, 2.3f);
            camera.transform.LookAt(floor.transform.position + Vector3.up * .8f);
            Capture(camera, "姿态修复_身体站姿");
            foreach (var item in new[] { floor, head, left, right, cameraObject, lightObject }) Object.DestroyImmediate(item);
            Debug.Log("站立、蹲身及双手外观预览已生成，未保存临时姿态。");
        }

        private static void Capture(Camera camera, string name)
        {
            var rt = new RenderTexture(1000, 1000, 24);
            var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("BuildLogs");
            File.WriteAllBytes("BuildLogs/" + name + ".png", texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
        }

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var all = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
            var body = all.Select(t => t.GetComponent<VRPlayerBody>()).FirstOrDefault(c => c);
            if (!body) throw new InvalidOperationException("当前场景没有玩家身体，停止修复。");
            foreach (var driver in body.trackingOrigin.GetComponentsInChildren<TrackedPoseDriver>(true))
            {
                Debug.Log("追踪节点：" + driver.name + "，位置绑定=" + string.Join("、", driver.positionInput.action.bindings.Select(b => b.path))
                    + "，旋转绑定=" + string.Join("、", driver.rotationInput.action.bindings.Select(b => b.path)));
            }
            // 原追踪节点使用手柄握持姿态，移除旧外观偏移，但不修改射线或交互器。
            body.leftHandTarget = body.leftTouchHand.transform.parent;
            body.rightHandTarget = body.rightTouchHand.transform.parent;
            body.gripOffset = new Vector3(0f, -.005f, -.01f);
            SplitFirstPersonTorso(body);
            var npc = all.First(t => t.name == CharacterAnimationSetup.CharacterName).gameObject;
            if (!npc.GetComponent<CharacterCrouchPose>()) npc.AddComponent<CharacterCrouchPose>();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
            if (!controller.parameters.Any(p => p.name == "Sitting")) controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
                if (transition.destinationState && (transition.destinationState.name == "Talk" || transition.destinationState.name == "Secret") &&
                    !transition.conditions.Any(c => c.parameter == "Sitting"))
                {
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
                    EditorUtility.SetDirty(transition);
                }
            var sit = controller.layers[0].stateMachine.states.First(s => s.state.name == "SitDown").state;
            sit.speed = 1f;
            // 非循环片段自然保持末帧；无需停止整个 Animator。
            Debug.Log("坐下片段长度=" + (sit.motion as AnimationClip)?.length + "，坐姿末帧保持，动画系统继续运行。");
            EditorUtility.SetDirty(sit);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("已修复当前千问场景：掌心追踪、蹲身视野、交替步态、独立蹲姿；地图和语音配置未重建。");
        }

        private static void SplitFirstPersonTorso(VRPlayerBody body)
        {
            var visible = body.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "第一人称身体_Body");
            if (body.firstPersonUpperTorso) return;
            var source = visible.sharedMesh;
            var vertices = source.vertices;
            var weights = source.boneWeights;
            var hips = body.avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var chest = body.avatarAnimator.GetBoneTransform(HumanBodyBones.Chest);
            var shoulderL = body.avatarAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var shoulderR = body.avatarAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
            var head = body.avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            var torsoBones = new HashSet<int>();
            for (int i = 0; i < visible.bones.Length; i++)
            {
                var bone = visible.bones[i];
                if (!bone) continue;
                bool trunk = bone == chest || bone.IsChildOf(chest);
                bool armOrHead = bone == head || bone.IsChildOf(head) ||
                    shoulderL && (bone == shoulderL || bone.IsChildOf(shoulderL)) ||
                    shoulderR && (bone == shoulderR || bone.IsChildOf(shoulderR));
                if (trunk && !armOrHead) torsoBones.Add(i);
            }
            bool IsUpper(int index)
            {
                var w = weights[index];
                float influence = (torsoBones.Contains(w.boneIndex0) ? w.weight0 : 0f) +
                    (torsoBones.Contains(w.boneIndex1) ? w.weight1 : 0f) +
                    (torsoBones.Contains(w.boneIndex2) ? w.weight2 : 0f) +
                    (torsoBones.Contains(w.boneIndex3) ? w.weight3 : 0f);
                return influence > .45f && visible.transform.TransformPoint(vertices[index]).y > hips.position.y + .22f;
            }
            var lower = Object.Instantiate(source);
            var upper = Object.Instantiate(source);
            int moved = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var keep = new List<int>();
                var near = new List<int>();
                var triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    bool isNear = (IsUpper(triangles[i]) ? 1 : 0) + (IsUpper(triangles[i + 1]) ? 1 : 0) +
                        (IsUpper(triangles[i + 2]) ? 1 : 0) >= 2;
                    var list = isNear ? near : keep;
                    list.Add(triangles[i]); list.Add(triangles[i + 1]); list.Add(triangles[i + 2]);
                    if (isNear) moved++;
                }
                lower.SetTriangles(keep, sub, false);
                upper.SetTriangles(near, sub, false);
            }
            if (moved < 50) throw new InvalidOperationException("上胸识别不完整，停止处理，原模型未改。");
            AssetDatabase.CreateAsset(lower, "Assets/_Project/PlayerAvatar/第一人称身体_舒适版.asset");
            AssetDatabase.CreateAsset(upper, "Assets/_Project/PlayerAvatar/第一人称上胸_舒适版.asset");
            visible.sharedMesh = lower;
            var item = new GameObject("第一人称上胸_蹲身避让");
            item.transform.SetParent(visible.transform, false);
            var renderer = item.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = upper;
            renderer.sharedMaterials = visible.sharedMaterials;
            renderer.bones = visible.bones;
            renderer.rootBone = visible.rootBone;
            renderer.localBounds = visible.localBounds;
            renderer.updateWhenOffscreen = true;
            body.firstPersonUpperTorso = renderer;
            Debug.Log("第一人称上胸独立避让三角面=" + moved + "；双臂、腰腿保持可见，原人物网格未改。");
        }
    }
}

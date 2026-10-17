using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class CharacterAnimationSetup
    {
        public const string Folder = "Assets/_Project/Characters/动画";
        public const string ControllerPath = Folder + "/男主_动作.controller";
        public const string CharacterName = "男主_雨夜公寓";

        private static readonly string[][] Clips =
        {
            new[] { "Ch36_nonPBR@Idle.fbx", "Idle", "1" },
            new[] { "Ch36_nonPBR@Standard Walk.fbx", "Walk", "1" },
            new[] { "Ch36_nonPBR@Stand To Sit.fbx", "SitDown", "0" },
            new[] { "Ch36_nonPBR@Stand Up.fbx", "StandUp", "0" },
            new[] { "Ch36_nonPBR@Talking.fbx", "Talk", "1" },
            new[] { "Ch36_nonPBR@Telling A Secret.fbx", "Secret", "1" },
        };

        public static void ConfigureImports()
        {
            foreach (var entry in Clips)
            {
                var path = Folder + "/" + entry[0];
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("找不到动作文件：" + path);
                bool loop = entry[2] == "1";
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importConstraints = false;
                var clips = importer.defaultClipAnimations;
                if (clips.Length == 0) throw new InvalidOperationException("没有动画：" + path);
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = entry[1];
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                    // 原待机片段后半段包含大幅反向扭腰，只保留前两秒自然呼吸姿势。
                    if (entry[1] == "Idle")
                    {
                        clips[i].firstFrame = 0f;
                        clips[i].lastFrame = 60f;
                    }
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log("动作已配置：" + entry[1] + "（循环=" + loop + "）");
            }
        }

        [MenuItem("雨夜公寓/修复男主待机扭腰")]
        public static void RepairIdleImport()
        {
            string path = Folder + "/Ch36_nonPBR@Idle.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("找不到原有待机动作。");
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) throw new InvalidOperationException("待机动作没有片段。");
            foreach (var clip in clips)
            {
                clip.name = "Idle";
                clip.firstFrame = 0f;
                clip.lastFrame = 60f;
                clip.loopTime = true;
                clip.loopPose = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log("待机动作已修复：仅循环前两秒站姿，避开中段反向扭腰。");
        }

        [MenuItem("雨夜公寓/给男主接 Mixamo 动作")]
        public static void SetupAndInstall()
        {
            ConfigureImports();
            AssetDatabase.Refresh();

            var clipAssets = new Dictionary<string, AnimationClip>();
            foreach (var entry in Clips)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + entry[0])
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview") && c.name == entry[1]);
                if (!clip)
                    clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + entry[0])
                        .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (!clip) throw new InvalidOperationException("加载不到片段：" + entry[1]);
                clipAssets[entry[1]] = clip;
                Debug.Log("片段：" + clip.name + "，时长=" + clip.length.ToString("0.00") + "秒");
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talk", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stand", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;

            AnimatorState Make(string name, string clip)
            {
                var state = sm.AddState(name);
                state.motion = clipAssets[clip];
                return state;
            }

            var idle = Make("Idle", "Idle");
            sm.defaultState = idle;
            var walk = Make("Walk", "Walk");
            var sitDown = Make("SitDown", "SitDown");
            var standUp = Make("StandUp", "StandUp");
            var talk = Make("Talk", "Talk");
            var secret = Make("Secret", "Secret");

            AnimatorStateTransition To(AnimatorState from, AnimatorState to, float duration, bool exitTime)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = exitTime;
                t.duration = duration;
                return t;
            }

            var t1 = To(idle, walk, 0.15f, false);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var t2 = To(walk, idle, 0.15f, false);
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var toSit = sm.AddAnyStateTransition(sitDown);
            toSit.hasExitTime = false;
            toSit.duration = 0.2f;
            toSit.AddCondition(AnimatorConditionMode.If, 0f, "Sit");

            var toStand = sm.AddAnyStateTransition(standUp);
            toStand.hasExitTime = false;
            toStand.duration = 0.2f;
            toStand.AddCondition(AnimatorConditionMode.If, 0f, "Stand");

            var t3 = To(standUp, idle, 0.2f, true);
            t3.exitTime = 0.92f;

            var toTalk = sm.AddAnyStateTransition(talk);
            toTalk.hasExitTime = false;
            toTalk.duration = 0.25f;
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talk");
            var t4 = To(talk, idle, 0.25f, true);
            t4.exitTime = 0.97f;

            EnsureSpeakingTransitions(controller);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("动作控制器已生成：" + ControllerPath);

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == CharacterName);
            if (!character) throw new InvalidOperationException("场景里找不到 " + CharacterName);
            var animator = character.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("男主人形骨架无效");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("动作已接到男主身上，场景已保存。");
        }

        [MenuItem("雨夜公寓/修复男主说话动作连接")]
        public static void UpgradeSpeaking()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) throw new InvalidOperationException("没有找到现有动作控制器。");
            EnsureSpeakingTransitions(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("说话动作已改为跟随语音播放全程循环，停止播放后回到待机。");
        }

        private static void EnsureSpeakingTransitions(AnimatorController controller)
        {
            if (!controller.parameters.Any(p => p.name == "Speaking"))
                controller.AddParameter("Speaking", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var talk = machine.states.FirstOrDefault(s => s.state.name == "Talk").state;
            var idle = machine.states.FirstOrDefault(s => s.state.name == "Idle").state;
            var walk = machine.states.FirstOrDefault(s => s.state.name == "Walk").state;
            if (!talk || !idle || !walk) throw new InvalidOperationException("现有动作控制器缺少待机、行走或说话状态。");

            foreach (var transition in machine.anyStateTransitions.ToArray())
                if (transition.destinationState == talk) machine.RemoveAnyStateTransition(transition);
            foreach (var transition in talk.transitions.ToArray())
                if (transition.destinationState == idle) talk.RemoveTransition(transition);

            var begin = machine.AddAnyStateTransition(talk);
            begin.hasExitTime = false;
            begin.canTransitionToSelf = false;
            begin.duration = .18f;
            begin.AddCondition(AnimatorConditionMode.If, 0f, "Speaking");
            begin.AddCondition(AnimatorConditionMode.Less, .1f, "Speed");

            var finish = talk.AddTransition(idle);
            finish.hasExitTime = false;
            finish.duration = .18f;
            finish.AddCondition(AnimatorConditionMode.IfNot, 0f, "Speaking");
            var move = talk.AddTransition(walk);
            move.hasExitTime = false;
            move.duration = .15f;
            move.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
            EditorUtility.SetDirty(controller);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    public static class NaturalActionInstaller
    {
        private const string Folder = "Assets/_Project/Characters/自然动作";
        private const string MaskPath = Folder + "/上半身动作.mask";
        private static readonly string[] Names =
        {
            "thinking", "relieved", "surprised", "shrug", "greeting", "laugh", "agree",
            "weight_shift", "thumbs_up", "talk_soft", "talk_animated",
            "nod_hard", "nod_yes", "nod_long", "nod_thoughtful", "shake_annoyed", "shake_thoughtful",
            "look_around", "look_around_2", "look_around_nervous", "look_away_gesture", "agree_soft",
            "talking", "talking_2", "point", "point_2", "point_gesture", "point_gesture_2",
            "point_forward", "point_angry", "clap", "clap_2", "clap_stand", "clap_stand_2",
            "bored", "excited", "bow_formal", "bow_informal", "sad", "sad_2",
            "breathing", "happy", "happy_2", "weight_shift_gesture"
        };

        [MenuItem("雨夜公寓/安装自然动作库")]
        public static void SetupAndInstall()
        {
            AssetDatabase.Refresh();
            foreach (string name in Names)
            {
                string path = Folder + "/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("缺少动作文件：" + path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                var clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) throw new InvalidOperationException("动作没有可读片段：" + path);
                clips[0].name = "Action_" + name;
                clips[0].loopTime = false;
                clips[0].loopPose = false;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
            if (!controller) throw new InvalidOperationException("人物原有动画控制器不存在。");
            ConfigureController(controller);

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == CharacterAnimationSetup.CharacterName);
            if (!character) throw new InvalidOperationException("场景里没有找到现有人物。");
            var animator = character.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("人物人形骨架无效，动作不会强行安装。");
            animator.runtimeAnimatorController = controller;
            var action = character.GetComponent<CharacterActionLibrary>();
            if (!action) action = character.gameObject.AddComponent<CharacterActionLibrary>();
            action.animator = animator;
            action.behaviour = character.GetComponent<CharacterBehaviour>();
            action.performance = character.GetComponent<CharacterVoicePerformance>();
            var dialogue = character.GetComponent<VoiceDialogue>();
            if (dialogue) dialogue.actions = action;
            EditorUtility.SetDirty(character.gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("自然动作已接到现有人物；地图、模型及骨架均未重建。");
        }

        public static void ConfigureController(AnimatorController controller)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (!mask)
            {
                mask = new AvatarMask();
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            int index = Array.FindIndex(controller.layers, layer => layer.name == "自然动作");
            if (index < 0)
            {
                controller.AddLayer("自然动作");
                index = controller.layers.Length - 1;
            }
            var layers = controller.layers;
            var layerConfig = layers[index];
            layerConfig.avatarMask = mask;
            // 无动作时完全退回基础待机层，避免遮罩把腰部留在另一套姿势。
            layerConfig.defaultWeight = 0f;
            layerConfig.blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index] = layerConfig;
            controller.layers = layers;
            var machine = controller.layers[index].stateMachine;
            var empty = machine.states.FirstOrDefault(s => s.state.name == "OverlayEmpty").state;
            if (!empty) empty = machine.AddState("OverlayEmpty");
            machine.defaultState = empty;
            foreach (string name in Names)
            {
                string path = Folder + "/" + name + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => c.name == "Action_" + name);
                if (!clip) throw new InvalidOperationException("动作导入失败：" + name);
                var state = machine.states.FirstOrDefault(s => s.state.name == "Action_" + name).state;
                if (!state) state = machine.AddState("Action_" + name);
                state.motion = clip;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
        }

        [MenuItem("雨夜公寓/验收实时语音和自然动作")]
        public static void Verify()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
            if (!controller) throw new InvalidOperationException("动画控制器不存在。");
            var layer = controller.layers.FirstOrDefault(item => item.name == "自然动作");
            if (layer == null || !layer.avatarMask) throw new InvalidOperationException("自然动作层或上半身遮罩缺失。");
            foreach (string name in Names)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + name + ".fbx")
                    .OfType<AnimationClip>().FirstOrDefault(item => item.name == "Action_" + name);
                if (!clip || !clip.humanMotion) throw new InvalidOperationException("动作没有完成人形导入：" + name);
                if (!layer.stateMachine.states.Any(item => item.state.name == "Action_" + name))
                    throw new InvalidOperationException("动作尚未接入控制器：" + name);
            }
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == CharacterAnimationSetup.CharacterName);
            if (!character) throw new InvalidOperationException("原人物不在场景中。");
            var dialogue = character.GetComponent<VoiceDialogue>();
            var action = character.GetComponent<CharacterActionLibrary>();
            if (!dialogue || !action || dialogue.actions != action || !dialogue.characterAudio)
                throw new InvalidOperationException("实时语音或动作组件未正确安装。");
            if (dialogue.characterAudio.spatialBlend < .99f)
                throw new InvalidOperationException("角色声音不是空间声音。");
            Debug.Log("验收通过：四十四段人形动作、上半身动画层、现有人物和三维声音都已接好。联网语音需填写密钥后另行试听。");
        }

        [MenuItem("雨夜公寓/构建实时语音试玩版")]
        public static void BuildRealtimeDemo()
        {
            Verify();
            const string folder = "Builds/DesignV2_Realtime";
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { DesignApartmentBuilder.ScenePath },
                locationPathName = folder + "/RainyApartmentRealtime.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("实时语音试玩版构建失败，错误数：" + report.summary.totalErrors);
            Debug.Log("实时语音试玩版已构建：" + folder + "/RainyApartmentRealtime.exe");
        }

    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    // 仅读取现有角色和待机片段，用于定位躯干扭转，不保存场景。
    public static class CharacterAnimationAudit
    {
        public static void RepairAndVerify()
        {
            CharacterAnimationSetup.RepairIdleImport();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
            if (!controller) throw new InvalidOperationException("找不到人物动作控制器。");
            NaturalActionInstaller.ConfigureController(controller);
            AssetDatabase.SaveAssets();
            NaturalActionInstaller.Verify();
            AuditIdle();
        }

        public static void AuditIdle()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == CharacterAnimationSetup.CharacterName);
            var animator = character.GetComponentInChildren<Animator>(true);
            var clip = AssetDatabase.LoadAllAssetsAtPath(CharacterAnimationSetup.Folder + "/Ch36_nonPBR@Idle.fbx")
                .OfType<AnimationClip>().First(item => item.name == "Idle");
            if (!animator || !animator.avatar || !animator.avatar.isHuman)
                throw new InvalidOperationException("无法检查待机动作：人物骨架无效。");

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            Debug.Log($"待机检查：片段时长={clip.length:0.00}秒，人物={character.name}，控制器={animator.runtimeAnimatorController?.name}");
            AnimationMode.StartAnimationMode();
            try
            {
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    var pose = new HumanPose();
                    for (int i = 0; i <= 8; i++)
                    {
                        float time = clip.length * i / 8f;
                        AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
                        handler.GetHumanPose(ref pose);
                        string muscles = string.Join("，", Enumerable.Range(0, HumanTrait.MuscleCount)
                            .Where(index => HumanTrait.MuscleName[index].Contains("Twist"))
                            .Take(8).Select(index => HumanTrait.MuscleName[index] + "=" + pose.muscles[index].ToString("0.00")));
                        Debug.Log($"待机检查：时间={time:0.00}；髋={hips.localEulerAngles}；脊柱={spine.localEulerAngles}；胸={chest.localEulerAngles}；{muscles}");
                    }
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
        }
    }
}

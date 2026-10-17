using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class CharacterInteractionTests
    {
        [UnityTest]
        public IEnumerator 新男主站姿与拥抱触发验收()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;

            var interaction = Object.FindFirstObjectByType<CharacterInteraction>();
            Assert.IsNotNull(interaction, "缺少男主拥抱交互。");
            var animator = interaction.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator);
            Assert.IsTrue(animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, "男主人形骨架无效。");
            Assert.IsNotNull(animator.runtimeAnimatorController, "现有说话与走路动作控制器不应被拥抱逻辑清掉。");

            Assert.GreaterOrEqual(interaction.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 3, "新人物网格不完整。");

            var camera = Camera.main;
            Assert.IsNotNull(camera, "没有玩家相机。");
            Vector3 front = interaction.transform.position + interaction.transform.forward * 1.05f;
            camera.transform.position = front + Vector3.up * 1.62f;
            camera.transform.LookAt(interaction.transform.position + Vector3.up * 1.42f);
            yield return null;

            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 leftBefore = leftHand.position;
            Vector3 rightBefore = rightHand.position;
            interaction.BeginHug();
            Assert.IsTrue(interaction.IsHugging, "满足距离与朝向后没有进入拥抱状态。");
            yield return new WaitForSeconds(.9f);
            Assert.Greater(leftHand.position.y, leftBefore.y + .18f, "左手没有抬起到拥抱位置。");
            Assert.Greater(rightHand.position.y, rightBefore.y + .10f, "右手没有抬起到拥抱位置。");
            Assert.IsFalse(float.IsNaN(leftHand.position.x) || float.IsNaN(rightHand.position.x), "动作产生了无效骨骼坐标。");

            camera.transform.position = interaction.transform.position - interaction.transform.forward * 1.05f + Vector3.up * 1.62f;
            camera.transform.LookAt(interaction.transform.position + Vector3.up * 1.42f);
            yield return null;
            yield return new WaitForSeconds(4.8f);
            interaction.BeginHug();
            Assert.IsFalse(interaction.IsHugging, "玩家在人物背后时不应触发拥抱。");
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class IdleStateTests
    {
        [UnityTest]
        public IEnumerator 人物静止时仍播放原待机动作()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return new WaitForSeconds(.6f);
            var behaviour = Object.FindFirstObjectByType<CharacterBehaviour>();
            Assert.IsNotNull(behaviour);
            var animator = behaviour.animator;
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            Assert.IsFalse(behaviour.IsWalking);
            Assert.IsFalse(behaviour.IsHandHeld);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "静止时没有回到原待机状态。");
            var shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.IsNotNull(shoulder);
            Assert.IsNotNull(hand);
            Debug.Log("待机运行姿势：左肩高度=" + shoulder.position.y.ToString("F3") +
                      "，左手高度=" + hand.position.y.ToString("F3"));
            Assert.Less(hand.position.y, shoulder.position.y - .12f, "人物仍然是张臂的未播放动画姿势。");
        }
    }
}

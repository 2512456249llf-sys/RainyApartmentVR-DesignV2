using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class VoiceDialogueTests
    {
        [UnityTest]
        public IEnumerator 现有人物语音组件与空间声音验收()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;
            var dialogue = Object.FindFirstObjectByType<VoiceDialogue>();
            Assert.IsNotNull(dialogue, "人物没有语音对话组件。");
            Assert.IsNotNull(dialogue.GetComponent<CharacterInteraction>(), "原有拥抱交互丢失。");
            Assert.IsNotNull(dialogue.performance, "人物说话动作组件缺失。");
            Assert.IsNotNull(dialogue.characterAudio, "人物空间声音组件缺失。");
            Assert.AreEqual(1f, dialogue.characterAudio.spatialBlend, .001f, "角色声音没有设为三维定位。");
            Assert.IsFalse(string.IsNullOrWhiteSpace(dialogue.Status), "没有语音状态提示。");
            Assert.AreEqual(VoiceDialogueState.Idle, dialogue.CurrentState);
            Assert.GreaterOrEqual(dialogue.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 3, "现有人物模型丢失。");
        }

        [UnityTest]
        public IEnumerator 说话动作持续播放且过来指令确实移动人物()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;
            var behaviour = Object.FindFirstObjectByType<CharacterBehaviour>();
            Assert.IsNotNull(behaviour);
            var animator = behaviour.animator;
            Assert.IsNotNull(animator.runtimeAnimatorController);
            behaviour.SetSpeaking(true);
            yield return new WaitForSeconds(.6f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Talk"), "声音播放期间没有进入说话动作。");
            behaviour.SetSpeaking(false);
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "声音结束后没有回到待机。");

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            camera.transform.position = behaviour.transform.position + behaviour.transform.forward * 2.6f + Vector3.up * 1.6f;
            yield return null;
            float before = Vector3.Distance(camera.transform.position, behaviour.transform.position);
            behaviour.ComeHere();
            yield return new WaitForSeconds(.8f);
            float after = Vector3.Distance(camera.transform.position, behaviour.transform.position);
            Assert.Less(after, before - .35f, "说‘过来’后人物没有真的向玩家移动。");
        }

        [UnityTest]
        public IEnumerator 动作库走路坐下起立均能衔接且手部触摸探测已安装()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;
            var behaviour = Object.FindFirstObjectByType<CharacterBehaviour>();
            Assert.IsNotNull(behaviour);
            var animator = behaviour.animator;
            Assert.IsNotNull(animator.runtimeAnimatorController);
            behaviour.MoveTo(behaviour.transform.position + behaviour.transform.forward * 3f);
            yield return new WaitForSeconds(.45f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "移动时没有切换到走路动作。");
            behaviour.Stop();
            yield return new WaitForSeconds(.4f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "停止移动后没有回到待机。");
            behaviour.Sit();
            yield return new WaitForSeconds(.45f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown"), "坐下动作没有触发。");
            yield return new WaitForSeconds(2.5f);
            behaviour.Stand();
            yield return new WaitForSeconds(.45f);
            var standState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(standState.IsName("StandUp") || standState.IsName("Idle"), "起立动作没有接回待机。");

            behaviour.Secret();
            yield return new WaitForSeconds(.45f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Secret"), "秘密动作素材已导入，但指令没有触发对应动画。");
            float secretDeadline = Time.time + 15f;
            while (animator.GetCurrentAnimatorStateInfo(0).IsName("Secret") && Time.time < secretDeadline) yield return null;
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("Secret"), "秘密动作结束后没有返回基础状态。");

            var touch = Object.FindFirstObjectByType<CharacterTouchSurfaces>();
            Assert.IsNotNull(touch, "人物触摸表面没有安装。");
            Assert.GreaterOrEqual(touch.Surfaces.Count, 6, "人物头部、手臂或身体触摸区域缺失。");
            var hands = Object.FindObjectsByType<VRHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(2, hands.Length);
            foreach (var hand in hands)
                Assert.IsNotNull(hand.GetComponentInChildren<SphereCollider>(true), "虚拟手缺少触摸边界球。");
        }

        [UnityTest]
        public IEnumerator 实时对话动作库只播放可用动作并接回基础状态()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;
            var actor = Object.FindFirstObjectByType<CharacterActionLibrary>();
            Assert.IsNotNull(actor, "现有人物缺少自然动作库。");
            int layer = actor.animator.GetLayerIndex("自然动作");
            Assert.GreaterOrEqual(layer, 1, "自然动作没有独立动画层。");
            Assert.LessOrEqual(actor.animator.GetLayerWeight(layer), .001f, "待机时动作层不应扭转人物上半身。");
            Assert.IsFalse(actor.TryPerform("不存在的动作"), "未知动作不应碰触人物骨骼。");
            Assert.IsTrue(actor.TryPerform("thinking"), "思考动作没有接入人物。");
            yield return new WaitForSeconds(.45f);
            Assert.IsTrue(actor.animator.GetCurrentAnimatorStateInfo(layer).IsName("Action_thinking"),
                "思考动作没有进入动画状态。");
            Assert.Greater(actor.animator.GetLayerWeight(layer), .9f, "动作开始后上半身动画层没有启用。");
            yield return new WaitForSeconds(5.6f);
            Assert.IsTrue(actor.animator.GetCurrentAnimatorStateInfo(layer).IsName("OverlayEmpty"),
                "动作结束后没有回到基础动画。");
            Assert.LessOrEqual(actor.animator.GetLayerWeight(layer), .001f, "动作结束后上半身动画层没有退出。");
        }
    }
}

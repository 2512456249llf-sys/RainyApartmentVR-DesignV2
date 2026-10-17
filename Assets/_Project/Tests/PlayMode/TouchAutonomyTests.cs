using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RainyApartmentVR.Tests
{
    [DefaultExecutionOrder(15000)] public sealed class SeatedVoicePoseProbe : MonoBehaviour
    {
        public Transform hip, foot, chest;
        public Vector3 hipPosition, footPosition;
        public Quaternion chestRotation;
        private void LateUpdate(){hipPosition=hip.position;footPosition=foot.position;chestRotation=chest.localRotation;}
    }
    public sealed class TouchAutonomyTests
    {
        private static TouchEvent Contact(string part = "Hand") => new TouchEvent { bodyPart = part, contactType = "Stroke", duration = 1.2f, playerHand = "Left", timestamp = "2026-10-01T00:00:00Z" };
        [Test] public void 最新接触合并冷却与过期()
        {
            var queue = new TouchAutonomyQueue();
            queue.Enqueue(Contact(), 0); queue.Enqueue(Contact("Head"), 1);
            Assert.IsNull(queue.Take(1, false)); Assert.AreEqual("Head", queue.Take(1, true).bodyPart);
            queue.Enqueue(Contact("Arm"), 2); Assert.IsNull(queue.Take(3, true));
            Assert.AreEqual("Arm", queue.Take(9, true).bodyPart);
            queue.Enqueue(Contact(), 10); Assert.IsNull(queue.Take(19, true)); Assert.IsFalse(queue.HasPending);
        }
        [TestCase("Hand", "手部")][TestCase("Arm", "手臂")][TestCase("Shoulder", "肩膀")][TestCase("Head", "头部")][TestCase("Torso", "躯干")]
        public void 感知文本说明真实部位和未知压力(string part, string chinese)
        {
            string text = TouchAutonomyPolicy.Describe(Contact(part));
            StringAssert.Contains(chinese, text); StringAssert.Contains("强度未知", text); StringAssert.Contains("不是玩家说的话", text);
        }
        [Test] public void 沉默合法且危险动作拒绝()
        {
            Assert.IsTrue(TouchAutonomyPolicy.IsValid(new TouchReplyPlan { shouldSpeak = false, action = "none" }));
            Assert.IsFalse(TouchAutonomyPolicy.IsValid(new TouchReplyPlan { shouldSpeak = true, utterance = "", action = "nod" }));
            Assert.IsFalse(TouchAutonomyPolicy.IsValid(new TouchReplyPlan { shouldSpeak = false, action = "sit" }));
        }
        [UnityTest] public IEnumerator 模型选择沉默不会播音或追加说话()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var voice = Object.FindFirstObjectByType<VoiceDialogue>(); Assert.IsNotNull(voice);
            var sent = new List<string>(); voice.TransportOverride = value => { sent.Add(value); return Task.CompletedTask; };
            voice.Handle("{\"type\":\"session.updated\"}");
            voice.GetComponent<PerceptionEventHub>().Publish(Contact()); yield return null;
            Assert.IsTrue(voice.IsAutonomousTurn); Assert.AreEqual(1, sent.Count);
            StringAssert.Contains("response.create", sent[0]); StringAssert.Contains("结构化游戏事件", sent[0]);
            StringAssert.DoesNotContain("\"audio\"", sent[0]);
            voice.Handle("{\"type\":\"response.audio.delta\",\"delta\":\"AAAAAA==\"}");
            voice.Handle("{\"type\":\"response.function_call_arguments.done\",\"name\":\"respond_to_touch\",\"call_id\":\"触摸一\",\"arguments\":\"{\\\"shouldSpeak\\\":false,\\\"utterance\\\":\\\"\\\",\\\"action\\\":\\\"none\\\"}\"}");
            voice.Handle("{\"type\":\"response.done\"}"); yield return null;
            Assert.AreEqual(2, sent.Count); StringAssert.Contains("function_call_output", sent[1]); Assert.IsNull(voice.characterAudio.clip);
            yield return new WaitForSeconds(1.1f); Assert.IsFalse(voice.IsAutonomousTurn); Assert.AreEqual(2, sent.Count);
        }
        [UnityTest] public IEnumerator 模型选择说话且重复工具不会重复执行()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var voice = Object.FindFirstObjectByType<VoiceDialogue>();
            var sent = new List<string>(); voice.TransportOverride = value => { sent.Add(value); return Task.CompletedTask; };
            voice.Handle("{\"type\":\"session.updated\"}"); voice.GetComponent<PerceptionEventHub>().Publish(Contact("Shoulder")); yield return null;
            string call = "{\"type\":\"response.function_call_arguments.done\",\"name\":\"respond_to_touch\",\"call_id\":\"触摸二\",\"arguments\":\"{\\\"shouldSpeak\\\":true,\\\"utterance\\\":\\\"我感觉到你在摸我的肩膀。\\\",\\\"action\\\":\\\"nod\\\"}\"}";
            voice.Handle(call); voice.Handle(call); voice.Handle("{\"type\":\"response.done\"}"); yield return null;
            Assert.AreEqual(3, sent.Count); StringAssert.Contains("Shoulder", sent[1]); StringAssert.Contains("\"audio\"", sent[2]);
            voice.Handle("{\"type\":\"response.done\"}"); yield return new WaitForSeconds(1.1f); Assert.IsFalse(voice.IsAutonomousTurn);
        }
        [UnityTest] public IEnumerator 坐稳说话保持髋腿且上身会变化()
        {
            SceneManager.LoadScene("ApartmentDesignV2"); yield return null; yield return null;
            var behaviour = Object.FindFirstObjectByType<CharacterBehaviour>(); behaviour.wanderEnabled = false;
            var performance = behaviour.GetComponent<CharacterVoicePerformance>(); var animator = behaviour.GetComponentInChildren<Animator>();
            behaviour.Sit(); float until = Time.time + 12;
            while (Time.time < until && (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown") || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)) yield return null;
            Assert.IsTrue(behaviour.IsSitting); Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown"));
            var hip = animator.GetBoneTransform(HumanBodyBones.Hips); var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot); var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            var probe=behaviour.gameObject.AddComponent<SeatedVoicePoseProbe>();probe.hip=hip;probe.foot=foot;probe.chest=chest;yield return null;yield return null;
            Vector3 hips = probe.hipPosition, feet = probe.footPosition; Quaternion before = probe.chestRotation;
            behaviour.SetSpeaking(true); performance.SetState(VoiceDialogueState.TalkNormal); yield return new WaitForSeconds(1.3f); yield return null;
            Assert.Less(Vector3.Distance(hips, probe.hipPosition), .01f); Assert.Less(Vector3.Distance(feet, probe.footPosition), .01f);
            Assert.Greater(Quaternion.Angle(before, probe.chestRotation), .1f);
            behaviour.SetSpeaking(false); performance.SetState(VoiceDialogueState.Idle); yield return new WaitForSeconds(1.5f); yield return null;
            Assert.Less(Quaternion.Angle(before, probe.chestRotation), .3f); behaviour.Stand(); yield return new WaitForSeconds(3f); Assert.IsFalse(behaviour.IsSitting);
        }
    }
}

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    // 在所有骨骼驱动执行后取样，不能在动画刚写入、尚未完成转头的半帧取样。
    [DefaultExecutionOrder(10000)]
    public sealed class ConversationHeadPoseProbe : MonoBehaviour
    {
        public Transform head, neck;
        public Quaternion headRotation, neckRotation;
        public Vector3 headPosition;
        private void LateUpdate()
        {
            headRotation = head.rotation;
            neckRotation = neck.rotation;
            headPosition = head.position;
        }
    }

    public sealed class CharacterConversationMotionTests
    {
        [UnityTest]
        public IEnumerator 近处玩家在说话结束后仍获得平滑头颈关注()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var performance = Object.FindFirstObjectByType<CharacterVoicePerformance>();
            var behaviour = performance.GetComponent<CharacterBehaviour>();
            var dialogue = performance.GetComponent<VoiceDialogue>();
            dialogue.enabled = false;
            behaviour.enabled = false;
            performance.facePlayer = false;
            var animator = behaviour.animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            var probe = performance.gameObject.AddComponent<ConversationHeadPoseProbe>();
            probe.head = head;
            probe.neck = neck;
            var target = new GameObject("验收用玩家位置");
            target.transform.position = head.position + performance.transform.forward * 1.4f
                + performance.transform.right * .5f + Vector3.down * .35f;
            typeof(CharacterVoicePerformance).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(performance, target.transform);
            performance.SetState(VoiceDialogueState.TalkNormal);
            behaviour.SetSpeaking(true);
            Assert.IsTrue(performance.GetComponent<CharacterActionLibrary>().TryPerform("nod_yes"));
            yield return new WaitForSeconds(1.5f);
            var axis = (Quaternion)typeof(CharacterVoicePerformance)
                .GetField("headAxisCorrection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(performance);
            Quaternion previousHead = probe.headRotation, previousNeck = probe.neckRotation;
            behaviour.SetSpeaking(false);
            performance.SetState(VoiceDialogueState.Idle);
            float finish = Time.time + 4f;
            float worstAim = 0f, worstHeadSpeed = 0f;
            while (Time.time < finish)
            {
                yield return null;
                float allowance = Mathf.Max(.5f, Time.deltaTime * 130f);
                float headStep = Quaternion.Angle(previousHead, probe.headRotation);
                Assert.Less(headStep, allowance, "说话结束时头部发生突然跳转。");
                Assert.Less(Quaternion.Angle(previousNeck, probe.neckRotation), allowance, "说话结束时颈部发生突然跳转。");
                Vector3 gaze = probe.headRotation * Quaternion.Inverse(axis) * Vector3.forward;
                float aim = Vector3.Angle(gaze, target.transform.position - probe.headPosition);
                Assert.Less(aim, 12f, "退回待机后没有继续用头部看向附近玩家。");
                worstAim = Mathf.Max(worstAim, aim);
                worstHeadSpeed = Mathf.Max(worstHeadSpeed, headStep / Mathf.Max(.001f, Time.deltaTime));
                previousHead = probe.headRotation;
                previousNeck = probe.neckRotation;
            }
            // 冻结身体动画后仍能转头，不出现偏移逐帧累乘。
            animator.speed = 0f;
            performance.proceduralBody = false;
            target.transform.position = head.position + performance.transform.forward * 1.4f
                - performance.transform.right * .6f + Vector3.down * .2f;
            yield return new WaitForSeconds(1.5f);
            Assert.Less(Vector3.Angle(probe.headRotation * Quaternion.Inverse(axis) * Vector3.forward,
                target.transform.position - probe.headPosition), 12f);
            Debug.Log("头颈验收通过：结束说话后最大视线误差=" + worstAim.ToString("F2")
                + "度，最大头部角速度=" + worstHeadSpeed.ToString("F2") + "度/秒。");
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator 动作清单接入且连续动作不截断前一段()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var actions = Object.FindFirstObjectByType<CharacterActionLibrary>();
            actions.GetComponent<VoiceDialogue>().enabled = false;
            var animator = actions.animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int layer = animator.GetLayerIndex("自然动作");
            Assert.GreaterOrEqual(layer, 0);
            var clips = (string[])typeof(CharacterActionLibrary).GetField("ClipIds", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.AreEqual(44, clips.Length);
            foreach (string id in clips)
                Assert.IsTrue(animator.HasState(layer, Animator.StringToHash("自然动作.Action_" + id)), "缺少动作：" + id);
            Assert.AreEqual(61, CharacterActionLibrary.ActionIds.Length); CollectionAssert.Contains(CharacterActionLibrary.ActionIds, "kneel_pray");
            Assert.IsTrue(actions.TryPerform("talk_soft"));
            string first = actions.LastPerformedId;
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(actions.TryPerform("shrug"));
            float deadline = Time.time + 20f;
            float lastFirstTime = 0f;
            bool secondStarted = false;
            while (Time.time < deadline)
            {
                yield return null;
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                if (info.IsName("自然动作.Action_" + first)) lastFirstTime = info.normalizedTime;
                if (info.IsName("自然动作.Action_shrug")) { secondStarted = true; break; }
            }
            Assert.IsTrue(secondStarted, "排队动作未开始。");
            Assert.GreaterOrEqual(lastFirstTime, .98f, "第二段动作截断了第一段。");
            Debug.Log("动作验收通过：44段动作均在控制器中；连续动作顺序播放，前一段完成度=" + lastFirstTime.ToString("F3"));
        }
    }
}


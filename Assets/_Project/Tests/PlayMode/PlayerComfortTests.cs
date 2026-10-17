using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    [DefaultExecutionOrder(12000)]
    public sealed class CrouchPoseProbe : MonoBehaviour
    {
        public Transform hips, leftFoot, rightFoot;
        public Vector3 hipPosition, leftPosition, rightPosition;
        private void LateUpdate()
        {
            hipPosition = hips.position;
            leftPosition = leftFoot.position;
            rightPosition = rightFoot.position;
        }
    }

    public sealed class PlayerComfortTests
    {
        [UnityTest]
        public IEnumerator 玩家掌心对准手柄且深蹲避让上胸并交替走步()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var body = Object.FindFirstObjectByType<VRPlayerBody>(FindObjectsInactive.Include);
            Assert.IsNotNull(body.firstPersonUpperTorso);
            Assert.AreEqual(body.leftTouchHand.transform.parent, body.leftHandTarget, "还在使用旧外观的前伸偏移。");
            var camera = body.headTarget.GetComponent<Camera>();
            var head = new GameObject("测试头显").transform;
            var left = new GameObject("测试左握持点").transform;
            var right = new GameObject("测试右握持点").transform;
            body.headTarget = head;
            body.leftHandTarget = left;
            body.rightHandTarget = right;
            Vector3 origin = body.trackingOrigin.position;
            head.position = origin + Vector3.up * 1.65f;
            left.position = head.position + new Vector3(-.3f, -.4f, .3f);
            right.position = head.position + new Vector3(.3f, -.4f, .3f);
            body.trackingOrigin.gameObject.SetActive(true);
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = listener.transform == camera.transform;
            body.enabled = false;
            body.ApplyTracking(.1f);
            float baseScale = body.transform.localScale.y;
            for (int i = 0; i < 12; i++)
            {
                left.rotation = Quaternion.Euler(i * 5f, i * -9f, i * 4f);
                right.rotation = Quaternion.Euler(i * -5f, i * 7f, i * -4f);
                body.ApplyTracking(.1f);
                Assert.Less(Vector3.Distance(body.LeftPalmPosition, left.TransformPoint(body.gripOffset)), .002f);
                Assert.Less(Vector3.Distance(body.RightPalmPosition, right.TransformPoint(body.gripOffset)), .002f);
                Assert.Greater(Vector3.Distance(body.RightWristPosition, body.RightTrackedGrip), .03f,
                    "错误地将手腕而非掌心贴到了手柄握点。");
            }
            head.position -= Vector3.up * .75f;
            head.rotation = Quaternion.Euler(50f, 0f, 0f);
            for (int i = 0; i < 12; i++) body.ApplyTracking(.1f);
            Assert.AreEqual(baseScale, body.transform.localScale.y, .0001f, "蹲下不能缩小整个人物。");
            Assert.Greater(body.CrouchWeight, .85f);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, body.firstPersonUpperTorso.shadowCastingMode);
            Assert.Less(Vector3.Distance(body.EyePosition, head.position), .002f, "蹲身姿态移动了镜头视点。");
            var knee = body.avatarAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var thigh = body.avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var footL = body.avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var footR = body.avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            Assert.Less(Vector3.Angle(thigh.position - knee.position, footL.position - knee.position), 145f, "蹲身时膝盖没有弯曲。");
            Assert.Greater(footL.position.y, origin.y - .03f);
            head.position += Vector3.up * .75f;
            head.rotation = Quaternion.identity;
            for (int i = 0; i < 12; i++) body.ApplyTracking(.1f);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, body.firstPersonUpperTorso.shadowCastingMode);
            float floor = Mathf.Min(footL.position.y, footR.position.y);
            bool leftRaised = false, rightRaised = false, plantedObserved = false;
            for (int i = 0; i < 100; i++)
            {
                head.position += Vector3.forward * .018f;
                left.position += Vector3.forward * .018f;
                right.position += Vector3.forward * .018f;
                body.ApplyTracking(.02f);
                leftRaised |= footL.position.y > floor + .025f;
                rightRaised |= footR.position.y > floor + .025f;
                plantedObserved |= Mathf.Abs(footL.position.y - footR.position.y) > .04f;
            }
            Assert.IsTrue(leftRaised && rightRaised && plantedObserved, "走路时双脚仍一起平移，没有交替抬脚。");
            Debug.Log("玩家舒适性验收通过：旋转手柄后掌心误差小于2毫米；蹲身不缩小，头显视点保留；双脚有交替步态。");
            Object.Destroy(head.gameObject); Object.Destroy(left.gameObject); Object.Destroy(right.gameObject);
        }

        [UnityTest]
        public IEnumerator 蹲下不是坐下且说话站起走路不冻结()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var actions = Object.FindFirstObjectByType<CharacterActionLibrary>();
            var behaviour = actions.behaviour;
            behaviour.dialogue.enabled = false;
            var animator = actions.animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var probe = behaviour.gameObject.AddComponent<CrouchPoseProbe>();
            probe.hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            probe.leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            probe.rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            yield return new WaitForSeconds(.5f);
            float standingY = probe.hipPosition.y;
            Assert.IsTrue(actions.TryPerform("蹲下"));
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(behaviour.IsCrouching);
            Assert.IsFalse(behaviour.IsSitting);
            Assert.Less(probe.hipPosition.y, standingY - .3f, "蹲身没产生骨盆下降。");
            Assert.AreEqual(1f, animator.speed, "蹲身后冻结了整个动画系统。");
            behaviour.SetSpeaking(true);
            behaviour.performance.SetState(VoiceDialogueState.TalkNormal);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Talk"), "蹲着时说话动作不能运行。");
            float time = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSeconds(.3f);
            Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, time, "蹲身时动画时钟停止。");
            Assert.IsTrue(actions.TryPerform("stand"));
            behaviour.SetSpeaking(false);
            behaviour.performance.SetState(VoiceDialogueState.Idle);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(behaviour.IsPostureLocked);
            Assert.Greater(probe.hipPosition.y, standingY - .12f, "起立后仍卡在蹲姿。");
            Assert.IsTrue(actions.TryPerform("sit"));
            yield return new WaitForSeconds(3f);
            behaviour.SetSpeaking(true);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(1f, animator.speed, "坐下后仍然冻结 Animator。");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown"), "坐着讲话不应被全身站立说话动作拉起。");
            behaviour.SetSpeaking(false);
            behaviour.Stand();
            yield return new WaitForSeconds(3f);
            Vector3 before = behaviour.transform.position;
            behaviour.MoveTo(before + behaviour.transform.forward * 2f);
            yield return new WaitForSeconds(.8f);
            Assert.Greater(Vector3.Distance(before, behaviour.transform.position), .4f, "起立后人物无法恢复行走。");
            Debug.Log("蹲姿与坐姿验收通过：无全局冻结，蹲着能说话，站起后能继续走路。");
        }
    }
}

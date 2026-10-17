using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RainyApartmentVR.Tests
{
    public sealed class PlayerAssetsTests
    {
        [UnityTest]
        public IEnumerator 玩家身体双手和视点随追踪移动且不替换男主()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var body = Object.FindFirstObjectByType<VRPlayerBody>(FindObjectsInactive.Include);
            Assert.IsNotNull(body);
            var rig = body.trackingOrigin.gameObject;
            var camera = body.headTarget.GetComponent<Camera>();
            int hiddenHeadLayer = LayerMask.NameToLayer("玩家自身头部");
            Assert.GreaterOrEqual(hiddenHeadLayer, 8);
            Assert.AreEqual(0, camera.cullingMask & (1 << hiddenHeadLayer), "自己的头部会挡住第一人称镜头。");
            Assert.IsNotNull(body.GetComponentInChildren<SkinnedMeshRenderer>(true));
            Assert.IsNull(body.GetComponent<VoiceDialogue>(), "玩家身体不能代替千问男主。");
            Assert.IsNotNull(Object.FindFirstObjectByType<CharacterBehaviour>());
            var head = new GameObject("验收头显");
            var left = new GameObject("验收左手柄");
            var right = new GameObject("验收右手柄");
            body.headTarget = head.transform;
            body.leftHandTarget = left.transform;
            body.rightHandTarget = right.transform;
            Vector3 origin = rig.transform.position;
            head.transform.position = origin + Vector3.up * 1.65f;
            left.transform.position = head.transform.position + new Vector3(-.28f, -.42f, .35f);
            right.transform.position = head.transform.position + new Vector3(.28f, -.42f, .35f);
            rig.SetActive(true);
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = listener.transform == camera.transform;
            yield return new WaitForSeconds(.4f);
            float worstHandError = 0f;
            var wrist = body.avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            for (int i = 0; i < 4; i++)
            {
                Quaternion beforeWrist = wrist.rotation;
                Quaternion beforeController = right.transform.rotation;
                head.transform.position += new Vector3(.12f, i == 1 ? -.25f : .04f, .10f);
                head.transform.rotation = Quaternion.Euler(i * 8f, i * 18f, 0f);
                left.transform.position = head.transform.position + new Vector3(-.28f - i * .035f, -.38f, .35f);
                right.transform.position = head.transform.position + new Vector3(.27f, -.35f + i * .025f, .3f);
                right.transform.rotation = Quaternion.Euler(i * 12f, i * 16f, i * 9f);
                yield return new WaitForSeconds(.35f);
                float leftError = Vector3.Distance(body.LeftWristPosition, body.LeftTrackedWrist);
                float rightError = Vector3.Distance(body.RightWristPosition, body.RightTrackedWrist);
                Assert.Less(leftError, .008f, "左手没有跟随左手柄。");
                Assert.Less(rightError, .008f, "右手没有跟随右手柄。");
                Assert.Less(Vector3.Distance(body.LeftPalmPosition, body.LeftTrackedGrip), .008f, "掌心没有对齐真实握持点。");
                Assert.Less(Vector3.Distance(body.RightPalmPosition, body.RightTrackedGrip), .008f, "掌心没有对齐真实握持点。");
                Assert.Less(Vector3.Distance(body.EyePosition, head.transform.position), .008f, "身体模型视点没有对齐头显。");
                Assert.Greater(body.avatarAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, origin.y - .02f, "蹲身时左脚沉入地面。");
                Assert.Greater(body.avatarAnimator.GetBoneTransform(HumanBodyBones.RightFoot).position.y, origin.y - .02f, "蹲身时右脚沉入地面。");
                Quaternion expected = right.transform.rotation * Quaternion.Inverse(beforeController) * beforeWrist;
                Assert.Less(Quaternion.Angle(expected, wrist.rotation), 1f, "手腕没有跟随手柄旋转。");
                worstHandError = Mathf.Max(worstHandError, leftError, rightError);
            }
            Debug.Log("玩家身体验收通过：双手位置和旋转、低头与蹲身视点均跟随追踪；最大手腕误差=" + worstHandError.ToString("F5") + "米，男主保留。");
            Object.Destroy(head);
            Object.Destroy(left);
            Object.Destroy(right);
        }

        [UnityTest]
        public IEnumerator 猪沙发能被手部交互器抓起移动并松手悬停()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            var furniture = Object.FindFirstObjectByType<MovableFurniture>();
            Assert.IsNotNull(furniture);
            var grab = furniture.GetComponent<XRGrabInteractable>();
            var body = furniture.GetComponent<Rigidbody>();
            Assert.IsTrue(furniture.HasHandGrab);
            Assert.IsFalse(body.useGravity);
            Assert.IsTrue(body.isKinematic);
            Assert.GreaterOrEqual(grab.colliders.Count, 4);
            var handObject = new GameObject("验收抓取手柄");
            handObject.transform.position = furniture.transform.position + new Vector3(0f, .5f, .45f);
            var trigger = handObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = .1f;
            var hand = handObject.AddComponent<XRDirectInteractor>();
            hand.interactionManager = Object.FindFirstObjectByType<XRInteractionManager>();
            yield return new WaitForSeconds(.2f);
            hand.StartManualInteraction((IXRSelectInteractable)grab);
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(grab.isSelected, "沙发没有进入手部抓取状态。");
            Vector3 start = furniture.transform.position;
            handObject.transform.position += new Vector3(.35f, .3f, .1f);
            yield return new WaitForSeconds(.4f);
            Assert.Greater(Vector3.Distance(start, furniture.transform.position), .2f, "沙发抓住后仍固定在原地。");
            hand.EndManualInteraction();
            yield return new WaitForSeconds(.15f);
            Vector3 released = furniture.transform.position;
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(grab.isSelected);
            Assert.IsTrue(body.isKinematic, "松手后没有保持悬停。");
            Assert.IsFalse(body.useGravity, "松手后仍会下落。");
            Assert.Less(Vector3.Distance(released, furniture.transform.position), .015f, "松手后沙发发生漂移。");
            Assert.Greater(furniture.transform.position.y, -.15f, "沙发穿过了地板。");
            Debug.Log("猪沙发验收通过：手部交互器能抓取和移动，松手保持当前位置和朝向。");
            Object.Destroy(handObject);
        }
    }
}

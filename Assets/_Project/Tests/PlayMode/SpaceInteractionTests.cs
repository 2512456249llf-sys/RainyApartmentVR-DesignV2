using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class SpaceInteractionTests
    {
        [UnityTest]
        public IEnumerator 窗帘与牵手走路验收()
        {
            SceneManager.LoadScene("ApartmentDesignV2");
            yield return null;
            yield return null;

            var curtain = Object.FindFirstObjectByType<InteractiveCurtain>();
            Assert.IsNotNull(curtain);
            Assert.IsNotNull(curtain.grabPoint);
            curtain.SetOpen01(.8f);
            yield return new WaitForSeconds(.55f);
            Assert.Greater(curtain.openAmount, .5f, "窗帘没有响应拉开的目标位置。");

            var hold = Object.FindFirstObjectByType<CharacterHandHold>();
            var behaviour = hold.GetComponent<CharacterBehaviour>();
            Assert.IsNotNull(hold.leftGrabPoint);
            Assert.IsNotNull(hold.rightGrabPoint);
            Assert.IsNotNull(behaviour);
            Vector3 start = behaviour.transform.position;
            Vector3 direction = behaviour.transform.forward;
            Vector3 destination = start + direction * 2f;
            behaviour.BeginHandHold();
            for (int i = 0; i < 10; i++)
            {
                behaviour.UpdateHandHoldTarget(destination, direction * .5f);
                yield return null;
            }
            Assert.IsTrue(behaviour.IsHandHeld);
            Assert.IsTrue(behaviour.IsWalking, "牵手移动时没有进入走路状态。");
            Assert.Greater(behaviour.animator.GetFloat("Speed"), .5f, "走路动画没有收到移动参数。");
            float deadline = Time.time + .55f;
            while (Time.time < deadline)
            {
                behaviour.UpdateHandHoldTarget(destination, direction * .5f);
                yield return null;
            }
            Assert.Greater(Vector3.Distance(start, behaviour.transform.position), .2f, "人物没有跟随牵手目标移动。");
            behaviour.EndHandHold();
            Assert.IsFalse(behaviour.IsHandHeld);
        }
    }
}

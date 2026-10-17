using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class DesktopTraversalTests
    {
        private CharacterController controller;

        [UnitySetUp]
        public IEnumerator 载入公寓并取得玩家()
        {
            SceneManager.LoadScene("Apartment", LoadSceneMode.Single);
            yield return null;

            GameObject player = GameObject.Find("DesktopPlayer");
            Assert.That(player, Is.Not.Null, "场景中缺少 DesktopPlayer。");

            DesktopPlayerController movement = player.GetComponent<DesktopPlayerController>();
            Assert.That(movement, Is.Not.Null);
            movement.enabled = false;

            controller = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);

            GameObject doorObject = GameObject.Find("BathroomDoorInteractable");
            if (doorObject != null)
            {
                doorObject.GetComponent<DoorInteractable>().SetOpenImmediate(true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 玩家能够走遍全部房间并上下楼梯()
        {
            yield return MoveTo(-1.1f, -1.4f, "客厅");

            yield return MoveTo(1.35f, 0f, "厨房门外");
            yield return MoveTo(3.6f, 0f, "厨房入口");
            yield return MoveTo(4f, 1.2f, "厨房");
            yield return MoveTo(3.6f, 0f, "离开厨房");
            yield return MoveTo(1.35f, 0f, "返回客厅");

            yield return MoveTo(-1.1f, -1.4f, "玄关内门");
            yield return MoveTo(-1.1f, -3.1f, "玄关");
            yield return MoveTo(1.35f, -3.1f, "浴室门外");
            yield return MoveTo(3.6f, -3.1f, "浴室");
            yield return MoveTo(1.35f, -3.1f, "离开浴室");

            yield return MoveTo(-4.95f, -3.05f, "楼梯底部");
            yield return MoveTo(-4.95f, 1.55f, "楼梯顶部");
            yield return MoveTo(-4.95f, 2.3f, "二楼平台");
            Assert.That(controller.transform.position.y, Is.GreaterThan(2.75f), "玩家没有成功登上二楼。");

            yield return MoveTo(-1.8f, 2.35f, "卧室一门外");
            yield return MoveTo(-1.8f, 0.6f, "卧室一");
            yield return MoveTo(-1.8f, 2.35f, "离开卧室一");

            yield return MoveTo(3f, 2.35f, "卧室二门外");
            yield return MoveTo(3f, 0.6f, "卧室二");
            yield return MoveTo(3f, 2.35f, "离开卧室二");

            yield return MoveTo(-4.95f, 2.3f, "返回楼梯平台");
            yield return MoveTo(-4.95f, -3.05f, "返回一楼");
            Assert.That(controller.transform.position.y, Is.LessThan(0.5f), "玩家没有正常返回一楼。");
        }

        private IEnumerator MoveTo(float targetX, float targetZ, string location)
        {
            const float distancePerFrame = 0.06f;
            const int maximumFrames = 900;

            for (int frame = 0; frame < maximumFrames; frame++)
            {
                Vector3 position = controller.transform.position;
                Vector3 offset = new Vector3(targetX - position.x, 0f, targetZ - position.z);
                if (offset.magnitude < 0.12f)
                {
                    // 让角色在目标地面上稳定下来，避免无界面测试中的帧时长影响重力。
                    for (int settleFrame = 0; settleFrame < 5; settleFrame++)
                    {
                        controller.Move(Vector3.down * 0.04f);
                        yield return null;
                    }

                    yield break;
                }

                Vector3 movement = offset.normalized * distancePerFrame;
                movement.y = -0.04f;
                controller.Move(movement);
                yield return null;
            }

            Assert.Fail($"玩家在前往{location}时被阻挡，当前位置：{controller.transform.position}。");
        }
    }
}

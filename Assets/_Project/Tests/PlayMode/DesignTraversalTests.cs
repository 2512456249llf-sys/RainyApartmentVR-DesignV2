using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RainyApartmentVR.Tests
{
    public sealed class DesignTraversalTests
    {
        CharacterController controller;
        [UnityTest]
        public IEnumerator 新版真实碰撞漫游与交互验收()
        {
            SceneManager.LoadScene("ApartmentDesignV2");yield return null;yield return null;
            var slab=GameObject.Find("二楼北侧楼板").GetComponent<BoxCollider>();
            Assert.That(slab.size.x,Is.EqualTo(22.4f).Within(.05f),"三个房间的扩建楼板宽度不正确。");
            Assert.That(slab.size.z,Is.EqualTo(9.8f).Within(.05f),"三个房间的扩建楼板深度不正确。");
            Assert.That(Object.FindObjectsByType<InteractiveCurtain>(FindObjectsSortMode.None).Length,Is.GreaterThanOrEqualTo(7),"可拉窗帘缺失。");
            Assert.That(Object.FindFirstObjectByType<CharacterHandHold>(),Is.Not.Null,"人物牵手交互缺失。");
            var player=GameObject.Find("DesktopPlayer");Assert.That(player,Is.Not.Null);
            player.GetComponent<DesktopPlayerController>().enabled=false;controller=player.GetComponent<CharacterController>();
            foreach(var door in Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None))
            {door.SetOpenImmediate(true);Assert.That(door.IsOpen,Is.True);}
            foreach(var light in Object.FindObjectsByType<LightSwitch>(FindObjectsSortMode.None))
            {bool old=light.IsOn;light.Interact();Assert.That(light.IsOn,Is.Not.EqualTo(old));light.Interact();}
            Physics.SyncTransforms();yield return null;
            // 客厅中央没有楼板，向上必须先碰到两层高的顶板。
            Assert.That(Physics.Raycast(new Vector3(-2,1,0),Vector3.up,out RaycastHit ceiling,10),Is.True);
            Assert.That(ceiling.point.y,Is.GreaterThan(6.3f),"客厅仍然被二层楼板封闭。");
            yield return Move(2,-5.45f,"玄关转入客厅");
            yield return Move(1.6f,-.1f,"客厅侧通道");
            yield return Move(3.1f,.7f,"厨房入口");
            yield return Move(4.2f,1.45f,"厨房餐区");
            yield return Move(3.1f,.7f,"离开餐区");
            yield return Move(1.6f,-.1f,"返回客厅");
            yield return Move(2,-5.45f,"返回玄关");
            yield return Move(4.3f,-5.3f,"楼梯底部");
            yield return Move(4.3f,1.35f,"登上二楼");
            Assert.That(controller.transform.position.y,Is.InRange(3.15f,3.6f));
            yield return Move(2.95f,1.4f,"东侧回廊入口");
            yield return Move(2.95f,-5.5f,"东侧回廊南端");
            yield return Move(2.95f,1.4f,"返回二楼走廊");
            yield return Move(-2.1f,1.4f,"主卧门前");
            yield return Move(-2.1f,2.9f,"进入主卧");
            yield return Move(-2.1f,1.4f,"离开主卧");
            yield return Move(0,1.4f,"浴室门前");
            yield return Move(0,3.1f,"进入浴室");
            yield return Move(0,1.4f,"离开浴室");
            yield return Move(5.15f,1.4f,"次卧门前");
            yield return Move(5.15f,2.8f,"进入次卧");
            yield return Move(5.15f,1.4f,"离开次卧");
            yield return Move(4.3f,1.35f,"楼梯上平台");
            yield return Move(4.3f,-5.3f,"返回一楼");
            Assert.That(controller.transform.position.y,Is.InRange(-.1f,.3f));
            // 确认玻璃具有碰撞，不能穿出高层。
            Assert.That(Physics.Raycast(new Vector3(-4,1.6f,0),Vector3.left,out RaycastHit window,3),Is.True);
            Assert.That(window.collider,Is.Not.Null);
        }
        IEnumerator Move(float x,float z,string name)
        {
            for(int i=0;i<700;i++)
            {
                Vector3 delta=new Vector3(x-controller.transform.position.x,0,z-controller.transform.position.z);
                if(delta.magnitude<.11f){for(int s=0;s<8;s++){controller.Move(Vector3.down*.045f);yield return null;}yield break;}
                controller.Move(delta.normalized*.055f+Vector3.down*.045f);yield return null;
            }
            Assert.Fail("路线受阻："+name+"，玩家位置："+controller.transform.position);
        }
    }
}

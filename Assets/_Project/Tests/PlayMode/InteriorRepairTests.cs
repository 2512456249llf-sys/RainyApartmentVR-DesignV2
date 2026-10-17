using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace RainyApartmentVR.Tests {
public class InteriorRepairTests {
[UnityTest] public IEnumerator 跪拜语音动作与起身切换(){
SceneManager.LoadScene("ApartmentDesignV2");yield return null;
var actions=Object.FindFirstObjectByType<CharacterActionLibrary>();var b=actions.GetComponent<CharacterBehaviour>();var a=actions.animator;var prayer=actions.GetComponent<CharacterPrayer>();
var voice=actions.GetComponent<VoiceDialogue>();if(voice)voice.enabled=false;
Assert.IsNotNull(prayer);Assert.IsTrue(a.avatar.isHuman&&a.avatar.isValid);
float standing=a.GetBoneTransform(HumanBodyBones.Hips).position.y;
var probe=actions.gameObject.AddComponent<ContinuationPoseProbe>();probe.animator=a;
Assert.IsTrue(actions.TryPerform("跪下"));Assert.AreEqual("kneel_pray",actions.LastPerformedId);Assert.IsFalse(b.IsSitting);Assert.IsTrue(b.IsPostureLocked);
yield return new WaitForSeconds(3.1f);b.SetSpeaking(true);yield return new WaitForSeconds(.4f);
Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Prayer"),"说话不能把跪姿切成站姿");
float hip=probe.hipHeight;Debug.Log("跪拜骨骼核对 站立髋高="+standing+" 跪拜髋高="+hip+" 左膝="+a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y);
Capture(actions.transform,"跪拜实际姿态");Assert.Less(hip,standing-.25f,"源动作未实际跪下");
Assert.IsTrue(actions.TryPerform("stand"));yield return new WaitForSeconds(.35f);Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("PrayerRise"));
yield return new WaitForSeconds(3.5f);Assert.IsFalse(b.IsPostureLocked);Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Talk"));
Assert.AreEqual(standing,probe.hipHeight,.10f,"起身后髋部未恢复站立高度");
Assert.Greater(probe.leftFootHeight,actions.transform.position.y-.02f,"起身后左脚穿地");
Assert.Greater(probe.rightFootHeight,actions.transform.position.y-.02f,"起身后右脚穿地");
Assert.Greater(probe.riseMinimumFoot,actions.transform.position.y-.04f,"起身过程脚尖穿地");
Debug.Log("最终起身骨骼验收 髋高="+probe.hipHeight+" 左脚="+probe.leftFootHeight+" 右脚="+probe.rightFootHeight+" 起身最低脚点="+probe.riseMinimumFoot);
Capture(actions.transform,"修复后起身实际姿态");
b.SetSpeaking(false);yield return new WaitForSeconds(.5f);Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
Assert.IsTrue(prayer.Begin());yield return new WaitForSeconds(8);
Assert.IsFalse(prayer.IsActive,"原跪拜片段结束应自动起身退出");Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
Assert.AreEqual(standing,probe.hipHeight,.10f,"自动起身后未恢复站姿");
Capture(actions.transform,"自动起身回待机实际姿态");
}
[UnityTest] public IEnumerator 相框材质尺寸桌面与碰撞(){SceneManager.LoadScene("ApartmentDesignV2");yield return null;
var frames=Object.FindObjectsByType<GrabbableProp>(FindObjectsSortMode.None).Where(g=>System.Text.RegularExpressions.Regex.IsMatch(g.name,"^相框_[0-9]{2}$")).OrderBy(g=>g.name).ToArray();Assert.AreEqual(12,frames.Length);
var textures=frames.Select(g=>g.GetComponentInChildren<Renderer>().sharedMaterials.First(m=>m.name.StartsWith("照片_")).mainTexture).ToArray();Assert.AreEqual(12,textures.Distinct().Count());
foreach(var f in frames){var r=f.GetComponentInChildren<Renderer>();Assert.AreEqual(.24f,r.bounds.size.y,.005f);Assert.AreEqual(.75f,r.bounds.min.y,.005f);Assert.IsNotNull(f.GetComponent<Collider>());}
var table=GameObject.Find("电视右侧_长方形展示桌");Assert.IsNotNull(table);Assert.GreaterOrEqual(table.GetComponentsInChildren<Collider>().Length,10);
Physics.SyncTransforms();foreach(var c in table.GetComponentsInChildren<Collider>()){var bb=c.bounds;var overlaps=Physics.OverlapBox(bb.center,bb.extents*.95f,Quaternion.identity).Where(o=>!o.transform.IsChildOf(table.transform)&&!o.name.StartsWith("相框_")).Select(o=>o.name).Distinct().ToArray();Debug.Log("桌体碰撞核对 "+c.name+" 外部重叠="+string.Join("、",overlaps));Assert.IsEmpty(overlaps);}
Assert.AreEqual(.42f,GameObject.Find("洗衣粉").GetComponentInChildren<Renderer>().bounds.size.y,.005f);
}
static void Capture(Transform npc,string name){var go=new GameObject("测试相机");var c=go.AddComponent<Camera>();c.fieldOfView=48;c.nearClipPlane=.02f;go.transform.position=npc.position+npc.forward*2.5f+npc.right*1.5f+Vector3.up*1.4f;go.transform.LookAt(npc.position+Vector3.up*.7f);var rt=new RenderTexture(1000,1000,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();Directory.CreateDirectory("BuildLogs");File.WriteAllBytes("BuildLogs/"+name+".png",tex.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.Destroy(go);Object.Destroy(rt);Object.Destroy(tex);}
}}



using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RainyApartmentVR.Editor {
public static class InteriorFinish {
const string Folder="Assets/_Project/Props/修复材质";
static readonly string[] Photos={"f044f1916090a5ba71d6846e9231bf05","db3ee8ba2441877329926566d02bdaa8","84a12bcdf4783cd6f041fd519fca2e76","247c99668a76d4aa42c6404e5a78686c","42dedd83bebfb9e18491d020fb80c694","52d2bd01c5ccf31c4bded0c0308336bf","e055624de6f4aac214a5c4c49f5e1e7b","73d79fe0258e7ea9909c0ccad1d9f99b","6b63ebf66e3f310fa863249b4d2332b6","7b80a06b8701f72e53ecb89cd3a68d35","591330c821c86a6a2bd281c05a04490f","cfbc5e91bd77269a422aef6cf789abb4"};
static Material Mat(string name,Texture tex,Color color) {
var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.18f);m.SetFloat("_Cull",0);m.DisableKeyword("_EMISSION");EditorUtility.SetDirty(m);return m;
}
static Bounds BoundsOf(GameObject go){var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
static void Part(Transform root,string name,Vector3 p,Vector3 size,Material m,bool cylinder=false){var go=GameObject.CreatePrimitive(cylinder?PrimitiveType.Cylinder:PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;}
public static void Apply(){
Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var scene=EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
var frames=UnityEngine.Object.FindObjectsByType<GrabbableProp>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(g=>System.Text.RegularExpressions.Regex.IsMatch(g.name,"^相框_[0-9]{2}$")).OrderBy(g=>g.name).ToArray();if(frames.Length!=12)throw new Exception("相框数量异常");
var wood=Mat("桌面胡桃木",null,new Color(.32f,.17f,.08f));var metal=Mat("桌腿深灰",null,new Color(.08f,.09f,.10f));
var table=GameObject.Find("电视右侧_长方形展示桌");if(!table){table=new GameObject("电视右侧_长方形展示桌");table.transform.position=new Vector3(.5f,0,4.6f);
// 厚桌面、收边、四根圆腿、围裙和横撑组成实际家具，桌面高度七十五厘米。
Part(table.transform,"胡桃木桌面",new Vector3(0,.725f,0),new Vector3(1.8f,.05f,.85f),wood);
Part(table.transform,"桌面收边",new Vector3(0,.694f,0),new Vector3(1.76f,.016f,.81f),wood);
foreach(float x in new[]{-.79f,.79f})foreach(float z in new[]{-.32f,.32f})Part(table.transform,"圆柱桌腿",new Vector3(x,.34f,z),new Vector3(.06f,.34f,.06f),metal,true);
foreach(float z in new[]{-.32f,.32f})Part(table.transform,"纵向围裙",new Vector3(0,.65f,z),new Vector3(1.58f,.09f,.025f),wood);
foreach(float x in new[]{-.79f,.79f})Part(table.transform,"侧面横撑",new Vector3(x,.26f,0),new Vector3(.025f,.025f,.64f),metal);
}
table.transform.position=new Vector3(.5f,0,4.6f);
for(int i=0;i<12;i++){
var go=frames[i].gameObject;go.transform.rotation=Quaternion.Euler(-90,0,0);var before=BoundsOf(go);float largest=Mathf.Max(before.size.x,Mathf.Max(before.size.y,before.size.z));go.transform.localScale*=.24f/largest;
var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(PropGrabInstaller.FrameFolder+"/"+Photos[i]+".jpg");if(!tex)throw new Exception("缺失照片："+Photos[i]);bool found=false;
foreach(var r in go.GetComponentsInChildren<Renderer>()){var mats=r.sharedMaterials;for(int j=0;j<mats.Length;j++){var old=mats[j];if(old&&(old.name.Contains("Photo")||old.name.StartsWith("照片_"))){mats[j]=Mat("照片_"+(i+1),tex,Color.white);found=true;}else if(old)mats[j]=Mat("相框_"+old.name,null,old.name.Contains("Back")?new Color(.1f,.1f,.1f):new Color(.35f,.21f,.11f));}r.sharedMaterials=mats;}if(!found)throw new Exception(go.name+" 未发现照片材质槽");
var b=BoundsOf(go);go.transform.position+=new Vector3(.5f+(i%6-2.5f)*.28f,.75f,4.6f+(i/6==0?-.23f:.20f))-new Vector3(b.center.x,b.min.y,b.center.z);
Debug.Log("逐框验收 "+go.name+" 原尺寸="+before.size+" 新尺寸="+BoundsOf(go).size+" 照片="+tex.name+" 位置="+go.transform.position);
}
var soap=GameObject.Find("洗衣粉");if(!soap)throw new Exception("洗衣粉缺失");var sb=BoundsOf(soap);soap.transform.localScale*=.42f/sb.size.y;sb=BoundsOf(soap);soap.transform.position+=new Vector3(-1.1f,.56f,5.5f)-new Vector3(sb.center.x,sb.min.y,sb.center.z);
var soapTex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Props/洗衣粉/texture_pbr_20250901.png");if(!soapTex)throw new Exception("洗衣粉彩色贴图缺失");foreach(var r in soap.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>Mat("洗衣粉原色",soapTex,Color.white)).ToArray();
InstallPrayer();foreach(var frame in frames)ContinuationRepair.CorrectPhotoNormals(frame.gameObject);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("修复已保存；地图和现有人物保留");Capture();
}
static void InstallPrayer(){var path="Assets/_Project/Characters/跪拜/跪下膜拜.fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importAnimation=true;var clips=imp.defaultClipAnimations;clips[0].name="跪下膜拜";clips[0].loopTime=false;clips[0].lockRootRotation=true;clips[0].lockRootPositionXZ=true;clips[0].lockRootHeightY=true;clips[0].keepOriginalPositionY=true;imp.clipAnimations=clips;imp.SaveAndReimport();var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));if(!clip.humanMotion)throw new Exception("跪拜不是有效人形动作");
var npc=GameObject.Find(CharacterAnimationSetup.CharacterName);var a=npc.GetComponentInChildren<Animator>();var comp=npc.GetComponent<CharacterPrayer>();if(!comp)comp=npc.AddComponent<CharacterPrayer>();comp.animator=a;
var ctl=(AnimatorController)a.runtimeAnimatorController;if(!ctl.parameters.Any(p=>p.name=="Praying"))ctl.AddParameter("Praying",AnimatorControllerParameterType.Bool);
var sm=ctl.layers[0].stateMachine;var state=sm.states.FirstOrDefault(s=>s.state.name=="Prayer").state;if(!state)state=sm.AddState("Prayer");state.motion=clip;
var rise=sm.states.FirstOrDefault(s=>s.state.name=="PrayerRise").state;if(!rise)rise=sm.AddState("PrayerRise");var reversePath="Assets/_Project/Characters/跪拜/跪拜起身.anim";var reverse=AssetDatabase.LoadAssetAtPath<AnimationClip>(reversePath);if(!reverse){reverse=new AnimationClip();AssetDatabase.CreateAsset(reverse,reversePath);} foreach(var binding in AnimationUtility.GetCurveBindings(clip)){var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys.Reverse().Select(k=>new Keyframe(clip.length-k.time,k.value,-k.outTangent,-k.inTangent)).ToArray();AnimationUtility.SetEditorCurve(reverse,binding,new AnimationCurve(keys));} var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(reverse,settings);EditorUtility.SetDirty(reverse);rise.motion=reverse;rise.speed=1;
// 倒放真实的跪下片段用于起身，避免套用坐下起身。
foreach(var t in sm.anyStateTransitions)if(!t.conditions.Any(c=>c.parameter=="Praying"))t.AddCondition(AnimatorConditionMode.IfNot,0,"Praying");
foreach(var t in rise.transitions.ToArray())rise.RemoveTransition(t);var idle=sm.states.First(s=>s.state.name=="Idle").state;var tr=rise.AddTransition(idle);tr.hasExitTime=true;tr.exitTime=.95f;tr.duration=.25f;
ContinuationRepair.ConfigurePrayer(a);EditorUtility.SetDirty(ctl);Debug.Log("跪拜接入 原始片段="+clip.name+" 长度="+clip.length+" 人形="+clip.humanMotion);
}
public static void Capture(){var go=new GameObject("临时验收相机");var c=go.AddComponent<Camera>();c.fieldOfView=48;c.nearClipPlane=.02f;var rt=new RenderTexture(1600,1000,24);c.targetTexture=rt;go.transform.position=new Vector3(.5f,1.6f,2.8f);go.transform.LookAt(new Vector3(.5f,.91f,4.6f));c.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("BuildLogs/修复后道具.png",tex.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
}
}





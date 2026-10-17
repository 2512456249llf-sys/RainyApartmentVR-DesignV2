using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RainyApartmentVR.Editor {
public static class ContinuationProbe {
 public static void Run(){
 var scene=EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
 foreach(var go in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("相框_")).Select(t=>t.gameObject)){
 foreach(var r in go.GetComponentsInChildren<Renderer>()){
 var mf=r.GetComponent<MeshFilter>();if(!mf)continue;var mesh=mf.sharedMesh;
 for(int i=0;i<mesh.subMeshCount;i++){var tris=mesh.GetTriangles(i);if(tris.Length==0)continue;var center=Vector3.zero;var normal=Vector3.zero;foreach(int index in tris){center+=mesh.vertices[index];normal+=mesh.normals[index];}Debug.Log("相框方向诊断 "+go.name+" 槽="+i+" 材质="+r.sharedMaterials[i].name+" 中心="+center/tris.Length+" 世界法线="+r.transform.TransformDirection(normal.normalized));}
 } }
 var a=GameObject.Find(CharacterAnimationSetup.CharacterName).GetComponentInChildren<Animator>();
 foreach(var path in new[]{"Assets/_Project/Characters/跪拜/跪下膜拜.fbx","Assets/_Project/Characters/跪拜/跪拜起身.anim"})foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))Debug.Log("起身片段诊断 "+clip.name+" 人形="+clip.humanMotion+" 时长="+clip.length+" 曲线="+AnimationUtility.GetCurveBindings(clip).Length);
 }
}}

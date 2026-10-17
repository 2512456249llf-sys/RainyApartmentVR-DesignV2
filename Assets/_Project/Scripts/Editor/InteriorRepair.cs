using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RainyApartmentVR.Editor {
public static class InteriorRepair {
public static void Audit() {
EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("相框_")||t.name=="洗衣粉"||t.name.Contains("电视"))) {
Debug.Log("核查 "+t.name+" 位置="+t.position+" 角度="+t.eulerAngles+" 比例="+t.lossyScale);
foreach(var r in t.GetComponentsInChildren<Renderer>()) {Debug.Log("网格 "+r.name+" 范围="+r.bounds);
foreach(var m in r.sharedMaterials) if(m) Debug.Log("材质 "+m.name+" 着色器="+m.shader.name+" 贴图="+m.mainTexture);
}}
var path="Assets/_Project/Characters/跪拜/跪下膜拜.fbx";
var importer=(ModelImporter)AssetImporter.GetAtPath(path); importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel; importer.importAnimation=true; importer.SaveAndReimport();
foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()) Debug.Log("祈祷片段="+clip.name+" 时长="+clip.length+" 人形="+clip.humanMotion);
}
}}

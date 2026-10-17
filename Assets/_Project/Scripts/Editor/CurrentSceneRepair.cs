using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RainyApartmentVR.Editor
{
    // 只修补当前场景的连接，不重建地图或人物。
    public static class CurrentSceneRepair
    {
        [MenuItem("雨夜公寓/修复当前语音动作和相框")]
        public static void Run()
        {
            CharacterAnimationSetup.UpgradeSpeaking();
            DesignVRHands.InstallIntoScene();
            VoiceDialogueInstaller.InstallIntoScene();
            PhotoFrameInstaller.DiagnoseAndFix();

            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var character = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == CharacterAnimationSetup.CharacterName);
            if (!character) throw new InvalidOperationException("现有人物不见了。");
            var animator = character.GetComponentInChildren<Animator>(true);
            var controller = animator ? animator.runtimeAnimatorController as AnimatorController : null;
            if (!controller || !controller.parameters.Any(p => p.name == "Speaking"))
                throw new InvalidOperationException("说话动作没有接入人物。");
            var behaviour = character.GetComponent<CharacterBehaviour>();
            if (!behaviour || behaviour.wanderEnabled)
                throw new InvalidOperationException("人物移动配置未更新。");
            var frame = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == PhotoFrameInstaller.ObjectName);
            if (!frame || Mathf.Abs(Vector3.Dot(frame.up, Vector3.up)) > .65f)
                throw new InvalidOperationException("相框还没有立起来。");
            var frameMesh = frame.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            var stand = frame.Find("相框背面支架_修复");
            var standMesh = stand ? stand.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (!frameMesh || frameMesh.subMeshCount < 4 || frameMesh.GetTriangles(3).Length != 0 || !standMesh ||
                standMesh.vertices.Any(vertex => vertex.y > -.020f))
                throw new InvalidOperationException("相框支架未完全移到照片背面。");
            Debug.Log("验收通过：原场景人物、说话循环、过来动作和立起的相框均已连接。");
        }
    }
}

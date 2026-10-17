using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    public static class VoiceDialogueInstaller
    {
        [MenuItem("雨夜公寓/给现有人物安装语音对话")]
        public static void InstallIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            InstallInOpenScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("已在现有人物上安装语音对话，地图和模型未重建。");
        }

        public static void InstallInOpenScene()
        {
            var character = EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "男主_雨夜公寓");
            if (!character) throw new InvalidOperationException("没有找到地图里已存在的人物。");
            var vrm = character.GetComponentsInChildren<Component>(true).FirstOrDefault(item => item && item.GetType().FullName == "UniVRM10.Vrm10Instance");
            var animator = character.GetComponentInChildren<Animator>(true);
            if (!vrm || !animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("现有人物的 VRM 表情或人形骨架不可用。");
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!head) throw new InvalidOperationException("现有人物缺少头部骨骼。");

            var voiceObject = head.Find("角色声音");
            if (!voiceObject)
            {
                voiceObject = new GameObject("角色声音").transform;
                voiceObject.SetParent(head, false);
                voiceObject.localPosition = new Vector3(0f, -.08f, .08f);
            }
            var audio = voiceObject.GetComponent<AudioSource>();
            if (!audio) audio = voiceObject.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = .75f;
            audio.maxDistance = 9f;

            var performance = character.GetComponent<CharacterVoicePerformance>();
            if (!performance) performance = character.gameObject.AddComponent<CharacterVoicePerformance>();
            performance.speechAudio = audio;
            var dialogue = character.GetComponent<VoiceDialogue>();
            if (!dialogue) dialogue = character.gameObject.AddComponent<VoiceDialogue>();
            var behaviour = character.GetComponent<CharacterBehaviour>();
            if (!behaviour) behaviour = character.gameObject.AddComponent<CharacterBehaviour>();
            var actions = character.GetComponent<CharacterActionLibrary>();
            if (!actions) actions = character.gameObject.AddComponent<CharacterActionLibrary>();
            if (!character.GetComponent<CharacterTouchSurfaces>()) character.gameObject.AddComponent<CharacterTouchSurfaces>();
            behaviour.performance = performance;
            behaviour.wanderEnabled = false;
            animator.applyRootMotion = false;
            dialogue.characterAudio = audio;
            dialogue.performance = performance;
            dialogue.behaviour = behaviour;
            dialogue.actions = actions;
            actions.animator = animator;
            actions.behaviour = behaviour;
            actions.performance = performance;
            EditorUtility.SetDirty(character.gameObject);
            EditorUtility.SetDirty(dialogue);
        }

        public static void Verify()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var dialogues = Object.FindObjectsByType<VoiceDialogue>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (dialogues.Length != 1) throw new InvalidOperationException("场景中语音对话组件数量异常。");
            var dialogue = dialogues[0];
            if (!dialogue.GetComponent<CharacterInteraction>()) throw new InvalidOperationException("原有拥抱交互丢失。");
            if (!dialogue.GetComponent<CharacterVoicePerformance>() || !dialogue.characterAudio || dialogue.characterAudio.spatialBlend < .99f)
                throw new InvalidOperationException("人物说话动作或空间声音没有安装完整。");
            if (!dialogue.GetComponentsInChildren<Component>(true).Any(item => item && item.GetType().FullName == "UniVRM10.Vrm10Instance"))
                throw new InvalidOperationException("原有人物模型丢失。");
            Debug.Log("语音对话验收通过：沿用现有人物、骨架和表情；声音挂在头部，地图保持原样。");
        }
    }
}

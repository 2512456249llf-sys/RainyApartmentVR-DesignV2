using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainyApartmentVR.Editor
{
    public static class DesignReview
    {
        public static void GenerateAndRender(){DesignApartmentBuilder.Generate();Render();}
        public static void BakeAndRender()
        {
            var scene=EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            var probe=Object.FindFirstObjectByType<ReflectionProbe>();
            if(!probe)throw new System.InvalidOperationException("反射探针缺失。");
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            string path="Assets/_Project/DesignV2/Settings/室内反射.exr";
            if(!Lightmapping.BakeReflectionProbe(probe,path))throw new System.InvalidOperationException("静态反射生成失败。");
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(!probe.customBakedTexture)throw new System.InvalidOperationException("生成的反射资源无法读取。");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Render();
        }
        public static void Render()
        {
            EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            // 编辑状态不会自动播放待机动作；预览图先取原待机片段的一帧，避免误显示张臂姿势。
            var character = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == CharacterAnimationSetup.CharacterName);
            var animator = character ? character.GetComponentInChildren<Animator>(true) : null;
            var idle = AssetDatabase.LoadAllAssetsAtPath(CharacterAnimationSetup.Folder + "/Ch36_nonPBR@Idle.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            bool sampledIdle = animator && idle;
            if (sampledIdle)
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(animator.gameObject, idle, .75f);
            }
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)) c.enabled=false;
            var obj=new GameObject("效果验收相机");var camera=obj.AddComponent<Camera>();
            camera.nearClipPlane=.04f;camera.farClipPlane=200;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.03f,.045f);
            camera.fieldOfView=70;camera.allowHDR=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            foreach(var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)) ps.Simulate(1.2f,true,true);
            Directory.CreateDirectory("BuildLogs");
            try
            {
                Capture(camera,"01_入户看客厅",new Vector3(2.4f,1.65f,-5.3f),new Vector3(-2.4f,2.35f,-.1f));
                Capture(camera,"02_二楼俯瞰挑空",new Vector3(2.6f,4.95f,1.35f),new Vector3(-2.9f,1.35f,-3.25f));
                Capture(camera,"03_客厅看楼梯",new Vector3(-4.65f,1.65f,-5.6f),new Vector3(3.3f,2.5f,.45f));
                Capture(camera,"04_主卧",new Vector3(-2.35f,4.95f,2.65f),new Vector3(-3.7f,4.3f,4.7f));
                // 单独输出模型剖视说明图；隐藏仅影响预览，不写回游戏场景。
                foreach(var item in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if(item.name=="通高顶板"||item.name=="东外墙"||item.name=="入户二楼墙"||item.name=="入口边墙"||item.name=="入户门"||item.name=="窗外城市"||item.name.StartsWith("入户墙")||item.name.StartsWith("南侧")||item.name.StartsWith("主卧入口")||item.name.StartsWith("浴室入口")||item.name.StartsWith("书房入口"))item.gameObject.SetActive(false);
                camera.orthographic=true;camera.orthographicSize=10;camera.backgroundColor=new Color(.014f,.02f,.028f);
                Capture(camera,"05_建筑剖视说明",new Vector3(17,16,-22),new Vector3(0,2.5f,0));
            }
            finally
            {
                Object.DestroyImmediate(obj);
                if (sampledIdle) AnimationMode.StopAnimationMode();
            }
            Debug.Log("四张实际场景预览已生成。");
        }
        static void Capture(Camera camera,string file,Vector3 from,Vector3 target)
        {
            camera.transform.position=from;camera.transform.LookAt(target);
            var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            camera.targetTexture=rt;camera.Render();camera.Render();camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes("BuildLogs/"+file+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        }
        public static void ConnectSecretAnimation()
        {
            const string controllerPath="Assets/_Project/Characters/动画/男主_动作.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)throw new BuildFailedException("男主动作用控制器缺失。");
            var machine=controller.layers[0].stateMachine;
            var idle=FindState(machine,"Idle");
            var walk=FindState(machine,"Walk");
            var talk=FindState(machine,"Talk");
            var secret=FindState(machine,"Secret");
            if(!idle||!walk||!talk||!secret)throw new BuildFailedException("私语动作状态或基础动作状态缺失。");
            if(!controller.parameters.Any(p=>p.name=="SecretPlaying"))
                controller.AddParameter("SecretPlaying",AnimatorControllerParameterType.Bool);

            foreach(var transition in machine.anyStateTransitions)
                if(transition.destinationState==talk&&HasCondition(transition,AnimatorConditionMode.If,"Speaking"))
                    EnsureCondition(transition,AnimatorConditionMode.IfNot,"SecretPlaying",0f);

            EnsureTriggerTransition(machine.anyStateTransitions,()=>machine.AddAnyStateTransition(secret),secret,"Talk");
            EnsureExitTransition(secret,talk,AnimatorConditionMode.If,"Speaking",0f,AnimatorConditionMode.Less,"Speed",.1f);
            EnsureExitTransition(secret,walk,AnimatorConditionMode.Greater,"Speed",.1f);
            EnsureExitTransition(secret,idle,AnimatorConditionMode.IfNot,"Speaking",0f,AnimatorConditionMode.Less,"Speed",.1f);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("私语动画已接入：明确请求后播放，结束后返回说话、待机或行走状态。");
        }

        static AnimatorState FindState(AnimatorStateMachine machine,string name)
        {
            foreach(var child in machine.states)if(child.state.name==name)return child.state;
            return null;
        }
        static bool HasCondition(AnimatorStateTransition transition,AnimatorConditionMode mode,string parameter)
        {
            foreach(var condition in transition.conditions)if(condition.mode==mode&&condition.parameter==parameter)return true;
            return false;
        }
        static void EnsureCondition(AnimatorStateTransition transition,AnimatorConditionMode mode,string parameter,float threshold)
        {
            if(!HasCondition(transition,mode,parameter))transition.AddCondition(mode,threshold,parameter);
        }
        static void EnsureTriggerTransition(AnimatorStateTransition[] transitions,System.Func<AnimatorStateTransition> create,
            AnimatorState destination,string trigger)
        {
            foreach(var transition in transitions)
                if(transition.destinationState==destination&&HasCondition(transition,AnimatorConditionMode.If,trigger))return;
            var added=create();
            added.hasExitTime=false;added.duration=.18f;added.AddCondition(AnimatorConditionMode.If,0f,trigger);
        }
        static void EnsureExitTransition(AnimatorState source,AnimatorState destination,
            AnimatorConditionMode modeA,string parameterA,float thresholdA,
            AnimatorConditionMode modeB=AnimatorConditionMode.If,string parameterB=null,float thresholdB=0f)
        {
            foreach(var transition in source.transitions)
                if(transition.destinationState==destination&&HasCondition(transition,modeA,parameterA)&&
                   (parameterB==null||HasCondition(transition,modeB,parameterB)))return;
            var added=source.AddTransition(destination);
            added.hasExitTime=true;added.exitTime=.9f;added.duration=.18f;
            added.AddCondition(modeA,thresholdA,parameterA);
            if(parameterB!=null)added.AddCondition(modeB,thresholdB,parameterB);
        }
        public static void Build()
        {
            ConnectSecretAnimation();
            var scene=EditorSceneManager.OpenScene(DesignApartmentBuilder.ScenePath);
            foreach(var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)>0) throw new BuildFailedException("丢失脚本："+go.name);
            if(!Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any())throw new BuildFailedException("没有交互门。");
            if(Object.FindObjectsByType<VRHandPose>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=2)throw new BuildFailedException("左右虚拟手没有正确安装。");
            if(Object.FindObjectsByType<CharacterInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=1)throw new BuildFailedException("男主拥抱交互没有正确安装。");
            var character=Object.FindFirstObjectByType<CharacterInteraction>();
            if(!character || character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length<3)throw new BuildFailedException("新 VRM 人物网格没有正确安装。");
            if(character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader)))throw new BuildFailedException("新 VRM 人物材质缺失。");
            var dialogue=character.GetComponent<VoiceDialogue>();
            if(!dialogue || !dialogue.characterAudio || !dialogue.GetComponent<CharacterVoicePerformance>())throw new BuildFailedException("人物语音对话尚未安装完整。");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(DesignApartmentBuilder.ScenePath,true)};
            PlayerSettings.productName="雨夜复式公寓 · 新版";
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            Directory.CreateDirectory("Builds/DesignV2");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{DesignApartmentBuilder.ScenePath},locationPathName="Builds/DesignV2/RainyApartmentV2.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("新版构建失败。");
            Debug.Log("新版构建成功："+report.summary.totalSize+" 字节。");
        }
    }
}

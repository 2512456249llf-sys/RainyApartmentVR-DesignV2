using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace RainyApartmentVR.Editor
{
    public static class DesignApartmentBuilder
    {
        public const string ScenePath="Assets/_Project/Scenes/ApartmentDesignV2.unity";
        const string Art="Assets/_Project/DesignV2";
        // 米制尺寸：挑空位于西南侧，房间位于北侧，楼梯位于东侧。
        const float HalfWidth=5.6f, HalfDepth=6.3f, Storey=3.3f;
        const int StairCount=20;
        const float Tread=.26f, StairStart=-4.5f, StairX=4.3f;
        static Transform root, shell, furnishings, detail, outside;
        static Material plaster,stone,wood,metal,fabric,linen,curtain,glass,ceramic,green,brass,emission,coolEmission,screen;
        static Mesh leafMesh;

        [MenuItem("雨夜公寓/生成新版设计场景")]
        public static void Generate()
        {
            foreach(string folder in new[]{Art,Art+"/Meshes",Art+"/Materials",Art+"/Settings",Art+"/Textures"}) Ensure(folder);
            DesignMesh.ClearCache();
            // 从原场景继承已经配置好的玩家、输入和头显。原场景资源不写回。
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Apartment.unity");
            foreach(GameObject item in scene.GetRootGameObjects())
                if(item.name!="DesktopPlayer" && item.name!="XROrigin_PCVR" && item.name!="PlayerModeManager") Object.DestroyImmediate(item);
            root=Group("雨夜复式公寓_新版",null);shell=Group("建筑与结构",root);
            furnishings=Group("室内家具",root);detail=Group("收边与装饰",root);outside=Group("窗外城市",root);
            SetupMaterials(); BuildShell(); BuildGlazing(); BuildStairs(); BuildLiving(); BuildKitchen();
            BuildBedroom("主卧",new Vector3(-3.4f,Storey,4.25f),2.1f);
            BuildBedroom("次卧书房",new Vector3(3.8f,Storey,4.45f),1.65f);
            BuildBath(); BuildLights(); BuildCity(); BuildRain(); SetupPlayers(); CharacterInstaller.Install(); VoiceDialogueInstaller.InstallInOpenScene();
            Physics.SyncTransforms();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("新版设计场景生成完成：右侧楼梯、二楼三室、西南侧客厅挑空。");
        }
        static void Ensure(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Ensure(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
        static Transform Group(string name,Transform parent)
        {var o=new GameObject(name);o.transform.SetParent(parent,false);return o.transform;}
        static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material mat,Vector3 pos,bool collide=false)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.localPosition=pos;
            o.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=o.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.On;
            if(collide){var c=o.AddComponent<BoxCollider>();c.center=mesh.bounds.center;c.size=mesh.bounds.size;}
            return o;
        }
        static GameObject Solid(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,float radius=.008f,bool collide=true)
            => MeshObject(name,parent,DesignMesh.Rounded(size,radius),mat,pos,collide);
        static GameObject Turned(string name,Transform parent,Vector3 pos,Vector2[] profile,Material mat,bool collide=false)
            => MeshObject(name,parent,DesignMesh.Lathe(name,profile),mat,pos,collide);
        static void Tube(string name,Transform parent,Vector3 a,Vector3 b,float r,Material mat)
        {
            float len=Vector3.Distance(a,b);var mesh=DesignMesh.Lathe($"圆杆_{r:F3}_{len:F3}",new[]{new Vector2(0,0),new Vector2(r,0),new Vector2(r,len),new Vector2(0,len)},12);
            var o=MeshObject(name,parent,mesh,mat,a);o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        }
        static Material Mat(string name,Color color,float smooth,float metallic=0,string texture=null,float repeat=1)
        {
            string path=Art+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            else m.shader=Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);
            if(texture!=null)
            {
                string prefix=Art+"/Textures/"+texture+"/"+texture+"_1K-JPG_";
                m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"Color.jpg"));
                string norm=prefix+"NormalGL.jpg";var importer=AssetImporter.GetAtPath(norm) as TextureImporter;
                if(importer!=null && importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
                m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(norm));m.SetFloat("_BumpScale",texture=="Fabric048"?.22f:.3f);m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BaseMap",Vector2.one*repeat);m.SetTextureScale("_BumpMap",Vector2.one*repeat);
            }
            EditorUtility.SetDirty(m);return m;
        }
        static Material Glow(string name,Color color,float strength)
        {
            Material m=Mat(name,color,.4f);m.SetColor("_EmissionColor",color*strength);m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;return m;
        }
        static void SetupMaterials()
        {
            plaster=Mat("蓝灰细砂墙",new Color(.43f,.46f,.48f),.17f,0,"Concrete034",.6f);
            stone=Mat("细磨石地面",new Color(.47f,.5f,.53f),.54f,0,"Concrete034",.55f);
            wood=Mat("烟熏木饰面",new Color(.4f,.31f,.23f),.38f,0,"WoodFloor051",.7f);
            metal=Mat("黑钛金属",new Color(.055f,.065f,.075f),.53f,.75f);
            fabric=Mat("深蓝编织布",new Color(.25f,.38f,.45f),.12f,0,"Fabric048",14);
            linen=Mat("浅灰床品",new Color(.76f,.78f,.78f),.08f,0,"Fabric048",2);
            curtain=Mat("垂落蓝灰窗帘",new Color(.21f,.27f,.3f),.1f,0,"Fabric048",2);
            curtain.SetFloat("_Cull",0);
            ceramic=Mat("暖白陶瓷",new Color(.76f,.74f,.68f),.65f);
            green=Mat("植物叶面",new Color(.055f,.14f,.065f),.28f);green.SetFloat("_Cull",0);
            brass=Mat("拉丝暖铜",new Color(.46f,.31f,.14f),.6f,.8f);
            emission=Glow("暖色灯带",new Color(1,.65f,.3f),3);
            coolEmission=Glow("远景冷色灯",new Color(.26f,.42f,.55f),.75f);
            screen=Mat("电视黑色玻璃",new Color(.012f,.018f,.024f),.85f,.25f);
            glass=Mat("低反射通透玻璃",new Color(.28f,.5f,.65f,.035f),.35f,0);
            glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);
            glass.shader=Shader.Find("RainyApartment/WindowRain");glass.SetColor("_Tint",new Color(.32f,.53f,.66f,1));
            leafMesh=DesignMesh.Leaf("弧形植物叶片");
        }
        static void BuildShell()
        {
            Solid("一楼地坪",shell,new Vector3(0,-.17f,0),new Vector3(11.6f,.34f,13),stone,.02f);
            // 北侧整块楼板加东侧入户平台，中央与西南均不封顶。
            Solid("二楼北侧楼板",shell,new Vector3(0,Storey-.16f,3.5f),new Vector3(11.2f,.32f,5.6f),stone,.025f);
            Solid("二楼东侧入户平台",shell,new Vector3(4.2f,Storey-.16f,-5.65f),new Vector3(2.8f,.32f,1.3f),stone,.018f);
            Solid("二楼东侧回廊",shell,new Vector3(2.95f,Storey-.16f,-2.8f),new Vector3(1.1f,.32f,7),stone,.018f);
            // 楼梯右侧窄边沿与承重外墙相接，防止楼梯孔穿出外墙。
            Solid("东外墙",shell,new Vector3(5.7f,3.3f,0),new Vector3(.2f,6.6f,12.8f),plaster);
            Solid("北外墙",shell,new Vector3(0,3.3f,6.4f),new Vector3(11.6f,6.6f,.2f),plaster);
            Solid("通高顶板",shell,new Vector3(0,6.68f,0),new Vector3(11.6f,.16f,13),plaster);
            // 南墙入户门洞。
            WallWithDoor("入户墙",shell,-5.6f,5.6f,-6.3f,0,4.3f,1.15f,plaster);
            Solid("入户二楼墙",shell,new Vector3(4.1f,4.95f,-6.3f),new Vector3(3f,3.3f,.18f),plaster);
            // 南侧大窗区域由玻璃替代，移除对应实体墙段。
            Object.DestroyImmediate(shell.Find("入户墙_左段").gameObject);
            Solid("入口边墙",shell,new Vector3(2.78f,1.65f,-6.3f),new Vector3(1.82f,3.3f,.18f),plaster);
            Solid("主卧隔墙",shell,new Vector3(-1.15f,4.95f,4.2f),new Vector3(.15f,3.3f,4.2f),plaster);
            Solid("书房隔墙",shell,new Vector3(1.4f,4.95f,4.2f),new Vector3(.15f,3.3f,4.2f),plaster);
            WallWithDoor("主卧入口",shell,-5.6f,-1.15f,2.1f,Storey,-2.1f,1.05f,plaster);
            WallWithDoor("浴室入口",shell,-1.15f,1.4f,2.1f,Storey,0,1.0f,plaster);
            WallWithDoor("书房入口",shell,1.4f,5.6f,2.1f,Storey,3.0f,1.05f,plaster);
            Door("主卧门",-2.1f,Storey,2.1f,1.02f);
            Door("浴室门",0,Storey,2.1f,.97f);
            Door("书房门",3f,Storey,2.1f,1.02f);
            Door("入户门",4.3f,0,-6.3f,1.12f);
            // 木饰面、收边及楼板前缘形成连续的建筑层次。
            Solid("二楼楼板黑色收边",detail,new Vector3(-1.1f,3.16f,.53f),new Vector3(9f,.2f,.055f),metal,.008f,false);
            Solid("客厅电视背板",detail,new Vector3(-2.8f,1.4f,5.97f),new Vector3(4.7f,2.8f,.14f),wood,.02f);
            for(int i=0;i<21;i++) Solid("背板竖向格栅",detail,new Vector3(-5.05f+i*.055f,1.4f,5.84f),new Vector3(.028f,2.8f,.045f),wood,.005f,false);
            for(int floor=0;floor<2;floor++)
            {
                float y=floor*Storey+.045f;
                Solid("北墙踢脚线",detail,new Vector3(0,y,6.24f),new Vector3(11.1f,.09f,.025f),metal,.003f,false);
                Solid("东墙踢脚线",detail,new Vector3(5.57f,y,0),new Vector3(.025f,.09f,12.5f),metal,.003f,false);
            }
            Rail(new Vector3(-5.5f,Storey,.7f),new Vector3(2.4f,Storey,.7f));
            Rail(new Vector3(2.4f,Storey,-6.2f),new Vector3(2.4f,Storey,.7f));
            Rail(new Vector3(3.5f,Storey,-4.95f),new Vector3(3.5f,Storey,.7f));
            foreach(float edge in new[]{2.4f,3.5f})
            {
                float length=edge<3?6.9f:5.66f;
                var guard=new GameObject("回廊防跌落碰撞");guard.transform.SetParent(shell);guard.transform.position=new Vector3(edge,Storey+.6f,edge<3?-2.75f:-2.13f);
                guard.AddComponent<BoxCollider>().size=new Vector3(.06f,1.2f,length);
            }
            // 挑空边缘的可见横栏杆另配隐形整体防跌落碰撞。
            var barrier=new GameObject("挑空防跌落碰撞");barrier.transform.SetParent(shell);barrier.transform.position=new Vector3(-1.55f,Storey+.6f,.72f);
            barrier.AddComponent<BoxCollider>().size=new Vector3(7.9f,1.2f,.08f);
        }
        static void WallWithDoor(string name,Transform parent,float min,float max,float z,float y,float door,float width,Material mat)
        {
            float left=door-width/2,right=door+width/2;
            if(left>min)Solid(name+"_左段",parent,new Vector3((min+left)/2,y+Storey/2,z),new Vector3(left-min,Storey,.18f),mat);
            if(right<max)Solid(name+"_右段",parent,new Vector3((right+max)/2,y+Storey/2,z),new Vector3(max-right,Storey,.18f),mat);
            Solid(name+"_门楣",parent,new Vector3(door,y+2.65f,z),new Vector3(width,1.3f,.18f),mat);
        }
        static void Door(string name,float x,float y,float z,float width)
        {
            Transform hinge=Group(name,root);hinge.position=new Vector3(x-width/2,y,z);
            Solid("胡桃木门扇",hinge,new Vector3(width/2,1,.01f),new Vector3(width,2,.055f),wood,.008f);
            Solid("门把手底座",hinge,new Vector3(width-.1f,1,-.037f),new Vector3(.04f,.16f,.025f),brass,.008f,false);
            Tube("执手",hinge,new Vector3(width-.1f,1,-.09f),new Vector3(width-.25f,1,-.09f),.015f,brass);
            var box=hinge.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(width/2,1,0);box.size=new Vector3(width,2,.07f);
            // 入户门为地图边界；三个房间门可交互并向室内开启。
            if(name!="入户门")
            {
                var door=hinge.gameObject.AddComponent<DoorInteractable>();
                var doorSettings=new SerializedObject(door);doorSettings.FindProperty("openAngle").floatValue=-90;doorSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            for(int s=-1;s<=1;s+=2) Solid(name+"门框",detail,new Vector3(x+s*(width/2+.032f),y+1.025f,z),new Vector3(.055f,2.05f,.21f),metal,.005f);
            Solid(name+"门框顶",detail,new Vector3(x,y+2.04f,z),new Vector3(width+.1f,.055f,.21f),metal,.005f);
        }
        static void BuildGlazing()
        {
            Transform windows=Group("通高落地窗",shell);
            for(int i=0;i<9;i++)
            {
                float z=-6.3f+i*1.575f;
                Solid("西侧竖框",windows,new Vector3(-5.57f,3.3f,z),new Vector3(.085f,6.6f,.085f),metal,.008f);
                if(i<8)for(int floor=0;floor<2;floor++)
                    Solid("西侧窗玻璃",windows,new Vector3(-5.61f,1.63f+floor*Storey,z+.7875f),new Vector3(.025f,3.17f,1.49f),glass,.001f);
            }
            for(int level=0;level<3;level++)
            {
                Solid("西侧横框",windows,new Vector3(-5.55f,.04f+level*Storey,0),new Vector3(.11f,.09f,12.6f),metal,.008f);
                Solid("南侧横框",windows,new Vector3(-1.9f,.04f+level*Storey,-6.3f),new Vector3(7.4f,.09f,.11f),metal,.008f);
            }
            for(int i=0;i<6;i++)
            {
                float x=-5.6f+i*1.48f;
                Solid("南侧竖框",windows,new Vector3(x,3.3f,-6.28f),new Vector3(.085f,6.6f,.085f),metal,.008f);
                if(i<5)for(int floor=0;floor<2;floor++)Solid("南侧窗玻璃",windows,new Vector3(x+.74f,1.63f+floor*Storey,-6.32f),new Vector3(1.4f,3.17f,.025f),glass,.001f);
            }
            foreach(float z in new[]{-5.6f,.05f,5.7f})
            {
                var drape=MeshObject("通高褶皱窗帘",detail,DesignMesh.Curtain("通高窗帘",1.02f,6.35f),curtain,new Vector3(-5.34f,.03f,z));drape.transform.localEulerAngles=new Vector3(0,90,0);
            }
            Tube("窗帘轨道",detail,new Vector3(-5.3f,6.43f,-6.1f),new Vector3(-5.3f,6.43f,6.1f),.018f,metal);
        }
        static void Rail(Vector3 a,Vector3 b)
        {
            int posts=Mathf.CeilToInt(Vector3.Distance(a,b)/1.15f);
            for(int i=0;i<=posts;i++)
            {Vector3 p=Vector3.Lerp(a,b,(float)i/posts);Tube("栏杆立柱",detail,p,p+Vector3.up*1.05f,.023f,metal);}
            Tube("圆角扶手",detail,a+Vector3.up*1.05f,b+Vector3.up*1.05f,.032f,metal);
            for(int i=1;i<5;i++)Tube("水平钢索",detail,a+Vector3.up*(i*.2f),b+Vector3.up*(i*.2f),.006f,metal);
        }
        static void BuildStairs()
        {
            Transform stairs=Group("开放式楼梯",shell);
            for(int i=0;i<StairCount;i++)
            {
                float h=(i+1)*Storey/StairCount,z=StairStart+(i+.5f)*Tread;
                Solid("实木踏板_"+i,stairs,new Vector3(StairX,h-.035f,z),new Vector3(1.5f,.07f,Tread+.04f),wood,.012f);
                Solid("踏步底部灯带",stairs,new Vector3(StairX,h-.08f,z-Tread*.44f),new Vector3(1.36f,.018f,.024f),emission,.004f,false);
            }
            Vector3 a=new Vector3(StairX,0,StairStart),b=new Vector3(StairX,Storey,.7f);
            foreach(float side in new[]{-.66f,.66f})
            {
                Vector3 start=a+new Vector3(side,-.16f,0),end=b+new Vector3(side,-.16f,0);
                var beam=Solid("斜向金属梯梁",stairs,(start+end)/2,new Vector3(.07f,.19f,Vector3.Distance(start,end)),metal,.014f,false);
                beam.transform.rotation=Quaternion.LookRotation(end-start);
                Rail(a+Vector3.right*side,b+Vector3.right*side);
            }
            // 与可见踏步吻合的薄连续坡面，让两种控制器都能稳定上楼。
            var ramp=new GameObject("楼梯连续行走碰撞");ramp.transform.SetParent(stairs,false);
            var vertices=new List<Vector3>{new Vector3(3.55f,.02f,-4.6f),new Vector3(5.05f,.02f,-4.6f),new Vector3(3.55f,3.3f,.7f),new Vector3(5.05f,3.3f,.7f)};
            var mesh=DesignMesh.Save("楼梯行走坡面",vertices,new List<int>{0,2,1,1,2,3},new List<Vector2>{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});
            ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static void Sofa(string name,Vector3 pos,float yaw,float length)
        {
            Transform g=Group(name,furnishings);g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);
            Solid("悬空底座",g,new Vector3(0,.22f,0),new Vector3(length,.2f,.84f),metal,.06f);
            Solid("软包靠背",g,new Vector3(0,.73f,.36f),new Vector3(length,.78f,.24f),fabric,.09f);
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*(length-.35f)/3;
                Solid("圆角座垫",g,new Vector3(x,.47f,-.035f),new Vector3((length-.4f)/3-.015f,.24f,.77f),fabric,.095f);
                var cushion=Solid("柔软背垫",g,new Vector3(x,.81f,.19f),new Vector3((length-.4f)/3-.03f,.48f,.2f),fabric,.087f,false);cushion.transform.localEulerAngles=new Vector3(-9,0,0);
            }
            foreach(int s in new[]{-1,1})Solid("圆角扶手",g,new Vector3(s*(length/2-.095f),.59f,0),new Vector3(.21f,.5f,1),fabric,.085f);
            var pillow=Solid("亚麻抱枕",g,new Vector3(-length*.28f,.82f,.04f),new Vector3(.43f,.43f,.18f),linen,.082f,false);pillow.transform.localEulerAngles=new Vector3(-12,0,-15);
        }
        static void BuildLiving()
        {
            Sofa("窗边三人沙发",new Vector3(-4.18f,0,-2.1f),-90,3.2f);
            Sofa("对坐三人沙发",new Vector3(.2f,0,-2.1f),90,3.2f);
            Sofa("南侧双人沙发",new Vector3(-2,0,-4.48f),180,2.7f);
            Solid("织物地毯",furnishings,new Vector3(-2,.018f,-2.25f),new Vector3(4.7f,.018f,4.65f),linen,.007f,false);
            Transform table=Group("椭圆岩板茶几",furnishings);table.position=new Vector3(-2,.0f,-2.15f);
            var top=Turned("椭圆石材台面",table,new Vector3(0,.41f,0),new[]{new Vector2(0,0),new Vector2(.67f,0),new Vector2(.7f,.02f),new Vector2(.7f,.065f),new Vector2(.67f,.085f),new Vector2(0,.085f)},stone,true);top.transform.localScale=new Vector3(1,1,1.75f);
            foreach(int s in new[]{-1,1})Solid("茶几金属脚",table,new Vector3(0,.23f,s*.5f),new Vector3(.55f,.4f,.055f),metal,.014f);
            Books(table,new Vector3(-.17f,.51f,.25f));Vase(table,new Vector3(.1f,.5f,-.45f),.65f);
            Solid("悬浮电视柜",furnishings,new Vector3(-2.8f,.38f,5.52f),new Vector3(3.9f,.36f,.56f),wood,.025f);
            Solid("超薄电视边框",furnishings,new Vector3(-2.8f,1.55f,5.83f),new Vector3(2.45f,1.39f,.06f),metal,.026f);
            Solid("电视屏幕",furnishings,new Vector3(-2.8f,1.55f,5.785f),new Vector3(2.37f,1.32f,.012f),screen,.012f,false);
            Plant(new Vector3(-4.8f,0,4.2f),1.45f);Plant(new Vector3(1.8f,0,-5.6f),1.1f);
            Lamp(new Vector3(-4.7f,0,-4.85f),1.55f);Lamp(new Vector3(.2f,0,.05f),1.45f);
            // 一层侧墙以细木条完成，而非单块占位墙。
            for(int i=0;i<22;i++)Solid("厨房边缘竖木饰条",detail,new Vector3(1.85f,1.1f,3.7f+i*.085f),new Vector3(.04f,2.2f,.042f),wood,.006f,false);
        }
        static void Books(Transform parent,Vector3 p)
        {for(int i=0;i<3;i++){var b=Solid("叠放书籍",parent,p+Vector3.up*i*.035f,new Vector3(.3f,.034f,.23f),i%2==0?wood:linen,.004f,false);b.transform.localEulerAngles=new Vector3(0,i*7,0);}}
        static void Vase(Transform parent,Vector3 p,float scale)
        {
            var vase=Turned("陶瓷花瓶",parent,p,new[]{new Vector2(0,0),new Vector2(.1f,0),new Vector2(.15f,.12f),new Vector2(.12f,.27f),new Vector2(.05f,.35f),new Vector2(.055f,.39f),new Vector2(.04f,.39f),new Vector2(.035f,.34f)},ceramic);
            vase.transform.localScale=Vector3.one*scale;
            for(int i=0;i<3;i++){Vector3 end=p+new Vector3((i-1)*.1f,.8f*scale,0);Tube("花枝",parent,p+Vector3.up*.2f,end,.005f,wood);var l=MeshObject("细叶",parent,leafMesh,green,end);l.transform.localScale=Vector3.one*.35f;l.transform.localEulerAngles=new Vector3(20,i*120,20);}
        }
        static void Plant(Vector3 pos,float scale)
        {
            Transform g=Group("室内观叶植物",furnishings);g.position=pos;g.localScale=Vector3.one*scale;
            Turned("水泥花盆",g,Vector3.zero,new[]{new Vector2(0,0),new Vector2(.18f,0),new Vector2(.25f,.4f),new Vector2(.25f,.45f),new Vector2(.22f,.45f),new Vector2(.2f,.08f),new Vector2(0,.08f)},stone);
            for(int i=0;i<13;i++)
            {float angle=i*137.5f;Vector3 end=Quaternion.Euler(0,angle,0)*new Vector3(.18f,.5f+(i%5)*.13f,.1f);Tube("叶柄",g,new Vector3(0,.3f,0),end,.006f,green);var leaf=MeshObject("弧形阔叶",g,leafMesh,green,end);leaf.transform.localEulerAngles=new Vector3(-15+i%3*12,angle,0);}
        }
        static void Lamp(Vector3 pos,float height)
        {
            Transform g=Group("暖光落地灯",furnishings);g.position=pos;
            Turned("圆形灯座",g,Vector3.zero,new[]{new Vector2(0,0),new Vector2(.2f,.01f),new Vector2(.22f,.035f),new Vector2(.2f,.065f),new Vector2(0,.065f)},metal);
            Tube("灯杆",g,Vector3.up*.05f,Vector3.up*height,.014f,brass);
            Turned("织物灯罩",g,Vector3.up*(height-.15f),new[]{new Vector2(.28f,0),new Vector2(.2f,.3f),new Vector2(.19f,.3f),new Vector2(.27f,0)},linen);
            Turned("灯罩内光源",g,Vector3.up*(height-.14f),new[]{new Vector2(0,0),new Vector2(.2f,0),new Vector2(.2f,.025f),new Vector2(0,.025f)},emission);
            Point("落地灯光",pos+Vector3.up*(height-.1f),new Color(1,.66f,.36f),1.0f,3.2f,false);
        }
        static void BuildKitchen()
        {
            Transform g=Group("开放厨房与餐区",furnishings);
            // 连续台面与分门缝柜体。
            Solid("东侧橱柜",g,new Vector3(5.17f,.45f,4.15f),new Vector3(.72f,.9f,3.7f),wood,.015f);
            Solid("石材工作台",g,new Vector3(5.12f,.93f,4.15f),new Vector3(.85f,.075f,3.75f),stone,.018f);
            for(int i=0;i<6;i++){Solid("柜门分缝",g,new Vector3(4.795f,.46f,2.62f+i*.61f),new Vector3(.025f,.85f,.015f),metal,.002f,false);Tube("橱柜拉手",g,new Vector3(4.77f,.77f,2.72f+i*.61f),new Vector3(4.77f,.77f,3.03f+i*.61f),.008f,brass);}
            Solid("后侧橱柜",g,new Vector3(3.2f,.45f,5.88f),new Vector3(3.2f,.9f,.67f),wood,.015f);
            Solid("后侧台面",g,new Vector3(3.2f,.93f,5.86f),new Vector3(3.2f,.075f,.76f),stone,.012f);
            Solid("吊柜",g,new Vector3(4.15f,2.25f,6.01f),new Vector3(2.7f,.85f,.43f),wood,.012f);
            Solid("台面灯带",g,new Vector3(4.15f,1.8f,5.86f),new Vector3(2.65f,.02f,.08f),emission,.004f,false);
            Solid("冰箱",g,new Vector3(2.15f,1.03f,5.86f),new Vector3(.88f,2.06f,.75f),metal,.04f);
            Solid("冰箱门分缝",g,new Vector3(2.15f,.78f,5.47f),new Vector3(.83f,.012f,.012f),screen,.003f,false);
            Tube("冰箱把手",g,new Vector3(2.49f,1.03f,5.43f),new Vector3(2.49f,1.72f,5.43f),.012f,brass);
            Solid("嵌入水槽",g,new Vector3(5.1f,.98f,4.5f),new Vector3(.55f,.05f,.73f),metal,.11f,false);
            Tube("龙头立柱",g,new Vector3(5.4f,.98f,4.5f),new Vector3(5.4f,1.32f,4.5f),.017f,metal);
            Tube("龙头出水口",g,new Vector3(5.4f,1.32f,4.5f),new Vector3(5.13f,1.32f,4.5f),.017f,metal);
            Solid("黑玻璃灶台",g,new Vector3(5.1f,.98f,3.1f),new Vector3(.57f,.025f,.75f),screen,.025f,false);
            for(int i=0;i<2;i++)Turned("灶环"+i,g,new Vector3(5.1f,1,2.92f+i*.34f),new[]{new Vector2(.11f,0),new Vector2(.115f,.008f),new Vector2(.11f,.012f)},metal);
            Solid("餐桌圆角台面",g,new Vector3(3.25f,.79f,3f),new Vector3(1.45f,.085f,1.4f),wood,.04f);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Tube("餐桌脚",g,new Vector3(3.25f+x*.56f,.04f,3+z*.5f),new Vector3(3.25f+x*.48f,.76f,3+z*.43f),.032f,metal);
            Chair(new Vector3(3.25f,0,1.92f),180);Chair(new Vector3(3.25f,0,4.1f),0);
            Vase(g,new Vector3(3.25f,.84f,3),.75f);
            Tube("餐吊灯线",g,new Vector3(3.25f,3.08f,3),new Vector3(3.25f,2.18f,3),.005f,metal);
            Turned("铜色吊灯",g,new Vector3(3.25f,2.02f,3),new[]{new Vector2(.38f,0),new Vector2(.37f,.035f),new Vector2(.24f,.19f),new Vector2(.04f,.26f)},brass);
            Point("餐区暖光",new Vector3(3.25f,1.99f,3),new Color(1,.7f,.45f),1.6f,4,false);
        }
        static void Chair(Vector3 p,float yaw)
        {
            Transform g=Group("曲面餐椅",furnishings);g.position=p;g.localEulerAngles=new Vector3(0,yaw,0);
            Solid("皮质椅垫",g,new Vector3(0,.49f,0),new Vector3(.52f,.095f,.51f),fabric,.044f);
            var back=Solid("包边椅背",g,new Vector3(0,.8f,.22f),new Vector3(.52f,.55f,.09f),fabric,.044f);back.transform.localEulerAngles=new Vector3(-8,0,0);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Tube("椅腿",g,new Vector3(x*.24f,.01f,z*.23f),new Vector3(x*.2f,.48f,z*.18f),.015f,metal);
        }
        static void BuildBedroom(string name,Vector3 p,float width)
        {
            Transform g=Group(name,furnishings);g.position=p;
            Solid("木质床架",g,new Vector3(0,.22f,0),new Vector3(width+.12f,.3f,2.32f),wood,.04f);
            Solid("厚床垫",g,new Vector3(0,.47f,0),new Vector3(width,.28f,2.2f),linen,.105f);
            Solid("床头软包",g,new Vector3(0,.72f,1.15f),new Vector3(width+.18f,1.28f,.12f),fabric,.05f);
            Solid("蓝灰绗缝被",g,new Vector3(0,.65f,-.28f),new Vector3(width+.07f,.1f,1.55f),fabric,.045f,false);
            for(int i=0;i<8;i++)Solid("被面细褶",g,new Vector3(-width/2+.1f+i*(width-.2f)/7,.709f,-.3f),new Vector3(.018f,.012f,1.4f),fabric,.005f,false);
            for(int s=-1;s<=1;s+=2)
            {
                var pillow=Solid("卧室枕头",g,new Vector3(s*width*.25f,.7f,.72f),new Vector3(width*.43f,.19f,.44f),linen,.085f,false);pillow.transform.localEulerAngles=new Vector3(7,0,s*3);
                Solid("床头柜",g,new Vector3(s*(width*.5f+.36f),.32f,.86f),new Vector3(.52f,.53f,.53f),wood,.025f);
                Tube("床头暖色灯线",g,new Vector3(s*(width*.5f+.36f),.67f,.86f),new Vector3(s*(width*.5f+.36f),.95f,.86f),.018f,brass);
                Turned("床头灯罩",g,new Vector3(s*(width*.5f+.36f),.93f,.86f),new[]{new Vector2(.16f,0),new Vector2(.12f,.2f)},linen);
            }
            Vector3 ward=name=="主卧"?new Vector3(-1.64f,Storey+1.2f,4.7f):new Vector3(1.93f,Storey+1.2f,4.7f);
            Solid(name+"衣柜",furnishings,ward,new Vector3(.72f,2.4f,2.5f),wood,.02f);
            for(int i=0;i<20;i++)Solid("衣柜细格栅",detail,ward+new Vector3(name=="主卧"?-.38f:.38f,0,-1.18f+i*.12f),new Vector3(.023f,2.34f,.032f),wood,.004f,false);
            if(name!="主卧")
            {
                Solid("书桌",furnishings,new Vector3(4.6f,Storey+.76f,2.62f),new Vector3(1.6f,.09f,.66f),wood,.03f);
                foreach(int s in new[]{-1,1})Solid("书桌侧脚",furnishings,new Vector3(4.6f+s*.65f,Storey+.38f,2.62f),new Vector3(.04f,.72f,.58f),metal,.009f);
                Chair(new Vector3(4.9f,Storey,3.3f),0);Books(furnishings,new Vector3(4.1f,Storey+.83f,2.62f));
            }
            Point(name+"暖光",p+new Vector3(0,1.75f,.85f),new Color(1,.72f,.48f),1.6f,4,false);
        }
        static void BuildBath()
        {
            Transform g=Group("二楼卫生间",furnishings);
            Solid("浴室地砖",g,new Vector3(.1f,Storey+.025f,4.2f),new Vector3(2.36f,.04f,4.1f),stone,.004f);
            Solid("浴室洗手台柜",g,new Vector3(-.67f,Storey+.42f,3.02f),new Vector3(.7f,.84f,1.05f),wood,.025f);
            var basin=Turned("台上盆",g,new Vector3(-.67f,Storey+.85f,3.02f),new[]{new Vector2(0,0),new Vector2(.25f,.01f),new Vector2(.3f,.13f),new Vector2(.29f,.17f),new Vector2(.25f,.17f),new Vector2(.22f,.055f),new Vector2(0,.04f)},ceramic);basin.transform.localScale=new Vector3(1,1,1.35f);
            Solid("浴室镜框",g,new Vector3(-1.03f,Storey+1.52f,3.02f),new Vector3(.035f,1.15f,.9f),brass,.012f);
            Solid("浴室镜面",g,new Vector3(-1.005f,Storey+1.52f,3.02f),new Vector3(.012f,1.07f,.82f),screen,.005f,false);
            Turned("坐便器底座",g,new Vector3(-.48f,Storey,4.42f),new[]{new Vector2(0,0),new Vector2(.19f,0),new Vector2(.15f,.27f),new Vector2(.3f,.35f),new Vector2(.31f,.43f),new Vector2(.26f,.47f),new Vector2(0,.47f)},ceramic,true);
            Solid("坐便器水箱",g,new Vector3(-.48f,Storey+.62f,4.77f),new Vector3(.58f,.56f,.22f),ceramic,.075f);
            Solid("淋浴底盘",g,new Vector3(.1f,Storey+.06f,5.62f),new Vector3(2.1f,.1f,1.12f),stone,.035f);
            Solid("淋浴玻璃隔断",g,new Vector3(.15f,Storey+1.08f,5.02f),new Vector3(1.4f,2.15f,.012f),glass,.002f);
            Tube("淋浴立管",g,new Vector3(.65f,Storey+.8f,6.1f),new Vector3(.65f,Storey+2.15f,6.1f),.014f,metal);
            Tube("淋浴横管",g,new Vector3(.65f,Storey+2.15f,6.1f),new Vector3(.65f,Storey+2.15f,5.75f),.014f,metal);
            Turned("顶喷",g,new Vector3(.65f,Storey+2.1f,5.75f),new[]{new Vector2(0,0),new Vector2(.13f,0),new Vector2(.13f,.035f),new Vector2(0,.035f)},metal);
            Point("浴室灯光",new Vector3(.1f,5.95f,4.2f),new Color(.77f,.85f,1),1.7f,4,false);
        }
        static Light Point(string name,Vector3 pos,Color color,float intensity,float range,bool shadow)
        {
            var o=new GameObject(name);o.transform.SetParent(root);o.transform.position=pos;
            var l=o.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity*5;l.range=range;l.shadows=shadow?LightShadows.Soft:LightShadows.None;return l;
        }
        static void BuildLights()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.34f,.43f,.54f);RenderSettings.ambientEquatorColor=new Color(.19f,.25f,.33f);RenderSettings.ambientGroundColor=new Color(.09f,.12f,.16f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.024f,.053f,.078f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.007f;
            var moon=Point("窗外冷色月光",new Vector3(-8,9,-4),new Color(.55f,.75f,1),1.05f,25,true);moon.type=LightType.Directional;moon.intensity=.65f;moon.transform.rotation=Quaternion.Euler(36,110,0);
            Point("窗边柔和漫反射",new Vector3(-4.7f,2.7f,-2.2f),new Color(.35f,.65f,1),2.8f,9,false);
            Point("二层窗边漫反射",new Vector3(-4.7f,5.3f,1),new Color(.35f,.6f,1),1.8f,8,false);
            Point("楼梯冷光漫反射",new Vector3(3.9f,4.6f,-2.5f),new Color(.5f,.65f,.78f),2.4f,8,false);
            Point("上层走廊柔光",new Vector3(1.8f,5.9f,1.1f),new Color(.62f,.71f,.8f),1.5f,7,false);
            root.gameObject.AddComponent<DesignAmbient>();
            Light living=Point("客厅可控灯",new Vector3(-2.3f,2.85f,4.4f),new Color(1,.73f,.48f),1.5f,6,false);
            var button=Solid("客厅灯开关",detail,new Vector3(2.8f,1.18f,-6.17f),new Vector3(.085f,.135f,.035f),ceramic,.01f);
            button.AddComponent<LightSwitch>().Configure(living,button.GetComponent<Renderer>(),true);
            for(int i=0;i<5;i++)
            {
                float x=-4.4f+i*2.1f;
                Turned("嵌入筒灯",detail,new Vector3(x,3.08f,1.3f),new[]{new Vector2(0,0),new Vector2(.065f,0),new Vector2(.065f,.018f),new Vector2(0,.018f)},emission);
            }
            Solid("挑空侧暖色灯槽",detail,new Vector3(-1.15f,3.0f,.91f),new Vector3(8.4f,.025f,.04f),emission,.003f,false);
            var volumeProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Art+"/Settings/雨夜后期.asset");
            if(!volumeProfile){volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(volumeProfile,Art+"/Settings/雨夜后期.asset");}
            foreach(var c in volumeProfile.components.ToArray())if(c){volumeProfile.Remove(c.GetType());Object.DestroyImmediate(c,true);}volumeProfile.components.Clear();
            var color=volumeProfile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(color,volumeProfile);color.postExposure.value=.35f;color.contrast.value=13;color.saturation.value=-13;
            var bloom=volumeProfile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,volumeProfile);bloom.intensity.value=.18f;bloom.threshold.value=1.05f;
            var tone=volumeProfile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,volumeProfile);tone.mode.value=TonemappingMode.ACES;
            var vo=new GameObject("新版全局后期");vo.transform.SetParent(root);var vol=vo.AddComponent<Volume>();vol.isGlobal=true;vol.sharedProfile=volumeProfile;vol.priority=20;
            // 静态环境只烘焙一次反射，游戏中不实时刷新。
            var probeObject=new GameObject("室内静态反射探针");probeObject.transform.SetParent(root);probeObject.transform.position=new Vector3(-1.5f,2.1f,-1.5f);
            var probe=probeObject.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.resolution=128;probe.size=new Vector3(12,7,14);probe.boxProjection=true;probe.intensity=.65f;
            EditorUtility.SetDirty(volumeProfile);
        }
        static void BuildCity()
        {
            var building=Mat("远景建筑蓝灰",new Color(.08f,.15f,.2f),.2f);
            var random=new System.Random(721);
            var warm=new List<CombineInstance>();var cold=new List<CombineInstance>();
            Mesh windowMesh=DesignMesh.Save("城市单窗低面网格",
                new List<Vector3>{new Vector3(-.375f,-.5f,0),new Vector3(.375f,-.5f,0),new Vector3(.375f,.5f,0),new Vector3(-.375f,.5f,0)},
                new List<int>{0,1,2,0,2,3,2,1,0,3,2,0},new List<Vector2>{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},
                new List<Vector3>{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward});
            for(int i=0;i<34;i++)
            {
                float x,z;if(i<20){x=-14-(float)random.NextDouble()*32;z=-42+(float)random.NextDouble()*86;}else{x=-9+(float)random.NextDouble()*55;z=-20-(float)random.NextDouble()*35;}
                float w=3+(float)random.NextDouble()*6,d=3+(float)random.NextDouble()*5,h=14+(float)random.NextDouble()*38;
                float bottom=-22;
                Solid("远景楼体",outside,new Vector3(x,bottom+h/2,z),new Vector3(w,h,d),building,.03f,false);
                for(int side=0;side<2;side++)for(int row=0;row<h/1.9f;row++)for(int col=0;col<w/1.35f;col++)
                {
                    if(random.NextDouble()>.42)continue;
                    Vector3 p=new Vector3(x-w/2+.7f+col*1.35f,bottom+1+row*1.9f,z+(side==0?-1:1)*(d/2+.03f));
                    var combine=new CombineInstance{mesh=windowMesh,transform=Matrix4x4.TRS(p,Quaternion.identity,Vector3.one)};
                    (random.NextDouble()>.25?cold:warm).Add(combine);
                }
                // 朝公寓的东向窗格。
                for(int row=0;row<h/1.9f;row++)for(int col=0;col<d/1.35f;col++)if(random.NextDouble()<.36)
                {var p=new Vector3(x+w/2+.03f,bottom+1+row*1.9f,z-d/2+.7f+col*1.35f);cold.Add(new CombineInstance{mesh=windowMesh,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,90,0),Vector3.one)});}
            }
            CombineWindows("城市冷色窗格",cold,coolEmission);CombineWindows("城市暖色窗格",warm,emission);
        }
        static void CombineWindows(string name,List<CombineInstance> list,Material mat)
        {
            var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(list.ToArray());
            string path=Art+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
            var obj=MeshObject(name,outside,mesh,mat,Vector3.zero);obj.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        static void BuildRain()
        {
            var rainMaterial=Mat("细雨粒子",new Color(.48f,.63f,.72f,.24f),0);
            rainMaterial.shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");rainMaterial.SetFloat("_Surface",1);rainMaterial.SetFloat("_Blend",0);rainMaterial.SetFloat("_SrcBlend",5);rainMaterial.SetFloat("_DstBlend",10);rainMaterial.SetFloat("_ZWrite",0);rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");rainMaterial.renderQueue=3000;
            foreach(int side in new[]{0,1})
            {
                var o=new GameObject("窗外细雨");o.transform.SetParent(outside);o.transform.position=side==0?new Vector3(-7.4f,11,0):new Vector3(-1.6f,11,-8.2f);o.transform.rotation=Quaternion.Euler(90,0,0);
                var ps=o.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.startLifetime=1.8f;main.startSpeed=9;main.startSize=.012f;main.maxParticles=900;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=new Color(.6f,.75f,.85f,.28f);main.prewarm=true;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=side==0?new Vector3(2.5f,15,1):new Vector3(13,2.5f,1);
                var em=ps.emission;em.rateOverTime=330;var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=rainMaterial;r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=6;r.velocityScale=.016f;r.shadowCastingMode=ShadowCastingMode.Off;
                ps.Play();
            }
            foreach(string name in new[]{"RainAudioSource","CityAmbienceAudioSource"}){var o=new GameObject(name);o.transform.SetParent(root);var a=o.AddComponent<AudioSource>();a.playOnAwake=false;a.loop=true;}
        }
        static void SetupPlayers()
        {
            Vector3 spawn=new Vector3(4.3f,.08f,-5.55f);
            var marker=new GameObject("PlayerSpawn");marker.transform.SetParent(root);marker.transform.position=spawn;
            var scene=EditorSceneManager.GetActiveScene();
            var desktop=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="DesktopPlayer");
            if(!desktop)
            {
                desktop=new GameObject("DesktopPlayer");
                var cc=desktop.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.27f;cc.center=new Vector3(0,.9f,0);cc.skinWidth=.03f;cc.minMoveDistance=0;
                var eye=new GameObject("DesktopCamera");eye.tag="MainCamera";eye.transform.SetParent(desktop.transform,false);eye.transform.localPosition=new Vector3(0,1.65f,0);
                eye.AddComponent<Camera>().nearClipPlane=.05f;eye.AddComponent<AudioListener>();eye.AddComponent<DesktopInteractionRaycaster>();
                desktop.AddComponent<DesktopPlayerController>().Configure(eye.transform);
            }
            var xr=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="XROrigin_PCVR");
            if(!xr)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
                if(!prefab)throw new InvalidOperationException("官方头显玩家预制体缺失。");
                xr=(GameObject)PrefabUtility.InstantiatePrefab(prefab);xr.name="XROrigin_PCVR";
                foreach(var child in xr.GetComponentsInChildren<Transform>(true))if(new[]{"Teleportation","Climb","Climb Teleport","Grab Move","Jump"}.Contains(child.name))child.gameObject.SetActive(false);
                foreach(var behaviour in xr.GetComponentsInChildren<Behaviour>(true))if(behaviour.GetType().Name=="ContinuousTurnProvider")behaviour.enabled=false;
            }
            DesignVRHands.Install(xr);
            var manager=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="PlayerModeManager");
            if(!manager)manager=new GameObject("PlayerModeManager");
            var bootstrap=manager.GetComponent<PlayerModeBootstrap>();if(!bootstrap)bootstrap=manager.AddComponent<PlayerModeBootstrap>();bootstrap.Configure(desktop,xr);
            xr.SetActive(false);desktop.SetActive(true);
            if(!Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>())
                new GameObject("交互管理器").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            foreach(var o in scene.GetRootGameObjects())if(o.name=="DesktopPlayer"||o.name=="XROrigin_PCVR")
            {o.transform.position=spawn;o.transform.rotation=Quaternion.Euler(0,-70,0);}
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.016f,.031f,.049f);camera.farClipPlane=220;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;}
            var controller=desktop.GetComponent<CharacterController>();controller.stepOffset=.26f;controller.slopeLimit=50;
        }
    }
}

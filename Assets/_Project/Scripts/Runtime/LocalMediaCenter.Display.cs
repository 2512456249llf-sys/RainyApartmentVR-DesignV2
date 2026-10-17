using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace RainyApartmentVR
{
    public sealed partial class LocalMediaCenter
    {
        private const float AppWidth = 1920, AppHeight = 1060;
        private Canvas appCanvas;
        private GraphicRaycaster desktopRaycaster;
        private Font font;
        private RectTransform topBar, sidebar, libraryRoot, playerRoot, transportRoot, offRoot;
        private RawImage videoImage;
        private Text heading, pageText, emptyText, currentTitle, currentStatus, progressText, volumeText, fullscreenText, playerInfo;
        private readonly List<Button> cards = new List<Button>();
        private readonly List<Text> cardTitles = new List<Text>(), cardDetails = new List<Text>(), cardIcons = new List<Text>();
        private readonly List<RawImage> cardCovers = new List<RawImage>();
        private readonly List<Image> categoryImages = new List<Image>();
        private readonly Dictionary<string,Texture2D> covers = new Dictionary<string,Texture2D>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Slider seekSlider, volumeSlider;
        private LocalMediaSeekBar seekInteraction;
        private Text pauseText;
        private bool libraryVisible = true, fullScreen, showRecent;
        private LocalMediaKind category = LocalMediaKind.Movies;
        private int page;
        private float controlsUntil;
        private string drawnPage;
        private Color background = new Color(.035f,.047f,.065f), panel = new Color(.063f,.083f,.11f), accent = new Color(.13f,.66f,.73f);
        private void BuildDisplay()
        {
            font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei",48);
            var node = new GameObject("电视应用画布",typeof(RectTransform),typeof(Canvas)); node.transform.SetParent(transform,false);
            node.transform.localPosition = new Vector3(0,0,-.06f); node.transform.localScale = Vector3.one*(2.34f/AppWidth);
            appCanvas = node.GetComponent<Canvas>(); appCanvas.renderMode = RenderMode.WorldSpace; appCanvas.worldCamera = Camera.main;
            appCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(AppWidth,AppHeight);
            node.AddComponent<TrackedDeviceGraphicRaycaster>(); desktopRaycaster = node.AddComponent<GraphicRaycaster>();
            desktopRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.ThreeD;
            if (!EventSystem.current)
            {
                var input = new GameObject("电视界面事件系统",typeof(EventSystem),typeof(XRUIInputModule)); input.transform.SetParent(transform,false);
            }
            var hit = new GameObject("电视应用交互平面"); hit.transform.SetParent(transform,false); hit.transform.localPosition = new Vector3(0,0,-.04f);
            var collider = hit.AddComponent<BoxCollider>(); collider.size = new Vector3(2.34f,AppHeight*2.34f/AppWidth,.005f);
            var root = appCanvas.GetComponent<RectTransform>();
            Fill("应用背景",root,new Rect(0,0,AppWidth,AppHeight),background);
            playerRoot = RectNode("播放器画面区域",root,new Rect(260,96,1660,798));
            Fill("播放器黑色底色",playerRoot,new Rect(0,0,1660,798),Color.black);
            videoImage = new GameObject("电视播放画面",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            videoImage.transform.SetParent(playerRoot,false); videoImage.texture = Texture2D.blackTexture; videoImage.raycastTarget = true;
            var wake = videoImage.gameObject.AddComponent<Button>(); wake.targetGraphic = videoImage; wake.transition = Selectable.Transition.None; wake.onClick.AddListener(ShowControls);
            playerInfo = Label("播放器状态提示",playerRoot,new Rect(80,280,1500,220),"",52,TextAnchor.MiddleCenter);
            topBar = RectNode("应用顶栏",root,new Rect(0,0,AppWidth,96)); Fill("顶栏底色",topBar,new Rect(0,0,AppWidth,96),panel);
            Label("应用名称",topBar,new Rect(32,16,300,64),"公寓影院",48,TextAnchor.MiddleLeft);
            Label("应用说明",topBar,new Rect(332,24,650,48),"本地视频 · 音乐 · 照片",28,TextAnchor.MiddleLeft,new Color(.53f,.64f,.73f));
            UiButton("电视按钮_刷新",topBar,new Rect(1510,20,170,56),"刷新媒体",RefreshLibrary,28);
            UiButton("电视按钮_电视开关",topBar,new Rect(1702,20,182,56),"关闭电视",()=>SetPower(false),28);
            sidebar = RectNode("应用侧边导航",root,new Rect(0,96,260,798)); Fill("导航底色",sidebar,new Rect(0,0,260,798),panel);
            string[] titles = { "短剧", "电影", "音乐", "照片" };
            for (int i=0;i<titles.Length;i++)
            {
                int index=i; var button=UiButton("媒体分类_"+titles[i],sidebar,new Rect(24,44+i*82,212,64),titles[i],()=>{category=(LocalMediaKind)index;showRecent=false;page=0;libraryVisible=true;fullScreen=false;drawnPage=null;RefreshDisplay();},34);
                categoryImages.Add(button.GetComponent<Image>());
            }
            UiButton("媒体分类_最近观看",sidebar,new Rect(24,420,212,64),"最近观看",()=>{showRecent=true;page=0;libraryVisible=true;fullScreen=false;drawnPage=null;RefreshDisplay();},30);
            UiButton("电视按钮_继续上次",sidebar,new Rect(24,506,212,64),"继续上次",ContinueLast,30);
            Label("媒体文件放置提示",sidebar,new Rect(30,628,205,128),"放入媒体文件后\n列表会自动更新",24,TextAnchor.UpperLeft,new Color(.53f,.64f,.73f));
            libraryRoot = RectNode("电视媒体列表",root,new Rect(260,96,1660,798));
            heading = Label("电视媒体标题",libraryRoot,new Rect(42,16,1000,48),"电影",38,TextAnchor.MiddleLeft);
            UiButton("媒体上一页",libraryRoot,new Rect(1280,16,108,48),"上页",()=>{page=Mathf.Max(0,page-1);drawnPage=null;RefreshDisplay();},25);
            UiButton("媒体下一页",libraryRoot,new Rect(1452,16,108,48),"下页",()=>{page++;ClampPage();drawnPage=null;RefreshDisplay();},25);
            pageText = Label("媒体页码",libraryRoot,new Rect(1378,18,86,44),"1/1",24,TextAnchor.MiddleCenter);
            for(int i=0;i<4;i++)
            {
                int index=i; var button=UiButton("媒体文件_"+i,libraryRoot,new Rect(42+(i%2)*794,82+(i/2)*336,760,306),"",()=>SelectRow(index),30);
                var card=button.GetComponent<RectTransform>(); cards.Add(button);
                Fill("媒体封面底色_"+i,card,new Rect(16,12,728,200),new Color(.085f,.16f,.21f));
                var cover = new GameObject("媒体封面_"+i,typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>(); cover.transform.SetParent(card,false); cover.raycastTarget=false; cardCovers.Add(cover);
                cardIcons.Add(Label("媒体图标_"+i,card,new Rect(16,12,728,200),"影片",56,TextAnchor.MiddleCenter,new Color(.5f,.8f,.85f)));
                cardTitles.Add(Label("媒体文件标题_"+i,card,new Rect(20,222,720,42),"",32,TextAnchor.MiddleLeft));
                cardDetails.Add(Label("媒体播放历史_"+i,card,new Rect(20,264,720,30),"",23,TextAnchor.MiddleLeft,new Color(.57f,.67f,.76f)));
            }
            emptyText = Label("媒体目录为空提示",libraryRoot,new Rect(150,224,1360,330),"",36,TextAnchor.MiddleCenter,new Color(.57f,.67f,.76f));
            transportRoot = RectNode("应用播放控制栏",root,new Rect(0,894,AppWidth,166)); Fill("控制栏底色",transportRoot,new Rect(0,0,AppWidth,166),panel);
            currentTitle = Label("电视当前内容",transportRoot,new Rect(30,12,385,56),"选择媒体开始播放",30,TextAnchor.MiddleLeft);
            currentStatus = Label("电视当前状态",transportRoot,new Rect(30,70,390,68),"",22,TextAnchor.UpperLeft,new Color(.56f,.66f,.75f));
            UiButton("电视按钮_上一集",transportRoot,new Rect(442,42,102,70),"上一个",()=>Next(-1),25);
            var pause=UiButton("电视按钮_播放暂停",transportRoot,new Rect(554,32,124,90),"播放",TogglePause,31,true); pauseText=pause.GetComponentInChildren<Text>();
            UiButton("电视按钮_下一集",transportRoot,new Rect(688,42,102,70),"下一个",()=>Next(1),25);
            seekSlider = UiSlider("电视播放进度",transportRoot,new Rect(824,40,688,54),value=>{});
            seekInteraction = seekSlider.gameObject.AddComponent<LocalMediaSeekBar>(); seekInteraction.Configure(seekSlider,()=>Duration,value=>{ShowControls();Seek(Duration*value);});
            progressText = Label("电视播放时间",transportRoot,new Rect(824,98,688,38),"00:00 / 00:00",24,TextAnchor.MiddleLeft,new Color(.64f,.73f,.8f));
            volumeText = Label("电视音量数值",transportRoot,new Rect(1576,12,294,40),"音量 55%",25,TextAnchor.MiddleLeft);
            volumeSlider = UiSlider("电视音量滑块",transportRoot,new Rect(1576,52,280,42),value=>{ShowControls();SetVolume(value);});
            UiButton("电视按钮_媒体库",transportRoot,new Rect(1580,104,126,42),"媒体库",()=>{libraryVisible=true;fullScreen=false;drawnPage=null;RefreshDisplay();},23);
            var full=UiButton("电视按钮_全屏",transportRoot,new Rect(1722,104,134,42),"全屏",()=>{fullScreen=!fullScreen;libraryVisible=false;ShowControls();RefreshDisplay();},23); fullscreenText=full.GetComponentInChildren<Text>();
            offRoot = RectNode("电视关闭界面",root,new Rect(0,0,AppWidth,AppHeight)); Fill("关机底色",offRoot,new Rect(0,0,AppWidth,AppHeight),background);
            Label("电视关闭提示",offRoot,new Rect(410,310,1100,130),"电视已关闭",58,TextAnchor.MiddleCenter,new Color(.5f,.6f,.7f));
            UiButton("电视打开按钮",offRoot,new Rect(790,494,340,90),"打开电视",()=>{fullScreen=false;SetPower(true);},38,true);
            BuildPhysicalRainSwitch(); ShowControls(); RefreshDisplay();
        }
        private RectTransform RectNode(string name,Transform parent,Rect rect)
        {
            var node=new GameObject(name,typeof(RectTransform)); node.transform.SetParent(parent,false); var target=node.GetComponent<RectTransform>(); SetRect(target,rect); return target;
        }
        private static void SetRect(RectTransform target,Rect rect)
        {
            target.anchorMin=target.anchorMax=new Vector2(0,1); target.pivot=new Vector2(0,1); target.anchoredPosition=new Vector2(rect.x,-rect.y); target.sizeDelta=rect.size;
        }
        private Image Fill(string name,Transform parent,Rect rect,Color color)
        {
            var node=RectNode(name,parent,rect); var image=node.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        private Text Label(string name,Transform parent,Rect rect,string value,int size,TextAnchor align,Color? color=null)
        {
            var node=RectNode(name,parent,rect); var text=node.gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size; text.text=value; text.alignment=align;
            text.color=color??new Color(.9f,.94f,.98f); text.raycastTarget=false; text.supportRichText=false; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate; return text;
        }
        private Button UiButton(string name,Transform parent,Rect rect,string title,Action action,int size,bool primary=false)
        {
            var node=RectNode(name,parent,rect); var image=node.gameObject.AddComponent<Image>(); image.color=primary?accent:new Color(.10f,.14f,.18f);
            var button=node.gameObject.AddComponent<Button>(); button.targetGraphic=image; var colors=button.colors;
            colors.highlightedColor=new Color(.6f,.9f,1); colors.pressedColor=new Color(.35f,.75f,.85f); button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            // 点击后释放界面焦点，避免空格语音按钮被当作界面提交键。
            button.onClick.AddListener(()=>{ShowControls();action?.Invoke();if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);});
            Label(name+"_文字",node,new Rect(4,0,rect.width-8,rect.height),title,size,TextAnchor.MiddleCenter); return button;
        }
        private Slider UiSlider(string name,Transform parent,Rect rect,Action<float> changed)
        {
            var node=RectNode(name,parent,rect); var backgroundImage=node.gameObject.AddComponent<Image>(); backgroundImage.color=Color.clear;
            Fill(name+"_轨道",node,new Rect(0,rect.height*.4f,rect.width,8),new Color(.19f,.26f,.33f));
            var fillArea=RectNode(name+"_填充区域",node,new Rect(0,rect.height*.4f,rect.width,8));
            var fill=Fill(name+"_进度",fillArea,new Rect(0,0,rect.width,8),accent);
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            var handle=Fill(name+"_手柄",node,new Rect(0,rect.height*.24f,18,24),Color.white);
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.anchoredPosition=Vector2.zero;
            node.gameObject.AddComponent<LocalMediaFocusRelease>();
            var slider=node.gameObject.AddComponent<Slider>(); slider.direction=Slider.Direction.LeftToRight; slider.fillRect=fill.rectTransform; slider.handleRect=handle.rectTransform; slider.targetGraphic=handle;
            slider.minValue=0; slider.maxValue=1; slider.onValueChanged.AddListener(value=>changed(value)); return slider;
        }
        private void BuildPhysicalRainSwitch()
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Cube); node.name="电视旁雨声开关"; node.transform.SetParent(transform,false);
            node.transform.localPosition=new Vector3(1.46f,-.36f,-.045f); node.transform.localScale=new Vector3(.48f,.28f,.025f);
            var material=new Material(Resources.Load<Shader>("电视屏幕")); material.SetColor("_BaseColor",new Color(.045f,.13f,.18f)); ownedMaterials.Add(material); node.GetComponent<Renderer>().sharedMaterial=material;
            node.AddComponent<LocalMediaButton>().Configure(()=>SetRainMuted(!RainMuted));
            var label=new GameObject("电视旁雨声开关_文字"); label.transform.SetParent(transform,false); label.transform.localPosition=new Vector3(1.46f,-.36f,-.065f);
            label.transform.SetParent(node.transform,true);
            var mesh=label.AddComponent<TextMesh>(); mesh.font=font; mesh.fontSize=48; mesh.characterSize=.013f; mesh.anchor=TextAnchor.MiddleCenter; mesh.alignment=TextAlignment.Center;
            label.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        private List<LocalMediaEntry> Filtered()
        {
            if (!showRecent) return entries.Where(e=>e.kind==category).ToList();
            return history.records.OrderByDescending(r=>r.lastSeen).Select(r=>entries.Find(e=>e.id==r.id)).Where(e=>e!=null).ToList();
        }
        private void ClampPage() => page=Mathf.Clamp(page,0,Mathf.Max(0,(Filtered().Count-1)/4));
        private void SelectRow(int row)
        {
            var list=Filtered(); int index=page*4+row; if(index>=list.Count)return;
            fullScreen=false; ShowControls(); Play(list[index]);
        }
        private static string Short(string text,int count) => text.Length>count?text.Substring(0,count-1)+"…":text;
        private static string Clock(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds))return "00:00"; var time=TimeSpan.FromSeconds(Math.Max(0,seconds));
            return time.TotalHours>=1?((int)time.TotalHours)+":"+time.Minutes.ToString("00")+":"+time.Seconds.ToString("00"):((int)time.TotalMinutes).ToString("00")+":"+time.Seconds.ToString("00");
        }
        private void ShowControls() => controlsUntil=Time.unscaledTime+5f;
        private void RefreshDisplay()
        {
            if(!appCanvas)return; if(Camera.main)appCanvas.worldCamera=Camera.main;
            bool overlay=Time.unscaledTime<controlsUntil||PlaybackState!="播放中";
            topBar.gameObject.SetActive(PowerOn&&(!fullScreen||overlay)); sidebar.gameObject.SetActive(PowerOn&&!fullScreen);
            libraryRoot.gameObject.SetActive(PowerOn&&libraryVisible); playerRoot.gameObject.SetActive(PowerOn&&!libraryVisible);
            transportRoot.gameObject.SetActive(PowerOn&&(!fullScreen||overlay)); offRoot.gameObject.SetActive(!PowerOn);
            SetRect(playerRoot,fullScreen?new Rect(0,0,AppWidth,AppHeight):new Rect(260,96,1660,798));
            var backdrop=playerRoot.GetChild(0).GetComponent<RectTransform>(); SetRect(backdrop,new Rect(0,0,playerRoot.rect.width,playerRoot.rect.height));
            string[] titles={"短剧","电影","音乐","照片"}; var list=Filtered(); ClampPage(); heading.text=(showRecent?"最近观看":titles[(int)category])+"   "+list.Count+" 个";
            pageText.text=(page+1)+"/"+Mathf.Max(1,(list.Count+3)/4);
            for(int i=0;i<categoryImages.Count;i++)categoryImages[i].color=!showRecent&&i==(int)category?new Color(.12f,.35f,.42f):new Color(.10f,.14f,.18f);
            string key=category+":"+showRecent+":"+page+":"+string.Join("|",list.Skip(page*4).Take(4).Select(e=>e.id+e.coverPath));
            if(key!=drawnPage){drawnPage=key;DrawCards(list);}
            for(int i=0;i<4;i++)
            {
                int index=page*4+i; if(index<list.Count)cardDetails[i].text=history.Position(list[index].id)>1?"继续播放 · "+Clock(history.Position(list[index].id)):"选择后播放";
            }
            emptyText.gameObject.SetActive(list.Count==0);
            emptyText.text=showRecent?"还没有观看记录\n从左侧选择分类，播放后会记在这里。":"这里还没有"+titles[(int)category]+"\n\n把文件放入 VR_Media/"+LocalMediaCatalog.Folders[(int)category]+"\n列表会自动更新，也可以点击右上角刷新。";
            currentTitle.text=Current==null?"选择媒体开始播放":Short(LocalMediaCatalog.DisplayTitle(Current),23);
            currentStatus.text=Current==null?"手柄射线选择\n电脑瞄准按 E":PlaybackState=="读取失败"?Short(Message,42):PlaybackState;
            pauseText.text=PlaybackState=="播放中"||PlaybackState=="准备中"?"暂停":"播放";
            progressText.text=Clock(PlaybackTime)+" / "+Clock(Duration); volumeText.text="音量 "+Mathf.RoundToInt(Volume*100)+"%";
            if(!seekInteraction.IsDragging)seekSlider.SetValueWithoutNotify(Duration>0?(float)(PlaybackTime/Duration):0);
            seekSlider.interactable=Duration>0; volumeSlider.SetValueWithoutNotify(Volume); fullscreenText.text=fullScreen?"退出全屏":"全屏";
            var physicalRain=transform.Find("电视旁雨声开关/电视旁雨声开关_文字"); if(physicalRain)physicalRain.GetComponent<TextMesh>().text=RainMuted?"雨声已关\n点击开启":"雨声已开\n点击关闭";
            playerInfo.gameObject.SetActive(Current==null||Current.kind==LocalMediaKind.Music||PlaybackState=="准备中"||PlaybackState=="读取失败");
            playerInfo.text=Current==null?"选择一个媒体文件":PlaybackState=="准备中"?"正在打开媒体…":PlaybackState=="读取失败"?Message:"音乐播放\n\n"+Short(LocalMediaCatalog.DisplayTitle(Current),25);
            SetRect(playerInfo.rectTransform,new Rect(60,playerRoot.rect.height*.32f,playerRoot.rect.width-120,280));
            FitVideo();
        }
        private void DrawCards(List<LocalMediaEntry> list)
        {
            foreach(var cover in covers.Values)if(cover)Destroy(cover); covers.Clear();
            string[] labels={"短剧","影片","音乐","照片"};
            for(int i=0;i<4;i++)
            {
                int index=page*4+i; cards[i].gameObject.SetActive(index<list.Count); if(index>=list.Count)continue;
                var item=list[index]; cardTitles[i].text=Short(LocalMediaCatalog.DisplayTitle(item),28); cardIcons[i].text=labels[(int)item.kind];
                Texture2D texture=null;
                try
                {
                    if(!string.IsNullOrEmpty(item.coverPath)&&File.Exists(item.coverPath)&&new FileInfo(item.coverPath).Length<8*1024*1024)
                    {
                        if(!covers.TryGetValue(item.coverPath,out texture))
                        { texture=new Texture2D(2,2,TextureFormat.RGBA32,false); if(texture.LoadImage(File.ReadAllBytes(item.coverPath),true))covers[item.coverPath]=texture;else{Destroy(texture);texture=null;} }
                    }
                }
                catch(IOException){} catch(UnauthorizedAccessException){}
                cardCovers[i].gameObject.SetActive(texture); cardIcons[i].gameObject.SetActive(!texture); cardCovers[i].texture=texture;
                if(texture){float aspect=(float)texture.width/texture.height;float w=Mathf.Min(728,200*aspect),h=Mathf.Min(200,728/aspect);SetRect(cardCovers[i].rectTransform,new Rect(16+(728-w)/2,12+(200-h)/2,w,h));}
            }
        }
        private void ClearTexture() { if(videoImage)videoImage.texture=Texture2D.blackTexture; }
        private void ShowTexture(Texture texture,uint width,uint height) { if(videoImage){videoImage.texture=texture;FitVideo();} }
        private void FitVideo()
        {
            if(!videoImage||!playerRoot)return; var texture=videoImage.texture; float aspect=texture&&texture.height>0?(float)texture.width/texture.height:16f/9f;
            float w=Mathf.Min(playerRoot.rect.width,playerRoot.rect.height*aspect),h=Mathf.Min(playerRoot.rect.height,playerRoot.rect.width/aspect);
            SetRect(videoImage.rectTransform,new Rect((playerRoot.rect.width-w)/2,(playerRoot.rect.height-h)/2,w,h));
        }
        public bool IsFullScreen => fullScreen;
        public void SetFullScreen(bool enabled) { fullScreen=enabled;libraryVisible=false;ShowControls();RefreshDisplay(); }
        public static bool TryInteractFromView(Transform view)
        {
            if(!view)return false;
            if(!Physics.Raycast(view.position,view.forward,out var hit,8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
            var center=hit.collider.GetComponentInParent<LocalMediaCenter>(); if(!center)return false;
            var physical=hit.collider.GetComponent<LocalMediaButton>(); if(physical){physical.Interact();return true;}
            return center.ClickScreen(hit.point);
        }
        internal bool ClickScreen(Vector3 point)
        {
            if(!EventSystem.current||!appCanvas.worldCamera)return false;
            var pointer=new PointerEventData(EventSystem.current){position=appCanvas.worldCamera.WorldToScreenPoint(point),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>(); desktopRaycaster.Raycast(pointer,hits);
            if(hits.Count==0){ShowControls();RefreshDisplay();return true;}
            pointer.pointerCurrentRaycast=hits[0]; pointer.pointerPressRaycast=hits[0]; pointer.pressPosition=pointer.position;
            GameObject target=ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject)??ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if(!target)return true; pointer.pointerPress=target;
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            ShowControls(); RefreshDisplay();return true;
        }
        private void DisposeDisplay()
        {
            foreach(var material in ownedMaterials)if(material)Destroy(material);
            foreach(var cover in covers.Values)if(cover)Destroy(cover);
            if(font)Destroy(font);
        }
    }
}

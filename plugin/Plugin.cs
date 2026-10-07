using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

namespace BFSForzaLive;

[BepInPlugin("zrock.bfs.forzalive","BFS Forza Live Backdrop","0.2.3")]
public class Plugin:BasePlugin
{
    internal static BepInEx.Logging.ManualLogSource Logger;
    public override void Load(){Logger=Log;AddComponent<LiveBackdrop>();Log.LogInfo("Forza live bridge loaded; window capture only, flat backdrop prototype.");}
}

public class LiveBackdrop:MonoBehaviour
{
    public LiveBackdrop(IntPtr ptr):base(ptr){}
    readonly object gate=new();
    byte[] latest; ulong latestSequence,displaySequence; long frameStamp;
    volatile bool stopping; Thread reader; string readerState="waiting";
    GameObject root; Camera background; RawImage image; Texture2D texture;
    bool requested=BackdropRow.autoEnable,active,preview=BackdropRow.previewInMenu,cleanVideo=BackdropRow.cleanVideo;
    float nextStatus,nextScene; int applied,errors; string folder;
    readonly Dictionary<Renderer,bool> hidden=new();
    readonly Dictionary<Camera,CameraState> saved=new();
    readonly Dictionary<GrassInstancedPathRenderer,bool> grass=new();
    readonly HashSet<Renderer> coloredTargets=new();
    class CameraState { public CameraClearFlags clear; public int mask; public HDAdditionalCameraData hd; public HDAdditionalCameraData.ClearColorMode hdClear; public bool depth,custom; public FrameSettings frames; public FrameSettingsOverrideMask overrides; }
    static void CleanCamera(HDAdditionalCameraData data,bool video=false)
    {
        var frames=data.m_RenderingPathCustomFrameSettings;
        var overrides=data.renderingPathCustomFrameSettingsOverrideMask;
        foreach(var name in BackdropRow.disabledFrameSettings)
        {
            var field=Enum.Parse<FrameSettingsField>(name);
            frames.SetEnabled(field,false);overrides.mask[(uint)field]=true;
        }
        foreach(var name in BackdropRow.gameplayFrameSettings)
        {
            var field=Enum.Parse<FrameSettingsField>(name);
            frames.SetEnabled(field,!video);overrides.mask[(uint)field]=true;
        }
        data.m_RenderingPathCustomFrameSettings=frames;
        data.renderingPathCustomFrameSettingsOverrideMask=overrides;
        data.customRenderingSettings=true;
    }
    static bool GameplayComponent(Transform t)
    {
        foreach(var name in BackdropRow.preserveComponentNames)
        {
            if(name=="CubeTriggerEvent"&&t.GetComponent<CubeTriggerEvent>()!=null)return true;
            if(name=="CubeExplodeEffectCaller"&&t.GetComponent<CubeExplodeEffectCaller>()!=null)return true;
            if(name=="ProximityPathVehicleMover"&&t.GetComponent<ProximityPathVehicleMover>()!=null)return true;
            if(name=="ChildObjectExplosion"&&t.GetComponent<ChildObjectExplosion>()!=null)return true;
        }
        return false;
    }
    void Awake()
    {
        folder=Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"ForzaLiveData");Directory.CreateDirectory(folder);
        reader=new Thread(ReadFrames){IsBackground=true,Name="BFS Forza frame reader"};reader.Start();
    }
    void ReadFrames()
    {
        while(!stopping)
        {
            try
            {
                using var map=MemoryMappedFile.OpenExisting(FrameRow.mapping,MemoryMappedFileRights.Read);
                using var view=map.CreateViewAccessor(0,FrameRow.headerBytes+FrameRow.width*FrameRow.height*4,MemoryMappedFileAccess.Read);
                byte[] buffer=new byte[FrameRow.width*FrameRow.height*4];ulong previous=0;
                while(!stopping)
                {
                    if(view.ReadUInt32(0)!=FrameRow.magic||view.ReadUInt32(4)!=1||view.ReadUInt32(8)!=FrameRow.width||view.ReadUInt32(12)!=FrameRow.height||view.ReadUInt32(32)!=buffer.Length)throw new InvalidDataException("Frame header mismatch");
                    ulong seq=view.ReadUInt64(16);long stamp=(long)view.ReadUInt64(24);
                    if(view.ReadUInt32(36)==0){readerState="capture stopped";break;}
                    if(seq==previous||(seq&1)!=0){Thread.Sleep(4);continue;}
                    view.ReadArray(FrameRow.headerBytes,buffer,0,buffer.Length);
                    if(seq!=view.ReadUInt64(16))continue;
                    lock(gate){latest=(byte[])buffer.Clone();latestSequence=seq;frameStamp=stamp;}
                    previous=seq;readerState="receiving";
                }
            }
            catch(FileNotFoundException){readerState="waiting for Forza capture";}
            catch(Exception ex){readerState=ex.GetType().Name+": "+ex.Message;}
            Thread.Sleep(250);
        }
    }
    void BuildBackground()
    {
        root=new GameObject("BFS Forza Live Background");Object.DontDestroyOnLoad(root);
        var camObj=new GameObject("Live Capture Camera");camObj.transform.SetParent(root.transform,false);
        background=camObj.AddComponent<Camera>();background.depth=BackdropRow.cameraDepth;background.cullingMask=1<<BackdropRow.layer;
        background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=Color.black;background.orthographic=true;
        background.nearClipPlane=.1f;background.farClipPlane=10;
        var hd=camObj.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=Color.black;CleanCamera(hd,true);
        var canvasObj=new GameObject("Live Capture Canvas");canvasObj.layer=BackdropRow.layer;canvasObj.transform.SetParent(root.transform,false);
        var canvas=canvasObj.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=background;canvas.planeDistance=1;
        var imageObj=new GameObject("Live Forza Frame");imageObj.layer=BackdropRow.layer;imageObj.transform.SetParent(canvasObj.transform,false);
        image=imageObj.AddComponent<RawImage>();image.raycastTarget=false;image.uvRect=new Rect(0,1,1,-1);
        var rect=image.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        texture=new Texture2D(FrameRow.width,FrameRow.height,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Bilinear;image.texture=texture;
        root.SetActive(false);
    }
    static PathFollower Follower()
    {
        var f=PathFollower.Instance;
        if(f!=null&&f.gameObject.activeInHierarchy&&f.pathSource!=null)return f;
        return null;
    }
    void Update()
    {
        try
        {
            if(Input.GetKeyDown(KeyCode.F8))requested=!requested;
            if(Input.GetKeyDown(KeyCode.F9)){Restore();cleanVideo=!cleanVideo;}
            if(Time.unscaledTime>=nextStatus)Commands();
            var follower=Follower();bool network=Mirror.NetworkClient.active||Mirror.NetworkServer.active;
            long age;byte[] pixels=null;ulong seq;
            lock(gate){age=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-frameStamp;seq=latestSequence;if(seq!=displaySequence)pixels=latest;}
            bool should=requested&&!network&&(preview||follower!=null)&&seq>0&&age>=0&&age<FrameRow.staleMs;
            if(should)
            {
                if(root==null)BuildBackground();
                if(!active){active=true;root.SetActive(true);nextScene=0;}
                if(Time.unscaledTime>=nextScene){PrepareScene();nextScene=Time.unscaledTime+.5f;}
                if(pixels!=null)
                {
                    var pin=GCHandle.Alloc(pixels,GCHandleType.Pinned);
                    try{texture.LoadRawTextureData(pin.AddrOfPinnedObject(),pixels.Length);texture.Apply(false,false);}
                    finally{pin.Free();}
                    displaySequence=seq;applied++;
                }
            }
            else if(active)Restore();
            if(Time.unscaledTime>=nextStatus)
            {
                nextStatus=Time.unscaledTime+.5f;
                var status=new {version="0.2.3",mode="flat live backdrop; no car/camera synchronization",requested,active,preview,cleanVideo,network,readerState,sequence=seq,receiveAgeMs=age,appliedFrames=applied,hiddenRenderers=hidden.Count,hiddenGrassSystems=grass.Count,preservedGameplayRenderers=coloredTargets.Count,modifiedCameras=saved.Count,cleanCameraEffects=cleanVideo&&active,distance=follower==null?0:follower.DistanceTraveled,speed=follower==null?0:follower.CurrentWorldSpeed,errors};
                File.WriteAllText(Path.Combine(folder,"status.json"),JsonSerializer.Serialize(status,new JsonSerializerOptions{WriteIndented=true}));
            }
        }
        catch(Exception ex){Plugin.Logger.LogError(ex);if(++errors>=3){requested=false;Restore();}}
    }
    void PrepareScene()
    {
        if(cleanVideo)foreach(var g in Resources.FindObjectsOfTypeAll<GrassInstancedPathRenderer>())
        {
            if(g==null||!g.gameObject.activeInHierarchy)continue;
            if(!grass.ContainsKey(g))grass[g]=g.enabled;
            g.enabled=false;
        }
        foreach(var camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if(camera==null||camera==background||!camera.gameObject.activeInHierarchy||camera.targetTexture!=null)continue;
            if(!saved.ContainsKey(camera))
            {
                var hd=camera.GetComponent<HDAdditionalCameraData>();
                saved[camera]=new CameraState{clear=camera.clearFlags,mask=camera.cullingMask,hd=hd,hdClear=hd==null?default:hd.clearColorMode,depth=hd!=null&&hd.clearDepth,custom=hd!=null&&hd.customRenderingSettings,frames=hd==null?default:hd.m_RenderingPathCustomFrameSettings,overrides=hd==null?default:hd.renderingPathCustomFrameSettingsOverrideMask};
            }
            camera.cullingMask&=~(1<<BackdropRow.layer);camera.clearFlags=CameraClearFlags.Depth;
            var data=saved[camera].hd;if(data!=null){data.clearColorMode=HDAdditionalCameraData.ClearColorMode.None;data.clearDepth=true;if(cleanVideo)CleanCamera(data);}
        }
        foreach(var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if(renderer==null||!renderer.gameObject.activeInHierarchy||!renderer.enabled)continue;
            if(hidden.ContainsKey(renderer)){renderer.enabled=false;continue;}
            if(renderer.transform.root==root.transform)continue;
            bool hide=cleanVideo,keep=false;
            for(var t=renderer.transform;t!=null;t=t.parent)
            {
                if(cleanVideo&&(GameplayComponent(t)||BackdropRow.preserveRootNames.Contains(t.name)||BackdropRow.preserveRootPrefixes.Any(p=>t.name.StartsWith(p,StringComparison.Ordinal))||BackdropRow.preserveRootPatterns.Any(p=>System.Text.RegularExpressions.Regex.IsMatch(t.name,p))))keep=true;
                if(BackdropRow.hideRootNames.Contains(t.name)||BackdropRow.hideRootPrefixes.Any(p=>t.name.StartsWith(p,StringComparison.Ordinal))){hide=true;break;}
            }
            if(keep)hide=false;
            if(keep)coloredTargets.Add(renderer);
            if(hide){hidden[renderer]=renderer.enabled;renderer.enabled=false;}
        }
    }
    void LateUpdate()
    {
        if(!active||!cleanVideo)return;
        try
        {
            foreach(var g in grass)if(g.Key!=null&&g.Key.enabled)g.Key.enabled=false;
        }
        catch(Exception ex){Plugin.Logger.LogError(ex);if(++errors>=3){requested=false;Restore();}}
    }
    void Restore()
    {
        if(root!=null)root.SetActive(false);
        foreach(var r in hidden)if(r.Key!=null)r.Key.enabled=r.Value;hidden.Clear();
        foreach(var g in grass)if(g.Key!=null)g.Key.enabled=g.Value;grass.Clear();
        coloredTargets.Clear();
        foreach(var c in saved)if(c.Key!=null){c.Key.clearFlags=c.Value.clear;c.Key.cullingMask=c.Value.mask;if(c.Value.hd!=null){c.Value.hd.clearColorMode=c.Value.hdClear;c.Value.hd.clearDepth=c.Value.depth;c.Value.hd.m_RenderingPathCustomFrameSettings=c.Value.frames;c.Value.hd.renderingPathCustomFrameSettingsOverrideMask=c.Value.overrides;c.Value.hd.customRenderingSettings=c.Value.custom;}}saved.Clear();active=false;
    }
    void Commands()
    {
        string path=Path.Combine(folder,"command.txt");if(!File.Exists(path))return;
        string cmd=File.ReadAllText(path).Trim();File.Delete(path);
        if(cmd=="on")requested=true;else if(cmd=="off")requested=false;else if(cmd=="preview-on"){preview=true;requested=true;}else if(cmd=="preview-off")preview=false;
        else if(cmd=="clean-on"){Restore();cleanVideo=true;}else if(cmd=="clean-off"){Restore();cleanVideo=false;}
        else if(cmd=="dump")DumpScene();
    }
    void DumpScene()
    {
        var rows=new List<object>();
        foreach(var r in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if(r==null||!r.gameObject.activeInHierarchy)continue;
            var names=new List<string>();for(var t=r.transform;t!=null;t=t.parent)names.Add(t.name);
            rows.Add(new {path=string.Join("/",names.AsEnumerable().Reverse()),enabled=r.enabled,type=r.GetIl2CppType().Name,layer=r.gameObject.layer,note=r.GetComponentInParent<CubeTriggerEvent>()!=null,vehicle=r.GetComponentInParent<ProximityPathVehicleMover>()!=null});
        }
        File.WriteAllText(Path.Combine(folder,"scene.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
    void OnDestroy(){stopping=true;Restore();if(texture!=null)Object.Destroy(texture);if(root!=null)Object.Destroy(root);}
}

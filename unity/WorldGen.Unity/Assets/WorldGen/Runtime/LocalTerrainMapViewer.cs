using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    V mapSelection;bool mapHasSelection,mapInspectDirty;int mapInspectRevision,mapInspectJobRevision;
    Layer mapInspectLayer=(Layer)(-1);string mapInspection="Click a location to inspect it.";
    Task<string> mapInspectJob;CancellationTokenSource mapInspectCancellation;Vector2 mapInfoScroll;
    public V MapSelection=>mapSelection;
    public string MapInspection=>mapInspection;
    void InitializeMapSelection(){if(!mapHasSelection)SelectMapPoint(Position);}
    void SelectMapPoint(V point)
    {mapSelection=point;mapHasSelection=true;mapInspectDirty=true;mapInspectRevision++;mapInspection="Inspecting selected location…";mapInspectCancellation?.Cancel();}
    void TeleportMapSelection(){if(mapHasSelection&&terrain!=null)Teleport(mapSelection);}
    void HandleMapSelectionKeys(Keyboard k)
    {
        if(k.pKey.wasPressedThisFrame)TeleportMapSelection();
        int x=(k.rightArrowKey.isPressed?1:0)-(k.leftArrowKey.isPressed?1:0),y=(k.upArrowKey.isPressed?1:0)-(k.downArrowKey.isPressed?1:0);
        if(x==0&&y==0)return;InitializeMapSelection();TerrainMapViewport.Degrees(mapSelection,out double lat,out double lon);
        double step=Math.Min(.1,Time.unscaledDeltaTime)*.3;SelectMapPoint(TerrainMapViewport.FromDegrees(Math.Max(-90,Math.Min(90,lat+y*mapViewport.LatitudeSpan*step)),TerrainMapViewport.Wrap(lon+x*mapViewport.LongitudeSpan*step)));
    }
    void UpdateMapInspection()
    {
        if(mapInspectJob!=null&&mapInspectJob.IsCompleted)
        {
            if(mapInspectJobRevision==mapInspectRevision&&!mapInspectJob.IsCanceled)
                mapInspection=mapInspectJob.IsFaulted?"Inspection failed: "+mapInspectJob.Exception.GetBaseException().Message:mapInspectJob.Result;
            mapInspectJob=null;
        }
        if(!mapOpen||terrain==null)return;
        InitializeMapSelection();if(mapInspectLayer!=mapLayer){mapInspectLayer=mapLayer;SelectMapPoint(mapSelection);}
        if(!mapInspectDirty||mapInspectJob!=null)return;
        mapInspectDirty=false;mapInspectCancellation?.Dispose();mapInspectCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=mapInspectCancellation.Token;
        var point=mapSelection;var layer=mapLayer;var source=sourceWorld;var local=terrain;double meridian=playState?.PrimeMeridianLongitude??0;mapInspectJobRevision=mapInspectRevision;
        mapInspectJob=Task.Run(()=>TerrainMapSampling.Inspect(local,source,point,layer,token,meridian),token);
    }
    void DisposeMapInspection(){mapInspectCancellation?.Cancel();mapInspectCancellation?.Dispose();}
    void DrawWorldMap()
    {
        if(!mapOpen||terrain==null)return;
        float width=Math.Max(240,Math.Min(Screen.width-64,Math.Min(1150,(Screen.height-300)*2)));mapRect=new Rect((Screen.width-width)/2,64,width,width*.5f);
        GUI.Box(new Rect(mapRect.x-12,mapRect.y-46,mapRect.width+24,mapRect.height+222),"");GUI.Label(new Rect(mapRect.x,mapRect.y-36,145,30),"WORLD MAP");
        SetMapClouds(GUI.Toggle(new Rect(mapRect.x+145,mapRect.y-36,90,30),mapClouds,"Clouds"));
        SetMapRivers(GUI.Toggle(new Rect(mapRect.x+235,mapRect.y-36,90,30),mapRivers,"Rivers"));
        if(GUI.Button(new Rect(mapRect.xMax-235,mapRect.y-38,190,30),GlobeView.LayerName(mapLayer)+"  ▾"))mapLayerMenu=!mapLayerMenu;
        if(GUI.Button(new Rect(mapRect.xMax-37,mapRect.y-38,37,30),"×")){mapOpen=false;CaptureMouse();}
        if(mapTexture!=null)
        {
            GUI.BeginGroup(mapRect);double w=mapRect.width*shownMapViewport.LongitudeSpan/mapViewport.LongitudeSpan,h=mapRect.height*shownMapViewport.LatitudeSpan/mapViewport.LatitudeSpan;
            double x=mapRect.width*(.5+TerrainMapViewport.Wrap(shownMapViewport.Longitude-mapViewport.Longitude)/mapViewport.LongitudeSpan)-w*.5,y=mapRect.height*(.5-(shownMapViewport.Latitude-mapViewport.Latitude)/mapViewport.LatitudeSpan)-h*.5;
            for(int wrap=-1;wrap<=1;wrap++)GUI.DrawTexture(new Rect((float)(x+wrap*360/mapViewport.LongitudeSpan*mapRect.width),(float)y,(float)w,(float)h),mapTexture,ScaleMode.StretchToFill,false);GUI.EndGroup();
        }
        else GUI.Box(mapRect,"Sampling terrain…");
        DrawCloudMap();DrawRiverMap();
        bool ScreenPoint(V point,out Vector2 result)
        {
            TerrainMapViewport.Degrees(point,out double lat,out double lon);double u=.5+TerrainMapViewport.Wrap(lon-mapViewport.Longitude)/mapViewport.LongitudeSpan,v=.5-(lat-mapViewport.Latitude)/mapViewport.LatitudeSpan;
            result=new Vector2(mapRect.x+(float)u*mapRect.width,mapRect.y+(float)v*mapRect.height);return u>=0&&u<=1&&v>=0&&v<=1;
        }
        if(ScreenPoint(Position,out var player))
        {
            GUI.color=new Color(1,.73f,.2f);GUI.DrawTexture(new Rect(player.x-4,player.y-4,8,8),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(player.x+6,player.y-10,55,23),"You");
        }
        if(mapHasSelection&&ScreenPoint(mapSelection,out var selected))
        {
            GUI.color=Color.black;GUI.DrawTexture(new Rect(selected.x-9,selected.y-2,18,4),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(selected.x-2,selected.y-9,4,18),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.DrawTexture(new Rect(selected.x-8,selected.y-1,16,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(selected.x-1,selected.y-8,2,16),Texture2D.whiteTexture);
        }
        float row=mapRect.yMax+8;
        GUI.Label(new Rect(mapRect.x,row,mapRect.width-375,44),"Click to select · arrows move selector · P to teleport\nDrag to pan · scroll to zoom · gold square = you"+(mapDirty||mapJob!=null?" · Sampling…":""));
        GUI.enabled=mapHasSelection;if(GUI.Button(new Rect(mapRect.xMax-365,row,120,30),"Teleport [P]"))TeleportMapSelection();GUI.enabled=true;
        if(GUI.Button(new Rect(mapRect.xMax-240,row,115,30),"Player")){TerrainMapViewport.Degrees(Position,out mapViewport.Latitude,out mapViewport.Longitude);InvalidateMap();}
        if(GUI.Button(new Rect(mapRect.xMax-120,row,120,30),"Whole world")){mapViewport=new TerrainMapViewport{Longitude=playState?.PrimeMeridianLongitude??0};InvalidateMap();}
        var info=new Rect(mapRect.x,row+48,mapRect.width,103);GUI.Box(info,"");GUILayout.BeginArea(info);mapInfoScroll=GUILayout.BeginScrollView(mapInfoScroll);GUILayout.Label(mapInspection,new GUIStyle(GUI.skin.label){wordWrap=true});GUILayout.EndScrollView();GUILayout.EndArea();
        if(mapLayerMenu)
        {
            var menu=new Rect(mapRect.xMax-260,mapRect.y-4,260,Math.Min(420,mapRect.height+100));GUI.Box(menu,"");GUILayout.BeginArea(menu);mapLayerScroll=GUILayout.BeginScrollView(mapLayerScroll);
            foreach(var candidate in GlobeView.VisibleLayers.Where(l=>l!=Layer.Simulation).Concat(new[]{Layer.TreeDensity}))
            {
                GUI.enabled=sourceWorld==null||GenerationPreview.Available(sourceWorld,candidate);
                if(GUILayout.Button(GlobeView.LayerName(candidate)+(TerrainMapSampling.Continuous(candidate)?" · continuous":" · cells"),GUILayout.Height(28))){mapLayer=candidate;mapLayerMenu=false;InvalidateMap();}
            }
            GUI.enabled=true;GUILayout.EndScrollView();GUILayout.EndArea();
        }
        if(!mapLayerMenu)MapInput(Event.current);
    }
}

using System;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    string screen="Menu";
    readonly WorldSetup setup=new WorldSetup();
    Vector2 mapPresetScroll;
    int selectedCell=-1;
    bool worldReady;
    string generationStage="";int generationPercent;
    Layer preferredLayer;
    GUISkin gameSkin;
    GUISkin Theme()
    {
        return BuildGameTheme();
    }
    Task<World> generationJob;
    CancellationTokenSource generationCancel;
    World nextPreview, previousComplete;
    bool Generating=>generationJob!=null;
    void StartGeneration()
    {
        if(Busy)return;
        try
        {
            ValidateInputs();var source=recipe.Clone();ClimateCalibration.Upgrade(source);var resolved=setup.Resolve(source);
            previousComplete=worldReady?world:null;worldReady=false;dirty=false;screen="Setup";selectedCell=-1;error="";preferredLayer=layer;
            generationCancel?.Dispose();generationCancel=new CancellationTokenSource();var token=generationCancel.Token;
            generationJob=Task.Run(()=>{var result=new WorldPipeline().Generate(resolved,p=>{generationStage=p.Stage;generationPercent=p.Percent;progress=p.Stage;percent=p.Percent;if(uiCheck)Debug.Log("WORLDGEN GENERATION "+p.Stage+" "+p.Percent+"%");},token,
                (stage,w)=>{if(stage!="biogeography")Interlocked.Exchange(ref nextPreview,GenerationPreview.Capture(stage,w));});WorldMeridian.For(result,token);return result;},token);
        }
        catch(Exception e){error=e.Message;}
    }
    void CancelGeneration(){generationCancel?.Cancel();cancel?.Cancel();}
    void PollGeneration()
    {
        if(generationJob==null || job!=null)return;
        if(generationJob.IsCompleted)
        {
            Interlocked.Exchange(ref nextPreview,null);
            if(generationJob.IsCanceled || generationJob.IsFaulted)
            {
                error=generationJob.IsFaulted?generationJob.Exception.GetBaseException().Message:"Generation cancelled";
                world=previousComplete;worldReady=world!=null;dirty=true;
                if(world==null){StartCoroutine(Release(globe));globe=null;}
            }
            else {world=generationJob.Result;worldReady=true;dirty=false;activeWorldId=null;play.HasPlayer=false;play.MeridianVersion=0;WorldMeridian.Apply(play,world);centerLongitude=(float)play.PrimeMeridianLongitude;if(string.IsNullOrWhiteSpace(saveName))saveName="New world";}
            generationJob=null;selectedCell=-1;
            if(worldReady)PrepareLocalTerrain();
            if(world!=null){layer=GenerationPreview.Available(world,preferredLayer)?preferredLayer:Layer.Terrain;Refresh();}
            return;
        }
        var preview=Interlocked.Exchange(ref nextPreview,null);
        if(preview!=null){world=preview;selectedCell=-1;layer=GenerationPreview.Available(world,preferredLayer)?preferredLayer:Layer.Microplates;Refresh();}
    }
    void OnGUI()
    {
        if(terrainExplorer!=null){if(saveDialog){GUI.skin=Theme();DrawSaveDialog();}return;}
        if(cameraView==null)return;
        GUI.skin=Theme();GUI.skin.label.wordWrap=true;GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;
        GUI.skin.button.fontSize=14;GUI.skin.label.fontSize=14;
        if(libraryOpen){DrawWorldLibrary();return;}
        if(saveDialog){DrawSaveDialog();return;}
        if(screen=="Menu" || screen=="Options")
        {
            DrawFrontEnd();return;
        }
        if(screen=="Simulation"){DrawSimulationShell();return;}
        float width=screen=="Setup"?PanelWidth:0;
        if(screen=="Setup")
        {
            DrawNewGameSetup(width);
        }
        if(world==null&&displayedWorld==null)
        {
            var r=new Rect(width+40,Screen.height*.38f,Math.Max(180,Screen.width-width-80),150);
            GUI.Label(r,"YOUR WORLD BEGINS HERE",new GUIStyle(TitleStyle(26)){alignment=TextAnchor.MiddleCenter});
            GUI.Label(new Rect(r.x,r.y+95,r.width,45),"Choose your game rules, then generate a world.",new GUIStyle(MutedStyle()){alignment=TextAnchor.MiddleCenter});return;
        }
        if(screen=="Simulation")GUI.Box(new Rect(8,8,Screen.width-16,116),"");
        GUILayout.BeginArea(new Rect(width+16,16,Math.Max(220,Screen.width-width-32),124));GUILayout.BeginHorizontal();
        foreach(var candidate in new[]{Layer.Microplates,Layer.Terrain,Layer.Satellite,Layer.Simulation,Layer.Climate,Layer.Heightmap,Layer.Ecoregions})
        {
            GUI.enabled=GenerationPreview.Available(displayedWorld,candidate);
            if(GUILayout.Toggle(layer==candidate,GlobeView.LayerName(candidate),GUI.skin.button,GUILayout.Width(Mathf.Clamp((Screen.width-width-200)/7,58,90))) && layer!=candidate){layer=preferredLayer=candidate;expandedInspection=false;Refresh();}
        }
        GUI.enabled=displayedWorld!=null;
        if(GUILayout.Button("Inspect ▾",GUILayout.Width(95)))layerMenu=!layerMenu;
        GUILayout.FlexibleSpace();if(GUILayout.Button(projection==Projection.Globe?"▧":"◉",GUILayout.Width(42)))SetProjection(projection==Projection.Globe?Projection.Map:Projection.Globe);
        GUI.enabled=true;GUILayout.EndHorizontal();
        {
            GUILayout.BeginHorizontal();GUI.enabled=displayedWorld!=null;
            bool wire=GUILayout.Toggle(wireframe,"Wireframe",GUILayout.Width(100)),rivers=GUILayout.Toggle(showRivers,"Rivers",GUILayout.Width(85));if(wire!=wireframe||rivers!=showRivers){wireframe=wire;showRivers=rivers;Refresh();}
            if(GUILayout.Button("Grid: "+(gridSpacing==0?"Off":gridSpacing+"°")+" ▾",GUILayout.Width(105)))gridMenu=!gridMenu;GUI.enabled=true;
            if(screen=="Simulation"){if(GUILayout.Button("Save world",GUILayout.Width(100))){libraryNotice="";saveDialog=true;};if(GUILayout.Button("Main menu",GUILayout.Width(100)))screen="Menu";}
            GUILayout.EndHorizontal();
        }
        if(layer==Layer.Simulation){GUILayout.BeginHorizontal();GUILayout.Label("Satellite opacity",GUILayout.Width(125));satelliteOpacity=GUILayout.HorizontalSlider(satelliteOpacity,0,1,GUILayout.Width(200));surfaceMaterial.SetFloat("_SatelliteOpacity",satelliteOpacity);GUILayout.EndHorizontal();}
        GUILayout.EndArea();GUI.enabled=true;
        if(screen=="Simulation")
        {
            GUI.Box(new Rect(8,132,Screen.width-16,128),"");
            GUILayout.BeginArea(new Rect(24,140,Screen.width-48,112));DrawHumanPlayback();GUILayout.EndArea();
        }
        if(layerMenu)
        {
            float popupWidth=270,popupHeight=Math.Min(290,Screen.height-150);
            float popupX=Math.Min(Screen.width-popupWidth-16,width+16+6*Mathf.Clamp((Screen.width-width-200)/6,58,90));
            GUI.Box(new Rect(popupX,58,popupWidth,popupHeight),"");
            GUILayout.BeginArea(new Rect(popupX+4,62,popupWidth-8,popupHeight-8));
            layerScroll=GUILayout.BeginScrollView(layerScroll);
            foreach(var candidate in GlobeView.VisibleLayers){GUI.enabled=GenerationPreview.Available(displayedWorld,candidate);if(GUILayout.Button(GlobeView.LayerName(candidate),GUILayout.Height(32))){layer=preferredLayer=candidate;layerMenu=false;expandedInspection=false;Refresh();}}
            GUI.enabled=true;GUILayout.EndScrollView();GUILayout.EndArea();
        }
        if(gridMenu)
        {
            var popup=new Rect(width+205,88,110,190);GUI.Box(popup,"");GUILayout.BeginArea(new Rect(popup.x+4,popup.y+4,popup.width-8,popup.height-8));
            foreach(int spacing in new[]{0,5,10,15,30})if(GUILayout.Button(spacing==0?"Off":spacing+"°",GUILayout.Height(30))){gridSpacing=spacing;gridMenu=false;Refresh();}
            GUILayout.EndArea();
        }
        float infoHeight=layer==Layer.Simulation?100:42;
        GUI.Box(new Rect(width+16,Screen.height-infoHeight-16,Screen.width-width-32,infoHeight),"");GUILayout.BeginArea(new Rect(width+30,Screen.height-infoHeight-8,Screen.width-width-60,infoHeight-12));
        if(layer==Layer.Simulation)GUILayout.Label("POPULATION & LIFESTYLE",new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,fontSize=12});
        GUILayout.BeginHorizontal();
        GUILayout.Label(layer==Layer.Simulation?PrehistoryView.Describe(humans,world?.Prehistory,selectedCell):CellInspection.Describe(displayedWorld,selectedCell,layer),new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=16});
        if(layer==Layer.Ecoregions && selectedCell>=0 && world?.Ecology!=null && GUILayout.Button("Details",GUILayout.Width(75)))expandedInspection=!expandedInspection;
        GUILayout.EndHorizontal();GUILayout.EndArea();
        if(expandedInspection && layer==Layer.Ecoregions && selectedCell>=0 && displayedWorld?.Ecology!=null)DrawEcologyDetails(width);
    }
    string rollOptionsField;
    bool DiceButton(bool menu=false)
    {
        bool clicked=GUILayout.Button(new GUIContent("",menu?"Roll distribution":"Roll seed"),GUILayout.Width(menu?42:30),GUILayout.Height(28));
        var rect=GUILayoutUtility.GetLastRect();
        if(Event.current.type==EventType.Repaint)
        {
            var color=GUI.color;float x=rect.x+7,y=rect.y+7;GUI.color=new Color(.90f,.87f,.77f);
            GUI.DrawTexture(new Rect(x,y,14,14),Texture2D.whiteTexture);GUI.color=new Color(.10f,.18f,.22f);
            foreach(var dot in new[]{new Vector2(3,3),new Vector2(9,3),new Vector2(6,6),new Vector2(3,9),new Vector2(9,9)})GUI.DrawTexture(new Rect(x+dot.x,y+dot.y,2,2),Texture2D.whiteTexture);
            GUI.color=color;if(menu)GUI.Label(new Rect(rect.x+25,rect.y+4,14,20),"▾");
        }
        return clicked;
    }
    void DrawSetting(FieldInfo f,string label=null)
    {
        var m=f.GetCustomAttribute<SettingAttribute>();bool integer=f.FieldType==typeof(int),seed=f.Name==nameof(Recipe.Seed);
        var range=setup.Get(f,recipe);double current=Convert.ToDouble(f.GetValue(recipe));
        if(!range.Enabled)range.Min=current;
        void Change(){dirty=true;preset="Custom";}
        double Number(string key,double value,double min,double max)
        {
            if(!inputText.TryGetValue(key,out string text) || GUI.GetNameOfFocusedControl()!=key)text=value.ToString("G",CultureInfo.InvariantCulture);
            GUI.SetNextControlName(key);string next=GUILayout.TextField(text,GUILayout.Width(seed?100:80));inputText[key]=next;
            if(next!=text && double.TryParse(next,NumberStyles.Float,CultureInfo.InvariantCulture,out var number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {Change();return Math.Max(min,Math.Min(max,seed?Math.Round(number):WorldSetup.Round(number,integer)));}
            return value;
        }
        GUILayout.BeginHorizontal();GUILayout.Label(label??m.Label);
        if(!seed)
        {
            bool enabled=GUILayout.Toggle(range.Enabled,"Range",GUILayout.Width(66));
            if(enabled!=range.Enabled){range.Enabled=enabled;range.Min=current;range.Max=Math.Max(current,range.Max);Change();}
        }
        if(seed)
        {
            double value=Number(f.Name,current,m.Min,m.Max);f.SetValue(recipe,(int)value);
            if(DiceButton())RollSeed();
            GUILayout.EndHorizontal();return;
        }
        GUILayout.EndHorizontal();
        bool percentage=f.Name==nameof(Recipe.LakeAmountPercent)||f.Name==nameof(Recipe.MountainStrengthPercent);
        double Slider(string key,double value,double low,double high,bool upper)
        {
            GUILayout.BeginHorizontal();if(range.Enabled)GUILayout.Label(upper?"Max":"Min",GUILayout.Width(30));
            value=Number(key,value,low,high);
            if(upper && DiceButton(true))rollOptionsField=rollOptionsField==f.Name?null:f.Name;
            GUILayout.EndHorizontal();
            float pos=(float)(percentage?PercentageControl.Position(value):WorldSetup.Position(value,m.Min,m.Max));
            float next=GUILayout.HorizontalSlider(pos,0,1);
            if(next!=pos){value=Math.Max(low,Math.Min(high,WorldSetup.Round(percentage?PercentageControl.Value(next):WorldSetup.Value(next,m.Min,m.Max),integer)));inputText.Remove(key);Change();}
            return value;
        }
        range.Min=Slider(f.Name,range.Min,m.Min,range.Enabled?range.Max:m.Max,false);
        f.SetValue(recipe,integer?(object)(int)range.Min:range.Min);
        if(range.Enabled)
        {
            range.Max=Slider(f.Name+"Max",range.Max,range.Min,m.Max,true);
            if(rollOptionsField==f.Name)foreach(WorldSetup.Roll roll in Enum.GetValues(typeof(WorldSetup.Roll)))
                if(GUILayout.Button((range.Distribution==roll?"✓ ":"")+(roll==WorldSetup.Roll.Linear?"Linear":roll==WorldSetup.Roll.Low?"Logarithmic · favor lower":"Logarithmic · favor higher"))){range.Distribution=roll;rollOptionsField=null;Change();}
        }
    }
    IEnumerator CheckScreens()
    {
        string folder=Argument("--worldgen-screen-check","");Directory.CreateDirectory(folder);screen="Menu";
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;}
        yield return Capture("menu");screen="Setup";recipe=Recipe.Preset("Earth");recipe.Detail=5000;setup.From(recipe);yield return Capture("setup");Generate();
        bool seen=false;while(Busy){if(!seen && globe!=null && !worldReady){seen=true;yield return Capture("preview");}yield return null;}
        if(!worldReady || error.Length>0){Debug.LogError("SCREEN CHECK "+error);Application.Quit(1);yield break;}
        layer=Layer.Climate;projection=Projection.Map;Refresh();while(Busy)yield return null;selectedCell=0;yield return Capture("earth-climate");
        screen="Simulation";yield return Capture("viewer");
        Debug.Log("SCREEN CHECK PASS stages, Earth setup, climate, viewer; intermediate preview="+seen);Application.Quit(seen?0:1);
    }
}

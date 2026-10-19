using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    bool peoplesOpen,chronicleOpen,detailsOpen,mapSettings,sortOpen;
    Vector2 peoplesScroll,chronicleScroll,detailsScroll,eventScroll;string societySort="Default";
    ChronicleEvent selectedEvent;
    AtlasFact selectedNews;
    ChronicleEvent[] cachedHeadlines=Array.Empty<ChronicleEvent>();
    PrehistoryState legendState;Prehistory legendEngine;HistoryOverlay legendOverlay;string legendSort;
    HistoryLegendEntry[] cachedHistoryLegend=Array.Empty<HistoryLegendEntry>();
    HistoryLegendEntry[] cachedMapLabels=Array.Empty<HistoryLegendEntry>();
    static bool SameHumanDomain(PrehistoryState a,PrehistoryState b)=>PrehistoryState.SameDomain(a,b);
    HistoryLegendEntry[] CurrentHistoryLegend()
    {
        var state=world?.Prehistory;
        if(!SameHumanDomain(legendState,state)||!ReferenceEquals(legendEngine,humans)||legendOverlay!=historyOverlay||legendSort!=societySort)
        {if(humanPaletteJob==null&&humanPaletteReady==null)UpdateHumanPalette();}
        return cachedHistoryLegend;
    }
    PrehistoryState inspectionState;Prehistory inspectionEngine;int inspectionCell=-2;HistoryOverlay inspectionOverlay;string cachedHumanInspection="";
    string CurrentHumanInspection()
    {
        if(!SameHumanDomain(inspectionState,world?.Prehistory)||!ReferenceEquals(inspectionEngine,humans)||inspectionCell!=selectedCell||inspectionOverlay!=historyOverlay)
        {inspectionState=world?.Prehistory;inspectionEngine=humans;inspectionCell=selectedCell;inspectionOverlay=historyOverlay;cachedHumanInspection=PrehistoryView.Describe(humans,inspectionState,selectedCell,historyOverlay);}
        return cachedHumanInspection;
    }
    const float DockHeight=36,PanelHeight=285;
    readonly float[] societyHeights={240,140,PanelHeight};
    readonly int[] societyResizeControls=new int[3];
    int resizingSociety=-1;float societyDragY,societyDragHeight;
    void ResizeSocietyPanel(float mouseY)
    {societyHeights[resizingSociety]=Mathf.Clamp(societyDragHeight+societyDragY-mouseY,100,Mathf.Max(100,Screen.height-DockHeight-64));}
    void DrawHistoryLegendRow(HistoryLegendEntry entry,Rect row)
    {
        var nameStyle=new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=false,margin=new RectOffset(),padding=new RectOffset()};
        var valueStyle=new GUIStyle(nameStyle){fontSize=11,alignment=TextAnchor.MiddleRight};
        string value=SocietyView.SortValue(entry,societySort);float valueWidth=value.Length==0?0:valueStyle.CalcSize(new GUIContent(value)).x+2;
        valueWidth=Mathf.Min(valueWidth,row.width*.48f);float nameWidth=Mathf.Max(12,row.width-18-valueWidth);
        var box=new Rect(row.x,row.y+2,10,14);var rgb=SocietyView.LegendColor(entry);
        GUI.color=new Color((float)rgb.X,(float)rgb.Y,(float)rgb.Z);GUI.DrawTexture(box,Texture2D.whiteTexture);GUI.color=Color.white;
        if(GUI.Button(new Rect(row.x+15,row.y,nameWidth,20),new GUIContent(SocietyView.Short(entry.Name,Math.Max(2,(int)(nameWidth/6))),entry.Name),nameStyle)){selectedCell=entry.Cell;detailsOpen=true;mapSettings=false;}
        if(value.Length>0)GUI.Label(new Rect(row.xMax-valueWidth,row.y,valueWidth,20),new GUIContent(value,value),valueStyle);
    }
    void DrawHistoryLegendPanel()
    {
        var r=SocietyPanel(0);DrawSocietyPanelFrame(0);
        GUI.Label(new Rect(r.x+12,r.y+12,r.width-92,25),historyOverlay.ToString().ToUpperInvariant());
        if(GUI.Button(new Rect(r.xMax-77,r.y+10,65,26),"Sort ▾"))sortOpen=!sortOpen;
        float top=r.y+43;
        if(sortOpen){float optionWidth=(r.width-24)/3;int i=0;foreach(string option in new[]{"Default","Population","Land area"}){if(GUI.Button(new Rect(r.x+12+i*optionWidth,top,optionWidth,25),option)){societySort=option;sortOpen=false;}i++;}top+=30;}
        var entries=CurrentHistoryLegend();var viewport=new Rect(r.x+10,top,r.width-20,Mathf.Max(20,r.yMax-top-10));
        int rows=Math.Max(1,(int)(viewport.height/21)),pages=Math.Max(1,(entries.Length+2*rows-1)/(2*rows));
        float column=(viewport.width-20)/2;
        peoplesScroll=GUI.BeginScrollView(viewport,peoplesScroll,new Rect(0,0,viewport.width-18,Mathf.Max(viewport.height,pages*rows*21)));
        for(int i=0;i<entries.Length;i++)DrawHistoryLegendRow(entries[i],new Rect(((i/rows)%2)*column,(i/(2*rows)*rows+i%rows)*21,column-8,20));
        if(entries.Length==0)GUI.Label(new Rect(0,0,viewport.width,25),"None yet.");GUI.EndScrollView();
    }
    Rect SocietyPanel(int index)
    {float height=Mathf.Clamp(societyHeights[index],100,Mathf.Max(100,Screen.height-DockHeight-64));return new Rect(index==0?0:index==1?Screen.width*.26f:Screen.width*.74f,Screen.height-DockHeight-height,index==1?Screen.width*.48f:Screen.width*.26f,height);}
    void DrawSocietyPanelFrame(int index)
    {
        var r=SocietyPanel(index);GUI.Box(r,"");var handle=new Rect(r.x,r.y,r.width,9);
        int control=societyResizeControls[index];var e=Event.current;
        if(e.type==EventType.MouseDown&&e.button==0&&handle.Contains(e.mousePosition))
        {resizingSociety=index;societyDragY=e.mousePosition.y;societyDragHeight=r.height;GUIUtility.hotControl=control;e.Use();}
        if(resizingSociety==index&&GUIUtility.hotControl==control)
        {
            if(e.type==EventType.MouseDrag){ResizeSocietyPanel(e.mousePosition.y);e.Use();}
            if(e.type==EventType.MouseUp){resizingSociety=-1;GUIUtility.hotControl=0;e.Use();}
        }
        if(e.type==EventType.Repaint){GUI.color=handle.Contains(e.mousePosition)||resizingSociety==index?new Color(.85f,.77f,.49f):new Color(.38f,.49f,.51f);GUI.DrawTexture(new Rect(r.center.x-24,r.y+3,48,2),Texture2D.whiteTexture);GUI.color=Color.white;}
    }
    bool OverSocietyPanel(Vector2 p)
    {
        var gui=new Vector2(p.x,Screen.height-p.y);return resizingSociety>=0||atlasOpen||expandedInspection||p.y<DockHeight||peoplesOpen&&SocietyPanel(0).Contains(gui)||chronicleOpen&&SocietyPanel(1).Contains(gui)||detailsOpen&&SocietyPanel(2).Contains(gui)||selectedEvent!=null;
    }
    void ChangeHumanSpeed(int direction)=>humanPlayback.Change(direction);
    void DrawSimulationShell()
    {
        // Reserve stable handles before variable-length legends or selections
        // allocate controls. A new state appearing must not interrupt a drag.
        for(int i=0;i<3;i++)societyResizeControls[i]=GUIUtility.GetControlID(18490+i,FocusType.Passive);
        DrawSocietyMarks();var state=world?.Prehistory;GUI.enabled=!atlasOpen;
        GUI.Box(new Rect(0,0,Screen.width,52),"");
        if(GUI.Button(new Rect(8,7,100,36),"World [W]"))WorldAtlasKey();
        GUI.enabled=!atlasOpen&&CanRequestTerrain;if(GUI.Button(new Rect(114,7,105,36),new GUIContent("Explore terrain","Explore the selected map location; simulation pauses until you return")))RequestTerrain();
        GUILayout.BeginArea(new Rect(Mathf.Max(232,Screen.width-600),7,Mathf.Min(594,Screen.width-238),40));GUILayout.BeginHorizontal();
        GUILayout.Label(state==null?"Preparing…":SimulationClockView.Date(state,humanPlayback.Rate),new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold},GUILayout.Width(195));
        GUI.enabled=!atlasOpen&&humans!=null&&!simulationPaused;
        if(GUILayout.Button(humanPlaying?"Ⅱ":"▶",GUILayout.Width(40))){humanPlaying=!humanPlaying;humanPlayback.Reset();}
        if(GUILayout.Button("‹",GUILayout.Width(28)))ChangeHumanSpeed(-1);GUILayout.Label(SimulationClockView.Rate(humanPlayback.Rate),GUILayout.Width(130));if(GUILayout.Button("›",GUILayout.Width(28)))ChangeHumanSpeed(1);
        bool automatic=!humanPlayback.Manual;
        bool nextAutomatic=GUILayout.Toggle(automatic,new GUIContent("Auto","Adjust playback speed to cultures, first states and technological ages"),GUILayout.Width(68));
        if(nextAutomatic!=automatic){if(nextAutomatic)humanPlayback.AutoWorld(state,CurrentMusicEra());else humanPlayback.HoldRate();}
        GUI.enabled=!atlasOpen;GUILayout.EndHorizontal();GUILayout.EndArea();
        GUI.Box(new Rect(0,Screen.height-DockHeight,Screen.width,DockHeight),"");
        if(GUI.Button(new Rect(5,Screen.height-32,Screen.width*.26f-10,28),(peoplesOpen?"▾  ":"▴  ")+historyOverlay.ToString()))peoplesOpen=!peoplesOpen;
        if(GUI.Button(new Rect(Screen.width*.26f+5,Screen.height-32,Screen.width*.48f-10,28),new GUIContent((chronicleOpen?"▾  ":"▴  ")+WorldEraView.Name(CurrentMusicEra())+"  ·  "+SocietyView.Population(state).ToString("N0")+" people",WorldEraView.Description(CurrentMusicEra()))))chronicleOpen=!chronicleOpen;
        if(GUI.Button(new Rect(Screen.width*.74f+5,Screen.height-32,Screen.width*.26f-10,28),(detailsOpen?"▾  ":"▴  ")+SelectionCaption()))detailsOpen=!detailsOpen;
        if(peoplesOpen)DrawHistoryLegendPanel();
        if(chronicleOpen){BeginSocietyPanel(1);chronicleScroll=GUILayout.BeginScrollView(chronicleScroll);GUILayout.BeginHorizontal();float columnWidth=Mathf.Max(60,(Screen.width*.48f-54)/4);GUILayout.BeginVertical(GUILayout.Width(columnWidth));GUILayout.Label("Global population",new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true,fontStyle=FontStyle.Bold});GUILayout.Space(10);GUILayout.Label(SocietyView.Population(state).ToString("N0"),new GUIStyle(GUI.skin.label){fontSize=18});GUILayout.Label(WorldEraView.Name(CurrentMusicEra()),new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true});GUILayout.EndVertical();
            foreach(var category in new[]{ChronicleCategory.Politics,ChronicleCategory.Society,ChronicleCategory.Discoveries}){GUILayout.BeginVertical(GUILayout.Width(columnWidth));GUILayout.Label(category==ChronicleCategory.Politics?"Politics & diplomacy":category==ChronicleCategory.Society?"Society & culture":"Inventions & discoveries",new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true,fontStyle=FontStyle.Bold});foreach(var e in cachedHeadlines.Where(e=>e.Category==category))if(GUILayout.Button(new GUIContent(SimulationClockView.Year(world?.Prehistory,e.Year)+"\n"+e.Text,e.Text),new GUIStyle(GUI.skin.button){fontSize=12,wordWrap=true,alignment=TextAnchor.UpperLeft,padding=new RectOffset(4,4,3,3)})){selectedEvent=e;selectedNews=HistoryAtlas.NewsDetail(state,e.Year,e.CultureId,e.Cell,e.Category);eventScroll=Vector2.zero;}GUILayout.EndVertical();}
            GUILayout.EndHorizontal();GUILayout.EndScrollView();GUILayout.EndArea();}
        if(detailsOpen){BeginSocietyPanel(2);GUILayout.BeginHorizontal();if(GUILayout.Toggle(!mapSettings,"Selection",GUI.skin.button))mapSettings=false;if(GUILayout.Toggle(mapSettings,"Map settings",GUI.skin.button))mapSettings=true;GUILayout.EndHorizontal();detailsScroll=GUILayout.BeginScrollView(detailsScroll);
            if(mapSettings)DrawSocietyMapSettings();else if(humans!=null)
            {
                var subject=humanOverlayOpacity>0?HistoryAtlas.Selection(state,selectedCell,historyOverlay):null;
                if(subject!=null&&GUILayout.Button("Read "+SocietyView.Short(subject.Name,24)))OpenAtlas(subject.Kind,subject.Id);
                if(humanOverlayOpacity>0)DrawSelectionCard(CurrentSelectionCard());
                else DrawSelectionCard(CurrentBaseCard());
                if(humanOverlayOpacity==0&&layer==Layer.Ecoregions&&selectedCell>=0&&displayedWorld?.Ecology!=null&&GUILayout.Button("Species and food web"))expandedInspection=true;
                if(humanOverlayOpacity>0){GUILayout.BeginHorizontal();foreach(var profile in HistoryAtlas.ProfilesAt(state,selectedCell))if(GUILayout.Button(new GUIContent(HistoryAtlas.ProfileLabel(profile),profile.Name),new GUIStyle(GUI.skin.button){fontSize=11,padding=new RectOffset(5,5,4,4)}))OpenAtlas(profile.Kind,profile.Id);GUILayout.EndHorizontal();}
            }
            if(error.Length>0)GUILayout.Label(error);GUILayout.EndScrollView();GUILayout.EndArea();}
        if(selectedEvent!=null){float width=Mathf.Min(580,Screen.width-40),height=Mathf.Min(350,Screen.height-80);var r=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);GUI.Box(r,"");GUILayout.BeginArea(new Rect(r.x+20,r.y+20,r.width-40,r.height-40));GUILayout.Label(SimulationClockView.Year(world?.Prehistory,selectedEvent.Year));eventScroll=GUILayout.BeginScrollView(eventScroll);GUILayout.Label(selectedNews?.Value??selectedEvent.Text,new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=18});foreach(var link in selectedNews?.Related??Array.Empty<AtlasLink>())if(GUILayout.Button(link.Name)){selectedEvent=null;OpenAtlas(link.Kind,link.Id);}GUILayout.EndScrollView();if(GUILayout.Button("Close",GUILayout.Height(32)))selectedEvent=null;GUILayout.EndArea();}
        GUI.enabled=true;if(expandedInspection&&layer==Layer.Ecoregions&&selectedCell>=0&&displayedWorld?.Ecology!=null)DrawEcologyDetails(0);DrawAtlas();
        if(simulationPaused){var r=new Rect(Screen.width/2-170,Screen.height/2-205,340,410);GUI.Box(r,"");GUILayout.BeginArea(new Rect(r.x+24,r.y+20,r.width-48,r.height-40));GUILayout.Label("SETTINGS · PAUSED",new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold});GUILayout.Space(16);DrawAudioOptions();if(GUILayout.Button("Resume",GUILayout.Height(42)))ToggleSimulationPause();if(GUILayout.Button("Save world",GUILayout.Height(42))){libraryNotice="";saveDialog=true;}if(Prehistory.CanUpgradeHistory(world?.Prehistory)&&humanStep==null&&GUILayout.Button(new GUIContent("Update simulation rules","Preserve recorded history; use new formation and administration rules for future events"),GUILayout.Height(42))){var engine=humans;var source=world.Prehistory;calendarWorkStarted=SimulationTime.Seconds(source);humanStep=System.Threading.Tasks.Task.Run(()=>engine.UpgradeToCurrentHistory(source));}if(GUILayout.Button("Main menu",GUILayout.Height(42))){humanPlaying=false;simulationPaused=false;screen="Menu";}GUILayout.EndArea();}
    }
    PrehistoryState cardState;int cardCell=-2;HistoryOverlay cardOverlay;SelectionCard selectionCard=new SelectionCard();
    World baseCardWorld;int baseCardCell=-2;Layer baseCardLayer;SelectionCard baseSelectionCard=new SelectionCard();
    SelectionCard CurrentBaseCard(){if(baseCardWorld!=displayedWorld||baseCardCell!=selectedCell||baseCardLayer!=layer){baseCardWorld=displayedWorld;baseCardCell=selectedCell;baseCardLayer=layer;baseSelectionCard=MapSelection.Read(displayedWorld,selectedCell,layer);}return baseSelectionCard;}
    SelectionCard CurrentSelectionCard(){if(!SameHumanDomain(cardState,world?.Prehistory)||cardCell!=selectedCell||cardOverlay!=historyOverlay){cardState=world?.Prehistory;cardCell=selectedCell;cardOverlay=historyOverlay;selectionCard=HistoryAtlas.Card(cardState,selectedCell,historyOverlay,humans);}return selectionCard;}
    void DrawSelectionCard(SelectionCard card)
    {
        AtlasParagraph(card.Title,20);AtlasParagraph(card.Subtitle,12);
        if(card.IllustrationKey.Length>0){string key=card.IllustrationKey;if(habitatRequested.Add(key))StartCoroutine(LoadHabitatTexture(key));var r=GUILayoutUtility.GetRect(100,108,GUILayout.ExpandWidth(true));if(habitatTextures.TryGetValue(key,out var texture))GUI.DrawTexture(r,texture,ScaleMode.ScaleAndCrop);AtlasParagraph("Illustrative habitat · not a reconstruction",10);}
        GUILayout.Space(8);AtlasFactTiles(card.Facts,2);
        foreach(var bar in card.Bars)SubsistenceBar(bar.Name,(int)bar.Value);
        AtlasParagraph(card.Note,11);foreach(var project in card.Projects){AtlasParagraph(project.Name+" · Lv "+project.Level+" → "+(project.Level+1),13);SubsistenceBar("Next level",project.ResearchPermille);}
    }
    string SelectionCaption(){if(selectedCell<0||humans==null)return "Select a location";return SocietyView.Short(humanOverlayOpacity>0?CurrentSelectionCard().Title:CellInspection.Describe(displayedWorld,selectedCell,layer),38);}
    void BeginSocietyPanel(int index){var r=SocietyPanel(index);DrawSocietyPanelFrame(index);GUILayout.BeginArea(new Rect(r.x+12,r.y+12,r.width-24,r.height-24));}
    void SubsistenceBar(string label,int value){GUILayout.Label(label+"  "+value/10+"%");var r=GUILayoutUtility.GetRect(20,6,GUILayout.ExpandWidth(true));GUI.color=new Color(.25f,.31f,.33f);GUI.DrawTexture(r,Texture2D.whiteTexture);r.width*=value/1000f;GUI.color=new Color(.71f,.65f,.4f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
    void DrawSocietyMapSettings()
    {
        AtlasParagraph(MapShortcuts.Help,11);
        if(GUILayout.Button("City markers: "+SettlementMarkers.Name(settlementMarkerMode)+" [C]"))settlementMarkerMode=SettlementMarkers.Next(settlementMarkerMode);
        GUILayout.Label("HISTORICAL OVERLAY");foreach(HistoryOverlay mode in Enum.GetValues(typeof(HistoryOverlay)))if(GUILayout.Toggle(historyOverlay==mode,mode==HistoryOverlay.ReligiousInstitutions?"Religious institutions":mode.ToString(),GUI.skin.button)&&historyOverlay!=mode){historyOverlay=mode;UpdateHumanPalette();}
        DrawTechnologyCeiling();
        if(world?.Prehistory?.History!=null){bool enabled=GUI.enabled;GUI.enabled=enabled&&exportJob==null;if(GUILayout.Button(exportJob==null?"Export campaign chronicle":"Export in progress…"))ExportCampaignChronicle();GUI.enabled=enabled;}
        GUILayout.Label("BASE MAP");foreach(var mode in new[]{Layer.Satellite,Layer.Terrain,Layer.Climate,Layer.Heightmap,Layer.Ecoregions,Layer.Microplates})if(GUILayout.Toggle(layer==mode,GlobeView.LayerName(mode),GUI.skin.button)&&layer!=mode){layer=preferredLayer=mode;Refresh();}
        GUILayout.Label("Base opacity  "+Mathf.RoundToInt(satelliteOpacity*100)+"%");satelliteOpacity=GUILayout.HorizontalSlider(satelliteOpacity,0,1);GUILayout.Label("Societies opacity  "+Mathf.RoundToInt(humanOverlayOpacity*100)+"%");humanOverlayOpacity=GUILayout.HorizontalSlider(humanOverlayOpacity,0,1);
        wireframe=GUILayout.Toggle(wireframe,"Cell boundaries");showRivers=GUILayout.Toggle(showRivers,"Rivers");if(GUILayout.Button("Grid: "+gridSpacing)){var values=new[]{0,5,10,15,30};gridSpacing=values[(Array.IndexOf(values,gridSpacing)+1)%values.Length];}if(GUILayout.Button(projection==Projection.Globe?"Map":"Globe"))SetProjection(projection==Projection.Globe?Projection.Map:Projection.Globe);ApplyCachedDisplay();
    }
    async void ExportCampaignChronicle()
    {
        if(exportJob!=null||world?.Prehistory?.History==null)return;
        var snapshot=world.Prehistory;var geography=world;
        string folder=System.IO.Path.Combine(Application.persistentDataPath,"Chronicles");
        string file=System.IO.Path.Combine(folder,"chronicle-"+snapshot.Hash+".txt");
        try
        {
            exportJob=System.Threading.Tasks.Task.Run(()=>{System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllText(file,SocietyView.CampaignLore(snapshot,geography));});
            await exportJob;error="Chronicle saved: "+file;
        }
        catch(Exception e){error="Chronicle export failed: "+e.Message;}
        finally{exportJob=null;}
    }
    bool SocietyPosition(int cell,out Vector2 point)
    {
        point=default;if(globe==null||world==null)return false;var p=SocietyView.Location(world,cell);Vector3 position;
        if(VisibleProjection==Projection.Map){float x=(float)(Math.Atan2(p.Y,p.X)*2/Math.PI)-centerLongitude/90;x=Mathf.Repeat(x+2,4)-2;position=new Vector3(x,(float)(Math.Asin(p.Z)*2/Math.PI),0);}
        else {position=globe.transform.TransformPoint(new Vector3((float)p.X,(float)p.Z,(float)-p.Y)*1.003f);if(Vector3.Dot(position.normalized,(cameraView.transform.position-position).normalized)<=0)return false;}
        var screenPoint=cameraView.WorldToScreenPoint(position);if(screenPoint.z<0||!cameraView.pixelRect.Contains(new Vector2(screenPoint.x,screenPoint.y)))return false;point=new Vector2(screenPoint.x,Screen.height-screenPoint.y);return true;
    }
    void DrawSocietyMarks()
    {
        var s=world?.Prehistory?.Society;if(s==null||humanOverlayOpacity<=0||simulationPaused)return;var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=12,fontStyle=FontStyle.Normal};style.normal.textColor=new Color(.07f,.09f,.11f);GUI.color=new Color(1,1,1,humanOverlayOpacity);
        var labelRects=new System.Collections.Generic.List<Rect>();
        foreach(var c in cachedMapLabels.Take(80))if(SocietyPosition(c.LabelCell>=0?c.LabelCell:c.Cell,out var p)){string name=c.LabelName.Length>0?c.LabelName:c.Name;float width=style.CalcSize(new GUIContent(name)).x+8;var r=new Rect(p.x-width/2,p.y-11,width,22);if(labelRects.Any(previous=>previous.Overlaps(r)))continue;labelRects.Add(r);var shadow=new Rect(r.x+1,r.y+1,r.width,r.height);style.normal.textColor=Color.white;GUI.Label(shadow,name,style);style.normal.textColor=new Color(.07f,.09f,.11f);GUI.Label(r,name,style);}
        if(cachedSettlementMarkers.Length==5&&settlementMarkerMode!=SettlementMarkerMode.Off)
        {
            if(settlementDot==null){settlementDot=new Texture2D(16,16,TextureFormat.RGBA32,false);var pixels=new Color[256];for(int y=0;y<16;y++)for(int x=0;x<16;x++){float distance=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(8,8));pixels[y*16+x]=new Color(1,1,1,Mathf.Clamp01(7.5f-distance));}settlementDot.SetPixels(pixels);settlementDot.Apply(false,true);}
            foreach(var t in cachedSettlementMarkers[(int)settlementMarkerMode])
            {
                bool visible=VisibleProjection==Projection.Map?mapZoom<(t.Capital?.9f:t.City?.5f:.18f):globeDistance<(t.Capital?4f:t.City?2.5f:1.7f);
                if(!visible||!SocietyPosition(t.Cell,out var p))continue;
                float radius=t.Capital?4:t.City?3:2;GUI.color=new Color(.07f,.12f,.16f,humanOverlayOpacity);GUI.DrawTexture(new Rect(p.x-radius-1,p.y-radius-1,2*radius+2,2*radius+2),settlementDot);
                GUI.color=t.Capital?new Color(1,.84f,.39f,humanOverlayOpacity):new Color(.95f,.95f,.91f,humanOverlayOpacity);GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,2*radius,2*radius),settlementDot);
                bool names=VisibleProjection==Projection.Map?mapZoom<(t.Capital?.32f:.16f):globeDistance<(t.Capital?1.9f:1.55f);if(!names)continue;
                float width=style.CalcSize(new GUIContent(t.Name)).x+8;var label=new Rect(p.x-width/2,p.y+radius+2,width,18);if(labelRects.Any(r=>r.Overlaps(label)))continue;labelRects.Add(label);
                GUI.color=new Color(1,1,1,humanOverlayOpacity);style.normal.textColor=Color.white;GUI.Label(new Rect(label.x+1,label.y+1,label.width,label.height),t.Name,style);style.normal.textColor=new Color(.07f,.09f,.11f);GUI.Label(label,t.Name,style);
            }
        }
        GUI.color=Color.white;
    }
}

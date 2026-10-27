using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    bool atlasOpen,atlasPast,atlasSearchFocused;
    string atlasKind="world",atlasSearch="",atlasSection="Overview";
    Vector2 atlasDirectoryScroll,atlasArticleScroll;
    AtlasDocument atlasDocument;
    AtlasTimeline atlasTimeline;
    AtlasLink[] atlasDirectory=Array.Empty<AtlasLink>();
    HashSet<string> atlasRepeatedNames=new HashSet<string>();
    int atlasDirectoryPage;
    string atlasSort="Name";bool atlasSortDescending;
    PrehistoryState atlasSnapshot;
    readonly Stack<Tuple<string,int>> atlasBack=new Stack<Tuple<string,int>>();
    void OpenAtlas(string kind="world",int id=-1)
    {
        if(atlasOpen&&atlasDocument!=null)atlasBack.Push(Tuple.Create(atlasDocument.Kind,atlasDocument.Id));
        else atlasBack.Clear();
        atlasOpen=true;atlasSnapshot=world?.Prehistory;LoadAtlasArticle(kind,id);RefreshAtlasDirectory();EndAtlasUiChange();
    }
    void WorldAtlasKey()
    {
        if(atlasOpen){OpenAtlas();return;}
        var subject=humanOverlayOpacity>0?HistoryAtlas.Selection(world?.Prehistory,selectedCell,historyOverlay):null;
        OpenAtlas(subject?.Kind??(selectedCell>=0?"location":"world"),subject?.Id??selectedCell);
    }
    void CloseWorldAtlas(){atlasOpen=false;atlasSearchFocused=false;GUIUtility.keyboardControl=0;}
    void LoadAtlasArticle(string kind,int id)
    {
        atlasSearchFocused=false;GUIUtility.keyboardControl=0;
        atlasDocument=kind=="location"?AtlasNavigation.Location(humanOverlayOpacity>0?CurrentSelectionCard():CurrentBaseCard(),id,atlasSnapshot?.Years??0,SimulationClockView.EpochYear(atlasSnapshot),atlasSnapshot?.DeepPast?.Active==true?HomininView.Date(atlasSnapshot):""):HistoryAtlas.Read(atlasSnapshot,kind,id,world,humans);
        atlasKind=AtlasNavigation.DirectoryKind(kind);atlasSection="Overview";atlasArticleScroll=Vector2.zero;atlasTimeline=null;
    }
    void RefreshAtlasDirectory(){if(!AtlasTables.Columns(atlasKind,atlasSnapshot).Contains(atlasSort)){atlasSort="Name";atlasSortDescending=false;}atlasDirectory=AtlasTables.Sort(HistoryAtlas.Directory(atlasSnapshot,atlasKind,atlasSearch,atlasPast),atlasSort,atlasSortDescending);atlasRepeatedNames=new HashSet<string>(atlasDirectory.GroupBy(x=>x.Name).Where(g=>g.Count()>1).Select(g=>g.Key));atlasDirectoryScroll=Vector2.zero;atlasDirectoryPage=0;}
    void AtlasNavigate(AtlasLink link){if(string.IsNullOrEmpty(link.Kind))return;atlasBack.Push(Tuple.Create(atlasDocument.Kind,atlasDocument.Id));LoadAtlasArticle(link.Kind,link.Id);RefreshAtlasDirectory();EndAtlasUiChange();}
    void EndAtlasUiChange(){if(Event.current!=null&&Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint)GUIUtility.ExitGUI();}
    void DrawAtlas()
    {
        if(!atlasOpen)return;
        float width=Mathf.Min(1120,Screen.width-32),height=Mathf.Min(800,Screen.height-100);var panel=new Rect((Screen.width-width)/2,58,width,height);
        GUI.Box(panel,"");GUILayout.BeginArea(new Rect(panel.x+16,panel.y+12,panel.width-32,panel.height-24));
        GUILayout.BeginHorizontal();GUILayout.Label("WORLD ATLAS",new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold});
        GUILayout.Label("Playback paused · "+SimulationClockView.Year(atlasSnapshot,atlasSnapshot?.Years??0),new GUIStyle(GUI.skin.label){wordWrap=true},GUILayout.Width(atlasSnapshot?.DeepPast?.Active==true?Mathf.Min(330,width*.4f):200));
        if(GUILayout.Button("Refresh",GUILayout.Width(80))){atlasSnapshot=world?.Prehistory;LoadAtlasArticle(atlasDocument.Kind,atlasDocument.Id);RefreshAtlasDirectory();EndAtlasUiChange();}
        if(GUILayout.Button("Close",GUILayout.Width(65))){CloseWorldAtlas();EndAtlasUiChange();}GUILayout.EndHorizontal();
        GUILayout.Space(8);GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(205));
        if(GUILayout.Toggle(atlasKind=="world","World overview",GUI.skin.button)&&atlasKind!="world")OpenAtlas();
        if(GUILayout.Toggle(atlasKind=="view","Map settings",GUI.skin.button)&&atlasKind!="view")OpenAtlas("view");
        var categories=AtlasNavigation.CategoriesFor(atlasSnapshot);
        foreach(var category in categories)
            if(GUILayout.Button(category.Name,new GUIStyle(GUI.skin.button){normal=atlasKind==category.Kind?GUI.skin.button.active:GUI.skin.button.normal})){atlasSearch="";OpenAtlas("directory:"+category.Kind);}
        if(categories.Any(c=>c.Kind==atlasKind))
        {
        GUI.SetNextControlName("AtlasSearch");string search=GUILayout.TextField(atlasSearch);atlasSearchFocused=GUI.GetNameOfFocusedControl()=="AtlasSearch";
        if(search!=atlasSearch){atlasSearch=search;RefreshAtlasDirectory();EndAtlasUiChange();}GUILayout.Label("Search names",new GUIStyle(GUI.skin.label){fontSize=11});
        bool past=GUILayout.Toggle(atlasPast,"Include historical records");if(past!=atlasPast){atlasPast=past;RefreshAtlasDirectory();EndAtlasUiChange();}
        int directoryPages=Math.Max(1,(atlasDirectory.Length+199)/200);
        if(directoryPages>1){GUILayout.BeginHorizontal();GUI.enabled=atlasDirectoryPage>0;if(GUILayout.Button("‹",GUILayout.Width(32))){atlasDirectoryPage--;atlasDirectoryScroll=Vector2.zero;EndAtlasUiChange();}GUI.enabled=true;GUILayout.Label((atlasDirectoryPage+1)+" / "+directoryPages,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});GUI.enabled=atlasDirectoryPage<directoryPages-1;if(GUILayout.Button("›",GUILayout.Width(32))){atlasDirectoryPage++;atlasDirectoryScroll=Vector2.zero;EndAtlasUiChange();}GUI.enabled=true;GUILayout.EndHorizontal();}
        atlasDirectoryScroll=GUILayout.BeginScrollView(atlasDirectoryScroll);
        foreach(var item in atlasDirectory.Skip(atlasDirectoryPage*200).Take(200)){string name=item.Name+(atlasRepeatedNames.Contains(item.Name)&&item.Context.Length>0?"\n"+item.Context:"");if(GUILayout.Button(new GUIContent(name,item.Context+" · "+item.Detail),new GUIStyle(GUI.skin.button){fontSize=13,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(8,8,5,5)}))AtlasNavigate(item);}
        if(atlasDirectory.Length==0)GUILayout.Label("No matching records.");GUILayout.EndScrollView();
        }
        else{atlasSearchFocused=false;GUILayout.FlexibleSpace();}
        GUILayout.EndVertical();
        GUILayout.Space(20);GUILayout.BeginVertical();GUILayout.BeginHorizontal();GUI.enabled=atlasBack.Count>0;
        if(GUILayout.Button("Back",GUILayout.Width(60))){var prior=atlasBack.Pop();LoadAtlasArticle(prior.Item1,prior.Item2);RefreshAtlasDirectory();EndAtlasUiChange();}GUI.enabled=true;
        if(GUILayout.Button("Copy article",GUILayout.Width(105)))GUIUtility.systemCopyBuffer=HistoryAtlas.PlainText(atlasDocument,atlasTimeline);
        GUILayout.FlexibleSpace();GUI.enabled=atlasDocument.Cell>=0;
        if(GUILayout.Button("Show on map",GUILayout.Width(116)))
        {
            selectedCell=atlasDocument.Cell;atlasOpen=false;atlasSearchFocused=false;detailsOpen=true;mapSettings=false;
            var position=SocietyView.Location(world,selectedCell);float lon=(float)(Math.Atan2(position.Y,position.X)*180/Math.PI),lat=(float)(Math.Asin(position.Z)*180/Math.PI);
            if(projection==Projection.Globe){yaw=90-lon;pitch=-lat;}else{centerLongitude=lon;mapPan.y=(float)(Math.Asin(position.Z)*2/Math.PI);}
            UpdateHumanPalette();ApplyCachedDisplay();EndAtlasUiChange();
        }
        GUI.enabled=true;GUILayout.EndHorizontal();
        GUILayout.Label(atlasDocument.Title,new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold});GUILayout.Label(atlasDocument.Subtitle,new GUIStyle(GUI.skin.label){fontSize=12});
        if(atlasKind=="view"||AtlasNavigation.IsDirectory(atlasDocument.Kind))
        {
            atlasArticleScroll=GUILayout.BeginScrollView(atlasArticleScroll);
            if(atlasKind=="view")DrawSocietyMapSettings();
            else
            {
                var columns=AtlasTables.Columns(atlasKind,atlasSnapshot);AtlasParagraph(atlasDirectory.Length.ToString("N0")+" records · click a heading to sort",11);
                float ColumnWidth(string column)=>column=="Name"?170:column=="Government"||column=="Way of life"||column=="Role"||column=="Cultural context"||column=="Parent heritage"||column=="Parent communication"||column=="Parent tradition"?170:115;
                GUILayout.BeginHorizontal();foreach(string column in columns)if(GUILayout.Button(column+(atlasSort==column?(atlasSortDescending?" ▾":" ▴"):" ↕"),GUILayout.Width(ColumnWidth(column)))){if(atlasSort==column)atlasSortDescending=!atlasSortDescending;else{atlasSort=column;atlasSortDescending=column!="Name";}atlasDirectory=AtlasTables.Sort(atlasDirectory,atlasSort,atlasSortDescending);atlasDirectoryPage=0;EndAtlasUiChange();}GUILayout.EndHorizontal();
                foreach(var entry in atlasDirectory.Skip(atlasDirectoryPage*200).Take(200))
                {GUILayout.BeginHorizontal();foreach(string column in columns){if(column=="Name"){if(GUILayout.Button(entry.Name,GUILayout.Width(ColumnWidth(column))))AtlasNavigate(entry);}else GUILayout.Label(AtlasTables.Cell(entry,column).Text,new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12},GUILayout.Width(ColumnWidth(column)));}GUILayout.EndHorizontal();}
                if(atlasDirectory.Length==0)AtlasParagraph("No matching records.",14);
            }
            GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.EndArea();return;
        }
        // A narrow table of contents keeps each deep section one click away.
        var allContents=new[]{"Overview"}.Concat(atlasDocument.Sections.Select(s=>s.Title)).ToArray();
        var groups=AtlasNavigation.Groups.Where(g=>allContents.Any(t=>AtlasNavigation.Group(t)==g)).ToArray();
        int activeGroup=Math.Max(0,Array.IndexOf(groups,AtlasNavigation.Group(atlasSection)));
        int chosenGroup=GUILayout.SelectionGrid(activeGroup,groups,groups.Length,new GUIStyle(GUI.skin.button){fontSize=11,padding=new RectOffset(4,4,7,7)});
        if(chosenGroup!=activeGroup){atlasSection=allContents.First(t=>AtlasNavigation.Group(t)==groups[chosenGroup]);atlasArticleScroll=Vector2.zero;EndAtlasUiChange();}
        var contents=allContents.Where(t=>AtlasNavigation.Group(t)==groups[activeGroup]).ToArray();
        int selected=Math.Max(0,Array.IndexOf(contents,atlasSection));int choice=selected;
        if(contents.Length>1)choice=GUILayout.SelectionGrid(selected,contents.Select(AtlasTabCaption).ToArray(),Mathf.Max(2,Mathf.Min(4,(int)((width-260)/170))),new GUIStyle(GUI.skin.button){fontSize=12,padding=new RectOffset(5,5,5,5),wordWrap=false});
        if(choice!=selected){atlasSection=contents[choice];atlasArticleScroll=Vector2.zero;EndAtlasUiChange();}
        GUILayout.Space(8);atlasArticleScroll=GUILayout.BeginScrollView(atlasArticleScroll);
        if(atlasSection=="Overview")
        {
            AtlasParagraph(AtlasDashboard.Lead(atlasDocument),15);AtlasFactTiles(AtlasDashboard.HeadlineFacts(atlasDocument),3);
            var panels=AtlasDashboard.Panels(atlasDocument);
            for(int i=0;i<panels.Length;i++)
            {
                var visual=AtlasDashboard.Visual(panels[i]);bool wide=visual==AtlasVisual.Network||visual==AtlasVisual.Appearance||visual==AtlasVisual.Technology;
                if(wide){GUILayout.BeginVertical(GUI.skin.box);DrawAtlasExhibit(panels[i],true,width-300);GUILayout.EndVertical();}
                else
                {
                    GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width((width-312)/2));DrawAtlasExhibit(panels[i],true,(width-328)/2);GUILayout.EndVertical();
                    if(i+1<panels.Length){var next=AtlasDashboard.Visual(panels[i+1]);if(next!=AtlasVisual.Network&&next!=AtlasVisual.Appearance&&next!=AtlasVisual.Technology){i++;GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width((width-312)/2));DrawAtlasExhibit(panels[i],true,(width-328)/2);GUILayout.EndVertical();}}
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(10);
            }
            DrawAtlasContext("overview","Overview context & definitions",atlasDocument.Introduction+"\n"+atlasDocument.Note);
            if(atlasDocument.Habitat!=null&&GUILayout.Button("Local landscape"))atlasLandscape=!atlasLandscape;
            if(atlasLandscape)DrawHabitatPortrait(atlasDocument.Habitat);
        }
        else
        {
            var section=atlasDocument.Sections.First(s=>s.Title==atlasSection);DrawAtlasExhibit(section,false,width-300);
        }
        GUILayout.Space(20);GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.EndArea();
    }
    void AtlasParagraph(string text,int size){if(!string.IsNullOrEmpty(text))GUILayout.Label(text,new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,padding=new RectOffset(0,8,3,3)});}
    void DrawCompactAtlasResources(AtlasSection section)
    {
        for(int k=0;k<section.Links.Length;k+=2)
        {
            GUILayout.BeginHorizontal();
            for(int column=0;column<2;column++)
            {
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                if(k+column<section.Links.Length)
                {
                    var link=section.Links[k+column];AtlasParagraph(link.Name,13);AtlasParagraph(link.Detail,11);
                    var bar=GUILayoutUtility.GetRect(10,5,GUILayout.ExpandWidth(true));GUI.color=new Color(.18f,.25f,.29f);GUI.DrawTexture(bar,Texture2D.whiteTexture);
                    bar.width*=Mathf.Clamp01(link.Value/1000f);GUI.color=new Color(.77f,.69f,.46f);GUI.DrawTexture(bar,Texture2D.whiteTexture);GUI.color=Color.white;
                }
                GUILayout.EndVertical();if(column==0)GUILayout.Space(18);
            }
            GUILayout.EndHorizontal();GUILayout.Space(3);
        }
    }
    void DrawTechnicalBar(string label,float percent)
    {
        GUILayout.BeginHorizontal();GUILayout.Label(label+" "+percent.ToString("0.0")+"%",GUILayout.Width(230));var bar=GUILayoutUtility.GetRect(60,10,GUILayout.ExpandWidth(true));GUI.color=new Color(.18f,.25f,.29f);GUI.DrawTexture(bar,Texture2D.whiteTexture);bar.width*=Mathf.Clamp01(percent/100);GUI.color=new Color(.77f,.69f,.46f);GUI.DrawTexture(bar,Texture2D.whiteTexture);GUI.color=Color.white;GUILayout.EndHorizontal();
    }
    string AtlasTabCaption(string title)
    {
        switch(title){case "Overlords and dependent courts":return "Dependent courts";case "People, court, and belief":return "People & belief";case "States associated with these people":return "Associated states";case "Origins, language, and belief":return "Origins & language";case "Ways of making a living":return "Livelihoods";case "Institutions and pressures":return "Institutions";case "Diplomatic neighborhood":return "Diplomacy";case "Food and everyday security":return "Food security";case "Provisioning and resilience":return "Food & resilience";case "Belief and political patronage":return "Belief & courts";case "Connections and fault lines":return "World relationships";default:return SocietyView.Short(title,24);}
    }
    void AtlasFacts(AtlasFact[] facts)
    {
        foreach(var fact in facts){GUILayout.BeginHorizontal();GUILayout.Label(fact.Label,new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold},GUILayout.Width(190));if(fact.Swatch.Length>0&&ColorUtility.TryParseHtmlString(fact.Swatch,out var tint)){var swatch=GUILayoutUtility.GetRect(30,20,GUILayout.Width(30));GUI.color=tint;GUI.DrawTexture(swatch,Texture2D.whiteTexture);GUI.color=Color.white;GUILayout.Space(8);}GUILayout.Label(fact.Value,new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true});GUILayout.EndHorizontal();}
    }
    void AtlasFactTiles(AtlasFact[] facts,int columns)
    {
        for(int i=0;i<facts.Length;i+=columns){GUILayout.BeginHorizontal();for(int j=i;j<Math.Min(facts.Length,i+columns);j++){GUILayout.BeginVertical(GUI.skin.box,GUILayout.ExpandWidth(true));GUILayout.Label(facts[j].Label,new GUIStyle(GUI.skin.label){fontSize=10,wordWrap=true});GUILayout.Label(AtlasDashboard.FactValue(facts[j]),new GUIStyle(GUI.skin.label){fontSize=AtlasDashboard.FactValue(facts[j]).Length>24?13:16,wordWrap=true,fontStyle=FontStyle.Bold});if(AtlasDashboard.FactContext(facts[j]).Length>0)AtlasParagraph(AtlasDashboard.FactContext(facts[j]),10);GUILayout.EndVertical();}GUILayout.EndHorizontal();}
    }
    void DrawAtlasDiplomacy(AtlasSection section,float width)
    {
        var links=section.Links.Take(8).ToArray();if(links.Length==0)return;
        var r=GUILayoutUtility.GetRect(100,Mathf.Max(130,links.Length*43+30),GUILayout.ExpandWidth(true));float startX=r.x+Mathf.Min(125,width*.2f),endX=r.x+Mathf.Min(310,width*.48f),center=r.y+r.height/2;
        GUI.Label(new Rect(r.x,center-32,160,25),SocietyView.Short(atlasDocument.Title,22));
        for(int i=0;i<links.Length;i++)
        {
            float y=r.y+25+i*43;ColorUtility.TryParseHtmlString(section.Diagram?AtlasDiagram.RelationColor(links[i].Detail):AtlasDashboard.Color(i),out var color);DrawAtlasLine(new Vector2(startX,center),new Vector2(endX,y),color);
            GUI.color=color;GUI.DrawTexture(new Rect(endX-3,y-3,6,6),Texture2D.whiteTexture);GUI.color=Color.white;
            if(GUI.Button(new Rect(endX+10,y-15,Mathf.Max(80,r.xMax-endX-10),35),new GUIContent(links[i].Name+"\n"+SocietyView.Short(links[i].Detail,60),links[i].Detail),new GUIStyle(GUI.skin.button){fontSize=11,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(4,4,3,3)}))AtlasNavigate(links[i]);
        }
        AtlasParagraph(!section.Diagram?"Recorded family, heritage or institutional connections.":section.Title.StartsWith("Empire",StringComparison.Ordinal)?"Lines connect overarching and constituent governments, not geographic positions.":"Red: war · Blue: alliance · Gold: trade · Gray: contact",11);
    }
    void DrawAtlasLine(Vector2 start,Vector2 end,Color color)
    {
        var old=GUI.matrix;var delta=end-start;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,start);GUI.DrawTexture(new Rect(start.x,start.y,delta.magnitude,2),Texture2D.whiteTexture);GUI.matrix=old;GUI.color=Color.white;
    }
}

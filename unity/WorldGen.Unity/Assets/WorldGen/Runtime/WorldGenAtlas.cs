using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    bool atlasOpen,atlasPast,atlasSearchFocused;
    string atlasKind="state",atlasSearch="",atlasSection="Overview";
    Vector2 atlasDirectoryScroll,atlasArticleScroll;
    AtlasDocument atlasDocument;
    AtlasTimeline atlasTimeline;
    AtlasLink[] atlasDirectory=Array.Empty<AtlasLink>();
    HashSet<string> atlasRepeatedNames=new HashSet<string>();
    int atlasDirectoryPage;
    PrehistoryState atlasSnapshot;
    readonly Stack<Tuple<string,int>> atlasBack=new Stack<Tuple<string,int>>();
    void OpenAtlas(string kind="world",int id=-1)
    {
        if(atlasOpen&&atlasDocument!=null)atlasBack.Push(Tuple.Create(atlasDocument.Kind,atlasDocument.Id));
        else atlasBack.Clear();
        atlasOpen=true;atlasSnapshot=world?.Prehistory;LoadAtlasArticle(kind,id);RefreshAtlasDirectory();EndAtlasUiChange();
    }
    void LoadAtlasArticle(string kind,int id)
    {
        atlasDocument=HistoryAtlas.Read(atlasSnapshot,kind,id,world);atlasSection="Overview";atlasArticleScroll=Vector2.zero;atlasTimeline=null;
    }
    void RefreshAtlasDirectory(){atlasDirectory=HistoryAtlas.Directory(atlasSnapshot,atlasKind,atlasSearch,atlasPast);atlasRepeatedNames=new HashSet<string>(atlasDirectory.GroupBy(x=>x.Name).Where(g=>g.Count()>1).Select(g=>g.Key));atlasDirectoryScroll=Vector2.zero;atlasDirectoryPage=0;}
    void AtlasNavigate(AtlasLink link){if(string.IsNullOrEmpty(link.Kind))return;atlasBack.Push(Tuple.Create(atlasDocument.Kind,atlasDocument.Id));LoadAtlasArticle(link.Kind,link.Id);EndAtlasUiChange();}
    void EndAtlasUiChange(){if(Event.current!=null&&Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint)GUIUtility.ExitGUI();}
    void DrawAtlas()
    {
        if(!atlasOpen)return;
        float width=Mathf.Min(1120,Screen.width-32),height=Mathf.Min(800,Screen.height-100);var panel=new Rect((Screen.width-width)/2,58,width,height);
        GUI.Box(panel,"");GUILayout.BeginArea(new Rect(panel.x+16,panel.y+12,panel.width-32,panel.height-24));
        GUILayout.BeginHorizontal();GUILayout.Label("WORLD ATLAS",new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold});
        GUILayout.Label("Playback paused · "+SimulationClockView.Year(atlasSnapshot,atlasSnapshot?.Years??0),GUILayout.Width(200));
        if(GUILayout.Button("Refresh",GUILayout.Width(80))){atlasSnapshot=world?.Prehistory;LoadAtlasArticle(atlasDocument.Kind,atlasDocument.Id);RefreshAtlasDirectory();EndAtlasUiChange();}
        if(GUILayout.Button("Close",GUILayout.Width(65))){atlasOpen=false;atlasSearchFocused=false;EndAtlasUiChange();}GUILayout.EndHorizontal();
        GUILayout.Space(8);GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(205));
        if(GUILayout.Button("World overview")){atlasBack.Push(Tuple.Create(atlasDocument.Kind,atlasDocument.Id));LoadAtlasArticle("world",-1);EndAtlasUiChange();}
        foreach(var category in new[]{("state","States"),("city","Cities"),("people","Peoples"),("religion","Religions"),("ruler","Rulers")})
            if(GUILayout.Toggle(atlasKind==category.Item1,category.Item2,GUI.skin.button)&&atlasKind!=category.Item1){atlasKind=category.Item1;RefreshAtlasDirectory();EndAtlasUiChange();}
        GUI.SetNextControlName("AtlasSearch");string search=GUILayout.TextField(atlasSearch);atlasSearchFocused=GUI.GetNameOfFocusedControl()=="AtlasSearch";
        if(search!=atlasSearch){atlasSearch=search;RefreshAtlasDirectory();EndAtlasUiChange();}GUILayout.Label("Search names",new GUIStyle(GUI.skin.label){fontSize=11});
        bool past=GUILayout.Toggle(atlasPast,"Include historical records");if(past!=atlasPast){atlasPast=past;RefreshAtlasDirectory();EndAtlasUiChange();}
        int directoryPages=Math.Max(1,(atlasDirectory.Length+199)/200);
        if(directoryPages>1){GUILayout.BeginHorizontal();GUI.enabled=atlasDirectoryPage>0;if(GUILayout.Button("‹",GUILayout.Width(32))){atlasDirectoryPage--;atlasDirectoryScroll=Vector2.zero;EndAtlasUiChange();}GUI.enabled=true;GUILayout.Label((atlasDirectoryPage+1)+" / "+directoryPages,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});GUI.enabled=atlasDirectoryPage<directoryPages-1;if(GUILayout.Button("›",GUILayout.Width(32))){atlasDirectoryPage++;atlasDirectoryScroll=Vector2.zero;EndAtlasUiChange();}GUI.enabled=true;GUILayout.EndHorizontal();}
        atlasDirectoryScroll=GUILayout.BeginScrollView(atlasDirectoryScroll);
        foreach(var item in atlasDirectory.Skip(atlasDirectoryPage*200).Take(200)){string name=item.Name+(atlasRepeatedNames.Contains(item.Name)&&item.Context.Length>0?"\n"+item.Context:"");if(GUILayout.Button(new GUIContent(name,item.Context+" · "+item.Detail),new GUIStyle(GUI.skin.button){fontSize=13,alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(8,8,5,5)}))AtlasNavigate(item);}
        if(atlasDirectory.Length==0)GUILayout.Label("No matching records.");GUILayout.EndScrollView();GUILayout.EndVertical();
        GUILayout.Space(20);GUILayout.BeginVertical();GUILayout.BeginHorizontal();GUI.enabled=atlasBack.Count>0;
        if(GUILayout.Button("Back",GUILayout.Width(60))){var prior=atlasBack.Pop();LoadAtlasArticle(prior.Item1,prior.Item2);EndAtlasUiChange();}GUI.enabled=true;
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
        // A narrow table of contents keeps each deep section one click away.
        var contents=new[]{"Overview"}.Concat(atlasDocument.Sections.Select(s=>s.Title)).ToArray();
        int selected=Math.Max(0,Array.IndexOf(contents,atlasSection));int choice=GUILayout.SelectionGrid(selected,contents.Select(AtlasTabCaption).ToArray(),Mathf.Max(2,Mathf.Min(4,(int)((width-260)/170))),new GUIStyle(GUI.skin.button){fontSize=12,padding=new RectOffset(5,5,5,5),wordWrap=false});
        if(choice!=selected){atlasSection=contents[choice];atlasArticleScroll=Vector2.zero;EndAtlasUiChange();}
        GUILayout.Space(8);atlasArticleScroll=GUILayout.BeginScrollView(atlasArticleScroll);
        if(atlasSection=="Overview")
        {
            AtlasParagraph(atlasDocument.Introduction,16);GUILayout.Space(12);
            DrawHabitatPortrait(atlasDocument.Habitat);
            AtlasFacts(atlasDocument.Facts);
            GUILayout.Space(14);AtlasParagraph("Explore the sections above for connections, livelihoods, institutions, and recorded events. Names in the article open related entries.",13);
        }
        else
        {
            var section=atlasDocument.Sections.First(s=>s.Title==atlasSection);GUILayout.Label(section.Title,new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold});AtlasParagraph(section.Text,14);AtlasFacts(section.Facts);
            if(section.Diagram)DrawAtlasDiplomacy(section,width-300);
            long maximum=section.Unit=="/ 1,000"?1000:Math.Max(1,section.Links.Select(x=>x.Value).DefaultIfEmpty(1).Max());
            foreach(var link in section.Links)
            {
                GUILayout.Space(5);if(!string.IsNullOrEmpty(link.Kind)){if(GUILayout.Button(link.Name,new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(8,8,5,5)}))AtlasNavigate(link);}else GUILayout.Label(link.Name);
                if(!string.IsNullOrEmpty(link.Detail))AtlasParagraph(link.Detail,13);
                if(section.Bars)
                {
                    var bar=GUILayoutUtility.GetRect(10,6,GUILayout.ExpandWidth(true));GUI.color=new Color(.18f,.25f,.29f);GUI.DrawTexture(bar,Texture2D.whiteTexture);bar.width*=Mathf.Clamp01((float)link.Value/maximum);var rgb=SocietyView.CivilizationColor(link.Id);GUI.color=link.Kind=="state"||link.Kind=="people"||link.Kind=="religion"?new Color((float)rgb.X,(float)rgb.Y,(float)rgb.Z):new Color(.77f,.69f,.46f);GUI.DrawTexture(bar,Texture2D.whiteTexture);GUI.color=Color.white;
                }
            }
            var points=section.Timeline;
            if(section.Title=="Recorded history")
            {
                if(atlasTimeline==null)atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id);
                GUILayout.BeginHorizontal();bool routine=GUILayout.Toggle(atlasTimeline.IncludeRoutine,"Include routine records");
                if(routine!=atlasTimeline.IncludeRoutine){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,0,routine);EndAtlasUiChange();}
                GUI.enabled=atlasTimeline.Offset>0;
                if(GUILayout.Button("Newer",GUILayout.Width(70))){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,atlasTimeline.Offset-40,atlasTimeline.IncludeRoutine);EndAtlasUiChange();}
                GUI.enabled=atlasTimeline.Offset+atlasTimeline.Records.Length<atlasTimeline.Total;
                if(GUILayout.Button("Older",GUILayout.Width(70))){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,atlasTimeline.Offset+40,atlasTimeline.IncludeRoutine);EndAtlasUiChange();}
                GUI.enabled=true;GUILayout.EndHorizontal();
                AtlasParagraph(atlasTimeline.Total==0?"No matching records.":$"Records {atlasTimeline.Offset+1:N0}–{atlasTimeline.Offset+atlasTimeline.Records.Length:N0} of {atlasTimeline.Total:N0}",12);points=atlasTimeline.Records;
            }
            foreach(var point in points)
            {
                GUILayout.Space(10);GUILayout.Label(point.Label,new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold});AtlasParagraph(point.Value,15);
                foreach(var related in point.Related.Where(x=>x.Kind!=atlasDocument.Kind||x.Id!=atlasDocument.Id))if(GUILayout.Button(new GUIContent(related.Name,related.Context),new GUIStyle(GUI.skin.button){fontSize=12,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(5,5,3,3)}))AtlasNavigate(related);
            }
            if(section.Links.Length==0&&section.Timeline.Length==0&&section.Facts.Length==0&&section.Text.Length==0)AtlasParagraph("No records yet.",14);
        }
        GUILayout.Space(20);AtlasParagraph(atlasDocument.Note,11);GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.EndArea();
    }
    void AtlasParagraph(string text,int size){if(!string.IsNullOrEmpty(text))GUILayout.Label(text,new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,padding=new RectOffset(0,8,3,3)});}
    string AtlasTabCaption(string title)
    {
        switch(title){case "Overlords and dependent courts":return "Dependent courts";case "People, court, and belief":return "People & belief";case "States associated with these people":return "Associated states";case "Origins, language, and belief":return "Origins & language";case "Ways of making a living":return "Livelihoods";case "Institutions and pressures":return "Institutions";case "Diplomatic neighborhood":return "Diplomacy";case "Food and everyday security":return "Food security";case "Provisioning and resilience":return "Food & resilience";case "Belief and political patronage":return "Belief & courts";case "Connections and fault lines":return "World relationships";default:return SocietyView.Short(title,24);}
    }
    void AtlasFacts(AtlasFact[] facts)
    {
        foreach(var fact in facts){GUILayout.BeginHorizontal();GUILayout.Label(fact.Label,new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold},GUILayout.Width(190));GUILayout.Label(fact.Value,new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true});GUILayout.EndHorizontal();}
    }
    void DrawAtlasDiplomacy(AtlasSection section,float width)
    {
        var links=section.Links.Take(8).ToArray();if(links.Length==0)return;
        var r=GUILayoutUtility.GetRect(100,Mathf.Max(130,links.Length*43+30),GUILayout.ExpandWidth(true));float startX=r.x+Mathf.Min(125,width*.2f),endX=r.x+Mathf.Min(310,width*.48f),center=r.y+r.height/2;
        GUI.Label(new Rect(r.x,center-32,160,25),SocietyView.Short(atlasDocument.Title,22));
        for(int i=0;i<links.Length;i++)
        {
            float y=r.y+25+i*43;ColorUtility.TryParseHtmlString(AtlasDiagram.RelationColor(links[i].Detail),out var color);DrawAtlasLine(new Vector2(startX,center),new Vector2(endX,y),color);
            GUI.color=color;GUI.DrawTexture(new Rect(endX-3,y-3,6,6),Texture2D.whiteTexture);GUI.color=Color.white;
            if(GUI.Button(new Rect(endX+10,y-15,Mathf.Max(80,r.xMax-endX-10),30),links[i].Name,new GUIStyle(GUI.skin.button){fontSize=12,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(4,4,3,3)}))AtlasNavigate(links[i]);
        }
        AtlasParagraph("Red: war · Blue: alliance · Gold: trade · Gray: contact",12);
    }
    void DrawAtlasLine(Vector2 start,Vector2 end,Color color)
    {
        var old=GUI.matrix;var delta=end-start;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,start);GUI.DrawTexture(new Rect(start.x,start.y,delta.magnitude,2),Texture2D.whiteTexture);GUI.matrix=old;GUI.color=Color.white;
    }
}

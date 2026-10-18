using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
public sealed partial class WorldGenRuntime
{
    readonly HashSet<string> atlasContext=new HashSet<string>();bool atlasLandscape;int atlasProjectFilter;
    Texture2D atlasCircle;
    void AtlasSectionJump(string title){atlasSection=title;atlasArticleScroll=Vector2.zero;EndAtlasUiChange();}
    void DrawAtlasContext(string key,string label,string text)
    {
        if(string.IsNullOrEmpty(text))return;bool open=atlasContext.Contains(key),next=GUILayout.Toggle(open,(open?"▾ ":"▸ ")+label,GUI.skin.button);
        if(next!=open){if(next)atlasContext.Add(key);else atlasContext.Remove(key);EndAtlasUiChange();}if(open)AtlasParagraph(text,12);
    }
    void DrawAtlasExhibit(AtlasSection section,bool compact,float width)
    {
        var visual=AtlasDashboard.Visual(section);GUILayout.Space(6);
        if(compact){if(GUILayout.Button(section.Title+"  ↗",new GUIStyle(GUI.skin.button){fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,wordWrap=true}))AtlasSectionJump(section.Title);}
        else AtlasParagraph(section.Title,20);
        AtlasParagraph(AtlasDashboard.Caption(section),10);
        if(visual==AtlasVisual.Appearance&&atlasDocument.Appearance?.Available==true)DrawAtlasPeople(atlasDocument.Appearance,width,compact);
        if(section.Facts.Length>0)AtlasFactTiles(section.Facts.Take(compact?4:int.MaxValue).ToArray(),width<420?2:3);
        if(visual==AtlasVisual.Network&&section.Links.Any(x=>x.Kind.Length>0))DrawAtlasDiplomacy(section,width);
        if(visual==AtlasVisual.Composition)
        {
            long sum=section.Links.Sum(x=>Math.Max(0,x.Value));if(sum>0){var strip=GUILayoutUtility.GetRect(10,16,GUILayout.ExpandWidth(true));float x=strip.x;for(int i=0;i<section.Links.Length;i++){float part=strip.width*Math.Max(0,section.Links[i].Value)/sum;ColorUtility.TryParseHtmlString(AtlasDashboard.Color(i),out var color);GUI.color=color;GUI.DrawTexture(new Rect(x,strip.y,Mathf.Max(0,part-1),strip.height),Texture2D.whiteTexture);x+=part;}GUI.color=Color.white;}
        }
        if(visual!=AtlasVisual.Network||!compact)
        {
            long maximum=AtlasDashboard.Maximum(section);int index=0;
            foreach(var link in section.Links.Take(compact?6:int.MaxValue))
            {
                GUILayout.BeginHorizontal();if(link.Kind.Length>0){if(GUILayout.Button(new GUIContent(link.Name,link.Detail+" · "+link.Context),new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=12}))AtlasNavigate(link);}else AtlasParagraph(link.Name,12);
                if(section.Bars)GUILayout.Label(AtlasDashboard.BarLabel(section,link),new GUIStyle(GUI.skin.label){fontSize=11,alignment=TextAnchor.MiddleRight,wordWrap=false},GUILayout.Width(Mathf.Min(140,width*.45f)));GUILayout.EndHorizontal();
                if(section.Bars&&visual!=AtlasVisual.Composition){var bar=GUILayoutUtility.GetRect(10,6,GUILayout.ExpandWidth(true));GUI.color=new Color(.17f,.25f,.30f);GUI.DrawTexture(bar,Texture2D.whiteTexture);ColorUtility.TryParseHtmlString(AtlasDashboard.Color(index),out var color);GUI.color=color;bar.width*=Mathf.Clamp01(link.Value/(float)maximum);GUI.DrawTexture(bar,Texture2D.whiteTexture);GUI.color=Color.white;}
                if(!compact&&link.Detail.Length>0)AtlasParagraph(link.Detail,11);index++;
            }
        }
        if(section.Technical.Length>0)
        {
            if(!compact)atlasProjectFilter=GUILayout.SelectionGrid(atlasProjectFilter,new[]{"All","Active research","Deployment pending","Established"},2);
            foreach(var project in section.Technical.Where(p=>compact||atlasProjectFilter==0||atlasProjectFilter==1&&p.AllocationBasisPoints>0||atlasProjectFilter==2&&p.Level>0&&p.DeploymentPermille<950||atlasProjectFilter==3&&p.Level>0).Take(compact?3:int.MaxValue))
            {
                GUILayout.BeginVertical(GUI.skin.box);AtlasParagraph(project.Name+" · Lv "+project.Level,15);AtlasParagraph(project.Source,11);
                DrawTechnicalBar("Research effort",project.AllocationBasisPoints/100f);DrawTechnicalBar(project.AtCeiling?"Model ceiling":"Next level",project.AtCeiling?100:project.ResearchPermille/10f);DrawTechnicalBar("Deployment",project.DeploymentPermille/10f);
                if(!compact)DrawAtlasContext("tech-"+project.Id,"Prerequisites & connections",project.Prerequisites);GUILayout.EndVertical();
            }
        }
        var points=section.Timeline;
        if(!compact&&section.Title=="Recorded history")
        {
            if(atlasTimeline==null)atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id);
            GUILayout.BeginHorizontal();bool routine=GUILayout.Toggle(atlasTimeline.IncludeRoutine,"Include routine records");if(routine!=atlasTimeline.IncludeRoutine){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,0,routine);EndAtlasUiChange();}
            GUI.enabled=atlasTimeline.Offset>0;if(GUILayout.Button("Newer",GUILayout.Width(65))){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,atlasTimeline.Offset-40,atlasTimeline.IncludeRoutine);EndAtlasUiChange();}
            GUI.enabled=atlasTimeline.Offset+atlasTimeline.Records.Length<atlasTimeline.Total;if(GUILayout.Button("Older",GUILayout.Width(65))){atlasTimeline=HistoryAtlas.Timeline(atlasSnapshot,atlasDocument.Kind,atlasDocument.Id,atlasTimeline.Offset+40,atlasTimeline.IncludeRoutine);EndAtlasUiChange();}GUI.enabled=true;GUILayout.EndHorizontal();points=atlasTimeline.Records;
            AtlasParagraph(atlasTimeline.Total==0?"No matching records.":$"Records {atlasTimeline.Offset+1:N0}–{atlasTimeline.Offset+points.Length:N0} of {atlasTimeline.Total:N0}",11);
        }
        foreach(var point in points.Take(compact?3:int.MaxValue))
        {
            GUILayout.BeginHorizontal();GUILayout.Label(point.Label,new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold,wordWrap=true},GUILayout.Width(Mathf.Min(100,width*.23f)));GUILayout.BeginVertical();AtlasParagraph(point.Value,13);
            foreach(var link in point.Related.Where(x=>x.Kind!=atlasDocument.Kind||x.Id!=atlasDocument.Id))if(GUILayout.Button(link.Name))AtlasNavigate(link);GUILayout.EndVertical();GUILayout.EndHorizontal();
        }
        if(compact){if(GUILayout.Button("Explore "+section.Title.ToLowerInvariant()+" →",new GUIStyle(GUI.skin.button){fontSize=11,alignment=TextAnchor.MiddleLeft}))AtlasSectionJump(section.Title);}
        else if(section.Text.Length>0)
        {if(section.Facts.Length+section.Links.Length+section.Timeline.Length+section.Technical.Length==0)AtlasParagraph(section.Text,14);else DrawAtlasContext(section.Title,"Context & definitions",section.Text);}
    }
    void DrawAtlasPeople(HumanAppearance profile,float width,bool compact)
    {
        if(atlasCircle==null){atlasCircle=new Texture2D(64,64,TextureFormat.RGBA32,false);for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32));atlasCircle.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(32-d)));}atlasCircle.Apply();}
        var r=GUILayoutUtility.GetRect(width,140);var samples=AtlasPortrait.Samples(profile);float part=r.width/6;
        foreach(var entry in samples.Select((p,i)=>new{P=p,I=i}))
        {
            var p=entry.P;float cx=r.x+part*(entry.I+.5f),face=30+p.FacialVariation/10000f*16,shoulder=part*(.45f+p.BodyCompactness/10000f*.25f);
            GUI.color=new Color(.34f,.47f,.53f);GUI.DrawTexture(new Rect(cx-shoulder/2,r.y+90,shoulder,45),atlasCircle);
            GUI.color=new Color((float)p.SkinColor.X,(float)p.SkinColor.Y,(float)p.SkinColor.Z);GUI.DrawTexture(new Rect(cx-face/2,r.y+24,face,65),atlasCircle);
            GUI.color=new Color((float)p.HairColor.X,(float)p.HairColor.Y,(float)p.HairColor.Z);GUI.DrawTexture(new Rect(cx-face/2-2,r.y+14,face+4,28),atlasCircle);
            GUI.color=new Color((float)p.IrisColor.X,(float)p.IrisColor.Y,(float)p.IrisColor.Z);GUI.DrawTexture(new Rect(cx-face*.23f-2,r.y+53,4,4),atlasCircle);GUI.DrawTexture(new Rect(cx+face*.23f-2,r.y+53,4,4),atlasCircle);
            GUI.color=new Color(.3f,.2f,.18f,.45f);GUI.DrawTexture(new Rect(cx-(2+p.NasalVariation/2000f)/2,r.y+63,2+p.NasalVariation/2000f,9),atlasCircle);GUI.color=Color.white;
        }
        AtlasParagraph("Illustrative individuals, not ethnic templates or facial reconstructions.",10);
        if(!compact)foreach(HumanAppearanceTrait trait in Enum.GetValues(typeof(HumanAppearanceTrait)))DrawTechnicalBar(HistoryHumanVariation.TraitName(trait),profile.Value(trait)/100f);
    }
}

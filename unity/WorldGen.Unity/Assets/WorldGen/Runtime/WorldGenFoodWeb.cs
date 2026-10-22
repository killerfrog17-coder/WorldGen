using System;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;
using WorldGen.Core;

public sealed partial class WorldGenRuntime
{
    Vector2 foodScroll;FoodWebDiagram foodGraph;World foodWorld;int foodRegion=-2,foodFocus=-1;
    HabitatPortrait foodPortrait;World foodPortraitWorld;int foodPortraitCell=-1;
    void DrawEcologyDetails(float left)
    {
        var rect=new Rect(left+16,105,Math.Max(200,Screen.width-left-32),Math.Max(200,Screen.height-185));
        GUI.Box(rect,"");GUILayout.BeginArea(new Rect(rect.x+12,rect.y+8,rect.width-24,35));
        if(GUILayout.Button("Close",GUILayout.Width(75)))expandedInspection=false;GUILayout.EndArea();
        float textWidth=Math.Max(180,rect.width*.40f);
        GUILayout.BeginArea(new Rect(rect.x+12,rect.y+48,textWidth-24,rect.height-60));
        inspectionScroll=GUILayout.BeginScrollView(inspectionScroll);
        if(foodPortraitWorld!=displayedWorld||foodPortraitCell!=selectedCell){foodPortraitWorld=displayedWorld;foodPortraitCell=selectedCell;foodPortrait=HabitatPortrait.At(displayedWorld,selectedCell);}
        DrawHabitatPortrait(foodPortrait,false);
        string text=EcologyView.Summary(displayedWorld,selectedCell)+"\n\n"+EcologyView.Describe(displayedWorld,selectedCell);int cut=text.IndexOf("\n\nFOOD WEB",StringComparison.Ordinal);GUILayout.Label(cut<0?text:text.Substring(0,cut),new GUIStyle(GUI.skin.label){wordWrap=true});GUILayout.EndScrollView();GUILayout.EndArea();
        int region=displayedWorld.Ecology.Region[selectedCell];
        if(foodGraph==null || foodWorld!=displayedWorld || foodRegion!=region){foodWorld=displayedWorld;foodRegion=region;foodGraph=FoodWebDiagram.Build(displayedWorld,selectedCell);foodScroll=Vector2.zero;foodFocus=-1;}
        var area=new Rect(rect.x+textWidth,rect.y+48,rect.width-textWidth-12,rect.height-60);
        FoodWebGui.Draw(area,foodGraph,ref foodScroll,ref foodFocus);
    }
}

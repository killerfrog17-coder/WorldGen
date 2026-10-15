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
        foodScroll=GUI.BeginScrollView(area,foodScroll,new Rect(0,0,foodGraph.Width,foodGraph.Height));
        GUI.Label(new Rect(20,5,700,30),"Food → consumer · click a species to highlight its connections");
        var prior=GUI.color;
        void Line(Vector2 a,Vector2 b,Color color)
        {
            var delta=b-a;var matrix=GUI.matrix;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,delta.magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=prior;
        }
        foreach(var link in foodGraph.Links)
        {
            var a=foodGraph.Nodes.First(n=>n.Id==link.From);var b=foodGraph.Nodes.First(n=>n.Id==link.To);
            bool highlight=foodFocus==link.From||foodFocus==link.To;var color=highlight?new Color(1,.82f,.4f):new Color(.48f,.65f,.7f,foodFocus<0?.35f:.10f);
            var from=new Vector2((float)a.X+160,(float)a.Y+15);var to=new Vector2((float)b.X,(float)b.Y+15);Line(from,to,color);
            var along=(to-from).normalized;var side=new Vector2(-along.y,along.x);Line(to,to-along*7+side*4,color);Line(to,to-along*7-side*4,color);
        }
        foreach(var node in foodGraph.Nodes)
        {
            GUI.backgroundColor=node.Id==foodFocus?new Color(1,.8f,.35f):node.Basal?new Color(.6f,1,.65f):Color.white;
            if(GUI.Button(new Rect((float)node.X,(float)node.Y,160,32),new GUIContent(FoodWebDiagram.ShortName(node.Name),node.Name),new GUIStyle(GUI.skin.button){fontSize=11,wordWrap=false}))foodFocus=foodFocus==node.Id?-1:node.Id;
        }
        GUI.backgroundColor=Color.white;GUI.color=prior;GUI.EndScrollView();
    }
}

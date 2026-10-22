using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using WorldGen.Presentation;

/// <summary>The same interactive food-web renderer for simulation and exploration.</summary>
static class FoodWebGui
{
    static readonly ConditionalWeakTable<FoodWebDiagram,Dictionary<int,FoodWebDiagram.Node>> nodes=new ConditionalWeakTable<FoodWebDiagram,Dictionary<int,FoodWebDiagram.Node>>();
    public static void Draw(Rect area,FoodWebDiagram graph,ref Vector2 scroll,ref int focus)
    {
        var lookup=nodes.GetValue(graph,g=>{var result=new Dictionary<int,FoodWebDiagram.Node>();foreach(var n in g.Nodes)result[n.Id]=n;return result;});
        scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,graph.Width,graph.Height));
        GUI.Label(new Rect(20,5,700,30),"Food → consumer · click a species to highlight its connections");
        var prior=GUI.color;
        void Line(Vector2 a,Vector2 b,Color color)
        {
            var delta=b-a;var matrix=GUI.matrix;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,delta.magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=prior;
        }
        foreach(var link in graph.Links)
        {
            var a=lookup[link.From];var b=lookup[link.To];bool highlight=focus==link.From||focus==link.To;
            var color=highlight?new Color(1,.82f,.4f):new Color(.48f,.65f,.7f,focus<0?.35f:.10f);
            var from=new Vector2((float)a.X+160,(float)a.Y+15);var to=new Vector2((float)b.X,(float)b.Y+15);Line(from,to,color);
            var along=(to-from).normalized;var side=new Vector2(-along.y,along.x);Line(to,to-along*7+side*4,color);Line(to,to-along*7-side*4,color);
        }
        var style=new GUIStyle(GUI.skin.button){fontSize=11,wordWrap=false};
        foreach(var node in graph.Nodes)
        {
            GUI.backgroundColor=node.Id==focus?new Color(1,.8f,.35f):node.Basal?new Color(.6f,1,.65f):Color.white;
            if(GUI.Button(new Rect((float)node.X,(float)node.Y,160,32),new GUIContent(FoodWebDiagram.ShortName(node.Name),node.Name),style))focus=focus==node.Id?-1:node.Id;
        }
        GUI.backgroundColor=Color.white;GUI.color=prior;GUI.EndScrollView();
    }
}

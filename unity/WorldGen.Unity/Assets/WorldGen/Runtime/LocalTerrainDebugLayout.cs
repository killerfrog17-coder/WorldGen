using System;
using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    readonly string[] debugCorners={"","","",""};readonly Rect[] debugCornerRects=new Rect[4];
    static readonly string[] DebugHeadings={"POSITION & GROUND","VEGETATION","WEATHER & SKY","TERRAIN & SYSTEM"};
    string debugGround="";GUIStyle debugLeft,debugRight,debugHeadingLeft,debugHeadingRight;
    int debugLayoutStamp,debugLaidOut=-1,debugScreenWidth,debugScreenHeight;bool debugLayoutFull;
    void RefreshDebugPosition(){debugCorners[0]=debugFastDisplay+"\n\n"+targetDisplay+"\n"+debugGround;debugLayoutStamp++;}
    void CacheDebugSections()
    {
        var sections=debugDisplay.Split(new[]{"\n\nVEGETATION\n","\n\nTERRAIN LOD\n","\n\nWEATHER & SKY\n"},StringSplitOptions.None);
        debugGround=sections[0].Replace("GROUND & STREAMING\n","");
        debugCorners[1]=sections.Length>1?sections[1]:"";debugCorners[2]=sections.Length>3?sections[3]:"";debugCorners[3]=sections.Length>2?sections[2]:"";
        string reuse=null;var lines=debugCorners[1].Split('\n');
        foreach(string line in lines)if(line.StartsWith("Terrain reuse ",StringComparison.Ordinal)){reuse=line;break;}
        if(reuse!=null){debugCorners[1]=debugCorners[1].Replace("\n"+reuse,"");debugCorners[3]+="\n"+reuse;}
        if(MusicDebugText.Length>0){debugCorners[2]=debugCorners[2].Replace("\n"+MusicDebugText,"");debugCorners[3]+="\n"+MusicDebugText;}
        if(error.Length>0){debugCorners[2]=debugCorners[2].Replace("\n"+error,"");debugCorners[3]+="\n"+error;}
        RefreshDebugPosition();
    }
    void PrepareDebugLayout()
    {
        if(debugLeft==null)
        {
            debugLeft=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,richText=false,alignment=TextAnchor.UpperLeft};debugLeft.normal.textColor=Color.white;
            debugRight=new GUIStyle(debugLeft){alignment=TextAnchor.UpperRight};
            debugHeadingLeft=new GUIStyle(debugLeft){fontStyle=FontStyle.Bold};debugHeadingRight=new GUIStyle(debugHeadingLeft){alignment=TextAnchor.UpperRight};
        }
        if(debugLaidOut==debugLayoutStamp&&debugScreenWidth==Screen.width&&debugScreenHeight==Screen.height&&debugLayoutFull==debugFull)return;
        debugLaidOut=debugLayoutStamp;debugScreenWidth=Screen.width;debugScreenHeight=Screen.height;debugLayoutFull=debugFull;
        float width=Math.Max(1,Screen.width-24),height=Math.Max(1,Screen.height-24),column=Math.Max(1,Math.Min(560,(width-16)*.5f));
        if(!debugFull){debugLeft.fontSize=14;return;}
        var sizes=new float[4];
        for(int font=14;font>=10;font--)
        {
            debugLeft.fontSize=debugRight.fontSize=debugHeadingLeft.fontSize=debugHeadingRight.fontSize=font;
            for(int i=0;i<4;i++)sizes[i]=28+debugLeft.CalcHeight(new GUIContent(debugCorners[i]),Math.Max(1,column-4));
            if(sizes[0]+sizes[2]+16<=height&&sizes[1]+sizes[3]+16<=height)break;
        }
        for(int side=0;side<2;side++)
        {
            int top=side,bottom=side+2;float lower=Math.Min(sizes[bottom],height*.42f),upper=Math.Min(sizes[top],Math.Max(1,height-lower-16));
            float x=side==0?12:Screen.width-12-column;
            debugCornerRects[top]=new Rect(x,12,column,upper);
            debugCornerRects[bottom]=new Rect(x,Screen.height-12-lower,column,lower);
        }
    }
    void DrawDebugOverlay()
    {
        PrepareDebugLayout();
        if(!debugFull)
        {
            GUI.BeginGroup(new Rect(12,12,Math.Max(1,Screen.width-24),Math.Max(1,Screen.height-24)));
            DebugTextShadow(new Rect(0,0,Math.Min(560,Math.Max(1,Screen.width-24)),Math.Max(1,Screen.height-24)),debugFastDisplay,debugLeft);GUI.EndGroup();return;
        }
        for(int i=0;i<4;i++)
        {
            var area=debugCornerRects[i];GUI.BeginGroup(area);bool right=i%2==1;
            DebugTextShadow(new Rect(0,0,area.width,22),DebugHeadings[i],right?debugHeadingRight:debugHeadingLeft);
            var color=GUI.color;GUI.color=new Color(.8f,.85f,.9f,.65f);GUI.DrawTexture(new Rect(0,22,area.width,1),Texture2D.whiteTexture);GUI.color=color;
            DebugTextShadow(new Rect(0,27,area.width,Math.Max(0,area.height-27)),debugCorners[i],right?debugRight:debugLeft);GUI.EndGroup();
        }
    }
    static void DebugTextShadow(Rect area,string text,GUIStyle style)
    {
        var color=style.normal.textColor;style.normal.textColor=new Color(0,0,0,.9f);var shadow=area;shadow.position+=Vector2.one;GUI.Label(shadow,text,style);style.normal.textColor=Color.white;GUI.Label(area,text,style);style.normal.textColor=color;
    }
}

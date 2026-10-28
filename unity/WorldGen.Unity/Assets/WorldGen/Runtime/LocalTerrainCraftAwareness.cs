using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    GUIStyle craftAwarenessStyle;
    bool CraftAwarenessVisible=>Ready&&!inventoryOpen&&!paused&&!mapOpen&&!ecologyOpen&&!chatOpen&&!DeathVisible&&ActiveItemCraft!=null;
    void DrawCraftAwareness()
    {
        if(!CraftAwarenessVisible)return;
        var job=ActiveItemCraft;if(job==null)return;
        float scale=Mathf.Clamp(Screen.height/1080f,.75f,1.5f),width=Mathf.Min(440*scale,Screen.width-32);
        var box=new Rect((Screen.width-width)*.5f,Screen.height-74*scale,width,48*scale);
        InventoryFill(box,new Color(.045f,.065f,.07f,.85f));
        craftAwarenessStyle??=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter};craftAwarenessStyle.fontSize=Mathf.RoundToInt(14*scale);
        GUI.Label(new Rect(box.x+8*scale,box.y+4*scale,box.width-16*scale,25*scale),CraftLabel(job)+" · "+Mathf.RoundToInt((float)job.Fraction*100)+"% · "+StoneCrafting.RealSecondsLeft(job,PlayerRules.Settings).ToString("0.0")+" s",craftAwarenessStyle);
        var track=new Rect(box.x+12*scale,box.yMax-13*scale,box.width-24*scale,6*scale);InventoryFill(track,new Color(.18f,.21f,.21f));
        track.width*=Mathf.Clamp01((float)job.Fraction);InventoryFill(track,InventoryGold);
    }
}

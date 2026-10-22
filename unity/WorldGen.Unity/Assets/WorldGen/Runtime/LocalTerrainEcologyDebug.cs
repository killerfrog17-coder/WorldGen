using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool ecologyOpen,ecologyRefreshQueued;Task<EcologyDossier> ecologyJob;CancellationTokenSource ecologyCancellation;
    EcologyDossier ecologyDossier;EcologyArticle[] ecologyFiltered=Array.Empty<EcologyArticle>();
    string ecologyCategory="Overview",ecologySearch="",ecologyMessage="",ecologyText="";
    int ecologyArticle=-1;Vector2 ecologyListScroll,ecologyArticleScroll;float ecologyBodyWidth=-1,ecologyBodyHeight;
    static readonly string[] EcologyCategories={"Overview","Fauna","Recorded flora","Basal resources","Ground cover","Flowers & herbs","Fungi","Mid-height plants","Trees","Food web"};
    public bool EcologyVisible=>ecologyOpen;
    void ToggleEcologyDebug(){ecologyOpen=!ecologyOpen;if(ecologyOpen){mapOpen=false;BeginEcologyDebug();}CaptureMouse();}
    void BeginEcologyDebug()
    {
        if(ecologyJob!=null&&!ecologyJob.IsCompleted){ecologyRefreshQueued=true;ecologyCancellation?.Cancel();ecologyMessage="Refreshing current region…";return;}
        if(terrain==null||sourceWorld==null){ecologyMessage="Ecology is not available yet.";return;}
        ecologyCancellation?.Dispose();ecologyCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=ecologyCancellation.Token;
        int cell=terrain.NearestCell(Position);var source=sourceWorld;ecologyMessage="Building regional ecology reference…";
        ecologyJob=Task.Run(()=>EcologyAtlas.Build(source,cell,token),token);
    }
    void UpdateEcologyDebug()
    {
        if(ecologyJob==null||!ecologyJob.IsCompleted)return;
        if(ecologyJob.IsFaulted)ecologyMessage="Ecology query failed: "+ecologyJob.Exception.GetBaseException().Message;
        else if(!ecologyJob.IsCanceled){ecologyDossier=ecologyJob.Result;ecologyMessage="";ecologyCategory="Overview";ecologySearch="";FilterEcologyArticles();}
        ecologyJob=null;
        if(ecologyRefreshQueued){ecologyRefreshQueued=false;BeginEcologyDebug();}
    }
    void FilterEcologyArticles()
    {
        ecologyFiltered=ecologyDossier==null?Array.Empty<EcologyArticle>():ecologyDossier.Articles.Where(a=>a.Category==ecologyCategory&&(ecologySearch.Length==0||a.Title.IndexOf(ecologySearch,StringComparison.OrdinalIgnoreCase)>=0||a.Body.IndexOf(ecologySearch,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
        ecologyListScroll=ecologyArticleScroll=Vector2.zero;ecologyArticle=-1;SetEcologyText(ecologyCategory=="Overview"?ecologyDossier?.Overview??ecologyMessage:ecologyFiltered.Length+" entries · select an article on the left.\n\nGenerated candidate catalogues are not guarantees of occurrence at your feet. Food web contains every recorded feeding link, grouped by consumer.");
    }
    void SetEcologyText(string text){ecologyText=text;ecologyBodyWidth=-1;ecologyArticleScroll=Vector2.zero;}
    void DrawEcologyDebug()
    {
        float width=Math.Min(1180,Screen.width-40),height=Screen.height-40;var panel=new Rect((Screen.width-width)/2,20,width,height);GUI.Box(panel,"");
        GUI.Label(new Rect(panel.x+15,panel.y+10,width-250,28),"ECOLOGY · "+(ecologyDossier?.Title??"current region"));
        if(GUI.Button(new Rect(panel.xMax-235,panel.y+8,180,30),"Refresh current location"))BeginEcologyDebug();
        if(GUI.Button(new Rect(panel.xMax-45,panel.y+8,30,30),"×")){ecologyOpen=false;CaptureMouse();}
        GUI.Label(new Rect(panel.x+15,panel.y+42,width-30,38),ecologyMessage.Length>0?ecologyMessage:"F3 + E / Esc closes · source cell "+ecologyDossier?.Cell+" · "+(ecologyDossier?.Articles.Length??0)+" articles · "+(ecologyDossier?.FoodLinks??0)+" recorded feeding links");
        float tabWidth=(width-30)/5;
        for(int i=0;i<EcologyCategories.Length;i++)if(GUI.Toggle(new Rect(panel.x+15+i%5*tabWidth,panel.y+80+i/5*29,tabWidth-3,27),ecologyCategory==EcologyCategories[i],EcologyCategories[i],GUI.skin.button)&&ecologyCategory!=EcologyCategories[i]){ecologyCategory=EcologyCategories[i];FilterEcologyArticles();}
        GUI.Label(new Rect(panel.x+15,panel.y+143,55,25),"Search");string query=GUI.TextField(new Rect(panel.x+72,panel.y+141,width-90,27),ecologySearch);if(query!=ecologySearch){ecologySearch=query;FilterEcologyArticles();}
        float left=ecologyCategory=="Overview"?0:Math.Min(310,width*.30f);var listRect=new Rect(panel.x+15,panel.y+177,left,height-192);
        if(left>0)
        {
            GUI.Box(listRect,"");ecologyListScroll=GUI.BeginScrollView(listRect,ecologyListScroll,new Rect(0,0,left-20,Math.Max(listRect.height-1,ecologyFiltered.Length*34)));
            int first=Math.Max(0,(int)(ecologyListScroll.y/34)),last=Math.Min(ecologyFiltered.Length,first+(int)(listRect.height/34)+2);
            for(int i=first;i<last;i++)if(GUI.Button(new Rect(3,i*34,left-26,32),(i==ecologyArticle?"› ":"")+ecologyFiltered[i].Title)){ecologyArticle=i;SetEcologyText(ecologyFiltered[i].Body);}
            GUI.EndScrollView();
        }
        var bodyRect=new Rect(panel.x+15+(left>0?left+10:0),panel.y+177,width-30-(left>0?left+10:0),height-192);var style=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false,fontSize=15};
        if(ecologyBodyWidth!=bodyRect.width){ecologyBodyWidth=bodyRect.width;ecologyBodyHeight=style.CalcHeight(new GUIContent(ecologyText),bodyRect.width-28)+15;}
        GUI.Box(bodyRect,"");ecologyArticleScroll=GUI.BeginScrollView(bodyRect,ecologyArticleScroll,new Rect(0,0,bodyRect.width-20,Math.Max(bodyRect.height-1,ecologyBodyHeight)));GUI.Label(new Rect(8,5,bodyRect.width-32,ecologyBodyHeight),ecologyText,style);GUI.EndScrollView();
    }
    void DisposeEcologyDebug(){ecologyCancellation?.Cancel();ecologyCancellation?.Dispose();}
}

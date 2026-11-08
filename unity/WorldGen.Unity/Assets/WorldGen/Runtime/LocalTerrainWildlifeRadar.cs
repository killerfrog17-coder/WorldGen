using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Rect wildlifeRadarPanel;
    bool wildlifeRadarOpen,radarF3Pending,radarPreviousDebug,radarPreviousFull;
    double wildlifeRadarRefreshAt;
    const double WildlifeRadarRange=50000;
    struct WildlifeRadarDot
    {public byte HumanKind;public V Position;public WildlifeTrackingTier Tier;public bool Group,Inactive,Dead;public int Members;public string Label;}
    readonly List<WildlifeRadarDot> wildlifeRadarDots=new List<WildlifeRadarDot>();
    readonly int[] wildlifeRadarTierCounts=new int[4];
    int wildlifeRadarActors,wildlifeRadarGroups,radarHumanActors,radarHumanCamps,radarHumanCommunities;
    Texture2D wildlifeRadarRing,wildlifeRadarDisc,wildlifeRadarDashedRing;
    readonly Vector3[] radarHudCorners=new Vector3[4];
    static readonly Color[] HumanRadarColors={Color.white,new Color(.87f,.66f,1),new Color(.3f,1,.87f),new Color(.85f,.65f,1)};
    GUIStyle wildlifeRadarText,wildlifeRadarTitle;
    static readonly Color[] WildlifeRadarColors={new Color(.55f,.92f,.45f),new Color(.35f,.8f,1),new Color(1,.76f,.3f),new Color(1,.4f,.5f)};
    static readonly string[] WildlifeRadarNames={"Fleeting / insects","Local / small animals","Resident groups","Notable / large animals"};
    static readonly double[] WildlifeRadarRings={35,220,900,2400};
    void ToggleWildlifeRadar()
    {wildlifeRadarOpen=!wildlifeRadarOpen;wildlifeRadarRefreshAt=0;}
    Vector2 WildlifeRadarPoint(V point,float radius)
    {
        TerrainHandoff.Coordinates(frame,point,out double x,out double z);double dx=x-motion.X,dz=z-motion.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        if(distance<.001)return Vector2.zero;
        // Expanded near field, explicitly labelled by the distance rings.
        double r=Math.Log(1+distance/50)/Math.Log(1+WildlifeRadarRange/50)*radius;
        return new Vector2((float)(dx/distance*r),(float)(-dz/distance*r));
    }
    void RefreshWildlifeRadar()
    {
        if(!wildlifeRadarOpen||!Ready||Time.unscaledTimeAsDouble<wildlifeRadarRefreshAt)return;
        wildlifeRadarRefreshAt=Time.unscaledTimeAsDouble+.2;wildlifeRadarDots.Clear();Array.Clear(wildlifeRadarTierCounts,0,4);
        wildlifeRadarActors=wildlifeRadarGroups=0;var represented=new Dictionary<ulong,int>();
        foreach(var animal in animals.Values)
        {
            if(animal.Retired||animal.Root==null||(animal.Planet-Position).Length*frame.RadiusM>WildlifeRadarRange)continue;
            var tier=AnimalBehaviour(animal.Spawn.Profile,animal.Spawn.Taxon).Tier;
            wildlifeRadarDots.Add(new WildlifeRadarDot{Position=animal.Planet,Tier=tier,Dead=animal.Dead,Members=1,
                Label=animal.Spawn.Profile.name+(animal.Dead?" / corpse":" / "+animal.Awareness.Mode+" / "+animal.Awareness.Sense)});
            wildlifeRadarActors++;if(!animal.Dead)wildlifeRadarTierCounts[(int)tier]++;
            if(!animal.Dead&&animal.Spawn.GroupId!=0){represented.TryGetValue(animal.Spawn.GroupId,out int count);represented[animal.Spawn.GroupId]=count+1;}
        }
        foreach(var group in wildlifeGroups.Values)
        {
            double distance=(group.Position-Position).Length*frame.RadiusM;if(distance>WildlifeRadarRange)continue;
            var profile=animalProfiles.FirstOrDefault(p=>p.slug==group.Species);if(profile==null)continue;
            var behaviour=AnimalBehaviour(profile,group.Taxon);bool inactive=distance>behaviour.TrackingM*1.25;
            represented.TryGetValue(group.Id,out int shown);int members=Math.Max(0,group.Members-shown);
            if(members==0)continue;
            wildlifeRadarDots.Add(new WildlifeRadarDot{Position=group.Position,Tier=behaviour.Tier,Group=true,Inactive=inactive,Members=members,
                Label=profile.name+" / "+members+" coarse members / "+group.Intent+(inactive?" / outside active range":"")});
            wildlifeRadarGroups++;if(!inactive)wildlifeRadarTierCounts[(int)behaviour.Tier]+=members;
        }
        RefreshHumanRadar();
    }
    void RefreshHumanRadar()
    {
        radarHumanActors=radarHumanCamps=radarHumanCommunities=0;
        foreach(var person in humanNpcs)
        {
            if(person.Root==null||HumanDistance(person.Planet,Position)>WildlifeRadarRange)continue;
            radarHumanActors++;
            wildlifeRadarDots.Add(new WildlifeRadarDot{HumanKind=1,Position=person.Planet,Dead=person.Health<=0,Members=1,
                Label=(person.Resident?.Name??"Human")+" / "+(person.Health<=0?"corpse":person.CurrentActivity.ToString())+" / physical actor"});
        }
        foreach(var group in livingOrdered)
        {
            var situation=group.Situation;if(HumanDistance(situation.Position,Position)>WildlifeRadarRange)continue;
            radarHumanCommunities++;
            bool schedules=group.Residents!=null&&situation.DistanceToPlayerM<=2400;
            wildlifeRadarDots.Add(new WildlifeRadarDot{HumanKind=3,Position=situation.Position,Group=true,Members=situation.Band.People,Inactive=!schedules,
                Label=situation.Band.Name+" / "+situation.Band.People+" people / "+situation.Phase+" / "+(schedules?"local schedules":"coarse projection")});
            if(situation.Phase==HumanCampPhase.Travelling)continue;
            radarHumanCamps++;
            wildlifeRadarDots.Add(new WildlifeRadarDot{HumanKind=2,Position=group.Grounded?group.Anchor:situation.Camp.Position,Group=true,Members=1,Inactive=!group.CampLoaded,
                Label=situation.Band.Name+" camp / "+situation.Camp.Design+" / "+(group.CampLoaded?"physical camp":group.Grounded?"surveyed site":"projected site")});
        }
    }
    void PrepareWildlifeRadar()
    {
        if(wildlifeRadarRing==null)
        {
            Texture2D Circle(bool ring,bool dashed=false)
            {
                const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=ring?"Wildlife radar ring":"Wildlife radar point",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float d=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f).magnitude;float a=ring?Mathf.Clamp01(1.5f-Mathf.Abs(d-size*29f/64)):Mathf.Clamp01(size*30f/64-d);if(dashed&&((int)((Mathf.Atan2(y+.5f-size*.5f,x+.5f-size*.5f)+Mathf.PI)*24/Mathf.PI)&1)==0)a=0;pixels[y*size+x]=new Color(1,1,1,a);}
                texture.SetPixels(pixels);texture.Apply(false,true);return texture;
            }
            wildlifeRadarRing=Circle(true);wildlifeRadarDisc=Circle(false);wildlifeRadarDashedRing=Circle(true,true);
        }
        wildlifeRadarText??=new GUIStyle(GUI.skin.label){fontSize=12,alignment=TextAnchor.MiddleLeft,normal={textColor=Color.white}};
        wildlifeRadarTitle??=new GUIStyle(wildlifeRadarText){fontSize=16,fontStyle=FontStyle.Bold};
    }
    Rect WildlifeRadarLayout(out float scale,out bool compact,out float diameter)
    {
        scale=Mathf.Clamp(Screen.height/1080f,.8f,1.5f);float top=12,bottom=Screen.height-12;
        if(debugOpen&&debugFull){PrepareDebugLayout();top=debugCornerRects[1].yMax+10;bottom=debugCornerRects[3].yMin-10;}
        if(survivalHudCanvas!=null&&survivalHudCanvas.enabled&&survivalHud!=null)
        {
            survivalHud.rectTransform.GetWorldCorners(radarHudCorners);
            bottom=Mathf.Min(bottom,Screen.height-radarHudCorners[1].y-12);
        }
        float available=Mathf.Max(1,bottom-top);compact=available<700*scale;
        diameter=compact?Mathf.Max(30,Mathf.Min(300*scale,available-100*scale)):380*scale;
        float width=Mathf.Min(Screen.width-24,compact?diameter+225*scale:440*scale);
        float height=Mathf.Min(available,compact?diameter+100*scale:700*scale);
        return new Rect(Screen.width-12-width,top+(available-height)*.5f,width,height);
    }
    void DrawWildlifeRadar()
    {
        if(!wildlifeRadarOpen||!Ready||mapOpen||ecologyOpen||chatOpen||inventoryOpen||DeathVisible)return;
        RefreshWildlifeRadar();if(Event.current.type!=EventType.Repaint)return;PrepareWildlifeRadar();
        var panel=WildlifeRadarLayout(out float scale,out bool compact,out float diameter);wildlifeRadarPanel=panel;float width=panel.width,height=panel.height;
        var prior=GUI.color;GUI.color=new Color(.035f,.055f,.065f,.18f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=Color.white;
        Vector2 mouse=Event.current.mousePosition-panel.position;
        GUI.BeginGroup(panel);wildlifeRadarText.fontSize=Mathf.Max(10,Mathf.RoundToInt(12*scale));wildlifeRadarTitle.fontSize=Mathf.RoundToInt(16*scale);
        GUI.Label(new Rect(16*scale,9*scale,width-32*scale,25*scale),"LIFE RADAR",wildlifeRadarTitle);
        GUI.Label(new Rect(16*scale,34*scale,width-32*scale,20*scale),"50 km / north up / expanded near-field scale",wildlifeRadarText);
        Vector2 center=new Vector2(compact?diameter*.5f+12*scale:width*.5f,compact?diameter*.5f+65*scale:259*scale);float radius=diameter*.5f;
        float outer=radius*32/29;
        GUI.color=new Color(.85f,.65f,1,.5f);GUI.DrawTexture(new Rect(center.x-outer,center.y-outer,outer*2,outer*2),wildlifeRadarDashedRing);
        GUI.color=Color.white;GUI.Label(new Rect(center.x+4*scale,center.y-radius-12*scale,90*scale,18*scale),"50 km",wildlifeRadarText);
        for(int i=3;i>=0;i--)
        {
            double range=WildlifeRadarRings[i];float r=(float)(Math.Log(1+range/50)/Math.Log(1+WildlifeRadarRange/50))*radius;
            float padded=r*32/29;
            GUI.color=new Color(WildlifeRadarColors[i].r,WildlifeRadarColors[i].g,WildlifeRadarColors[i].b,.36f);GUI.DrawTexture(new Rect(center.x-padded,center.y-padded,2*padded,2*padded),wildlifeRadarRing);
            GUI.color=Color.white;GUI.Label(new Rect(center.x+4*scale,center.y-r-12*scale,70*scale,18*scale),range<1000?range+" m":(range/1000).ToString("0.0")+" km",wildlifeRadarText);
        }
        GUI.color=new Color(1,1,1,.12f);GUI.DrawTexture(new Rect(center.x-radius,center.y,radius*2,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(center.x,center.y-radius,1,radius*2),Texture2D.whiteTexture);
        string hovered="";
        foreach(var dot in wildlifeRadarDots)
        {
            var at=center+WildlifeRadarPoint(dot.Position,radius);Color color=dot.Dead?new Color(.65f,.65f,.65f):dot.HumanKind>0?HumanRadarColors[dot.HumanKind]:WildlifeRadarColors[(int)dot.Tier];if(dot.Inactive)color.a=.5f;
            float size=(dot.Group?12:dot.Dead?5:7)*scale;GUI.color=color;GUI.DrawTexture(new Rect(at.x-size*.5f,at.y-size*.5f,size,size),dot.HumanKind==3?wildlifeRadarDashedRing:dot.Group?wildlifeRadarRing:wildlifeRadarDisc);
            if(dot.HumanKind==2){GUI.DrawTexture(new Rect(at.x-1.5f*scale,at.y-1.5f*scale,3*scale,3*scale),wildlifeRadarDisc);}
            if(dot.Group&&dot.Members>1){GUI.color=Color.white;GUI.Label(new Rect(at.x+6*scale,at.y-9*scale,36*scale,18*scale),dot.Members.ToString(),wildlifeRadarText);}
            if((mouse-at).sqrMagnitude<100*scale*scale)hovered=dot.Label;
        }
        GUI.color=Color.white;GUI.DrawTexture(new Rect(center.x-3*scale,center.y-3*scale,6*scale,6*scale),wildlifeRadarDisc);
        var direction=new Vector2(Mathf.Sin(heading*Mathf.Deg2Rad),-Mathf.Cos(heading*Mathf.Deg2Rad));
        GUI.DrawTexture(new Rect(center.x+direction.x*10*scale-2*scale,center.y+direction.y*10*scale-2*scale,4*scale,4*scale),wildlifeRadarDisc);
        GUI.Label(new Rect(center.x-4*scale,center.y-radius-20*scale,20*scale,20*scale),"N",wildlifeRadarText);
        float y=compact?70*scale:460*scale,legendX=compact?diameter+24*scale:16*scale;
        for(int i=0;i<4;i++)
        {GUI.color=WildlifeRadarColors[i];GUI.DrawTexture(new Rect(legendX+2*scale,y+5*scale,9*scale,9*scale),wildlifeRadarRing);GUI.color=Color.white;GUI.Label(new Rect(legendX+20*scale,y,width-legendX-24*scale,20*scale),WildlifeRadarNames[i]+" : "+wildlifeRadarTierCounts[i],wildlifeRadarText);y+=21*scale;}
        foreach(var row in new[]{("People",radarHumanActors,1),("Camps",radarHumanCamps,2),("Communities / 50 km",radarHumanCommunities,3)})
        {
            GUI.color=HumanRadarColors[row.Item3];GUI.DrawTexture(new Rect(legendX+2*scale,y+5*scale,9*scale,9*scale),row.Item3==3?wildlifeRadarDashedRing:row.Item3==2?wildlifeRadarRing:wildlifeRadarDisc);
            GUI.color=Color.white;GUI.Label(new Rect(legendX+20*scale,y,width-legendX-24*scale,20*scale),row.Item1+" : "+row.Item2,wildlifeRadarText);y+=21*scale;
        }
        if(compact)
        {
            GUI.Label(new Rect(legendX,y+2*scale,width-legendX-8*scale,40*scale),"Solid: actor / Hollow: camp or group\nDotted: community / Dim: coarse",wildlifeRadarText);
            GUI.Label(new Rect(legendX,y+46*scale,width-legendX-8*scale,40*scale),wildlifeRadarActors+" actors\n"+wildlifeRadarGroups+" coarse groups",wildlifeRadarText);
        }
        else
        {
            GUI.Label(new Rect(16*scale,y+2*scale,width-30*scale,20*scale),"Solid: actor / Hollow: group or camp / Dotted: community",wildlifeRadarText);
            GUI.Label(new Rect(16*scale,y+22*scale,width-30*scale,20*scale),wildlifeRadarActors+" actors / "+wildlifeRadarGroups+" coarse groups; dim = inactive",wildlifeRadarText);
        }
        if(hovered.Length>0)GUI.Label(new Rect(16*scale,height-26*scale,width-30*scale,20*scale),hovered,wildlifeRadarText);
        GUI.EndGroup();GUI.color=prior;
    }
    void DisposeWildlifeRadar()
    {if(wildlifeRadarRing!=null)Destroy(wildlifeRadarRing);if(wildlifeRadarDisc!=null)Destroy(wildlifeRadarDisc);if(wildlifeRadarDashedRing!=null)Destroy(wildlifeRadarDashedRing);wildlifeRadarDots.Clear();}
    IEnumerator CheckWildlifeRadar(Animal focus,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("RADAR "+text);report.Add("PASS "+text);Debug.Log("RADAR PASS "+text);}
        var oldKeyboard=testKeyboard;var currentKeyboard=Keyboard.current;var oldBackground=InputSystem.settings.backgroundBehavior;
        bool oldDebug=debugOpen,oldFull=debugFull,oldRadar=wildlifeRadarOpen;var keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;TestKeyboard(keyboard);wildlifeRadarOpen=false;
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F3));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F3,Key.A));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(wildlifeRadarOpen&&debugOpen==oldDebug,"F3 then A opens the radar without changing ordinary F3 visibility");
            RefreshWildlifeRadar();Check(wildlifeRadarDots.Any(p=>!p.Group&&!p.Dead&&p.Label.StartsWith(focus.Spawn.Profile.name)),"Radar reads the actual live actor without spawning replacements");
            float near=WildlifeRadarPoint(frame.Position(motion.X,motion.Z+35),190).magnitude,far=WildlifeRadarPoint(frame.Position(motion.X,motion.Z+2400),190).magnitude;
            Check(near>0&&near<far&&far<190,"Expanded radar distance rings preserve near/far order within the3km plot");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F3,Key.A));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(!wildlifeRadarOpen&&debugOpen==oldDebug,"F3+A closes the radar and keeps the standard debug overlay state");
        }
        finally
        {TestKeyboard(oldKeyboard);InputSystem.RemoveDevice(keyboard);if(currentKeyboard!=null&&currentKeyboard.added)currentKeyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=oldBackground;debugOpen=oldDebug;debugFull=oldFull;wildlifeRadarOpen=oldRadar;radarF3Pending=false;}
    }
}

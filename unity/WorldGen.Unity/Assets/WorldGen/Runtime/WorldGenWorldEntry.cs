using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    bool worldEntryOpen;string worldEntryError="";
    Task<PlaySession> worldEntryWork;
    void ChooseWorldEntry()
    {
        if(!CanRequestTerrain)return;
        humanPlaying=false;simulationPaused=false;worldEntryOpen=true;worldEntryError="";GUIUtility.keyboardControl=0;
    }
    void StartWorldEntry(bool resident)
    {
        if(worldEntryWork!=null||humanStep!=null)return;
        var source=world;var session=play.Copy();int seed=Guid.NewGuid().GetHashCode();if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-mind-check"))seed=int.Parse(Argument("--worldgen-entry-seed","3"));
        PrepareLocalTerrain();var preparing=localTerrainTask;worldEntryError="";bool diagnostic=Environment.GetCommandLineArgs().Contains("--worldgen-entry-check");
        if(diagnostic)Debug.Log("ENTRY preparing scale="+session.Settings.HorizontalScale+" / terrain="+preparing.Status);
        worldEntryWork=Task.Run(async()=>
        {
            var terrain=await preparing;if(diagnostic)Debug.Log("ENTRY sampler ready");var rng=new System.Random(seed);var landRng=new Rng(seed,"world-entry-land",0);ResidentEntry entry=resident?PlayerWorldEntry.RandomResident(source,session,seed):null;
            V point=entry?.Position??ExplorerNavigation.RandomPoint(ref landRng);bool dry=false;
            for(int attempt=0;attempt<1024;attempt++)
            {
                if(entry==null)point=ExplorerNavigation.RandomPoint(ref landRng);
                else if(attempt>0)
                {
                    var local=new LocalTerrainFrame(entry.Group.Position,terrain.RadiusM,0,session.Settings.HorizontalScale);
                    double a=attempt*2.399963229728653,r=Math.Min(300,Math.Sqrt(attempt)*3);
                    point=local.Position(Math.Cos(a)*r,Math.Sin(a)*r);
                }
                if(source.Hydrology.OceanConnected[terrain.NearestCell(point)])continue;
                var patch=terrain.Patch(point,2000);var sample=patch.Sample(point);
                if(sample.Underwater||sample.ElevationM<=sample.WaterSurfaceM+.15)continue;
                var frame=new LocalTerrainFrame(point,terrain.RadiusM,sample.ElevationM,session.Settings.HorizontalScale);
                if(Math.Abs(patch.Elevation(frame.Position(2,0))-sample.ElevationM)>.9||Math.Abs(patch.Elevation(frame.Position(0,2))-sample.ElevationM)>.9)continue;
                dry=true;break;
            }
            if(!dry)throw new InvalidOperationException("No safe dry entry point was found. Please try again.");
            // Keep world edits and existing dropped goods. Leave the old life's
            // carried objects at its last location instead of deleting them.
            if(session.HasPlayer)
            {
                foreach(var item in session.Inventory.Items.Where(i=>i.Place!=PossessionPlace.Container&&session.Inventory.Carried(i)))
                {item.Place=PossessionPlace.Ground;item.Position=session.PlayerPosition;item.InFlight=item.GroundMoving=false;item.GroundOffsetM=0;}
                if(session.Identity!=null)
                {
                    var memory=session.Humans.People.FirstOrDefault(p=>p.Person==session.Identity.Person.Id);
                    if(memory==null){memory=new HumanPersonMemory{Person=session.Identity.Person.Id,Group=session.Identity.Group};session.Humans.People.Add(memory);}
                    memory.Position=session.PlayerPosition;memory.Health=session.Survival.Health;memory.Dead=session.Survival.Dead;memory.SolarDays=session.SolarDays;memory.Bleeding=session.Survival.Bleeding?.Copy();
                }
            }
            session.Inventory.Crafts.Clear();session.Inventory.ToolWorkEquipment=null;
            session.HasPlayer=true;session.PlayerPosition=point;session.Mode=resident?GameMode.Survival:GameMode.Creative;session.Flying=!resident;
            session.Survival=new PlayerSurvival();session.Survival.Restore();session.CameraDistance=0;session.CameraDistanceLocked=false;session.HeightAboveGround=1.65;session.Heading=rng.Next(360);session.Pitch=0;
            session.Identity=entry?.Identity;session.PlayerHumanVariant=entry==null?-1:PlayerWorldEntry.Variant(entry.Identity.Person);
            session.Inventory.PlayerName=entry?.Identity.Person.Name??"Player";
            if(entry!=null)
            {
                entry.Position=point;
                var memory=session.Humans.People.FirstOrDefault(p=>p.Person==entry.Identity.Person.Id);
                if(memory!=null){session.Survival.Health=Math.Max(1,memory.Health);session.Survival.Bleeding=memory.Bleeding?.Copy();}
                foreach(var item in HumanEquipment.Held(entry))
                {
                    var old=session.Inventory.Find(item.Id);if(old!=null)session.Inventory.Items.Remove(old);
                    session.Inventory.Items.Add(item);
                }
            }
            session.Validate();if(diagnostic)Debug.Log("ENTRY prepared "+session.Mode+" / resident="+(session.Identity?.Person.Id??0));return session;
        });
    }
    void PollWorldEntry()
    {
        if(worldEntryWork?.IsCompleted==true)
        {
            var work=worldEntryWork;worldEntryWork=null;
            if(work.IsFaulted){worldEntryError=work.Exception.GetBaseException().Message;Debug.LogWarning("World entry: "+worldEntryError);}
            else{play=work.Result;worldEntryOpen=false;RequestTerrain();return;}
        }
    }
    void DrawWorldEntry()
    {
        float width=Math.Min(470,Screen.width-32),height=play.HasPlayer?360:310;var rect=new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);MenuCard(rect);
        GUILayout.BeginArea(new Rect(rect.x+26,rect.y+22,rect.width-52,rect.height-44));GUILayout.Label("ENTER WORLD",TitleStyle(26));GUILayout.Space(14);
        GUI.enabled=worldEntryWork==null&&humanStep==null;
        if(play.HasPlayer&&MenuButton("Resume current character")){worldEntryOpen=false;RequestTerrain();}
        if(MenuButton("Random land · Creative"))StartWorldEntry(false);
        GUI.enabled=worldEntryWork==null&&humanStep==null&&Prehistory.Foraging(world?.Prehistory);
        if(MenuButton("Become a random adult NPC"))StartWorldEntry(true);
        GUI.enabled=true;
        if(worldEntryWork!=null)GUILayout.Label("Finding your place in the world…",MutedStyle());
        else if(worldEntryError.Length>0)GUILayout.Label(worldEntryError,MutedStyle());
        else GUILayout.Label("Join an existing tribe in Survival, with that person's identity, family and held belongings.",MutedStyle());
        GUILayout.FlexibleSpace();GUI.enabled=worldEntryWork==null;if(MenuButton("Back"))worldEntryOpen=false;GUI.enabled=true;GUILayout.EndArea();
    }
}

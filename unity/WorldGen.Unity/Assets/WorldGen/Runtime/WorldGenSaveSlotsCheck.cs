using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public void PrepareSaveBloodFixture()
    {
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        var renderer=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        if(!humanPlayer.Blood.ContainsKey(renderer)){var mask=new byte[65536];mask[99]=217;var canvas=new WorldGenBloodCanvas(mask);humanPlayer.Blood.Add(renderer,canvas);BindHumanBlood(renderer,canvas);}
        Wildlife.Individuals.Add(new WildlifeIndividual{Id=999,Species="deer",Position=Position,Health=0,MaximumHealth=100,BloodMask=new byte[65536]});Wildlife.Individuals.Last().BloodMask[43]=200;
        var rock=new Possession{Id="save-check-rock",Kind=PossessionKind.SmallRock,Position=Position,Place=PossessionPlace.Ground};Inventory.Items.Add(rock);Inventory.Hold(rock.Id,out _);
        Camp.Objects.Clear();Vitals.Tiredness=63;
        var post=new CampObject{Id="save-check-post",Kind=CampKind.Stake,Position=frame.Position(motion.X+2,motion.Z),EmbedM=.25,SoilDepthM=.8,LastSimulatedSeconds=Camp.SimulatedSeconds};
        CampRules.Add(post,WoodCrafting.Branch(new ScatterInstance{Position=post.Position,Kind=ScatterKind.Branch,WidthM=1.5,HeightM=.03}));Camp.Objects.Add(post);
        var bed=new CampObject{Id="save-check-bed",Kind=CampKind.Bedding,Position=frame.Position(motion.X-2,motion.Z),LastSimulatedSeconds=Camp.SimulatedSeconds};
        CampRules.Add(bed,PortableTechnology.Create(PossessionKind.DryGrass,bed.Position));Camp.Objects.Add(bed);
        var fire=new CampObject{Id="save-check-fire",Kind=CampKind.Fire,Position=frame.Position(motion.X,motion.Z+3.5),LastSimulatedSeconds=Camp.SimulatedSeconds};
        CampRules.Add(fire,PortableTechnology.Create(PossessionKind.TinderBundle,fire.Position));CampRules.Add(fire,PortableTechnology.Create(PossessionKind.KindlingBundle,fire.Position));CampRules.Ignite(fire,true);Camp.Objects.Add(fire);
    }
    public IEnumerator CheckBackgroundCraft(List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("BACKGROUND "+text);report.Add("PASS "+text);Debug.Log("BACKGROUND PASS "+text);}
        InitializeInventory();PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();Vitals.Restore();TestWeatherOverride=new WeatherFrame(25,.75,0,0,0,0,1500,1700,8500,0,0,0,0);
        var branch=new Possession{Id="background-branch",Kind=PossessionKind.Branch,Position=Position,Place=PossessionPlace.Ground,WidthM=.03,DepthM=.03,HeightM=.9,MassKg=.4,WoodSoundness=.95,WoodStraightness=1};
        var cutter=new Possession{Id="background-cutter",Kind=PossessionKind.StoneFlake,Position=Position,Place=PossessionPlace.Ground,EdgeSharpness=.8,WidthM=.1,DepthM=.07,HeightM=.01};
        Inventory.Items.Add(branch);Inventory.Items.Add(cutter);SetCraftWorkArea(branch);SetCraftWorkArea(cutter,1);SetInventoryOpen(true);
        Check(StartInventoryKnapping(branch.Id,ItemCraftAction.TrimBranch),"Background crafting fixture starts real nearby woodworking");var job=ActiveItemCraft;job.RequiredSeconds=100000;
        Check(!CraftAwarenessVisible,"Outside-inventory progress overlay is hidden inside inventory");SetInventoryOpen(false);Check(CraftAwarenessVisible,"Closing inventory exposes the running craft's awareness overlay");
        double work=job.WorkSeconds,hunger=Vitals.Hunger;yield return new WaitForSecondsRealtime(.5f);
        Check(job.Running&&job.WorkSeconds>work,"Craft progress advances with the inventory closed in the background-capable player");
        Check(Vitals.Hunger>hunger,"Survival needs continue advancing during background gameplay");
        var item=new Possession{Id="background-drop",Kind=PossessionKind.SmallRock,Place=PossessionPlace.Ground,Position=Position};Inventory.Items.Add(item);ReleasePossession(item,true);
        float y=inventoryDrops[item.Id].Body.position.y;yield return new WaitForSecondsRealtime(.25f);
        Check(inventoryDrops.TryGetValue(item.Id,out var body)&&body.Body!=null&&body.Body.position.y<y-.01f,"Loose-item gravity continues during background gameplay");
        PauseItemCraft(job);TestWeatherOverride=null;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Check(!CraftAwarenessVisible,"Pausing the work hides the running-progress overlay");
    }
}
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckSaveSlotsRuntime(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("SAVE SLOTS "+text);report.Add("PASS "+text);Debug.Log("SAVE SLOTS PASS "+text);}
        library=new WorldLibrary(Path.Combine(output,"save-library"));activeWorldId=activeSaveId=null;saveName="Camp native check";terrainExplorer.PrepareSaveBloodFixture();
        var timings=new List<string>();double started=Time.realtimeSinceStartupAsDouble;SaveToLibrary();while(exportJob!=null)yield return null;timings.Add("first,"+(Time.realtimeSinceStartupAsDouble-started));
        Check(activeSaveId!=null&&library.Entries().Length==1,"Actual game save publishes a named manual slot");string camp=activeSaveId,group=activeWorldId;
        var first=library.Entries().Single();var session=WorldLibrary.ReadPlaySession(first.SnapshotPath);
        Check(session.Survival.Blood.Any(b=>b.Mask[99]==217)&&session.Wildlife.Individuals.Single(i=>i.Id==999).BloodMask[43]==200,"Packaged binary session cold-loads both player and animal blood data above the old JSON cap");
        Check(session.Camp.Objects.Count==3&&session.Camp.Objects.Single(c=>c.Id=="save-check-post").EmbedM==.25&&session.Camp.Objects.Single(c=>c.Id=="save-check-fire").Burning&&session.Survival.Tiredness==63,"Actual named save stores bedding, planted depth, burning fuel and independent tiredness");
        string baseFile=Directory.GetFiles(Path.Combine(library.Root,".bases"),"*.world").Single();var baseTime=File.GetLastWriteTimeUtc(baseFile);
        started=Time.realtimeSinceStartupAsDouble;SaveToLibrary();while(exportJob!=null)yield return null;timings.Add("overwrite,"+(Time.realtimeSinceStartupAsDouble-started));
        Check(activeSaveId==camp&&library.Entries().Length==1&&File.GetLastWriteTimeUtc(baseFile)==baseTime,"Same-name game save overwrites its slot without rewriting the planet");
        SaveToLibrary(false,"Hunting native check");while(exportJob!=null)yield return null;string hunt=activeSaveId;
        Check(hunt!=camp&&activeWorldId==group&&library.Entries().Length==2,"Changing the game save name creates a distinct slot in the same playthrough");
        var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();var oldBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            for(int i=0;i<2;i++)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F5));yield return null;HandleQuickSave(keyboard,true);while(exportJob!=null)yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            }
        }
        finally{InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=oldBackground;}
        var quick=library.Entries().SingleOrDefault(e=>e.Kind=="quick");
        Check(quick!=null&&quick.GroupId==group&&quick.ParentSaveId==hunt&&library.Entries().Length==3,"F5 key events update one quicksave linked to the active manual slot");
        Check(saveName=="Hunting native check"&&activeSaveId==hunt,"Quicksaving preserves the default manual save name and target");
        var read=Task.Run(()=>WorldFile.ReadFile(quick.SnapshotPath));while(!read.IsCompleted)yield return null;if(read.IsFaulted)throw read.Exception.GetBaseException();
        Check(read.Result.Stages.Last().Hash==world.Stages.Last().Hash&&read.Result.Prehistory.Hash==read.Result.Prehistory.ComputeHash(),"Packaged shared-world load preserves geography and verified historical state");
        var originalPlay=play;string otherGroup=Guid.NewGuid().ToString("N");SaveToLibrary(false,"Context source check");play=new PlaySession();activeWorldId=otherGroup;activeSaveId=null;saveName="Another playthrough";
        while(exportJob!=null)yield return null;
        Check(activeWorldId==otherGroup&&activeSaveId==null&&saveName=="Another playthrough","A completed background save cannot rebind a different current playthrough");
        play=originalPlay;activeWorldId=group;activeSaveId=hunt;saveName="Hunting native check";
        Check(Application.runInBackground,"Player is configured to keep running while unfocused");
        yield return terrainExplorer.CheckBackgroundCraft(report);
        ReturnFromPlay("Menu");double deadline=Time.realtimeSinceStartupAsDouble+15;while(terrainUnloading!=null&&!terrainUnloading.isDone&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        OpenSave(quick);deadline=Time.realtimeSinceStartupAsDouble+120;
        while((Busy||terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.PerformanceGroundSettled)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(!Busy&&terrainExplorer!=null&&terrainExplorer.PerformanceGroundSettled,"Real library quicksave load returns to the playable world, including terrain-worker source loading");
        Check(activeWorldId==group&&activeSaveId==hunt&&saveName=="Hunting native check"&&play.Inventory.Items.Any(i=>i.Id=="save-check-rock"),"Reload restores quicksave inventory and its manual-slot association");
        Check(play.Camp.Objects.Count==3&&play.Camp.Objects.Single(c=>c.Id=="save-check-post").EmbedM==.25&&play.Camp.Objects.Single(c=>c.Id=="save-check-fire").Burning&&Math.Abs(play.Survival.Tiredness-63)<.1,"Full quicksave re-entry restores the same placed camp and sleep pressure");
        File.WriteAllLines(Path.Combine(output,"save-timings.csv"),timings);
    }
}

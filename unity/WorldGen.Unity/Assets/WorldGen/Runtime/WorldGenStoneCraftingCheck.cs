using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckStoneCraftingFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("STONE CRAFT CHECK PASS "+text);}
        IEnumerator Image(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        NearbyPossession core=null,tool=null;double deadline=Time.unscaledTimeAsDouble+50;
        NearbyPossession Approach(ScatterInstance stone)
        {
            TerrainHandoff.Coordinates(frame,stone.Position,out double x,out double z);
            motion.Place(x+.65,z,Ground(x+.65,z));TestHumanView(0);heading=90;pitch=65;UpdateCamera();RefreshInventoryNearby(true);
            return inventoryNearby.FirstOrDefault(n=>n.Item.NaturalId==stone.Id);
        }
        while(core==null&&Time.unscaledTimeAsDouble<deadline)
        {
            // Gather separately, like a player: useful loose stones need not lie
            // within arm's reach of each other in a natural scatter distribution.
            var stones=scatterChunks.Values.SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Stone&&!CollectedRock(p)).ToArray();
            foreach(var stone in stones)
            {
                var item=RockPossession(stone);if(!StoneCrafting.StrikingStone(item))continue;
                if(tool==null)
                {
                    var near=Approach(stone);
                    if(near!=null&&MovePossession(near.Item.Id,PossessionPlace.RightHand))tool=new NearbyPossession{Item=Inventory.Find(near.Item.Id)};
                    continue;
                }
                if(StoneCrafting.WorkpieceReason(item).Length>0)continue;
                core=Approach(stone);if(core!=null)break;
            }
            if(core==null)yield return null;
        }
        if(core==null)Debug.Log("STONE CRAFT FIXTURE stones="+string.Join(";",scatterChunks.Values.SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Stone).Take(16).Select(p=>p.Variant+":"+p.WidthM.ToString("F3")+"/"+p.HeightM.ToString("F3"))));
        Check(core!=null&&tool!=null,"real natural stones provide a usable workpiece and striking stone");
        SetInventoryOpen(true);inventoryContext=core;inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.3f);yield return Image("craft-action");inventoryContext=null;
        Check(StartInventoryKnapping(core.Item.Id),"inventory flaking action starts using physical stone roles");
        var job=ActiveItemCraft;var work=Inventory.Find(job.WorkpieceId);var hammer=Inventory.Find(job.ToolId);double mass=work.MassKg+hammer.MassKg;
        Check(work.Name=="Small rock"&&hammer.Name=="Small rock","input stones retain ordinary names");
        Check(inventoryCollected.Contains(work.NaturalId)&&inventoryCollected.Contains(hammer.NaturalId),"both originals are collected once, without duplicate scatter");
        Check(!Paused&&inventoryOpen,"crafting allows solar time to advance while the inventory is open");
        Check(stoneCraftClips?.Length==6&&stoneCraftSoundCount>0,"real AI impact bank is loaded and dispatched");
        PlayerRules.Settings.ItemCraftingTimeMultiplier=4;double solar=PlayerRules.SolarDays;
        deadline=Time.unscaledTimeAsDouble+20;while(job.Fraction<.3&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(job.Fraction>=.3&&Inventory.Crafts.Contains(job),"visible timed work advances before producing an output");
        Check(PlayerRules.SolarDays>solar,"inventory crafting advances the real play clock");
        PauseItemCraft(job);double progress=job.WorkSeconds;yield return new WaitForSecondsRealtime(.25f);
        Check(job.WorkSeconds==progress&&!job.Running,"pausing preserves exact progress");
        Check(CraftFor(work.Id)==job&&CraftFor(hammer.Id)==job,"both involved item icons expose the same progress");
        inventoryContext=new NearbyPossession{Item=work};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.3f);yield return Image("craft-progress-paused");inventoryContext=null;
        var saved=new PlaySession{Settings=PlayerRules.Settings.Copy()};StorePlayer(saved);string json=WorldJson.Write(saved);File.WriteAllText(Path.Combine(output,"crafting-session.json"),json);var restored=WorldJson.Read<PlaySession>(json);restored.Validate();
        Check(Math.Abs(restored.Inventory.Crafts.Single().WorkSeconds-progress)<1e-8&&restored.Settings.ItemCraftingTimeMultiplier==4,"partial work and multiplier survive full-session serialization");
        Check(MovePossession(hammer.Id,PossessionPlace.Ground),"tool can be set down without losing the unfinished project");
        Check(StartInventoryKnapping(work.Id)&&ActiveItemCraft.WorkSeconds==progress,"resume retrieves the tool and retains work");
        deadline=Time.unscaledTimeAsDouble+30;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<deadline)yield return null;
        var flakes=Inventory.Items.Where(p=>p.Kind==PossessionKind.StoneFlake).ToArray();Check(flakes.Length==1&&Inventory.Crafts.Count==0,"one completed job yields exactly one physical cutting flake");
        Check(work.FlakeRemovals==1&&Math.Abs(work.MassKg+hammer.MassKg+flakes[0].MassKg-mass)<1e-10,"finished flake mass comes from the same original stone");
        Check(flakes[0].EdgeSharpness>0&&flakes[0].NaturalId==0,"output carries cutting capability without copying a natural identity");
        yield return Image("craft-finished");Inventory.Validate();
    }
}
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckStoneCraftingRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=listener;
        double deadline=Time.realtimeSinceStartupAsDouble+180;while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null)throw new InvalidOperationException("Crafting fixture unavailable");
        TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;
        var geology=GroundEnvironment.For(world);V target=TerrainMapViewport.FromDegrees(50,10);
        for(int i=0;i<world.Surface.Cells.Length;i++)
        {
            if(world.Hydrology.OceanConnected[i]||world.Ecology.Habitat[i].MeanTenthsC<50||world.Ecology.Habitat[i].MeanTenthsC>250)continue;
            if(geology.Cell(i).Bedrock==RockFamily.Basalt){target=world.Surface.Cells[i].Position;break;}
        }
        ExploreTerrain(target);deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new InvalidOperationException("Crafting terrain unavailable");
        yield return terrainExplorer.CheckStoneCraftingFlow(output,report);CapturePlayer();play.Validate();ReturnFromPlay("Simulation");yield return null;yield return null;
        Application.logMessageReceived-=listener;if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));report.Add("PASS no runtime errors or exceptions");
        File.WriteAllLines(Path.Combine(output,"crafting-checks.txt"),report);Debug.Log("STONE CRAFT FLOW PASS "+report.Count);Application.Quit(0);
    }
}

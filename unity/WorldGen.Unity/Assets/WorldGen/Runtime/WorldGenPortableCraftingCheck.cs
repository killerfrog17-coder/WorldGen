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
    public IEnumerator CheckPortableFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("PORTABLE CHECK PASS "+text);}
        IEnumerator Image(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        Possession Add(PossessionKind kind,string species="") {var p=PortableTechnology.Create(kind,Position,species,1);Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        void EmptyHands(){foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)){p.Place=PossessionPlace.Ground;SetCraftWorkArea(p);}}
        IEnumerator Finish(string recipe,Possession primary)
        {
            EmptyHands();RefreshInventoryNearby(true);Check(StartPortableCraft(recipe,primary.Id),"Live contextual action starts: "+recipe);double deadline=Time.unscaledTimeAsDouble+12;
            while(Inventory.Crafts.Any(j=>j.WorkpieceId==primary.Id)&&Time.unscaledTimeAsDouble<deadline)yield return null;
            Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==primary.Id),"Live work completes: "+recipe);Inventory.Validate();
        }
        yield return CheckAutomaticCraftEquipment(report);
        InitializeInventory();Check(Inventory.Items.Count==0,"No starter equipment is silently granted");PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        var fibre=Add(PossessionKind.RawFibre);var edge=Add(PossessionKind.StoneBlade);var awl=Add(PossessionKind.BoneAwl);var hide=Add(PossessionKind.RawHide,"deer");
        SetInventoryOpen(true);yield return Finish("prepare-fibres",fibre);yield return Finish("twist-cord",fibre);Check(fibre.Kind==PossessionKind.Cordage,"Raw fibres become a reusable physical binding ingredient");
        yield return Finish("scrape-hide",hide);yield return Finish("work-hide",hide);Check(hide.Kind==PossessionKind.WorkedHide,"Hide preparation preserves source object");
        EmptyHands();RefreshInventoryNearby(true);Check(StartPortableCraft("hide-pouch",hide.Id),"Multi-input sewing/lacing action starts");var job=ActiveItemCraft;Check(CraftFor(fibre.Id)==job&&CraftFor(edge.Id)==job&&CraftFor(awl.Id)==job,"Inputs and tools share job/progress");
        yield return null;PauseItemCraft(job);double progress=job.WorkSeconds;var roundTrip=WorldJson.Read<PlayerInventory>(WorldJson.Write(Inventory));roundTrip.Validate();Check(roundTrip.Crafts[0].IngredientIds.Count==2,"Multi-material reservations survive save/reload");
        inventoryContext=new NearbyPossession{Item=hide};inventoryMenuAt=new Vector2(Screen.width*.66f,Screen.height*.22f);yield return Image("contextual-crafting");inventoryContext=null;
        Check(StartPortableCraft("hide-pouch",hide.Id)&&ActiveItemCraft.WorkSeconds==progress,"Resume preserves work");double finishDeadline=Time.unscaledTimeAsDouble+12;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<finishDeadline)yield return null;
        Check(hide.Kind==PossessionKind.HidePouch&&Inventory.Find(fibre.Id)==null&&Inventory.Find(edge.Id)!=null&&Inventory.Find(awl.Id)!=null,"Exactly the ingredients consumed; tools retained");
        Check(hide.AppearanceSpecies=="deer"&&hide.AppearanceCoat==1,"Specific animal coat survives pouch crafting");EmptyHands();
        if(PortableTechnology.Worn(hide.Place))Check(MovePossession(hide.Id,PossessionPlace.Ground),"Automatically equipped finished pouch can be set down before fitting a belt");
        var pack=Add(PossessionKind.HidePack,"deer");var satchel=Add(PossessionKind.HideSatchel,"deer");var belt=Add(PossessionKind.FibreBelt);var knife=Add(PossessionKind.HaftedKnife);
        Check(MovePossession(pack.Id,PossessionPlace.Back)&&MovePossession(satchel.Id,PossessionPlace.Shoulder)&&MovePossession(belt.Id,PossessionPlace.Waist),"Back, shoulder and waist equipment coexist");
        Check(MovePossession(knife.Id,PossessionPlace.Container,belt.Id,0,0),"Belt actually carries a knife");Check(MovePossession(hide.Id,PossessionPlace.Container,autoStow:true),"Quick stow searches available worn carriers");
        UpdateInventoryVisuals();double coatDeadline=Time.unscaledTimeAsDouble+15;while(!animalModels.ContainsKey("deer")&&Time.unscaledTimeAsDouble<coatDeadline)yield return null;
        Check(animalModels.ContainsKey("deer"),"Existing source animal coat loads for hide equipment");UpdatePortableAppearances();RenderInventoryPreview();
        var root=inventoryVisuals[pack.Id];var props=new MaterialPropertyBlock();root.GetComponentInChildren<Renderer>().GetPropertyBlock(props);
        Check(props.GetTexture("_SourceCoats")==animalModels["deer"].Asset.Coats&&props.GetFloat("_Coat")==1,"Actual unchanged animal texture array is bound to hide mesh");
        Check(inventoryPreviewActor!=null&&inventoryPreviewGear.Count>=4,"Native character portrait includes carrying equipment and belt tool");yield return Image("equipment-storage");
        Inventory.Validate();var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full session accepts equipment, contents and provenance");
        Check(ExecuteGive(new[]{"/give","deer","hide","coat=2"}).Success,"Species-specific material give command accepts coat index");
        inventoryContext=new NearbyPossession{Item=Inventory.Items.Last()};inventoryMenuAt=new Vector2(Screen.width*.66f,Screen.height*.22f);yield return Image("raw-hide-options");inventoryContext=null;
        SetInventoryOpen(false);
    }
}
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckPortableCraftingRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=listener;
        double deadline=Time.realtimeSinceStartupAsDouble+180;while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null)throw new InvalidOperationException("Portable crafting fixture unavailable");
        TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;ExploreTerrain(TerrainMapViewport.FromDegrees(50,10));deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new InvalidOperationException("Portable terrain unavailable");
        yield return terrainExplorer.CheckPortableFlow(output,report);CapturePlayer();play.Validate();ReturnFromPlay("Simulation");yield return null;yield return null;
        Application.logMessageReceived-=listener;if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));report.Add("PASS no runtime errors or exceptions");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("PORTABLE FLOW PASS "+report.Count);Application.Quit(0);
    }
}

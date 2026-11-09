using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcMaterials(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC MATERIAL PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)&&Time.unscaledTimeAsDouble<until)yield return null;
        var actor=livingPeople.Values.FirstOrDefault(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18);Check(actor!=null,"An actual adult resident exists for material work");var group=livingGroups[actor.GroupId];paused=true;
        void Stand(V at)
        {TerrainHandoff.Coordinates(frame,at,out double x,out double z);actor.Motor.Place(x,z,VisibleGround(x,z,actor.Motor.Y-TerrainMotion.EyeHeightM));actor.Planet=frame.Position(x,z);actor.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));}
        var branch=scatterChunks.Values.Where(c=>c.Roots!=null).SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Branch&&!CollectedRock(p)&&RockPossession(p).MassKg<=4).OrderBy(p=>HumanDistance(p.Position,actor.Planet)).FirstOrDefault();
        Check(branch.Id!=0,"A real uncollected fallen branch exists in loaded scatter");Stand(branch.Position);
        var task=new NpcMaterialTask{Purpose="fuel",Plant=branch,Point=branch.Position,Natural=true,Needed=8};V target=actor.Planet;int before=npcMaterialHarvests;
        NpcGatherMaterial(actor,task,1,ref target);
        Check(npcMaterialHarvests==before+1&&inventoryCollected.Contains(branch.Id)&&Inventory.CollectedNatural.Contains(branch.Id),"Gathering the actual fallen branch uses the player's shared depletion ledger");
        var actual=actor.Belongings.Items.Concat(Inventory.Items).Single(p=>p.NaturalId==branch.Id);Check(actual.OwnerGroup==actor.GroupId&&actual.Kind==PossessionKind.Branch,"The gathered branch is a single real owned possession");
        var repeat=new NpcMaterialTask{Purpose="fuel",Plant=branch,Point=branch.Position,Natural=true,Needed=8};NpcGatherMaterial(actor,repeat,1,ref target);Check(npcMaterialHarvests==before+1,"The same natural branch cannot be gathered twice");
        if(!actor.Belongings.Items.Contains(actual)){Check(AcquireNpcItem(actor,actual),"The worker can physically retrieve its gathered branch");}
        var fire=new CampObject{Kind=CampKind.Fire,Position=actor.Planet,NpcGroup=actor.GroupId,NpcSite=group.Situation.Camp.Id,EmberSeconds=900};Camp.Objects.Add(fire);double mass=actual.MassKg;
        Check(NpcMaterialUse.Feed(actor.Belongings,fire,actual)&&Math.Abs(fire.FuelKg-mass)<1e-9,"A real harvested branch transfers its finite mass to the hearth");
        var fine=new NpcMaterialTask{Purpose="tinder"};npcMaterialSearchFrame=-1;Check(NpcFindMaterial(actor,fine,"tinder"),"The worker finds actual local tinder material");Stand(fine.Point);NpcGatherMaterial(actor,fine,10,ref target);
        var tinder=actor.Belongings.Items.FirstOrDefault(p=>actor.Belongings.Carried(p)&&CampRules.Tinder(p));Check(tinder!=null,"Gathering fine material creates a real carried bundle");
        Check(NpcMaterialUse.Feed(actor.Belongings,fire,tinder)&&fire.Burning,"Actually feeding the fine bundle relights live embers");
        // A real regional food plant, tested in its own fruiting season.
        var foodPlant=scatterChunks.Values.Where(c=>c.Roots!=null).SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.MidPlant||p.Kind==ScatterKind.GeneratedTree||IsSpecialist(p.Kind)).Take(3000)
            .Select(p=>(plant:p,product:NpcPlantProducts(p).FirstOrDefault(v=>v.KcalPerKg>0&&v.Toxicity<=.08&&v.Kind!=PossessionKind.EdibleRoot))).FirstOrDefault(p=>p.product!=null);
        Check(foodPlant.product!=null,"A real placed regional plant has a known edible above-ground product");
        double previousYear=skyYear;TerrainMapViewport.Degrees(foodPlant.plant.Position,out double latitude,out _);skyYear=Math.Floor(skyYear)+foodPlant.product.SeasonCentre-(latitude<0?.5:0);
        Stand(foodPlant.plant.Position);var foodTask=new NpcMaterialTask{Purpose="food",Plant=foodPlant.plant,Point=foodPlant.plant.Position,Product=foodPlant.product,Natural=true,Needed=35};NpcGatherMaterial(actor,foodTask,10,ref target);skyYear=previousYear;
        var meal=actor.Belongings.Items.FirstOrDefault(p=>actor.Belongings.Carried(p)&&Foraging.Food(p));Check(meal!=null,"The worker harvests the actual regional plant in its controlled fruiting season");
        double mealMass=meal.MassKg;var record=NpcPersonRecord(actor);record.Nutrition??=new PlayerSurvival();double consumed=NpcMaterialUse.Eat(actor.Belongings,record.Nutrition,meal);
        Check(consumed>0&&(!actor.Belongings.Items.Contains(meal)||meal.MassKg<mealMass),"Eating reduces the real harvested food quantity");
        NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);
        var core=HumanEquipment.Item(PossessionKind.SmallRock,actor.ResidentId,"material-probe-l",actor.Planet);core.Variant=ToolStone.Encode(ToolStoneMaterial.Chert,3);core.MassKg=.6;core.WidthM=.095;core.HeightM=.11;core.DepthM=.085;
        var hammer=HumanEquipment.Item(PossessionKind.SmallRock,actor.ResidentId,"material-probe-r",actor.Planet);hammer.Variant=ToolStone.Encode(ToolStoneMaterial.Granite,3);
        actor.Belongings.Items.Add(core);actor.Belongings.Items.Add(hammer);actor.Belongings.Hold(core.Id,out _);actor.Belongings.Hold(hammer.Id,out _);double oldMass=core.MassKg;actor.TaskWaiting=false;actor.Threat=null;actor.CurrentActivity=HumanActivity.Craft;
        var craft=new HumanActivitySample{Activity=HumanActivity.Craft,Position=actor.Planet};AdvanceNpcMaterialUse(actor,group,craft,20,actor.Planet);
        Check(core.MassKg<oldMass&&npcMaterialFlakes>0,"NPC knapping uses the same real stone recipe and removes mass from its core");
        LivingState.Copy().Validate();Inventory.Validate();actor.Belongings.Validate();Check(true,"Harvested goods, natural ledgers, actual food consumption and crafting validate together");
        File.WriteAllText(Path.Combine(output,"materials.json"),JsonUtility.ToJson(new NpcMaterialReceipt{Searches=npcMaterialSearches,Harvests=npcMaterialHarvests,Meals=npcMaterialMeals,Flakes=npcMaterialFlakes},true));
        paused=false;DisposeNpcMinds();
    }
    [Serializable] sealed class NpcMaterialReceipt{public int Searches,Harvests,Meals,Flakes;}
}

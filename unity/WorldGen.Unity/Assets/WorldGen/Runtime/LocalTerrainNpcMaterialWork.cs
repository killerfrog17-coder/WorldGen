using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcMaterialTask
    {
        public string Purpose="";public ScatterInstance Plant;public ForageProduct Product;public Possession Loose;
        public V Point;public bool Natural,Grass,Delivering;public double Work,SearchAt,Needed=30;
    }
    int npcMaterialSearchFrame=-1;int npcMaterialSearches,npcMaterialHarvests,npcMaterialFeeds,npcMaterialMeals,npcMaterialFlakes;
    double NpcWorkSeconds(float dt)=>dt*86400/(PlayerRules.Settings.RealMinutesPerDay*60);
    ForageProduct[] NpcPlantProducts(ScatterInstance p)
    {
        var flora=terrain.FloraRegion(walkingPatch.CommunityCell(p.Position));
        if(p.Kind==ScatterKind.GeneratedTree){foreach(var s in flora.Trees)if(s.MorphologyCode==(p.Variant&~7))return Foraging.Products(s);}
        else if(p.Kind==ScatterKind.MidPlant){foreach(var s in flora.MidPlants)if((s.MorphologyCode&~3)==(p.Variant&~3))return Foraging.Products(s,p.WidthM);}
        else if(IsSpecialist(p.Kind)){foreach(var s in flora.Specialists)if(s.MorphologyCode==p.Variant)return Foraging.Products(s);}
        return Array.Empty<ForageProduct>();
    }
    bool NpcProductReady(HumanActor actor,ScatterInstance plant,ForageProduct product)
    {
        TerrainMapViewport.Degrees(plant.Position,out double latitude,out _);
        if(product.Toxicity>.08||!product.Available(skyYear,latitude,CurrentWeather.TemperatureC))return false;
        if(product.Kind==PossessionKind.EdibleRoot&&!actor.Belongings.Items.Any(p=>actor.Belongings.Carried(p)&&p.Kind==PossessionKind.DiggingStick&&ItemUseRules.Condition(p)>0))return false;
        var record=Inventory.ToolResources.FirstOrDefault(r=>r.Id=="forage-"+plant.Id.ToString("x16")+"-"+product.Kind);
        return record==null||record.HarvestYear!=ForageYear||record.Harvests==0;
    }
    bool NpcFindMaterial(HumanActor actor,NpcMaterialTask task,string purpose)
    {
        if(npcMaterialSearchFrame==Time.frameCount||actor.SimulationAge<task.SearchAt)return false;
        npcMaterialSearchFrame=Time.frameCount;npcMaterialSearches++;task.SearchAt=actor.SimulationAge+3+HumanHash(actor.ResidentId)%20*.1;
        task.Purpose=purpose;task.Natural=false;task.Grass=false;task.Loose=null;task.Product=null;task.Work=0;
        bool Match(Possession p)=>purpose=="food"?Foraging.Food(p):purpose=="tinder"?CampRules.Tinder(p):purpose=="kit"?p.Kind==PossessionKind.HandDrillSet:NpcMaterialUse.Fuel(p)&&!CampRules.Tinder(p);
        var community=livingGroups[actor.GroupId];bool wild=actor.CurrentActivity==HumanActivity.GatherFuel||actor.CurrentActivity==HumanActivity.GatherPlants;
        if(community.MaterialStockRadius<=0||community.MaterialStockSite!=community.Situation.Camp.Id)
        {community.MaterialStockSite=community.Situation.Camp.Id;community.MaterialStockRadius=HumanCampLayout.Radius(community.Residents,community.Situation.Camp.Id,sourceWorld.Recipe.Seed)+5;}
        double campRadius=community.MaterialStockRadius;
        var loose=Inventory.Items.Where(p=>Match(p)&&NpcCanUseWorldItem(actor,p)&&(!wild||HumanDistance(NpcWorldItemPosition(p),community.Anchor)>campRadius)).OrderBy(p=>HumanDistance(actor.Planet,NpcWorldItemPosition(p))).FirstOrDefault();
        if(loose!=null){task.Loose=loose;task.Point=NpcWorldItemPosition(loose);task.Needed=5;return true;}
        if(purpose=="kit")return false;
        var here=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(actor.Motor.Z-originZ));double best=47*47;int considered=0;
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(here)>best)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(here)>best)continue;
                foreach(int i in group.Indices)
                {
                    var plant=chunk.Source.Instances[i];double distance=(chunk.Roots[i]-here).sqrMagnitude;if(distance>=best||CollectedRock(plant)||CampClearedPlant(plant))continue;
                    if(purpose=="fuel")
                    {
                        if(plant.Kind!=ScatterKind.Branch)continue;var branch=RockPossession(plant);if(branch.MassKg>4||branch.MassKg<.025)continue;
                        task.Plant=plant;task.Product=null;task.Point=plant.Position;task.Natural=true;task.Needed=8;best=distance;continue;
                    }
                    if(plant.Kind!=ScatterKind.GeneratedTree&&plant.Kind!=ScatterKind.MidPlant&&!IsSpecialist(plant.Kind))continue;
                    if(++considered>128)break;
                    var product=NpcPlantProducts(plant).FirstOrDefault(p=>(purpose=="food"?p.KcalPerKg>0:NpcMaterialUse.Tinder(p.Kind))&&NpcProductReady(actor,plant,p));
                    if(product==null)continue;
                    task.Plant=plant;task.Product=product;task.Point=plant.Position;task.Natural=true;task.Needed=product.Kind==PossessionKind.EdibleRoot?120:35;best=distance;
                }
            }
        }
        if(task.Natural)return true;
        if(purpose=="tinder")
        {
            var sample=walkingPatch.Sample(actor.Planet);var record=ToolResource("grass-"+GroundWorkKey(actor.Planet));
            if(CampGrassAvailable(here,sample)&&(record.HarvestYear!=ForageYear||record.Harvests<3)){task.Grass=true;task.Point=actor.Planet;task.Needed=40;return true;}
        }
        return false;
    }
    bool NpcHasMaterialTarget(NpcMaterialTask task)=>task.Loose!=null||task.Natural||task.Grass;
    bool NpcCarryProduced(HumanActor actor,Possession item)
    {
        var stock=actor.Belongings;stock.Items.Add(item);
        if(!NpcBelongings.Pack(stock,item)&&!stock.Hold(item.Id,out _))
        {stock.Items.Remove(item);NpcBelongings.Equip(stock,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);stock.Items.Add(item);stock.Hold(item.Id,out _);}
        item.Position=actor.Planet;item.OwnerPerson=0;item.OwnerGroup=actor.GroupId;item.OwnerHousehold=0;item.OwnerName=livingGroups[actor.GroupId].Residents.Name;
        if(Foraging.Food(item)&&sourceWorld.Prehistory.Foragers.Cultures[livingGroups[actor.GroupId].Residents.Culture].FoodSharing<=.65)item.OwnerHousehold=actor.Resident.Household;
        if(item.NaturalId!=0)
        {if(!stock.CollectedNatural.Contains(item.NaturalId))stock.CollectedNatural.Add(item.NaturalId);if(inventoryCollected.Add(item.NaturalId)&&!Inventory.CollectedNatural.Contains(item.NaturalId))Inventory.CollectedNatural.Add(item.NaturalId);scatterDirty=true;}
        PublishNpcGroundGoods(actor);return true;
    }
    void NpcGatherMaterial(HumanActor actor,NpcMaterialTask task,float dt,ref V target)
    {
        target=task.Point;actor.TaskDetail="going to gather "+(task.Purpose=="food"?"a known local food":task.Purpose=="tinder"?"fine fire material":task.Purpose=="kit"?"the fire-making kit":"fallen wood");actor.TaskWaiting=true;
        if(HumanDistance(actor.Planet,task.Point)>1.7)return;
        actor.TaskDetail="gathering "+(task.Loose?.Name??task.Product?.Species??(task.Grass?"dry grass":"fallen wood"));actor.TaskWaiting=false;task.Work+=NpcWorkSeconds(dt);if(task.Work<task.Needed)return;
        bool gathered=false;
        if(task.Loose!=null&&Inventory.Items.Contains(task.Loose))
        {
            gathered=AcquireNpcItem(actor,task.Loose);
            if(!gathered&&NpcCanUseWorldItem(actor,task.Loose)){NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);gathered=AcquireNpcItem(actor,task.Loose);}
        }
        else if(task.Natural)
        {
            if(task.Product==null){if(!CollectedRock(task.Plant))gathered=NpcCarryProduced(actor,RockPossession(task.Plant));}
            else if(!CollectedRock(task.Plant)&&NpcProductReady(actor,task.Plant,task.Product))
            {
                gathered=NpcCarryProduced(actor,Foraging.Gather(task.Product,actor.Planet));
                if(gathered){var record=ToolResource("forage-"+task.Plant.Id.ToString("x16")+"-"+task.Product.Kind);record.HarvestYear=ForageYear;record.Harvests=1;if(task.Plant.Kind==ScatterKind.Fungus&&inventoryCollected.Add(task.Plant.Id)){Inventory.CollectedNatural.Add(task.Plant.Id);scatterDirty=true;}}
            }
        }
        else if(task.Grass)
        {
            var record=ToolResource("grass-"+GroundWorkKey(task.Point));if(record.HarvestYear!=ForageYear){record.HarvestYear=ForageYear;record.Harvests=0;}
            if(record.Harvests<3&&CampGrassAvailable(actor.Root.transform.localPosition,walkingPatch.Sample(task.Point)))
            {gathered=NpcCarryProduced(actor,PortableTechnology.Create(PossessionKind.DryGrass,actor.Planet));if(gathered)record.Harvests++;}
        }
        if(gathered){npcMaterialHarvests++;if(task.Purpose=="fuel"||task.Purpose=="food"&&actor.Belongings.Items.Where(p=>actor.Belongings.Carried(p)&&Foraging.Food(p)).Sum(p=>p.MassKg)>=.4)task.Delivering=true;}
        task.Loose=null;task.Natural=task.Grass=false;task.Product=null;task.Work=0;task.SearchAt=actor.SimulationAge+(gathered?1:3);inventoryNearbyAt=0;
    }
    bool PrepareNpcMaterialTask(HumanActor actor,LivingGroup group,HumanActivitySample routine,float dt,ref V target)
    {
        if(dt<=0||actor.Resident.AgeYears<7)return false;
        var task=actor.MaterialTask??(actor.MaterialTask=new NpcMaterialTask());string purpose=routine.Activity==HumanActivity.GatherFuel?"fuel":routine.Activity==HumanActivity.GatherPlants?"food":"";
        if(routine.Activity==HumanActivity.TendFire&&group.TenderDuty?.Person==actor.ResidentId)
        {
            var fire=group.TendedFire;if(fire==null)return false;
            bool needsTinder=!fire.Burning&&fire.Materials.Where(CampRules.Tinder).Sum(p=>p.MassKg)<.025;
            bool needsFine=!fire.Burning&&fire.EmberSeconds<=0&&fire.Materials.Where(CampRules.Kindling).Sum(p=>p.MassKg)<.1;
            purpose=needsTinder?"tinder":needsFine?"kindling":fire.FuelKg<1.4?"fuel":!fire.Burning&&!NpcMaterialUse.HasIgnitionKit(actor.Belongings)?"kit":"";
            if(purpose=="kindling"&&NpcSplitKindling(actor,dt)){target=actor.Planet;return true;}
            bool Match(Possession p)=>purpose=="tinder"?CampRules.Tinder(p):purpose=="kindling"?CampRules.Kindling(p)&&NpcMaterialUse.Fuel(p):purpose=="fuel"?NpcMaterialUse.Fuel(p)&&!CampRules.Tinder(p):false;
            var material=actor.Belongings.Items.FirstOrDefault(p=>actor.Belongings.Carried(p)&&Match(p));
            if(material!=null)
            {
                target=NpcFireWorkPoint(actor,fire);actor.TaskDetail="carrying "+material.Name+" to the hearth";actor.TaskWaiting=false;
                if(HumanDistance(actor.Planet,fire.Position)<2.2)
                {task.Work+=NpcWorkSeconds(dt);if(task.Work>15&&NpcMaterialUse.Feed(actor.Belongings,fire,material)){npcMaterialFeeds++;task.Work=0;InvalidateCampVisual(fire);}}
                return true;
            }
            if(purpose.Length==0)
            {
                target=NpcFireWorkPoint(actor,fire);actor.TaskWaiting=false;actor.TaskDetail=fire.Burning?"checking the hearth":"working the fire drill";
                if(HumanDistance(actor.Planet,fire.Position)<2.2)
                {
                    if(!fire.Burning&&NpcMaterialUse.HasIgnitionKit(actor.Belongings)&&group.BandHas(9)&&CampRules.IgniteReason(fire,true).Length==0)
                    {task.Work+=NpcWorkSeconds(dt);if(task.Work>=150){CampRules.Ignite(fire,true);InvalidateCampVisual(fire);}}
                    if(fire.Burning&&fire.FuelKg>=1.3){group.TenderDuty=null;group.NextTend=Time.unscaledTimeAsDouble+10;task.Work=0;}
                }
                return true;
            }
            if(purpose=="kindling")purpose="fuel";
        }
        if(purpose.Length==0)return false;
        if(routine.Activity==HumanActivity.GatherFuel||routine.Activity==HumanActivity.GatherPlants)
        {
            if(actor.Belongings.Items.Any(p=>actor.Belongings.Carried(p)&&NpcMaterialUse.Fuel(p)&&!CampRules.Tinder(p))&&routine.Activity==HumanActivity.GatherFuel)task.Delivering=true;
            if(actor.Belongings.Items.Where(p=>actor.Belongings.Carried(p)&&Foraging.Food(p)).Sum(p=>p.MassKg)>=.4&&routine.Activity==HumanActivity.GatherPlants)task.Delivering=true;
            if(task.Delivering)
            {
                target=LivingHouse(actor)?.Position??group.Anchor;actor.TaskWaiting=false;actor.TaskDetail="bringing gathered materials back to the household";
                if(HumanDistance(actor.Planet,target)<2)
                {
                    foreach(var item in actor.Belongings.Items.Where(p=>actor.Belongings.Carried(p)&&(purpose=="food"?Foraging.Food(p):NpcMaterialUse.Fuel(p))).ToArray())
                    {item.Place=PossessionPlace.Ground;item.ContainerId="";item.Position=actor.Planet;item.GroundRotation=new V(90,0,0);item.GroundOffsetM=.04;}
                    PublishNpcGroundGoods(actor);task.Delivering=false;task.SearchAt=actor.SimulationAge+2;
                }
                return true;
            }
        }
        if(task.Purpose!=purpose){task.Purpose=purpose;task.Loose=null;task.Natural=task.Grass=false;task.SearchAt=0;task.Work=0;}
        if(!NpcHasMaterialTarget(task)&&!NpcFindMaterial(actor,task,purpose)){actor.TaskWaiting=true;actor.TaskDetail="looking for reachable "+purpose+"; none gathered yet";return true;}
        if(NpcHasMaterialTarget(task))NpcGatherMaterial(actor,task,dt,ref target);return true;
    }
    bool NpcSplitKindling(HumanActor actor,float dt)
    {
        var stock=actor.Belongings;var branch=stock.Items.FirstOrDefault(p=>stock.Carried(p)&&p.Kind==PossessionKind.Branch&&p.WidthM<=.06&&p.WidthM>.025);if(branch==null)return false;
        var job=stock.Crafts.FirstOrDefault(j=>j.RecipeId=="split-kindling");
        if(job==null||!job.Running){if(!PortableCrafting.Plan(stock,stock.Items.Where(stock.Carried),PortableCrafting.Recipe("split-kindling"),branch.Id,out var plan,out _))return false;job=PortableCrafting.Start(stock,plan);}
        actor.TaskDetail="splitting actual wood into thin kindling";actor.TaskWaiting=false;
        var result=StoneCrafting.Advance(stock,job,dt,PlayerRules.Settings);
        if(result!=null&&result.Place==PossessionPlace.Ground){stock.Items.Remove(result);NpcCarryProduced(actor,result);}return true;
    }
    V NpcFireWorkPoint(HumanActor actor,CampObject fire)
    {TerrainHandoff.Coordinates(frame,fire.Position,out double x,out double z);double angle=HumanHash(actor.ResidentId)%628/100d;return frame.Position(x+Math.Cos(angle)*1.3,z+Math.Sin(angle)*1.3);}
    void AdvanceNpcMaterialUse(HumanActor actor,LivingGroup group,HumanActivitySample routine,float dt,V target)
    {
        if(dt<=0||actor.TaskWaiting||HumanDistance(actor.Planet,target)>1.8||actor.Threat!=null)return;
        var stock=actor.Belongings;var record=NpcPersonRecord(actor);
        if(routine.Activity==HumanActivity.PrepareFood&&PlayerRules.SolarDays<record.NextMealSolarDay){actor.TaskWaiting=true;actor.TaskDetail="resting after the last meal";return;}
        if(routine.Activity==HumanActivity.PrepareFood&&PlayerRules.SolarDays>=record.NextMealSolarDay)
        {
            var food=stock.Items.FirstOrDefault(p=>stock.Carried(p)&&Foraging.Food(p));if(food==null)return;
            actor.NpcWorkDone+=NpcWorkSeconds(dt);if(actor.NpcWorkDone<90)return;actor.NpcWorkDone=0;
            record.Nutrition??=new PlayerSurvival();double mass=NpcMaterialUse.Eat(stock,record.Nutrition,food);
            if(mass>0){npcMaterialMeals++;record.NextMealSolarDay=PlayerRules.SolarDays+(2+HumanHash(actor.ResidentId)%30*.1)/24;actor.TaskDetail="ate an actual portion of "+food.Name;}
        }
        if(routine.Activity==HumanActivity.FetchWater&&group.Resources?.HasWater==true&&HumanDistance(actor.Planet,group.Resources.Water)<2.5)
        {actor.NpcWorkDone+=NpcWorkSeconds(dt);if(actor.NpcWorkDone>=30){record.Nutrition??=new PlayerSurvival();PlayerMetabolism.Hydrate(record.Nutrition,.35);actor.NpcWorkDone=-600;actor.TaskDetail="drinking at the actual water source";}}
        if(routine.Activity!=HumanActivity.Craft)return;
        if(stock.Items.Count(p=>p.Kind==PossessionKind.StoneFlake)>=3){actor.TaskDetail="finished the needed cutting flakes";return;}
        var stones=NpcBelongings.ForTask(stock,"WorkKnapping");if(stones.Length!=2)return;
        var job=stock.Crafts.FirstOrDefault(j=>j.Action==ItemCraftAction.Flake)??StoneCrafting.Start(stock,stones[0].Id,stones[1].Id,out _);if(job==null)return;
        var flake=StoneCrafting.Advance(stock,job,dt,PlayerRules.Settings);
        if(flake!=null){NpcOwnership.Assign(flake,actor.Resident,actor.GroupId);flake.Position=actor.Planet;NpcBelongings.Pack(stock,flake);PublishNpcGroundGoods(actor);npcMaterialFlakes++;actor.TaskDetail="struck a real cutting flake from the core";}
    }
}

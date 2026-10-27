using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    SurvivalState survival;bool survivalOpen,survivalNewSession;int survivalTab;Vector2 survivalScroll;long survivalSelected;
    string survivalMessage="",survivalAction="";double survivalMessageUntil,survivalActionRemaining,survivalActionTotal;
    Action survivalCompletion;V survivalActionOrigin;bool survivalSleeping;double survivalSleepRemaining;bool survivalPriorGrounded;double survivalPriorFallSpeed;double survivalRefreshAt,survivalLastX,survivalLastZ;
    readonly Dictionary<long,GameObject> survivalSiteObjects=new Dictionary<long,GameObject>();
    LocalScatter survivalLattice;
    readonly Dictionary<string,SurvivalNode> survivalNodes=new Dictionary<string,SurvivalNode>();
    readonly List<Material> survivalMaterials=new List<Material>();
    sealed class SurvivalNode{public string Key,Item;public int Count;public V Position;public GameObject Root;}
    SurvivalNode survivalNodeTarget;SurvivalCorpse survivalCorpseTarget;SurvivalSite survivalSiteTarget;
    GameObject heldSurvivalTool;long heldSurvivalId=-1;float survivalAttackClock;double survivalAttackAt;
    public SurvivalState Survival=>survival;
    bool SurvivalBlocked=>survivalOpen||survival?.Dead==true;
    bool SurvivalBusy=>survivalCompletion!=null||survivalSleeping;
    void InitializeSurvival()
    {
        survivalNewSession=playState?.Survival==null;
        survival=playState?.Survival??new SurvivalState{Seed=sourceWorld.Recipe.Seed};survival.Validate();if(playState!=null){playState.Version="play-session-2";playState.Survival=survival;}
        survivalLastX=motion.X;survivalLastZ=motion.Z;
        if(survival.Mode==SurvivalMode.Survival)motion.Flying=false;
        InitializeRagdolls();RefreshSurvivalScene();
    }
    void SurvivalNotice(string message){survivalMessage=message;survivalMessageUntil=Time.realtimeSinceStartupAsDouble+5;}
    void OpenSurvival(int tab=0)
    {
        survivalOpen=true;survivalTab=tab;chatOpen=false;survivalScroll=Vector2.zero;CaptureMouse();
    }
    bool HandleSurvivalKeys()
    {
        if(survival==null||paused||mapOpen||ecologyOpen||chatOpen)return false;var k=ActiveKeyboard;
        if(k==null)return survivalOpen;
        if(k.iKey.wasPressedThisFrame||k.tabKey.wasPressedThisFrame)
        {survivalOpen=!survivalOpen;if(survivalOpen)survivalTab=0;CaptureMouse();return true;}
        if(k.bKey.wasPressedThisFrame){OpenSurvival(2);return true;}
        if(k.escapeKey.wasPressedThisFrame&&(survivalOpen||SurvivalBusy))
        {survivalOpen=false;CancelSurvivalAction();CaptureMouse();return true;}
        if(survivalOpen||survival.Dead)return true;
        if(k.eKey.wasPressedThisFrame)
        {
            FindSurvivalTarget();
            if(survivalNodeTarget!=null)GatherSurvivalNode(survivalNodeTarget);
            else if(survivalCorpseTarget!=null||survivalSiteTarget!=null)OpenSurvival(3);
            else TryDrinkAtWater();
        }
        var mouse=Mouse.current;
        if(mouse!=null&&mouse.leftButton.wasPressedThisFrame)AttackSurvival(false);
        if(mouse!=null&&mouse.rightButton.wasPressedThisFrame)AttackSurvival(true);
        return false;
    }
    void UpdateSurvival()
    {
        if(survival==null||!Ready)return;
        float dt=Math.Min(.1f,Time.unscaledDeltaTime);bool stopped=!LocalTimeRunning;
        double dx=motion.X-survivalLastX,dz=motion.Z-survivalLastZ;survivalLastX=motion.X;survivalLastZ=motion.Z;
        double speed=dt>0?Math.Min(12,Math.Sqrt(dx*dx+dz*dz)/dt):0;
        if(!stopped)
        {
            double shelter=0,insulation=survival.Inventory.Items.Where(i=>i.Worn&&SurvivalItems.Get(i.Item).Insulation>0).GroupBy(i=>i.Item).Sum(g=>SurvivalItems.Get(g.Key).Insulation),warmth=0;
            foreach(var site in survival.Sites)
            {
                double distance=(site.Position-Position).Length*frame.RadiusM;
                var blueprint=SurvivalCrafting.Shelters.FirstOrDefault(b=>b.Id==site.Kind);
                if(site.Progress>=1&&blueprint!=null&&distance<blueprint.Radius){shelter=Math.Max(shelter,blueprint.Shelter);insulation=Math.Max(insulation,blueprint.Insulation);}
                if(site.Kind=="hearth"&&site.Lit&&site.FuelHours>0&&distance<5)warmth=Math.Max(warmth,28*Math.Max(0,1-distance/5));
            }
            double playableSeconds=dt*86400/((playState?.Settings.RealMinutesPerDay??60)*60);
            if(survivalSleeping)
            {
                double extra=Math.Min(survivalSleepRemaining,playableSeconds*59);
                playState?.Accumulate(extra*((playState?.Settings.RealMinutesPerDay??60)*60)/86400);
                playableSeconds+=extra;survivalSleepRemaining-=playableSeconds;
                if(survivalSleepRemaining<=0||survival.Fatigue<.02){survivalSleeping=false;if(humanPlayer!=null)humanPlayer.ActionRemaining=0;}
            }
            if(survival.Mode==SurvivalMode.Survival&&motion.Grounded&&!survivalPriorGrounded&&survivalPriorFallSpeed< -7)
            {double hurt=Math.Pow(-survivalPriorFallSpeed-6,2)*1.3;survival.Health=Math.Max(0,survival.Health-hurt);SurvivalNotice("Hurt by the fall.");}
            survivalPriorGrounded=motion.Grounded;survivalPriorFallSpeed=motion.VerticalSpeed;
            bool water=false;if(walkingPatch!=null){var sample=walkingPatch.Sample(Position);water=!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.5&&HeightAboveGround<1.8;}
            SurvivalPhysiology.Advance(survival,playableSeconds,new SurvivalEnvironment{TemperatureC=weatherScreen!=null?CurrentWeather.TemperatureC:18,
                WindMps=weatherScreen!=null?CurrentWeather.GustMps:1,RainMmPerHour=CurrentWeather.RainMmPerHour,SpeedMps=motion.Flying?0:speed,
                CarriedKg=survival.Inventory.Mass,Shelter=shelter,Insulation=insulation,FireWarmth=warmth,InWater=water,
                Sprinting=speed>4,Resting=SurvivalBusy&&speed<.1,Sleeping=survivalSleeping});
            if(survivalCompletion!=null)
            {
                if((Position-survivalActionOrigin).Length*frame.RadiusM>1.5)CancelSurvivalAction();
                else {survivalActionRemaining-=playableSeconds;if(survivalActionRemaining<=0){var complete=survivalCompletion;survivalCompletion=null;survivalAction="";complete();RefreshSurvivalScene();}}
            }
        }
        if(survival.Dead&&(!survivalOpen||SurvivalBusy)){CancelSurvivalAction();survivalSleeping=false;survivalOpen=true;CaptureMouse();}
        motion.SpeedFactor=survival.Mode==SurvivalMode.Survival?SurvivalPhysiology.MovementFactor(survival):1;
        if(Time.realtimeSinceStartupAsDouble>=survivalRefreshAt){survivalRefreshAt=Time.realtimeSinceStartupAsDouble+1;RefreshSurvivalScene();}
        UpdateSurvivalHeldTool(dt);UpdateRagdolls(stopped?0:dt);
    }
    void BeginSurvivalAction(string label,double seconds,Action complete)
    {
        if(SurvivalBusy){SurvivalNotice("Finish or cancel the current action first.");return;}
        survivalAction=label;survivalActionRemaining=survivalActionTotal=survival.Mode==SurvivalMode.Creative?1:seconds;
        survivalCompletion=complete;survivalActionOrigin=Position;survivalOpen=false;CaptureMouse();TriggerHumanSurvivalAction(label.StartsWith("Gathering",StringComparison.Ordinal)?"Pick up":"Interact");
        if(humanPlayer!=null&&humanPlayer.Action>=0)humanPlayer.ActionRemaining=(float)Math.Max(.5,survivalActionTotal*(playState?.Settings.RealMinutesPerDay??60)*60/86400);
    }
    void CancelSurvivalAction(){survivalCompletion=null;survivalAction="";survivalSleeping=false;if(humanPlayer!=null){humanPlayer.Action=-1;humanPlayer.ActionRemaining=0;}}
    void RefreshSurvivalScene()
    {
        if(survival==null||frame==null)return;
        var liveSites=new HashSet<long>(survival.Sites.Select(s=>s.Id));
        foreach(var id in survivalSiteObjects.Keys.Where(id=>!liveSites.Contains(id)).ToArray()){Destroy(survivalSiteObjects[id]);survivalSiteObjects.Remove(id);}
        foreach(var site in survival.Sites)
        {
            double d=(site.Position-Position).Length*frame.RadiusM;
            if(d>250){if(survivalSiteObjects.TryGetValue(site.Id,out var old)){Destroy(old);survivalSiteObjects.Remove(site.Id);}continue;}
            if(survivalSiteObjects.TryGetValue(site.Id,out var existing)&&site.Progress>=1&&existing.name.EndsWith("unfinished",StringComparison.Ordinal)){Destroy(existing);survivalSiteObjects.Remove(site.Id);}
            if(!survivalSiteObjects.TryGetValue(site.Id,out var root)){root=CreateSurvivalStructure(site);survivalSiteObjects[site.Id]=root;}
            TerrainHandoff.Coordinates(frame,site.Position,out double x,out double z);root.transform.localPosition=new Vector3((float)(x-originX),(float)VisibleGround(x,z,Ground(x,z)),(float)(z-originZ));
            var fire=root.transform.Find("Fire");if(fire!=null)fire.gameObject.SetActive(site.Lit&&site.FuelHours>0&&site.Progress>=1);
        }
        RefreshSurvivalNodes();
    }
    bool ResourceAvailable(string key,int total)=>!survival.Resources.Any(r=>r.Key==key&&survival.Days-r.UsedDay<30&&r.Count>=total);
    void RefreshSurvivalNodes()
    {
        survivalLattice??=new LocalScatter(terrain,HorizontalScale);
        var keep=new HashSet<string>();
        foreach(var tile in survivalLattice.Around(Position,48,true))for(int slot=0;slot<16;slot++)
        {
            ulong address=tile.Id(15,slot);uint hash=Rng.Mix((uint)address^(uint)(address>>32)^(uint)sourceWorld.Recipe.Seed);
            V point=survivalLattice.Position(tile,((slot%4)+.25+(hash%101)/202d)/4,((slot/4)+.25+((hash>>8)%101)/202d)/4);
            if((point-Position).Length*frame.RadiusM>48)continue;
            TerrainHandoff.Coordinates(frame,point,out double px,out double pz);
            var sample=walkingPatch.Sample(point);
            if(sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.02)continue;
            var habitat=sourceWorld.Ecology.Habitat[sample.Cell];
            bool woody=habitat.Biome==Biome.TemperateForest||(habitat.Biome==Biome.TropicalRainforest||habitat.Biome==Biome.TropicalSeasonalForest)||habitat.Biome==Biome.BorealForest||habitat.Biome==Biome.Savanna;
            bool vegetated=habitat.AridityMilli>180&&habitat.MeanTenthsC>-40&&habitat.Biome!=Biome.Ice;
            string[] pool=woody?new[]{"stick","stick","stone","fibre","grass","tinder","bark","berries","roots","nuts","resin"}:
                vegetated?new[]{"stone","fibre","grass","tinder","roots","stone"}:new[]{"stone","stone","stone","stone"};
            string item=pool[hash%(uint)pool.Length];
            var bedrock=GroundEnvironment.For(sourceWorld).Cell(sample.Cell).Bedrock;
            if(hash%19==0&&(bedrock==RockFamily.Limestone||bedrock==RockFamily.Mudstone))item="flint";
            if(hash%157==0&&(bedrock==RockFamily.Metamorphic||bedrock==RockFamily.Mudstone))item="pyrite";
            string key=address.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int count=item=="flint"||item=="pyrite"?1:2+(int)(hash%3);
            if(!ResourceAvailable(key,count))continue;
            double floor=VisibleGround(px,pz,Ground(px,pz));if((item=="grass"||item=="fibre"||item=="berries"||item=="roots")&&(Math.Abs(Ground(px+1,pz)-floor)>1||Math.Abs(Ground(px,pz+1)-floor)>1))continue;
            keep.Add(key);if(!survivalNodes.TryGetValue(key,out var node))
            {node=new SurvivalNode{Key=key,Item=item,Position=point,Count=count};node.Root=CreateSurvivalPickup(item);survivalNodes[key]=node;}
            node.Root.transform.localPosition=new Vector3((float)(px-originX),(float)floor+.08f,(float)(pz-originZ));
        }
        foreach(var key in survivalNodes.Keys.Where(k=>!keep.Contains(k)).ToArray()){Destroy(survivalNodes[key].Root);survivalNodes.Remove(key);}
    }
    void FindSurvivalTarget()
    {
        survivalNodeTarget=null;survivalCorpseTarget=null;survivalSiteTarget=null;double nearest=3.7;
        foreach(var node in survivalNodes.Values){double d=(node.Position-Position).Length*frame.RadiusM;if(d<nearest){nearest=d;survivalNodeTarget=node;}}
        foreach(var corpse in survival.Corpses){double d=(corpse.Position-Position).Length*frame.RadiusM;if(d<nearest){nearest=d;survivalNodeTarget=null;survivalCorpseTarget=corpse;}}
        foreach(var site in survival.Sites){double d=(site.Position-Position).Length*frame.RadiusM;if(d<nearest){nearest=d;survivalNodeTarget=null;survivalCorpseTarget=null;survivalSiteTarget=site;}}
    }
    void GatherSurvivalNode(SurvivalNode node)
    {
        BeginSurvivalAction("Gathering "+SurvivalItems.Get(node.Item).Name.ToLowerInvariant(),8,()=>
        {
            if(!ResourceAvailable(node.Key,node.Count)){SurvivalNotice("This patch has been gathered.");return;}
            if(!survival.Inventory.TryAdd(node.Item,1,out var why)){SurvivalNotice(why);return;}
            var use=survival.Resources.FirstOrDefault(r=>r.Key==node.Key);if(use==null){use=new SurvivalResourceUse{Key=node.Key};survival.Resources=survival.Resources.Concat(new[]{use}).ToArray();}
            if(survival.Days-use.UsedDay>=30)use.Count=0;use.Count++;use.UsedDay=survival.Days;SurvivalNotice("Gathered "+SurvivalItems.Get(node.Item).Name.ToLowerInvariant()+".");
        });
    }
    void TryDrinkAtWater()
    {
        var point=frame.Position(motion.X+Math.Sin(heading*Math.PI/180)*1.8,motion.Z+Math.Cos(heading*Math.PI/180)*1.8);var sample=walkingPatch.Sample(point);
        bool fresh=!sourceWorld.Hydrology.OceanConnected[sample.Cell]&&!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>=sample.ElevationM-.1;
        if(!fresh){SurvivalNotice("No fresh water or gatherable object within reach.");return;}
        BeginSurvivalAction("Collecting fresh water",8,()=>
        {
            if(survival.Inventory.Has("empty-waterskin"))
            {var trial=survival.Inventory.Copy();trial.Remove("empty-waterskin",1);if(trial.TryAdd("raw-water",1,out var why))survival.Inventory=trial;else{SurvivalNotice(why);return;}}
            else
            {
                survival.WaterDeficitL=Math.Max(0,survival.WaterDeficitL-.5);
                if(Rng.Value(survival.Seed,"survival-water-risk",survival.ConsumptionEvents++)<.04)survival.Illness=Math.Min(1,survival.Illness+.25);
            }
            // Untreated freshwater has risk, not a guaranteed immediate illness.
            SurvivalNotice("Collected untreated freshwater.");
        });
    }
    bool HasSurvivalFire=>survival.Sites.Any(s=>s.Kind=="hearth"&&s.Progress>=1&&s.Lit&&s.FuelHours>0&&(s.Position-Position).Length*frame.RadiusM<5);
    public static bool SurvivalHitInReach(Vector3 playerOrigin,Vector3 hitPoint,double reach)
        =>reach>0&&!double.IsNaN(reach)&&!double.IsInfinity(reach)&&(hitPoint-playerOrigin).sqrMagnitude<=reach*reach+1e-6;
    void AttackSurvival(bool thrown)
    {
        if(SurvivalBusy||Time.realtimeSinceStartupAsDouble<survivalAttackAt||survival.Dead)return;
        var weapon=survival.Inventory.Held;double damage=weapon?.Damage??3;if(damage<=0)damage=3;
        if(thrown&&weapon!=null&&weapon.Id!="stone-spear"&&weapon.Id!="wood-spear"&&weapon.Id!="throwing-stick"&&weapon.Id!="sling"&&weapon.Id!="stone")return;
        if(thrown&&weapon==null)return;
        if(thrown&&weapon.Id=="sling"&&!survival.Inventory.Has("stone")){SurvivalNotice("A stone is needed for the sling.");return;}
        if(survival.Mode==SurvivalMode.Survival&&survival.Stamina<8){SurvivalNotice("Too exhausted to attack.");return;}
        survivalAttackAt=Time.realtimeSinceStartupAsDouble+(thrown?.9:.65);survivalAttackClock=1;
        if(survival.Mode==SurvivalMode.Survival)survival.Stamina=Math.Max(0,survival.Stamina-8);
        TriggerHumanSurvivalAction("Attack");if(humanPlayer!=null)humanPlayer.TargetYaw=heading;
        Vector3 from=view.transform.position,dir=view.transform.forward;
        // Reach starts at upper-body height, rather than the orbiting camera or
        // the eyes: short tools must still be able to reach ground-level prey.
        Vector3 playerFrom=transform.TransformPoint(new Vector3((float)(motion.X-originX),(float)motion.Y-.55f,(float)(motion.Z-originZ)));
        Vector3 aimedPoint=from+dir*60;double reach=thrown?45:weapon!=null&&weapon.Reach>0?weapon.Reach:1.4;
        Animal hit=null;float best=float.PositiveInfinity;
        foreach(var animal in animals.Values)
        {
            if(animal.Dead)continue;var bounds=animal.Renderer.bounds;
            if(!bounds.IntersectRay(new Ray(from,dir),out float distance)||distance>best)continue;
            Vector3 target=from+dir*distance;if(!SurvivalHitInReach(playerFrom,target,reach))continue;
            bool blocked=false;for(int i=1;i<=6;i++){var p=transform.InverseTransformPoint(Vector3.Lerp(playerFrom,target,i/6f));if(p.y<VisibleGround(p.x+originX,p.z+originZ,p.y)-.1){blocked=true;break;}}
            if(blocked)continue;hit=animal;best=distance;aimedPoint=target;
        }
        if(thrown)
        {
            string item=weapon.Id=="sling"?"stone":weapon.Id;
            if(survival.Mode!=SurvivalMode.Creative&&!(weapon.Id=="sling"?survival.Inventory.Remove(item,1):survival.Inventory.RemoveStack(survival.Inventory.Equipped,1)))return;
            SpawnSurvivalProjectile(item,playerFrom,(aimedPoint-playerFrom).normalized,damage*1.7);
        }
        else if(hit!=null)DamageSurvivalAnimal(hit,damage,dir);
    }
    readonly Dictionary<string,Taxon> survivalTaxa=new Dictionary<string,Taxon>();
    Taxon SurvivalTaxon(Animal animal)
    {
        if(animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length)return sourceWorld.Ecology.Taxa[animal.Spawn.Taxon];
        if(survivalTaxa.Count==0&&animalProfiles!=null){var taxa=EarthFaunaPack.Create();foreach(var pair in FaunaPrototypePlanner.BindProfiles(taxa,animalProfiles))survivalTaxa[pair.Value.slug]=taxa[pair.Key];}
        return survivalTaxa.TryGetValue(animal.Spawn.Profile.slug,out var t)?t:null;
    }
    void UpdateSurvivalAnimalThreat(Animal animal)
    {
        if(survival==null||survivalOpen||paused||mapOpen||ecologyOpen||chatOpen||survival.Mode!=SurvivalMode.Survival||survival.Dead||motion.Flying||Time.realtimeSinceStartupAsDouble<animal.NextThreat)return;
        animal.NextThreat=Time.realtimeSinceStartupAsDouble+.4;
        var profile=animal.Spawn.Profile;if(profile.tiny||profile.medium=="water")return;
        double dx=motion.X-animal.X,dz=motion.Z-animal.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        bool crouch=ActiveKeyboard!=null&&ActiveKeyboard.ctrlKey.isPressed;
        double alarm=Math.Max(8,Math.Min(30,profile.lengthM*15))*(crouch?.4:1);
        if(distance>alarm)return;
        double middle=VisibleGround((animal.X+motion.X)*.5,(animal.Z+motion.Z)*.5,animal.GroundY);
        if(middle>Math.Max(animal.Root.transform.localPosition.y+.8,motion.Y))return;
        var taxon=SurvivalTaxon(animal);double mass=taxon?.MassKg??1;
        bool predator=mass>12&&taxon!=null&&(taxon.Eats&Food.Vertebrate)!=0&&(taxon.Eats&(Food.Grass|Food.Browse|Food.Fruit|Food.Seed))==0;
        bool defensive=mass>250&&distance<Math.Max(2.5,profile.lengthM);
        bool attack=(predator&&distance<10||defensive)&&!HasSurvivalFire;
        animal.TargetHeading=(float)(Math.Atan2(attack?dx:-dx,attack?dz:-dz)*180/Math.PI);
        animal.TargetSpeed=(float)profile.runMps;animal.NextDecision=animal.Life+2;
        if(attack&&distance<Math.Max(1.1,profile.lengthM*.55)&&Time.realtimeSinceStartupAsDouble>=animal.NextBite)
        {
            animal.NextBite=Time.realtimeSinceStartupAsDouble+2.3;
            survival.Health=Math.Max(0,survival.Health-Math.Min(45,5+Math.Sqrt(mass)*1.8));survival.Bleeding=Math.Min(10,survival.Bleeding+.12);
            SurvivalNotice("Struck by "+profile.name.ToLowerInvariant()+".");
        }
    }
    double SurvivalAnimalMass(Animal animal)
    {
        var taxon=SurvivalTaxon(animal);if(taxon!=null&&SurvivalMath.Finite(taxon.MassKg)&&taxon.MassKg>0)return taxon.MassKg;
        if(animal.Spawn.Profile.derivedFirefly)return .00002; // 20 mg authored proxy, not a 20 g unknown mammal.
        return Math.Max(.02,Math.Pow(animal.Spawn.Profile.lengthM,3)*10);
    }
    void DamageSurvivalAnimal(Animal animal,double damage,Vector3 direction)
    {
        if(animal.Dead)return;double mass=SurvivalAnimalMass(animal);
        if(animal.Health<=0)animal.Health=SurvivalPhysiology.AnimalVitality(mass);
        animal.Health-=damage;animal.TargetHeading=(float)(Math.Atan2(animal.X-motion.X,animal.Z-motion.Z)*180/Math.PI);animal.TargetSpeed=(float)animal.Spawn.Profile.runMps;animal.NextDecision=animal.Life+7;
        if(animal.Health>0){SurvivalNotice("Wounded "+animal.Spawn.Profile.name.ToLowerInvariant()+".");return;}
        animal.Dead=true;var corpse=SurvivalPhysiology.Carcass(animal.Spawn.Profile.slug,animal.Spawn.Id,animal.Planet,mass,animal.Heading);
        corpse.AppearanceHash=animal.Spawn.Hash;corpse.CoatSeed=animal.Spawn.CoatSeed;corpse.ColdC=animal.Spawn.ColdC;corpse.Aridity=animal.Spawn.Aridity;
        animal.GroundY=animal.Root.transform.localPosition.y;corpse.RootOffsetM=animal.GroundY-Ground(animal.X,animal.Z);nextAnimalPlan=0;
        survival.Corpses=survival.Corpses.Concat(new[]{corpse}).ToArray();StartAnimalRagdoll(animal,corpse,direction);SurvivalNotice(animal.Spawn.Profile.name+" killed.");
    }
    void UpdateSurvivalHeldTool(float dt)
    {
        if(humanPlayer==null)return;var stack=survival.Inventory.Items.FirstOrDefault(s=>s.Id==survival.Inventory.Equipped);
        long id=stack?.Id??0;
        if(id!=heldSurvivalId)
        {
            heldSurvivalId=id;if(heldSurvivalTool!=null)Destroy(heldSurvivalTool);
            if(stack!=null)
            {
                heldSurvivalTool=CreateSurvivalTool(stack.Item);var hand=humanPlayer.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="hand.R"||t.name=="hand_r"||t.name.Contains("hand")&&t.name.EndsWith(".R",StringComparison.Ordinal));
                heldSurvivalTool.transform.SetParent(hand??humanPlayer.Root.transform,false);heldSurvivalTool.transform.localPosition=hand!=null?Vector3.zero:new Vector3(.3f,1,0);
                var scale=heldSurvivalTool.transform.parent.lossyScale;
                heldSurvivalTool.transform.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
                heldSurvivalTool.transform.rotation=humanPlayer.Root.transform.rotation*Quaternion.Euler(0,0,-12);
            }
        }
        survivalAttackClock=Math.Max(0,survivalAttackClock-dt*2);
    }
    void DisposeSurvival()
    {
        DisposeRagdolls();foreach(var root in survivalSiteObjects.Values)if(root!=null)Destroy(root);foreach(var node in survivalNodes.Values)if(node.Root!=null)Destroy(node.Root);
        if(heldSurvivalTool!=null)Destroy(heldSurvivalTool);foreach(var material in survivalMaterials)if(material!=null)Destroy(material);
        survivalSiteObjects.Clear();survivalNodes.Clear();survivalMaterials.Clear();
    }
}

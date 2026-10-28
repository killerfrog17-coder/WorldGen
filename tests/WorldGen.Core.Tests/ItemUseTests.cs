using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

static class ItemUseTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        V ground=new V(1,0,0);
        var light=new Possession{Id="light",Kind=PossessionKind.SmallRock,MassKg=.05,Position=ground};var heavy=light.Copy();heavy.Id="heavy";heavy.MassKg=.8;
        Check(ItemUseRules.ThrowSpeed(light,1)>ItemUseRules.ThrowSpeed(heavy,1),"Small rocks launch faster than large rocks");
        Check(ItemUseRules.ImpactDamage(.8,20,0)>ItemUseRules.ImpactDamage(.05,20,0),"At a fixed speed heavier projectiles cause more damage");
        Check(ItemUseRules.KineticEnergy(.5,20)==100,"Kinetic energy uses physical mass and squared speed");
        Check(ItemUseRules.ThrowSpeed(light,0)<ItemUseRules.ThrowSpeed(light,1),"Charge changes launch work");
        Check(ItemUseRules.ThrowSpeed(light,1,0)<ItemUseRules.ThrowSpeed(light,1,1),"Fatigue limits launch effort");
        for(double mass=.001;mass<20;mass*=1.7)
        {var p=light.Copy();p.MassKg=mass;double v=ItemUseRules.ThrowSpeed(p,1);Check(v<=28&&ItemUseRules.KineticEnergy(mass,v)<=90+1e-9&&mass*v<=11+1e-9,"Hand-thrown stones obey velocity/work/impulse budgets");}
        Check(ItemUseRules.DragAcceleration(.05,.002,20)>ItemUseRules.DragAcceleration(.8,.002,20),"Air drag depends on mass as well as frontal area");
        var arrow=PortableTechnology.Create(PossessionKind.Arrow,ground);Check(ItemUseRules.BowSpeed(arrow,.5)<ItemUseRules.BowSpeed(arrow,1),"Bow draw affects arrow energy");
        Check(ItemUseRules.KineticEnergy(arrow.MassKg,ItemUseRules.BowSpeed(arrow,1))<=55+1e-8,"Bow energy is bounded");
        var spear=new Possession{Id="spear",Kind=PossessionKind.WoodenSpear,Position=ground,HeightM=1.9,MassKg=.9};Check(ItemUseRules.Profile(spear).ReachM>ItemUseRules.Profile(light).ReachM,"Spear reach differs from a stone in hand");
        Check(ItemUseRules.Profile(spear).Secondary==ItemSecondaryUse.Throw,"Spear secondary throws");
        Check(ItemUseRules.Profile(PortableTechnology.Create(PossessionKind.Bone,ground)).Secondary==ItemSecondaryUse.Block,"Bone secondary blocks rather than becoming an unexplained missile");
        foreach(var kind in new[]{PossessionKind.RawFibre,PossessionKind.Cordage,PossessionKind.RawHide,PossessionKind.WorkedHide,PossessionKind.Feather,PossessionKind.BoneNeedle,PossessionKind.HidePack})Check(!ItemUseRules.Profile(PortableTechnology.Create(kind,ground)).Weapon,"Nonweapon component/carrier does not become a generic club: "+kind);
        double stamina=30,unblocked=40;double front=ItemUseRules.BlockDamage(unblocked,spear,1,ref stamina);Check(front<unblocked&&stamina<30,"Facing block absorbs damage and consumes stamina");
        stamina=30;Check(ItemUseRules.BlockDamage(unblocked,spear,-1,ref stamina)==unblocked&&stamina==30,"Guard does not stop an attack from behind");
        stamina=0;Check(ItemUseRules.BlockDamage(unblocked,spear,1,ref stamina)==unblocked,"Exhausted guard cannot grant immunity");
        Check(ItemUseRules.HealthForMass(500)>ItemUseRules.HealthForMass(50)&&ItemUseRules.HealthForMass(.01)>=1,"Animal health scales smoothly with body mass");
        var record=new WildlifeIndividual{Id=88,Species="deer",Position=ground,Health=0,MaximumHealth=80,Coat=1,Parts=new[]{15,0,0,0},CorpsePose=new double[16]};
        var session=new PlaySession();session.Wildlife.Individuals.Add(record);var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();Check(saved.Wildlife.Individuals[0].Dead&&saved.Wildlife.Individuals[0].Id==88,"Killed identity and corpse state survive full session serialization");
        var copy=session.Copy();copy.Wildlife.Individuals[0].Parts[0]=31;copy.Wildlife.Individuals[0].CorpsePose[0]=2;Check(record.Parts[0]==15&&record.CorpsePose[0]==0,"Wildlife copies do not alias mutable corpse data");
        var legacyJson=System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(new PlaySession())).AsObject();legacyJson.Remove("Wildlife");var legacy=WorldJson.Read<PlaySession>(legacyJson.ToJsonString());legacy.Validate();Check(legacy.Wildlife!=null,"Legacy sessions initialize optional wildlife state during validation");
        var worn=PortableTechnology.Create(PossessionKind.StoneBlade,ground);worn.UseDamage=.8;worn.EdgeSharpness=.1;double originalMass=worn.MassKg;ItemMaintenance.Complete(worn,true);
        Check(worn.UseDamage<.1&&worn.EdgeSharpness>.8&&worn.MassKg<originalMass,"Retouching restores a useful edge at a small real material cost");
        var inventory=new PlayerInventory();inventory.ToolResources.Add(new NaturalToolRecord{Id="tree-123",Bark=2,Branches=1,Work=30});var inventorySave=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));inventorySave.Validate();
        Check(inventorySave.ToolResources[0].Bark==2&&inventorySave.ToolResources[0].Work==30,"Actual tree extraction/depletion persists");var inventoryCopy=inventory.Copy();inventoryCopy.ToolResources[0].Bark=0;Check(inventory.ToolResources[0].Bark==2,"Tool-resource ledgers do not alias across copied saves");
        var wolf=WildlifeBehaviour.Describe("wolf",40,Food.Vertebrate);var deer=WildlifeBehaviour.Describe("deer",80,Food.Browse);var moose=WildlifeBehaviour.Describe("moose",450,Food.Browse);var bear=WildlifeBehaviour.Describe("bear",200,Food.Fruit);var goose=WildlifeBehaviour.Describe("goose",4,Food.Grass,true);
        Check(wolf.PackHunter&&wolf.TrackingM>deer.TrackingM&&wolf.GroupMin>1,"Pack predators retain wider regional tracking than ordinary prey");
        Check(deer.Herd&&!deer.Defensive&&moose.Defensive&&!moose.Herd,"Moose defence/solitary priors differ from ordinary deer herds");
        Check(WildlifeBehaviour.EdiblePrey(.7,false,.05,.08,.05)&&WildlifeBehaviour.EdiblePrey(.7,false,.05,.08,.08),"Native prey limits include their exact boundary masses");
        Check(!WildlifeBehaviour.EdiblePrey(.7,false,.001,.08,.5),"A small-prey specialist does not inherit a larger body-mass fallback limit");
        Check(!WildlifeBehaviour.EdiblePrey(40,true,1,180,200),"Pack hunting does not silently override a known native upper prey limit");
        Check(WildlifeBehaviour.EdiblePrey(40,false,0,0,50)&&!WildlifeBehaviour.EdiblePrey(40,false,0,0,61),"Unknown solo prey limits use the documented body-size fallback");
        Check(WildlifeBehaviour.EdiblePrey(40,true,0,0,200),"Unknown pack prey limits retain the pack fallback");
        Check(WildlifeBehaviour.EdiblePrey(.012,false,.000001,.002,.00001),"Native tiny-prey ranges are not replaced by an arbitrary one-gram cutoff");
        Check(!WildlifeBehaviour.EdiblePrey(double.NaN,false,0,1,.5)&&!WildlifeBehaviour.EdiblePrey(1,false,0,double.PositiveInfinity,.5)&&!WildlifeBehaviour.EdiblePrey(1,false,0,1,double.NaN),"Invalid masses or bounds cannot admit prey");
        Check(!WildlifeBehaviour.EdiblePrey(1,false,2,1,1.5)&&!WildlifeBehaviour.EdiblePrey(1,false,-1,1,.5)&&!WildlifeBehaviour.EdiblePrey(1,false,0,-1,.5),"Reversed or negative prey ranges are rejected");
        var drinking=new LocalTerrainSample{WaterKind=LocalWaterKind.River,WaterSurfaceM=2,ElevationM=1,WaterMaskM=0};
        Check(WildlifeBehaviour.DrinkingWater(drinking),"Actual river water can support drinking");
        drinking.WaterKind=LocalWaterKind.Lake;Check(WildlifeBehaviour.DrinkingWater(drinking),"Lake water is a candidate at the terrain-kind classification layer");
        drinking.WaterKind=LocalWaterKind.Ocean;Check(!WildlifeBehaviour.DrinkingWater(drinking),"Ocean water does not satisfy ordinary terrestrial thirst");
        drinking.WaterKind=LocalWaterKind.River;drinking.ElevationM=3;Check(!WildlifeBehaviour.DrinkingWater(drinking),"A dry river-influence footprint is not drinkable water");
        drinking.ElevationM=1;drinking.WaterMaskM=-1;Check(!WildlifeBehaviour.DrinkingWater(drinking),"Clipped water outside the actual footprint is not drinkable");
        drinking.WaterMaskM=0;drinking.WaterSurfaceM=double.NaN;Check(!WildlifeBehaviour.DrinkingWater(drinking),"Unknown water height cannot supply drinking water");
        Check(WildlifeBehaviour.Dormant(bear,0,50,-10,-4)&&!WildlifeBehaviour.Dormant(bear,.5,50,-10,20),"Temperate bear winter denning follows season and actual cold");
        Check(!WildlifeBehaviour.Dormant(bear,0,5,20,25)&&!WildlifeBehaviour.Dormant(WildlifeBehaviour.Describe("polar-bear",350,Food.Vertebrate),0,70,-30,-20),"Tropical bears and general polar-bear population do not receive blanket hibernation");
        Check(!WildlifeBehaviour.SeasonallyPresent(goose,0,55,-10)&&WildlifeBehaviour.SeasonallyPresent(goose,.5,55,-10),"Cold breeding-ground migratory birds differ between winter and summer");
        Check(WildlifeBehaviour.Winter(0,50)==WildlifeBehaviour.Winter(.5,-50),"Seasonal priors reverse with hemisphere");
        Check(WildlifeBehaviour.MigrationHeading(.18,50)==0&&WildlifeBehaviour.MigrationHeading(.32,50)==0&&WildlifeBehaviour.MigrationHeading(.68,50)==Math.PI&&WildlifeBehaviour.MigrationHeading(.82,50)==Math.PI,"Migration heading does not reverse midway through northern spring/autumn");
        Check(WildlifeBehaviour.MigrationHeading(.18,-50)==0&&WildlifeBehaviour.MigrationHeading(.68,-50)==Math.PI,"Southern migrants head equatorward in their autumn and poleward in their spring");
        Check(WildlifeBehaviour.Activity(deer,6,false,0,0)>WildlifeBehaviour.Activity(deer,12,false,0,0),"Crepuscular prey are more active at dawn than noon");
        session.Wildlife.Groups.Add(new WildlifeGroup{Id=778,Species="wolf",Home=ground,Position=ground,Goal=ground,Intent=WildlifeIntent.Hunt,Hunger=.6,Members=5});var savedGroups=WorldJson.Read<PlaySession>(WorldJson.Write(session));savedGroups.Validate();Check(savedGroups.Wildlife.Groups[0].Intent==WildlifeIntent.Hunt&&savedGroups.Wildlife.Groups[0].Members==5,"Cheap regional groups save their actual intent and membership size");
        var identities=new System.Collections.Generic.HashSet<ulong>();var latticeKey=new ScatterKey(2,333,777,false);for(int g=0;g<520;g++)for(int m=0;m<12;m++)identities.Add(FaunaPrototypePlanner.Identity(latticeKey,14,g,m));Check(identities.Count==6240&&identities.All(id=>id!=0&&id<0x2000000000000000UL),"Population identities remain distinct beyond old group/slot limits and outside the manual namespace");
        Check(new FaunaPrototypeProfile{medium="air",lengthM=1.4,radiusM=170}.VisibleRadiusM==600&&new FaunaPrototypeProfile{tiny=true,radiusM=22}.VisibleRadiusM==22,"Large flying silhouettes retain distance while tiny ambient animals remain local");
        Check(!WildlifeBehaviour.Describe("sea-lion",180,Food.Fish).PackHunter&&!WildlifeBehaviour.Describe("mountain-lion",60,Food.Vertebrate).PackHunter,"A name substring cannot turn sea lions and mountain lions into lion prides");
        Check(new[]{"spider-monkey","wildebeest","giant-panda","flying-fox","elephant-shrew","coastal-forage-fish-guild"}.All(p=>FaunaBlood.Kind(p)==FaunaBloodKind.Red),"Animal name substrings cannot give vertebrates insect or spider blood");
        Check(FaunaBlood.Kind("orb-weaving-spiders-guild")==FaunaBloodKind.CopperHemolymph&&FaunaBlood.Kind("bees-guild")==FaunaBloodKind.PaleHemolymph,"Actual invertebrate profiles retain their distinct hemolymph groups");
        record.GroupId=778;record.GroupIndex=2;record.GroupSize=5;record.Taxon=17;var identitySave=WorldJson.Read<PlaySession>(WorldJson.Write(session));identitySave.Validate();
        Check(identitySave.Wildlife.Individuals[0].GroupId==778&&identitySave.Wildlife.Individuals[0].Taxon==17&&identitySave.Wildlife.Individuals[0].GroupIndex==2,"Injured individual saves preserve their source taxon and social identity");
        Console.WriteLine("PASS "+checks+" item-use, physical launch, block and wildlife persistence checks");
    }
}

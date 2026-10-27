using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    // Focused native-player serialization check. It bypasses display options,
    // music, game saves and scene generation, so it can run without a GPU.
    IEnumerator CheckHomininPersistence()
    {
        int exit=0;var checks=new List<string>();
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add("PASS "+text);}
        string output=Argument("--worldgen-output","");
        try
        {
            Directory.CreateDirectory(output);string input=Argument("--worldgen-hominin-check","");byte[] original=File.ReadAllBytes(input);
            var world=input.EndsWith(".world",StringComparison.OrdinalIgnoreCase)?WorldFile.Read(original):WorldJson.ReadWorld(System.Text.Encoding.UTF8.GetString(original));
            var engine=new Prehistory(world);var initial=engine.Start(engine.PrepareHominins());
            Check(initial.DeepPast.Version==2&&initial.DeepPast.RulesVersion==4,"native player creates the current early-history schema and rules");
            Check(initial.DeepPast.Languages.Length==1,"native player retains the founding communication identity");
            var state=engine.AdvanceYears(initial,20000);engine.Validate(state);
            File.WriteAllText(Path.Combine(output,"hominin-state.json"),WorldJson.Write(state));
            Check(state.DeepPast.Languages.Length==state.DeepPast.NextLanguage,"native player tracks communication branches");
            var clock=new PlaySession();clock.Accumulate(300);
            Check(!SimulationClockView.LocalHistoryEventDue(state,(long)clock.PendingHistorySeconds),"ordinary local play retains clock credit without requesting a demographic publication");
            Check(SimulationClockView.LocalPublication(state,3*SimulationTime.Year).Seconds==3*SimulationTime.Year,"leaving play can flush empty years in one native publication");
            Check(state.DeepPast.Traditions.All(t=>(t.Knowledge&LithicKnowledge.Pottery)==0),"early foundation does not grant late pottery");
            world.Prehistory=state;var restored=WorldFile.Read(WorldFile.Write(world));var resumed=new Prehistory(restored);
            Check(restored.Prehistory.Hash==state.Hash,"compact native save retains early-history canonical hash");
            Check(restored.Prehistory.DeepPast.RulesVersion==4,"compact native save retains the explicit rule revision");
            Check(resumed.AdvanceYears(restored.Prehistory,1000).Hash==engine.AdvanceYears(state,1000).Hash,"fresh native engine resumes the same populations and traditions");
            var people=new HomininActorSampler(engine,state);
            Check(people.TrySample(engine.Patches[state.Origin].Cell,123,false,out var person)&&WorldJson.Read<HomininActorProfile>(WorldJson.Write(person)).HistoryHash==state.Hash,"native actor profiles retain demographic provenance through serialization");
            var returning=state.Copy();var ancestral=returning.DeepPast.Populations.First(p=>!returning.DeepPast.Lineages[p.Lineage].SapiensLike);ancestral.Communication=ancestral.Learning=.9;
            int lostId=returning.DeepPast.Lineages.Length;var lost=returning.DeepPast.Lineages[ancestral.Lineage].Copy();lost.Id=lostId;lost.Parent=ancestral.Lineage;lost.Name="Lost descendant fixture";lost.SapiensLike=true;lost.FoundedYear=10000;lost.ExtinctYear=15000;
            returning.DeepPast.Lineages=returning.DeepPast.Lineages.Concat(new[]{lost}).ToArray();returning.DeepPast.SealDomain();returning.Seal();var living=engine.AdvanceYears(returning,1000);engine.Validate(living);
            Check(living.DeepPast.Lineages[lostId].ExtinctYear==15000&&!living.DeepPast.Populations.Any(p=>p.Lineage==lostId),"native current rules do not resurrect a recorded extinct lineage");
            var arranged=new SurvivalInventory{View=InventoryView.Grid,NextId=7,Items=new[]{
                new SurvivalStack{Id=1,Item="pack",Container=-2},
                new SurvivalStack{Id=2,Item="stone-spear",Container=0,X=3,Rotated=true},
                new SurvivalStack{Id=3,Item="dryhide",Container=0,X=4,Rotated=true},
                new SurvivalStack{Id=4,Item="hide-wrap",Container=0,X=1,Rotated=true},
                new SurvivalStack{Id=5,Item="hide-wrap",Container=0,X=1,Y=3},
                new SurvivalStack{Id=6,Item="stone-spear",Container=0,Rotated=true}}};
            arranged.Validate();string slots=WorldJson.Write(arranged);
            Check(!arranged.Compact(out _),"native greedy packing reports a failed rearrangement");
            Check(WorldJson.Write(arranged)==slots,"native failed packing leaves every original slot unchanged");
            arranged.Validate();Check(arranged.Pack(out _),"native manually arranged inventory remains valid");
            var harvest=new SurvivalState();var carcass=SurvivalPhysiology.Carcass("small-deer",999,new V(1,0,0),10,0);harvest.Corpses=new[]{carcass};
            bool harvested=true;for(int i=0;i<4;i++)harvested&=SurvivalPhysiology.Harvest(harvest,carcass,"bone",out _);
            WorldJson.Read<SurvivalState>(WorldJson.Write(harvest)).Validate();
            Check(harvested&&carcass.BoneKg==0&&!SurvivalPhysiology.Harvest(harvest,carcass,"bone",out _),"native exhausted carcass retains a valid nonnegative saved balance");
            var tired=new SurvivalState{Stamina=6};SurvivalPhysiology.Advance(tired,15,new SurvivalEnvironment{TemperatureC=22,SpeedMps=6,Sprinting=true});
            Check(tired.SprintExhausted&&!SurvivalPhysiology.CanSprint(tired),"native sprint exhaustion enters recovery");
            SurvivalPhysiology.Advance(tired,60,new SurvivalEnvironment{TemperatureC=22,SpeedMps=3});tired=WorldJson.Read<SurvivalState>(WorldJson.Write(tired));
            Check(tired.Stamina>5&&!SurvivalPhysiology.CanSprint(tired),"native sprint recovery persists instead of alternating gaits at the cutoff");
            SurvivalPhysiology.Advance(tired,120,new SurvivalEnvironment{TemperatureC=22,Resting=true});Check(SurvivalPhysiology.CanSprint(tired),"native useful stamina recovery permits sprinting again");
            tired.Stamina=0;tired.Mode=SurvivalMode.Creative;Check(SurvivalPhysiology.CanSprint(tired),"native creative movement ignores survival exhaustion");
            Vector3 playerEye=new Vector3(0,1.65f,0),closeHit=new Vector3(0,1.65f,2.8f);
            Check(LocalTerrainExplorer.SurvivalHitInReach(playerEye,closeHit,3),"native melee reach includes a nearby physical hit point");
            Check(!LocalTerrainExplorer.SurvivalHitInReach(playerEye,new Vector3(0,10,1),3),"native melee reach rejects a vertically distant target despite its nearby map location");
            var thirdPersonRay=new Ray(new Vector3(0,1.65f,-8),Vector3.forward);var targetBounds=new Bounds(new Vector3(0,1.65f,3),new Vector3(1,1,1));
            Check(targetBounds.IntersectRay(thirdPersonRay,out float rangeFromCamera)&&rangeFromCamera>3&&LocalTerrainExplorer.SurvivalHitInReach(playerEye,thirdPersonRay.GetPoint(rangeFromCamera),3),"native third-person aim does not consume the player's physical weapon reach");
            var cover=LocalTerrainExplorer.CreateSurvivalShelterPanel(Vector3.zero,Vector3.right,Vector3.up);
            Check(cover.triangles.Length==3&&cover.normals.Length==3&&cover.normals.All(n=>n.sqrMagnitude>.99f),"native double-sided shelter cover has valid lighting normals without cancelling duplicate faces");Destroy(cover);
            var insect=SurvivalPhysiology.Carcass("bee",991,new V(1,0,0),.0001,0);var small=new SurvivalState{Corpses=new[]{insect}};
            Check(insect.MassKg==.0001&&insect.MeatKg<.0001,"native tiny carcasses do not gain a ten-gram minimum mass");
            double carried=small.Inventory.Mass;bool picked=SurvivalPhysiology.Harvest(small,insect,"meat",out _);WorldJson.Read<SurvivalState>(WorldJson.Write(small)).Validate();
            Check(picked&&Math.Abs(small.Inventory.Mass-carried-.0001*.32)<1e-9,"native tiny food portions conserve biomass and remain valid saved data");
            Check(SurvivalPhysiology.AnimalVitality(.025)<=3&&SurvivalPhysiology.AnimalVitality(100)>SurvivalItems.Get("stone-spear").Damage,"native animal vitality is proportionate for small prey and deer");
            Check(LocalTerrainExplorer.SurvivalHitInReach(new Vector3(0,1.1f,0),new Vector3(0,.1f,.3f),1.4),"native upper-body reach can strike ground-level small prey with a short tool");
            string beforeAtlas=WorldJson.Write(state);var populationRows=HistoryAtlas.Directory(state,"population");
            Check(populationRows.Sum(row=>row.Value)==state.Population.Sum()&&HistoryAtlas.Directory(state,"people").Sum(row=>row.Value)==state.Population.Sum()&&HistoryAtlas.Directory(state,"language").Sum(row=>row.Value)==state.Population.Sum(),"native early atlas headcounts reconcile across independent identities");
            var categories=AtlasNavigation.CategoriesFor(state);Check(categories.Any(c=>c.Kind=="lineage")&&categories.Any(c=>c.Kind=="population")&&!categories.Any(c=>c.Kind=="ethnicity"||c.Kind=="state"),"native early atlas does not mislabel biological lineages as ethnicities or states");
            var overview=HistoryAtlas.Read(state,"world",-1,world,engine);var resident=state.DeepPast.Populations[0];var populationPage=HistoryAtlas.Read(state,"population",resident.Id,world,engine);
            var stages=HomininView.Stages(state);Check(stages.Population==state.Population.Sum()&&stages.Label.Contains("Paleolithic")&&overview.Facts.Any(f=>f.Label=="Technical frontier"),"native published lithic overview exposes local stages without changing era IDs");
            Check(overview.Facts.Any(f=>f.Label=="Living lineages")&&populationPage.Cell==engine.Patches[resident.Region].Cell&&populationPage.Facts.SelectMany(f=>f.Related).Any(link=>link.Kind=="language"),"native early atlas presents actual population records and their geographic context");
            var timeline=HistoryAtlas.Timeline(state);Check(overview.Sections.Any(section=>section.Title=="Recorded history")&&overview.SnapshotDate==HomininView.Date(state)&&timeline.Total==state.DeepPast.Events.Length&&timeline.Records.All(f=>f.Label.Contains("before reference")),"native early timeline exposes the recorded historical events");
            File.WriteAllText(Path.Combine(output,"hominin-atlas-world.txt"),HistoryAtlas.PlainText(overview,timeline));File.WriteAllText(Path.Combine(output,"hominin-atlas-population.txt"),HistoryAtlas.PlainText(populationPage));
            Check(WorldJson.Write(state)==beforeAtlas,"native atlas inspection does not mutate the simulation checkpoint");
            var later=engine.PrepareSapiens();Check(ReferenceEquals(AtlasNavigation.CategoriesFor(later),AtlasNavigation.Categories)&&HistoryAtlas.Read(later).Facts.Any(f=>f.Label=="Surviving states"),"native later-history atlas retains its established adapter");
            var play=new PlaySession{Survival=new SurvivalState()};play.Survival.Corpses=Enumerable.Range(0,8).Select(i=>SurvivalPhysiology.Carcass("deer",(ulong)i+1,new V(1,0,0),70,0)).ToArray();
            bool creativeFoods=true,creativePortions=true;
            foreach(var recipe in SurvivalCrafting.Recipes.Where(r=>SurvivalItems.Get(r.Output).Kind==SurvivalItemKind.Food))
            {
                var creative=new SurvivalState{Mode=SurvivalMode.Creative,Inventory=new SurvivalInventory()};creative.Inventory.TryAdd("pack",1,out _);
                creativeFoods&=SurvivalCrafting.Craft(creative,recipe,false,out _);
                creativePortions&=creative.Inventory.Items.Single(i=>i.Item==recipe.Output).Fraction==1;creative.Validate();
            }
            Check(creativeFoods,"native Creative food recipes do not read absent ingredients");
            Check(creativePortions,"native Creative food recipes create valid full portions");
            var storage=new SurvivalState{Inventory=new SurvivalInventory(),NextSite=2};var rack=new SurvivalSite{Id=1,Kind="cache",Position=new V(1,0,0)};storage.Sites=new[]{rack};
            storage.Inventory.TryAdd("pack",1,out _);storage.Inventory.TryAdd("raw-meat",2,out _,.4,.15,.7);long storedId=storage.Inventory.Items.Single(i=>i.Item=="raw-meat").Id;
            Check(SurvivalStorage.Store(storage,rack,storedId,2,out _),"native storage removes actual carried portions");
            var entry=rack.Contents.Single();double beforeTransferMass=storage.Inventory.Mass+entry.Count*entry.Fraction*SurvivalItems.Get(entry.Item).Kg;
            Check(SurvivalStorage.Take(storage,rack,entry,1,out _)&&storage.Inventory.Items.Single(i=>i.Item=="raw-meat").Condition==.7&&storage.Inventory.Items.Single(i=>i.Item=="raw-meat").Fraction==.15&&Math.Abs(beforeTransferMass-storage.Inventory.Mass-rack.Contents.Sum(i=>i.Count*i.Fraction*SurvivalItems.Get(i.Item).Kg))<1e-10,"native storage preserves food condition, portion size and total mass");
            string storageBefore=WorldJson.Write(storage);Check(!SurvivalStorage.Take(storage,rack,entry,1,out _)&&WorldJson.Write(storage)==storageBefore,"native storage rejects stale entry requests without mutation");
            long takeId=storage.Inventory.Items.Single(i=>i.Item=="raw-meat").Id;
            Check(SurvivalStorage.Drop(storage,takeId,1,new V(1,0,0),out var dropped,out _)&&SurvivalStorage.Take(storage,dropped,dropped.Contents[0],1,out _)&&!storage.Sites.Any(site=>site.Id==dropped.Id),"native dropped-item round trip retires its empty saved site");
            WorldJson.Read<SurvivalState>(WorldJson.Write(storage)).Validate();Check(storage.Sites.Single().Contents.Single().Fraction==.15,"native stored portions survive saved-data validation");
            foreach(var corpse in play.Survival.Corpses)corpse.SettledPose=Enumerable.Repeat(.12345678f,2048).ToArray();
            string json=WorldJson.Write(play);File.WriteAllText(Path.Combine(output,"play-session.json"),json);
            Check(System.Text.Encoding.UTF8.GetByteCount(json)>65536,"native survival save exceeds the former camera-only limit");
            var loaded=WorldLibrary.ReadPlaySession(Path.Combine(output,"snapshot.world"));
            Check(loaded.Survival.Corpses.Length==8&&loaded.Survival.Corpses.Last().SettledPose.Length==2048,"actual native library loader retains multiple physics poses");
            using(var hash=SHA256.Create())Check(hash.ComputeHash(original).SequenceEqual(hash.ComputeHash(File.ReadAllBytes(input))),"input world file is unchanged");
            File.WriteAllLines(Path.Combine(output,"hominin-persistence-check.txt"),checks);Debug.Log("HOMININ PERSISTENCE PASS "+checks.Count);
        }
        catch(Exception error){Debug.LogError(error.ToString());exit=1;}
        Application.Quit(exit);yield break;
    }
}

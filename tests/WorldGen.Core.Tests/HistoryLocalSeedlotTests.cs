using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryLocalSeedlotTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args);
    static HistoricalCultivar Wheat(int latitude=316)=>new HistoricalCultivar{CropId=0,OriginCulture=28,ParentCulture=49,SelectedYears=4000,MaturityDays=105,MinimumWarmTenthsC=100,DaylengthLatitudeTenths=latitude,WaterRequirementPermille=350,YieldPermille=1450};
    static HistoricalTownSeeds Seeds(int town,HistoricalCultivar seed)
    {var slots=new HistoricalLocalSeedlot[6];slots[0]=new HistoricalLocalSeedlot(seed,100,-1,7,4000,100);return new HistoricalTownSeeds(town,7,slots);}
    public static void Run(Action<bool,string> check)
    {
        var input=Wheat();var record=Seeds(2,input);var tree=new HistoricalTownSeedTree().With(2,record).With(int.MaxValue,Seeds(int.MaxValue,Wheat(400)));
        input.DaylengthLatitudeTenths=800;check(tree[2][0].Cultivar.DaylengthLatitudeTenths==316,"Seed inventory snapshots mutable donor traits rather than aliasing the global culture cultivar.");
        var returned=tree[2][0].Cultivar;returned.SelectedYears=0;check(tree[2][0].Cultivar.SelectedYears==4000,"Public seed inspection cannot mutate the authoritative immutable record.");
        check(tree[int.MaxValue].TownId==int.MaxValue&&tree[33554433]==null&&tree[-1]==null,"Sparse seed records cover lifetime nonnegative int town IDs without terrain-count or packed-key overflow.");
        var before=tree.Fingerprint();var branch=tree.With(2,Seeds(2,Wheat(320)));check(tree.Fingerprint()==before&&branch.Fingerprint()!=before,"Updating one town shares immutable untouched history while leaving the source publication unchanged.");
        check(branch[int.MaxValue][0].Cultivar.DaylengthLatitudeTenths==400,"A local breeding update cannot retune another distant town of the same culture.");
        var ordered=new HistoricalTownSeedTree().With(int.MaxValue,Seeds(int.MaxValue,Wheat(400))).With(2,record);
        check(tree.Fingerprint()==ordered.Fingerprint()&&StateHash.Compute(tree)==StateHash.Compute(ordered),"Semantic seed canonical hashes are independent of insertion order and node allocation sharing.");
        check(tree.Fingerprint()==tree.Fingerprint(true),"Trusted cached seed fingerprints equal explicit literal recomputation.");
        var serializer=new DataContractSerializer(typeof(HistoricalTownSeedTree));using var stream=new MemoryStream();serializer.WriteObject(stream,tree);stream.Position=0;var restored=(HistoricalTownSeedTree)serializer.ReadObject(stream)!;
        check(restored.Fingerprint(true)==tree.Fingerprint()&&restored[2][0].SourceCell==7,"Saved sparse seed inventories retain exact traits, chronology and provenance.");
        var extension=new HistoricalLocalSeedlots{SinceYear=100,Towns=tree};var fork=extension.Copy();fork.Towns=fork.Towns.With(2,Seeds(2,Wheat(330)));
        check(extension.Towns.Fingerprint()==before&&fork.Towns.Fingerprint()!=before,"Connection copying shares only immutable seed roots, never writable inventories.");
        check(tree.With(2,null)[2]==null&&tree[2]!=null,"Explicit record removal cannot delete a source snapshot or unrelated sparse entries.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Null seed extension preserves literal preceding colonial canonical bytes.");
        bool floor=true;
        foreach(int id in Enumerable.Range(0,HistoricalCropCatalog.Crops.Length))foreach(int years in new[]{25,1000,2000,4000,20000})
        {var definition=HistoricalCropCatalog.Crops[id];var fresh=new HistoricalCultivar{CropId=id,MaturityDays=definition.MaturityDays,MinimumWarmTenthsC=definition.MinimumWarmC*10,WaterRequirementPermille=definition.MinimumWaterPermille,DaylengthLatitudeTenths=300};HistoricalLocalSeedSelectionEffects.Apply(fresh,years,400,0);floor&=fresh.YieldPermille>=Math.Min(1500,650+years/5)&&fresh.MaturityDays<=Math.Max(definition.MaturityDays-35,definition.MaturityDays-years/100)&&fresh.MinimumWarmTenthsC<=Math.Max((definition.MinimumWarmC-4)*10,definition.MinimumWarmC*10-years/100)&&fresh.WaterRequirementPermille>0&&fresh.YieldPermille<=1500;}
        check(floor,"Revision14 fresh seed retains the preceding active aggregate yield/maturity/warmth curve as a floor across all18 crops and five practice durations, alongside finite physiological bounds.");
        var noPractice=Wheat();string dormant=StateHash.Compute(noPractice);HistoricalLocalSeedSelectionEffects.Apply(noPractice,0,500,100);check(StateHash.Compute(noPractice)==dormant,"The revision14 trait floor does not grant selection at upgrade, without fields, or with zero effective practice.");
        int movement=Enumerable.Range(0,10000).Sum(i=>HistoricalLocalSeedSelectionEffects.LatitudeMovement(300,500,1,i/10000.0));
        check(movement==1000&&HistoricalLocalSeedSelectionEffects.LatitudeMovement(499,500,100,0)==1,"Revision14 local standing-variation movement averages.1tenths per effective practice year, without forced minimum or target overshoot.");
        var beforeCorruption=restored.Fingerprint();var raw=(HistoricalCultivar)typeof(HistoricalLocalSeedlot).GetField("cultivar",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(restored[2][0])!;raw.SelectedYears++;
        check(restored.Fingerprint(true)!=beforeCorruption,"Explicit integrity verification recomputes actual saved seed traits rather than trusting an existing cached fingerprint after corruption.");
    }
    static string Signature(HistoricalTownSeeds seeds)=>string.Join("|",Enumerable.Range(0,6).Select(k=>seeds[k]==null?"absent":StateHash.Compute(seeds[k].Cultivar)+":"+seeds[k].PracticeYears+":"+seeds[k].LastPracticeYear));
    public static void World(string path,Action<bool,string> check,bool cold=true)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();
        var s=engine.UpgradeRealmLifecycles(source);s.History.Aggregates.Empires.Connections.Realms.Version=14;
        check(StateHash.Compute(source.History.Agriculture)==StateHash.Compute(s.History.Agriculture),"The complete explicit revision14 upgrade preserves every existing global cultivar, adoption date and practice history before future actor events.");
        string initialCrops=StateHash.Compute(s.History.Agriculture);Call(engine,"InitializeLocalSeedlots",s);Call(engine,"ValidateLocalSeedlots",s);
        check(StateHash.Compute(s.History.Agriculture)==initialCrops,"Explicit seed inventory setup changes no existing crop, trait, adoption or culture practice record.");
        var accounts=s.History.Aggregates;var occupied=(from t in s.Society.Settlements where accounts.Living.Cities[t.Id].AbandonedYear<0&&accounts.Cities[t.Id].Rural>=10&&accounts.Cities[t.Id].RuralCapacity>0&&t.CultivatedPermille>=50&&s.Society.Cultures[t.CultureId].Farming>=50 from cropGroup in Enumerable.Range(0,6) let lot=engine.LocalSeedsAt(s,t.Id)?[cropGroup] where lot!=null&&(double)Call(engine,"CommunityTransferFit",s,t.Cell,lot.Cultivar,true)>=.08 select(Town:t.Id,Culture:t.CultureId,Group:cropGroup,Cell:t.Cell,Latitude:Math.Abs(Math.Asin(world.Surface.Cells[t.Cell].Position.Z))*180/Math.PI)).ToArray();
        var pair=(from a in occupied from b in occupied where a.Town!=b.Town&&a.Culture==b.Culture&&a.Group==b.Group&&Math.Abs(a.Latitude-b.Latitude)>=.5&&Math.Abs(a.Latitude*10-engine.LocalSeedsAt(s,a.Town)[a.Group].Cultivar.DaylengthLatitudeTenths)>=5 orderby Math.Abs(a.Latitude-b.Latitude) descending,a.Town,b.Town select(A:a,B:b)).First();
        var probe=s.Copy();var initial=engine.LocalSeedsAt(probe,pair.A.Town)[pair.A.Group].Cultivar;string distant=Signature(engine.LocalSeedsAt(probe,pair.B.Town)),global=StateHash.Compute(probe.History.Agriculture);
        long people=probe.History.Aggregates.TotalPopulation,food=probe.Society.Settlements.Sum(t=>t.FoodReserve),cash=probe.History.Polities.Sum(p=>p.Treasury);
        for(int step=0;step<50;step++){probe.Years+=20;Call(engine,"AdvanceLocalTownSeeds",probe,pair.A.Town);}
        var local=engine.LocalSeedsAt(probe,pair.A.Town)[pair.A.Group];int target=(int)Math.Round(pair.A.Latitude*10);
        check(local.Cultivar.SelectedYears>initial.SelectedYears&&Math.Abs(local.Cultivar.DaylengthLatitudeTenths-target)<Math.Abs(initial.DaylengthLatitudeTenths-target),"Actual occupied local farming selects its own seed toward its local field rather than the culture origin.");
        check(Signature(engine.LocalSeedsAt(probe,pair.B.Town))==distant&&StateHash.Compute(probe.History.Agriculture)==global,"Another real same-culture town and the culture fallback remain unchanged by independent local breeding.");
        check(local.Cultivar.OriginCulture==initial.OriginCulture&&local.Cultivar.ParentCulture==initial.ParentCulture&&local.Cultivar.CropId==initial.CropId,"Actual local selection preserves crop identity and inherited genetic provenance.");
        check(probe.History.Aggregates.TotalPopulation==people&&probe.Society.Settlements.Sum(t=>t.FoodReserve)==food&&probe.History.Polities.Sum(p=>p.Treasury)==cash,"Local seed selection manufactures no households, warehouse food or public funds.");
        var carried=(HistoricalTownSeeds)Call(engine,"SnapshotLocalSeeds",probe,pair.B.Town,pair.A.Culture,pair.B.Cell,pair.A.Town);
        check(StateHash.Compute(carried[pair.A.Group].Cultivar)==StateHash.Compute(local.Cultivar)&&carried[pair.A.Group].SourceTown==pair.A.Town&&carried[pair.A.Group].SourceCell==pair.A.Cell,"A physical parent inventory snapshots the actual locally bred lot and immediate departure, not the global Chinese-origin default.");
        check((int)Call(engine,"LocalSeedPracticeAt",probe,pair.A.Culture,pair.A.Group,engine.LocalSeedsAt(probe,pair.A.Town),pair.B.Cell,true)==carried[pair.A.Group].PracticeYears,"Carried destination planning discounts actual source-field husbandry by the same habitat continuity used by committed seed inheritance.");
        double fitBefore=(double)Call(engine,"CarriedCropLandscape",probe,pair.A.Culture,pair.B.Cell,false,pair.A.Town,-1);
        probe.History.Agriculture[pair.A.Culture].Cultivars[pair.A.Group].DaylengthLatitudeTenths=900;
        check((double)Call(engine,"CarriedCropLandscape",probe,pair.A.Culture,pair.B.Cell,false,pair.A.Town,-1)==fitBefore,"Destination livelihood planning uses the same explicit parent's carried lot even when the culture-global seed is deliberately different.");
        var daughter=s.Copy();int child=Enumerable.Range(0,daughter.History.Cultures.Length).First(c=>c!=pair.A.Culture);var inherited=engine.LocalSeedsAt(daughter,pair.A.Town)[pair.A.Group].Cultivar;
        int destination=occupied.First(x=>x.Group==pair.A.Group&&(double)Call(engine,"CommunityTransferFit",daughter,x.Cell,inherited,false)>=.12).Cell;
        Call(engine,"InheritLocalSeedCommunity",daughter,child,pair.A.Culture,destination,pair.A.Cell,pair.A.Town);var received=daughter.History.Agriculture[child].Cultivars[pair.A.Group];var expected=inherited.Copy();expected.ParentCulture=pair.A.Culture;
        check(StateHash.Compute(received)==StateHash.Compute(expected)&&daughter.History.Agriculture[child].PracticeCell[pair.A.Group]==destination,"A real source-town seed snapshot survives daughter-community inheritance, including the destination practice field and immediate parent culture.");
        daughter.History.Agriculture[child].Cultivars[pair.A.Group]=null;daughter.History.Agriculture[child].AdoptedYear[pair.A.Group]=0;Call(engine,"InheritLocalSeedCommunity",daughter,child,pair.A.Culture,destination,-1,-1);
        check(daughter.History.Agriculture[child].Cultivars[pair.A.Group]==null,"Missing physical departure does not authorize copying an unrelated global genotype into a daughter community.");
        var freshField=(from field in occupied let oldSeed=engine.LocalSeedsAt(s,field.Town)[field.Group].Cultivar let definition=HistoricalCropCatalog.Crops[oldSeed.CropId] let seed=new HistoricalCultivar{CropId=oldSeed.CropId,OriginCulture=oldSeed.OriginCulture,ParentCulture=oldSeed.ParentCulture,MaturityDays=definition.MaturityDays,WaterRequirementPermille=definition.MinimumWaterPermille,MinimumWarmTenthsC=definition.MinimumWarmC*10,DaylengthLatitudeTenths=(int)Math.Round(field.Latitude*10)} where (double)Call(engine,"CommunityTransferFit",s,field.Cell,seed,true)>=.08 select(Field:field,Seed:seed)).First();
        var young=s.Copy();young.Years+=1000;var crop=young.History.Agriculture[freshField.Field.Culture];crop.Cultivars[freshField.Field.Group]=freshField.Seed;crop.PracticeCell[freshField.Field.Group]=freshField.Field.Cell;
        Call(engine,"AdvanceAggregateLocalCropSelection",young,freshField.Field.Culture,freshField.Field.Group,1000,freshField.Field.Cell);var cultivated=crop.Cultivars[freshField.Field.Group];
        check(cultivated.SelectedYears>0&&cultivated.YieldPermille>=Math.Min(1500,650+cultivated.SelectedYears/5),"Actual revision14 pre-town/culture fallback selection also retains the preceding fresh-seed aggregate curve, not just already-established town lots.");
        var empty=s.Copy();empty.History.Aggregates.Cities[pair.A.Town].Rural=0;empty.History.Aggregates.Cities[pair.A.Town].RuralCapacity=0;empty.Society.Settlements[pair.A.Town].CultivatedPermille=0;
        var unplanted=engine.LocalSeedsAt(empty,pair.A.Town)[pair.A.Group].Cultivar;empty.Years+=100;Call(engine,"AdvanceLocalTownSeeds",empty,pair.A.Town);
        check(StateHash.Compute(engine.LocalSeedsAt(empty,pair.A.Town)[pair.A.Group].Cultivar)==StateHash.Compute(unplanted)&&engine.LocalSeedsAt(empty,pair.A.Town)[pair.A.Group].PracticeYears==engine.LocalSeedsAt(s,pair.A.Town)[pair.A.Group].PracticeYears,"An import-only harbor configuration with no local rural fields holds seed but earns no local practice or adaptation.");
        check(engine.LocalSeedsAt(empty,pair.A.Town)[pair.A.Group].LastPracticeYear==empty.Years,"A non-cultivating interval advances its clock without accumulating future practice arrears.");
        var zero=(from t in s.Society.Settlements where accounts.Living.Cities[t.Id].AbandonedYear<0 from cropGroup in Enumerable.Range(0,6) let lot=engine.LocalSeedsAt(s,t.Id)?[cropGroup] where lot!=null&&(double)Call(engine,"CommunityTransferFit",s,t.Cell,lot.Cultivar,true)==0 select(Town:t.Id,Group:cropGroup)).First();
        var failure=s.Copy();failure.Years+=100;var failed=engine.LocalSeedsAt(failure,zero.Town)[zero.Group];Call(engine,"AdvanceLocalTownSeeds",failure,zero.Town);
        check(StateHash.Compute(engine.LocalSeedsAt(failure,zero.Town)[zero.Group].Cultivar)==StateHash.Compute(failed.Cultivar)&&engine.LocalSeedsAt(failure,zero.Town)[zero.Group].PracticeYears==failed.PracticeYears,"A known but zero-fit seed at a real settlement cannot breed itself through environmental failure.");
        var old=s.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=13;old.Years+=100;string oldRoot=old.History.Aggregates.Empires.Connections.LocalSeeds.Towns.Fingerprint();Call(engine,"AdvanceLocalTownSeeds",old,pair.A.Town);
        check(old.History.Aggregates.Empires.Connections.LocalSeeds.Towns.Fingerprint()==oldRoot,"Revision13 cannot silently acquire revision14 local seed behavior.");
        var isolated=s.Copy();int foreign=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&world.Landmass[cell]!=world.Landmass[pair.A.Cell]);var ia=isolated.History.Aggregates;
        ia.TownAt=ia.TownAt.With(foreign,-1);ia.Cells=ia.Cells.With(foreign*5+3,pair.A.Town);ia.Empires.Territories.Sources=ia.Empires.Territories.Sources.With(foreign,-1);
        check(Call(engine,"LocalSeedHolder",isolated,pair.A.Culture,foreign,-1)==null,"An artificial market label across a disconnected landmass cannot supply the same culture's distant seed inventory.");
        int teacherCell=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&cell!=pair.A.Cell&&ia.TownAt[cell]<0);
        ia.Cells=ia.Cells.With(teacherCell*5,pair.A.Culture).With(teacherCell*5+3,-1);ia.Empires.Territories.Sources=ia.Empires.Territories.Sources.With(teacherCell,-1);ia.Living.Regions[engine.CellPatch[teacherCell]].Teacher=pair.A.Culture;
        string receiptBefore=ia.Empires.Connections.LocalSeeds.Towns.Fingerprint();Call(engine,"RecordDeliveredLocalSeeds",isolated,teacherCell,pair.A.Cell);
        check(ia.Empires.Connections.LocalSeeds.Towns.Fingerprint()==receiptBefore,"A teacher-only regional memory without an actual local seed holder cannot deliver a free genotype.");
        var connection=(from offer in occupied from receiver in accounts.Cities[offer.Town].Neighbors where receiver>=0&&receiver<s.Society.Settlements.Length&&accounts.Living.Cities[receiver].AbandonedYear<0&&accounts.Cities[receiver].Rural>=10&&s.Society.Settlements[receiver].CultivatedPermille>=100 let t=s.Society.Settlements[receiver] let lot=engine.LocalSeedsAt(s,offer.Town)[offer.Group] where (double)Call(engine,"CommunityTransferFit",s,t.Cell,lot.Cultivar,true)>=.12&&(bool)Call(engine,"InstitutionalRouteAllowed",s,offer.Cell,t.Cell,offer.Culture) select(Offer:offer,Receiver:receiver,Cell:t.Cell)).First();
        var delivery=s.Copy();var inventory=engine.LocalSeedsAt(delivery,connection.Receiver);var saved=Enumerable.Range(0,6).Select(k=>inventory[k]).ToArray();var poor=engine.LocalSeedsAt(delivery,connection.Offer.Town)[connection.Offer.Group].Cultivar;poor.MinimumWarmTenthsC=1000;
        saved[connection.Offer.Group]=new HistoricalLocalSeedlot(poor,delivery.Years,-1,connection.Cell,0,delivery.Years);delivery.History.Aggregates.Empires.Connections.LocalSeeds.Towns=delivery.History.Aggregates.Empires.Connections.LocalSeeds.Towns.With(connection.Receiver,new HistoricalTownSeeds(connection.Receiver,connection.Cell,saved));
        delivery.Years=Enumerable.Range(delivery.Years,128).First(year=>(int)((double)Call(engine,"HistoryRoll","delivered-seed-group-14",connection.Receiver,year)*6)==connection.Offer.Group);
        Call(engine,"RecordDeliveredLocalSeeds",delivery,connection.Offer.Cell,connection.Cell);var delivered=engine.LocalSeedsAt(delivery,connection.Receiver)[connection.Offer.Group];
        check(delivered.SourceTown==connection.Offer.Town&&delivered.SourceCell==connection.Offer.Cell&&StateHash.Compute(delivered.Cultivar)==StateHash.Compute(engine.LocalSeedsAt(delivery,connection.Offer.Town)[connection.Offer.Group].Cultivar),"A completed real-neighbor endpoint delivery can replace an unsuitable local lot with the actual viable supplied genotype and dated source, rather than only rejecting all seed exchange.");
        var actualPort=accounts.Empires.Connections.ProvisionedPorts.Ports.FirstOrDefault(p=>accounts.Living.Cities[p.TownId].AbandonedYear<0&&engine.LocalSeedsAt(s,p.TownId)!=null);
        if(actualPort!=null)
        {
            var harbor=s.Copy();var held=engine.LocalSeedsAt(harbor,actualPort.TownId);string genotype=string.Join("|",Enumerable.Range(0,6).Select(k=>StateHash.Compute(held[k]?.Cultivar)));int practice=Enumerable.Range(0,6).Sum(k=>held[k]?.PracticeYears??0);harbor.Years+=100;Call(engine,"AdvanceLocalTownSeeds",harbor,actualPort.TownId);held=engine.LocalSeedsAt(harbor,actualPort.TownId);
            check(accounts.Cities[actualPort.TownId].Rural==0&&accounts.Cities[actualPort.TownId].RuralCapacity==0&&genotype==string.Join("|",Enumerable.Range(0,6).Select(k=>StateHash.Compute(held[k]?.Cultivar)))&&practice==Enumerable.Range(0,6).Sum(k=>held[k]?.PracticeYears??0),"An actual saved provisioned harbor with zero farms receives no seed adaptation or local husbandry from imported food.");
            Console.WriteLine("Actual unfarmed saved harbor negative: town"+actualPort.TownId+".");
        }
        check(Signature(engine.LocalSeedsAt(s,pair.A.Town))!=Signature(engine.LocalSeedsAt(probe,pair.A.Town)),"Detached local practice never mutates the upgrade publication's shared immutable seed tree.");
        if(original=="eacf2838970bf41cf3ff49bb2d7edb1ec7911222c0df74a9e59d5f0afb1af4d6")
        {
            var iberia=s.Copy();int capital=iberia.History.Polities[3408].CapitalId;var t=iberia.Society.Settlements[capital];var before=engine.LocalSeedsAt(iberia,capital)[0].Cultivar;double yieldBefore=(double)Call(engine,"LivingCropLandscape",iberia,t.CultureId,t.Cell,false);string globalBefore=StateHash.Compute(iberia.History.Agriculture[51]);
            for(int step=0;step<50;step++){iberia.Years+=20;Call(engine,"AdvanceLocalTownSeeds",iberia,capital);}var after=engine.LocalSeedsAt(iberia,capital)[0].Cultivar;double yieldAfter=(double)Call(engine,"LivingCropLandscape",iberia,t.CultureId,t.Cell,false);
            check(t.CultureId==51&&t.Cell==163381&&after.SelectedYears>before.SelectedYears&&after.DaylengthLatitudeTenths>before.DaylengthLatitudeTenths&&yieldAfter>yieldBefore&&StateHash.Compute(iberia.History.Agriculture[51])==globalBefore,"Lifufi's actual Iberian capital independently adapts carried grain and local yield while its distant Chinese culture practice remains unchanged.");
            Console.WriteLine($"Lifufi actual Iberian capital{capital}/field{t.Cell}: selected{before.SelectedYears}->{after.SelectedYears}, daylength{before.DaylengthLatitudeTenths}->{after.DaylengthLatitudeTenths}, landscape{yieldBefore:F6}->{yieldAfter:F6}; controlled1000yr field practice, not a historical replay.");
        }
        Call(engine,"ValidateLocalSeedlots",s);s.Seal();
        if(cold)
        {
            engine.Validate(s);world.Prehistory=s;var restoredWorld=WorldFile.Read(WorldFile.Write(world));var restored=restoredWorld.Prehistory;var fresh=new Prehistory(restoredWorld);fresh.Validate(restored);
            check(restored.Hash==s.Hash&&restored.History.Aggregates.Empires.Connections.LocalSeeds.Towns.Fingerprint()==s.History.Aggregates.Empires.Connections.LocalSeeds.Towns.Fingerprint(),"Full-world serialization retains exact local seed inventories, traits, source identities and practice clocks.");
            var full=engine.AdvanceYears(s,12);var split=fresh.AdvanceYears(fresh.AdvanceYears(restored,5),7);engine.Validate(full);check(full.Hash==split.Hash,"Actual local-seed calendar continuation matches cold save/load and5+7-year publication partitions.");
        }
        check(source.ComputeHash()==original,"Actual fixture probes and seed upgrade leave the original saved world exactly unchanged.");
        Console.WriteLine($"Local seedlots: culture{pair.A.Culture}, town{pair.A.Town}/field{pair.A.Cell} vs town{pair.B.Town}/field{pair.B.Cell}, group{pair.A.Group}, selection{initial.SelectedYears}->{local.Cultivar.SelectedYears}, latitude{initial.DaylengthLatitudeTenths}->{local.Cultivar.DaylengthLatitudeTenths}, target{target}; cold={cold}.");
    }
}

using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryDelegationTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalDelegationEffects.CommunicationReachM(100000,false,false,0)==100000,"Without writing, capital communication does not extend local service");
        check(HistoricalDelegationEffects.CommunicationReachM(100000,true,true,500)>HistoricalDelegationEffects.CommunicationReachM(100000,true,false,500),"Roads support long-distance capital communication independently of local administration");
        check(HistoricalDelegationEffects.RelayOverheadM(1000000)>HistoricalDelegationEffects.RelayOverheadM(100000),"A distant regional office retains capital communication overhead");
        check(HistoricalDelegationEffects.IntegrationStrain(300000,100000,2)>HistoricalDelegationEffects.IntegrationStrain(50000,100000,2),"Integration strain uses the transport/service network rather than an unconditional geographic radius");
        check(HistoricalDistrictEffects.LandServiceCost(10000,.5)==HistoricalDistrictEffects.LandServiceCost(2500,.5)*4,"Land administration cost depends on physical area, not terrain-cell count");
        check(HistoricalDistrictEffects.Opportunity(80,10000,700,0)==HistoricalDistrictEffects.Opportunity(20,2500,700,0),"Equal benefit density gives equal administrative opportunity across mesh resolutions");
        check(HistoricalDistrictEffects.AreaKm2(false,false)<HistoricalDistrictEffects.AreaKm2(true,false)&&HistoricalDistrictEffects.AreaKm2(true,false)<HistoricalDistrictEffects.AreaKm2(true,true),"Early local districts remain smaller than literate road-connected administrations");
        check(HistoricalFaithEffects.CompetitiveExposure(100,100)==0&&HistoricalFaithEffects.CompetitiveExposure(100,105)==0,"Balanced or marginal neighboring belief cannot force continual reciprocal conversion");
        check(HistoricalFaithEffects.CompetitiveExposure(100,180)==80&&HistoricalFaithEffects.CompetitiveExposure(0,500)==300,"Sustained dominant competing support can accumulate, with bounded exposure");
        var districtCopySource=new HistoricalIncorporation{Cells=new[]{1,2,3}};var copiedDistrict=districtCopySource.Copy();copiedDistrict.Cells[0]=9;check(districtCopySource.Cells[0]==1,"Administrative district membership is deep-copied rather than aliased across checkpoints");
        check(HistoricalUrbanEffects.ChronicLossRate(100000,0,.5)>HistoricalUrbanEffects.ChronicLossRate(1000,0,.5),"Chronic crowding burden increases gradually with settlement scale");
        check(HistoricalUrbanEffects.ChronicLossRate(100000,800,.5)<HistoricalUrbanEffects.ChronicLossRate(100000,0,.5),"Public works buffer, but do not remove, the crowding burden");
        check(HistoricalUrbanEffects.MigrationRate(.7,1,1,750,false)>HistoricalUrbanEffects.MigrationRate(.7,.2,1,750,false),"Local access affects rural migration");
        check(HistoricalUrbanEffects.MigrationRate(.7,1,1,750,true)<HistoricalUrbanEffects.MigrationRate(.7,1,1,750,false),"An ongoing epidemic discourages arrivals");
        check(HistoricalUrbanEffects.MigrationRate(0,1,1,750,false)==0&&HistoricalUrbanEffects.MigrationRate(.7,0,1,750,false)==0,"Full destinations and absent access cannot attract migrants");
        check(HistoricalUrbanEffects.BoundedMigration(1000,100,250)==100&&HistoricalUrbanEffects.BoundedMigration(1000,1000,25)==25,"Migration cannot create residents or overfill destinations");
        double War(int ambition,int skill,long funds,int exhaustion=0,double defense=2000)=>HistoricalStrategicEffects.DecadalWarChance(ambition,skill,700,exhaustion,funds,5000,defense,0,false,false,0);
        check(War(900,900,100000)>War(300,400,100000),"Expansionist leadership and military opportunities affect war decisions");
        check(War(900,900,0)==0&&War(900,900,100000,700)==0,"Insolvent or exhausted courts cannot start a discretionary campaign");
        check(War(900,900,100000,0,30000)<War(900,900,100000),"Strong opposition reduces expansionist opportunity");
        check(HistoricalStrategicEffects.DecadalWarChance(800,800,700,0,100000,5000,2000,0,false,true,50)<HistoricalStrategicEffects.DecadalWarChance(800,800,700,0,100000,5000,2000,0,false,false,50),"Trade restrains conflict without creating permanent peace");
        check(HistoricalVoyageEffects.SettlementChance(2000000,1800,450,.6,.7)>0,"Experienced oral navigators can establish rare communities beyond the prehistory route graph");
        check(HistoricalVoyageEffects.SettlementChance(2000000,100,450,.6,.7)==0&&HistoricalVoyageEffects.SettlementChance(4000000,1800,450,.6,.7)==0,"New navigators and excessively long voyages do not gain unlimited global access");
        check(HistoricalVoyageEffects.SettlementChance(1200000,1800,450,.6,.7)>HistoricalVoyageEffects.SettlementChance(3000000,1800,450,.6,.7),"Ocean settlement opportunities decrease with distance");
        check(HistoricalVoyageEffects.SettlementChance(1200000,1800,450,.6,.7,20000)>HistoricalVoyageEffects.SettlementChance(1200000,1800,450,.6,.7,1200),"More actual local coastal residents can sustain more voyaging attempts");
        check(HistoricalVoyageEffects.Provisioning(100,3000000)>HistoricalVoyageEffects.Provisioning(100,1200000)&&HistoricalVoyageEffects.Provisioning(100,1200000)>0,"Ocean passage provisioning has a real positive distance-dependent food cost");
        check(HistoricalVoyageEffects.Provisioning(0,3000000)==0,"No voyagers cannot consume a fictitious minimum cargo");
        check(HistoricalLivelihoodEffects.FishingTarget(5000,10000)>HistoricalLivelihoodEffects.FishingTarget(0,10000)&&HistoricalLivelihoodEffects.Approach(50,500,50)==62,"Livelihoods adapt gradually to occupied aquatic resources instead of remaining fixed at birthplace");
        check(2*D.Sin(3500.0/(2*6371))>.5,"The spatial voyage radius retains fractional angular units");
        var inlandPeople=new CultureIdentity{FoundedYear=0,Fishing=50,Population=50000};var oral=new HistoricalCulture{Sedentism=350};oral.Technology[0]=1;oral.Technology[(int)HistoricalTechnology.Sailing]=401;
        check(HistoricalPracticeRequirements.Supports(2000,inlandPeople,oral,Array.Empty<Settlement>(),HistoricalTechnology.Navigation,false,false,"history-11",true),"A mostly inland people can sustain oral navigation through an actual coastal specialist community");
        check(!HistoricalPracticeRequirements.Supports(2000,inlandPeople,oral,Array.Empty<Settlement>(),HistoricalTechnology.Navigation,false,false,"history-11",false),"An abstract cultural capability cannot replace the required occupied coastal community");

        string fixture=Environment.GetEnvironmentVariable("DELEGATION_FIXTURE")??"D:/WorldGen-local-history-20261013/final-tests/Earth.world";
        var world=WorldFile.Read(File.ReadAllBytes(fixture));var engine=new Prehistory(world);var legacy=world.Prehistory;
        check(legacy.History.Version=="history-10","Mechanism fixture reconstructs the accepted history10 release");
        string oldHash=legacy.Hash,geo=StateHash.Compute(world);var start=engine.UpgradeToDelegatedHistory(legacy);engine.Validate(start);
        check(start.History.Version=="history-11"&&legacy.Hash==oldHash&&legacy.ComputeHash()==oldHash,"Explicit future-model upgrade cannot mutate an old checkpoint");
        check(start.History.Archive.Count==legacy.History.Archive.Count&&start.Society.Settlements.Select(t=>t.FoundedYear).SequenceEqual(legacy.Society.Settlements.Select(t=>t.FoundedYear)),"Recorded events, cities and founding dates survive the upgrade");
        check(SimulationTime.Seconds(start)==SimulationTime.Seconds(legacy)&&SimulationClockView.EpochYear(start)==SimulationClockView.EpochYear(legacy),"Upgrade retains precise time and civilization Year0");
        var owners=engine.AssessAdministrativeOwners(start);var expectedOwners=(int[])owners.Clone();Array.Fill(owners,-123);check(engine.AssessAdministrativeOwners(start).SequenceEqual(expectedOwners),"Diagnostic callers cannot mutate cached connectivity");
        var accounts=engine.AssessAdministrativeAccounts(start);long receipts=accounts.Sum(a=>a.RuralReceipts);accounts[0].ReachM=0;accounts[0].RuralReceipts=123;check(engine.AssessAdministrativeAccounts(start).Sum(a=>a.RuralReceipts)==receipts,"Diagnostic callers cannot mutate cached annual accounts");
        var normal=engine.AdvanceSeconds(start,37*SimulationTime.Year+17*SimulationTime.Day);
        var split=engine.AdvanceSeconds(engine.AdvanceSeconds(start,12*SimulationTime.Year+5*SimulationTime.Hour),25*SimulationTime.Year+17*SimulationTime.Day-5*SimulationTime.Hour);
        check(normal.Hash==split.Hash,"Administration, migration and strategy do not depend on playback partitions");
        var freshEngine=new Prehistory(world);check(freshEngine.AdvanceSeconds(start,37*SimulationTime.Year+17*SimulationTime.Day).Hash==normal.Hash,"Fresh and warm network caches produce identical histories");
        var irrelevant=start.Copy();Array.Fill(irrelevant.Society.Knowledge,10000);irrelevant.Seal();var noKnowledge=engine.AdvanceYears(start,3);var highKnowledge=engine.AdvanceYears(irrelevant,3);Array.Clear(highKnowledge.Society.Knowledge);highKnowledge.Seal();check(noKnowledge.Hash==highKnowledge.Hash,"Local specialists and new voyages cannot revive the retired generic knowledge score");
        check(normal.History.Towns.All(t=>t.Demography!=null&&t.Demography.Year<=normal.Years),"Every recorded town has real annual demographic accounts");
        check(normal.History.Towns.Sum(t=>t.Demography.TotalRuralInflow)>0,"Actual histories retain local migration instead of only a theoretical rule");
        check(normal.History.Towns.All(t=>t.Demography.RuralInflow>=0&&t.Demography.RuralOutflow>=0),"Actual annual migration remains non-negative");
        check(normal.Society.Cultures.All(c=>c.Fishing>=0&&c.Gathering>=0&&c.Hunting>=0&&c.Herding>=0&&c.Fishing+c.Gathering+c.Hunting+c.Herding==1000),"Livelihood adaptation conserves the bounded subsistence allocation");
        check(StateHash.Compute(world)==geo,"Historical changes cannot reshape terrain, climate, hydrology or ecology");
        var town=normal.Society.Settlements.First(t=>t.Population>=100);
        check(HistoryAtlas.Read(normal,"city",town.Id).Sections.Any(s=>s.Title=="Population and movement"),"City articles explain the recorded demographic flows");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool rejected=false;try{engine.AdvanceSeconds(start,SimulationTime.Year,token:cancelled.Token);}catch(OperationCanceledException){rejected=true;}
        check(rejected&&start.ComputeHash()==start.Hash,"Cancellation leaves its source unchanged");
        world.Prehistory=normal;var restored=WorldFile.Read(WorldFile.Write(world));
        check(new Prehistory(restored).AdvanceYears(restored.Prehistory,17).Hash==engine.AdvanceYears(normal,17).Hash,"Demographic accounts and campaign context survive binary continuation");
        var json=WorldJson.Read<PrehistoryState>(WorldJson.Write(normal));engine.Validate(json);check(json.Hash==normal.Hash,"New records survive JSON reconstruction with exact integrity");
        var invalid=normal.Copy();invalid.History.Towns[0].Demography.TotalRuralInflow=-1;invalid.Seal();rejected=false;try{engine.Validate(invalid);}catch(ArgumentException){rejected=true;}check(rejected,"Corrupt demographic flow records are rejected");
        var brokenDistrict=normal.Copy();var district=brokenDistrict.History.Administration.Projects.First(p=>p.EndedYear>=0);int farCell=Enumerable.Range(0,engine.CellPatch.Length).First(i=>engine.CellPatch[i]>=0&&i!=district.Cell&&!world.Surface.Cells[district.Cell].Neighbors.Contains(i));district.Cells=new[]{district.Cell,farCell}.OrderBy(i=>i).ToArray();brokenDistrict.Seal();rejected=false;try{engine.Validate(brokenDistrict);}catch(ArgumentException e){rejected=e.Message.Contains("disconnected administrative district");}check(rejected,"Malformed district membership cannot claim disconnected territories through a saved record");
        var crossVersion=normal.Copy();crossVersion.History.Version="history-10";crossVersion.Seal();rejected=false;try{engine.Validate(crossVersion);}catch(ArgumentException){rejected=true;}check(rejected,"New authoritative records cannot be smuggled into an older historical version");
        var oldRecords=legacy.History.Towns.Select(t=>t.Demography==null).All(x=>x);check(oldRecords,"Legacy saves retain absent new fields rather than fabricated old observations");
        check(engine.UpgradeToCurrentHistory(legacy).History.Version==Prehistory.CurrentHistoryVersion,"Existing local-model worlds can explicitly adopt current rules");
        Console.WriteLine($"Delegation integration: {normal.Years}, {normal.History.Polities.Count(p=>p.EndedYear<0)} states, local arrivals {normal.History.Towns.Sum(t=>t.Demography.TotalRuralInflow)}");
        string output=Environment.GetEnvironmentVariable("DELEGATION_OUTPUT")??"D:/WorldGen-history11-20261013/mechanism-tests";Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"Earth.world"),WorldFile.Write(world));
    }
    public static void Network(string fixture,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(fixture));var engine=new Prehistory(world);var original=world.Prehistory;
        var state=original.History.Version=="history-10"?engine.UpgradeToDelegatedHistory(original):original.Copy();
        var with=engine.AssessAdministrativeOwners(state);var noOffices=state.Copy();
        foreach(var existingOffice in noOffices.History.Administration.Offices.Where(o=>o.EndedYear<0))existingOffice.EndedYear=noOffices.Years;
        noOffices.Seal();var without=engine.AssessAdministrativeOwners(noOffices);
        var extended=Enumerable.Range(0,with.Length).Where(i=>with[i]>=0&&with[i]!=without[i]).ToArray();
        check(extended.Length>0,"Paid local offices actually support districts beyond the capital's local service catchment");
        int polity=with[extended[0]];var office=state.History.Administration.Offices.First(o=>o.PolityId==polity&&o.EndedYear<0);
        var poor=state.Copy();poor.History.Polities[polity].Treasury=0;poor.Seal();var unfunded=engine.AssessAdministrativeOwners(poor);
        check(extended.Where(i=>with[i]==polity).Any(i=>unfunded[i]!=polity),"Unfunded offices cannot keep projecting service into their districts");
        var relocated=state.Copy();var town=relocated.Society.Settlements[office.TownId];int prior=town.Cell;
        int capital=relocated.Society.Settlements[relocated.History.Polities[polity].CapitalId].Cell;
        var occupied=relocated.Society.Settlements.Select(t=>t.Cell).ToHashSet();
        int remote=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>engine.CellPatch[i]>=0&&!occupied.Contains(i)&&world.Landmass[i]!=world.Landmass[capital]).OrderByDescending(i=>D.Angle(world.Surface.Cells[i].Position,world.Surface.Cells[capital].Position)).First();
        town.Cell=remote;relocated.History.CellPolity[prior]=-1;relocated.History.CellPolity[remote]=polity;relocated.Society.CellCulture[remote]=town.CultureId;relocated.History.CellStage[remote]=3;relocated.Seal();engine.Validate(relocated);
        check(engine.AssessAdministrativeOwner(relocated,remote)!=polity&&engine.AssessAdministrativeCommunication(relocated)[remote]!=polity,"Even a paid regional office cannot connect an isolated continent to its capital");
        var remoteAccounts=engine.AssessAdministrativeAccounts(relocated);check(remoteAccounts[polity].DisconnectedUrbanResidents>=town.Population,"An isolated city cannot send its inhabitants' taxes or recruits to a disconnected capital");
        long heldUrban=relocated.Society.Settlements.Where(t=>t.PolityId==polity&&t.Population>=100).Sum(t=>t.Population);check(remoteAccounts[polity].TaxableUrbanResidents+remoteAccounts[polity].DisconnectedUrbanResidents==heldUrban,"Connected and disconnected urban residents partition the actually held settlements");
        check(engine.AssessAdministrativeOwners(state).SequenceEqual(with),"Changed office/funding/ownership snapshots cannot poison warm connectivity caches");
        check(original.ComputeHash()==original.Hash&&state.ComputeHash()==state.Hash,"Network mechanism fixtures leave their source saves unchanged");
        Console.WriteLine($"Actual office-supported cells: {extended.Length}; unfunded/remote offices fail as required.");
    }
}

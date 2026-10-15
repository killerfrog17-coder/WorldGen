using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryLocalTests
{
    public static void Run(Action<bool,string> check)
    {
        check(!HistoricalLocalEffects.AgricultureOpen(99999)&&HistoricalLocalEffects.AgricultureOpen(100000)&&HistoricalLocalEffects.AgricultureOpen(200000),"One shared agricultural opening stays open across later dispersal cycles");
        check(HistoricalLocalEffects.CommunityChance(.6,.8,.05,.8,2000)>0,"A supported local community can form without a knowledge threshold");
        check(HistoricalLocalEffects.CommunityChance(.6,.8,.05,.8,2000)>HistoricalLocalEffects.CommunityChance(.6,.05,.05,.8,2000),"Seasonally concentrated resources support aggregation without a latitude bonus");
        check(HistoricalLocalEffects.CommunityChance(.2,.05,.05,.8,2000)>0,"Forest-like low-seasonality resources are rare opportunities, not prohibited");
        check(HistoricalLocalEffects.CommunityChance(.6,.8,.01,.8,2000)==0&&HistoricalLocalEffects.CommunityChance(.1,.8,.05,.8,2000)==0,"Sparse occupation or absent storable resources do not create regional institutions");
        check(HistoricalLocalEffects.FoundationReadiness(.8,.8,.8,.8,.8,.5)>HistoricalLocalEffects.FoundationReadiness(.8,.8,.8,0,0,0),"Cooperative infrastructure, concentration and exchange matter for state formation");
        check(HistoricalLocalEffects.FoundationChance(.1)==0&&HistoricalLocalEffects.FoundationChance(.8)<HistoricalInstitutionRequirements.FormationChance(.8),"States are contingent and less automatic, not a fixed population upgrade");
        check(HistoricalLocalEffects.AdministrativeReachKm(6000,500,0,false,false)<HistoricalLocalEffects.AdministrativeReachKm(6000,500,500,true,true),"Roads, accounts and public works improve limited administrative reach");
        string fixture=Environment.GetEnvironmentVariable("LOCAL_HISTORY_FIXTURE")??"artifacts/history-5/tests/Earth.world";
        if(!File.Exists(fixture))throw new InvalidOperationException("Set LOCAL_HISTORY_FIXTURE to an Earth history9 mechanism fixture, or run the history9 suite first.");
        var world=WorldFile.Read(File.ReadAllBytes(fixture));world.Prehistory=null;
        string geography=StateHash.Compute(world),climate=StateHash.Compute(world.Climate);world.Prehistory=new PrehistoryState{Version="prehistory-6",History=new HistoryState{Version="history-10"}};var engine=new Prehistory(world);var start=engine.Start(engine.Prepare());world.Prehistory=null;
        check(start.History.Version=="history-10","The accepted local formation model remains reconstructible");
        var cold=start.Copy();cold.Years=85000;cold.Seal();int patch=Enumerable.Range(0,engine.Patches.Length).Where(p=>engine.Patches[p].Capacity>100).OrderBy(p=>world.Ecology.Habitat[engine.Patches[p].Cell].MeanTenthsC).First();
        check(engine.AssessForagerCapacity(cold,patch)==engine.CapacityAt(patch,85000,false),"Dispersal-cycle intensity cannot reduce food capacity");
        check(engine.CapacityAt(patch,85000,true)<engine.AssessForagerCapacity(cold,patch),"The mechanism test actually distinguishes the removed glacial penalty");
        var high=start.Copy();Array.Fill(high.Society.Knowledge,10000);high.Seal();
        var normal=engine.AdvanceSeconds(start,120000*SimulationTime.Year);var ignored=engine.AdvanceSeconds(high,120000*SimulationTime.Year);Array.Clear(ignored.Society.Knowledge);ignored.Seal();engine.Validate(normal);
        check(normal.Hash==ignored.Hash,"Knowledge values cannot influence dispersal, culture, agriculture, towns or states");
        check(normal.Society.Knowledge.All(k=>k==0),"New simulations never accumulate the retired knowledge score");
        check(normal.Society.Cultures.Any(c=>c.FoundedYear<58000)&&normal.Society.Cultures.Select(c=>c.FoundedYear/10000).Distinct().Count()>3,"Actual cultures do not wait for or cluster at the former 58–70k threshold");
        check(normal.History.Agriculture.SelectMany(a=>a.AdoptedYear).Where(y=>y>0).All(y=>y>HistoricalLocalEffects.AgricultureOpeningYear),"Actual crop adoption respects the global opening without climate changes");
        Console.WriteLine($"Integration at {normal.Years}: cultures {normal.Society.Cultures.Length}, towns {normal.Society.Settlements.Length}, states {normal.History.Polities.Length}, medieval {HistoryAudit.MedievalCeilingYear(normal.History)}");
        check(normal.History.Polities.Length>0&&HistoryAudit.MedievalCeilingYear(normal.History)>=0,"The constrained model still produces civilizations and medieval development");
        check(StateHash.Compute(world)==geography&&StateHash.Compute(world.Climate)==climate,"Terrain, climate, hydrology and ecology remain immutable");
        var split=engine.AdvanceSeconds(engine.AdvanceSeconds(start,67891*SimulationTime.Year+13*SimulationTime.Day),52109*SimulationTime.Year-13*SimulationTime.Day);
        check(split.Hash==normal.Hash,"New formation and administration are independent of irregular playback checkpoints");
        world.Prehistory=normal;var restored=WorldFile.Read(WorldFile.Write(world));check(new Prehistory(restored).AdvanceSeconds(restored.Prehistory,37*SimulationTime.Year).Hash==engine.AdvanceSeconds(normal,37*SimulationTime.Year).Hash,"Administrative records and projects survive binary continuation");world.Prehistory=null;
        check(HistoryAtlas.Read(normal,"state",normal.History.Polities.First(p=>p.EndedYear<0).Id).Sections.Any(s=>s.Title=="Administration and expansion"),"State profiles expose the actual administrative model");

        // Deliberately isolated mechanism fixture: move a non-capital town far
        // away. It used to become a fresh influence origin regardless of access.
        var oldWorld=WorldFile.Read(File.ReadAllBytes(fixture));var oldEngine=new Prehistory(oldWorld);var legacy=oldWorld.Prehistory.Copy();
        var state=legacy.History.Polities.First(p=>p.EndedYear<0&&legacy.Society.Settlements.Any(t=>t.PolityId==p.Id&&t.Id!=p.CapitalId&&t.Population>=2500));
        var town=legacy.Society.Settlements.First(t=>t.PolityId==state.Id&&t.Id!=state.CapitalId&&t.Population>=2500);int capital=legacy.Society.Settlements[state.CapitalId].Cell;
        var occupied=legacy.Society.Settlements.Select(t=>t.Cell).ToHashSet();int remote=Enumerable.Range(0,oldWorld.Surface.Cells.Length).Where(i=>oldEngine.CellPatch[i]>=0&&!occupied.Contains(i)).OrderByDescending(i=>D.Angle(oldWorld.Surface.Cells[capital].Position,oldWorld.Surface.Cells[i].Position)).First();
        foreach(var hc in legacy.History.Cultures)for(int k=5;k<hc.Technology.Length;k++)hc.Technology[k]=0;
        town.Cell=remote;legacy.Society.CellCulture[remote]=town.CultureId;legacy.History.CellPolity[remote]=state.Id;legacy.History.CellStage[remote]=3;legacy.Seal();oldEngine.Validate(legacy);
        check(oldEngine.AssessAdministrativeOwner(legacy,remote)==state.Id,"Legacy fixture demonstrates the free distant-town projection");
        var upgraded=oldEngine.UpgradeHistoricalModel(legacy);oldEngine.Validate(upgraded);
        check(oldEngine.AssessAdministrativeOwner(upgraded,remote)!=state.Id,"A remote town no longer resets administrative distance or connects itself to the capital");
        check(upgraded.History.Archive.Count==legacy.History.Archive.Count&&upgraded.History.Polities.Select(p=>p.FoundedYear).SequenceEqual(legacy.History.Polities.Select(p=>p.FoundedYear)),"Explicit upgrade preserves historical events and dates");
        check(legacy.History.Version=="history-9"&&legacy.ComputeHash()==legacy.Hash,"Upgrade and network diagnostics cannot mutate their legacy source");
        var accounts=oldEngine.AssessAdministrativeAccounts(upgraded);check(accounts[state.Id].DisconnectedResidents>0&&accounts[state.Id].ServiceCost>0,"Disconnected territory incurs costs without automatic rural revenue");

        var projectSource=normal.Copy();var earlier=projectSource.History.Administration.Projects.First(p=>p.Outcome=="Local administration established"&&projectSource.History.Polities[p.PolityId].EndedYear<0&&projectSource.Society.Settlements[projectSource.History.Polities[p.PolityId].CapitalId].Cell!=p.Cell);
        var controlled=earlier.Copy();controlled.Id=0;controlled.StartedYear=projectSource.Years;controlled.DueYear=projectSource.Years+3;controlled.EndedYear=-1;controlled.AnnualCost=5;controlled.SetupCost=100;controlled.Outcome="Ongoing local negotiations and administrative establishment";
        projectSource.History.Administration.Projects=new[]{controlled};projectSource.History.CellPolity[controlled.Cell]=-1;
        foreach(var city in projectSource.Society.Settlements.Where(t=>t.Cell==controlled.Cell))city.PolityId=-1;
        foreach(var relation in projectSource.History.Relations){relation.WarSince=-1;relation.Trade=false;relation.Alliance=false;}
        foreach(var war in projectSource.History.Dynamics.Campaigns.Where(c=>c.EndedYear<0)){war.EndedYear=projectSource.Years;war.Outcome="Closed for isolated incorporation test";}
        var controller=projectSource.History.Polities[controlled.PolityId];controller.Treasury=10000000;controller.Cohesion=1000;projectSource.History.Dynamics.Leaders[projectSource.History.Dynamics.States[controller.Id].LeaderId].ExpectedEndYear=projectSource.Years+100;
        projectSource.Seal();engine.Validate(projectSource);
        var waiting=engine.AdvanceYears(projectSource,1);check(waiting.History.Administration.Projects[0].EndedYear<0&&waiting.History.CellPolity[controlled.Cell]<0,"Funded incorporation takes real time rather than instantly coloring its district");
        var completed=engine.AdvanceYears(projectSource,3);check(completed.History.Administration.Projects[0].Outcome=="Local administration established"&&completed.History.CellPolity[controlled.Cell]==controlled.PolityId,"Supported funded incorporation eventually establishes control");
        var debt=projectSource.Copy();debt.History.Administration.Projects[0].AnnualCost=100000000;debt.Seal();var abandoned=engine.AdvanceYears(debt,1);check(abandoned.History.Administration.Projects[0].EndedYear>=0&&abandoned.History.CellPolity[controlled.Cell]<0,"Unfunded incorporation is abandoned instead of granting territory");
        var corrupt=normal.Copy();corrupt.History.Administration.Accounts[0].ServiceCost=-1;corrupt.Seal();bool rejected=false;try{engine.Validate(corrupt);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid administrative financial records are rejected");
        check(normal.ComputeHash()==normal.Hash&&projectSource.ComputeHash()==projectSource.Hash,"Project continuation and corruption fixtures do not mutate their source");

        string output=Environment.GetEnvironmentVariable("LOCAL_HISTORY_TEST_OUTPUT")??"artifacts/history-10/tests";Directory.CreateDirectory(output);world.Prehistory=normal;File.WriteAllBytes(Path.Combine(output,"Earth.world"),WorldFile.Write(world));
        Console.WriteLine($"Local model: first culture {normal.Society.Cultures.Min(c=>c.FoundedYear)}, first crop {normal.History.Agriculture.SelectMany(a=>a.AdoptedYear).Where(y=>y>0).Min()-1}, first state {normal.History.Polities.Min(p=>p.FoundedYear)}, cultures {normal.Society.Cultures.Length}, states {normal.History.Polities.Count(p=>p.EndedYear<0)}, projects {normal.History.Administration?.Projects.Length??0}, offices {normal.History.Administration?.Offices.Length??0}");
    }
}

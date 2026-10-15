using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryTechnicalTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,Hidden)!.Invoke(engine,args)!;
    public static void Run(string path,Action<bool,string> check)
    {
        for(int k=0;k<HistoricalTechnicalGraph.Count;k++)
        {
            check(HistoricalTechnicalGraph.Requirements(k).All(r=>r.Technology>=0&&r.Technology<k&&r.Level>=1&&r.Level<=8),"Leveled prerequisites form an ordered acyclic graph: "+k);
            check(HistoricalTechnicalGraph.Cost(k,2)>HistoricalTechnicalGraph.Cost(k,1)&&HistoricalTechnicalGraph.Cost(k,3)>HistoricalTechnicalGraph.Cost(k,2),"Further levels require additional effort: "+k);
        }
        var world=WorldFile.Read(File.ReadAllBytes(path));string legacy=world.Prehistory.Hash;var engine=new Prehistory(world);
        var state=engine.UpgradeToCurrentHistory(world.Prehistory);engine.Validate(state);
        check(world.Prehistory.Hash==legacy&&state.History.Version==Prehistory.TechnicalHistoryVersion,"An explicit upgrade preserves the source checkpoint");
        check(state.History.Cultures.All(c=>c.Technical!=null)&&state.History.Aggregates.Empires!=null,"Every current actor has persisted technical/imperial accounts");
        check(SocietyView.Population(state)==SocietyView.Population(world.Prehistory),"The new rules do not create migrants or residents during migration");
        var peers=state.History.Polities.Where(p=>p.EndedYear<0).Take(3).Select(p=>p.Id).ToArray();check(peers.Length==3,"Political fixture has three live governments");
        int patron=peers[0],client=peers[1],third=peers[2];var probe=state.Copy();
        check((bool)Call(engine,"SetImperialPatron",probe,client,patron,true,"Test settlement."),"A client retains its local court under an imperial agreement");
        check(HistoricalImperialHierarchy.Sovereign(probe,client)==patron&&probe.History.Polities[client].EndedYear<0,"Subordination is not court destruction or direct annexation");
        check(probe.History.Aggregates.Empires.Governments[patron].SubjectPopulation==probe.History.Polities[client].Population,"Imperial totals receive cached population deltas");
        check((bool)Call(engine,"SetImperialPatron",probe,third,client,false,"Nested agreement."),"Constituent governments may contain delegated local governments");
        check(HistoricalImperialHierarchy.Sovereign(probe,third)==patron&&probe.History.Aggregates.Empires.Governments[patron].SubjectCount==2,"Bounded ancestor deltas preserve the overarching realm");
        check(!(bool)Call(engine,"SetImperialPatron",probe,patron,third,false,"Invalid cycle."),"Imperial allegiance cannot create cycles");
        Call(engine,"ValidateImperialSystem",probe);
        int site=probe.Society.Settlements[probe.History.Polities[client].CapitalId].Cell;
        check(SocietyView.OverlayId(probe,site,HistoryOverlay.States)==patron&&SocietyView.OverlayId(probe,site,HistoryOverlay.Territories)==client,"States and Territories expose overarching and constituent governments separately");
        check(HistoricalCells.Polity(probe,site)==client,"Imperial rendering never rewrites local ownership or culture");
        check(HistoryAtlas.Selection(probe,site,HistoryOverlay.States)!.Id==patron&&HistoryAtlas.Selection(probe,site,HistoryOverlay.Territories)!.Id==client,"Map selection follows the chosen political level");
        check((bool)Call(engine,"SetImperialPatron",probe,client,-1,false,"Test independence."),"A breakaway retains existing towns and population");
        check(HistoricalImperialHierarchy.Sovereign(probe,third)==client&&probe.History.Aggregates.Empires.Governments[patron].SubjectPopulation==0,"Breaking away takes the local delegated hierarchy, not its former empire");
        Call(engine,"ValidateImperialSystem",probe);
        int culture=state.History.Polities[patron].CultureId;var research=state.Copy();var hc=research.History.Cultures[culture];var t=hc.Technical;
        hc.Technology=new int[37];hc.Technology[0]=1;t.Levels=new int[37];t.Levels[0]=1;t.Deployment=new int[37];t.Deployment[0]=500;t.Progress=new long[37];t.Learning=new long[37];
        Call(engine,"AdvanceTechnicalResearch",research,culture,40,-1,research.Society.Cultures[culture].OriginCell);
        check(t.LastResearchSpent<=t.LastResearchBudget&&t.Allocation.Sum()<=10000&&t.LastLearningSpent<=t.LastLearningBudget,"Concurrent projects share finite research and learning budgets");
        check(t.Progress[(int)HistoricalTechnology.Computing]==0&&t.Levels[(int)HistoricalTechnology.Computing]==0,"Missing leveled prerequisites block an unrelated advanced project");
        check(state.History.Cultures[culture].Technology[(int)HistoricalTechnology.Computing]>0,"Research experiments do not mutate the public source");
        hc.Technology[(int)HistoricalTechnology.Railways]=research.Years+1;t.Levels[(int)HistoricalTechnology.Railways]=1;t.Deployment[(int)HistoricalTechnology.Railways]=0;
        check(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,research.Years)==0&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways),"Knowing the railway recipe does not create an installed railway network");
        t.Deployment[(int)HistoricalTechnology.Railways]=800;
        check(HistoricalModernEffects.LandTransport(hc,research.Years)>1&&HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways),"Practical deployment changes real logistical effects");
        var article=HistoryAtlas.Read(state,"state",patron,world);
        check(article.Sections.Any(s=>s.Technical.Length>0)&&HistoryAtlas.PlainText(article).Contains("research share"),"Profiles and plain-text exports expose levels, research allocation and deployment");
        var saved=state.Copy();saved.Seal();world.Prehistory=saved;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==saved.Hash&&loaded.Prehistory.History.Cultures[culture].Technical.Levels.SequenceEqual(state.History.Cultures[culture].Technical.Levels),"Save/load preserves the complete leveled model");
        var first=new SimulationSession(new Prehistory(world),saved);var second=new SimulationSession(new Prehistory(world),saved);
        first.AdvanceSeconds(12*SimulationTime.Year,int.MaxValue);second.AdvanceSeconds(4*SimulationTime.Year,int.MaxValue);second.AdvanceSeconds(8*SimulationTime.Year,int.MaxValue);
        check(first.Hash==second.Hash,"Technical and imperial actors are invariant to publication partitions");
        new Prehistory(world).Validate(first.LastSnapshot);
    }
}

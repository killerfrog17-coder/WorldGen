using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static class HistoryNuclearTests
{
    static object Call(Prehistory engine,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
    public static void Run(Action<bool,string> check)
    {
        int fission=(int)HistoricalTechnology.NuclearFission,weapons=(int)HistoricalTechnology.NuclearWeapons;
        check(fission==37&&weapons==38&&HistoricalTechnicalGraph.Count==39,"Nuclear capabilities append stable IDs without reinterpreting37 previous technologies");
        check(HistoricalTechnologies.Count("history-1")==15&&HistoricalTechnologies.Count("history-12")==15&&HistoricalTechnologies.Count("history-13")==37&&HistoricalTechnologies.Count("history-31")==37&&HistoricalTechnologies.Count("history-32")==39,"Capability counts retain the recorded historical version");
        check(new HistoricalTechnicalPractice().Levels.Length==37&&new HistoricalTechnicalPractice(39).Sources.All(i=>i==-1),"Legacy default technical records remain37; new sources start unknown rather than culture0");
        check(HistoricalTechnicalGraph.UsefulLevel((int)HistoricalTechnology.ScientificMethod,"history-31")==3&&HistoricalTechnicalGraph.UsefulLevel((int)HistoricalTechnology.ScientificMethod,"history-32")==5,"New prerequisites do not silently change old societies' research priorities");
        for(int k=0;k<HistoricalTechnicalGraph.Count;k++)
        {
            check(HistoricalTechnicalGraph.Requirements(k).All(r=>r.Technology>=0&&r.Technology<k&&r.Level>=1&&r.Level<=8),"Nuclear graph remains ordered and finite: "+k);
            check(HistoricalTechnicalGraph.Cost(k,2)>HistoricalTechnicalGraph.Cost(k,1)&&HistoricalTechnicalGraph.Cost(k,3)>HistoricalTechnicalGraph.Cost(k,2),"Further levels cost additional conserved work: "+k);
        }
        var levels=Enumerable.Repeat(1,39).ToArray();check(!HistoricalTechnicalGraph.Prerequisites(fission,levels),"Basic electricity and metallurgy do not suffice for independent nuclear research");
        levels[(int)HistoricalTechnology.ScientificMethod]=4;levels[(int)HistoricalTechnology.Electricity]=3;levels[(int)HistoricalTechnology.IndustrialMetallurgy]=3;
        check(HistoricalTechnicalGraph.Prerequisites(fission,levels)&&!HistoricalTechnicalGraph.Prerequisites(weapons,levels),"Civilian fission and nuclear armament are distinct capabilities");
        levels[fission]=2;levels[(int)HistoricalTechnology.ScientificMethod]=5;levels[(int)HistoricalTechnology.IndustrialMetallurgy]=4;
        check(HistoricalTechnicalGraph.Prerequisites(weapons,levels),"Supported advanced fission/science/industry can reach nuclear armament");
        check(!HistoricalTechnicalGraph.WithinCeiling(fission,HistoricalTechnologyCeiling.Industrial)&&HistoricalTechnicalGraph.WithinCeiling(fission,HistoricalTechnologyCeiling.Modern),"Industrial-or-lower technology limits exclude nuclear development");
        var demand=typeof(Prehistory).GetMethod("TechnicalDemand",BindingFlags.NonPublic|BindingFlags.Static)!;
        double peace=(double)demand.Invoke(null,new object[]{weapons,0d,0d,0d,1d,false,1d,true})!,war=(double)demand.Invoke(null,new object[]{weapons,1d,1d,0d,1d,false,1d,true})!;
        check(peace==0&&war>0,"Isolated peaceful societies receive no free independent nuclear-weapons research motivation");
        var old=new HistoricalCulture{Technology=new int[37],Technical=new HistoricalTechnicalPractice()};
        check(HistoricalNuclearEffects.CivilianEnergy(old,5000)==0&&HistoricalTechnicalGraph.Installed(old,fission)==0,"Old records can be inspected without reading beyond saved arrays");
        var current=new HistoricalCulture{Technology=new int[39],Technical=new HistoricalTechnicalPractice(39)};current.Technology[fission]=current.Technology[(int)HistoricalTechnology.Electricity]=1;
        check(HistoricalNuclearEffects.CivilianEnergy(current,5000)==0,"Nuclear design knowledge is not an installed power system");
        current.Technical.Deployment[fission]=600;current.Technical.Deployment[(int)HistoricalTechnology.Electricity]=800;
        check(HistoricalNuclearEffects.CivilianEnergy(current,5000)==.48,"Civilian nuclear capability depends on both actual nuclear and electric deployment");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var before=world.Prehistory;string original=before.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLivingWorldHistory(before);engine.Validate(s);int fission=37,weapons=38;
        check(before.ComputeHash()==original&&before.History.Cultures.All(c=>c.Technology.Length==37),"Explicit nuclear extension preserves the original37-node checkpoint and hash");
        check(s.History.Cultures.All(c=>c.Technology.Length==39&&c.Technical.Levels.Length==39&&c.Technical.Sources.Skip(37).All(i=>i==-1)&&c.Technology.Skip(37).All(i=>i==0)),"Upgrade appends unknown research without awarding a discovery, power plant or weapon");
        var next=engine.AdvanceYears(s,10);var split=engine.AdvanceYears(engine.AdvanceYears(s,3),7);check(next.Hash==split.Hash,"39-node research and contact are partition independent");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==next.Hash&&engine.AdvanceYears(loaded.Prehistory,3).Hash==engine.AdvanceYears(next,3).Hash,"Expanded technical arrays preserve saved continuation");
        var probe=s.Copy();int culture=Enumerable.Range(0,probe.History.Cultures.Length).OrderByDescending(c=>probe.History.Aggregates.Cultures[c].SupportedUrban).First();var hc=probe.History.Cultures[culture];var t=hc.Technical;var account=probe.History.Aggregates.Cultures[culture];
        account.SupportedCities=4;account.SupportedUrban=500000;account.CivilizationSince=probe.Years-1000;hc.Institution=hc.Surplus=1000;
        for(int k=0;k<37;k++){hc.Technology[k]=1;t.Levels[k]=8;t.LevelYear[k]=1;t.Deployment[k]=1000;}
        t.Progress[fission]=HistoricalTechnicalGraph.Cost(fission,1,probe.History.Version);int site=probe.Society.Cultures[culture].OriginCell;
        Call(engine,"AdvanceTechnicalResearch",probe,culture,40,-1,site);
        check(t.Levels[fission]>=1&&hc.Technology[fission]>0&&t.Deployment[fission]==0,"A funded supported scientific program can discover fission without retroactive instant deployment");
        check(t.LastResearchSpent<=t.LastResearchBudget&&t.LastLearningSpent<=t.LastLearningBudget&&t.Allocation.Sum()<=10000,"Nuclear projects still share the one finite specialist budget");
        check(t.Levels[weapons]==0,"Civilian discovery does not automatically grant nuclear weapons");
        t.Levels[fission]=0;hc.Technology[fission]=0;t.Deployment[fission]=0;t.Offers[weapons]=1;t.OfferYear[weapons]=probe.Years;t.Sources[weapons]=(culture+1)%probe.History.Cultures.Length;t.Learning[weapons]=HistoricalTechnicalGraph.Cost(weapons,1,probe.History.Version)/8;
        Call(engine,"AdvanceTechnicalResearch",probe,culture,40,-1,site);
        check(t.Levels[weapons]>0&&t.Deployment[weapons]==0,"An imported weapon design cannot conjure its absent nuclear/industrial deployment chain");
        check(TechnicalResearchView.Read(before,culture).All(p=>p.Id<37)&&HistoricalTechnologyCoverage.Measure(before).Length==39,"New catalogs safely render an unchanged37-node old save");
    }
}

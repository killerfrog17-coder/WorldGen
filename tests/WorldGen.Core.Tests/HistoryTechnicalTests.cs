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
        int province=HistoricalProvinceIdentity.Of(probe,site);
        check(province>=0&&HistoryAtlas.Selection(probe,site,HistoryOverlay.Provinces)!.Kind=="province","A province is independently selectable beneath its local and imperial governments");
        check(HistoryAtlas.Read(probe,"province",province,world).Introduction.Contains("administrative"),"Province profiles explain their actual administrative role");
        check(HistoryAtlas.Selection(probe,site,HistoryOverlay.States)!.Id==patron&&HistoryAtlas.Selection(probe,site,HistoryOverlay.Territories)!.Id==client,"Map selection follows the chosen political level");
        check((bool)Call(engine,"SetImperialPatron",probe,client,-1,false,"Test independence."),"A breakaway retains existing towns and population");
        check(HistoricalImperialHierarchy.Sovereign(probe,third)==client&&probe.History.Aggregates.Empires.Governments[patron].SubjectPopulation==0,"Breaking away takes the local delegated hierarchy, not its former empire");
        check(probe.History.Aggregates.Empires.Governments[patron].Height==0&&probe.History.Aggregates.Empires.Governments[client].Height==1,"Removing a branch reduces cached hierarchy heights without scanning its subjects");
        Call(engine,"ValidateImperialSystem",probe);
        var chain=state.Copy();var chainIds=chain.History.Polities.Where(p=>p.EndedYear<0).Take(9).Select(p=>p.Id).ToArray();
        for(int k=1;k<8;k++)check((bool)Call(engine,"SetImperialPatron",chain,chainIds[k],chainIds[k-1],false,"Bounded hierarchy test."),"A supported local hierarchy may use depth "+k);
        check(!(bool)Call(engine,"SetImperialPatron",chain,chainIds[8],chainIds[7],false,"Excessive hierarchy."),"An eighth nested delegation is rejected before creating an unresolvable graph");
        check(HistoricalImperialHierarchy.Sovereign(chain,chainIds[7])==chainIds[0],"The maximum supported delegation depth resolves deterministically");Call(engine,"ValidateImperialSystem",chain);
        var wrongPayroll=state.Copy();wrongPayroll.History.Aggregates.Empires.Governments[patron].AdministrativePostCost++;bool rejectedPayroll=false;
        try{Call(engine,"ValidateTerritorialAdministration",wrongPayroll);}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){rejectedPayroll=true;}
        check(rejectedPayroll,"A saved province payroll must match actual active offices");
        var territorialCopy=state.Copy();int originalSource=state.History.Aggregates.Empires.Territories.Sources[site];Call(engine,"AssignTerritorialSource",territorialCopy,site,state.History.Polities[patron].CapitalId);
        check(state.History.Aggregates.Empires.Territories.Sources[site]==originalSource,"Administrative source writes cannot mutate a published map");
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
        var oldMigrants=new HistoricalCulture{Technology=new int[37]};
        check(HistoricalTechnicalMigration.ResidencyYears(400,300,.5,oldMigrants,100)==1610,"Legacy/forager residency remains geography-dependent and unchanged");
        var modernMigrants=new HistoricalCulture{Technology=new int[37],Technical=new HistoricalTechnicalPractice()};
        modernMigrants.Technology[(int)HistoricalTechnology.Railways]=1;
        check(HistoricalTechnicalMigration.ResidencyYears(400,300,.5,modernMigrants,100)==1610,"Merely knowing a railway recipe does not accelerate a settlement front");
        modernMigrants.Technical.Deployment[(int)HistoricalTechnology.Railways]=800;
        check(HistoricalTechnicalMigration.ResidencyYears(400,300,.5,modernMigrants,100)<150,"A deployed railway network removes millennia-long forager residency delays");
        check(HistoricalTechnicalMigration.SettlementInvestment(modernMigrants,100)>4,"Actual transport makes provisioned descendant settlements easier to establish");
        check(HistoricalTechnicalMigration.CarriedDeployment((int)HistoricalTechnology.Writing,800)==500,"Migrants retain portable accounting skills rather than relearning writing from scratch");
        check(HistoricalTechnicalMigration.CarriedDeployment((int)HistoricalTechnology.Railways,800)==0&&HistoricalTechnicalMigration.CarriedDeployment((int)HistoricalTechnology.IndustrialMetallurgy,800)==0,"Knowing industrial technology does not let emigrants carry away an installed industrial network");
        check(HistoricalTechnicalGraph.Requirements((int)HistoricalTechnology.Navigation).All(r=>r.Technology!=(int)HistoricalTechnology.Writing)&&HistoricalTechnicalGraph.Requirements((int)HistoricalTechnology.OceanNavigation).All(r=>r.Technology!=(int)HistoricalTechnology.Steel),"Oral maritime traditions do not require writing or steel");
        check(HistoricalTechnicalGraph.WithinCeiling((int)HistoricalTechnology.OceanNavigation,HistoricalTechnologyCeiling.Medieval)&&!HistoricalTechnicalGraph.WithinCeiling((int)HistoricalTechnology.SteamPower,HistoricalTechnologyCeiling.Medieval),"A medieval ceiling allows traditional ocean navigation without allowing steam industry");
        var oceanCraft=new HistoricalCulture{Technology=new int[37],Technical=new HistoricalTechnicalPractice()};int oceanId=(int)HistoricalTechnology.OceanNavigation;
        oceanCraft.Technology[oceanId]=1;oceanCraft.Technical.Levels[oceanId]=1;oceanCraft.Technical.Deployment[oceanId]=800;int earlyRange=HistoricalModernVoyageEffects.RangeM(oceanCraft,100);
        oceanCraft.Technical.Levels[oceanId]=4;
        check(earlyRange<4000000&&HistoricalModernVoyageEffects.RangeM(oceanCraft,100)>earlyRange,"Further navigation levels expand range; the first ocean craft is not an instant intercontinental fleet");
        check(WorldEraView.CultureEra(oceanCraft)<7,"Independent traditional ocean navigation does not turn a people into an early-modern industrial society");
        var maritime=state.Copy();var maritimeCulture=maritime.Society.Cultures[culture];var maritimeHistory=maritime.History.Cultures[culture];var maritimeAccount=maritime.History.Aggregates.Cultures[culture];
        maritimeCulture.Fishing=400;maritimeCulture.FoundedYear=maritime.Years-5000;maritimeAccount.CivilizationSince=-1;maritimeAccount.SupportedUrban=2000;maritimeHistory.Surplus=300;maritimeHistory.Technology[(int)HistoricalTechnology.Writing]=maritimeHistory.Technology[(int)HistoricalTechnology.Steel]=0;
        check((bool)Call(engine,"AggregatePracticeSupported",maritime,culture,(int)HistoricalTechnology.OceanNavigation,false),"A provisioned maritime craft tradition can innovate without a literate state or steel industry");
        int computing=(int)HistoricalTechnology.Computing;hc.Technology[computing]=0;t.Levels[computing]=0;t.Deployment[computing]=0;t.Offers[computing]=1;t.OfferYear[computing]=research.Years;t.Sources[computing]=state.History.Polities[client].CultureId;t.Learning[computing]=HistoricalTechnicalGraph.Cost(computing,1)/8;
        Call(engine,"AdvanceTechnicalResearch",research,culture,40,-1,research.Society.Cultures[culture].OriginCell);
        check(t.Levels[computing]==1&&hc.Technology[computing]>0&&t.Deployment[computing]==0,"Delivered learning can convey advanced knowledge without pretending a computer industry has been installed");
        var demand=typeof(Prehistory).GetMethod("TechnicalDemand",BindingFlags.NonPublic|BindingFlags.Static)!;
        double peace=(double)demand.Invoke(null,new object[]{16,0d,0d,0d,1d,false,1d,true})!,war=(double)demand.Invoke(null,new object[]{16,1d,1d,0d,1d,false,1d,true})!;
        check(peace==0&&war>peace,"No military threat means no free gunpowder-research incentive; war creates demand");
        double lowDisease=(double)demand.Invoke(null,new object[]{23,0d,0d,0d,1d,false,1d,true})!,highDisease=(double)demand.Invoke(null,new object[]{23,0d,0d,1d,1d,false,1d,true})!;
        check(highDisease>lowDisease*3,"Medical investigation responds strongly to disease pressure rather than just elapsed years");
        var seafaring=new HistoricalCulture{Technology=new int[37],Technical=new HistoricalTechnicalPractice()};seafaring.Technology[17]=1;seafaring.Technical.Deployment[17]=1000;
        check(WorldEraView.CultureEra(seafaring)<7,"One ocean-going boat tradition does not declare an early-modern society");
        seafaring.Technology[15]=1;seafaring.Technical.Deployment[15]=1000;
        check(WorldEraView.CultureEra(seafaring)==7,"Era presentation recognizes a deployed technological bundle");
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

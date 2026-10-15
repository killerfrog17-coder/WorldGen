using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryEmergenceTests
{
    public static void Run(string source,Action<bool,string> check)
    {
        double Native(double fiscal,double storage,double cooperation,double isolation,double exchange)
            =>HistoricalEmergenceEffects.OriginMultiplier(fiscal,storage,cooperation,isolation,exchange,0);
        double prime=Native(.8,.7,1,.7,.6),marginal=Native(.25,.25,.1,.1,.1);
        check(prime>marginal*40,"Independent institutions strongly favor exceptional concentrated support");
        check(marginal>0,"Marginal worlds retain a rare independent path, without biome bans");
        check(HistoricalEmergenceEffects.OriginMultiplier(.8,.7,1,.7,.6,800)>prime*8,"Real institutional examples substantially favor diffusion over reinvention");
        check(HistoricalEmergenceEffects.OriginMultiplier(.2,.2,.1,.1,0,1000)<.66,"Diffusion does not replace other founding prerequisites");
        double abundant=HistoricalEmergenceEffects.CultivationIncentive(.4,.45,.3,1),pressed=HistoricalEmergenceEffects.CultivationIncentive(1,.9,.8,.2);
        check(pressed>abundant*5,"Strong foraging alternatives reduce cultivation incentives");
        check(HistoricalEmergenceEffects.NativePractice(.9,pressed)>HistoricalEmergenceEffects.NativePractice(.2,pressed)*100,"Domesticable progenitors, not merely edible wild plants, constrain independent practice");
        check(HistoricalEmergenceEffects.NativePractice(0,abundant)>0,"Rare long-tail domestication remains possible");
        check(HistoricalEmergenceEffects.FarmingLimit(.25,abundant)<200,"Low-return cultivation remains a mixed livelihood");
        check(HistoricalEmergenceEffects.FarmingLimit(1,1)==850,"Effective established cultivation can support intensive farming");
        var fertile=new HabitatCell{GrowingDays=240,SoilFertilityPermille=750};
        var frozen=new HabitatCell{GrowingDays=70,SoilFertilityPermille=200,PermafrostPermille=700};
        check(HistoricalEmergenceEffects.EcologicalConcentration(fertile,.8,.45,0)>HistoricalEmergenceEffects.EcologicalConcentration(frozen,.8,.45,0)*10,"Soil and growing season distinguish native regional concentration");
        check(HistoricalEmergenceEffects.EcologicalConcentration(fertile,.8,0,.8)>.2,"Concentrated aquatic food supports a non-farming cultural pathway");
        check(HistoricalEmergenceEffects.EarlyGovernmentScale(false,false,false)<HistoricalEmergenceEffects.EarlyGovernmentScale(true,true,true),"Transport and administrative practices expand supported state reach");
        var world=WorldFile.Read(File.ReadAllBytes(source));var engine=new Prehistory(world);var original=world.Prehistory;string oldHash=original.Hash;
        var s=engine.UpgradeToCurrentHistory(original);check(s.History.Version==Prehistory.CurrentHistoryVersion,"Explicit upgrade selects the current rules");
        var reach=typeof(Prehistory).GetMethod("MarketReach",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var initialCulture=new HistoricalCulture();int site=s.Society.Settlements[0].Cell;
        double basic=(double)reach.Invoke(engine,new object[]{s,initialCulture,site})!,legacy=(double)reach.Invoke(engine,new object[]{original,initialCulture,site})!;
        check(basic<legacy,"New settlements cannot draw food from the former oversized pre-transport catchment");
        initialCulture.Technology[(int)HistoricalTechnology.Roads]=initialCulture.Technology[(int)HistoricalTechnology.Writing]=1;
        check((double)reach.Invoke(engine,new object[]{s,initialCulture,site})!>basic,"Roads and accounts expand actual provisioning catchments");
        check(original.Hash==oldHash&&original.ComputeHash()==oldHash,"Upgrade preserves the original world and legacy integrity");
        if(s.History.Towns.Length==0)throw new ArgumentException("Use a checkpoint with towns.");
        var copy=s.Copy();copy.History.Towns[0].CivicTeacherPolityId=0;copy.Seal();check(copy.Hash!=s.Hash,"Institutional teacher provenance participates in integrity hashes");
        var memoryRoundTrip=WorldFile.Read(WorldFile.Write(WithState(world,copy))).Prehistory;
        check(memoryRoundTrip.History.Towns[0].CivicTeacherPolityId==0&&memoryRoundTrip.Hash==copy.Hash,"Actual institutional teacher provenance survives saving");
        var learning=s.Copy();var target=learning.Society.Settlements.First(t=>t.PolityId<0);var teacher=learning.Society.Settlements.First(t=>t.PolityId>=0&&learning.Years-learning.History.Polities[t.PolityId].FoundedYear>=50&&t.Population>=1000&&t.FoodCapacity>=t.Population*.8);
        var method=typeof(Prehistory).GetMethod("LearnLocalInstitutions",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        learning.History.Towns[target.Id].CivicExampleExposure=0;
        for(int i=0;i<120;i++)method.Invoke(engine,new object[]{learning,target,new[]{teacher.Id}});
        check(learning.History.Towns[target.Id].CivicExampleExposure==600&&learning.History.Towns[target.Id].CivicTeacherPolityId==teacher.PolityId,"Sustained actual neighboring examples accumulate local experience");
        method.Invoke(engine,new object[]{learning,target,Array.Empty<int>()});
        check(learning.History.Towns[target.Id].CivicExampleExposure==598&&learning.History.Towns[target.Id].CivicTeacherPolityId==teacher.PolityId,"Lost contact erodes exposure without erasing recorded experience");
        var changed=s.Copy();changed.History.Agriculture[0].Practice[0]++;changed.Seal();check(changed.Hash!=s.Hash,"New model hashes crop practice, including histories affected by the old omission");
        var roundTrip=WorldFile.Read(WorldFile.Write(WithState(world,s))).Prehistory;check(roundTrip.Hash==s.Hash,"New history survives binary saving and loading");
        var a=engine.AdvanceYears(s,10);var restored=new Prehistory(world).AdvanceYears(roundTrip,10);check(a.Hash==restored.Hash,"Restored continuation matches the original branch");
        var early=s.History.Cultures[0].Copy();Array.Fill(early.Technology,1);var people=s.Society.Cultures[0].Copy();people.Farming=800;early.Institution=800;early.Surplus=800;
        var town=new Settlement{Population=30000,FoodCapacity=50000,Stability=800,PolityId=0};
        check(HistoricalPracticeRequirements.Supports(200000,people,early,new[]{town},HistoricalTechnology.Printing,false,false,"history-15"),"New rules retain post-medieval technological development");
        check(!HistoricalPracticeRequirements.Supports(200000,people,early,new[]{town},HistoricalTechnology.Printing,false,false,"history-14"),"Previously saved history14 keeps its original technology behavior");
        town.PolityId=-1;
        check(!HistoricalPracticeRequirements.Supports(200000,people,early,new[]{town},HistoricalTechnology.Printing,false,false,"history-15"),"Large decentralized settlements do not independently skip straight to industrial research institutions");
        check(!HistoricalPracticeRequirements.Supports(200000,people,early,new[]{town},HistoricalTechnology.Writing,false,false,"history-15"),"Independent formal civic accounts require an established court");
        check(HistoricalPracticeRequirements.Supports(200000,people,early,new[]{town},HistoricalTechnology.Writing,true,false,"history-15"),"Borrowed writing can spread to locally supported decentralized settlements");
        if(a.History.Polities.Length>0)
        {
            var invalid=a.Copy();invalid.History.Towns[0].CivicTeacherPolityId=invalid.History.Polities.Length;invalid.Seal();bool rejected=false;try{engine.Validate(invalid);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid institutional provenance is rejected");
        }
    }
    static World WithState(World world,PrehistoryState state){world.Prehistory=state;return world;}
}

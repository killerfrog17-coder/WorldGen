using WorldGen.Core;
public static class HistoryEraTests
{
    public static void Run(Action<bool,string> check)
    {
        HistoryDiplomacyTests.Run(check);
        HistoryDependencyTests.Run(check);
        HistoryResilienceTests.Run(check);
        check(HistoricalSuccessionEffects.CrisisChance(400,600,600,1,true)==0,"One-town polity cannot fragment into a nonexistent rival urban court");
        check(HistoricalSuccessionEffects.CrisisChance(400,600,600,4,true)>HistoricalSuccessionEffects.CrisisChance(900,50,0,4,false),"Strained personal empires differ from supported council transitions");
        check(HistoricalSuccessionEffects.DivideFunds(1000,300,1000)==300&&HistoricalSuccessionEffects.DivideFunds(1000,3000,1000)==1000,"Separating courts divide existing finances and cannot exceed the old treasury");
        check(HistoricalSuccessionEffects.DivideFunds(0,300,1000)==0&&HistoricalSuccessionEffects.DivideFunds(1000,300,0)==0,"No new funds arise from empty financial or demographic support");
        check(!HistoricalSuccessionEffects.RegionalBase(2,6000,10000)&&!HistoricalSuccessionEffects.RegionalBase(4,3000,10000),"A small faction cannot manufacture a regional breakaway court");
        check(!HistoricalSuccessionEffects.RegionalBase(8,5000,50000)&&HistoricalSuccessionEffects.RegionalBase(8,15000,50000),"Separating courts require a substantial supported regional bloc");
        double dispersed=HistoricalInstitutionRequirements.Readiness(.8,.2,.1,.1,.8);
        double concentrated=HistoricalInstitutionRequirements.Readiness(.8,.9,.8,.8,.8);
        check(concentrated>dispersed,"Concentration, shared infrastructure and restricted alternatives support state institutions without a desert label");
        check(HistoricalInstitutionRequirements.Readiness(0,1,1,1,1)==0&&HistoricalInstitutionRequirements.FormationChance(.1)==0,"Environment alone cannot found a state without stored surplus and institutions");
        check(HistoricalInstitutionRequirements.Readiness(1,1,1,1,0)==0,"Surplus alone cannot substitute for institutional support");
        check(HistoricalLeadershipEffects.AdministrativeReachKm(2500,600,0,false,false)<HistoricalLeadershipEffects.AdministrativeReachKm(200000,600,0,false,false),"Small city-state has less administrative reach than a supported large population");
        check(HistoricalLeadershipEffects.AdministrativeReachKm(200000,800,600,true,true)>HistoricalLeadershipEffects.AdministrativeReachKm(200000,300,0,false,false),"Administration, infrastructure and transport support wider political reach");
        check(HistoricalLeadershipEffects.TreatyTrustBonus(900,900)>HistoricalLeadershipEffects.TreatyTrustBonus(200,200)&&HistoricalLeadershipEffects.TreatyFailureChance(900,900)<HistoricalLeadershipEffects.TreatyFailureChance(200,200),"Diplomatic ability affects trust and succession treaty renewal");
        check(HistoricalLeadershipEffects.IntegrationStrain(3000,500,20,5)>HistoricalLeadershipEffects.IntegrationStrain(500,500,20,5),"Remote provincial cities strain integration even in a many-city state");
        check(HistoricalLeadershipEffects.IntegrationStrain(3000,900,20,5)<HistoricalLeadershipEffects.IntegrationStrain(3000,300,20,5),"Supported administration can reduce rather than simply veto imperial expansion");
        check(HistoricalLeadershipEffects.TransferableFunds(100,0,200)==100&&HistoricalLeadershipEffects.TransferableFunds(100,999999990,100)==10,"War settlements cannot spend missing funds or overflow the recipient treasury");
        long tribute=HistoricalLeadershipEffects.TransferableFunds(1000,200,300);check((1000-tribute)+(200+tribute)==1200,"War indemnities transfer existing state funds without creating wealth");
        check(HistoricalLeadershipEffects.TransferableFunds(1000,1000000000,1000)==0&&HistoricalLeadershipEffects.TransferableFunds(1000,0,-1)==0,"Full treasury and negative demands cannot create transfers");
        foreach(object record in new object[]{new HistoricalLeader(),new HistoricalStateDynamics(),new HistoricalCampaign()})
        {
            string originalHash=StateHash.Compute(record);
            foreach(var field in record.GetType().GetFields())
            {
                object original=field.GetValue(record)!;
                object changed=field.FieldType==typeof(string)?(object)((string)original+" changed"):field.FieldType==typeof(int[])?new int[]{42}:field.FieldType==typeof(HistoricalPoliticalCycle)?new HistoricalPoliticalCycle{ConquestDebt=1}:field.FieldType.IsEnum?Enum.ToObject(field.FieldType,1):field.FieldType==typeof(long)?(object)((long)original+1):(object)((int)original+1);
                field.SetValue(record,changed);check(StateHash.Compute(record)!=originalHash,"History-5 integrity includes "+record.GetType().Name+"."+field.Name);field.SetValue(record,original);
            }
        }
        var habitat=new HabitatCell{WarmTenthsC=280,ColdTenthsC=120,GrowingDays=280,AridityMilli=900,WarmSeasonDryPermille=450,SoilFertilityPermille=700};
        double grain=HistoricalCrops.Suitability(habitat,0);
        check(grain>.5,"Productive seasonal habitat supports cereal cultivation");
        habitat.AridityMilli=80;
        check(HistoricalCrops.Suitability(habitat,0)<grain*.2,"Dry hinterland cannot become productive grain country without water");
        check(HistoricalCrops.Suitability(habitat,0,1)>grain*.8,"Local freshwater can support cultivation in otherwise dry habitat");
        habitat.PermafrostPermille=1000;
        check(Enumerable.Range(0,6).All(k=>HistoricalCrops.Suitability(habitat,k,1)==0),"Permafrost blocks all current crop systems");
        habitat.PermafrostPermille=0;habitat.SalinityMilliPpt=10000;
        check(Enumerable.Range(0,6).All(k=>HistoricalCrops.Suitability(habitat,k,1)==0),"Saline habitat is not treated as a freshwater farm");
        check(HistoricalCrops.Storage[3]<HistoricalCrops.Storage[0],"Productive gardens are distinct from easily stored grain surplus");
        var c=new CultureIdentity{Farming=0,Fishing=500};var hc=new HistoricalCulture{Surplus=800,Institution=800,Sedentism=800};
        hc.Technology[0]=1;hc.Technology[2]=1;
        var camp=new[]{new Settlement{Population=400,FoodCapacity=500,Stability=800}};
        bool Supports(HistoricalTechnology t,Settlement[] towns,int year=1000,bool contact=false,bool water=true)=>HistoricalPracticeRequirements.Supports(year,c,hc,towns,t,contact,water);
        check(Supports(HistoricalTechnology.Copper,camp),"Small well-supported nonfarming community can work native copper");
        check(!Supports(HistoricalTechnology.Writing,camp),"Writing requires more than a small camp and accumulated years");
        check(!Supports(HistoricalTechnology.Navigation,camp),"Navigation cannot bypass prerequisite knowledge");
        var city=new[]{new Settlement{Population=9000,FoodCapacity=10000,Stability=800}};
        check(Supports(HistoricalTechnology.Writing,city),"Supported urban specialists permit writing without a universal farming veto");
        city[0].FoodCapacity=1000;
        check(!Supports(HistoricalTechnology.Writing,city),"Starving urban population does not provide a sustainable specialist base");
        city[0].FoodCapacity=10000;hc.Technology[3]=1;hc.Technology[6]=901;
        check(!Supports(HistoricalTechnology.Navigation,city),"Independent advanced practice needs mature prerequisites");
        check(Supports(HistoricalTechnology.Navigation,city,1000,true),"Contact can shorten learning while retaining support requirements");
        check(!Supports(HistoricalTechnology.Navigation,city,2000,false,false),"Landlocked town alone cannot support the navigation practice");
        check(Supports(HistoricalTechnology.Navigation,city,2000),"Supported water-connected city can develop navigation");
        var voyagers=new CultureIdentity{Population=1600,Fishing=500};
        var oral=new HistoricalCulture{Sedentism=500,Institution=350,Surplus=250};
        oral.Technology[(int)HistoricalTechnology.Storage]=1;oral.Technology[(int)HistoricalTechnology.Sailing]=1;
        bool Wayfinding(bool coast=true,int year=1000,bool contact=false)=>HistoricalPracticeRequirements.Supports(year,voyagers,oral,Array.Empty<Settlement>(),HistoricalTechnology.Navigation,contact,coast,"history-6");
        check(Wayfinding(),"Sustained oral maritime community can develop wayfinding without writing or urban centers");
        check(!HistoricalPracticeRequirements.Supports(1000,voyagers,oral,Array.Empty<Settlement>(),HistoricalTechnology.Navigation,false,true,"history-5"),"History-5 navigation preserves its original writing prerequisite");
        check(!Wayfinding(false),"Inland community cannot acquire ocean wayfinding merely from accumulated years");
        check(!Wayfinding(true,100),"Independent ocean wayfinding requires mature sailing experience");
        oral.Technology[(int)HistoricalTechnology.Sailing]=0;check(!Wayfinding(),"Ocean wayfinding cannot bypass boats and coastal sailing");oral.Technology[(int)HistoricalTechnology.Sailing]=1;
        voyagers.Population=500;check(!Wayfinding(true,1000,true),"Even introduced navigation requires a viable specialist-supporting community");
        voyagers.Population=900;check(!Wayfinding()&&Wayfinding(true,1000,true),"Contact-assisted learning lowers but does not erase demographic support requirements");
        voyagers.Population=1600;oral.Surplus=0;check(!Wayfinding(),"Unsupported maritime specialists cannot research without stored resources");
        check(HistoricalMaritimeKnowledge.CanCarryCrop(1000,901,.5),"Established food plants can be carried to a suitable habitat");
        check(!HistoricalMaritimeKnowledge.CanCarryCrop(1000,1001,.5)&&!HistoricalMaritimeKnowledge.CanCarryCrop(1000,0,.5),"Migration cannot carry future or undomesticated crops");
        check(!HistoricalMaritimeKnowledge.CanCarryCrop(1000,901,.1),"Imported crops cannot automatically thrive in unsuitable habitat");
        check(HistoricalMaritimeKnowledge.FoundingChance(300000,true,1200,.6,.7)>HistoricalMaritimeKnowledge.FoundingChance(300000,false,1200,.6,.7),"Wayfinding supports more distant founding networks without guaranteeing success");
        check(HistoricalMaritimeKnowledge.FoundingChance(600000,true,1200,.6,.7)<HistoricalMaritimeKnowledge.FoundingChance(100000,true,1200,.6,.7),"Long voyages remain rarer than short crossings");
        check(HistoricalMaritimeKnowledge.FoundingChance(100000,true,0,.6,.7)==0&&HistoricalMaritimeKnowledge.FoundingChance(100000,true,1200,0,.7)==0,"No overseas community is created without existing residents and storage opportunities");
        var ancestorRecord=new HistoricalCulture();string noAncestor=StateHash.Compute(ancestorRecord);ancestorRecord.ParentCultureId=0;
        check(StateHash.Compute(ancestorRecord)!=noAncestor,"Community ancestry participates in checkpoint integrity");
        check(HistoricalFaithEffects.Exposure(ReligiousReach.Missionary,true,false,5000)>HistoricalFaithEffects.Exposure(ReligiousReach.Local,true,false,5000),"Traveling teaching institutions use exchange networks differently from place-based practice");
        check(HistoricalFaithEffects.Exposure(ReligiousReach.Portable,false,true,5000)>HistoricalFaithEffects.Exposure(ReligiousReach.Portable,false,false,5000),"Court patronage supports propagation without imposing immediate conversion");
        check(HistoricalFaithEffects.Accumulate(2,400,2,100)==500,"Repeated contact accumulates a competing tradition's influence");
        check(HistoricalFaithEffects.Accumulate(2,400,-1,0)==330,"Unmaintained religious exposure decays rather than permanently locking in");
        check(HistoricalFaithEffects.Accumulate(2,400,3,100)<100,"Competing traditions cannot freely inherit each other's accumulated support");
        check(!HistoricalFaithEffects.CanBecomeDominant(1100,1000,900)&&!HistoricalFaithEffects.CanBecomeDominant(1500,1000,500),"Recent conversion and weak influence prevent arbitrary repeated religious color flips");
        check(HistoricalFaithEffects.CanBecomeDominant(1300,1000,700),"Sustained support can establish a new dominant tradition");
        var religiousState=new HistoricalFaithState{Candidate=new[]{2},Influence=new[]{400},LastChanged=new[]{1000}};
        var religiousCopy=religiousState.Copy();religiousCopy.Influence[0]=800;
        check(religiousState.Influence[0]==400,"Religious competition checkpoints deep-copy their local pressure");
        check(StateHash.Compute(religiousState)!=StateHash.Compute(religiousCopy),"Religious competition influences are integrity-hashed");
        var crop=new HistoricalAgriculture{NativeMask=1};crop.Practice[0]=600;crop.AdoptedYear[0]=1001;
        var copy=crop.Copy();copy.Practice[0]=900;copy.SourceCulture[0]=3;
        check(crop.Practice[0]==600&&crop.SourceCulture[0]==-1,"Crop checkpoint copies do not mutate their source");
        long[] weights={3,0,5,2};long prefix=0,totalPopulation=0;
        foreach(long weight in weights){totalPopulation+=Prehistory.AllocateRuralShare(103,prefix,weight,10);prefix+=weight;}
        check(totalPopulation==103,"Habitat-weighted rural allocation conserves exact population");
        check(Prehistory.AllocateRuralShare(103,3,0,10)==0,"Zero-capacity cell receives no phantom rural residents");
        check(Prehistory.AllocateRuralShare(100,0,8,10)==80,"Productive cells receive proportionate inhabitants");
        check(Prehistory.AllocateRuralShare(10000000000,0,2000000000,2000000000)==10000000000,"Large population allocation avoids integer-product overflow");
        var history=new HistoryState();var entry=new HistoricalEvent{Text="A recorded event",Kind="test"};history.Archive.Add(entry);
        string eventHash=StateHash.Compute(history);
        foreach(var field in typeof(HistoricalEvent).GetFields())
        {
            object original=field.GetValue(entry);
            field.SetValue(entry,field.FieldType==typeof(string)?(object)((string)original+" altered"):field.FieldType.IsEnum?Enum.ToObject(field.FieldType,1):(object)((int)original+1));
            check(StateHash.Compute(history)!=eventHash,"Compact history integrity includes "+field.Name);field.SetValue(entry,original);
        }
        entry.Text=null!;string nullText=StateHash.Compute(history);entry.Text="";
        check(StateHash.Compute(history)!=nullText,"Compact event hashing distinguishes null and empty strings");
        entry.Text="A recorded event";history.Archive.Add(entry);
        check(StateHash.Compute(history)!=eventHash,"Compact event hashing includes archive length and repeated records");
        var first=new HistoricalCulture();var second=new HistoricalCulture();history.Cultures=new[]{first,second};
        first.Technology[(int)HistoricalTechnology.Steel]=1001;second.Technology[(int)HistoricalTechnology.Navigation]=1201;second.Technology[(int)HistoricalTechnology.Scholasticism]=1301;
        check(HistoryAudit.MedievalCeilingYear(history)==-1,"Different cultures' partial capstones cannot falsely finish the era audit");
        first.Technology[(int)HistoricalTechnology.Navigation]=1401;first.Technology[(int)HistoricalTechnology.Scholasticism]=1501;
        check(HistoryAudit.MedievalCeilingYear(history)==1500,"Audit ceiling is the last capstone's actual adoption year");
        first.Technology[(int)HistoricalTechnology.Navigation]=0;check(HistoryAudit.MedievalCeilingYear(history,false)==1500&&HistoryAudit.MedievalCeilingYear(history)==-1,"Oceanless audits use actual land capstones instead of waiting forever for an irrelevant ocean capability");
        check(HistoryAudit.TargetYear(200000,1500,750)==2250,"Normal audit continues exactly 750 years after the ceiling");
        check(HistoryAudit.TargetYear(2000,1500,750)==2000&&HistoryAudit.TargetYear(2000,-1,750)==2000,"Safety limit remains bounded when no complete ceiling-tail run is possible");
    }
}

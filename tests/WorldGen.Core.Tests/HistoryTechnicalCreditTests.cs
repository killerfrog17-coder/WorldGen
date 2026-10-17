using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;

public static class HistoryTechnicalCreditTests
{
    const long Cost=1960200000,Research=1896899403,Lessons=149644289;
    static HistoricalTechnicalCredit Credit(long p=Research,long l=Lessons,long fresh=1,int version=23,int level=2,int offer=3,int received=163590,int now=163600,bool source=true,bool prerequisites=true,long cost=Cost)
        =>HistoricalTechnicalLearningCredit.Mixed(version,cost,p,l,fresh,level,offer,received,now,source,prerequisites);
    public static void Run(Action<bool,string> check)
    {
        var actual=Credit();check(actual.Acquired&&actual.ResearchUsed==Research&&actual.LessonsUsed==7912575,"Recorded c191 partial understanding closes its actual next-rank gap using exact existing research and7.912575 lesson units.");
        check(Research-actual.ResearchUsed==0&&Lessons-actual.LessonsUsed==141731714,"Mixed completion consumes every credited experiment and only the required lessons, retaining legitimate uncredited lessons without reusing research.");
        check(actual.ResearchUsed+actual.LessonsUsed*8>=Cost&&actual.ResearchUsed+actual.LessonsUsed*8-Cost<8,"Integer rounding creates no effort and wastes fewer than eight research micro-units.");
        for(int v=0;v<=22;v++)check(!Credit(version:v).Acquired,"Preceding revision"+v+" never combines banks under the new rule.");
        check(!Credit(p:0).Acquired&&!Credit(l:0).Acquired&&!Credit(fresh:0).Acquired,"No own experiments, no stored lessons, or no actual newly allocated instruction cannot impersonate this mixed completion.");
        check(!Credit(offer:2).Acquired&&!Credit(offer:0).Acquired&&!Credit(source:false).Acquired,"A remembered identity or unsupported offer cannot replace a valid next-level delivered source.");
        check(!Credit(prerequisites:false).Acquired&&!Credit(received:-1).Acquired&&!Credit(received:163601).Acquired,"Missing prerequisites, an absent receipt or a future receipt cannot finance coherent mixed learning.");
        check(Credit(received:100,now:163600).Acquired,"A valid retained old delivery remains useful without inventing a rule erasing books when their former route or sender disappears.");
        check(!Credit(level:8,offer:8).Acquired&&!Credit(offer:9).Acquired,"An offer cannot leap multiple ranks or exceed the recorded graph limit.");
        var excess=Credit(p:Cost+99,l:1);check(excess.Acquired&&excess.ResearchUsed==Cost&&excess.LessonsUsed==0,"Already paid excess experiments remain bounded: only the current cost is credited, never a negative debit or multiple-rank effect.");
        var maximum=Credit(p:1,l:long.MaxValue,cost:long.MaxValue);check(maximum.Acquired&&maximum.ResearchUsed==1&&maximum.LessonsUsed==1152921504606846976,"Maximum public long banks use overflow-safe division, not lessons-times-eight or cost-plus-seven arithmetic.");
        check(!Credit(p:-1).Acquired&&!Credit(l:-1).Acquired&&!Credit(cost:0).Acquired,"Malformed negative banks and nonpositive costs produce no acquisition.");
        for(long p=1;p<80;p++)for(long l=0;l<12;l++)
        {
            var r=Credit(p:p,l:l,cost:80);bool expected=p+8*l>=80;
            check(r.Acquired==expected&&(!r.Acquired||r.ResearchUsed<=p&&r.LessonsUsed<=l&&r.ResearchUsed+r.LessonsUsed*8>=80),"Small exact credit boundaries consume only actual available banks.");
        }
        Caller(check);
    }
    static void Caller(Action<bool,string> check)
    {
        PrehistoryState Frame(int revision)
        {
            var t=new HistoricalTechnicalPractice();foreach(var pair in new[]{(6,3),(10,2),(11,2),(14,2)}){t.Levels[pair.Item1]=pair.Item2;t.Deployment[pair.Item1]=700;t.LevelYear[pair.Item1]=160000;}
            t.Progress[14]=Research;t.Learning[14]=Lessons;t.Offers[14]=3;t.OfferYear[14]=163590;t.Sources[14]=1;
            var hc=new HistoricalCulture{Id=0,Technical=t,Technology=new int[37],Research=new int[37]};foreach(int k in new[]{6,10,11,14})hc.Technology[k]=160001;
            return new PrehistoryState{Years=163600,Population=new long[]{10000},Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0,OriginCell=0,Population=10000},new CultureIdentity{Id=1,OriginCell=0}}},History=new HistoryState{Version="history-29",Modern=new HistoricalModernSettings{TechnologyCeiling=HistoricalTechnologyCeiling.Medieval},Cultures=new[]{hc,new HistoricalCulture{Id=1,Technical=new HistoricalTechnicalPractice()}},Agriculture=new[]{new HistoricalAgriculture{CultureId=0}},Aggregates=new HistoricalAggregateState{TotalPopulation=10000,Cultures=new[]{new HistoricalCultureAggregate{Id=0}},Provinces=new HistoricalProvincialSupport{Exchanges=new[]{new HistoricalExchangeMemory()}},Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision}}}}}};
        }
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        typeof(Prehistory).GetField("World")!.SetValue(engine,new World{Recipe=new Recipe{Seed=123},Ecology=new EcologyResult{Habitat=new[]{new HabitatCell()}}});
        typeof(Prehistory).GetField("Patches")!.SetValue(engine,new[]{new Prehistory.Patch{Island=true}});typeof(Prehistory).GetField("CellPatch")!.SetValue(engine,new[]{0});
        typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(engine,new double[]{0});
        var actor=typeof(Prehistory).GetMethod("AdvanceTechnicalResearch",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var old=Frame(22);var next=Frame(23);actor.Invoke(engine,new object[]{old,0,1,-1,0});actor.Invoke(engine,new object[]{next,0,1,-1,0});
        var a=old.History.Cultures[0].Technical;var b=next.History.Cultures[0].Technical;
        check(a.Levels[14]==2&&a.Progress[14]==Research&&b.Levels[14]==3&&b.Progress[14]==0,"The actual private research caller preserves22's stalled receiver but23 settles its single mixed rank without retaining the credited experiments.");
        check(a.LastResearchBudget==b.LastResearchBudget&&a.LastResearchSpent==b.LastResearchSpent&&a.LastLearningBudget==b.LastLearningBudget&&a.LastLearningSpent==b.LastLearningSpent&&b.Learning[14]==Lessons+b.LastLearningSpent-7912575,"Actual caller budgets and allocations are identical: only settlement of already paid understanding changes.");
        check(next.History.Aggregates.TotalPopulation==10000&&next.Population[0]==10000&&b.Levels[14]==3,"The caller advances exactly one rank and creates no inhabitants, treasury or extra research effort.");
        var advancedOffer=Frame(23);advancedOffer.History.Cultures[0].Technical.Offers[14]=8;actor.Invoke(engine,new object[]{advancedOffer,0,1,-1,0});
        check(advancedOffer.History.Cultures[0].Technical.Levels[14]==3,"Even a delivered level8 offer advances only the single paid next rank in one actual actor review.");
        var unsupported=Frame(23);unsupported.History.Cultures[0].Technical.Levels[6]=2;actor.Invoke(engine,new object[]{unsupported,0,1,-1,0});
        check(unsupported.History.Cultures[0].Technical.Levels[14]==2,"The actual mixed caller cannot use funded but conceptually premature instruction to bypass a missing scholarly prerequisite.");
        foreach(bool pureLesson in new[]{true,false})
        {
            var before=Frame(22);var after=Frame(23);
            foreach(var state in new[]{before,after})
            {
                var h=state.History.Cultures[0];
                if(pureLesson)h.Technical.Learning[14]=Cost/8;
                else{h.Technical.Progress[14]=0;h.Technology[0]=160001;h.Technical.Levels[0]=1;h.Technical.Progress[0]=HistoricalTechnicalGraph.Cost(state,0,2);}
                actor.Invoke(engine,new object[]{state,0,1,-1,0});
            }
            check(StateHash.Compute(before.History.Cultures[0].Technical)==StateHash.Compute(after.History.Cultures[0].Technical)&&after.History.Cultures[0].Technical.Levels[pureLesson?14:0]==(pureLesson?3:2),"Actual caller pure "+(pureLesson?"full-lesson":"full-experiment")+" acquisition keeps every preceding bank, level, deployment and budget value literal.");
        }
    }
    public static void Recorded(string path,Action<bool,string> check)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        var culture=root.GetProperty("Technical").EnumerateArray().Single(c=>c.GetProperty("Id").GetInt32()==191);var p=culture.GetProperty("Practice");const int k=14;
        long work=p.GetProperty("Progress")[k].GetInt64(),lessons=p.GetProperty("Learning")[k].GetInt64();int level=p.GetProperty("Levels")[k].GetInt32(),offer=p.GetProperty("Offers")[k].GetInt32(),receipt=p.GetProperty("OfferYear")[k].GetInt32(),source=p.GetProperty("Sources")[k].GetInt32();
        check(root.GetProperty("Year").GetInt32()==163600&&work==Research&&lessons==Lessons&&level==2&&offer==3&&receipt==163590&&source==149,"The exact c191 experiment, lesson and dated-source fixture matches the already exported V21 Earth record, not rounded reported numbers.");
        var result=Credit(p:work,l:lessons,level:level,offer:offer,received:receipt);
        check(result.Acquired&&result.ResearchUsed==work&&result.LessonsUsed==7912575,"The recorded local catch-up case settles both existing banks at unchanged nominal rank cost.");
    }
}

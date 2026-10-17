using System.Reflection;
using System.Security.Cryptography;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryRealmClaimantTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,args);
    static HistoricalRealmLocalClaimant Seed(PrehistoryState s,int owner,int city,int support=900)
    {
        var r=s.History.Aggregates.Empires.Connections.Realms.Claimants;
        var c=new HistoricalRealmLocalClaimant{City=city,Owner=owner,DeclaredYear=s.Years,LastYear=s.Years,CrisisSinceYear=s.History.Aggregates.Empires.Connections.Realms.Governments[owner].RegimeSince,SupportMilli=support,Name="Recorded local claimant",LeaderSinceYear=s.Years,LeaderUntilYear=s.Years+20};
        int index=r.At[city];if(index<0){index=r.Records.Length;r.Records=r.Records.Append(c).ToArray();r.At=r.At.With(city,index);}else r.Records[index]=c;return c;
    }
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalRealmClaimantEffects.EmergenceHazard(0,0,1,1)==0,"A town's age or remoteness cannot invent a claimant without a real central dispute.");
        check(HistoricalRealmClaimantEffects.EmergenceHazard(.9,.1,.9,.9)>HistoricalRealmClaimantEffects.EmergenceHazard(.9,.9,.2,.1)*2,"Actual disputed authority and weak inclusion favor a supported local challenge, not an ethnicity hierarchy.");
        int supported=HistoricalRealmClaimantEffects.Support(800,20,0,.1,.9,.7,.7,.1,.05,true);
        check(supported>600&&HistoricalRealmClaimantEffects.LocalShock(supported)>0,"A supported unresolved local claim can outlast the central crisis deadline.");
        int reconciled=HistoricalRealmClaimantEffects.Support(800,80,0,.95,0,0,0,.8,.95,true);
        check(reconciled<180&&HistoricalRealmClaimantEffects.LocalShock(reconciled)==0,"Inclusion and functioning service can reconcile the claim without an automatic new country.");
        check(HistoricalRealmClaimantEffects.Support(800,30,0,.1,1,1,1,0,0,false)<180,"Loss of the actual resource center disperses support even if political grievances remain.");
        check(HistoricalRealmClaimantEffects.Support(750,0,1,0,1,1,1,0,0,true)==750,"No elapsed city time means no new political support.");
        check(HistoricalRealmClaimantEffects.CaptureRenown(0)==0&&HistoricalRealmClaimantEffects.CaptureRenown(50000)>HistoricalRealmClaimantEffects.CaptureRenown(200),"Real town captures can sustain a personal conqueror's reputation; empty land cannot.");
        var records=new HistoricalRealmClaimants{At=new HistoricalCellTree(33554432).With(7,0),Records=new[]{new HistoricalRealmLocalClaimant{City=7,Name="Local testimony",SupportMilli=900}}};var copy=records.Copy();copy.Records[0].Name="New claimant";copy.At=copy.At.With(8,1);
        check(records.Records[0].Name=="Local testimony"&&records.At[8]<0,"Claimant publication copies detach mutable local records and preserve immutable sparse roots.");
        check(StateHash.Compute(records)!=StateHash.Compute(copy),"Named local support and sparse references participate in canonical state.");
        var old=new HistoricalRealmLifecycles{Version=5,SinceYear=42,Governments=new[]{new HistoricalRealmAccount{FoundedYear=12,RegimeSince=20}}};
        string PredecessorHash()
        {
            using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
            {
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalRealmLifecycles");var write=typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic)!;
                foreach(var f in typeof(HistoricalRealmLifecycles).GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal))
                {
                    if(f.Name==nameof(HistoricalRealmLifecycles.Claimants)||f.Name==nameof(HistoricalRealmLifecycles.ClaimRoutes))continue;
                    writer.Write(f.Name);write.Invoke(null,new object[]{writer,f.GetValue(old)});
                }
            }
            return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
        }
        check(StateHash.Compute(old)==PredecessorHash(),"A null claimant extension preserves literal preceding realm5 canonical fields, not a regenerated golden.");
        old.Claimants=new HistoricalRealmClaimants{SinceYear=42};check(StateHash.Compute(old)!=PredecessorHash(),"An explicitly present claimant extension is distinguishable from old saved rules.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var source=w.Prehistory;e.Validate(source);string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source);e.Validate(s);
        check(s.History.Aggregates.Empires.Connections.Realms.Version>=6&&HistoricalRealmClaimantHistory.Enabled(s),"Only explicit revision6 upgrade enables saved local claimants.");
        check(source.ComputeHash()==original&&ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"The complete revision upgrade is idempotent and never rewrites its input.");
        var a=s.History.Aggregates;
        bool Eligible(Settlement t)
        {
            int parent=a.Resolve(t.PolityId,s.History);if(parent<0||t.Id==s.History.Polities[parent].CapitalId||t.Population<600||!a.Cities[t.Id].Supported||a.Living.Cities[t.Id].AbandonedYear>=0||s.Years-t.FoundedYear<50)return false;
            // Retain actual physical neighbors. Choose a controlled independent
            // founding case, rather than disabling a real successor contact.
            return !a.Cities[t.Id].Neighbors.Any(peer=>{int state=a.Resolve(s.Society.Settlements[peer].PolityId,s.History);return state>=0&&state!=parent&&s.History.Polities[state].ParentId==parent&&s.History.Polities[state].CultureId==t.CultureId&&s.Years-s.History.Polities[state].FoundedYear<=50;});
        }
        int city=Array.FindIndex(s.Society.Settlements,Eligible);
        check(city>=0,"The real-world fixture contains an existing noncapital administrative center.");if(city<0)return;
        int owner=a.Resolve(s.Society.Settlements[city].PolityId,s.History);
        var probe=s.Copy();var c=Seed(probe,owner,city);probe.History.Aggregates.Empires.Connections.Realms.Governments[owner].CrisisUntil=probe.Years;
        check((double)Call(e,"RealmClaimantShock",probe,owner,city)!>0&&HistoricalRealmClaimantHistory.Active(probe,c),"An actual local record retains influence after its central succession deadline, without refounding the country.");
        long funds=probe.History.Polities.Sum(p=>p.Treasury),people=probe.History.Aggregates.TotalPopulation,army=probe.History.Polities.Sum(p=>(long)p.Army);int states=probe.History.Polities.Length;
        var t=probe.Society.Settlements[city];var local=probe.History.Aggregates.Communities.Cities[city];var account=probe.History.Aggregates.Polities[owner];var dynamics=probe.History.Dynamics.States[owner];
        t.Stability=800;probe.History.Aggregates.Living.Cities[city].DeclineYears=0;probe.History.Aggregates.Cities[city].Supported=true;local.InclusionMilli=1000;dynamics.PublicWorks=1000;dynamics.Cycle.ConquestDebt=0;probe.History.Polities[owner].Exhaustion=0;account.Receipts=Math.Max(100,account.ServiceCost*2);
        for(int k=0;k<5;k++){probe.Years+=20;Call(e,"AdvanceRealmClaimant",probe,owner,city,20);}
        check(c.ResolvedYear>=0&&c.SupportMilli<180&&!HistoricalRealmClaimantHistory.Active(probe,c)&&probe.History.Polities.Length==states,"Restored inclusion and service reconcile a genuinely existing city's supported coalition, not create a mandatory successor.");
        check(c.Name!="Recorded local claimant"&&c.LeaderSinceYear>c.DeclaredYear&&c.LeaderUntilYear<=c.LeaderSinceYear+50,"A persistent political coalition receives ordinary local leadership turnover instead of one immortal named claimant.");
        check(probe.History.Polities.Sum(p=>p.Treasury)==funds&&probe.History.Aggregates.TotalPopulation==people&&probe.History.Polities.Sum(p=>(long)p.Army)==army,"Political support and local ordinary leadership create no cash, people or soldiers.");
        probe=s.Copy();c=Seed(probe,owner,city,150);var central=probe.History.Aggregates.Empires.Connections.Realms.Governments[owner];central.CrisisUntil=probe.Years+20;central.CrisisSeverity=500;
        probe.History.Aggregates.Communities.Cities[city].InclusionMilli=1000;probe.History.Aggregates.Communities.Governments[owner].RepresentationMilli=1000;probe.History.Aggregates.Living.Cities[city].DeclineYears=0;probe.Society.Settlements[city].Stability=800;probe.History.Dynamics.States[owner].Cycle.ConquestDebt=0;probe.History.Polities[owner].Exhaustion=0;
        probe.Years++;Call(e,"AdvanceRealmClaimant",probe,owner,city,1);
        check(c.ResolvedYear==probe.Years&&central.CrisisUntil>probe.Years,"A sufficiently reconciled local coalition can settle before the central crisis deadline; the central clock is not a compulsory local faction lifetime.");
        probe=s.Copy();c=Seed(probe,owner,city);Call(e,"TransferRealmClaimantCity",probe,city,owner,-1);
        check(c.ResolvedYear==probe.Years&&!HistoricalRealmClaimantHistory.Active(probe,c),"An actual loss of the claimant's center resolves its local claim rather than leaving phantom territorial influence.");
        probe=s.Copy();c=Seed(probe,owner,city);probe.History.Aggregates.Empires.Connections.Realms.Governments[owner].CrisisUntil=probe.Years;var queue=new SimulationEventQueue(probe.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;field.SetValue(e,queue);
        funds=probe.History.Polities.Sum(p=>p.Treasury);people=probe.History.Aggregates.TotalPopulation;army=probe.History.Polities.Sum(p=>(long)p.Army);states=probe.History.Polities.Length;
        // Select a positive controlled event using the project's own named
        // draw, rather than retrying whole worlds or replacing expectations.
        double shock=(double)Call(e,"RealmClaimantShock",probe,owner,city)!,hazard=HistoricalRealmEffects.ProvincialRupture(shock,10,probe.History.Aggregates.Communities.Cities[city].InclusionMilli/1000.0,probe.Society.Settlements[city].CultureId!=probe.History.Polities[owner].CultureId,probe.History.Aggregates.Communities.Governments[owner].RepresentationMilli/1000.0);
        double draw=(double)Call(e,"HistoryRoll","realm-provincial-contest-1",city,probe.Years)!;int exposure=1+(int)Math.Ceiling(-Math.Log(Math.Max(1e-12,1-draw))/hazard);bool separated;
        try{Call(e,"PrepareAggregateRelations",probe);separated=(bool)Call(e,"TryRealmRegionalSeparation",probe,owner,city,exposure,10.0)!;}finally{field.SetValue(e,null);}
        probe.Timeline.Events=queue.Ordered();probe.Seal();e.Validate(probe);
        check(separated&&probe.History.Polities.Length==states+1&&probe.History.Polities[states].Ruler==c.Name&&c.SuccessorPolity==states,"An actual supported regional contest after the central deadline installs its previously named claimant, not an unrelated invented ruling actor.");
        check(probe.History.Polities.Sum(p=>p.Treasury)==funds&&probe.History.Aggregates.TotalPopulation==people&&probe.History.Polities.Sum(p=>(long)p.Army)==army,"The actual claimant-led founding path conserves the existing total treasury, military stock and residents.");
        w.Prehistory=probe;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;
        check(restored.Hash==probe.Hash&&HistoricalRealmClaimantHistory.At(restored,city).Name==c.Name,"Full serialization retains the named claimant's actual successor and sparse city reference.");
        var full=e.AdvanceYears(probe,12);var split=new Prehistory(w).AdvanceYears(new Prehistory(w).AdvanceYears(restored,5),7);e.Validate(full);
        check(full.Hash==split.Hash,"Named succession memory, local calendars and support replay across cold save/load and12 versus5+7 years.");
        probe=s.Copy();var leader=probe.History.Dynamics.Leaders[probe.History.Dynamics.States[owner].LeaderId];int victories=leader.Victories,renown=leader.Renown;long residents=probe.Society.Settlements[city].Population;
        Call(e,"CreditRealmTownCapture",probe,owner,-1);check(leader.Victories==victories&&leader.Renown==renown,"The capture-credit hook gives no personal victory for empty land.");
        Call(e,"CreditRealmTownCapture",probe,owner,city);check(leader.Victories==victories+1&&leader.Renown==(renown>=1000?renown:Math.Min(1000,renown+HistoricalRealmClaimantEffects.CaptureRenown(residents))),"The controlled successful-transfer credit reaches the responsible current ruler and is bounded by the real town's size.");
        var legacy=source.Copy();var oldLeader=legacy.History.Dynamics.Leaders[legacy.History.Dynamics.States[owner].LeaderId];int oldVictories=oldLeader.Victories,oldRenown=oldLeader.Renown;
        Call(e,"CreditRealmTownCapture",legacy,owner,city);check(source.History.Aggregates.Empires.Connections.Realms.Version>=6||oldLeader.Victories==oldVictories&&oldLeader.Renown==oldRenown,"Pre-revision6 capture outcomes retain exact preceding leader-credit behavior.");
        var section=HistoryAtlas.Read(s,"state",owner,w,e).Sections.First(x=>x.Title=="Country and government continuity");check(section.Facts.Any(f=>f.Label=="Last contested court change")&&!section.Facts.Any(f=>f.Label=="Current regime since"),"Country profiles do not mislabel ordinary leadership continuity as an untouched millennial dynasty.");
        check(source.ComputeHash()==original,"Every political probe and publication leaves the source checkpoint unchanged.");
    }
}

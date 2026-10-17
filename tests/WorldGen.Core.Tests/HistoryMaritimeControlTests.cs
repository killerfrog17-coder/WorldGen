using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMaritimeControlTests
{
    public static void Run(Action<bool,string> check)
    {
        check(!HistoricalMaritimeControlEffects.Administration(0,1,900),"Practical ocean navigation alone cannot establish a fiscal overseas shipping service, even on a short crossing.");
        check(!HistoricalMaritimeControlEffects.Administration(.19,1,900)&&!HistoricalMaritimeControlEffects.Administration(1,.19,900)&&!HistoricalMaritimeControlEffects.Administration(1,1,349),"Writing, navigation and real institutional capacity each constrain persistent state shipping.");
        check(HistoricalMaritimeControlEffects.Administration(.2,.2,350),"A deployed literate regional maritime state remains possible without modern industry.");
        check(HistoricalMaritimeControlEffects.Reach(500,1000,0,0,0),"Ancient short regional crossings can extend an actual Mediterranean-scale port network.");
        check(!HistoricalMaritimeControlEffects.Reach(701,0,0,0,0)&&!HistoricalMaritimeControlEffects.Reach(500,1500,0,0,0),"Neither a long unsupported ancient leg nor a succession of short flags evades the court's shipping reach.");
        check(HistoricalMaritimeControlEffects.Reach(5000,2000,.8,0,0),"A developed ocean fleet can quickly supply a transoceanic colonial venture.");
        check(!HistoricalMaritimeControlEffects.Reach(5000,2000,0,0,0)&&HistoricalMaritimeControlEffects.Reach(12000,10000,1,1,1),"Powered modern supply networks have materially greater real reach than early coastal fleets.");
        var queue=new SimulationEventQueue(new[]{new SimulationEvent{Process=27,Entity=1,Due=1000,Ticket=10},new SimulationEvent{Process=27,Entity=2,Due=20,Ticket=11},new SimulationEvent{Process=23,Entity=3,Due=20,Ticket=12}});
        check(queue.TryMoveEarlier(27,1,20)&&queue.Count==3,"A prompt chart delivery advances an existing wakeup without duplicating events.");
        var order=new[]{queue.Pop(),queue.Pop(),queue.Pop()};
        check(order[0].Process==23&&order[1].Entity==1&&order[1].Ticket==10&&order[2].Entity==2,"Rescheduling preserves the original ticket and canonical process/entity ties at coincident arrival times.");
        check(!queue.TryMoveEarlier(27,1,0),"A completed actor has no stale indexed event.");
        var events=Enumerable.Range(0,2048).Select(k=>new SimulationEvent{Process=27,Entity=k,Due=10000+k,Ticket=k}).ToArray();queue=new SimulationEventQueue(events);
        for(int k=2047;k>=0;k-=3)check(queue.TryMoveEarlier(27,k,k%17),"An indexed heap event can move through several parents.");
        var expected=events.Select(e=>{var c=e.Copy();if(c.Entity%3==2047%3)c.Due=c.Entity%17;return c;}).OrderBy(e=>e,Comparer<SimulationEvent>.Create(SimulationEventQueue.Compare)).ToArray();
        check(Enumerable.Range(0,2048).All(k=>{var e=queue.Pop();return e.Entity==expected[k].Entity&&e.Due==expected[k].Due&&e.Ticket==expected[k].Ticket;}),"Indexed earlier arrivals retain complete heap order across thousands of unrelated actors.");
        var map=new HistoricalGeographicMap(12);map.Record(2,2,20,100,-1,3);map.Sharing=new HistoricalGeographicSharing{ReportCursor=3,ContactCursor=2,RecentRegion=2};var copy=map.Copy();copy.Sharing.ReportCursor++;
        check(map.Sharing.ReportCursor==3&&copy.Sharing.ReportCursor==4,"Saved geographical cursors detach between public checkpoints.");
        var legacy=new HistoricalGeographicMap(12);
        using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write(typeof(HistoricalGeographicMap).FullName);
            var write=typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic)!;
            foreach(var field in typeof(HistoricalGeographicMap).GetFields().Where(f=>f.Name!="Sharing").OrderBy(f=>f.Name,StringComparer.Ordinal))
            {writer.Write(field.Name);write.Invoke(null,new object[]{writer,field.GetValue(legacy)});}
        }
        check(StateHash.Compute(legacy)==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())).ToLowerInvariant(),"Absent sharing records retain exact predecessor geographical-map canonical bytes.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var source=w.Prehistory;var e=new Prehistory(w);e.Validate(source);string original=source.ComputeHash();
        var s=e.UpgradeGeographicKnowledge(e.UpgradeRealmLifecycles(source)).Copy();var a=s.History.Aggregates;var h=s.History;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        var town=s.Society.Settlements.First(t=>a.Resolve(t.PolityId,h)>=0&&a.Cities[t.Id].Supported&&a.Living.Cities[t.Id].AbandonedYear<0&&t.Population>=500&&(bool)Call("DiffusionShore",t.Cell));
        int owner=a.Resolve(town.PolityId,h);var hc=h.Cultures[h.Polities[owner].CultureId];
        // Controlled current-world contrast: place this court at its existing
        // actual populated port, removing unrelated old imperial remoteness.
        h.Polities[owner].CapitalId=town.Id;Call("AssignTerritorialSource",s,town.Cell,town.Id);
        void Deploy(HistoricalTechnology technology,int amount){hc.Technology[(int)technology]=1;hc.Technical.Deployment[(int)technology]=amount;hc.Technical.Levels[(int)technology]=Math.Max(1,hc.Technical.Levels[(int)technology]);}
        Deploy(HistoricalTechnology.Sailing,1000);Deploy(HistoricalTechnology.Navigation,1000);Deploy(HistoricalTechnology.Writing,0);Deploy(HistoricalTechnology.OceanNavigation,0);Deploy(HistoricalTechnology.SteamPower,0);Deploy(HistoricalTechnology.CombustionEngines,0);hc.Institution=900;town.FoodReserve=100000000;
        var route=new Prehistory.Route{CellA=town.Cell,CellB=town.Cell,A=e.CellPatch[town.Cell],B=e.CellPatch[town.Cell],Sea=true,DistanceM=100000};
        check(!(bool)Call("MaritimeCharterLogistics",s,owner,route,town.Id,-1,0L),"The actual current-state logistics path blocks an unwritten short sea charter; the old800km exemption cannot apply.");
        Deploy(HistoricalTechnology.Writing,1000);
        object[] args={s,owner,route,town.Id,-1,0L};
        check((bool)Call("MaritimeCharterLogistics",args)&&(int)args[4]==town.Id,"A real supported literate regional port can still fit out its bounded expedition.");
        route.DistanceM=5000000;
        check(!(bool)Call("MaritimeCharterLogistics",s,owner,route,town.Id,-1,0L),"An actual rich coastal state cannot use a local charter entry to send an early5000km convoy.");
        Deploy(HistoricalTechnology.OceanNavigation,1000);
        check((bool)Call("MaritimeCharterLogistics",s,owner,route,town.Id,-1,0L),"The same actual populated food depot supports a5000km venture when its ocean fleet is deployed.");
        var map=a.Empires.Connections.Geography.Governments[owner];long now=SimulationTime.Seconds(s);
        map.Missions=new[]{new HistoricalSeaMission{Kind=0,Due=now+SimulationTime.Year,Source=town.Cell,Target=town.Cell,Region=e.CellPatch[town.Cell]}};map.ScheduledDue=now+SimulationTime.Year;
        var queue=new SimulationEventQueue(new[]{new SimulationEvent{Process=27,Entity=owner,Due=map.ScheduledDue,Ticket=765}});
        typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(e,queue);
        map.Missions=map.Missions.Concat(new[]{new HistoricalSeaMission{Kind=2,Due=now+SimulationTime.Day,Source=town.Cell,Target=town.Cell,Region=e.CellPatch[town.Cell]}}).ToArray();Call("ScheduleSeaMissions",s,owner);
        check(map.ScheduledDue==now+SimulationTime.Day&&queue.Count==1&&queue.Pop().Ticket==765,"An actual incoming chart wakes tomorrow despite a fleet already due next year, retaining one dated actor event.");
        var peerTown=s.Society.Settlements.FirstOrDefault(t=>a.Resolve(t.PolityId,h)>=0&&a.Resolve(t.PolityId,h)!=owner&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Supported
            &&(bool)Call("InstitutionalRouteAllowed",s,town.Cell,t.Cell,h.Polities[owner].CultureId));
        if(peerTown!=null)
        {
            int peer=a.Resolve(peerTown.PolityId,h);h.Polities[peer].CapitalId=peerTown.Id;h.Polities[peer].CultureId=h.Polities[owner].CultureId;
            s.Society.Cultures[h.Polities[owner].CultureId].Contacts=Array.Empty<int>();a.Cities[town.Id].Neighbors=new[]{peerTown.Id};
            var relation=(HistoricalRelation)Call("AggregateRelation",s,owner,peer);relation.Trade=true;relation.Trust=100;relation.WarSince=-1;
            var sender=new HistoricalGeographicMap(e.Patches.Length);for(int k=0;k<Math.Min(16,e.Patches.Length);k++)sender.Record(k,2,s.Years,500,-1,e.Patches[k].Cell);
            sender.Sharing=new HistoricalGeographicSharing{RecentRegion=0};a.Empires.Connections.Geography.Governments[owner]=sender;
            var receiver=new HistoricalGeographicMap(e.Patches.Length);a.Empires.Connections.Geography.Governments[peer]=receiver;
            int first=0;for(int k=0;k<16;k++)
            {
                sender.Sharing.CityCursor=a.Polities[owner].Cities.Positions[town.Id];
                Call("ShareRevisedGeographicReports",s,owner);
                if(k==0){first=receiver.Missions.Length;check(receiver.Missions.Any(m=>m.Region==0&&m.Due>now&&m.Due<now+SimulationTime.Year),"A just-improved old map entry reaches a physically connected same-culture trading court promptly, even without foreign-culture contacts.");}
                foreach(var report in receiver.Missions)receiver.Record(report.Region,report.Grade,report.ReportedYear,report.Opportunity,report.Host,report.Target);
                receiver.Missions=Array.Empty<HistoricalSeaMission>();receiver.ScheduledDue=0;while(queue.Count>0)queue.Pop();
            }
            check(first<=2&&receiver.Places.Count==sender.Places.Count,"Bounded fair sharing eventually delivers the entire older archive rather than repeatedly sampling only familiar places.");
        }
        check(source.ComputeHash()==original,"Controlled maritime branches leave the source checkpoint unchanged.");
    }
}

using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryServiceRecoveryTests
{
    public static void Run(Action<bool,string> check)
    {
        HistoryServiceCursorBufferTests.Run(check);
        foreach(int source in new[]{0,1,1000000,-2,-3,-1000002})check(HistoricalServiceRecoveryEffects.Source(HistoricalServiceRecoveryEffects.Key(source))==source,"City and retired post source keys round-trip without collision.");
        check(HistoricalServiceRecoveryEffects.Key(-1)==-1,"Absent service has no queued source identity.");
        check(HistoricalServiceRecoveryEffects.Credit(0,0,1000000)==0,"Repeated same-year events cannot accelerate physical retreat.");
        check(HistoricalServiceRecoveryEffects.Credit(0,1,1000000)==50000&&HistoricalServiceRecoveryEffects.Credit(0,1,1000)==5000,"Large and small districts receive finite physical-area review budgets rather than a mesh-cell quota.");
        check(HistoricalServiceRecoveryEffects.Credit(100,3,1000000)==HistoricalServiceRecoveryEffects.Credit(HistoricalServiceRecoveryEffects.Credit(100,1,1000000),2,1000000),"Unchanged district area accrues the same capacity over partitioned elapsed time.");
        check(HistoricalServiceRecoveryEffects.Credit(int.MaxValue,100000,int.MaxValue)==int.MaxValue,"Long silent districts cannot overflow their saved physical-area account.");
        var r=new HistoricalTerritorialServiceRecovery{CityEdges=new[]{new HistoricalLocalBag()}};r.PendingSources.Add(7);r.CityEdges[0].Add(12);var copy=r.Copy();copy.PendingSources.Remove(7);copy.CityEdges[0].Remove(12);copy.CreditKm2=copy.CreditKm2.With(7,42);
        check(r.PendingSources.Count==1&&r.CityEdges[0].Count==1&&r.CreditKm2[7]==-1,"Recovery queues and city boundary indices detach across public checkpoint copies.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent service recovery preserves released optional-extension canonical bytes.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(w);var normal=e.UpgradeRealmLifecycles(original);e.Validate(normal);
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,values)!;
        var s=normal.Copy();var a=s.History.Aggregates;var t=a.Empires.Territories;var r=a.Empires.Connections.ServiceRecovery;
        check(r.PendingSources.Count>0,"Explicit upgrade discovers and preserves stranded retired service districts in the actual saved world.");
        int source=-1;
        for(int k=0;k<r.PendingSources.Count&&source== -1;k++)
        {
            int candidate=HistoricalServiceRecoveryEffects.Source(r.PendingSources.At(k));var edges=(HistoricalLocalBag)Call("ServiceRecoveryEdges",s,candidate);
            for(int j=0;j<edges.Count;j++)if(HistoricalCells.Polity(s,edges.At(j))>=0&&!(bool)Call("ServiceSourceStructurallyLive",s,HistoricalCells.Polity(s,edges.At(j)),candidate)){source=candidate;break;}
        }
        if(source== -1)throw new Exception("Fixture has no still-controlled stranded service district.");
        int key=HistoricalServiceRecoveryEffects.Key(source);r.PendingSources=new HistoricalLocalBag();r.PendingSources.Add(key);r.PendingCursor=0;r.RetiredGovernments=new HistoricalLocalBag();
        long area=source>=0?t.DistrictAreas[source]:t.Posts[-source-2].AreaKm2,population=a.TotalPopulation;string titles=StateHash.Compute(a.Empires.Connections.Titles),cultures=StateHash.Compute(s.Society.Cultures);long changed=r.ReassignedCells+r.WithdrawnCells;
        r.CreditKm2=r.CreditKm2.With(key,0);r.LastReviewYear=r.LastReviewYear.With(key,s.Years);Call("AdvanceTerritorialServiceRecovery",s,false);
        check(changed==r.ReassignedCells+r.WithdrawnCells,"A queued district does not vanish merely because several courts happen to update in the same year.");
        r.CreditKm2=r.CreditKm2.With(key,int.MaxValue);Call("AdvanceTerritorialServiceRecovery",s,false);
        long after=source>=0?t.DistrictAreas[source]:t.Posts[-source-2].AreaKm2;
        check(after<area&&r.ReassignedCells+r.WithdrawnCells>changed,"A genuinely dead service district relinquishes or reassigns bounded boundary cells instead of retaining permanent control.");
        check(r.ReassignedCells+r.WithdrawnCells-changed<=8,"One source visit performs at most eight local cell repairs.");
        check(a.TotalPopulation==population&&StateHash.Compute(a.Empires.Connections.Titles)==titles&&StateHash.Compute(s.Society.Cultures)==cultures,"Administrative withdrawal conserves the population and does not annul recognized titles or change local societies.");
        Call("ValidateTerritorialServiceRecovery",s);Call("ValidateTerritorialAdministration",s);
        var court=s.History.Polities.First(p=>p.EndedYear<0&&a.Living.Cities[p.CapitalId].AbandonedYear<0);a.Cities[court.CapitalId].Supported=false;
        check((bool)Call("ServiceSourceStructurallyLive",s,court.Id,court.CapitalId),"A temporary underprovisioned but occupied city remains a legitimate local service source.");
        var fair=normal.Copy();var fr=fair.History.Aggregates.Empires.Connections.ServiceRecovery;fr.PendingSources=new HistoricalLocalBag();fr.PendingCursor=0;
        var ids=fair.History.Polities.Where(p=>p.EndedYear<0&&fair.History.Aggregates.Living.Cities[p.CapitalId].AbandonedYear<0).Select(p=>p.CapitalId).Distinct().Take(16).ToArray();if(ids.Length<9)throw new Exception("Fixture needs nine current courts for queue fairness.");
        foreach(int city in ids){Call("QueueTerritorialServiceRecovery",fair,city);int k=HistoricalServiceRecoveryEffects.Key(city);fr.LastReviewYear=fr.LastReviewYear.With(k,fair.Years-1);}
        for(int i=0;i<ids.Length+2;i++)Call("AdvanceTerritorialServiceRecovery",fair,false);
        check(ids.All(city=>fr.LastReviewYear[HistoricalServiceRecoveryEffects.Key(city)]==fair.Years),"Fair source scheduling reaches every queued court beyond the fixed first-eight review window.");
        var orphan=normal.Copy();var or=orphan.History.Aggregates.Empires.Connections.ServiceRecovery;or.PendingSources=new HistoricalLocalBag();or.PendingSources.Add(key);or.PendingCursor=0;or.CreditKm2=or.CreditKm2.With(key,int.MaxValue);
        var oe=(HistoricalLocalBag)Call("ServiceRecoveryEdges",orphan,source);int cell=oe.At(0);int oldOwner=HistoricalCells.Polity(orphan,cell);if(oldOwner>=0){orphan.History.Polities[oldOwner].EndedYear=orphan.Years;orphan.History.Aggregates.Successors=orphan.History.Aggregates.Successors.With(oldOwner,-1);}long cleared=or.WithdrawnCells;Call("AdvanceTerritorialServiceRecovery",orphan,true);
        check(or.WithdrawnCells>cleared,"Background regional work can clear orphaned service metadata even when the responsible court no longer has state events.");
        var collapsed=normal.Copy();var ca=collapsed.History.Aggregates;var cr=ca.Empires.Connections.ServiceRecovery;cr.RetiredGovernments=new HistoricalLocalBag();cr.GovernmentCursor=new HistoricalCellTree(33554432);cr.PendingSources=new HistoricalLocalBag();cr.PendingCursor=0;
        var dead=collapsed.History.Polities.First(p=>p.EndedYear<0&&ca.Empires.Governments[p.Id].AdministrativePosts.Count>0);int office=ca.Empires.Governments[dead.Id].AdministrativePosts.At(0);
        foreach(var p in collapsed.History.Polities){p.EndedYear=collapsed.Years;ca.Successors=ca.Successors.With(p.Id,-1);}
        Call("QueueRetiredServiceGovernment",collapsed,dead.Id);Call("AdvanceTerritorialServiceRecovery",collapsed,true);
        check(ca.Empires.Territories.Posts[office].ClosedYear==collapsed.Years&&cr.PendingSources.Count>0,"With every state actor gone, the regional fallback retires a real abandoned office and retains its service district in the global work queue.");
        int officeKey=HistoricalServiceRecoveryEffects.Key(-2-office);cr.PendingSources=new HistoricalLocalBag();cr.PendingSources.Add(officeKey);cr.PendingCursor=0;cr.Cursor=cr.Cursor.With(officeKey,0);cr.Reviewed=cr.Reviewed.With(officeKey,0);cr.CreditKm2=cr.CreditKm2.With(officeKey,int.MaxValue);cr.LastReviewYear=cr.LastReviewYear.With(officeKey,collapsed.Years);cleared=cr.WithdrawnCells;
        Call("AdvanceTerritorialServiceRecovery",collapsed,true);check(cr.WithdrawnCells>cleared,"The same no-courts regional event actually clears closed-office physical service, not just its administrative label.");
        // Boundaries within one source must track changing political ownership,
        // not merely different service codes at the outside of the district.
        var split=normal.Copy();var sa=split.History.Aggregates;int splitCell=-1,splitNeighbor=-1;
        for(int i=0;i<w.Surface.Cells.Length&&splitCell<0;i++)if(sa.Empires.Territories.Sources[i]>=0)foreach(int n in w.Surface.Cells[i].Neighbors)if(sa.Empires.Territories.Sources[n]==sa.Empires.Territories.Sources[i]&&HistoricalCells.Polity(split,i)>=0){splitCell=i;splitNeighbor=n;break;}
        if(splitCell<0)throw new Exception("Fixture needs a multi-cell city district.");
        int splitSource=sa.Empires.Territories.Sources[splitCell],current=HistoricalCells.Polity(split,splitCell),other=split.History.Polities.First(p=>p.EndedYear<0&&p.Id!=current).Id;
        Call("SetAggregateLayer",split,splitNeighbor,1,other);
        check(sa.Empires.Territories.Sources[splitNeighbor]==splitSource&&sa.Empires.Connections.ServiceRecovery.CityEdges[splitSource].Positions[splitCell]>=0,"Changing a political interior within one service source creates the required local repair boundary without scanning district membership.");
        check(original.ComputeHash()==hash,"Controlled recovery experiments leave the loaded old-rules checkpoint unchanged.");
        w.Prehistory=normal;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;e.Validate(restored);check(restored.Hash==normal.Hash,"New source queues, fair cursors and boundary trees survive a cold save/load.");
        var whole=e.AdvanceYears(normal,12);var pieces=e.AdvanceYears(e.AdvanceYears(restored,5),7);e.Validate(whole);e.Validate(pieces);
        check(whole.Hash==pieces.Hash,"Recovery and territory ledgers replay identically from cold restore across twelve versus five-plus-seven years.");
        Console.WriteLine("Recovery experiment cells repaired="+(r.ReassignedCells+r.WithdrawnCells-changed)+" old-source km2="+area+" -> "+after+"; normal12 pending="+whole.History.Aggregates.Empires.Connections.ServiceRecovery.PendingSources.Count);
    }
    public static void Checkpoints(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;var e=new Prehistory(w);e.Validate(original);string originalHash=original.ComputeHash();
        object Call(PrehistoryState state)=>typeof(Prehistory).GetMethod("ValidateTerritorialServiceRecovery",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,new object[]{state})!;
        bool Rejected(PrehistoryState state){try{Call(state);return false;}catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException){return true;}}
        var trailing=original.Copy();var a=trailing.History.Aggregates;var recovery=a.Empires.Connections.ServiceRecovery;
        // Isolate the service validator's actual town-append invariant: a new
        // independent town adds a city but no territorial source or edge bag.
        a.Cities=a.Cities.Concat(new[]{new HistoricalCityAggregate{Id=a.Cities.Length}}).ToArray();
        string before=trailing.ComputeHash();Call(trailing);
        check(recovery.CityEdges.Length<a.Cities.Length,"A checkpoint accepts lazy empty service bags after an independent town append.");
        check(trailing.ComputeHash()==before,"Validating a lazy checkpoint does not allocate bags or change its canonical bytes.");
        var copy=trailing.Copy();Call(copy);check(copy.ComputeHash()==before,"Copy preserves the same valid lazy checkpoint representation and bytes.");
        var legacy=trailing.Copy();var legacyRecovery=legacy.History.Aggregates.Empires.Connections.ServiceRecovery;legacyRecovery.Version=1;legacyRecovery.RetiredGovernments=null;legacyRecovery.GovernmentCursor=null;string legacyHash=legacy.ComputeHash();Call(legacy);
        check(legacy.ComputeHash()==legacyHash,"The same lazy-index validation preserves revision1 recovery bytes and absent optional retired-government fields.");
        var oversized=original.Copy();Array.Resize(ref oversized.History.Aggregates.Empires.Connections.ServiceRecovery.CityEdges,oversized.History.Aggregates.Cities.Length+1);
        check(Rejected(oversized),"A service bag for a nonexistent city remains invalid.");
        var assigned=original.Copy();var aa=assigned.History.Aggregates;int source=Enumerable.Range(0,w.Surface.Cells.Length).Select(c=>aa.Empires.Territories.Sources[c]).Where(c=>c>=0).DefaultIfEmpty(-1).Max();
        if(source<0)throw new Exception("Checkpoint fixture requires an actual city service source.");
        Array.Resize(ref aa.Empires.Connections.ServiceRecovery.CityEdges,source);
        check(Rejected(assigned),"A missing bag for any real assigned service source remains invalid, not silently treated as an empty district.");
        var broken=original.Copy();broken.History.Aggregates.Empires.Connections.ServiceRecovery.CityEdges=null;
        check(Rejected(broken),"A null boundary index remains invalid.");
        check(original.ComputeHash()==originalHash,"Checkpoint regressions leave the loaded real world unchanged.");
        var restoredWorld=WorldFile.Read(WorldFile.Write(w));var restored=restoredWorld.Prehistory;e.Validate(restored);
        check(restored.ComputeHash()==originalHash,"A cold compact save retains the exact existing service-ledger checkpoint bytes.");
        var whole=e.AdvanceYears(original,12);var cold=new Prehistory(restoredWorld);var parts=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);e.Validate(whole);cold.Validate(parts);
        check(whole.Hash==parts.Hash,"Twelve years replay identically to cold five-plus-seven with validation-only lazy service indices.");
        check(original.ComputeHash()==originalHash,"Saved continuation checks leave their source checkpoint unchanged.");
    }
}

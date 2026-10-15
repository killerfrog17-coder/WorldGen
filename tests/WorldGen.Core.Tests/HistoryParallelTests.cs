using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryParallelTests
{
    public static void Run(string file,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(file));var source=world.Prehistory;string hash=source.Hash;
        var serial=new Prehistory(world){MaximumWorkers=1};var parallel=new Prehistory(world){MaximumWorkers=4};
        var owners=serial.AssessAdministrativeOwners(source);var messages=serial.AssessAdministrativeCommunication(source);var accounts=serial.AssessAdministrativeAccounts(source);
        check(parallel.AssessAdministrativeOwners(source).SequenceEqual(owners),"Parallel actor reductions preserve every service owner and ordered neutral-frontier tie");
        check(parallel.AssessAdministrativeCommunication(source).SequenceEqual(messages),"Parallel capital communication matches serial searches");
        check(StateHash.Compute(parallel.AssessAdministrativeAccounts(source))==StateHash.Compute(accounts),"Parallel route searches preserve live annual financial/demographic accounts");
        void WarmAndFresh(PrehistoryState state,string description)
        {
            var fresh=new Prehistory(world){MaximumWorkers=1};
            check(parallel.AssessAdministrativeOwners(state).SequenceEqual(fresh.AssessAdministrativeOwners(state)),description+": service graph");
            check(StateHash.Compute(parallel.AssessAdministrativeAccounts(state))==StateHash.Compute(fresh.AssessAdministrativeAccounts(state)),description+": uncached accounts");
        }
        var office=source.History.Administration.Offices.First(o=>o.EndedYear<0&&source.History.Polities[o.PolityId].EndedYear<0);
        var unfunded=source.Copy();unfunded.History.Polities[office.PolityId].Treasury=0;unfunded.Seal();WarmAndFresh(unfunded,"Withdrawing office funding invalidates only actual dependencies");
        var townCells=new HashSet<int>(source.Society.Settlements.Select(t=>t.Cell));
        int barrier=Enumerable.Range(0,owners.Length).First(i=>owners[i]>=0&&!townCells.Contains(i)&&!world.Hydrology.OceanConnected[i]);
        int rival=source.History.Polities.First(p=>p.EndedYear<0&&p.Id!=owners[barrier]).Id;
        var obstructed=source.Copy();obstructed.History.CellPolity[barrier]=rival;obstructed.Seal();WarmAndFresh(obstructed,"Changing a real control barrier invalidates affected cached searches");
        WarmAndFresh(source,"Returning to the original checkpoint after intervening branches");
        if(source.History.Modern?.Shipping.Length>0)
        {
            if(world.Surface.Cells.Length>=80000)
            {
                var livePorts=new HashSet<int>(source.History.Modern.Shipping.SelectMany(r=>new[]{source.Society.Settlements[r.PortA].Cell,source.Society.Settlements[r.PortB].Cell}));
                var nearby=(from r in source.History.Modern.Shipping let a=source.Society.Settlements[r.PortA] let b=source.Society.Settlements[r.PortB] where a.Population>=100&&b.Population>=100&&HistoricalTechnologies.Has(source.History.Cultures[a.CultureId],HistoricalTechnology.OceanNavigation) from cell in world.Surface.Cells[a.Cell].Neighbors where !livePorts.Contains(cell)&&D.Angle(world.Surface.Cells[cell].Position,world.Surface.Cells[a.Cell].Position)*world.Recipe.RadiusKm<=150 select (Cell:cell,Target:b.Cell,Culture:a.CultureId)).First();
                var contact=typeof(Prehistory).GetMethod("ModernShippingContact",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                check((bool)contact.Invoke(parallel,new object[]{source,nearby.Cell,nearby.Target,nearby.Culture})!,"A non-port cell near an actual port retains150km sea access; the broad-phase radius cannot collapse through integer division");
            }
            var shipping=source.Copy();var link=shipping.History.Modern.Shipping.First(r=>r.DistanceM<9199000);link.DistanceM+=1000;shipping.Seal();
            WarmAndFresh(shipping,"Changing a real shipping-edge distance invalidates dependent searches");
            var removed=source.Copy();removed.History.Modern.Shipping=removed.History.Modern.Shipping.Skip(1).Select((r,i)=>{var copy=r.Copy();copy.Id=i;return copy;}).ToArray();removed.Seal();
            WarmAndFresh(removed,"Removing an ocean edge invalidates both endpoint dependencies");
            WarmAndFresh(source,"Restoring shipping after distance and removal branches");
        }
        using(var cancellation=new CancellationTokenSource())
        {
            var interrupted=new Prehistory(world){MaximumWorkers=4};bool observed=false,stopped=false;
            interrupted.WorkObserver=m=>{if(m.Stage=="route cache misses"){observed=true;cancellation.Cancel();}};
            try{interrupted.AssessAdministrativeAccounts(source,cancellation.Token);}catch(OperationCanceledException){stopped=true;}finally{interrupted.WorkObserver=null;}
            check(observed&&stopped,"Cancellation after completed parallel searches cannot publish partly merged accounts");
            check(StateHash.Compute(interrupted.AssessAdministrativeAccounts(source))==StateHash.Compute(accounts),"Canceled parallel work cannot poison a later complete query");
        }
        check(serial.AdvanceYears(source,3).Hash==parallel.AdvanceYears(source,3).Hash,"Three years of actual history are independent of worker count, cache warmth and prior branches");
        check(source.ComputeHash()==hash,"Parallel diagnostics do not mutate their authoritative source");
        Console.WriteLine("Parallel execution checked on "+world.Surface.Cells.Length+" cells, "+Environment.ProcessorCount+" available processors.");
    }
}

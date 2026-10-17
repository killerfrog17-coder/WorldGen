using System.Reflection;
using System.Text.Json;
using WorldGen.Core;

// Explicit offline replay diagnostics only, never called by simulation turns.
public static class HistoryPreparedPlotAudit
{
    public static void Check(Prehistory engine,PrehistoryState state,string directory)
    {
        if(state.History.Aggregates.Empires.Connections.PreparedPlots==null)return;
        try{typeof(Prehistory).GetMethod("ValidatePreparedPlots",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{state});}
        catch(TargetInvocationException error)when(error.InnerException!=null)
        {
            var cause=error.InnerException;int region=cause.Data.Contains("PreparedRegion")?(int)cause.Data["PreparedRegion"]!:-1;
            var plots=state.History.Aggregates.Empires.Connections.PreparedPlots;var a=state.History.Aggregates;var rows=new List<object>();
            for(int cell=0;cell<engine.CellPatch.Length;cell++)if(engine.CellPatch[cell]==region&&plots.Cells[cell*6]>=0)
            {
                int key=cell*6,holder=plots.Cells[key+4],town=a.TownAt[cell],port=town<0?-1:a.Empires.Connections.ProvisionedPorts.ByTown[town];
                rows.Add(new{Cell=cell,Raw=plots.Cells[key],Effective=engine.PreparedFarmersAt(state,cell),PreparedYear=plots.Cells[key+1],ReviewYear=plots.Cells[key+2],VacantYear=plots.Cells[key+3],HolderTown=holder,PlotEpoch=plots.Cells[key+5],CurrentTown=town,CurrentPort=port,HolderAbandoned=holder<0?-1:a.Living.Cities[holder].AbandonedYear,CurrentTownAbandoned=town<0?-1:a.Living.Cities[town].AbandonedYear});
            }
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"prepared-failure.json"),JsonSerializer.Serialize(new{state.Years,state.Hash,Realm=state.History.Aggregates.Empires.Connections.Realms.Version,LedgerVersion=plots.Version,Region=region,Population=region<0?-1:state.Population[region],Recorded=region<0?-1:engine.PreparedRegionalFarmersAt(state,region),Epoch=region<0?-1:plots.RegionalEpoch[region],Error=cause.Message,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
            throw new InvalidOperationException("Prepared-field replay audit failed; see "+Path.Combine(directory,"prepared-failure.json"),cause);
        }
    }
}

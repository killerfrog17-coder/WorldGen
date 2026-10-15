using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    [Serializable] public sealed class TechnicalProgress
    {
        public int Id,Level,ResearchPermille,DeploymentPermille,AllocationBasisPoints;
        public string Name="",Prerequisites="",Source="";
    }
    public static class TechnicalResearchView
    {
        public static TechnicalProgress[] Read(PrehistoryState state,int culture)
        {
            var c=state.History.Cultures[culture];var t=c.Technical;if(t==null)return Array.Empty<TechnicalProgress>();
            return Enumerable.Range(0,HistoricalTechnicalGraph.Count).Where(k=>t.Levels[k]>0||t.Allocation[k]>0||t.Offers[k]>0).Select(k=>new TechnicalProgress{
                Id=k,Name=HistoricalTechnologies.Names[k],Level=t.Levels[k],DeploymentPermille=t.Deployment[k],AllocationBasisPoints=t.Allocation[k],
                ResearchPermille=(int)Math.Min(1000,1000*(t.Offers[k]>t.Levels[k]?t.Learning[k]*8:t.Progress[k])/(double)HistoricalTechnicalGraph.Cost(k,t.Levels[k]+1)),
                Prerequisites=string.Join("; ",HistoricalTechnicalGraph.Requirements(k).Select(r=>HistoricalTechnologies.Names[r.Technology]+" level "+r.Level+" (currently "+t.Levels[r.Technology]+")")),
                Source=t.Offers[k]>t.Levels[k]&&t.Sources[k]>=0?"Learning from "+state.Society.Cultures[t.Sources[k]].Name:t.Allocation[k]>0?"Local investigation":"Established knowledge"
            }).OrderByDescending(p=>p.AllocationBasisPoints).ThenBy(p=>p.Id).ToArray();
        }
    }
}

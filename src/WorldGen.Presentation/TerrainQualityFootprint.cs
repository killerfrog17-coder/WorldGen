using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Loaded area only qualifies when its sample spacing meets the
    /// requested surface detail. A horizon backdrop is not finished near ground.</summary>
    public sealed class TerrainQualityFootprint
    {
        readonly Dictionary<TerrainTileKey,int> source=new Dictionary<TerrainTileKey,int>();
        readonly Dictionary<double,TerrainCoverageFootprint> tiers=new Dictionary<double,TerrainCoverageFootprint>();
        readonly HashSet<double> validTiers=new HashSet<double>();
        readonly List<TerrainTileKey> tierKeys=new List<TerrainTileKey>();
        readonly Dictionary<double,List<TerrainTileKey>> requestedGroups=new Dictionary<double,List<TerrainTileKey>>();
        readonly List<double> requestedOrder=new List<double>();
        public TerrainQualityFootprint(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {Reset(visible);}
        public void Reset(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {
            source.Clear();foreach(var pair in visible)source.Add(pair.Key,pair.Value);
            // Every resident/quality change invalidates every spacing tier.
            // Rebuild lazily on the next query, reusing only its storage.
            validTiers.Clear();
        }
        public bool Matches(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {
            int count=0;foreach(var p in visible){count++;if(!source.TryGetValue(p.Key,out int segments)||segments!=p.Value)return false;}return count==source.Count;
        }
        public double NearestGap(IReadOnlyDictionary<TerrainTileKey,int> requested,double x,double z,double maximumDistance)
        {
            double nearest=maximumDistance;
            foreach(double spacing in requestedOrder)requestedGroups[spacing].Clear();requestedOrder.Clear();
            foreach(var pair in requested)
            {
                double spacing=pair.Key.SizeM/pair.Value;
                if(!requestedGroups.TryGetValue(spacing,out var group))requestedGroups.Add(spacing,group=new List<TerrainTileKey>());
                if(group.Count==0)requestedOrder.Add(spacing);group.Add(pair.Key);
            }
            foreach(double spacing in requestedOrder)
            {
                if(!validTiers.Contains(spacing))
                {
                    tierKeys.Clear();foreach(var pair in source)if(pair.Value>0&&pair.Key.SizeM/pair.Value<=spacing*(1+1e-9))tierKeys.Add(pair.Key);
                    if(tiers.TryGetValue(spacing,out var previous))previous.Reset(tierKeys);
                    else tiers.Add(spacing,new TerrainCoverageFootprint(tierKeys));
                    validTiers.Add(spacing);
                }
                var footprint=tiers[spacing];
                nearest=Math.Min(nearest,footprint.NearestGap(requestedGroups[spacing],x,z,nearest));
            }
            return nearest;
        }
    }
}

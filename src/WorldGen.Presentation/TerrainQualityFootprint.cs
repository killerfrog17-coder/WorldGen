using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Presentation
{
    /// <summary>Loaded area only qualifies when its sample spacing meets the
    /// requested surface detail. A horizon backdrop is not finished near ground.</summary>
    public sealed class TerrainQualityFootprint
    {
        readonly Dictionary<TerrainTileKey,int> source;
        readonly Dictionary<double,TerrainCoverageFootprint> tiers=new Dictionary<double,TerrainCoverageFootprint>();
        readonly Dictionary<double,List<TerrainTileKey>> requestedGroups=new Dictionary<double,List<TerrainTileKey>>();
        readonly List<double> requestedOrder=new List<double>();
        public TerrainQualityFootprint(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {source=visible.ToDictionary(p=>p.Key,p=>p.Value);}
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
                if(!tiers.TryGetValue(spacing,out var footprint))
                {footprint=BuildTier(spacing);tiers.Add(spacing,footprint);}
                nearest=Math.Min(nearest,footprint.NearestGap(requestedGroups[spacing],x,z,nearest));
            }
            return nearest;
        }
        TerrainCoverageFootprint BuildTier(double spacing)
        {
            var keys=new List<TerrainTileKey>();
            foreach(var pair in source)if(pair.Value>0&&pair.Key.SizeM/pair.Value<=spacing*(1+1e-9))keys.Add(pair.Key);
            return new TerrainCoverageFootprint(keys);
        }
    }
}

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
        public TerrainQualityFootprint(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {source=visible.ToDictionary(p=>p.Key,p=>p.Value);}
        public bool Matches(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {
            int count=0;foreach(var p in visible){count++;if(!source.TryGetValue(p.Key,out int segments)||segments!=p.Value)return false;}return count==source.Count;
        }
        public double NearestGap(IReadOnlyDictionary<TerrainTileKey,int> requested,double x,double z,double maximumDistance)
        {
            double nearest=maximumDistance;
            foreach(var group in requested.GroupBy(p=>p.Key.SizeM/p.Value))
            {
                double spacing=group.Key;
                if(!tiers.TryGetValue(spacing,out var footprint))
                {footprint=new TerrainCoverageFootprint(source.Where(p=>p.Value>0&&p.Key.SizeM/p.Value<=spacing*(1+1e-9)).Select(p=>p.Key));tiers.Add(spacing,footprint);}
                nearest=Math.Min(nearest,footprint.NearestGap(group.Select(p=>p.Key),x,z,nearest));
            }
            return nearest;
        }
    }
}

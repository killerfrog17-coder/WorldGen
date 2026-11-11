using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Exact loaded coverage at each requested sampling spacing. A
    /// resident edit changes only its bounded quadtree ancestor chain.</summary>
    public sealed class TerrainQualityFootprint
    {
        const int MaximumLevel=23;
        sealed class Node
        {
            public double Own=double.PositiveInfinity,Best=double.PositiveInfinity;
            public bool Children;
        }
        readonly Dictionary<TerrainTileKey,int> source=new Dictionary<TerrainTileKey,int>();
        readonly Dictionary<TerrainTileKey,int> incoming=new Dictionary<TerrainTileKey,int>();
        readonly Dictionary<TerrainTileKey,Node> nodes=new Dictionary<TerrainTileKey,Node>();
        readonly Dictionary<double,List<TerrainTileKey>> requestedGroups=new Dictionary<double,List<TerrainTileKey>>();
        readonly List<double> requestedOrder=new List<double>();
        int highest;
        public TerrainQualityFootprint(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible){Reset(visible);}
        static TerrainTileKey Parent(TerrainTileKey key)=>new TerrainTileKey(key.X>>1,key.Z>>1,key.Level+1,key.Scale);
        static TerrainTileKey Child(TerrainTileKey key,int x,int z)=>new TerrainTileKey(key.X*2+x,key.Z*2+z,key.Level-1,key.Scale);
        void SetOwn(TerrainTileKey key,double spacing)
        {
            var at=key;
            while(true)
            {
                bool existed=nodes.TryGetValue(at,out var node);node=node??new Node();
                double before=node.Best;
                if(at.Equals(key))node.Own=spacing;
                double children=0;bool any=false;
                if(at.Level==0)children=double.PositiveInfinity;
                else for(int z=0;z<2;z++)for(int x=0;x<2;x++)
                {
                    if(nodes.TryGetValue(Child(at,x,z),out var child)){any=true;children=Math.Max(children,child.Best);}
                    else children=double.PositiveInfinity;
                }
                node.Children=any;node.Best=Math.Min(node.Own,children);
                bool present=!double.IsPositiveInfinity(node.Own)||any;
                if(present){nodes[at]=node;if(!double.IsPositiveInfinity(node.Best))highest=Math.Max(highest,at.Level);}
                else nodes.Remove(at);
                if(at.Level>=MaximumLevel||existed==present&&before==node.Best)break;
                at=Parent(at);
            }
        }
        public void Reset(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {
            incoming.Clear();foreach(var pair in visible)incoming.Add(pair.Key,pair.Value);
            foreach(var pair in source)if(!incoming.ContainsKey(pair.Key))SetOwn(pair.Key,double.PositiveInfinity);
            foreach(var pair in incoming)if(!source.TryGetValue(pair.Key,out int old)||old!=pair.Value)
                SetOwn(pair.Key,pair.Value>0?pair.Key.SizeM/pair.Value:double.PositiveInfinity);
            source.Clear();foreach(var pair in incoming)source.Add(pair.Key,pair.Value);
        }
        public bool Matches(IEnumerable<KeyValuePair<TerrainTileKey,int>> visible)
        {
            int count=0;foreach(var pair in visible){count++;if(!source.TryGetValue(pair.Key,out int n)||n!=pair.Value)return false;}return count==source.Count;
        }
        bool Covered(TerrainTileKey key,double spacing)
        {
            if(nodes.TryGetValue(key,out var node)&&!double.IsPositiveInfinity(node.Best)&&node.Best<=spacing)return true;
            for(var at=key;at.Level<=highest;at=Parent(at))
                if(nodes.TryGetValue(at,out var ancestor)&&!double.IsPositiveInfinity(ancestor.Own)&&ancestor.Own<=spacing)return true;
            return false;
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
                double square=nearest*nearest,required=spacing*(1+1e-9);var group=requestedGroups[spacing];
                for(int i=0;i<group.Count;i++)if(!Covered(group[i],required))Visit(group[i],required,x,z,ref square);
                nearest=Math.Min(nearest,Math.Sqrt(square));
            }
            return nearest;
        }
        void Visit(TerrainTileKey key,double spacing,double x,double z,ref double nearest)
        {
            double distance=TerrainLod.DistanceSquared(key,x,z);if(distance>=nearest)return;
            if(!nodes.TryGetValue(key,out var node)){nearest=distance;return;}
            if(!double.IsPositiveInfinity(node.Best)&&node.Best<=spacing)return;
            if(key.Level<=0||!node.Children){nearest=distance;return;}
            for(int j=0;j<2;j++)for(int i=0;i<2;i++)Visit(Child(key,i,j),spacing,x,z,ref nearest);
        }
    }
}

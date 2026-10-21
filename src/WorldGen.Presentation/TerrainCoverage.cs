using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    public readonly struct TerrainFadeStamp
    {
        public readonly TerrainTileKey Key;public readonly float In,Out;
        public TerrainFadeStamp(TerrainTileKey key,float incoming=0,float outgoing=0){Key=key;In=incoming;Out=outgoing;}
    }
    public sealed class TerrainFadeTree
    {public float[] Nodes=Array.Empty<float>(),Timings=Array.Empty<float>();}
    /// <summary>Sparse exact quadtree holes in a resident fallback tile. No
    /// terrain heights change and missing children never remove coverage.</summary>
    public static class TerrainCoverage
    {
        public const double FadeSeconds=3;
        public static double Fade(double time,double incoming,double outgoing)
        {
            double Smooth(double x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
            return Math.Min(incoming<=0?1:Smooth((time-incoming)/FadeSeconds),outgoing<=0?1:1-Smooth((time-outgoing)/FadeSeconds));
        }
        public static TerrainFadeTree BuildFaded(TerrainTileKey tile,IEnumerable<TerrainFadeStamp> resident)
        {
            var nodes=new List<float>{0,0,0,0};var timings=new List<float>();bool any=false;
            // Coarser covering descendants dominate their children, whose own
            // fades are capped by those ancestors in the runtime.
            var sorted=new List<TerrainFadeStamp>(resident);sorted.Sort((a,b)=>b.Key.Level.CompareTo(a.Key.Level));
            foreach(var stamp in sorted)
            {
                var fine=stamp.Key;int depth=tile.Level-fine.Level;if(depth<=0||depth>24||fine.Scale!=tile.Scale)continue;
                long span=1L<<depth,x=fine.X-(long)tile.X*span,z=fine.Z-(long)tile.Z*span;
                if(x<0||z<0||x>=span||z>=span)continue;any=true;int node=0;
                for(int bit=depth-1;bit>=0;bit--)
                {
                    int slot=node*4+(int)((x>>bit)&1)+2*(int)((z>>bit)&1);
                    if(nodes[slot]<0)break;
                    if(bit==0){nodes[slot]=-1-timings.Count/4;timings.Add(stamp.In);timings.Add(stamp.Out);timings.Add(0);timings.Add(0);break;}
                    if(nodes[slot]==0){int next=nodes.Count/4;nodes.AddRange(new float[4]);nodes[slot]=next+1;}
                    node=(int)nodes[slot]-1;
                }
            }
            return any?new TerrainFadeTree{Nodes=nodes.ToArray(),Timings=timings.ToArray()}:new TerrainFadeTree();
        }
        // Four children/node: zero=visible, -1=covered, positive=node index+1.
        public static float[] Build(TerrainTileKey tile,IEnumerable<TerrainTileKey> resident)
        {
            var nodes=new List<float>{0,0,0,0};bool any=false;
            foreach(var fine in resident)
            {
                int depth=tile.Level-fine.Level;if(depth<=0||depth>24||fine.Scale!=tile.Scale)continue;
                long span=1L<<depth,x=fine.X-(long)tile.X*span,z=fine.Z-(long)tile.Z*span;
                if(x<0||z<0||x>=span||z>=span)continue;any=true;int node=0;
                for(int bit=depth-1;bit>=0;bit--)
                {
                    int slot=node*4+(int)((x>>bit)&1)+2*(int)((z>>bit)&1);
                    if(nodes[slot]<0)break;
                    if(bit==0){nodes[slot]=-1;break;}
                    if(nodes[slot]==0){int next=nodes.Count/4;nodes.AddRange(new float[4]);nodes[slot]=next+1;}
                    node=(int)nodes[slot]-1;
                }
            }
            return any?nodes.ToArray():Array.Empty<float>();
        }
        public static bool Visible(float[] nodes,double u,double v)
        {
            int node=0;if(nodes.Length==0)return true;
            u=Math.Max(0,Math.Min(1-1e-12,u));v=Math.Max(0,Math.Min(1-1e-12,v));
            for(int depth=0;depth<24;depth++)
            {
                u*=2;v*=2;int x=(int)u,z=(int)v;float next=nodes[node*4+x+z*2];
                if(next<=0)return next==0;node=(int)next-1;u-=x;v-=z;
            }
            return true;
        }
    }
}

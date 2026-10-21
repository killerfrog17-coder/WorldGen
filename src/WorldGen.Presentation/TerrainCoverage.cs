using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Sparse exact quadtree holes in a resident fallback tile. No
    /// terrain heights change and missing children never remove coverage.</summary>
    public static class TerrainCoverage
    {
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

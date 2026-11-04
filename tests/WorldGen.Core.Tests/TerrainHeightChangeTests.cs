using WorldGen.Presentation;

static class TerrainHeightChangeTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(int n in new[]{8,16,32,64,128})
        {
            var before=new float[(n+1)*(n+1)];for(int i=0;i<before.Length;i++)before[i]=(float)Math.Sin(i*.13);
            Check(TerrainHeightChange.Compare(before,(float[])before.Clone(),n)==null,"Unchanged surface needs no root reprojection");
            foreach(int pattern in new[]{0,1,2,3})
            {
                var after=(float[])before.Clone();
                for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
                    if(pattern==0?x==0:pattern==1?x==0||x==n||z==0||z==n:pattern==2?x==n/2&&z==n/2:(x*17+z*29)%47==0)after[z*(n+1)+x]+=.25f;
                var mask=TerrainHeightChange.Compare(before,after,n)!;
                double Height(float[] grid,int x,int z,double u,double v)
                {
                    int side=n+1;double a=grid[z*side+x],b=grid[z*side+x+1],c=grid[(z+1)*side+x],d=grid[(z+1)*side+x+1];
                    return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
                }
                int affected=0;
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)foreach(var uv in new[]{(0d,0d),(.17,.28),(.8,.6),(1d,1d)})
                {
                    bool different=Height(before,x,z,uv.Item1,uv.Item2)!=Height(after,x,z,uv.Item1,uv.Item2);
                    bool changed=mask.Affects(x+uv.Item1,z+uv.Item2);
                    Check(!different||changed,"Height mask omitted an altered rendered triangle");
                    if(changed)affected++;
                }
                if(pattern<=1)Check(affected<n*n*2,"Seam-only changes must preserve the unchanged tile interior");
                Check(!mask.Affects(-1,-1)&&!mask.Affects(n+1,n+1),"No changes outside the tile");
                double probe=pattern==2?n*.5:.5;
                Check(mask.Affects(probe,probe,1),"A footprint reaching changed cells must reproject");
            }
            var left=(float[])before.Clone();left[0]+=.25f;
            var right=(float[])before.Clone();right[right.Length-1]-=.25f;
            var union=TerrainHeightChange.Compare(before,left,n)!;union.Union(TerrainHeightChange.Compare(before,right,n)!);
            Check(union.Affects(.1,.1)&&union.Affects(n-.1,n-.1)&&!union.Affects(n*.5,n*.5),"Multiple replacements union their affected cells without filling the interior");
        }
        Console.WriteLine($"PASS {checks} rendered triangle containment, seam-only interiors, footprint margins and union checks");
    }
}

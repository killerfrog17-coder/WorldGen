using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainSurfaceSeamTests
{
    static int Index(int n,int edge,int k)=>edge==0?k*(n+1):edge==1?k*(n+1)+n:edge==2?k:n*(n+1)+k;
    public static double AttributeGap(LocalTerrainMesh a,LocalTerrainMesh b,int edge,Func<LocalTerrainMesh,float[]> field,int stride,int channels)
    {
        bool vertical=edge<2;double start=vertical?a.NorthM:a.EastM,other=vertical?b.NorthM:b.EastM;double gap=0;
        var av=field(a);var bv=field(b);
        for(int k=0;k<=a.Segments;k++)
        {
            double at=start+k*a.SizeM/a.Segments;if(at<other-1e-7||at>other+b.SizeM+1e-7)continue;
            double f=(at-other)/b.SizeM*b.Segments;int lo=Math.Clamp((int)Math.Floor(f),0,b.Segments-1);double t=f-lo;
            for(int c=0;c<channels;c++)
            {
                double expected=bv[Index(b.Segments,edge^1,lo)*stride+c]*(1-t)+bv[Index(b.Segments,edge^1,lo+1)*stride+c]*t;
                gap=Math.Max(gap,Math.Abs(av[Index(a.Segments,edge,k)*stride+c]-expected));
            }
        }
        return gap;
    }
    public static void Run(string path,bool canyon=false)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var world=path==null?null:WorldFile.ReadFile(path);
        if(world==null)
        {
            var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var surface=SphericalMesh.Build(recipe,default);int count=surface.Cells.Length;
            world=new World{Recipe=recipe,Surface=surface,ElevationM=Enumerable.Repeat(2500,count).ToArray(),FoldPermille=Enumerable.Repeat(1000,count).ToArray(),PlateauPermille=new int[count],Hydrology=new HydrologyResult{OceanConnected=new bool[count]}};
        }
        var terrain=new LocalTerrain(world,horizontalScale:.1);
        int originCell=path==null?world.Surface.Cells.Length/3:Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30).OrderByDescending(i=>terrain.Profile(i).MountainHeightM).First();
        var origin=world.Surface.Cells[originCell].Position;
        if(canyon)
        {
            var reach=terrain.Amplification.Canyons.Reaches.OrderByDescending(r=>r.Relief*r.Strength).First();
            V center=(reach.A+reach.B).Unit,side=V.Cross(center,reach.B-reach.A).Unit;
            origin=(center+side*(reach.HalfWidth*.45/(terrain.RadiusM*.1))).Unit;
            Console.WriteLine($"Actual fractured canyon seam context: source{reach.Source}");
        }
        Console.WriteLine($"Surface-seam terrain: cell{originCell}, elevation{world.ElevationM[originCell]}, inherited relief{terrain.Profile(originCell).MountainHeightM}");
        foreach(double scale in new[]{.1,.3,1d})for(int turn=0;turn<4;turn++)
        {
            TerrainTileKey Rotate(int x,int z,int level){for(int i=0;i<turn;i++)(x,z)=(-z-1,x);return new TerrainTileKey(x,z,level,scale);}
            var a=Rotate(-1,0,1);var b=Rotate(0,0,0);var c=Rotate(0,1,0);
            var plan=new Dictionary<TerrainTileKey,int>{{a,32},{b,32},{c,8}};var edges=TerrainEdges.Build(plan);
            foreach(var item in edges)Check(item.Value.SequenceEqual(TerrainEdges.For(item.Key,plan)),"Indexed corner constraints equal an inclusive literal incident-tile lookup");
            var frame=new LocalTerrainFrame(origin,terrain.RadiusM,0,scale);
            LocalTerrainMesh Mesh(TerrainTileKey k)=>LocalTerrainMesh.Build(terrain,frame,k.X*k.SizeM,k.Z*k.SizeM,k.SizeM,plan[k],edgeSteps:edges[k]);
            var ma=Mesh(a);var mb=Mesh(b);var mc=Mesh(c);int side=turn==0?1:turn==1?3:turn==2?0:2;
            foreach(var pair in new[]{(mb,ma,side^1),(ma,mc,side)})
            {
                var fine=pair.Item1;var coarse=pair.Item2;int edge=pair.Item3;
                string context=$"scale{scale:R}, turn{turn}, edge{edge}, first({fine.EastM:R},{fine.NorthM:R},size{fine.SizeM:R},segments{fine.Segments}), second({coarse.EastM:R},{coarse.NorthM:R},size{coarse.SizeM:R},segments{coarse.Segments})";
                double geometryGap=TerrainBoundaryTests.Gap(fine,coarse,edge),normalGap=AttributeGap(fine,coarse,edge,m=>m.Normals,3,3);
                Check(geometryGap<.001,$"Surface shading never opens a geometric boundary: gap{geometryGap:R}, {context}");
                Check(normalGap<2e-6,$"Interpolated lighting normals match across detail levels: gap{normalGap:R}, {context}");
                Check(AttributeGap(fine,coarse,edge,m=>m.Colors,4,4)<2e-6,"Albedo and cover alpha match across detail levels");
                Check(AttributeGap(fine,coarse,edge,m=>m.SurfaceWeights,4,4)<2e-6&&AttributeGap(fine,coarse,edge,m=>m.GroundWeightsB,4,4)<2e-6,"All geology material weights match across detail levels");
                // Channel13 is the conservative physical-water root gate; it
                // intentionally depends on each grid cell's overlap with water.
                double coverGap=AttributeGap(fine,coarse,edge,m=>m.GroundCover,16,13);
                Check(coverGap<2e-6,$"Ground-cover colours and forms match across detail levels: {coverGap:R}, scale{scale}, turn{turn}, edge{edge}");
            }
        }
        foreach(double scale in new[]{.01,.1,.3,1d,2d})
        {
            var plan=new Dictionary<TerrainTileKey,int>{{new(-1,-1,0,scale),4},{new(0,-1,0,scale),8},{new(-1,0,0,scale),16},{new(0,0,0,scale),32}};
            var edges=TerrainEdges.Build(plan);double largest=1024*scale/4;
            foreach(var pair in plan)
            {
                var k=pair.Key;double atX=k.X<0?k.SizeM:0,atZ=k.Z<0?k.SizeM:0;
                double radius=Math.Max(TerrainEdges.SurfaceAt(edges[k],k.X<0?1:0,atZ),TerrainEdges.SurfaceAt(edges[k],k.Z<0?3:2,atX));
                Check(Math.Max(radius,k.SizeM/pair.Value)==largest,"All four tiles agree on the diagonal coarse corner's derivative radius");
            }
        }
        Console.WriteLine($"PASS {checks} mixed-detail normal/material/cover/corner assertions");
    }
}

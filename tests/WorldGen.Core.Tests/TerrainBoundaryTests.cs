using WorldGen.Core;
using WorldGen.Presentation;

static class TerrainBoundaryTests
{
    public static double Gap(LocalTerrainMesh a,LocalTerrainMesh b,int edge)
    {
        bool vertical=edge<2;double origin=vertical?a.NorthM:a.EastM,other=vertical?b.NorthM:b.EastM;
        int Index(int segments,int side,int k)=>side==0?k*(segments+1):side==1?k*(segments+1)+segments:side==2?k:segments*(segments+1)+k;
        double gap=0;
        void Probe(LocalTerrainMesh from,LocalTerrainMesh to,int side,int opposite,double start,double end)
        {
            for(int k=0;k<=from.Segments;k++)
            {
                double at=start+k*from.SizeM/from.Segments;if(at<end-1e-7||at>end+to.SizeM+1e-7)continue;
                double f=(at-end)/to.SizeM*to.Segments;int lo=Math.Clamp((int)Math.Floor(f),0,to.Segments-1);double t=f-lo;
                double y=to.Positions[Index(to.Segments,opposite,lo)*3+1]*(1-t)+to.Positions[Index(to.Segments,opposite,lo+1)*3+1]*t;
                gap=Math.Max(gap,Math.Abs(from.Positions[Index(from.Segments,side,k)*3+1]-y));
            }
        }
        Probe(a,b,edge,edge^1,origin,other);Probe(b,a,edge^1,edge,other,origin);return gap;
    }
    public static void Run(LocalTerrain terrain,LocalTerrainFrame original,Action<bool,string> check)
    {
        foreach(double scale in new[]{.1,1d,2d})for(int turn=0;turn<4;turn++)
        {
            TerrainTileKey Rotate(int x,int z,int level)
            {for(int i=0;i<turn;i++)(x,z)=(-z-1,x);return new TerrainTileKey(x,z,level,scale);}
            var left=Rotate(-1,0,1);var near=Rotate(0,0,0);var far=Rotate(0,1,0);
            var plan=new Dictionary<TerrainTileKey,int>{{left,64},{near,64},{far,16}};var edges=TerrainEdges.Build(plan);
            var frame=new LocalTerrainFrame(original.Origin,terrain.RadiusM,original.AltitudeM,scale);
            LocalTerrainMesh Build(TerrainTileKey k)=>LocalTerrainMesh.Build(terrain,frame,k.X*k.SizeM,k.Z*k.SizeM,k.SizeM,plan[k],edgeSteps:edges[k]);
            var a=Build(left);var b=Build(near);var c=Build(far);int side=turn==0?1:turn==1?3:turn==2?0:2;
            double gap=Math.Max(Gap(a,b,side),Gap(a,c,side));
            check(gap<.001,$"Both halves of a mixed-neighbour boundary join after rotation {turn}, scale {scale}: gap {gap:R}m");
            check(edges.All(p=>p.Value.SequenceEqual(TerrainLod.EdgeSteps(p.Key,plan))),"Indexed intervals equal the independent all-pairs boundary lookup");
            foreach(var pair in plan)
            {
                using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);using var reader=new BinaryReader(stream);
                TerrainMeshWire.WriteRequest(writer,frame,pair.Key.X*pair.Key.SizeM,pair.Key.Z*pair.Key.SizeM,pair.Key.SizeM,pair.Value,edges[pair.Key]);stream.Position=0;
                TerrainMeshWire.ReadRequest(reader,out _,out _,out _,out int n,out var restored);
                check(n==pair.Value&&restored.SequenceEqual(edges[pair.Key]),"Worker transport preserves every partial-edge interval");
            }
        }
        foreach(double scale in new[]{.01,.1,.3,1d,2d})
        {
            var key=new TerrainTileKey(6,-7,0,scale);var neighbour=new TerrainTileKey(7,-7,0,scale);
            var plan=new Dictionary<TerrainTileKey,int>{{key,64},{neighbour,16}};var edges=TerrainEdges.Build(plan);
            check(TerrainEdges.At(edges[key],1,key.SizeM*.5)==neighbour.SizeM/16,"Integer grid adjacency survives non-binary world scales and negative coordinates");
        }
        var tile=new TerrainTileKey(0,0,0);var first=new[]{0d,64,0,0,1,0,512,16,1,512,1024,64};var second=(double[])first.Clone();second[7]=32;
        check(!new TerrainProductKey(tile,64,first).Equals(new TerrainProductKey(tile,64,second)),"Cached products distinguish partial-edge changes even with identical four maxima");
        var frozen=new TerrainProductKey(tile,64,first);first[7]=99;check(!frozen.Equals(new TerrainProductKey(tile,64,first)),"Stored cache keys own an immutable edge snapshot");
        var lookup=new[]{0d,64,0,0,1,0,512,16,1,512,1024,64};long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)_ = new TerrainProductKey(tile,64,lookup,false).GetHashCode();
        check(GC.GetAllocatedBytesForCurrentThread()==before,"Repeated cache lookup keys allocate no arrays");
    }
}

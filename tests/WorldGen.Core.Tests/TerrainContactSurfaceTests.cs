using WorldGen.Presentation;

static class TerrainContactSurfaceTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
        // A steep wall and a cut riverbed share the same regular corner heights.
        // The extra centre is part of the final mesh and must own contact height.
        var mesh=new LocalTerrainMesh{SizeM=16,Segments=4,GroundVertices=25,
            Positions=new float[]{0,0,0, 16,160,0, 0,0,16, 16,160,16, 8,-4,8},
            Triangles=new[]{0,2,4, 0,4,1, 1,4,3, 2,3,4}};
        var surface=TerrainContactSurface.Build(mesh)!;
        Check(surface.TryHeight(8,8,out double y)&&Math.Abs(y+4)<1e-8,"Actual refined riverbed centre, not regular-grid80m, owns contact");
        Check(surface.TryHeight(12,8,out y)&&Math.Abs(y-78)<1e-8,"Steep rendered triangle owns player support");
        Check(surface.TryHeight(0,8,out y)&&Math.Abs(y)<1e-8,"Shared edge retains exact contact");
        Check(surface.TryHeight(16,16,out y)&&Math.Abs(y-160)<1e-8,"Inclusive outer boundary resolves");
        Check(!surface.TryHeight(-1,8,out y),"Unowned query does not invent ground");
        Check(!surface.TryHeight(double.NaN,8,out y),"Invalid coordinates do not alias a grid cell");
        mesh.Positions[13]=90;
        Check(surface.TryHeight(8,8,out y)&&Math.Abs(y+4)<1e-8,"Contact retains geometry after transport arrays are reused");
        long originalBytes=surface.Bytes;surface.SetHeight(0,40);surface.SetHeight(2,40);
        Check(surface.TryHeight(0,8,out y)&&Math.Abs(y-40)<1e-8,"Contact follows a conformed resident edge");
        Check(surface.TryHeight(4,8,out y)&&Math.Abs(y-18)<1e-8,"Interior triangle interpolates its changed edge and unchanged riverbed");
        Check(surface.ChangedTriangleCells(4,new[]{0,2})!.Affects(3,.25),"Large incident triangles mark their true distant cell bounds, not a regular vertex stencil");
        Check(surface.TryOriginalHeight(0,8,out y)&&Math.Abs(y)<1e-8&&surface.OriginalHeight(0)==0,"Canonical edge sampling survives resident fitting");
        Check(surface.TryOriginalHeight(4,8,out y)&&Math.Abs(y+2)<1e-8,"Original triangle interpolation is independent of fitted heights");
        Check(surface.Bytes==originalBytes+5*4,"Only edited contacts retain one extra height per vertex");
        surface.SetHeight(0,surface.OriginalHeight(0));surface.SetHeight(2,surface.OriginalHeight(2));
        Check(surface.TryHeight(4,8,out y)&&Math.Abs(y+2)<1e-8,"Restoring original edge vertices restores contact interpolation");
        mesh.SizeM=4096;var distant=TerrainContactSurface.Build(mesh);
        Check(distant!=null&&distant.TryHeight(0,8,out y)&&Math.Abs(y)<1e-8,"Refined distant resident edges retain their actual triangle contact");
        var regular=new LocalTerrainMesh{SizeM=16,Segments=4,GroundVertices=25,Positions=new float[75],Triangles=LocalTerrainMesh.SurfaceTriangles(4)};
        Check(TerrainContactSurface.Build(regular)==null,"Ordinary regular-grid interpolation has no duplicate contact data");
        // Refined perimeter knots are not regular-grid vertex indices. Their
        // actual incident triangles, not a guessed four-cell stencil, define
        // where root heights may change after resident edge fitting.
        const int n=8,side=9;const double size=80,step=10;
        var positions=new List<float>();
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
        {positions.Add(x*10);positions.Add((float)(Math.Sin(x*.3)+z*.2));positions.Add(z*10);}
        var indices=LocalTerrainMesh.SurfaceTriangles(n).ToList();var edits=new List<int>{0};
        foreach(var point in new[]{(0f,5f),(80f,35f),(25f,0f),(45f,80f)})
        {
            int vertex=positions.Count/3;edits.Add(vertex);positions.Add(point.Item1);positions.Add(-2);positions.Add(point.Item2);
            var split=new List<int>();bool found=false;
            for(int i=0;i<indices.Count;i+=3)
            {
                bool divided=false;
                for(int e=0;e<3;e++)
                {
                    int a=indices[i+e],b=indices[i+(e+1)%3],c=indices[i+(e+2)%3];
                    double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2],px=point.Item1,pz=point.Item2;
                    if(Math.Abs((px-ax)*(bz-az)-(pz-az)*(bx-ax))>1e-6||(px-ax)*(px-bx)+(pz-az)*(pz-bz)>=0)continue;
                    split.AddRange(new[]{a,vertex,c,vertex,b,c});divided=found=true;break;
                }
                if(!divided)split.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
            }
            Check(found,"Refined-mask fixture must split an actual perimeter edge");indices=split;
        }
        var refinedMesh=new LocalTerrainMesh{EastM=-160,NorthM=-240,SizeM=size,Segments=n,GroundVertices=side*side,Positions=positions.ToArray(),Triangles=indices.ToArray()};
        var refined=TerrainContactSurface.Build(refinedMesh)!;
        Check(refined.ChangedTriangleCells(n,new[]{-1})==null&&refined.ChangedTriangleCells(n,new[]{positions.Count/3})==null,"Invalid edited vertices require conservative whole-key fallback");
        Check(refined.ChangedTriangleCells(4,new[]{0})==null,"Incompatible regular-grid resolution requires conservative whole-key fallback");
        foreach(int vertex in edits)
        {
            var mask=refined.ChangedTriangleCells(n,new[]{vertex})!;
            refined.SetHeight(vertex,refined.OriginalHeight(vertex)+7);
            int changed=0;
            for(int z=0;z<=160;z++)for(int x=0;x<=160;x++)
            {
                double localX=x*.5,localZ=z*.5;
                bool originalFound=refined.TryOriginalHeight(localX,localZ,out double before),currentFound=refined.TryHeight(localX,localZ,out double current);
                Check(originalFound&&currentFound,"Refined-mask sample must have actual drawn contact");
                if(Math.Abs(current-before)<1e-10)continue;
                changed++;
                // Use negative world origins exactly as the runtime mask
                // consumer does; masks themselves remain tile-local cells.
                double worldX=refinedMesh.EastM+localX,worldZ=refinedMesh.NorthM+localZ;
                Check(mask.Affects((worldX-refinedMesh.EastM)/step,(worldZ-refinedMesh.NorthM)/step),"Refined mask omitted a changed drawn triangle height");
            }
            Check(changed>0,"Each refined edited vertex must alter observed contact");
            double vx=positions[vertex*3]/step,vz=positions[vertex*3+2]/step;
            Check(mask.Affects(vx,vz)&&mask.Affects(vx-.03,vz-.03,.05),"Refined seam/border and crossing footprint margins remain affected");
            Check(!mask.Affects(4,4)&&!mask.Affects(4,4,.1),"Refined seam changes must leave the distant unchanged interior unmasked");
            refined.SetHeight(vertex,refined.OriginalHeight(vertex));
        }
        Console.WriteLine("Terrain final-triangle contact PASS "+checks);
    }
}

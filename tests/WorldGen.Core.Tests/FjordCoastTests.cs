using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class FjordCoastTests
{
    public static void Run(string input,string output)
    {
        Directory.CreateDirectory(output);int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var timer=System.Diagnostics.Stopwatch.StartNew();var world=WorldFile.ReadFile(input);string history=world.Prehistory.Hash;
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);double prepared=timer.Elapsed.TotalSeconds;
        var fjords=terrain.Amplification.Fjords;double radius=terrain.RadiusM*.1;
        Console.WriteLine($"Fjords {fjords.Inlets.Length}, sections {fjords.Sections.Length}, faces {fjords.IndexedFaces}, index {fjords.IndexBytes} bytes, max candidates {fjords.MaximumCandidates}, prepare {prepared:F2}s");
        Check(fjords.Inlets.Length>0,"Actual glaciated coasts produce at least one inlet");
        var locator=new CellLocator(world.Surface.Cells);var rows=new List<object>();var exact=new List<(V,LocalTerrainSample)>();int indexProbes=0,wetProbes=0;
        foreach(var inlet in fjords.Inlets)
        {
            Check(world.Glaciated[inlet.Mouth]&&world.Hydrology.OceanConnected[inlet.Ocean],"Fjord belongs to mapped glacial coast and existing ocean receiver");
            Check(inlet.LengthM>=3500&&inlet.LengthM<=35000&&inlet.Relief>=300,"Short creeks and low coastal tundra do not become fjords");
            var origin=fjords.Sections[inlet.First].A;var patch=terrain.Patch(origin,50000);
            double shallowest=double.NegativeInfinity;int dry=0;
            for(int i=inlet.First;i<inlet.First+inlet.Count;i++)
            {
                var s=fjords.Sections[i];
                foreach(double t in new[]{0d,.25,.5,.75,1})
                {
                    V p=(s.A*(1-t)+s.B*t).Unit;var sample=terrain.Patch(p,3000).Sample(p);wetProbes++;shallowest=Math.Max(shallowest,sample.ElevationM);
                    if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.Ocean||sample.WaterSurfaceM!=0||sample.WaterBodyKey!=1)dry++;
                }
                if((i-inlet.First)%Math.Max(1,inlet.Count/3)==0)
                {
                    V center=(s.A+s.B).Unit;
                    // Determine the actual containing coarse face through an
                    // incident-cell walk, not a guessed nearest face ID.
                    int cell=locator.Nearest(center);var candidates=new HashSet<int>{cell};foreach(int n in world.Surface.Cells[cell].Neighbors)candidates.Add(n);
                    int face=-1;var ids=world.Surface.Triangles;
                    for(int f=0;f<ids.Length/3;f++)if(candidates.Contains(ids[f*3])&&Contains(world.Surface.Cells[ids[f*3]].Position,world.Surface.Cells[ids[f*3+1]].Position,world.Surface.Cells[ids[f*3+2]].Position,center)){face=f;break;}
                    Check(face>=0,"Find a real coarse face for fjord index proof");
                    double indexed=fjords.Apply(center,face,3000,out double fp,out double bare),brute=3000,bf=double.NegativeInfinity,be=0;
                    foreach(var section in fjords.Sections)
                    {
                        double cut=section.Cut(center,radius,3000,out double local,out double exposure);
                        if(cut<brute){brute=cut;be=exposure;}else if(cut==brute)be=Math.Min(be,exposure);bf=Math.Max(bf,local);
                    }
                    Check(indexed==brute&&bare==be&&(fp==bf||fp<=0&&bf<=0),"Numeric face index agrees with exhaustive physical sections");indexProbes++;
                }
            }
            Console.WriteLine($"Inlet {inlet.Mouth}: {inlet.LengthM:F0}m, {inlet.Count} sections, shallowest {shallowest:F3}, non-ocean {dry}");
            Check(dry==0,"Fjord centerline has continuous real sea water through every section and joint: "+inlet.Mouth+" / "+dry);
            if(rows.Count<5)
            {
                var middle=fjords.Sections[inlet.First+inlet.Count/2];V center=(middle.A+middle.B).Unit,side=V.Cross(center,middle.B-middle.A).Unit;
                var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);
                var a=LocalTerrainMesh.Build(terrain,frame,-128,-64,128,32);var b=LocalTerrainMesh.Build(terrain,frame,0,-64,128,32);
                Check(a.WaterTriangles.Length>0&&b.WaterTriangles.Length>0,"Submerged inlet has rendered ocean geometry on both adjacent tiles");
                for(int z=0;z<=32;z++)Check(a.Positions[(z*33+32)*3+1]==b.Positions[z*33*3+1],"Fjord ground tiles agree exactly at shared edge");
                Check(a.WaterColors.Where((_,i)=>i%4==3).All(v=>v==3)&&b.WaterColors.Where((_,i)=>i%4==3).All(v=>v==3),"Fjord mesh uses existing ocean surface encoding");
                var crossPatch=terrain.Patch(center,50000);
                for(int j=-20;j<=20;j++)
                {V p=(center+side*(j*inlet.HalfWidth/15/radius)).Unit;var sample=crossPatch.Sample(p);exact.Add((p,sample));Check(Math.Abs(crossPatch.Elevation(p)-sample.ElevationM)<=.002,"Collision height agrees with complete fjord sample");}
                rows.Add(new{inlet.Mouth,inlet.Ocean,inlet.Head,inlet.LengthM,inlet.HalfWidth,inlet.Depth,inlet.Relief,inlet.Count,Position=new[]{center.X,center.Y,center.Z},Shallowest=shallowest});
                if(rows.Count<=3)
                {
                    var start=fjords.Sections[inlet.First];var end=fjords.Sections[inlet.First+inlet.Count-1];
                    double low=0,high=Math.Max(middle.WidthA,middle.WidthB)*1.6;
                    for(int j=0;j<20;j++)
                    {
                        double d=(low+high)*.5;var sample=crossPatch.Sample((center+side*(d/radius)).Unit);
                        if(sample.Underwater&&sample.WaterKind==LocalWaterKind.Ocean&&sample.WaterSurfaceM-sample.ElevationM>5)low=d;else high=d;
                    }
                    V bank=(center+side*(low/radius)).Unit;
                    foreach(var (label,at) in new[]{("head",start.A),("mouth",end.A),("bank",bank)})
                    {
                        var waterFrame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);var mesh=LocalTerrainMesh.Build(terrain,waterFrame,-128,-128,256,64);
                        var local=terrain.Patch(at,5000);int wet=0;
                        for(int z=1;z<32;z++)for(int x=1;x<32;x++)
                        {
                            double mx=x*8,mz=z*8;V p=waterFrame.Position(mesh.EastM+mx,mesh.NorthM+mz);var sample=local.Sample(p);
                            if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.Ocean||sample.WaterSurfaceM-sample.ElevationM<3)continue;
                            wet++;Check(Covered(mesh,mx,mz,waterFrame.SurfaceY(p,0)),"Actual ocean water triangles cover submerged "+label+" in inlet "+inlet.Mouth);
                        }
                        Check(wet>0,"Coverage proof includes actual submerged "+label+" points");
                        Check(mesh.WaterPositions.All(float.IsFinite),"Head/mouth/bank water geometry stays finite");
                        Console.WriteLine($"  {label}: {wet} actual wet points covered by water triangles");
                    }
                }
            }
        }
        var cached=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);Check(cached.Amplification.UsedCachedRiverDatums,"Fjord terrain supports cached final river derivation");
        var wooded=fjords.Inlets.First(f=>f.Mouth==173461);var woodedSection=fjords.Sections[wooded.First+wooded.Count/2];V woodedCenter=(woodedSection.A+woodedSection.B).Unit;
        var woodedSample=terrain.Patch(woodedCenter,5000).Sample(woodedCenter);
        Check(woodedSample.Province.GlacialExposure<.05&&woodedSample.Forest.Coverage>.3,"A neighbour's nonwinning wall cannot strip soil/forest potential from the wooded inlet basin");
        foreach(var (p,s) in exact.AsEnumerable().Reverse())Check(StateHash.Compute(cached.Patch(p,50000).Sample(p))==StateHash.Compute(s),"Cached reverse-order terrain/water/soil reconstruction is exact");
        var profiles=Enumerable.Range(0,world.Surface.Cells.Length).Select(terrain.Profile).ToArray();var scaleRows=new List<object>();
        foreach(double scale in new[]{.01,.5,1d,2})
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();var resized=new FjordCoasts(world,profiles,scale,locator);
            Check(resized.Inlets.Select(f=>(f.Mouth,f.Ocean,f.Head)).SequenceEqual(fjords.Inlets.Select(f=>(f.Mouth,f.Ocean,f.Head))),"Mapped fjord admission is stable across horizontal scale "+scale);
            Check(resized.Inlets.All(f=>double.IsFinite(f.HalfWidth)&&f.HalfWidth>0&&f.Depth>0),"Rescaled inlet dimensions remain finite and positive");
            scaleRows.Add(new{Scale=scale,Inlets=resized.Inlets.Length,Sections=resized.Sections.Length,resized.IndexBytes,PrepareMs=clock.Elapsed.TotalMilliseconds});
        }
        Check(world.Prehistory.Hash==history,"Local fjord terrain does not mutate broad history");
        File.WriteAllText(Path.Combine(output,"fjords.json"),System.Text.Json.JsonSerializer.Serialize(new{PreparedSeconds=prepared,Checks=checks,Inlets=fjords.Inlets.Length,Sections=fjords.Sections.Length,fjords.IndexBytes,fjords.IndexedFaces,fjords.MaximumCandidates,IndexProbes=indexProbes,ContinuousWetProbes=wetProbes,ExactCachedSamples=exact.Count,Scales=scaleRows,Sites=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} fjord controls, {wetProbes} continuous ocean probes, {indexProbes} exhaustive index comparisons; {timer.Elapsed.TotalSeconds:F2}s");
    }
    static bool Contains(V a,V b,V c,V p)
    {
        double Dot(V u,V v)=>u.X*v.X+u.Y*v.Y+u.Z*v.Z;
        double det=Dot(a,V.Cross(b,c));if(Math.Abs(det)<1e-30)return false;
        return Math.Min(Dot(p,V.Cross(b,c))/det,Math.Min(Dot(p,V.Cross(c,a))/det,Dot(p,V.Cross(a,b))/det))>=-1e-8;
    }
    static bool Covered(LocalTerrainMesh mesh,double x,double z,double expectedY)
    {
        var p=mesh.WaterPositions;var triangles=mesh.WaterTriangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            int a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3;
            double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-bz*cx;
            if(Math.Abs(det)<1e-12)continue;
            double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
            if(u< -1e-6||v< -1e-6||u+v>1.000001)continue;
            double y=p[a+1]+(p[b+1]-p[a+1])*u+(p[c+1]-p[a+1])*v;
            if(Math.Abs(y-expectedY)<.025)return true;
        }
        return false;
    }
}

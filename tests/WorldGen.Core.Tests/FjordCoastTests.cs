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
                    {brute=Math.Min(brute,section.Cut(center,radius,3000,out double local,out double exposure));bf=Math.Max(bf,local);be=Math.Max(be,exposure);}
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
                Console.WriteLine("Water kinds: "+string.Join(",",a.WaterColors.Where((_,i)=>i%4==3).Distinct())+" / "+string.Join(",",b.WaterColors.Where((_,i)=>i%4==3).Distinct()));
                foreach(var mesh in new[]{a,b})
                {
                    var levels=new List<double>();for(int k=0;k<mesh.WaterColors.Length/4;k++)if(mesh.WaterColors[k*4+3]!=3)
                    {var p=frame.Position(mesh.EastM+mesh.WaterPositions[k*3],mesh.NorthM+mesh.WaterPositions[k*3+2]);levels.Add(mesh.WaterPositions[k*3+1]-frame.SurfaceY(p,0));}
                    Console.WriteLine("Nonsea vertices "+levels.Count+" sea-relative range "+(levels.Count>0?levels.Min()+".."+levels.Max():"none"));
                }
                Check(a.WaterColors.Where((_,i)=>i%4==3).All(v=>v==3)&&b.WaterColors.Where((_,i)=>i%4==3).All(v=>v==3),"Fjord mesh uses existing ocean surface encoding");
                var crossPatch=terrain.Patch(center,50000);
                for(int j=-20;j<=20;j++)
                {V p=(center+side*(j*inlet.HalfWidth/15/radius)).Unit;var sample=crossPatch.Sample(p);exact.Add((p,sample));Check(Math.Abs(crossPatch.Elevation(p)-sample.ElevationM)<=.002,"Collision height agrees with complete fjord sample");}
                rows.Add(new{inlet.Mouth,inlet.Ocean,inlet.Head,inlet.LengthM,inlet.HalfWidth,inlet.Depth,inlet.Relief,inlet.Count,Position=new[]{center.X,center.Y,center.Z},Shallowest=shallowest});
            }
        }
        var cached=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);Check(cached.Amplification.UsedCachedRiverDatums,"Fjord terrain supports cached final river derivation");
        foreach(var (p,s) in exact.AsEnumerable().Reverse())Check(StateHash.Compute(cached.Patch(p,50000).Sample(p))==StateHash.Compute(s),"Cached reverse-order terrain/water/soil reconstruction is exact");
        Check(world.Prehistory.Hash==history,"Local fjord terrain does not mutate broad history");
        File.WriteAllText(Path.Combine(output,"fjords.json"),System.Text.Json.JsonSerializer.Serialize(new{PreparedSeconds=prepared,Checks=checks,Inlets=fjords.Inlets.Length,Sections=fjords.Sections.Length,fjords.IndexBytes,fjords.IndexedFaces,fjords.MaximumCandidates,IndexProbes=indexProbes,ContinuousWetProbes=wetProbes,ExactCachedSamples=exact.Count,Sites=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} fjord controls, {wetProbes} continuous ocean probes, {indexProbes} exhaustive index comparisons; {timer.Elapsed.TotalSeconds:F2}s");
    }
    static bool Contains(V a,V b,V c,V p)
    {
        double Dot(V u,V v)=>u.X*v.X+u.Y*v.Y+u.Z*v.Z;
        double det=Dot(a,V.Cross(b,c));if(Math.Abs(det)<1e-30)return false;
        return Math.Min(Dot(p,V.Cross(b,c))/det,Math.Min(Dot(p,V.Cross(c,a))/det,Dot(p,V.Cross(a,b))/det))>=-1e-8;
    }
}

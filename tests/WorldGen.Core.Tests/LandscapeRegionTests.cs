using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LandscapeRegionTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        var sandy=new GroundProfile{Sand=.94,Clay=.03,Gravel=.02,DepthM=2,Drainage=.85,RockMixA=new V(0,0,1)};
        var dunes=LandscapeProvince.Describe(sandy,Biome.Desert,.08,0,40,0,0);
        Check(dunes.Sand>.85,"Dry loose sand supports dunes");
        var gravel=sandy;gravel.Sand=.25;gravel.Gravel=.65;
        Check(LandscapeProvince.Describe(gravel,Biome.Desert,.08,0,40,0,0).Sand<.01,"Rocky desert is not blanket dunes");
        var peat=new GroundProfile{Organic=.45,Drainage=.05,Clay=.2,Sand=.2};
        var bog=LandscapeProvince.Describe(peat,Biome.Peatland,.9,.1,30,0,0);
        Check(bog.Peat==1&&bog.Wet==1,"Peatlands retain their wet organic character");
        Check(LandscapeProvince.Region(bog,new V(1000,2000,3000),41,Biome.Peatland)==LandscapeRegion.Peatland,"Peat is distinct from woody swamp");
        var frozen=LandscapeProvince.Describe(gravel,Biome.Tundra,.7,0,60,1,0);
        Check(frozen.Glacial==0&&frozen.Frozen==1&&LandscapeProvince.Region(frozen,new V(1000,2000,3000),41,Biome.Tundra)==LandscapeRegion.Tundra,"Permafrost alone does not imply a glaciated landscape");
        Check(LandscapeProvince.Region(frozen,new V(1000,2000,3000),41,Biome.Ice)==LandscapeRegion.Icefield,"Active ice cover is distinct from exposed glacial lowlands");
        Check(LandscapeProvince.Region(new LandscapeControls{Wet=.41f,Canopy=.6f},new V(1000,2000,3000),41,Biome.BorealForest)!=LandscapeRegion.WoodedSwamp,"A moist upland forest is not automatically classified as a swamp");
        var marshHabitat=new HabitatCell{Biome=Biome.Marsh,WarmTenthsC=200,FloodFractionPermille=500};
        var marshSoil=new GroundProfile{DepthM=1};bool openWetland=false,woodedWetland=false;
        for(int i=0;i<100;i++)
        {
            var setting=LandscapeProvince.WetlandTreeSetting(marshHabitat,marshSoil,new V(i*2000,1200,2700),41);
            if(setting==Biome.Marsh)openWetland=true;else woodedWetland=true;
        }
        Check(openWetland&&woodedWetland,"Forest-capable wet lowlands retain both coherent open and wooded settings");
        marshHabitat.WarmTenthsC=80;
        Check(LandscapeProvince.WetlandTreeSetting(marshHabitat,marshSoil,new V(1000,2000,3000),41)==Biome.Marsh,"Cold treeless marsh does not acquire a warm swamp canopy");
        var wet=new LandscapeControls{Wet=1,Moisture=1,Canopy=.2f};
        double radius=637100;var center=new V(1,0,0);var basins=new SmallWaterBodies(73421,radius);
        int probes=0;SmallWaterGround Flat(V p){probes++;return new SmallWaterGround(100,double.NaN,LocalWaterKind.None,wet);}
        var candidates=basins.Candidates(center,6000);Check(candidates.Length>20,"Bounded deterministic basin candidate footprint");
        var lakes=candidates.Select(p=>basins.At(p,Flat)).Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
        Check(lakes.Length>5,"Suitable wet lowland produces surveyed enclosed ponds");
        double separation=double.PositiveInfinity;
        for(int i=0;i<lakes.Length;i++)for(int j=0;j<i;j++)separation=Math.Min(separation,(lakes[i].Center-lakes[j].Center).Length*radius/(lakes[i].Radius+lakes[j].Radius));
        Console.WriteLine("Closest independent pond centre separation / combined radius "+separation.ToString("F5"));
        Check(separation>1.9,"Independent pond water planes and bed collars cannot overlap");
        double boundaryRadius=SmallWaterBodies.GridM*Math.Round(radius/SmallWaterBodies.GridM);
        var boundaryBasins=new SmallWaterBodies(73421,boundaryRadius);
        var boundaryLakes=boundaryBasins.Candidates(center,6000).Select(p=>boundaryBasins.At(p,Flat)).Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
        double boundarySeparation=double.PositiveInfinity;
        for(int i=0;i<boundaryLakes.Length;i++)for(int j=0;j<i;j++)boundarySeparation=Math.Min(boundarySeparation,(boundaryLakes[i].Center-boundaryLakes[j].Center).Length*boundaryRadius/(boundaryLakes[i].Radius+boundaryLakes[j].Radius));
        Console.WriteLine("Planet-shell grid-boundary pond separation "+boundarySeparation.ToString("F5"));
        Check(boundarySeparation>1.9,"Planet shell addressing cannot produce coincident independent pond basins");
        foreach(var lake in lakes)
        {
            Check(SmallWaterBodies.IsLocal(lake.Id),"Local pond identity is distinct from source lakes");
            Check(lake.Level<100&&lake.Bed(100,0)<lake.Level-1,"Water stands inside an actual cut basin");
            Check(lake.Bed(100,1.5)==100,"Local pond preserves terrain outside its collar");
            Check(lake.Radius>=12&&lake.Radius<=90,"Ponds have bounded physical dimensions");
            int before=probes;var same=basins.At(lake.Center,Flat);Check(same?.Id==lake.Id&&before==probes,"Warm pond query reuses the accepted survey");
            var dry=new SmallWaterBodies(73421,radius);
            Check(dry.At(lake.Center,p=>new SmallWaterGround(100,double.NaN,LocalWaterKind.None,default))==null,"Arid ground does not acquire permanent random water");
            var river=new SmallWaterBodies(73421,radius);
            Check(river.At(lake.Center,p=>new SmallWaterGround(100,101,LocalWaterKind.River,wet))==null,"Existing channels exclude independent pond planes");
            var slope=new SmallWaterBodies(73421,radius);
            Check(slope.At(lake.Center,p=>new SmallWaterGround(100+(p.Y-lake.Center.Y)*radius*.15,double.NaN,LocalWaterKind.None,wet))==null,"Steep slopes fail the complete rim survey");
            var quality=WaterQuality.From(new LocalTerrainSample{WaterKind=LocalWaterKind.Lake,WaterBodyKey=lake.Id},new HabitatCell{SalinityMilliPpt=35000},35);
            Check(quality.Fresh,"A freshwater pond never inherits its drainage ocean salinity");
        }
        // Same canonical point, fresh context, reverse visit order.
        var reverse=new SmallWaterBodies(73421,radius);
        foreach(var lake in lakes.Reverse())Check(reverse.At(lake.Center,Flat)?.Id==lake.Id,"Pond results do not depend on visit order");
        var pools=new SmallWaterBodies(73421^0x31b47,radius,true,basins);
        var shallow=pools.Candidates(center,1600).Select(p=>pools.At(p,Flat)).Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
        Check(shallow.Length>5,"Saturated lowlands contain a finer shallow-pool mosaic, not just rare deep lakes");
        foreach(var pool in shallow)
        {
            Check(pool.Depth>=.18&&pool.Depth<=.85,"Wetland pools stay shallow");
            Check(pool.Bed(100,0)<pool.Level&&pool.Level<100,"Shallow pools have actual depressed beds");
            foreach(var lake in lakes)Check((pool.Center-lake.Center).Length*radius>(pool.Radius+lake.Radius)*1.9,"Fine wetland and deep pond collars do not intersect");
        }
        for(int i=0;i<shallow.Length;i++)for(int j=0;j<i;j++)
            Check((shallow[i].Center-shallow[j].Center).Length*radius>(shallow[i].Radius+shallow[j].Radius)*1.9,"Fine wetland pools cannot overlap each other");
        var marshCover=new GroundCoverProfile{Grasses=new V(.3,.3,.3),Stature=1};
        var marshMid=new MidCoverProfile();var marshForest=new ForestCoverProfile{Coverage=.65};var marshBiome=Biome.TemperateForest;
        LandscapeProvince.Cover(wet,new V(1000,2000,3000),41,ref marshCover,ref marshMid,ref marshForest,ref marshBiome);
        Check(marshBiome==Biome.Marsh&&marshForest.Coverage<.02,"Upland material kernels cannot cover an open saturated marsh with trees");
        Check(marshCover.Weight(3)>.3&&marshCover.Weight(0)<.04,"Open marsh ground favours sedges instead of upland lawn");
        var woodedControl=wet;woodedControl.Canopy=.7f;var woodedCover=marshCover;var woodedMid=marshMid;
        var woodedForest=new ForestCoverProfile{Coverage=.7};var woodedBiome=Biome.Marsh;
        LandscapeProvince.Cover(woodedControl,new V(1000,2000,3000),41,ref woodedCover,ref woodedMid,ref woodedForest,ref woodedBiome);
        Check(woodedForest.Coverage>.65,"The open-marsh exclusion must preserve a supported wooded-wetland canopy");
        var reed=new MidPlantSpecies{Form=MidGrowthForm.ReedStand,HeightM=2};
        var water=new LocalTerrainSample{ElevationM=10,WaterSurfaceM=10.3,WaterMaskM=.3,WaterKind=LocalWaterKind.Lake};
        Check(MidFlora.WaterAdmission(reed,water)>.9,"Reeds can grow rooted in shallow standing water");
        Check(MidFlora.WaterAdmission(new MidPlantSpecies{Form=MidGrowthForm.LeafyBush,HeightM=2},water)==0,"Ordinary shrubs do not grow submerged alongside reeds");
        water.WaterSurfaceM=12;Check(MidFlora.WaterAdmission(reed,water)==0,"Deep water excludes emergent reeds");
        water.WaterSurfaceM=10.3;water.WaterSpeedMPerS=2;Check(MidFlora.WaterAdmission(reed,water)==0,"Fast flowing river channel excludes still-water reed colonies");
        double largest=0;for(int x=-200;x<200;x++)
        {
            var p=new V(x,1200,2700);double a=LandscapeProvince.HeightOffset(p,73421,dunes),b=LandscapeProvince.HeightOffset(p+new V(.01,0,0),73421,dunes);
            Check(double.IsFinite(a)&&a>=0&&a<30,"Finite bounded sand relief");largest=Math.Max(largest,Math.Abs(a-b)/.01);
        }
        Check(largest<.8,"Dune slopes remain walkable rather than vertical oscillations");
        bool bareDune=false;for(int i=0;i<100;i++)
        {
            var p=new V(i*800,1200,2700);if(LandscapeProvince.SandSea(p,73421,dunes)<.35)continue;
            var cover=new GroundCoverProfile{Grasses=new V(.3,.3,.3)};var mid=new MidCoverProfile{Coverage=.5};var forest=new ForestCoverProfile{Coverage=.9};var biome=Biome.Grassland;
            LandscapeProvince.Cover(dunes,p,73421,ref cover,ref mid,ref forest,ref biome);
            Check(cover.Coverage<.08&&forest.Coverage<.08&&biome==Biome.Desert,"Neighbouring grassland material kernels cannot turn mobile dunes into lawns");bareDune=true;break;
        }
        Check(bareDune,"Fixture exercises an actual mobile sand province");
        foreach(int sign in new[]{-1,1})
        {
            V pole=new V(.73,.41,.54).Unit*sign,tangent=V.Cross(pole,new V(0,0,1)).Unit;int selected=-1;
            var available=new LandscapeControls{Sand=1};
            for(int s=0;s<256;s++)if(LandscapeProvince.SandSea(pole*radius,s,available)>.8){selected=s;break;}
            Check(selected>=0,"Directional test locates a true sand accumulation province");
            double low=double.MaxValue,high=double.MinValue;
            for(int x=-400;x<=400;x+=8){var p=(pole+tangent*(x/radius)).Unit*radius;double h=LandscapeProvince.HeightOffset(p,selected,available);low=Math.Min(low,h);high=Math.Max(high,h);}
            Check(high-low>14,"Dune relief does not disappear where the first wave direction is radial");
        }
        Console.WriteLine("PASS "+count+" landscape controls / enclosed pond / query-order / slope checks; "+lakes.Length+" wetland ponds; "+probes+" surveys");
    }
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);var timer=Stopwatch.StartNew();
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);Console.WriteLine("Terrain prepared "+timer.Elapsed.TotalSeconds.ToString("F3")+"s");
        var profile=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]).Select(i=>(Cell:i,P:terrain.Profile(i))).ToArray();
        foreach(var p in profile.GroupBy(x=>LandscapeProvince.Region(x.P.Landscape,world.Surface.Cells[x.Cell].Position*(terrain.RadiusM*.1),world.Recipe.Seed,x.P.Biome)).OrderBy(g=>g.Key))Console.WriteLine(p.Key+" "+p.Count());
        bool deepValidated=false,shallowValidated=false;
        foreach(var candidate in profile.OrderByDescending(x=>x.P.Landscape.Wet+x.P.Landscape.Glacial*.7).Take(24))
        {
            var at=world.Surface.Cells[candidate.Cell].Position;var lakes=terrain.Amplification.SmallLakesNear(at,4000);
            Console.WriteLine("Pond search "+candidate.Cell+" wet "+candidate.P.Landscape.Wet+" found "+lakes.Length+" shallow "+lakes.Count(l=>l.Depth<1));
            if(lakes.Length==0)continue;
            File.WriteAllText(Path.Combine(output,"ponds.json"),WorldJson.Write(lakes));
            foreach(var lake in lakes.Where(l=>l.Depth>=1).Take(2).Concat(lakes.Where(l=>l.Depth<1).Take(2)))
            {
                var patch=terrain.Patch(lake.Center,2000);var s=patch.Sample(lake.Center);
                if(!s.Underwater||s.WaterBodyKey!=lake.Id||!double.IsFinite(s.ElevationM)||s.WaterSurfaceM-s.ElevationM<Math.Min(.9,lake.Depth*.9))throw new Exception("Actual pond centre does not contain its modelled water");
                var frame=new LocalTerrainFrame(lake.Center,terrain.RadiusM,0,.1);
                var left=LocalTerrainMesh.Build(terrain,frame,-128,-64,128,32);
                var right=LocalTerrainMesh.Build(terrain,frame,0,-64,128,32);
                for(int z=0;z<=32;z++)if(left.Positions[(z*33+32)*3+1]!=right.Positions[z*33*3+1])throw new Exception("Pond terrain differs at a shared chunk edge");
                foreach(var mesh in new[]{left,right})
                {
                    if(mesh.WaterTriangles.Length==0)throw new Exception("Actual pond has no renderable water triangles");
                    for(int i=0;i<mesh.WaterPositions.Length;i+=3)
                    {
                        var point=frame.Position(mesh.EastM+mesh.WaterPositions[i],mesh.NorthM+mesh.WaterPositions[i+2]);
                        if(!lakes.Any(body=>body.NormalizedRadius(point,frame.RadiusM)<=1.15))
                            throw new Exception("Pond water mesh escaped every surveyed enclosed footprint");
                    }
                }
                if(lake.Depth>=1)deepValidated=true;else shallowValidated=true;
            }
            if(deepValidated&&shallowValidated){Console.WriteLine("PASS actual terrain contains enclosed local ponds and shallow wetland pools");return;}
        }
        throw new Exception("No actual wetland ponds found in the selected physical terrain contexts");
    }
    public static void Benchmark(string path,string baselineDirectory)
    {
        var reference=WorldFile.ReadFile(path);
        var sites=new[]{Biome.Desert,Biome.Marsh,Biome.TemperateForest}.Select(b=>Enumerable.Range(0,reference.Surface.Cells.Length).First(i=>reference.Ecology.Habitat[i].Biome==b&&reference.ElevationM[i]>10)).Select(i=>reference.Surface.Cells[i].Position).ToArray();
        object Probe(string directory)
        {
            var context=new System.Runtime.Loader.AssemblyLoadContext("landscape-cost-"+Guid.NewGuid(),true);
            context.Resolving+=(c,n)=>{string file=Path.Combine(directory,n.Name+".dll");return File.Exists(file)?c.LoadFromAssemblyPath(Path.GetFullPath(file)):null;};
            var core=context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(directory,"WorldGen.Core.dll")));
            var serialization=context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(directory,"WorldGen.Serialization.dll")));
            var read=serialization.GetType("WorldGen.Serialization.WorldFile").GetMethods().First(m=>m.Name=="ReadFile"&&m.GetParameters()[0].ParameterType==typeof(string));
            var input=read.GetParameters().Select((p,i)=>i==0?(object)path:p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
            object world=read.Invoke(null,input);var terrainType=core.GetType("WorldGen.Core.LocalTerrain");var ctor=terrainType.GetConstructors().Single();
            var values=ctor.GetParameters().Select((p,i)=>i==0?world:p.ParameterType==typeof(double)?(object).1:p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
            var timer=Stopwatch.StartNew();object terrain=ctor.Invoke(values);double preparation=timer.Elapsed.TotalMilliseconds;
            var v=core.GetType("WorldGen.Core.V");var patches=sites.Select(p=>terrainType.GetMethod("Patch").Invoke(terrain,new[]{Activator.CreateInstance(v,p.X,p.Y,p.Z),(object)10000d})).ToArray();
            var sample=patches[0].GetType().GetMethod("Sample",new[]{typeof(double),typeof(double)});double[] costs=new double[3];double sum=0;
            for(int pass=0;pass<3;pass++)
            {
                timer.Restart();foreach(var patch in patches)for(int z=0;z<65;z++)for(int x=0;x<65;x++){var s=sample.Invoke(patch,new object[]{5000+(x-32)*80d,(z-32)*80d});sum+=(double)s.GetType().GetField("ElevationM").GetValue(s);}costs[pass]=timer.Elapsed.TotalMilliseconds;
            }
            var result=new{Directory=directory,PreparationMs=preparation,LandSample12675Ms=costs,HeightSum=sum};context.Unload();return result;
        }
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(Probe(baselineDirectory)));GC.Collect();GC.WaitForPendingFinalizers();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(Probe(Path.GetDirectoryName(typeof(LocalTerrain).Assembly.Location))));
    }
}

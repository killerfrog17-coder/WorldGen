using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WetlandComplexTests
{
    public static void Controls()
    {
        int checks=0,wetCount=0,dryCount=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        const int seed=73421;const double radius=637100;
        var wet=new LandscapeControls{Wet=1,Moisture=1,Canopy=.7f};
        SmallWaterGround Flat(V p)=>new SmallWaterGround(100,0,LocalWaterKind.None,wet);
        var centres=new[]{new V(1,0,0),new V(0,1,0),new V(0,0,1),new V(-.73,.41,.54).Unit};
        var all=new List<SmallLake>();
        foreach(var at in centres)
        {
            var bodies=new SmallWaterBodies(seed,radius,complexRegion:p=>true);
            var lakes=bodies.Candidates(at,4500).Select(p=>bodies.At(p,Flat)).Where(p=>p!=null).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
            Check(lakes.Length>5,"Wet terrain must admit actual finite complexes at axes/poles");
            for(int i=0;i<lakes.Length;i++)for(int j=0;j<i;j++)
                Check((lakes[i].Center-lakes[j].Center).Length*radius>(lakes[i].Radius+lakes[j].Radius)*1.9,"Complexes cannot overlap each other's bounded bed collars");
            var fresh=new SmallWaterBodies(seed,radius,complexRegion:p=>true);
            foreach(var lake in lakes.Reverse())
            {
                Check(lake.Complex&&lake.Radius>=120&&lake.Radius<=280&&lake.Depth>=.25&&lake.Depth<=.75,"Physical wetland extent/depth bounds");
                Check(fresh.At(lake.Center,Flat)?.Id==lake.Id,"Reverse cold visitation retains the same complex identity");
                for(int angle=0;angle<48;angle++)foreach(double q in new[]{.2,.8,1.05,1.29,1.31,1.5})
                {
                    double a=angle*Math.PI/24;
                    // Search the true radius, including irregular outline and
                    // heading/aspect, rather than assuming a circular basin.
                    V direction=lake.East*Math.Cos(a)+lake.North*Math.Sin(a);
                    double lo=0,hi=lake.Radius*3;
                    for(int k=0;k<30;k++){double m=(lo+hi)*.5;var p=(lake.Center+direction*(m/radius)).Unit;if(lake.NormalizedRadius(p,radius)<q)lo=m;else hi=m;}
                    V point=(lake.Center+direction*((lo+hi)*.5/radius)).Unit;
                    double r=lake.NormalizedRadius(point,radius),bed=lake.Bed(100,r,point*radius);
                    Check(double.IsFinite(bed)&&bed<=100&&bed>=lake.Level-lake.Depth-1e-9,"Finite cut-only bed and bounded standing depth");
                    if(q<1.29)Check(bodies.At(point,Flat)?.Id==lake.Id,"27-cell address lookup must discover the entire physical bed, including grid/sphere boundaries");
                    if(q>=1.31)Check(bed==100,"Outer collar is exactly unchanged and dry");
                    if(q<.8){if(bed<lake.Level)wetCount++;else dryCount++;}
                }
            }
            all.AddRange(lakes);
        }
        Check(wetCount>100&&dryCount>100,"Complexes must contain both shallow water and emergent dry hummocks");
        var ordinary=new SmallWaterBodies(seed,radius);
        var disabled=new SmallWaterBodies(seed,radius,complexRegion:p=>false);
        foreach(var at in ordinary.Candidates(centres[0],3000))
        {
            var a=ordinary.At(at,Flat);var b=disabled.At(at,Flat);
            Check((a==null)==(b==null),"Dry-region classifier leaves ordinary pond acceptance unchanged");
            if(a!=null)Check(a.Id==b.Id&&a.Radius==b.Radius&&a.Level==b.Level&&!b.Complex,"Ordinary pond dimensions/datum/identity unchanged");
        }
        var excluded=new SmallWaterBodies(seed,radius,complexRegion:p=>true);
        var probe=all[0].Center;
        Check(excluded.At(probe,p=>new SmallWaterGround(100,0,LocalWaterKind.None,default))==null,"Dry region cannot acquire a wetland complex");
        var slope=new SmallWaterBodies(seed,radius,complexRegion:p=>true);
        Check(slope.At(probe,p=>new SmallWaterGround(100+(p.Y-probe.Y)*radius*.2,0,LocalWaterKind.None,wet))==null,"A steep slope cannot acquire a floating independent wetland plane");
        int shoreWet=0,shoreDry=0;
        for(int x=-80;x<=80;x++)for(int z=-20;z<=20;z++)
        {
            V p=new V(x*1.7,800,z*1.7);double original=100.25;
            double bed=WetlandMicrorelief.Bed(original,100,wet,p,seed);
            Check(bed<=original&&bed>=original-.85,"Existing standing-water shore has bounded cut-only microrelief");
            if(bed<100)shoreWet++;else shoreDry++;
            double adjacent=WetlandMicrorelief.Bed(original,100,wet,p+new V(.001,0,0),seed);
            Check(Math.Abs(adjacent-bed)<.001,"Shore microrelief is continuous in physical space");
            Check(WetlandMicrorelief.Bed(102,100,wet,p,seed)==102,"Higher shore remains exactly unchanged");
            Check(WetlandMicrorelief.Bed(original,100,default,p,seed)==original,"Dry-region lake shores remain exactly unchanged");
        }
        Check(shoreWet>100&&shoreDry>100,"Existing lake fringes support both flooded hollows and retained hummocks");
        var reed=new MidPlantSpecies{Form=MidGrowthForm.ReedStand,HeightM=2,Moisture=1.2,Shade=.1,MinWarmC=0,MaxWarmC=45,MinPH=5,MaxPH=8.5,MaxSalinity=.1,FloodTolerance=1};
        var soil=new GroundProfile{DepthM=1,PH=7,WaterCapacityMm=100,Fertility=.7,Gravel=.1};
        var habitat=new HabitatCell{AridityMilli=800,WaterTableCm=300};
        double dryFit=MidFlora.Suitability(reed,habitat,soil,Biome.Marsh,20,.1);
        double wetFit=MidFlora.Suitability(reed,habitat,soil,Biome.Marsh,20,.1,0,.9);
        Check(dryFit==0&&wetFit>.1,"Locally saturated shores support reeds even when the broad habitat water table is deeper");
        Check(MidFlora.Suitability(reed,habitat,soil,Biome.Marsh,20,.1,0,0)==dryFit,"Default upland flora fit is unchanged");
        soil.Salinity=.2;Check(MidFlora.Suitability(reed,habitat,soil,Biome.Marsh,20,.1,0,.9)==0,"Saturated freshwater shores do not bypass species salt limits");
        soil.Salinity=0;soil.PH=3;Check(MidFlora.Suitability(reed,habitat,soil,Biome.Peatland,20,.1,0,.9)==0,"Acid bogs do not acquire unsuitable generic tall reeds");
        var wetRock=new LandscapeControls{Wet=1,Peat=1,GlacialExposure=1};
        Check(LandscapeProvince.Ground(new GroundProfile{DepthM=1,Organic=.5},wetRock,new V(800,900,700),seed).Organic==0,"Peat and wetland organic floors cannot recolonize an explicitly bare rock wall");
        wetRock.GlacialExposure=0;
        Check(LandscapeProvince.Ground(new GroundProfile{DepthM=1,Organic=.5},wetRock,new V(800,900,700),seed).Organic>=.45,"Wetland peat floor remains on soil-bearing ground");
        Console.WriteLine($"PASS {checks} wetland containment/discovery/shape checks; {all.Count} complexes, {wetCount} wet and {dryCount} dry interior probes");
    }

    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        // Invoke the survey's exact pre-local-water sampler. PreWaterElevationM
        // predates the sub-metre beauty pass too, so it is not a valid reference
        // for measuring this later basin cut.
        var contextType=typeof(HyperAmplification).GetNestedType("Patch",System.Reflection.BindingFlags.NonPublic);
        var context=Activator.CreateInstance(contextType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new object[]{terrain.Amplification,Array.Empty<int>()},null);
        var beforeWater=(Func<V,SmallWaterGround>)Delegate.CreateDelegate(typeof(Func<V,SmallWaterGround>),context,contextType.GetMethod("SmallWaterGroundAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic));
        int checks=0,wet=0,dry=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var sites=new[]{new V(.004921601,-.464940574,-.88532821).Unit,new V(.032332564,-.451417,-.891727143).Unit};
        var bodies=new Dictionary<ulong,SmallLake>();
        var further=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>terrain.Profile(i).Landscape.Wet>.8&&world.Ecology.Habitat[i].WarmTenthsC>80&&!world.Hydrology.OceanConnected[i])
            .OrderBy(i=>Rng.Mix(unchecked((uint)i^0x32175u))).Take(12).Select(i=>world.Surface.Cells[i].Position);
        foreach(var site in sites.Concat(further))
        {
            var nearby=terrain.Amplification.SmallLakesNear(site,4000);
            Console.WriteLine($"Wetland site {site.X:F6},{site.Y:F6},{site.Z:F6}: {nearby.Length} bodies / {nearby.Count(p=>p.Complex)} complexes");
            foreach(var body in nearby.Where(p=>p.Complex))bodies[body.Id]=body;
            if(bodies.Count>=4)break;
        }
        Check(bodies.Count>=2,"Actual wet provinces must support multiple enclosed complexes");
        var rows=new List<object>();
        foreach(var body in bodies.Values.Take(4))
        {
            var patch=terrain.Patch(body.Center,3000);patch.RiverSpans();
            V wetPoint=default;int localWet=0,localDry=0;
            for(int z=-6;z<=6;z++)for(int x=-6;x<=6;x++)
            {
                V p=(body.Center+body.East*(x*body.Radius/8/(terrain.RadiusM*.1))+body.North*(z*body.Radius/8/(terrain.RadiusM*.1))).Unit;
                var s=patch.Sample(p);if(body.NormalizedRadius(p,terrain.RadiusM*.1)>.75)continue;
                Check(s.WaterKind==LocalWaterKind.Lake&&s.WaterBodyKey==body.Id,"Water and hummocks retain their one finite owner");
                var original=beforeWater(p);
                Check(s.WaterSurfaceM==body.Level,"Shared exact constant water head");
                Check(s.ElevationM<=original.Height+.001&&original.Height-s.ElevationM<=1.201,"Bounded actual cut-only ground");
                if(s.Underwater){wet++;localWet++;wetPoint=p;Check(s.WaterSurfaceM-s.ElevationM<=Math.Max(body.Depth,body.Level-original.Height)+.002,"Actual depth stays shallow unless inherited ground is already deeper");}
                else{dry++;localDry++;}
            }
            Check(localWet>0&&localDry>0,"Each sampled actual complex must have wet hollows and emergent hummocks");
            var frame=new LocalTerrainFrame(wetPoint,terrain.RadiusM,0,.1);
            var left=LocalTerrainMesh.Build(terrain,frame,-100,-50,100,32);
            var right=LocalTerrainMesh.Build(terrain,frame,0,-50,100,32);
            for(int z=0;z<=32;z++)Check(left.Positions[(z*33+32)*3+1]==right.Positions[z*33*3+1],"Exact shared terrain edges through wetland hollows");
            foreach(var mesh in new[]{left,right})
            {
                Check(mesh.WaterTriangles.Length>0&&mesh.WaterPositions.All(float.IsFinite),"Actual wetland produces finite water geometry");
                foreach(int i in mesh.WaterTriangles)Check(i>=0&&i<mesh.WaterPositions.Length/3,"Valid bounded water topology");
            }
            rows.Add(new{body.Id,body.Radius,body.Level,body.Depth,Center=new[]{body.Center.X,body.Center.Y,body.Center.Z},WetPoint=new[]{wetPoint.X,wetPoint.Y,wetPoint.Z},Wet=localWet,Dry=localDry});
        }
        var shoreAt=sites[0];var shorePatch=terrain.Patch(shoreAt,10000);shorePatch.RiverSpans();
        var shoreFrame=new LocalTerrainFrame(shoreAt,terrain.RadiusM,0,.1);int shoreWet=0,shoreDry=0;
        const double existingHead=131.90736925395524;
        var knownShore=shorePatch.Sample(shoreAt);
        Check(knownShore.WaterKind==LocalWaterKind.Lake&&knownShore.WaterSurfaceM==existingHead,"Known wetland pond retains its previous accepted head");
        for(int z=-10;z<=10;z++)for(int x=-10;x<=10;x++)
        {
            var s=shorePatch.Sample(shoreFrame.Position(x*6,z*6));
            if(s.WaterBodyKey!=knownShore.WaterBodyKey)continue;
            Check(s.WaterKind==LocalWaterKind.Lake&&s.WaterSurfaceM==existingHead,"One finite shore retains its accepted constant head/ownership");
            if(s.Underwater)shoreWet++;else shoreDry++;
        }
        Check(shoreWet>10&&shoreDry>10,"Actual wooded lake fringe contains inundated hollows and dry ground");
        var scatter=new LocalScatter(terrain,.1);var vegetation=scatter.Build(scatter.Address(shoreAt));int trees=0;
        foreach(var tree in vegetation.Instances.Where(i=>i.Kind==ScatterKind.GeneratedTree))
        {
            var s=terrain.Patch(tree.Position,1000).Sample(tree.Position);trees++;
            Check(!s.Underwater&&tree.GroundM==s.ElevationM,"Actual generated trees root on retained dry terrain, never in flooded hollows");
        }
        Check(trees>0,"Actual wooded shore exercises the production tree-placement path");
        var shoreLeft=LocalTerrainMesh.Build(terrain,shoreFrame,-100,-50,100,32);
        var shoreRight=LocalTerrainMesh.Build(terrain,shoreFrame,0,-50,100,32);
        for(int z=0;z<=32;z++)Check(shoreLeft.Positions[(z*33+32)*3+1]==shoreRight.Positions[z*33*3+1],"Shared chunk edges remain EXACT across standing-water shore hollows");
        rows.Add(new{Kind="Existing lake fringe",Head=existingHead,Wet=shoreWet,Dry=shoreDry,DryRootedTrees=trees});
        Check(world.Prehistory.Hash==history,"Wetland generation never mutates history");
        File.WriteAllText(Path.Combine(output,"complexes.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} actual wetland water/terrain/edge checks; {bodies.Count} admitted complexes, {wet} wet and {dry} dry interior probes");
    }
}

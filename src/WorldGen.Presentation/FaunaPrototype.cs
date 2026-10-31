using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class FaunaPrototypeCatalogue
    {public int version;public FaunaPrototypeProfile[] animals=Array.Empty<FaunaPrototypeProfile>();}
    [Serializable] public sealed class FaunaPrototypeProfile
    {
        public string slug="",name="",medium="ground",bundle="";
        public string[] aliases=Array.Empty<string>();
        public double lengthM,walkMps,runMps,radiusM,densityPerKm2;
        public bool tiny,derivedFirefly,freshwater,nocturnal;
        [NonSerialized] double sourceWalkMps,sourceRunMps;
        public void PrepareLocomotion()
        {
            if(sourceRunMps>0)return;
            sourceWalkMps=walkMps;sourceRunMps=runMps;
            if(medium=="ground"&&WildlifeLocomotion.TryGround(slug,out var pace)){walkMps=pace.WalkMps;runMps=pace.BurstMps;}
        }
        public double ClipSpeed(string role)
        {
            double walk=sourceWalkMps>0?sourceWalkMps:walkMps,run=sourceRunMps>0?sourceRunMps:runMps;
            return role=="run"?run:role=="trot"?(walk+run)*.5:walk;
        }
        public double VisibleRadiusM=>slug=="butterflies-and-moths-guild"||derivedFirefly?Math.Max(radiusM,220):tiny?radiusM:
            medium=="air"?Math.Max(radiusM,lengthM>=.8?1200:650):
            medium=="water"&&lengthM>=1.5?Math.Max(radiusM,lengthM>=8?1600:650):
            medium=="ground"&&lengthM>=1?Math.Max(radiusM,lengthM>=3?1600:1100):
            medium=="ground"&&lengthM>=.5?Math.Max(radiusM,300):radiusM;
        public double PreloadRadiusM=>VisibleRadiusM+(tiny?10:Math.Min(250,Math.Max(70,VisibleRadiusM*.2)));
    }
    public sealed class FaunaPrototypeSpawn
    {
        public ulong Id,GroupId;public int GroupIndex,GroupSize;public uint Hash;public V Position,GroupHome;
        public FaunaPrototypeProfile Profile=null!;
        public int Region,Taxon,CoatSeed;public double GroundM,WaterM=double.NaN,HeightM,ColdC,Aridity;
    }
    /// <summary>Native regional population candidates. Runtime may retain large
    /// individuals/groups; this planner never mutates the ecology census.</summary>
    public sealed class FaunaPrototypePlanner
    {
        readonly World world;readonly LocalTerrain terrain;readonly LocalScatter lattice;
        readonly Dictionary<int,FaunaPrototypeProfile> models=new Dictionary<int,FaunaPrototypeProfile>();
        readonly Dictionary<int,List<int>> pools=new Dictionary<int,List<int>>();
        readonly Dictionary<int,Taxon[]> communities=new Dictionary<int,Taxon[]>();
        readonly FaunaPrototypeProfile? firefly;
        public int MappedTaxa=>models.Count;
        static string Key(string value)=>new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        public static ulong Identity(ScatterKey key,int taxon,int group,int member)
        {
            ulong value=key.Id(member<0?14:15,0)^((ulong)(uint)(taxon+9127)*0x9e3779b97f4a7c15UL)^((ulong)(uint)group*0xd1b54a32d192ed03UL)^((ulong)(uint)(member+1)*0x94d049bb133111ebUL);
            value=(value^(value>>30))*0xbf58476d1ce4e5b9UL;value=(value^(value>>27))*0x94d049bb133111ebUL;value=(value^(value>>31))&0x1fffffffffffffffUL;return value==0?1:value;
        }
        public FaunaPrototypePlanner(World world,LocalTerrain terrain,FaunaPrototypeProfile[] profiles,double scale)
        {
            this.world=world;this.terrain=terrain;lattice=new LocalScatter(terrain,scale);
            foreach(var profile in profiles)
            {
                if(profile.derivedFirefly)firefly=profile;
            }
            if(world.Ecology==null)return;
            models=BindProfiles(world.Ecology.Taxa,profiles);
        }
        public static Dictionary<int,FaunaPrototypeProfile> BindProfiles(Taxon[] taxa,FaunaPrototypeProfile[] profiles)
        {
            var result=new Dictionary<int,FaunaPrototypeProfile>();var aliases=new Dictionary<string,FaunaPrototypeProfile>();
            foreach(var profile in profiles)foreach(var alias in profile.aliases)aliases[Key(alias)]=profile;
            foreach(var taxon in taxa)
                if(!taxon.Producer&&!taxon.BasalResource&&(aliases.TryGetValue(Key(taxon.Name),out var profile)||aliases.TryGetValue(Key(taxon.ScientificName),out profile)))result[taxon.Id]=profile;
            return result;
        }
        List<int> Pool(int region)
        {
            if(pools.TryGetValue(region,out var found))return found;
            found=new List<int>();var e=world.Ecology;
            if(region>=0&&region<e.Regions.Length)foreach(var presence in e.Regions[region].Species)if(models.ContainsKey(presence.TaxonId))found.Add(presence.TaxonId);
            pools[region]=found;return found;
        }
        Taxon[] Community(int region)
        {
            if(communities.TryGetValue(region,out var found))return found;
            var e=world.Ecology;found=region>=0&&region<e.Regions.Length?e.Regions[region].Species
                .Select(p=>e.Taxa[p.TaxonId]).Where(t=>!t.Producer&&!t.BasalResource).ToArray():Array.Empty<Taxon>();
            communities[region]=found;return found;
        }
        public List<FaunaPrototypeSpawn> Build(V center,double scale,double hour,CancellationToken token,double year=0,double temperatureC=15,double humidity=.7,double rain=0,double wind=0)
        {
            var result=new List<FaunaPrototypeSpawn>();var e=world.Ecology;if(e==null)return result;
            var patch=terrain.Patch(center,3200/scale);double radius=terrain.RadiusM*scale;
            var communityCache=new Dictionary<(ScatterKey,int),WildlifeCommunity>();
            var siteCache=new Dictionary<ScatterKey,WildlifeHabitat>();
            WildlifeHabitat Site(V point)
            {
                var sample=patch.Sample(point);var climate=e.Habitat[sample.Cell];
                double canopy=TreeFlora.CanopyShade(sample.Forest,point,terrain.SourceSeed,radius);
                return WildlifeHabitat.From(sample,climate,canopy,0,temperatureC);
            }
            WildlifeCommunity Population(ScatterKey key,int region)
            {
                if(communityCache.TryGetValue((key,region),out var found))return found;
                if(!siteCache.TryGetValue(key,out var site))
                {
                    site=new WildlifeHabitat{Biome=patch.Sample(lattice.Position(key)).Biome};
                    site.Add(Site(lattice.Position(key)),.2);
                    foreach(double u in new[]{.2,.8})foreach(double v in new[]{.2,.8})site.Add(Site(lattice.Position(key,u,v)),.2);
                    siteCache[key]=site;
                }
                found=WildlifeAbundance.Allocate(Community(region),site);communityCache[(key,region)]=found;return found;
            }
            double latitude=Math.Asin(center.Z)*180/Math.PI;
            foreach(bool detail in new[]{false,true})foreach(var key in lattice.Around(center,detail?220:2400,detail))
            {
                token.ThrowIfCancellationRequested();V middle=lattice.Position(key);int cell=terrain.NearestCell(middle),region=e.Region[cell];
                var candidates=new List<int>(Pool(region));
                // Marine feeding pools are recorded on coastal land regions.
                // Consult bounded neighboring cells, never a global coast scan.
                if(world.Hydrology.OceanConnected[cell])foreach(int neighbor in world.Surface.Cells[cell].Neighbors)
                    foreach(int id in Pool(e.Region[neighbor]))if(!candidates.Contains(id))candidates.Add(id);
                foreach(int id in candidates)
                {
                    var profile=models[id];var behaviour=WildlifeBehaviour.Describe(profile.slug,e.Taxa[id].MassKg,e.Taxa[id].Eats,profile.medium=="air",profile.tiny,e.Taxa[id].Name);
                    if(detail!=(behaviour.Tier<WildlifeTrackingTier.Resident&&profile.VisibleRadiusM<250))continue;
                    if((middle-center).Length*radius>Math.Max(profile.PreloadRadiusM,behaviour.TrackingM)+(detail?100:800))continue;
                    var taxon=e.Taxa[id];int nativeRegion=region;
                    if(!Pool(nativeRegion).Contains(id))
                    {nativeRegion=-1;foreach(int neighbor in world.Surface.Cells[cell].Neighbors)if(Pool(e.Region[neighbor]).Contains(id)){nativeRegion=e.Region[neighbor];break;}}
                    if(nativeRegion<0)continue;
                    Add(profile,taxon,id,nativeRegion,key);
                }
                if(detail&&firefly!=null&&region>=0&&(hour>=17.5||hour<6))
                    Add(firefly,null,-1,region,key);
            }
            // Ecology supplies population; admission is sliced across frames,
            // instead of discarding members to fit a fixed entity count.
            return result.OrderBy(s=>(s.Position-center).Length/Math.Max(1,s.Profile.VisibleRadiusM)).ThenBy(s=>s.Id).ToList();
            void Add(FaunaPrototypeProfile profile,Taxon? taxon,int id,int region,ScatterKey key)
            {
                bool daylightNocturnal=!profile.derivedFirefly&&WildlifeBehaviour.IsNocturnal(profile.slug,profile.nocturnal)&&hour>6&&hour<18;
                if(daylightNocturnal&&profile.medium=="air")return;
                uint rank=Rng.Mix((uint)key.Id(0,0)^(uint)(key.Id(0,0)>>32)^(uint)(id+9127)*19349663u);
                var rng=new Rng(world.Recipe.Seed,"local-fauna-prototype-1",unchecked((int)rank));
                double density;
                if(taxon!=null)
                    density=Population(key,region).Density(id)*WildlifeAbundance.ExposedFraction(taxon.MassKg,profile.medium=="air",profile.tiny);
                else
                {
                    V at=lattice.Position(key);var sample=patch.Sample(at);var climate=e.Habitat[sample.Cell];
                    var lineage=AmbientFauna.For(world).Region(region).FirstOrDefault(s=>s.Family==AmbientFaunaFamily.Firefly);
                    double habitatFit=FireflyDisplay.HabitatGain(lineage,climate,sample);
                    double activity=FireflyDisplay.ActivityGain(lineage,hour,year,latitude,temperatureC,humidity,rain,wind);
                    density=Math.Max(0,profile.densityPerKm2)*habitatFit*activity*.15;
                }
                if(density<=0)return;
                if(daylightNocturnal)density*=.08;
                var habitat=e.Habitat[e.Regions[region].RepresentativeCell];
                var behaviour=WildlifeBehaviour.Describe(profile.slug,taxon?.MassKg??.001,taxon?.Eats??Food.None,profile.medium=="air",profile.tiny,taxon?.Name??"");
                if(WildlifeBehaviour.Dormant(behaviour,year,latitude,habitat.ColdTenthsC/10.0,temperatureC)||!WildlifeBehaviour.SeasonallyPresent(behaviour,year,latitude,habitat.ColdTenthsC/10.0))return;
                V a=lattice.Position(key,0,0),b=lattice.Position(key,1,0),c=lattice.Position(key,0,1);
                double area=V.Cross(b-a,c-a).Length*radius*radius;
                int groupSize=behaviour.GroupMin+(int)(rank%(uint)(behaviour.GroupMax-behaviour.GroupMin+1));
                if(groupSize==1&&!behaviour.HasNaturalHistory)groupSize=profile.tiny?3:profile.medium=="water"&&profile.lengthM<1?5:1;
                double expected=area*density/1000000/groupSize;
                int groups=(int)Math.Floor(expected)+(rng.Unit()<expected-Math.Floor(expected)?1:0);
                for(int g=0;g<groups;g++)
                {
                    double u=.08+.84*rng.Unit(),v=.08+.84*rng.Unit();
                    V groupHome=lattice.Position(key,u,v);ulong groupId=Identity(key,id,g,-1);
                    for(int member=0;member<groupSize;member++)
                    {
                        token.ThrowIfCancellationRequested();
                        double spread=(detailSize(key)?8:25)/Math.Max(64,Math.Sqrt(area));
                        V position=lattice.Position(key,Math.Max(.01,Math.Min(.99,u+(rng.Unit()-.5)*spread)),Math.Max(.01,Math.Min(.99,v+(rng.Unit()-.5)*spread)));
                        if((position-center).Length*radius>Math.Max(profile.PreloadRadiusM,behaviour.TrackingM))continue;
                        int actualCell=terrain.NearestCell(position);if(e.Region[actualCell]!=region&&!world.Hydrology.OceanConnected[actualCell])continue;
                        var sample=patch.Sample(position);bool wet=sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.03;
                        if(profile.derivedFirefly&&FireflyDisplay.HabitatGain(AmbientFauna.For(world).Region(region).First(s=>s.Family==AmbientFaunaFamily.Firefly),e.Habitat[sample.Cell],sample)<=0)continue;
                        if(taxon!=null)
                        {
                            var local=Site(position);double fit=local.Fit(taxon);
                            // Habitat filtering is deterministic per identity, not
                            // rerolled on every eight-second planner refresh.
                            uint habitatRoll=Rng.Mix((uint)Identity(key,id,g,member)^0x6c8e9cf5u);
                            if(fit<=0||habitatRoll/(double)uint.MaxValue>fit)continue;
                        }
                        double water=sample.WaterSurfaceM;if(wet&&double.IsNaN(water)&&world.Hydrology.OceanConnected[actualCell])water=0;
                        if(profile.medium=="ground"&&wet||profile.medium=="water"&&(!wet||double.IsNaN(water)||water-sample.ElevationM<Math.Max(.25,profile.lengthM*.25)))continue;
                        if(profile.medium=="water"&&profile.freshwater&&world.Hydrology.OceanConnected[actualCell])continue;
                        if(profile.medium=="water"&&!profile.freshwater&&!world.Hydrology.OceanConnected[actualCell])continue;
                        ulong ident=Identity(key,id,g,member);
                        uint hash=Rng.Mix((uint)ident^(uint)(ident>>32));
                        result.Add(new FaunaPrototypeSpawn{Id=ident,GroupId=groupId,GroupIndex=member,GroupSize=groupSize,GroupHome=groupHome,Hash=hash,Profile=profile,Position=position,Region=region,Taxon=id,
                            CoatSeed=unchecked(region*8191+(id+9127)*31),GroundM=sample.ElevationM,WaterM=water,
                            HeightM=profile.medium=="air"?(profile.derivedFirefly?FireflyHeight(region,rng.Unit()):profile.tiny?.6+1.2*rng.Unit():6+18*rng.Unit()):profile.medium=="water"?-.4-2*rng.Unit():0,
                            ColdC=habitat.ColdTenthsC/10.0,Aridity=habitat.AridityMilli/1000.0});
                    }
                }
            }
        }
        static bool detailSize(ScatterKey key)=>key.Detail;
        double FireflyHeight(int region,double unit)
        {
            var display=AmbientFauna.For(world).Region(region).First(s=>s.Family==AmbientFaunaFamily.Firefly).Display;
            return display.MinFlightHeightM+(display.MaxFlightHeightM-display.MinFlightHeightM)*unit;
        }
    }
}

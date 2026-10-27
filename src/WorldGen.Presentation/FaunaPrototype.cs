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
    }
    public sealed class FaunaPrototypeSpawn
    {
        public ulong Id;public uint Hash;public V Position;
        public FaunaPrototypeProfile Profile=null!;
        public int Region,Taxon,CoatSeed;public double GroundM,WaterM=double.NaN,HeightM,ColdC,Aridity;
    }
    /// <summary>Ephemeral local presentation population, never saved ecosystem
    /// actors. Uses recorded regional presence without changing those records.</summary>
    public sealed class FaunaPrototypePlanner
    {
        readonly World world;readonly LocalTerrain terrain;readonly LocalScatter lattice;
        readonly Dictionary<int,FaunaPrototypeProfile> models=new Dictionary<int,FaunaPrototypeProfile>();
        readonly Dictionary<int,List<int>> pools=new Dictionary<int,List<int>>();
        readonly FaunaPrototypeProfile? firefly;
        public int MappedTaxa=>models.Count;
        static string Key(string value)=>new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
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
        public List<FaunaPrototypeSpawn> Build(V center,double scale,double hour,CancellationToken token)
        {
            var result=new List<FaunaPrototypeSpawn>();var e=world.Ecology;if(e==null)return result;
            var patch=terrain.Patch(center,2000/scale);double radius=terrain.RadiusM*scale;
            foreach(bool detail in new[]{false,true})foreach(var key in lattice.Around(center,detail?220:700,detail))
            {
                token.ThrowIfCancellationRequested();V middle=lattice.Position(key);int cell=terrain.NearestCell(middle),region=e.Region[cell];
                var candidates=new List<int>(Pool(region));
                // Marine feeding pools are recorded on coastal land regions.
                // Consult bounded neighboring cells, never a global coast scan.
                if(world.Hydrology.OceanConnected[cell])foreach(int neighbor in world.Surface.Cells[cell].Neighbors)
                    foreach(int id in Pool(e.Region[neighbor]))if(!candidates.Contains(id))candidates.Add(id);
                foreach(int id in candidates)
                {
                    var profile=models[id];if(detail!=(profile.radiusM<250))continue;
                    if((middle-center).Length*radius>profile.radiusM+(detail?100:800))continue;
                    var taxon=e.Taxa[id];int nativeRegion=region;
                    if(!Pool(nativeRegion).Contains(id))
                    {nativeRegion=-1;foreach(int neighbor in world.Surface.Cells[cell].Neighbors)if(Pool(e.Region[neighbor]).Contains(id)){nativeRegion=e.Region[neighbor];break;}}
                    if(nativeRegion<0)continue;
                    Add(profile,taxon,id,nativeRegion,key);
                }
                if(detail&&firefly!=null&&region>=0&&e.Habitat[cell].WarmTenthsC>=100&&e.Habitat[cell].AridityMilli>=500&&(hour>=18||hour<6))
                    Add(firefly,null,-1,region,key);
            }
            // Ecology supplies population; admission is sliced across frames,
            // instead of discarding members to fit a fixed entity count.
            return result.OrderBy(s=>(s.Position-center).Length/Math.Max(1,s.Profile.radiusM)).ThenBy(s=>s.Id).ToList();
            void Add(FaunaPrototypeProfile profile,Taxon? taxon,int id,int region,ScatterKey key)
            {
                if(profile.nocturnal&&hour>6&&hour<18)return;
                uint rank=Rng.Mix((uint)key.Id(0,0)^(uint)(key.Id(0,0)>>32)^(uint)(id+9127)*19349663u);
                var rng=new Rng(world.Recipe.Seed,"local-fauna-prototype-1",unchecked((int)rank));
                double density=profile.densityPerKm2;
                bool meat=taxon!=null&&(taxon.Eats&Food.Vertebrate)!=0;
                bool plants=taxon!=null&&(taxon.Eats&(Food.Grass|Food.Browse|Food.Seed|Food.Fruit|Food.Nectar))!=0;
                if(density<=0)
                {
                    density=Math.Min(650,Math.Max(.025,95.5*Math.Pow(Math.Max(.01,taxon?.MassKg??1),-.75)));
                    if(meat)density*=plants?.45:.08;
                }
                else if(profile.medium=="air"&&meat&&!plants)density*=.035;
                else if(profile.medium=="water")
                {if(taxon!=null&&taxon.MassKg>15)density=Math.Max(.015,2.5*Math.Pow(taxon.MassKg,-.35));else density=profile.freshwater?4500:1500;}
                var habitat=e.Habitat[e.Regions[region].RepresentativeCell];
                double productivity=Math.Max(.12,Math.Min(1.4,habitat.ProductivityGCM2Year/500.0));density*=productivity;
                V a=lattice.Position(key,0,0),b=lattice.Position(key,1,0),c=lattice.Position(key,0,1);
                double area=V.Cross(b-a,c-a).Length*radius*radius;
                int groupSize=profile.tiny?3:profile.medium=="water"&&profile.lengthM<1?5:taxon!=null&&taxon.MassKg>=10&&!meat?3:1;
                double expected=area*density/1000000/groupSize;
                int groups=Math.Min(8,(int)Math.Floor(expected)+(rng.Unit()<expected-Math.Floor(expected)?1:0));
                for(int g=0;g<groups;g++)
                {
                    double u=.08+.84*rng.Unit(),v=.08+.84*rng.Unit();
                    for(int member=0;member<groupSize;member++)
                    {
                        token.ThrowIfCancellationRequested();
                        double spread=(detailSize(key)?8:25)/Math.Max(64,Math.Sqrt(area));
                        V position=lattice.Position(key,Math.Max(.01,Math.Min(.99,u+(rng.Unit()-.5)*spread)),Math.Max(.01,Math.Min(.99,v+(rng.Unit()-.5)*spread)));
                        if((position-center).Length*radius>profile.radiusM)continue;
                        int actualCell=terrain.NearestCell(position);if(e.Region[actualCell]!=region&&!world.Hydrology.OceanConnected[actualCell])continue;
                        var sample=patch.Sample(position);bool wet=sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.03;
                        double water=sample.WaterSurfaceM;if(wet&&double.IsNaN(water)&&world.Hydrology.OceanConnected[actualCell])water=0;
                        if(profile.medium=="ground"&&wet||profile.medium=="water"&&(!wet||double.IsNaN(water)||water-sample.ElevationM<Math.Max(.25,profile.lengthM*.25)))continue;
                        if(profile.medium=="water"&&profile.freshwater&&world.Hydrology.OceanConnected[actualCell])continue;
                        if(profile.medium=="water"&&!profile.freshwater&&!world.Hydrology.OceanConnected[actualCell])continue;
                        ulong ident=key.Id(15,g*16+member)^((ulong)(uint)(id+9127)*0x9e3779b97f4a7c15UL);
                        uint hash=Rng.Mix((uint)ident^(uint)(ident>>32));
                        result.Add(new FaunaPrototypeSpawn{Id=ident,Hash=hash,Profile=profile,Position=position,Region=region,Taxon=id,
                            CoatSeed=unchecked(region*8191+(id+9127)*31),GroundM=sample.ElevationM,WaterM=water,
                            HeightM=profile.medium=="air"?(profile.tiny?.6+1.2*rng.Unit():6+18*rng.Unit()):profile.medium=="water"?-.4-2*rng.Unit():0,
                            ColdC=habitat.ColdTenthsC/10.0,Aridity=habitat.AridityMilli/1000.0});
                    }
                }
            }
        }
        static bool detailSize(ScatterKey key)=>key.Detail;
    }
}

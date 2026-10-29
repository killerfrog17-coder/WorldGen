using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    public enum FaunaSoundKind { Cricket, Cicada, Katydid, Frog, UnderstoryBird, Cockatoo, Hornbill, Hummingbird, Parrot, Pigeon, Raven, Toucan, Turaco, Woodpecker, Owl }
    public sealed class FaunaVoiceProfile
    {
        public string Id="",Name="";
        public FaunaSoundKind Kind;
        public int NativeTaxonId=-1,RegionId;
        public InsectAcousticTraits Acoustics;
        public InsectActivityTraits Activity;
        public AmbientFaunaSpecies Candidate;
    }
    public readonly struct FaunaSoundAnchor
    {
        public readonly ulong Id;public readonly uint Hash;
        public readonly V Position;public readonly FaunaSoundKind Kind;
        public FaunaSoundAnchor(ulong id,uint hash,V position,FaunaSoundKind kind){Id=id;Hash=hash;Position=position;Kind=kind;}
    }
    /// <summary>Bounded sound opportunities, not a live animal population or
    /// saved ecology upgrade. Geography and traits do not depend on camera/chunks.</summary>
    public sealed class FaunaAmbience
    {
        public const string Version="fauna-ambience-2";
        public const int KindCount=15;
        public static readonly string[] Banks={"cricket","cicada","katydid","frog","bird","cockatoo","hornbill","hummingbird","parrot","pigeon","raven","toucan","turaco","woodpecker","owl"};
        public static readonly string[] Names={"Crickets","Cicadas","Katydids","Native frogs","Understory birds","Cockatoos","Hornbills","Hummingbirds","Parrots","Pigeons","Ravens","Toucans","Turacos","Woodpeckers","Woodland owls"};
        static readonly string[] taxonNames={"","","","Anura habitat guild","Understory bird guild","Cacatuidae","Bucerotidae","Trochilidae","Psittaciformes","Columbidae","Corvus","Ramphastidae","Musophagidae","Picidae","Strigidae woodland"};
        static readonly ConditionalWeakTable<World,FaunaAmbience> worlds=new ConditionalWeakTable<World,FaunaAmbience>();
        static readonly V[] normals={new V(1,0,0),new V(-1,0,0),new V(0,1,0),new V(0,-1,0),new V(0,0,1),new V(0,0,-1)};
        readonly World world;readonly int[] taxa=new int[KindCount];
        readonly ConcurrentDictionary<int,FaunaVoiceProfile> profiles=new ConcurrentDictionary<int,FaunaVoiceProfile>();
        public static FaunaAmbience For(World world)=>worlds.GetValue(world,w=>new FaunaAmbience(w));
        FaunaAmbience(World world)
        {
            this.world=world;
            for(int i=0;i<taxa.Length;i++)taxa[i]=-1;
            if(world.Ecology==null)return;
            foreach(var t in world.Ecology.Taxa)
                for(int i=3;i<KindCount;i++)if(t.ScientificName==taxonNames[i])taxa[i]=t.Id;
        }
        public bool TryProfile(int cell,FaunaSoundKind kind,out FaunaVoiceProfile profile)
        {
            profile=null;var e=world.Ecology;
            if(e==null||cell<0||cell>=e.Region.Length||e.Region[cell]<0)return false;
            if((int)kind<0||(int)kind>=KindCount)return false;
            int taxon=taxa[(int)kind];
            if((int)kind>=3&&(taxon<0||!Biogeography.Present(e,cell,taxon)))return false;
            int region=e.Region[cell];profile=profiles.GetOrAdd(region*KindCount+(int)kind,key=>Create(region,kind,taxon));return true;
        }
        FaunaVoiceProfile Create(int region,FaunaSoundKind kind,int taxon)
        {
            var result=new FaunaVoiceProfile{Kind=kind,NativeTaxonId=taxon,RegionId=region};
            if((int)kind<3)
            {
                var s=AmbientFauna.For(world).Region(region)[(int)kind];
                result.Candidate=s;result.Id=s.Id;result.Name=s.Name;result.Activity=s.Activity;result.Acoustics=s.Acoustics;return result;
            }
            var e=world.Ecology;int province=e.Regions[region].ProvinceId;
            var founder=new Rng(world.Recipe.Seed,"fauna-guild-call-founder-1",unchecked(province*31+(int)kind));
            var branch=new Rng(world.Recipe.Seed,"fauna-guild-call-branch-1",unchecked(region*31+(int)kind));
            bool isBird=kind!=FaunaSoundKind.Frog;
            double carrier=kind==FaunaSoundKind.Pigeon?380:kind==FaunaSoundKind.Owl?600:kind==FaunaSoundKind.Raven?700:kind==FaunaSoundKind.Hornbill?500:kind==FaunaSoundKind.Cockatoo||kind==FaunaSoundKind.Parrot?1700:kind==FaunaSoundKind.Hummingbird?6500:3500;
            double variation=.97+.06*branch.Unit();
            result.Id=Version+":"+world.Recipe.Seed+":"+region+":"+taxon;result.Name=e.Taxa[taxon].Name+" / regional calling form";
            result.Acoustics=new InsectAcousticTraits{Shape=isBird?InsectSongShape.WhistlePhrase:InsectSongShape.PulsedCroak,
                Function=isBird?"Contact/territorial song; stronger in breeding activity":"Male breeding call",
                CarrierHz=D.Q((isBird?carrier*(.8+.4*founder.Unit()):300+650*founder.Unit())*variation,1000),
                RelativeLoudness=.5+.2*founder.Unit(),IndividualPitchSpreadCents=8,IndividualTimingSpreadFraction=.04,
                PulseOnSeconds=isBird?.18:.35,PulseGapSeconds=.12,PulsesPerPhrase=2+(int)(3*founder.Unit()),PhraseGapSeconds=isBird?4:1.2,
                AttackSeconds=.015,ReleaseSeconds=.04,HarmonicMix=isBird?.2:.65,NoiseMix=isBird?.02:.15};
            int cell=e.Regions[region].RepresentativeCell;var h=e.Habitat[cell];
            result.Activity=new InsectActivityTraits{StartSolarHour=isBird?4.5:17,DurationHours=isBird?15:12,MinTemperatureC=kind==FaunaSoundKind.Raven?-30:kind==FaunaSoundKind.Pigeon||kind==FaunaSoundKind.Woodpecker?-12:isBird?1:7,
                PreferredTemperatureC=isBird?20:23,MaxTemperatureC=38,SeasonCue=h.ColdTenthsC>=180?InsectSeasonCue.WarmMoistOpportunities:InsectSeasonCue.AnnualWarmSeason,
                SeasonPeakOffsetDays=isBird?-40:-25,AdultDisplayWindowDays=isBird?240:160,RainSuppressionMmPerHour=isBird?2:6,WindSuppressionMps=7};
            if(kind==FaunaSoundKind.Owl){result.Activity.StartSolarHour=18;result.Activity.DurationHours=12;result.Activity.MinTemperatureC=-18;result.Acoustics.PhraseGapSeconds=20;result.Acoustics.RelativeLoudness=.55;}
            return result;
        }
        public static double HabitatGain(FaunaVoiceProfile p,HabitatCell h,LocalTerrainSample sample)
        {
            if(h==null||sample.Underwater||sample.Biome==Biome.Ocean||sample.Biome==Biome.Ice||sample.Ground.Salinity>.4)return 0;
            if(p.Candidate!=null)
            {
                double fit=AmbientFauna.HabitatSuitability(p.Candidate,h);
                double cover=p.Kind==FaunaSoundKind.Cricket?Math.Max(sample.Cover.Coverage*2,sample.Forest.Coverage*.85+sample.Ground.Organic*.5):sample.Cover.Coverage+sample.Forest.Coverage;
                return fit*D.Clamp(cover);
            }
            if(p.Kind==FaunaSoundKind.Raven)return D.Clamp(.2+sample.Forest.Coverage+sample.Cover.Coverage*.5);
            if(p.Kind==FaunaSoundKind.Pigeon||p.Kind==FaunaSoundKind.Hummingbird)return D.Clamp(sample.Forest.Coverage*2+sample.Cover.Coverage*.5);
            if((int)p.Kind>=4)return D.Clamp(sample.Forest.Coverage*2);
            double refuge=Math.Max(sample.RiverInfluence,Math.Max(h.FloodFractionPermille*.001,D.Clamp((180-h.WaterTableCm)/180.0)));
            double dampForest=D.Clamp((h.AridityMilli*.001-.65)/.5)*D.Clamp(sample.Ground.Organic*3)*(1-sample.Ground.Drainage*.7);
            return D.Clamp(Math.Max(refuge,dampForest));
        }
        static double Smooth(double a,double b,double v){double x=D.Clamp((v-a)/(b-a));return x*x*(3-2*x);}
        public static double ActivityGain(FaunaVoiceProfile p,double hour,double year,double latitude,double temperature,double humidity,double rain,double wind)
        {
            var a=p.Activity;double elapsed=(hour-a.StartSolarHour+48)%24;
            double daily=Smooth(0,.8,elapsed)*(1-Smooth(a.DurationHours-.8,a.DurationHours,elapsed));
            double thermal=Smooth(a.MinTemperatureC,a.PreferredTemperatureC,temperature)*(1-Smooth(a.MaxTemperatureC-3,a.MaxTemperatureC,temperature));
            double season=1;
            if(a.SeasonCue==InsectSeasonCue.AnnualWarmSeason)
            {
                double day=(year-Math.Floor(year))*365,peak=(latitude>=0?.56:.06)*365+a.SeasonPeakOffsetDays;
                peak=(peak%365+365)%365;double offset=Math.Abs(day-peak);offset=Math.Min(offset,365-offset);
                season=1-Smooth(a.AdultDisplayWindowDays*.38,a.AdultDisplayWindowDays*.5+12,offset);
                if((int)p.Kind>=4)season=.25+.75*season;
            }
            double weather=1/(1+Math.Pow(Math.Max(0,rain)/a.RainSuppressionMmPerHour,2));
            weather/=1+Math.Pow(Math.Max(0,wind)/a.WindSuppressionMps,2);
            if(p.Kind==FaunaSoundKind.Frog)weather*=Smooth(.35,.75,humidity+.1*Math.Min(2,rain));
            if((int)p.Kind>=4&&p.Kind!=FaunaSoundKind.Owl)daily*=.35+.65*Math.Exp(-Math.Pow((hour-6.5)/2.3,2))+.3*Math.Exp(-Math.Pow((hour-17)/2.5,2));
            return D.Clamp(daily*thermal*season*weather);
        }
        public static void Anchors(int seed,V center,double renderRadiusM,double rangeM,List<FaunaSoundAnchor> result)
        {
            result.Clear();int side=(int)Math.Ceiling(2*renderRadiusM/48),margin=(int)Math.Ceiling(rangeM/16)+2;
            double chord=rangeM/renderRadiusM;
            for(int face=0;face<6;face++)
            {
                V n=normals[face];double d=V.Dot(center,n);if(d<.5)continue;
                V u=face<4?V.Cross(new V(0,0,1),n).Unit:new V(1,0,0),v=V.Cross(n,u).Unit;
                int cx=(int)Math.Floor((V.Dot(center,u)/d+1)*.5*side),cy=(int)Math.Floor((V.Dot(center,v)/d+1)*.5*side);
                for(int y=Math.Max(0,cy-margin);y<=Math.Min(side-1,cy+margin);y++)for(int x=Math.Max(0,cx-margin);x<=Math.Min(side-1,cx+margin);x++)
                {
                    ulong id=((ulong)face<<60)|((ulong)x<<30)|(uint)y;uint hash=Rng.Mix((uint)id^(uint)(id>>32)^(uint)seed);
                    var r=new Rng(seed,"fauna-source-grid-1",unchecked((int)hash));if(r.Unit()>.6)continue;
                    V point=(n+u*((x+.15+.7*r.Unit())*2/side-1)+v*((y+.15+.7*r.Unit())*2/side-1)).Unit;
                    if((point-center).Length>chord)continue;
                    uint bucket=hash%100;
                    int kind=bucket<25?0:bucket<43?1:bucket<61?2:bucket<75?3:bucket<85?4:5+(int)(Rng.Mix(hash)%10);
                    result.Add(new FaunaSoundAnchor(id,hash,point,(FaunaSoundKind)kind));
                }
            }
            result.Sort((a,b)=>{int c=(a.Position-center).Length.CompareTo((b.Position-center).Length);return c!=0?c:a.Id.CompareTo(b.Id);});
        }
    }
}

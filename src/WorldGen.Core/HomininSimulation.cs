using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        Dictionary<int,Route[]> homininRoutes;
        static bool EarlyHominins(PrehistoryState s)=>s?.DeepPast?.Active==true;
        public PrehistoryState PrepareHominins(int origin=-1,bool earthLike=true)
        {
            if(origin<0)origin=ChooseOrigin();if(origin<0||origin>=Patches.Length||Patches[origin].Capacity<25)throw new ArgumentException("Choose a habitable founding region.");
            var h=new HomininHistory{EarthLikeTrajectory=earthLike,
                Lineages=new[]{new HomininLineage{Id=0,OriginRegion=origin,Name="Homo "+HomininName(0,"lineage")}},
                Traditions=new[]{new HomininTradition{Id=0,OriginRegion=origin,Name=HomininName(0,"tradition")+" tradition"}},
                Languages=new[]{new HomininLanguage{Id=0,OriginRegion=origin,Name=HomininName(0,"language")+" communication tradition"}}};
            var s=new PrehistoryState{Version="prehistory-hominin-1",Species="Homo",Origin=origin,WorldHash=World.Stages.Last().Hash,DeepPast=h,
                Population=new long[Patches.Length],ArrivalYear=Enumerable.Repeat(-1,Patches.Length).ToArray(),Ancestry=Enumerable.Repeat(5000,Patches.Length*8).ToArray(),
                Society=new SocietyState{Knowledge=new int[Patches.Length],CellCulture=Enumerable.Repeat(-1,World.Surface.Cells.Length).ToArray(),SelectedStepYears=1000},
                Timeline=new SimulationTimeline{Version="event-calendar-hominin-1",Events=Array.Empty<SimulationEvent>()}};
            PublishHomininAggregates(s);s.DeepPast.SealDomain();s.Seal();return s;
        }
        string HomininName(int id,string kind)
        {
            string[] a={"Ka","Ta","Na","Sa","Mo","Ha","Ru","Wa","Pe","Lu","Ye","O"},b={"ra","na","mi","ku","lo","si","ta","we","ru","ya"};
            uint n=Rng.Mix((uint)World.Recipe.Seed^(uint)(id*7919)^(uint)(kind=="lineage"?3817:9511));
            return a[n%(uint)a.Length]+b[(n>>8)%(uint)b.Length]+b[(n>>16)%(uint)b.Length];
        }
        PrehistoryState StartHominins(PrehistoryState source)
        {
            ValidateHominins(source);source=UpgradeHomininLanguagesOwned(source);if(source.Started)return source;var s=source.Copy();s.Started=true;
            s.DeepPast.Populations=new[]{new HomininPopulation{Id=0,Region=s.Origin,Lineage=0,Tradition=0,Language=0,People=Math.Min(150,Patches[s.Origin].Capacity),Ancestry=new[]{new HomininAncestry{Lineage=0,Share=1}}}};
            HumanAppearanceEnvironment(s.Origin,out _,out double uv,out int mean);
            for(int k=0;k<8;k++)s.DeepPast.Populations[0].Appearance[k]=HistoryHumanVariation.FounderValue(World.Recipe.Seed,s.Origin,(HumanAppearanceTrait)k,uv,mean);
            s.ArrivalYear[s.Origin]=0;PublishHomininAggregates(s);s.DeepPast.SealDomain();s.Seal();return s;
        }
        PrehistoryState AdvanceHominins(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validate)
        {
            if(validate)ValidateHominins(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            source=UpgradeHomininLanguagesOwned(source);
            long now=SimulationTime.Seconds(source);if(seconds<1||seconds>SimulationTime.Maximum-now||maximumEvents<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            long target=now+seconds;
            if(target<source.DeepPast.NextBoundaryYear*SimulationTime.Year)
            {
                // Domain is immutable between scheduled boundaries. Fine local
                // clocks share it privately; public snapshots still detach it.
                var clock=source.CopyClockOnly();clock.Timeline.Seconds=target;clock.Years=(int)(target/SimulationTime.Year);clock.Seal();return clock;
            }
            var s=source.Copy();var h=s.DeepPast;int events=0;
            while(h.NextBoundaryYear*SimulationTime.Year<=target&&events<maximumEvents)
            {
                token.ThrowIfCancellationRequested();s.Years=h.NextBoundaryYear;s.Timeline.Seconds=s.Years*SimulationTime.Year;
                AdvanceHomininBoundary(s,token);h.LastBoundaryYear=s.Years;
                int bp=h.YearsBeforePresent(s.Years),step=bp>1000000?1000:bp>200000?500:bp>20000?200:50;
                h.NextBoundaryYear=s.Years+step;s.Society.SelectedStepYears=step;s.Timeline.ProcessedEvents++;events++;
            }
            if(events<maximumEvents||h.NextBoundaryYear*SimulationTime.Year>target){s.Timeline.Seconds=target;s.Years=(int)(target/SimulationTime.Year);}
            PublishHomininAggregates(s);s.DeepPast.SealDomain();s.Seal();return s;
        }
        void AdvanceHomininBoundary(PrehistoryState s,CancellationToken token)
        {
            var h=s.DeepPast;int dt=s.Years-h.LastBoundaryYear,bp=h.YearsBeforePresent(s.Years),seed=World.Recipe.Seed;
            homininRoutes??=Routes.SelectMany(r=>new[]{(region:r.A,route:r),(region:r.B,route:r)}).GroupBy(x=>x.region).ToDictionary(g=>g.Key,g=>g.Select(x=>x.route).ToArray());
            var groups=h.Populations.Where(p=>p.People>=1).OrderBy(p=>p.Id).ToList();var traditions=h.Traditions.ToList();var lineages=h.Lineages.ToList();var languages=h.Languages.ToList();var events=h.Events.ToList();var habitats=h.Habitats.ToDictionary(e=>e.Region);
            var byRegion=groups.GroupBy(p=>p.Region).ToDictionary(g=>g.Key,g=>g.ToList());
            var beforeRegional=byRegion.ToDictionary(p=>p.Key,p=>p.Value.Sum(g=>g.People));
            double globalGlacial=PleistoceneClimate.GlacialFraction(seed,bp),globalSea=-120*PleistoceneClimate.GlacialFraction(seed,bp+3000);
            var regionalCapacity=new Dictionary<int,double>();
            void Event(HomininPopulation p,string kind,string text)=>events.Add(new HomininEvent{Year=s.Years,Region=p.Region,Lineage=p.Lineage,Tradition=p.Tradition,Kind=kind,Text=text});
            HomininHabitat Environment(int region)
            {
                if(habitats.TryGetValue(region,out var existing)&&existing.UpdatedYear==s.Years)return existing;
                int cell=Patches[region].Cell;var habitat=World.Ecology.Habitat[cell];V point=World.Surface.Cells[cell].Position;
                double latitude=Math.Asin(point.Z)*180/Math.PI,longitude=Math.Atan2(point.Y,point.X)*180/Math.PI;
                var c=PleistoceneClimate.At(seed,bp,latitude,longitude,World.ElevationM[cell]);
                if(existing==null)existing=new HomininHabitat{Region=region,UpdatedYear=s.Years-dt,WoodyCover=HomininWoodland(habitat.Biome),GrassCover=.5,LargePrey=.6,SmallPrey=.6,Aquatic=.5};
                double mean=habitat.MeanTenthsC/10d+c.TemperatureDeltaC,moisture=habitat.AridityMilli/1000d*c.PrecipitationFactor;
                double thermal=SurvivalMath.Clamp((mean+10)/20),wet=SurvivalMath.Clamp(moisture/.6);
                double trees=HomininWoodland(habitat.Biome)*thermal*wet*(1-c.IceSuitability),grass=SurvivalMath.Clamp((1-trees*.7)*wet*thermal)*(1-c.IceSuitability);
                // Persistence and colonisation lag: mature woodland cannot appear
                // instantaneously after a warm-state threshold changes.
                int habitatElapsed=Math.Max(0,s.Years-existing.UpdatedYear);
                double treeResponse=1-Math.Exp(-habitatElapsed/(trees>existing.WoodyCover?3000d:800d));
                existing.WoodyCover+=(trees-existing.WoodyCover)*treeResponse;existing.GrassCover+=(grass-existing.GrassCover)*(1-Math.Exp(-habitatElapsed/250d));
                existing.LargePrey+=(SurvivalMath.Clamp(.15+existing.GrassCover*.8+existing.WoodyCover*.25)*(1-c.IceSuitability)-existing.LargePrey)*(1-Math.Exp(-habitatElapsed/600d));
                existing.SmallPrey+=(SurvivalMath.Clamp(existing.GrassCover*.5+existing.WoodyCover*.6)*(1-c.IceSuitability)-existing.SmallPrey)*(1-Math.Exp(-habitatElapsed/200d));
                existing.Aquatic=SurvivalMath.Clamp((habitat.FloodDaysPerYear>0?.7:.3)*c.PrecipitationFactor)*(1-c.IceSuitability);
                existing.Ice=c.IceSuitability;existing.TemperatureDelta=c.TemperatureDeltaC;existing.RainFactor=c.PrecipitationFactor;existing.UpdatedYear=s.Years;habitats[region]=existing;return existing;
            }
            double Capacity(HomininPopulation p)
            {
                var env=Environment(p.Region);var t=traditions[p.Tradition];var k=t.Knowledge;int cell=Patches[p.Region].Cell;var habitat=World.Ecology.Habitat[cell];
                var factors=new PleistoceneConditions(globalGlacial,env.TemperatureDelta,env.RainFactor,globalSea,env.Ice);
                double climate=PleistoceneClimate.CapacityFactor(habitat,factors,p.ColdAdaptation,(k&LithicKnowledge.FireKeeping)!=0,(k&LithicKnowledge.HideWorking)!=0);
                double food=.2+.35*env.LargePrey+.2*env.SmallPrey+.35*env.GrassCover+.2*env.WoodyCover+((k&LithicKnowledge.Fishing)!=0?env.Aquatic*.3:0);
                double technique=(.35+Math.Min(.8,CountBits((long)k)*.035)+t.Cultivation*6+t.Herding*2)*(.65+.5*p.Communication);
                return Patches[p.Region].Capacity*climate*food*technique;
            }
            foreach(var p in groups)
            {
                token.ThrowIfCancellationRequested();var env=Environment(p.Region);var t=traditions[p.Tradition];var lineage=lineages[p.Lineage];
                double total=beforeRegional[p.Region],cap=Capacity(p);regionalCapacity[p.Region]=Math.Max(cap,regionalCapacity.TryGetValue(p.Region,out var old)?old:0);
                double growth=cap<=0?-dt*.02:dt*.0012*(1-total/Math.Max(1,cap));p.People*=Math.Exp(Math.Max(-40,Math.Min(8,growth)));
                if(p.People<2){p.People=0;Event(p,"local-loss","The local band disappeared; ancestry and surviving relatives remain recorded.");continue;}
                double targetCold=SurvivalMath.Clamp((120-World.Ecology.Habitat[Patches[p.Region].Cell].ColdTenthsC-env.TemperatureDelta*10)/350);
                double adaptation=1-Math.Exp(-dt/60000d);p.ColdAdaptation+=(targetCold-p.ColdAdaptation)*adaptation;p.Compactness+=(targetCold-p.Compactness)*adaptation*.3;
                double variability=.7+.3*globalGlacial;
                double social=SurvivalMath.Clamp(Math.Log10(Math.Max(10,total))/5,.25,1),trajectory=h.EarthLikeTrajectory?1:.55;
                // Calibrated evolutionary priors, not inferred genes or a claim
                // that language was invented on a particular historical date.
                p.Communication=SurvivalMath.Clamp(p.Communication+dt*3.5e-7*social*variability*trajectory,0,.98);
                p.Learning=SurvivalMath.Clamp(p.Learning+dt*2.9e-7*(.6+.4*social)*trajectory,0,.98);
                HumanAppearanceEnvironment(p.Region,out _,out double uv,out int mean);
                for(int k=0;k<8;k++)p.Appearance[k]=HistoryHumanVariation.Evolve(seed,unchecked(p.Region*397+p.Id),s.Years,(HumanAppearanceTrait)k,p.Appearance[k],dt,(long)p.People,uv,mean+(int)(env.TemperatureDelta*10),(t.Knowledge&LithicKnowledge.HideWorking)!=0?.2:0);
                p.GeneticDrift+=dt*(.0000008+.0000015/Math.Sqrt(Math.Max(2,p.People)));
                t.Teaching=Math.Max(t.Teaching,p.Communication*.75+t.Network*.15);t.Retention=SurvivalMath.Clamp(.15+p.Learning*.45+t.Teaching*.25+social*.15);
            }
            groups.RemoveAll(p=>p.People<2);
            // Contacts and admixture act on co-resident populations. Language and
            // learned culture do not become genetically inherited attributes.
            foreach(var residents in groups.GroupBy(p=>p.Region))
            {
                var list=residents.ToArray();for(int i=0;i<list.Length;i++)for(int j=i+1;j<list.Length;j++)
                {
                    var a=list[i];var b=list[j];a.LastContactYear=b.LastContactYear=s.Years;a.GeneticDrift*=.98;b.GeneticDrift*=.98;
                    var ta=traditions[a.Tradition];var tb=traditions[b.Tradition];ta.Network=SurvivalMath.Clamp(ta.Network+.08);tb.Network=SurvivalMath.Clamp(tb.Network+.08);
                    TransferHomininKnowledge(ta,tb,Math.Min(a.Communication,b.Communication),seed,a.Id+b.Id,s.Years,dt);
                    if(a.Lineage!=b.Lineage&&Math.Abs(a.Compactness-b.Compactness)+Math.Abs(a.Stature-b.Stature)<.7)
                    {
                        double fraction=1-Math.Exp(-dt/60000d);
                        if(h.RulesVersion>=2)ExchangeHomininAncestry(a,b,fraction);
                        else
                        {
                            MixHomininAncestry(a,b,fraction);MixHomininAncestry(b,a,fraction);
                            double communication=a.Communication;a.Communication+=(b.Communication-a.Communication)*fraction;b.Communication+=(communication-b.Communication)*fraction;
                        }
                    }
                }
            }
            var arrivals=new List<HomininPopulation>();
            var residentIndex=groups.GroupBy(p=>(p.Region,p.Lineage)).ToDictionary(g=>g.Key,g=>g.First());
            foreach(var p in groups.ToArray())
            {
                if(p.People<70||!homininRoutes.TryGetValue(p.Region,out var routes))continue;var t=traditions[p.Tradition];
                var route=routes[(int)(Rng.Value(seed,"hominin-route",unchecked(p.Id*7919+s.Years))*routes.Length)];int to=route.A==p.Region?route.B:route.A;
                double sea=globalSea;
                bool exposed=route.Shallow&&-sea>=route.SeaDropM;
                if(route.Sea&&!exposed&&(t.Knowledge&LithicKnowledge.Watercraft)==0)continue;
                if(route.Sea&&!exposed&&route.DistanceM>20000&&Rng.Value(seed,"hominin-sea",p.Id+s.Years)>.015)continue;
                double pressure=p.People/Math.Max(1,regionalCapacity[p.Region]);
                double chance=1-Math.Exp(-dt*.0003*(.2+Math.Min(2,pressure)));
                if(Rng.Value(seed,"hominin-migration",unchecked(p.Id*193+s.Years))>chance)continue;
                var incoming=p.Copy();incoming.Region=to;double cap=Capacity(incoming);if(cap<15)continue;
                double moved=Math.Min(p.People*.12,Math.Max(12,cap*.03));if(moved<10)continue;p.People-=moved;
                residentIndex.TryGetValue((to,p.Lineage),out var resident);
                if(resident!=null){double fraction=moved/(resident.People+moved);MixHomininAncestry(resident,p,fraction);resident.People+=moved;resident.LastContactYear=s.Years;resident.Communication+=(p.Communication-resident.Communication)*fraction;resident.Learning+=(p.Learning-resident.Learning)*fraction;TransferHomininKnowledge(traditions[resident.Tradition],t,Math.Min(resident.Communication,p.Communication),seed,p.Id+resident.Id,s.Years,dt);continue;}
                incoming.Id=h.NextPopulation++;incoming.People=moved;incoming.FoundedYear=incoming.LastMoveYear=s.Years;incoming.LastContactYear=s.Years;
                if(s.Years-p.FoundedYear>5000)
                {var tradition=t.Copy();tradition.Id=traditions.Count;tradition.Parent=t.Id;tradition.FoundedYear=s.Years;tradition.OriginRegion=to;tradition.Name=HomininName(tradition.Id,"tradition")+" tradition";traditions.Add(tradition);incoming.Tradition=tradition.Id;incoming.Language=h.NextLanguage++;
                    languages.Add(new HomininLanguage{Id=incoming.Language,Parent=p.Language,FoundedYear=s.Years,OriginRegion=to,Name=HomininName(incoming.Language,"language")+" communication tradition"});}
                arrivals.Add(incoming);residentIndex[(to,incoming.Lineage)]=incoming;if(s.ArrivalYear[to]<0){s.ArrivalYear[to]=s.Years;Event(incoming,"arrival","A descendant population established itself in a new region.");}
            }
            groups.AddRange(arrivals);
            foreach(var p in groups)
            {
                if(s.Years-p.LastContactYear>120000&&p.GeneticDrift>.16&&s.Years-lineages[p.Lineage].FoundedYear>150000)
                {
                    var lineage=new HomininLineage{Id=lineages.Count,Parent=p.Lineage,FoundedYear=s.Years,OriginRegion=p.Region,Name="Homo "+HomininName(lineages.Count,"lineage"),Communication=p.Communication,Learning=p.Learning,BodyCompactness=p.Compactness,Stature=p.Stature,SapiensLike=p.Communication>.72&&p.Learning>.75};
                    lineage.FoundingAncestry=p.Ancestry.Select(a=>a.Copy()).ToArray();lineages.Add(lineage);p.Lineage=lineage.Id;p.Ancestry=new[]{new HomininAncestry{Lineage=lineage.Id,Share=1}};p.GeneticDrift=0;p.LastContactYear=s.Years;Event(p,"lineage","A long-isolated population became a distinct regional hominin lineage.");
                }
                if(!lineages[p.Lineage].SapiensLike&&p.Communication>.72&&p.Learning>.75)
                {
                    int parent=p.Lineage;bool newlyCreated=false;var descendant=lineages.FirstOrDefault(l=>l.Parent==parent&&l.SapiensLike);
                    if(descendant==null)
                    {
                        descendant=new HomininLineage{Id=lineages.Count,Parent=parent,FoundedYear=s.Years,OriginRegion=p.Region,Name="Homo "+HomininName(lineages.Count,"lineage"),
                            SapiensLike=true,Communication=p.Communication,Learning=p.Learning,BodyCompactness=p.Compactness,Stature=p.Stature,FoundingAncestry=p.Ancestry.Select(a=>a.Copy()).ToArray()};
                        lineages.Add(descendant);newlyCreated=true;
                    }
                    p.Lineage=descendant.Id;p.Ancestry=new[]{new HomininAncestry{Lineage=descendant.Id,Share=1}};
                    if(newlyCreated)Event(p,"communication","A sapiens-like descendant population emerged. Other populations of its ancestral lineage retain their own traits and identity.");
                }
            }
            foreach(var culturalGroup in groups.GroupBy(p=>p.Tradition))
            {
                var t=traditions[culturalGroup.Key];var residents=culturalGroup.ToArray();
                double population=residents.Sum(p=>p.People),communication=residents.Sum(p=>p.People*p.Communication)/population,learning=residents.Sum(p=>p.People*p.Learning)/population;
                t.Network=Math.Max(0,t.Network-dt/100000d);AdvanceLithicTradition(s,t,residents[0],population,communication,learning,dt,events);
            }
            var extant=new HashSet<int>(groups.Select(p=>p.Lineage));
            foreach(var lineage in lineages)if(!extant.Contains(lineage.Id)&&lineage.ExtinctYear<0){lineage.ExtinctYear=s.Years;events.Add(new HomininEvent{Year=s.Years,Region=lineage.OriginRegion,Lineage=lineage.Id,Kind="extinction",Text=lineage.Name+" ceased to exist as a distinct living population; mixed ancestry may survive."});}
            var livingLanguages=new HashSet<int>(groups.Select(p=>p.Language));foreach(var language in languages)if(language.ExtinctYear<0&&!livingLanguages.Contains(language.Id))language.ExtinctYear=s.Years;
            h.Populations=groups.OrderBy(p=>p.Id).ToArray();h.Lineages=lineages.ToArray();h.Traditions=traditions.ToArray();h.Languages=languages.ToArray();h.Events=events.ToArray();h.Habitats=habitats.Values.OrderBy(e=>e.Region).ToArray();
        }
        public PrehistoryState UpgradeHomininLanguages(PrehistoryState source)
        {ValidateHominins(source);return UpgradeHomininLanguagesOwned(source);}
        PrehistoryState UpgradeHomininLanguagesOwned(PrehistoryState source)
        {
            if(source.DeepPast.Version>=2)return source;
            var s=source.Copy();var h=s.DeepPast;h.Version=2;
            // The first checkpoint stored IDs but not language parentage. Keep
            // those identities; do not invent ancestors or dates during upgrade.
            h.Languages=Enumerable.Range(0,h.NextLanguage).Select(i=>new HomininLanguage{Id=i,OriginRegion=-1,FoundedYear=-1,LegacyOriginUnknown=true,Name=HomininName(i,"language")+" communication tradition"}).ToArray();
            h.SealDomain();s.Seal();return s;
        }
        static int CountBits(long bits){int n=0;while(bits!=0){bits&=bits-1;n++;}return n;}
        static double HomininWoodland(Biome biome)=>biome==Biome.TropicalRainforest||biome==Biome.TemperateRainforest?.9:biome==Biome.TropicalSeasonalForest||biome==Biome.TemperateForest?.7:biome==Biome.BorealForest?.6:biome==Biome.Savanna||biome==Biome.MediterraneanWoodland?.3:.05;
        static void MixHomininAncestry(HomininPopulation recipient,HomininPopulation donor,double fraction)
        {
            var shares=new Dictionary<int,double>();foreach(var a in recipient.Ancestry)shares[a.Lineage]=a.Share*(1-fraction);
            foreach(var a in donor.Ancestry){shares.TryGetValue(a.Lineage,out double previous);shares[a.Lineage]=previous+a.Share*fraction;}
            for(int k=0;k<8;k++)recipient.Appearance[k]=(int)Math.Round(recipient.Appearance[k]*(1-fraction)+donor.Appearance[k]*fraction);
            recipient.Ancestry=shares.OrderBy(p=>p.Key).Select(p=>new HomininAncestry{Lineage=p.Key,Share=p.Value}).ToArray();
        }
        /// <summary>Coarse, reciprocal gene flow with equal represented people
        /// exchanged. A smaller cohort changes more; ancestry totals are not
        /// created by mixing a large cohort with an already modified donor.
        /// Learned traditions and language identity are unaffected.</summary>
        public static void ExchangeHomininAncestry(HomininPopulation a,HomininPopulation b,double fraction)
        {
            if(a==null||b==null||!SurvivalMath.Finite(fraction)||fraction<0||fraction>1||!SurvivalMath.Finite(a.People)||!SurvivalMath.Finite(b.People)||a.People<=0||b.People<=0)throw new ArgumentException("Invalid reciprocal ancestry exchange.");
            if(ReferenceEquals(a,b)||fraction==0)return;
            var originalA=a.Copy();double exchanged=Math.Min(a.People,b.People)*fraction,fa=exchanged/a.People,fb=exchanged/b.People;
            MixHomininAncestry(a,b,fa);MixHomininAncestry(b,originalA,fb);
            double ac=a.Communication,al=a.Learning,ad=a.ColdAdaptation,ab=a.Compactness,asize=a.Stature;
            a.Communication+=(b.Communication-ac)*fa;b.Communication+=(ac-b.Communication)*fb;
            a.Learning+=(b.Learning-al)*fa;b.Learning+=(al-b.Learning)*fb;
            a.ColdAdaptation+=(b.ColdAdaptation-ad)*fa;b.ColdAdaptation+=(ad-b.ColdAdaptation)*fb;
            a.Compactness+=(b.Compactness-ab)*fa;b.Compactness+=(ab-b.Compactness)*fb;
            a.Stature+=(b.Stature-asize)*fa;b.Stature+=(asize-b.Stature)*fb;
        }
        static void TransferHomininKnowledge(HomininTradition a,HomininTradition b,double communication,int seed,int entity,int year,int dt)
        {
            if(Rng.Value(seed,"hominin-transmission",unchecked(entity*397+year))>1-Math.Exp(-dt/1000d*(.1+communication)))return;
            var combined=a.Knowledge|b.Knowledge;a.Knowledge=combined;b.Knowledge=combined;
        }
        void PublishHomininAggregates(PrehistoryState s)
        {
            Array.Clear(s.Population,0,s.Population.Length);Array.Fill(s.Society.CellCulture,-1);var h=s.DeepPast;
            foreach(var g in h.Populations)s.Population[g.Region]+=(long)Math.Round(g.People);
            foreach(var region in h.Populations.GroupBy(p=>p.Region))
            {double total=region.Sum(p=>p.People);for(int k=0;k<8;k++)s.Ancestry[region.Key*8+k]=(int)Math.Round(region.Sum(p=>p.People*p.Appearance[k])/Math.Max(1,total));}
            s.Society.Cultures=h.Traditions.Select(t=>new CultureIdentity{Id=t.Id,Name=t.Name,OriginCell=Patches[t.OriginRegion].Cell,FoundedYear=t.FoundedYear,Stage=SocietyStage.Foragers,Population=(long)Math.Round(h.Populations.Where(p=>p.Tradition==t.Id).Sum(p=>p.People)),Subsistence=HomininHistory.StageName(HomininHistory.Stage(t,h.RulesVersion<3)),Farming=(int)(t.Cultivation*1000)}).ToArray();
            foreach(var region in h.Populations.GroupBy(p=>p.Region)){int culture=region.OrderByDescending(p=>p.People).First().Tradition;foreach(int cell in Patches[region.Key].Cells)s.Society.CellCulture[cell]=culture;}
            s.Society.Events=h.Events.Select(e=>new ChronicleEvent{Year=e.Year,Cell=Patches[e.Region].Cell,CultureId=e.Tradition,Category=e.Kind=="technology"?ChronicleCategory.Discoveries:ChronicleCategory.Society,Text=e.Text}).ToArray();
        }
        void ValidateHominins(PrehistoryState s)
        {
            if(s==null||s.Version!="prehistory-hominin-1"||s.Species!="Homo"||s.WorldHash!=World.Stages.Last().Hash||s.DeepPast==null||(s.DeepPast.Version!=1&&s.DeepPast.Version!=2)||s.Origin<0||s.Origin>=Patches.Length||s.Population?.Length!=Patches.Length||s.Ancestry?.Length!=Patches.Length*8||s.ArrivalYear?.Length!=Patches.Length||s.Timeline?.Version!="event-calendar-hominin-1"||s.Timeline.Seconds<0||s.Years!=s.Timeline.Seconds/SimulationTime.Year||s.Society==null||s.Hash!=s.ComputeHash())throw new ArgumentException("Invalid early-hominin save.");
            var h=s.DeepPast;if(h.CanonicalHash!=h.ComputeDomainHash())throw new ArgumentException("Early-hominin domain fingerprint changed.");if(h.RulesVersion<1||h.RulesVersion>3||h.Version==1&&h.RulesVersion!=1||h.NextBoundaryYear<=s.Years||h.LastBoundaryYear>s.Years||h.NextLanguage<1||h.NextPopulation<1||h.Lineages==null||h.Lineages.Length==0||h.Traditions==null||h.Traditions.Length==0||h.Populations==null||h.Habitats==null||h.Events==null)throw new ArgumentException("Invalid hominin timeline.");
            var ids=new HashSet<int>();foreach(var p in h.Populations)
                if(p==null||!ids.Add(p.Id)||p.Id<0||p.Id>=h.NextPopulation||p.Region<0||p.Region>=Patches.Length||p.Lineage<0||p.Lineage>=h.Lineages.Length||p.Tradition<0||p.Tradition>=h.Traditions.Length||!SurvivalMath.Finite(p.People)||p.People<0||p.Appearance==null||p.Appearance.Length!=8||p.Appearance.Any(v=>v<0||v>10000)||p.Ancestry==null||Math.Abs(p.Ancestry.Sum(a=>a.Share)-1)>1e-6||p.Ancestry.Any(a=>a.Lineage<0||a.Lineage>=h.Lineages.Length||!SurvivalMath.Finite(a.Share)||a.Share<0))throw new ArgumentException("Invalid hominin population.");
            bool Unit(double x)=>SurvivalMath.Finite(x)&&x>=0&&x<=1;
            foreach(var p in h.Populations)if(!Unit(p.Communication)||!Unit(p.Learning)||!Unit(p.ColdAdaptation)||!Unit(p.Compactness)||!Unit(p.Stature)||!SurvivalMath.Finite(p.GeneticDrift)||p.GeneticDrift<0||p.People>1e12||p.Language<0||p.Language>=h.NextLanguage||p.FoundedYear<0||p.FoundedYear>s.Years||p.LastContactYear>s.Years)throw new ArgumentException("Invalid hominin traits or chronology.");
            for(int i=0;i<h.Lineages.Length;i++)
            {
                var l=h.Lineages[i];if(l==null||l.Id!=i||l.Parent>=i||l.Parent< -1||l.Name==null||l.OriginRegion<0||l.OriginRegion>=Patches.Length||l.FoundedYear<0||l.FoundedYear>s.Years||l.ExtinctYear< -1||l.ExtinctYear>s.Years||!Unit(l.Communication)||!Unit(l.Learning)||!Unit(l.BodyCompactness)||!Unit(l.Stature)||l.FoundingAncestry==null)throw new ArgumentException("Invalid hominin lineage.");
            }
            for(int i=0;i<h.Traditions.Length;i++)
            {var t=h.Traditions[i];if(t==null||t.Id!=i||t.Parent>=i||t.Parent< -1||t.Name==null||t.OriginRegion<0||t.OriginRegion>=Patches.Length||t.FoundedYear<0||t.FoundedYear>s.Years||!Unit(t.Teaching)||!Unit(t.Network)||!Unit(t.Retention)||!Unit(t.Sedentism)||!Unit(t.Cultivation)||!Unit(t.Herding)||((long)t.Knowledge&~((1L<<(h.RulesVersion>=3?26:25))-1))!=0)throw new ArgumentException("Invalid hominin tradition.");}
            if(h.Version>=2)
            {
                if(h.Languages==null||h.Languages.Length!=h.NextLanguage)throw new ArgumentException("Missing hominin language identities.");
                for(int i=0;i<h.Languages.Length;i++)
                {
                    var l=h.Languages[i];
                    if(l==null||l.Id!=i||l.Parent< -1||l.Parent>=i||l.Name==null||l.ExtinctYear< -1||l.ExtinctYear>s.Years||l.FoundedYear>s.Years||l.OriginRegion>=Patches.Length||
                       (l.LegacyOriginUnknown?(l.OriginRegion< -1||l.FoundedYear< -1):(l.OriginRegion<0||l.FoundedYear<0)))throw new ArgumentException("Invalid hominin language ancestry.");
                }
                if(h.Populations.Any(p=>h.Languages[p.Language].ExtinctYear>=0))throw new ArgumentException("A living population refers to an extinct communication tradition.");
            }
            var totals=new long[Patches.Length];foreach(var p in h.Populations)totals[p.Region]+=(long)Math.Round(p.People);
            if(!totals.SequenceEqual(s.Population)||s.Society.CellCulture.Length!=World.Surface.Cells.Length||s.Society.Cultures.Length!=h.Traditions.Length||s.Society.CellCulture.Any(c=>c< -1||c>=h.Traditions.Length))throw new ArgumentException("Early-hominin publication totals disagree with the authoritative cohorts.");
            foreach(var e in h.Events)if(e==null||e.Year<0||e.Year>s.Years||e.Region<0||e.Region>=Patches.Length||e.Lineage<0||e.Lineage>=h.Lineages.Length||e.Tradition<0||e.Tradition>=h.Traditions.Length||e.Kind==null||e.Text==null)throw new ArgumentException("Invalid hominin event provenance.");
        }
    }
}

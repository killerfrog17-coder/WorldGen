using System;

namespace WorldGen.Core
{
    // Opt-in history32 revision: regional traditions and local institutions are
    // different units. Sparse actor records never require a terrain census.
    [Serializable] public sealed class HistoricalRegionalTraditions
    {
        public int Version=1,SinceYear,FirstProtoYear=-1;
        public HistoricalCellTree Predecessors,RegionalCounts=new HistoricalCellTree(33554432),Retired=new HistoricalCellTree(33554432);
        public HistoricalCellTree Preparation=new HistoricalCellTree(33554432),LastReview=new HistoricalCellTree(33554432),Decline=new HistoricalCellTree(33554432),ProtoSince=new HistoricalCellTree(33554432),Qualified=new HistoricalCellTree(33554432);
        public HistoricalRegionalTraditions Copy()=>(HistoricalRegionalTraditions)MemberwiseClone();
        public static bool Proto(PrehistoryState s,int town)=>town>=0&&s?.History?.RegionalTraditions?.Qualified[town]==1;
        public static int RegionalCulture(PrehistoryState s,int cell)
        {
            var a=s?.History?.Aggregates;var rules=s?.History?.RegionalTraditions;
            if(rules==null||cell<0||cell>=HistoricalCells.Count(s))return -1;
            int region=a.Empires.Connections.CellRegions[cell];
            if(region<0||s.Population[region]+a.Living.Regions[region].SettledPopulation<=0)return -1;
            return a.Regions[region].Culture;
        }
        public static int Culture(PrehistoryState s,int cell)
        {
            var a=s.History.Aggregates;int raw=a.Cells[cell*5];
            // Cities/enclaves retain their own identities. Decentralized people
            // share a regional tradition, not a cell-by-cell paint frontier.
            if(raw>=0&&(a.TownAt[cell]>=0||a.Cells[cell*5+1]>=0||a.Cultures[raw].CivilizationSince>=0))return raw;
            return RegionalCulture(s,cell);
        }
        public static int Stage(PrehistoryState s,int cell)
        {
            var a=s.History.Aggregates;int raw=a.Cells[cell*5+1];
            if(raw>=0&&a.Resolve(raw,s.History)>=0)return 3;
            int town=a.TownAt[cell];if(town<0)town=a.Cells[cell*5+3];
            if(Proto(s,town)&&a.Living.Cities[town].AbandonedYear<0)return 2;
            return Culture(s,cell)>=0?1:0;
        }
    }
    public static class HistoricalProtoCentreEffects
    {
        // Model priors, not universal archaeological population definitions.
        // A supported village must sustain provisioning and coordination for
        // generations before it becomes a proto-urban institutional center.
        public static bool Provisioned(long residents,long rural,int farming,int institution,double fiscal,double reserve,bool supported,bool declining,int peers)
            =>residents>=1200&&farming>=250&&institution>=350&&fiscal>=.10&&reserve>=.30&&supported&&!declining&&(rural>=residents||peers>=2||residents>=2500);
        public static int Preparation(int previous,int elapsed,bool provisioned)=>Math.Max(0,Math.Min(600,previous+(provisioned?elapsed:-2*elapsed)));
        public static double Coordination(double fiscal,long residents,int institution,double water,int peers,double season)
            =>D.Clamp(fiscal/.30)*D.Clamp(residents/3500.0)*D.Clamp(institution/600.0)*(.60+.25*D.Clamp(water*4)+.15*D.Clamp(peers/4.0))*(.85+.15*(.25+.75*season));
        public static double AnnualOpportunity(double coordination,double environment,double staples,double homeland,int exposure)
            =>D.Q(Math.Min(.02,10*HistoricalLivingWorldOrigins.IndependentStateHazard(coordination,environment,staples,homeland)+2*HistoricalEnvironmentalEffects.LearnedStateHazard(coordination,environment,staples,exposure)),1000000000000000);
        public static double InstitutionalHazard(double coordination,int institutionalYears)
            =>D.Q(.0004*(.2+.8*D.Clamp(coordination/.5))*(.4+.6*D.Clamp(institutionalYears/1000.0)),1000000000000000);
    }
    public sealed partial class Prehistory
    {
        bool RegionalTraditions(PrehistoryState s)=>s?.History?.RegionalTraditions!=null;
        public PrehistoryState UpgradeRegionalTraditions(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Regional traditions require history32.");
            if(RegionalTraditions(source))return source;
            var s=source.Copy();InitializeRegionalTraditions(s);s.Seal();return s;
        }
        void InitializeRegionalTraditions(PrehistoryState s)
        {
            if(RegionalTraditions(s))return;
            var a=s.History.Aggregates;var rules=new HistoricalRegionalTraditions{SinceYear=s.Years,Predecessors=new HistoricalCellTree(a.Regions.Length)};
            s.History.RegionalTraditions=rules;
            foreach(var region in a.Regions)if(region.Culture>=0)
            {int id=region.Culture;rules.RegionalCounts=rules.RegionalCounts.With(id,Math.Max(0,rules.RegionalCounts[id])+1);}
            // Explicit upgrade alone may inspect actor records. Do not infer
            // proto-cities from historical/abandoned village counts.
            foreach(var polity in s.History.Polities)
                if(rules.FirstProtoYear<0||polity.FoundedYear<rules.FirstProtoYear)rules.FirstProtoYear=polity.FoundedYear;
            foreach(var c in s.Society.Cultures)
                if(a.Cultures[c.Id].CivilizationSince<0)c.Stage=SocietyStage.AdvancedCulture;
        }
        void RegisterRegionalTradition(PrehistoryState s,int region,int culture)
        {
            if(!RegionalTraditions(s))return;var rules=s.History.RegionalTraditions;var a=s.History.Aggregates;
            rules.RegionalCounts=rules.RegionalCounts.With(culture,Math.Max(0,rules.RegionalCounts[culture])+1);
            s.Society.Cultures[culture].AreaKm2+=a.Regions[region].AreaKm2;
        }
        void AdvanceRegionalTradition(PrehistoryState s,int region,int elapsed)
        {
            if(!RegionalTraditions(s))return;
            var a=s.History.Aggregates;var r=a.Regions[region];int id=r.Culture;if(id<0)return;
            var c=s.Society.Cultures[id];var ca=a.Cultures[id];var rules=s.History.RegionalTraditions;
            if(ca.CivilizationSince>=0||ca.Cities.Count>0||c.Farming>=180||s.Years-r.LastCultureYear<2500)return;
            // Loss of a recognizable interaction/ritual network is not human
            // extinction. Households and ancestry remain in the same region.
            double stress=s.Population[region]<400?3:1;
            if(HistoryRoll("regional-tradition-dissolution",region,s.Years)>=1-Math.Exp(-elapsed*stress/15000.0))return;
            r.Culture=-1;r.LastCultureYear=s.Years;rules.Predecessors=rules.Predecessors.With(region,id);
            int count=Math.Max(0,rules.RegionalCounts[id]-1);rules.RegionalCounts=rules.RegionalCounts.With(id,count);
            c.Population=Math.Max(0,c.Population-s.Population[region]);c.AreaKm2=Math.Max(0,c.AreaKm2-r.AreaKm2);
            if(count==0){rules.Retired=rules.Retired.With(id,s.Years);c.Stage=SocietyStage.Foragers;a.Empires.Connections.Cultures[id].DormantSince=s.Years;}
            HistoryEvent(s,"regional-tradition-decline",c.Name+" ceases to be a cohesive regional tradition here; its descendant communities continue locally.",r.BestCell,id,category:ChronicleCategory.Society);
        }
        bool AdoptRegionalTradition(PrehistoryState s,int region)
        {
            if(!RegionalTraditions(s))return false;
            var a=s.History.Aggregates;var r=a.Regions[region];var local=a.Living.Regions[region];int teacher=local.Teacher;
            if(teacher<0||local.Exposure<600||s.Years-local.LastExchangeYear>250||a.Cultures[teacher].CivilizationSince>=0||s.History.RegionalTraditions.Retired[teacher]>=0)return false;
            int from=s.Society.Cultures[teacher].OriginCell;
            double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[from],World.Ecology.Habitat[from],World.Ecology.Habitat[r.BestCell]);
            if(continuity<.75||!InstitutionalRouteAllowed(s,from,r.BestCell,teacher)||HistoryRoll("regional-shared-tradition",region,s.Years)>.6*continuity)return false;
            r.Culture=teacher;r.LastCultureYear=s.Years;RegisterRegionalTradition(s,region,teacher);s.Society.Cultures[teacher].Population+=s.Population[region];return true;
        }
        void AdvanceProtoCentre(PrehistoryState s,int id,int elapsed)
        {
            if(!RegionalTraditions(s))return;var h=s.History;var a=h.Aggregates;var rules=h.RegionalTraditions;var town=s.Society.Settlements[id];var city=a.Cities[id];
            if(a.Living.Cities[id].AbandonedYear>=0){rules.Qualified=rules.Qualified.With(id,0);return;}
            if(a.Resolve(town.PolityId,h)>=0)return;
            int peers=0;foreach(int peer in city.Neighbors)if(a.Living.Cities[peer].AbandonedYear<0&&s.Society.Settlements[peer].Population>=500)peers++;
            double fiscal=LivingCropLandscape(s,town.CultureId,town.Cell,true);
            bool supported=HistoricalProtoCentreEffects.Provisioned(town.Population,city.Rural+city.Suburban,s.Society.Cultures[town.CultureId].Farming,h.Cultures[town.CultureId].Institution,
                fiscal,town.FoodReserve/(double)Math.Max(1,town.Population),city.Supported,a.Living.Cities[id].DeclineYears>0,peers);
            int prior=rules.LastReview[id];int years=prior<0?0:Math.Min(elapsed,Math.Max(0,s.Years-prior));rules.LastReview=rules.LastReview.With(id,s.Years);
            int preparation=HistoricalProtoCentreEffects.Preparation(Math.Max(0,rules.Preparation[id]),years,supported);rules.Preparation=rules.Preparation.With(id,preparation);
            int decline=supported?0:Math.Max(0,rules.Decline[id])+years;rules.Decline=rules.Decline.With(id,decline);
            bool qualified=rules.Qualified[id]==1;int threshold=200+(int)(200*Rng.Value(World.Recipe.Seed,"proto-institution-maturation",id));
            double opportunity=0;
            if(!qualified&&supported&&preparation>=threshold)
            {
                double coordination=HistoricalProtoCentreEffects.Coordination(fiscal,town.Population,h.Cultures[town.CultureId].Institution,riverAccess[town.Cell],peers,landscapeSeason[town.Cell]);
                opportunity=HistoricalProtoCentreEffects.AnnualOpportunity(coordination,EnvironmentalEstablishment(s,town.Cell,town.CultureId),EnvironmentalStaples(s,town.CultureId,town.Cell),HomelandOriginFactor(s,town.Cell),h.Towns[id].CivicExampleExposure);
            }
            if(!qualified&&supported&&preparation>=threshold&&HistoryRoll("proto-institution-coordination",id,s.Years)<1-Math.Exp(-opportunity*years))
            {
                rules.Qualified=rules.Qualified.With(id,1);if(rules.ProtoSince[id]<0)rules.ProtoSince=rules.ProtoSince.With(id,s.Years);
                if(rules.FirstProtoYear<0)rules.FirstProtoYear=s.Years;
                HistoryEvent(s,"proto-urban-center",town.Name+" develops sustained provisioning, specialist coordination and a proto-urban center within "+s.Society.Cultures[town.CultureId].Name+".",town.Cell,town.CultureId,town:id,category:ChronicleCategory.Society);
            }
            else if(qualified&&decline>=80)
            {rules.Qualified=rules.Qualified.With(id,0);HistoryEvent(s,"proto-urban-decline",town.Name+" loses its proto-urban institutions; surviving village communities remain.",town.Cell,town.CultureId,town:id,category:ChronicleCategory.Society);}
        }
        void SeedRegionalCulturalFrontier(PrehistoryState s,int culture)
        {
            if(!RegionalTraditions(s))return;var actor=s.History.Aggregates.Cultures[culture];
            if(actor.CivilizationSince<0||actor.Frontier.Count>0||actor.Region<0)return;
            foreach(int cell in regionalCultureEdges[actor.Region])
                if(HistoricalCells.Culture(s,cell)==culture)RefreshAggregateFrontier(s,cell,0,culture);
        }
        void ValidateRegionalTraditions(PrehistoryState s)
        {
            var rules=s.History.RegionalTraditions;if(rules==null)return;
            if(!LivingWorldHistory(s)||rules.Version!=1||rules.SinceYear<0||rules.SinceYear>s.Years||rules.FirstProtoYear<-1||rules.FirstProtoYear>s.Years||rules.Predecessors?.Length!=Patches.Length)throw new ArgumentException("Invalid regional tradition rules.");
            foreach(var tree in new[]{rules.RegionalCounts,rules.Retired,rules.Preparation,rules.LastReview,rules.Decline,rules.ProtoSince,rules.Qualified})if(tree?.Length!=33554432)throw new ArgumentException("Invalid regional actor records.");
            var counts=new int[s.Society.Cultures.Length];foreach(var r in s.History.Aggregates.Regions)if(r.Culture>=0)counts[r.Culture]++;
            for(int id=0;id<counts.Length;id++)if(Math.Max(0,rules.RegionalCounts[id])!=counts[id])throw new ArgumentException("Regional tradition count mismatch.");
            int cultures=counts.Length,towns=s.Society.Settlements.Length;
            if(!rules.Predecessors.AllPresentValues((id,value)=>value>=-1&&value<cultures)||!rules.Retired.AllPresentValues((id,value)=>id<cultures&&value>=0&&value<=s.Years)||!rules.Preparation.AllPresentValues((id,value)=>id<towns&&value>=0&&value<=600)||!rules.LastReview.AllPresentValues((id,value)=>id<towns&&value>=0&&value<=s.Years)||!rules.Decline.AllPresentValues((id,value)=>id<towns&&value>=0&&value<=s.Years)||!rules.ProtoSince.AllPresentValues((id,value)=>id<towns&&value>=0&&value<=s.Years)||!rules.Qualified.AllPresentValues((id,value)=>id<towns&&(value==0||value==1&&s.History.Aggregates.Living.Cities[id].AbandonedYear<0)))throw new ArgumentException("Invalid regional tradition actor record.");
        }
    }
}

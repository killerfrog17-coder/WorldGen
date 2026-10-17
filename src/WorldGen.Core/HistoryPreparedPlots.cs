using System;

namespace WorldGen.Core
{
    // Prepared farmer/person-equivalents, NOT hectares of the much larger town
    // catchment. The persistent trees expose no writable pages.
    [Serializable] public sealed class HistoricalPreparedPlots
    {
        public int Version=1,SinceYear;
        public HistoricalCellTree Cells,RegionalCapacity,RegionalEpoch;
        public HistoricalPreparedPlots Copy()=>new HistoricalPreparedPlots{Version=Version,SinceYear=SinceYear,Cells=Cells,RegionalCapacity=RegionalCapacity,RegionalEpoch=RegionalEpoch};
    }
    public static class HistoricalPreparedFieldEffects
    {
        public static bool OrdinaryForest(HabitatCell h)=>(h.Habitat&Habitat.Forest)!=0&&h.Biome!=Biome.TropicalRainforest&&!HistoricalLandscapeEffects.EverWet(h);
        public static long Food(long farmers)=>(Math.Max(0,farmers)+1)/2;
        public static long Investment(long farmers)=>(Math.Max(0,farmers)+1)/2;
        public static long Remaining(long prepared,int vacantYear,int year)
            =>prepared<=0?0:vacantYear<0?prepared:year-vacantYear>=160?0:(long)Math.Floor(prepared*Math.Exp(-Math.Max(0,year-vacantYear)/40.0));
        public static double ClearingGain(HabitatCell h,double freshwater)
        {
            if(!OrdinaryForest(h))return 1;
            double fresh=D.Clamp(freshwater),soil=D.Clamp(h.SoilFertilityPermille/700.0),rain=D.Clamp((h.AridityMilli-100)/550.0)*soil;
            double alluvial=.9*(.55+.45*soil)*fresh,virgin=.7*rain*(1-fresh)+alluvial,prepared=rain*(1-fresh)+alluvial;
            return virgin<=0?1:prepared/virgin/.85;
        }
        public static double Landscape(double virgin,double gain,long prepared,long actualFarmers)
            =>D.Q(D.Clamp(virgin*(1+Math.Max(0,gain-1)*Math.Min(1,Math.Max(0,prepared)/(double)Math.Max(1,actualFarmers)))),1000000);
        public static long Capacity(long virgin,double gain,long prepared)
            =>virgin+Math.Min(Math.Max(0,virgin),(long)Math.Floor(Math.Min(Math.Max(0,virgin),Math.Max(0,prepared))*Math.Max(0,gain-1)));
    }
    public sealed partial class Prehistory
    {
        static bool PreparedFieldHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=16&&s.History.Aggregates.Empires.Connections.PreparedPlots!=null;
        sealed class PreparedFieldQuote
        {
            public int Cell,Culture,SourceTown,Owner;
            public long Farmers,NewPreparation,Food,Investment;
            public double Landscape;
        }
        void InitializePreparedPlots(PrehistoryState s)
        {
            var connections=s.History.Aggregates.Empires.Connections;if(connections.PreparedPlots!=null){if(connections.Realms.Version>=17)UpgradePreparedPlotLedger(s);return;}
            connections.PreparedPlots=new HistoricalPreparedPlots{SinceYear=s.Years,Cells=new HistoricalCellTree(CellPatch.Length*6),RegionalCapacity=new HistoricalCellTree(Patches.Length),RegionalEpoch=new HistoricalCellTree(Patches.Length)};
            // Explicit setup/upgrade only. Already occupied, working fields are
            // historical investment, not the entire unworked potential catchment.
            foreach(var town in s.Society.Settlements)
            {
                var city=s.History.Aggregates.Cities[town.Id];
                if(s.History.Aggregates.Living.Cities[town.Id].AbandonedYear<0&&ProvisionedPort(s,town.Id)==null&&city.Rural>=10&&town.CultivatedPermille>=50&&s.Society.Cultures[town.CultureId].Farming>=180&&HistoricalPreparedFieldEffects.OrdinaryForest(CropHabitat(s,town.Cell))&&LivingCropLandscape(s,town.CultureId,town.Cell,false)>0)
                    RecordPreparedField(s,town.Cell,Math.Min(city.Rural,city.RuralCapacity),town.Id);
            }
            if(connections.Realms.Version>=17)UpgradePreparedPlotLedger(s);
        }
        long RegionalPreparedCredit(HistoricalPreparedPlots plots,int region)
            =>plots.Version<2?Math.Max(0,plots.RegionalCapacity[region]):Math.Max(0,plots.RegionalCapacity[region*2])+((long)Math.Max(0,plots.RegionalCapacity[region*2+1])<<31);
        void SetRegionalPreparedCredit(HistoricalPreparedPlots plots,int region,long value)
        {
            value=Math.Max(0,value);
            if(plots.Version<2)plots.RegionalCapacity=plots.RegionalCapacity.With(region,(int)Math.Min(int.MaxValue,value));
            else plots.RegionalCapacity=plots.RegionalCapacity.With(region*2,(int)(value&int.MaxValue)).With(region*2+1,(int)(value>>31));
        }
        // Explicit setup/upgrade only: visit allocated plot rows, not every
        // terrain cell, and derive the exact long counter from its raw ledger.
        void UpgradePreparedPlotLedger(PrehistoryState s)
        {
            var plots=s.History.Aggregates.Empires.Connections.PreparedPlots;if(plots.Version>=2)return;
            var old=plots.Cells;var cells=old;var epochs=new HistoricalCellTree(Patches.Length);var totals=new long[Patches.Length];
            old.AllPresentValues((key,value)=>
            {
                if(key%6!=0)return true;int region=CellPatch[key/6];bool active=old[key+4]>=0||old[key+5]>=plots.RegionalEpoch[region];
                cells=cells.With(key+5,active?0:-1);epochs=epochs.With(region,0);
                if(active&&old[key+4]<0)totals[region]+=value;return true;
            });
            plots.Version=2;plots.Cells=cells;plots.RegionalEpoch=epochs;plots.RegionalCapacity=new HistoricalCellTree(Patches.Length*2);
            for(int region=0;region<totals.Length;region++)if(totals[region]>0)SetRegionalPreparedCredit(plots,region,totals[region]);
        }
        long PreparedFieldCapacity(PrehistoryState s,int cell)
        {
            if(!PreparedFieldHistory(s))return 0;var p=s.History.Aggregates.Empires.Connections.PreparedPlots;int key=cell*6;
            long capacity=Math.Max(0,p.Cells[key]);if(capacity==0)return 0;
            if(p.Cells[key+4]<0&&p.Cells[key+5]<p.RegionalEpoch[CellPatch[cell]])return 0;
            return HistoricalPreparedFieldEffects.Remaining(capacity,p.Cells[key+3],s.Years);
        }
        public long PreparedFarmersAt(PrehistoryState s,int cell)
            =>cell>=0&&cell<CellPatch.Length&&CellPatch[cell]>=0?PreparedFieldCapacity(s,cell):0;
        public long PreparedRegionalFarmersAt(PrehistoryState s,int region)
            =>PreparedFieldHistory(s)&&region>=0&&region<Patches.Length?RegionalPreparedCredit(s.History.Aggregates.Empires.Connections.PreparedPlots,region):0;
        void RecordPreparedField(PrehistoryState s,int cell,long capacity,int town)
        {
            var p=s.History.Aggregates.Empires.Connections.PreparedPlots;int key=cell*6,region=CellPatch[cell];
            long old=PreparedFieldCapacity(s,cell);bool wasRural=p.Cells[key+4]<0;
            bool revised=s.History.Aggregates.Empires.Connections.Realms.Version>=17&&p.Version>=2;
            long debit=revised&&wasRural?(p.Cells[key+5]>=p.RegionalEpoch[region]?Math.Max(0,p.Cells[key]):0):old;
            if(wasRural&&debit>0)SetRegionalPreparedCredit(p,region,RegionalPreparedCredit(p,region)-debit);
            capacity=Math.Min(int.MaxValue,Math.Max(0,capacity));
            p.Cells=p.Cells.With(key,(int)capacity).With(key+1,s.Years).With(key+2,s.Years).With(key+3,-1).With(key+4,town).With(key+5,Math.Max(0,p.RegionalEpoch[region]));
            if(town<0)SetRegionalPreparedCredit(p,region,RegionalPreparedCredit(p,region)+capacity);
        }
        double PreparedTownLandscape(PrehistoryState s,int town,bool storage)
        {
            var t=s.Society.Settlements[town];double virgin=LivingCropLandscape(s,t.CultureId,t.Cell,storage);
            if(!PreparedFieldHistory(s))return virgin;
            return HistoricalPreparedFieldEffects.Landscape(virgin,HistoricalPreparedFieldEffects.ClearingGain(CropHabitat(s,t.Cell),riverAccess[t.Cell]),PreparedFieldCapacity(s,t.Cell),s.History.Aggregates.Cities[town].Rural);
        }
        long PreparedTownRuralCapacity(PrehistoryState s,int town,long virgin)
        {
            if(!PreparedFieldHistory(s))return virgin;int cell=s.Society.Settlements[town].Cell;
            return HistoricalPreparedFieldEffects.Capacity(virgin,HistoricalPreparedFieldEffects.ClearingGain(CropHabitat(s,cell),riverAccess[cell]),PreparedFieldCapacity(s,cell));
        }
        PreparedFieldQuote QuotePreparedFields(PrehistoryState s,int culture,int cell,int sourceTown,long farmers,int sourceCell=-1,int carrierTown=-2)
        {
            if(!PreparedFieldHistory(s)||farmers<10||!HistoricalPreparedFieldEffects.OrdinaryForest(CropHabitat(s,cell))||s.Society.Cultures[culture].Farming<180)return null;
            double virgin=CarriedCropLandscape(s,culture,cell,false,carrierTown==-2?sourceTown:carrierTown,sourceCell),gain=HistoricalPreparedFieldEffects.ClearingGain(CropHabitat(s,cell),riverAccess[cell]);
            // Clearing cannot supply absent seed, water, heat or soil. The same
            // carried profile is tested before and after the paid preparation.
            if(virgin<=0||gain<=1)return null;
            var plots=s.History.Aggregates.Empires.Connections.PreparedPlots;int key=cell*6;
            // Occupied fields are not spare plots for another incoming cohort.
            // Only the same working town or genuinely vacated clearing can
            // reuse an existing paid allowance without preparing more land.
            bool reusable=s.History.Aggregates.TownAt[cell]==sourceTown||plots.Cells[key+3]>=0;
            long existing=reusable?Math.Min(farmers,PreparedFieldCapacity(s,cell)):0,work=Math.Max(0,farmers-existing);
            int owner=sourceTown>=0?s.History.Aggregates.Resolve(s.Society.Settlements[sourceTown].PolityId,s.History):-1;
            return new PreparedFieldQuote{Cell=cell,Culture=culture,SourceTown=sourceTown,Owner=owner,Farmers=farmers,NewPreparation=work,Food=HistoricalPreparedFieldEffects.Food(work),Investment=owner>=0?HistoricalPreparedFieldEffects.Investment(work):0,Landscape=HistoricalPreparedFieldEffects.Landscape(virgin,gain,farmers,farmers)};
        }
        bool PreparedQuoteAffordable(PrehistoryState s,PreparedFieldQuote quote,long otherFood,long otherInvestment)
        {
            if(quote==null||quote.SourceTown<0)return false;var source=s.Society.Settlements[quote.SourceTown];
            return s.History.Aggregates.Living.Cities[quote.SourceTown].AbandonedYear<0&&source.FoodReserve>=otherFood+quote.Food+source.Population/4&&(quote.Owner<0||s.History.Polities[quote.Owner].Treasury>=2*(otherInvestment+quote.Investment));
        }
        void CommitPreparedFields(PrehistoryState s,PreparedFieldQuote quote,int town)
        {
            if(quote==null)return;var source=s.Society.Settlements[quote.SourceTown];
            if(source.FoodReserve<quote.Food||quote.Owner>=0&&s.History.Polities[quote.Owner].Treasury<quote.Investment)throw new InvalidOperationException("Prepared fields lost their quoted real funding.");
            source.FoodReserve-=quote.Food;if(quote.Owner>=0)s.History.Polities[quote.Owner].Treasury-=quote.Investment;
            RecordPreparedField(s,quote.Cell,PreparedFieldCapacity(s,quote.Cell)+quote.NewPreparation,town);
        }
        void AdoptExistingPreparedTownFields(PrehistoryState s,int town)
        {
            if(!PreparedFieldHistory(s))return;var t=s.Society.Settlements[town];long rural=s.History.Aggregates.Cities[town].Rural,capacity=PreparedFieldCapacity(s,t.Cell);
            if(rural>0&&capacity>0)RecordPreparedField(s,t.Cell,capacity,town);
        }
        PreparedFieldQuote QuoteFoundingFields(PrehistoryState s,int culture,int cell,int parent)
        {
            if(!PreparedFieldHistory(s)||parent<0)return null;long available=LivingFounderAvailability(s,parent),urban=Math.Min(250,available/12),rural=Math.Min(available/3,Math.Max(800,urban*8));
            var quote=QuotePreparedFields(s,culture,cell,parent,rural);if(quote==null)return null;
            LivingInlandSettlementCosts(s,parent,cell,urban+rural,out long provisions,out long investment);
            return PreparedQuoteAffordable(s,quote,urban+rural+provisions,investment)?quote:null;
        }
        double FoundingFieldLandscape(PrehistoryState s,int culture,int cell,int parent)
            =>QuoteFoundingFields(s,culture,cell,parent)?.Landscape??CarriedCropLandscape(s,culture,cell,false,parent);
        PreparedFieldQuote QuoteRemoteFields(PrehistoryState s,int culture,int fromCell,int target,int owner,long farmers)
        {
            if(!PreparedFieldHistory(s))return null;var a=s.History.Aggregates;int source=a.Empires.Territories.Sources[fromCell];
            if(!TerritorialNodeLive(s,owner,source))return null;
            for(int depth=0;source<=-2&&depth<8;depth++)source=a.Empires.Territories.Posts[-source-2].ParentSource;
            if(source<0||source>=a.Cities.Length||!a.Cities[source].Supported||a.Living.Cities[source].AbandonedYear>=0)return null;
            var quote=QuotePreparedFields(s,culture,target,source,farmers,fromCell,-1);if(quote==null)return null;
            double effort=LivingTravelKm(s,s.Society.Settlements[source].Cell,fromCell,culture)+LivingTravelKm(s,fromCell,target,culture),transport=HistoricalModernEffects.LandTransport(s.History.Cultures[culture],s.Years);
            if(double.IsInfinity(effort)||effort>=double.MaxValue)return null;
            long food=HistoricalInlandSettlement.Provisions(farmers,effort,transport)+farmers/2,investment=HistoricalInlandSettlement.Investment(farmers,effort,transport);
            return PreparedQuoteAffordable(s,quote,food+s.Society.Settlements[source].Population/4,investment)?quote:null;
        }
        void AdvancePreparedTownFields(PrehistoryState s,int town,int elapsed)
        {
            if(!PreparedFieldHistory(s)||elapsed<=0)return;var t=s.Society.Settlements[town];var city=s.History.Aggregates.Cities[town];var p=s.History.Aggregates.Empires.Connections.PreparedPlots;int key=t.Cell*6;
            if(s.History.Aggregates.Living.Cities[town].AbandonedYear>=0||ProvisionedPort(s,town)!=null)return;
            if(!HistoricalPreparedFieldEffects.OrdinaryForest(CropHabitat(s,t.Cell)))return;
            if(city.Rural<10||t.CultivatedPermille<50||s.Society.Cultures[t.CultureId].Farming<180)
            {if(p.Cells[key]>0&&p.Cells[key+3]<0)p.Cells=p.Cells.With(key+3,s.Years);return;}
            int review=p.Cells[key+2];if(review>=0&&s.Years-review<10)return;
            long prepared=PreparedFieldCapacity(s,t.Cell);
            // Revision17 keeps only actually maintained farming-person capacity
            // after partial rural collapse. Frozen16 retains its old estate
            // allowance; this is not a new clock or a whole-town-area rewrite.
            if(s.History.Aggregates.Empires.Connections.Realms.Version>=17&&prepared>city.Rural)
            {prepared=city.Rural;RecordPreparedField(s,t.Cell,prepared,town);}
            if(p.Cells[key+3]>=0&&prepared>0){prepared=Math.Min(prepared,city.Rural);RecordPreparedField(s,t.Cell,prepared,town);}
            long work=Math.Min(Math.Max(0,city.Rural-prepared),(long)(city.Rural*Math.Min(10,review<0?elapsed:s.Years-review)*.05));
            var quote=QuotePreparedFields(s,t.CultureId,t.Cell,town,prepared+work);
            p.Cells=p.Cells.With(key+2,s.Years);
            if(quote!=null&&work>0&&PreparedQuoteAffordable(s,quote,t.Population*2,0))CommitPreparedFields(s,quote,town);
        }
        void VacatePreparedTownFields(PrehistoryState s,int town)
        {
            if(!PreparedFieldHistory(s))return;var p=s.History.Aggregates.Empires.Connections.PreparedPlots;int key=s.Society.Settlements[town].Cell*6;
            if(p.Cells[key]>=0&&(s.History.Aggregates.Empires.Connections.Realms.Version<17||p.Version<2||p.Cells[key+4]==town))p.Cells=p.Cells.With(key+3,s.Years);
        }
        long PreparedRegionalCapacity(PrehistoryState s,int region,long ordinary)
        {
            if(!PreparedFieldHistory(s))return ordinary;var p=s.History.Aggregates.Empires.Connections.PreparedPlots;
            if(s.Population[region]<=0)
            {if(RegionalPreparedCredit(p,region)>0){int epoch=s.History.Aggregates.Empires.Connections.Realms.Version>=17&&p.Version>=2?checked(Math.Max(0,p.RegionalEpoch[region])+1):s.Years;SetRegionalPreparedCredit(p,region,0);p.RegionalEpoch=p.RegionalEpoch.With(region,epoch);}return ordinary;}
            // Only the paid small-plot cohort, not every resident of an enormous
            // ecoregion, receives this additive allowance.
            return ordinary+Math.Min(RegionalPreparedCredit(p,region),s.Population[region]);
        }
        void ValidatePreparedPlots(PrehistoryState s)
        {
            var p=s.History.Aggregates.Empires.Connections.PreparedPlots;if(p==null)return;
            bool FieldValue(int key,int value)
            {
                if(CellPatch[key/6]<0||value<0)return false;
                if(key%6==0)return true;
                if(key%6==4)return value<s.Society.Settlements.Length&&s.Society.Settlements[value].Cell==key/6;
                if(key%6==5&&p.Version>=2)return true;
                return value<=s.Years;
            }
            if(p.Version<1||p.Version>2||p.SinceYear<0||p.SinceYear>s.Years||p.Cells?.Length!=CellPatch.Length*6||p.RegionalCapacity?.Length!=Patches.Length*(p.Version>=2?2:1)||p.RegionalEpoch?.Length!=Patches.Length||!p.Cells.AllPresentValues(FieldValue)||!p.RegionalCapacity.AllPresentValues((region,value)=>value>=0)||!p.RegionalEpoch.AllPresentValues((region,epoch)=>epoch>=0&&(p.Version>=2||epoch<=s.Years)))throw new ArgumentException("Invalid bounded prepared fields.");
            var rural=new long[Patches.Length];
            p.Cells.AllPresentValues((key,value)=>{if(key%6==0&&p.Cells[key+4]<0&&p.Cells[key+5]>=p.RegionalEpoch[CellPatch[key/6]])rural[CellPatch[key/6]]+=value;return true;});
            for(int region=0;region<rural.Length;region++)if((p.Version<2?Math.Min(int.MaxValue,rural[region]):rural[region])!=RegionalPreparedCredit(p,region))
            {
                int badRegion=region,shown=0;var details=new System.Text.StringBuilder();
                p.Cells.AllPresentValues((key,value)=>{if(key%6==0&&CellPatch[key/6]==badRegion&&shown++<8)details.Append(" cell=").Append(key/6).Append(" raw=").Append(value).Append(" effective=").Append(PreparedFieldCapacity(s,key/6)).Append(" holder=").Append(p.Cells[key+4]).Append(" vacant=").Append(p.Cells[key+3]).Append(" epoch=").Append(p.Cells[key+5]).Append(';');return true;});
                var error=new ArgumentException("Prepared rural plot capacity conservation: year="+s.Years+" region="+region+" expectedRaw="+rural[region]+" recorded="+RegionalPreparedCredit(p,region)+" regionEpoch="+p.RegionalEpoch[region]+"."+details);
                error.Data["PreparedRegion"]=region;throw error;
            }
        }
    }
}

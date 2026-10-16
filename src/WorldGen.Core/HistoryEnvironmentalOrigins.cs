using System;
using System.Linq;
using System.Collections.Generic;

namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalOriginReach
    {
        public int DistantResidenceYear=-1,DistantRegion=-1;
        public HistoricalOriginReach Copy()=>(HistoricalOriginReach)MemberwiseClone();
    }
    // Explicit game priors. Environment affects production, storage and actual
    // exchange, not human intelligence. No Koppen/continent/racial whitelist.
    public static class HistoricalEnvironmentalEffects
    {
        public static double WetForestBurden(HabitatCell h,double rainSeason)
        {
            double heat=D.Clamp((h.ColdTenthsC-120)/100.0),wet=D.Clamp((h.AridityMilli-650)/650.0);
            double evergreen=heat*wet*(1-.75*D.Clamp(rainSeason));
            if(h.Biome==Biome.TropicalRainforest)evergreen=Math.Max(evergreen,.85*(1-.35*D.Clamp(rainSeason)));
            return D.Q(D.Clamp(evergreen),1000000);
        }
        public static double Establishment(HabitatCell h,double rainSeason,double freshwater,int practiceYears=0,double management=0)
        {
            double practice=D.Clamp(practiceYears/20000.0),managed=D.Clamp(management);
            double growing=.01+.99*Math.Pow(D.Clamp((h.GrowingDays-70+35*managed)/160.0),2);
            // Above-freezing days are not equivalent to a productive crop season:
            // a cool nine-month season must not beat a warm harvest belt merely
            // because it also has excellent winter storage. Later adaptation helps.
            double thermal=.01+.99*Math.Pow(D.Clamp((h.MeanTenthsC-50+40*managed+20*practice)/120.0),3);
            double soil=.06+.94*Math.Pow(D.Clamp(h.SoilFertilityPermille/650.0),2);
            if((h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent)&&h.FloodFractionPermille>=10)soil=Math.Max(soil,.7);
            double burden=WetForestBurden(h,rainSeason);
            double forest=.002+.998*Math.Pow(1-burden,4);
            forest+=(1-forest)*(.03*practice+.65*managed);
            double harvest=.10+.90*Math.Max(D.Clamp((220-h.ColdTenthsC)/140.0),.8*D.Clamp(rainSeason));
            double water=.002+.998*D.Clamp(h.AridityMilli/550.0+freshwater*12);
            double frost=Math.Pow(D.Clamp(1-h.PermafrostPermille/700.0),2);
            return D.Q(Math.Max(.0000001,growing*thermal*soil*forest*harvest*water*frost),1000000000);
        }
        public static double HomelandMultiplier(double km,bool distantPopulation)
        {
            if(!distantPopulation)return 1;
            double x=D.Clamp((km-1500)/2000.0),smooth=x*x*(3-2*x);
            return D.Q(.00001+.99999*smooth*smooth,1000000000);
        }
        public static double LatitudeTransfer(double sourceLatitude,double targetLatitude,double selection,bool perennial=false)
        {
            double difference=Math.Abs(Math.Abs(sourceLatitude)-Math.Abs(targetLatitude));
            double tolerance=(perennial?24:9)+15*D.Clamp(selection);
            return D.Q(Math.Exp(-difference/tolerance),1000000);
        }
        public static double IndependentStateHazard(double core,double environment,double staples,double homeland)
            =>Math.Max(.000000000000001,D.Q((.000000002+.00015*Math.Pow(D.Clamp(core),8))*environment*Math.Pow(.01+.99*D.Clamp(staples),2)*homeland,1000000000000000));
        public static double LearnedStateHazard(double core,double environment,double staples,int exposure)
            =>D.Q(.008*Math.Pow(D.Clamp(core),2)*D.Clamp(exposure/1000.0)*environment*(.05+.95*D.Clamp(staples)),1000000000000000);
        public static double CulturalAdoption(double environment,double management)
            =>D.Q(.000001+.999999*Math.Pow(D.Clamp(environment+(1-environment)*D.Clamp(management)),.75),1000000000);
        public static double TraversalCost(HabitatCell h,double rainSeason,double management)
        {
            double jungle=WetForestBurden(h,rainSeason),dry=D.Clamp((220-h.AridityMilli)/220.0)*(1-D.Clamp(h.FloodFractionPermille/100.0));
            double forest=(h.Habitat&Habitat.Forest)!=0?1:0;
            return D.Q(1+(25*jungle*jungle+1.5*forest+8*dry*dry+8*Math.Pow(D.Clamp(h.ReliefM/1800.0),2)+4*h.PermafrostPermille/1000.0)*(1-.75*D.Clamp(management)),1000000);
        }
    }
    public sealed partial class Prehistory
    {
        public const string EnvironmentalHistoryVersion="history-31";
        static bool EnvironmentalHistory(PrehistoryState s)=>s?.History?.Version==EnvironmentalHistoryVersion||LivingWorldHistory(s);
        readonly Dictionary<int,int> environmentalNativeMasks=new Dictionary<int,int>();
        readonly Queue<int> environmentalNativeOrder=new Queue<int>();
        public PrehistoryState UpgradeToEnvironmentalHistory(PrehistoryState source)
        {
            Validate(source);if(EnvironmentalHistory(source))return source;
            source=UpgradeToLanguageHistory(source);var s=source.Copy();s.History.Version=EnvironmentalHistoryVersion;s.History.OriginReach=new HistoricalOriginReach();
            // Explicit migration/setup only; ordinary reach updates inspect one actor.
            for(int r=0;r<Patches.Length;r++)RecordDistantResidence(s,r);
            s.Seal();return s;
        }
        double OriginDistanceKm(PrehistoryState s,int cell)=>D.Angle(World.Surface.Cells[Patches[s.Origin].Cell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
        void RecordDistantResidence(PrehistoryState s,int region)
        {
            if(!EnvironmentalHistory(s)||s.History.OriginReach.DistantResidenceYear>=0)return;
            if(s.Population[region]+s.History.Aggregates.Living.Regions[region].SettledPopulation>=100&&OriginDistanceKm(s,Patches[region].Cell)>=3500)
            {s.History.OriginReach.DistantResidenceYear=s.Years;s.History.OriginReach.DistantRegion=region;}
        }
        double HomelandOriginFactor(PrehistoryState s,int cell)=>HistoricalEnvironmentalEffects.HomelandMultiplier(OriginDistanceKm(s,cell),s.History.OriginReach.DistantResidenceYear>=0);
        double EnvironmentalOriginRoll(string stream,int id,int year)
        {
            // Rare origins need more than the ordinary24-bit sampling grid;
            // otherwise its zero bucket overwhelms tiny ecological penalties.
            var r=new Rng(World.Recipe.Seed,"environment-origin/"+stream,unchecked(id*7919+year*104729));
            return ((r.Next()>>5)*67108864.0+(r.Next()>>6))/9007199254740992.0;
        }
        double EnvironmentalManagement(PrehistoryState s,int culture)
        {
            if(culture<0)return 0;var c=s.History.Cultures[culture];
            return D.Clamp(.35*HistoricalModernEffects.Adoption(c,HistoricalTechnology.CropRotation,s.Years)+.35*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Sanitation,s.Years)+.30*HistoricalModernEffects.Adoption(c,HistoricalTechnology.ArtificialFertilizers,s.Years));
        }
        double EnvironmentalEstablishment(PrehistoryState s,int cell,int culture=-1)
        {
            int practice=0;
            if(culture>=0){var a=s.History.Agriculture[culture];for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0)practice=Math.Max(practice,a.HusbandryYears?[k]??0);}
            return HistoricalEnvironmentalEffects.Establishment(World.Ecology.Habitat[cell],landscapeSeason[cell],riverAccess[cell],practice,EnvironmentalManagement(s,culture));
        }
        double EnvironmentalWildResources(PrehistoryState s,int cell)
        {
            if(!environmentalNativeMasks.TryGetValue(cell,out int mask))
            {
                mask=CommunityNativeCropMask(s,cell);
                if(environmentalNativeMasks.Count>=16384)environmentalNativeMasks.Remove(environmentalNativeOrder.Dequeue());
                environmentalNativeMasks[cell]=mask;environmentalNativeOrder.Enqueue(cell);
            }
            double resources=(mask&7)!=0?.8:(mask&(1<<5))!=0?.5:(mask&24)!=0?.18:0;
            // Rich fisheries may support specialized forager cultures, not a free state.
            return .02+.98*Math.Max(resources,.25*D.Clamp(riverAccess[cell]*3));
        }
        double EnvironmentalStaples(PrehistoryState s,int culture,int cell)
        {
            var a=s.History.Agriculture[culture];double best=0;
            for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0&&a.Cultivars[k]!=null)
            {
                double fit=CommunityTransferFit(s,cell,a.Cultivars[k],HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation));
                double collectable=k<3?1:k==5?.65:k==3?.16:.18;best=Math.Max(best,fit*collectable);
            }
            return D.Clamp(best);
        }
        double EnvironmentalCropTransfer(PrehistoryState s,int cell,HistoricalCultivar cultivar)
        {
            var crop=HistoricalCropCatalog.Crops[cultivar.CropId];
            // Existing sensitive crops already have a photoperiod factor. Add the
            // missing annual-cereal/legume adaptation cost, not a duplicate penalty.
            if(crop.DaylengthSensitive)return 1;
            return HistoricalEnvironmentalEffects.LatitudeTransfer(cultivar.DaylengthLatitudeTenths/10.0,CropLatitude(cell),cultivar.SelectedYears/12000.0,crop.Group==4);
        }
        double EnvironmentalTransmission(PrehistoryState s,int from,int to,int culture,bool sea=false)
        {
            if(from==to)return 1;
            var hc=s.History.Cultures[culture];double transport=HistoricalModernEffects.LandTransport(hc,s.Years)*(1+1.5*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)),management=EnvironmentalManagement(s,culture);
            double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm,cost=0;
            if(!sea)
            {
                var start=World.Surface.Cells[from].Position;var end=World.Surface.Cells[to].Position;
                // Fixed8-sample static raster query. No fine-cell path/census.
                for(int k=0;k<8;k++)
                {
                    double t=(k+.5)/8;var point=(start*(1-t)+end*t).Unit;
                    int x=Math.Max(0,Math.Min(OceanRasterWidth-1,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*OceanRasterWidth))),y=Math.Max(0,Math.Min(OceanRasterHeight-1,(int)((.5-Math.Asin(point.Z)/Math.PI)*OceanRasterHeight)));
                    int cell=aggregateTerrainRaster[y*OceanRasterWidth+x];
                    cost+=World.Hydrology.OceanConnected[cell]?8:HistoricalEnvironmentalEffects.TraversalCost(World.Ecology.Habitat[cell],landscapeSeason[cell],management);
                }
                cost=1+(Math.Max(1,cost/8)-1)*Math.Min(3,km/150.0)/Math.Max(1,transport);
            }
            else cost=1;
            double latitude=HistoricalEnvironmentalEffects.LatitudeTransfer(CropLatitude(from),CropLatitude(to),management);
            // Learning industrial knowledge need not transport the founder crop.
            double communication=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years);
            latitude+=(1-latitude)*communication;cost=1+(cost-1)*(1-.9*communication);
            return D.Q(Math.Max(.00001,latitude/cost),1000000000);
        }
        int EnvironmentalContactAnchor(PrehistoryState s,int culture)
        {var sites=s.History.Aggregates.Cultures[culture].Cities;return sites.Count>0?s.Society.Settlements[sites.At(0)].Cell:s.Society.Cultures[culture].OriginCell;}
        int EnvironmentalContactYear(PrehistoryState s,int receiver,int sender)
        {var m=s.History.Aggregates.Provinces.Exchanges[receiver];int at=Array.IndexOf(m.Peers,sender);return at<0?-1:m.LastYear[at];}
        bool EnvironmentalDirectContact(PrehistoryState s,int first,int second,int from,int to)
        {
            if(from<0)from=EnvironmentalContactAnchor(s,first);if(to<0)to=EnvironmentalContactAnchor(s,second);
            bool sea=DiffusionShore(from)&&DiffusionShore(to)&&DiffusionSeaEffort(s,from,to,first)!=double.MaxValue;
            double transmission=EnvironmentalTransmission(s,from,to,first,sea);
            return HistoryRoll("environment-contact-"+second,first,s.Years)<transmission;
        }
        void ValidateOriginReach(PrehistoryState s)
        {
            var r=s.History.OriginReach;if(!EnvironmentalHistory(s)){if(r!=null)throw new ArgumentException("Legacy origin reach extension.");return;}
            if(r==null||r.DistantResidenceYear< -1||r.DistantResidenceYear>s.Years||r.DistantRegion< -1||r.DistantRegion>=Patches.Length||(r.DistantRegion<0)!=(r.DistantResidenceYear<0)||r.DistantRegion>=0&&OriginDistanceKm(s,Patches[r.DistantRegion].Cell)<3500)throw new ArgumentException("Invalid distant population provenance.");
        }
    }
}

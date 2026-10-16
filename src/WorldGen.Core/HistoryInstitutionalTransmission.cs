using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    // Bounded, physical delivery rules. Coefficients are game priors, not a
    // reconstruction of a historical route, an origin quota or a continent ban.
    public static class HistoricalInstitutionalTransmission
    {
        public static double OpenWaterLimitKm(double sailing,double navigation,double oceanNavigation,int navigationLevel=1)
            =>oceanNavigation>=.20?14000:navigation>=.20?Math.Min(800,250+100*Math.Max(1,navigationLevel)):sailing>=.15?120:35;
        public static double RelayExposure(double sourceExposure,double transmission,double elapsed)
            =>sourceExposure<=0?0:D.Q(2*Math.Max(0,elapsed)*(.25+.75*D.Clamp(sourceExposure/500))*D.Clamp(transmission),1000000);
        public static int RetainedExposure(int exposure,int elapsed,bool practiced,bool inhabited)
            =>practiced?exposure:Math.Max(0,exposure-Math.Max(1,elapsed/(inhabited?100:2)));
        public static double LocalLearning(double sourceExposure,double transmission,double elapsed)
            =>3*Math.Max(0,elapsed)*D.Clamp(sourceExposure/350)*D.Clamp(transmission);
        public static double RegularOceanDelivery(double unsupportedKm,int navigationLevel,double oceanInfrastructure)
            =>Math.Exp(-Math.Max(0,unsupportedKm)/(350+100*Math.Max(0,navigationLevel)+6000*Math.Max(0,oceanInfrastructure)));
    }
    public sealed partial class Prehistory
    {
        byte[] institutionalShoreDistance;
        readonly Dictionary<long,(double Open,bool Water)> institutionalWaterCache=new Dictionary<long,(double,bool)>();
        readonly Queue<long> institutionalWaterOrder=new Queue<long>();
        void PrepareInstitutionalShoreDistance()
        {
            if(institutionalShoreDistance!=null)return;
            // Fixed32768-node setup, never a fine-cell route or periodic census.
            int width=OceanRasterWidth,height=OceanRasterHeight;var d=new byte[width*height];var q=new Queue<int>();
            for(int i=0;i<d.Length;i++){d[i]=255;if(!aggregateOceanRaster[i]){d[i]=0;q.Enqueue(i);}}
            while(q.Count>0)
            {
                int at=q.Dequeue(),x=at%width,y=at/width,nextDistance=Math.Min(254,d[at]+1);
                void Offer(int node){if(d[node]<=nextDistance)return;d[node]=(byte)nextDistance;q.Enqueue(node);}
                Offer(y*width+(x+1)%width);Offer(y*width+(x+width-1)%width);if(y>0)Offer(at-width);if(y+1<height)Offer(at+width);
            }
            institutionalShoreDistance=d;
        }
        int InstitutionalRasterNode(V point)
        {
            int x=Math.Max(0,Math.Min(OceanRasterWidth-1,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*OceanRasterWidth)));
            int y=Math.Max(0,Math.Min(OceanRasterHeight-1,(int)((.5-Math.Asin(point.Z)/Math.PI)*OceanRasterHeight)));
            return y*OceanRasterWidth+x;
        }
        double InstitutionalUnsupportedWaterKm(int from,int to,out bool crossesWater)
        {
            long key=((long)Math.Min(from,to)<<32)|(uint)Math.Max(from,to);
            if(institutionalWaterCache.TryGetValue(key,out var known)){crossesWater=known.Water;return known.Open;}
            // Canonical direction makes a cold/warm cache and reverse delivery
            // agree even at a raster boundary.
            if(from>to){int swap=from;from=to;to=swap;}
            PrepareInstitutionalShoreDistance();var start=World.Surface.Cells[from].Position;var end=World.Surface.Cells[to].Position;
            double km=D.Angle(start,end)*World.Recipe.RadiusKm,run=0,longest=0;crossesWater=false;
            // Use64 equally spaced angular samples; normalized linear samples
            // over-compress the middle of long ocean passages.
            double angle=D.Angle(start,end),sin=Math.Sin(angle);
            for(int k=0;k<64;k++)
            {
                double t=(k+.5)/64;var point=Math.Abs(sin)<.000001?(start*(1-t)+end*t).Unit:(start*(Math.Sin((1-t)*angle)/sin)+end*(Math.Sin(t*angle)/sin)).Unit;
                int node=InstitutionalRasterNode(point);if(aggregateOceanRaster[node])crossesWater=true;
                // One coarse raster step can be a shore/strait ambiguity. Open
                // water unsupported by nearby land is tracked separately.
                if(institutionalShoreDistance[node]>=2){run+=km/64;longest=Math.Max(longest,run);}else run=0;
            }
            double result=D.Q(longest,1000);if(institutionalWaterCache.Count>=16384)institutionalWaterCache.Remove(institutionalWaterOrder.Dequeue());
            institutionalWaterCache.Add(key,(result,crossesWater));institutionalWaterOrder.Enqueue(key);return result;
        }
        bool InstitutionalRouteAllowed(PrehistoryState s,int from,int to,int culture)
        {
            if(culture<0||from<0||to<0||from>=World.Surface.Cells.Length||to>=World.Surface.Cells.Length)return false;if(from==to)return true;
            var hc=s.History.Cultures[culture];double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
            double open=InstitutionalUnsupportedWaterKm(from,to,out bool water);
            bool same=World.Landmass[from]>=0&&World.Landmass[from]==World.Landmass[to];
            // A land-only route cannot silently walk an ocean channel. Tiny
            // coarse-raster coastal ambiguities retain the existing land test.
            if(!same||water&&open>35)
            {
                if(!DiffusionShore(from)||!DiffusionShore(to)||DiffusionSeaEffort(s,from,to,culture)==double.MaxValue)return false;
                double limit=HistoricalInstitutionalTransmission.OpenWaterLimitKm(
                    HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years),
                    HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),
                    HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),hc.Technical?.Levels[(int)HistoricalTechnology.Navigation]??1);
                return open<=limit;
            }
            double transmission=EnvironmentalTransmission(s,from,to,culture);
            return km/Math.Max(.00001,transmission)<=600*HistoricalModernEffects.LandTransport(hc,s.Years);
        }
        double InstitutionalLocalExample(PrehistoryState s,int teacher,int from,int to)
        {
            if(teacher<0||teacher>=s.History.Polities.Length)return 0;
            int culture=s.History.Polities[teacher].CultureId;if(!InstitutionalRouteAllowed(s,from,to,culture))return 0;
            bool sea=DiffusionShore(from)&&DiffusionShore(to)&&DiffusionSeaEffort(s,from,to,culture)!=double.MaxValue;
            double transmission=EnvironmentalTransmission(s,from,to,culture,sea);
            double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
            if(sea&&km>300)
            {
                int receiver=HistoricalCells.Culture(s,to);
                if(receiver>=0&&receiver!=culture&&!ProvincialRecentContact(s,receiver,culture,true))return 0;
            }
            double transport=HistoricalModernEffects.LandTransport(s.History.Cultures[culture],s.Years);
            return D.Q(transmission*Math.Exp(-km/(450*Math.Max(1,transport))),1000000);
        }
        double InstitutionalRegionalExample(PrehistoryState s,int region,int teacher)
        {
            var a=s.History.Aggregates;int court=ProvincialRegionalCourt(s,region);
            if(court>=0)return court==teacher?1000:0;
            var local=a.Living.Regions[region];if(local.CivicTeacher!=teacher)return 0;
            int culture=a.Regions[region].Culture;
            bool practiced=culture>=0&&s.Society.Cultures[culture].Farming>=180&&ProvincialContactResidents(s,region)>=100;
            if(!practiced&&s.Years-local.LastExchangeYear>300)return 0;
            return local.CivicExposure;
        }
        void AccumulateInstitutionalDelivery(PrehistoryState s,int region,int teacher,double source,double transmission,int elapsed,string stream)
        {
            if(source<=0||transmission<=0)return;var local=s.History.Aggregates.Living.Regions[region];
            double expected=HistoricalInstitutionalTransmission.RelayExposure(source,transmission,elapsed);int gain=(int)expected;
            if(HistoryRoll(stream,region,s.Years)<expected-gain)gain++;
            if(gain<=0)return;
            // Ordered local practice accumulates from actual delivered lessons;
            // it is not capped to a shrinking fraction of every former teacher.
            // The last contributor records provenance, not a remote live hotline.
            local.CivicTeacher=teacher;local.CivicExposure=Math.Min(1000,local.CivicExposure+gain);
        }
        bool InstitutionalVoyageDelivery(PrehistoryState s,Route route,int culture,int sourceState,int sourceTown)
        {
            if(route.CellA<0||route.CellB<0)return false;
            if(route.DistanceM<=120000)return true;
            var a=s.History.Aggregates;int port=sourceTown;
            if(port<0)foreach(int candidate in NearbyLivingTowns(s,route.CellA,180))
            {
                var t=s.Society.Settlements[candidate];
                if(!a.Cities[candidate].Supported||a.Living.Cities[candidate].AbandonedYear>=0||t.Population<300||!DiffusionShore(t.Cell))continue;
                if(t.CultureId!=culture&&(sourceState<0||a.Resolve(t.PolityId,s.History)!=sourceState))continue;
                port=candidate;break;
            }
            // Occasional forager voyages still move people below. A region's
            // remote population is not a free provisioned departure port.
            if(port<0||!a.Cities[port].Supported||a.Living.Cities[port].AbandonedYear>=0)return false;
            var town=s.Society.Settlements[port];var hc=s.History.Cultures[culture];
            long provisions=Math.Max(24,route.DistanceM/500000*24);if(town.FoodReserve<provisions+town.Population/4)return false;
            double open=InstitutionalUnsupportedWaterKm(route.CellA,route.CellB,out _);
            double infrastructure=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years)
                +HistoricalModernEffects.NetworkUse(hc,HistoricalTechnology.SteamPower,s.Years)
                +HistoricalModernEffects.NetworkUse(hc,HistoricalTechnology.CombustionEngines,s.Years);
            double probability=HistoricalInstitutionalTransmission.RegularOceanDelivery(open,hc.Technical?.Levels[(int)HistoricalTechnology.Navigation]??1,infrastructure);
            if(HistoryRoll("institutional-provisioned-delivery-"+route.B,route.A,s.Years)>=probability)return false;
            town.FoodReserve-=provisions;return true;
        }
    }
}

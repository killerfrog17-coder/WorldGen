using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public enum HarvestHazard { None,Drought,Flood,ColdSeason }
    public readonly struct HistoricalShipment
    {
        public readonly long Sent,Received,Lost;
        public HistoricalShipment(long sent,long received){Sent=sent;Received=received;Lost=sent-received;}
    }
    public static class HistoricalFoodEffects
    {
        public static double GrowthPressure(long population,long localCapacity,long importedFood)
            =>D.Q(D.Clamp(1-population/(double)Math.Max(1,localCapacity+importedFood),-1,1),1000000);
        public static long RemainingReserve(long previousReserve,long capacity,long annualFood,long population)
            =>Math.Max(0,Math.Min(Math.Max(previousReserve,Math.Max(capacity,annualFood)*3),previousReserve+annualFood-population));
        public static HistoricalShipment Deliver(long surplus,long need,int retentionPermille,long cargoLimit)
        {
            if(surplus<=0||need<=0||cargoLimit<=0||retentionPermille<=0)return new HistoricalShipment(0,0);
            int retained=Math.Min(1000,retentionPermille);
            long sent=(long)Math.Min((decimal)Math.Min(surplus,cargoLimit),decimal.Ceiling((decimal)need*1000/retained));
            return new HistoricalShipment(sent,(long)((decimal)sent*retained/1000));
        }
        public static double ResilientLoss(double severity,int diverseCrops,int publicWorks,bool irrigation,bool river)
            =>D.Q(D.Clamp(severity)*(1-Math.Min(.30,Math.Max(0,diverseCrops-1)*.06))*(1-.15*D.Clamp(publicWorks/1000.0))*(irrigation&&river?.78:1),1000000);
        public static double ShockWeight(V cell,V center,double radiusKm,double planetRadiusKm)
            =>D.Q(D.Kernel(D.Angle(cell,center)*planetRadiusKm,Math.Max(1,radiusKm)),1000000);
    }
    public sealed partial class Prehistory
    {
        sealed class HistoricalFoodYear
        {
            public long[] Harvest,Received;public System.Collections.Generic.HashSet<(int,int)> Voyages=new System.Collections.Generic.HashSet<(int,int)>();
        }
        sealed class HarvestEvent
        {
            public V Center;public int Start,End;public double RadiusKm,Severity;public HarvestHazard Kind;
        }
        int harvestPeriod=-1;HarvestEvent[] harvestEvents;
        void PrepareHarvestEvents(int year)
        {
            int period=year/16;if(harvestPeriod==period)return;
            var events=new HarvestEvent[8];
            for(int i=0;i<events.Length;i++)
            {
                var rng=new Rng(World.Recipe.Seed,"history-6-harvest-events",unchecked(period*31+i));
                double z=rng.Unit()*2-1,longitude=rng.Unit()*2*D.Pi,r=D.Sqrt(1-z*z);
                int duration=3+(int)(rng.Unit()*7),start=period*16+(int)(rng.Unit()*(17-duration));
                events[i]=new HarvestEvent{Center=new V(r*D.Sin(longitude+D.Pi/2),r*D.Sin(longitude),z),Start=start,End=start+duration-1,RadiusKm=800+1000*rng.Unit(),Severity=.30+.40*rng.Unit(),Kind=(HarvestHazard)(1+(int)(rng.Unit()*3))};
            }
            harvestEvents=events;harvestPeriod=period;
        }
        double HistoricalHarvestWeather(PrehistoryState s,Settlement town,out HarvestHazard hazard)
        {
            PrepareHarvestEvents(s.Years);int cell=town.Cell;var habitat=World.Ecology.Habitat[cell];var culture=s.Society.Cultures[town.CultureId];
            double weather=.84+.24*HistoryRoll("regional-harvest",CellPatch[cell],s.Years/5)+.08*HistoryRoll("local-harvest",town.Id,s.Years);
            double maximum=0;hazard=HarvestHazard.None;
            foreach(var e in harvestEvents)
            {
                if(s.Years<e.Start||s.Years>e.End)continue;
                double susceptibility=e.Kind==HarvestHazard.Drought?.15+.55*D.Clamp(1-habitat.AridityMilli/1500.0)+.30*habitat.WarmSeasonDryPermille/1000.0:e.Kind==HarvestHazard.Flood?habitat.Flooding!=FloodRegime.None?.25+.65*habitat.FloodFractionPermille/1000.0:HistoricalRiverAccess(cell)>.02?.25:0:D.Clamp((240-habitat.GrowingDays)/220.0);
                double loss=e.Severity*susceptibility*HistoricalFoodEffects.ShockWeight(World.Surface.Cells[cell].Position,e.Center,e.RadiusKm,World.Recipe.RadiusKm);
                if(loss>maximum){maximum=loss;hazard=e.Kind;}
            }
            int diversity=Enumerable.Range(0,6).Count(k=>s.History.Agriculture[town.CultureId].AdoptedYear[k]>0&&(ModernHistory(s)?CropTransferFit(s,town.CultureId,cell,k):CropSuitability(cell,k))>.3);
            int works=town.PolityId<0?0:s.History.Dynamics.States[town.PolityId].PublicWorks;
            double buffered=HistoricalFoodEffects.ResilientLoss(maximum,diversity,works,hazard==HarvestHazard.Drought&&HistoricalTechnologies.Has(s.History.Cultures[town.CultureId],HistoricalTechnology.Irrigation),HistoricalRiverAccess(cell)>.02);
            weather*=1-buffered*(.20+.80*D.Clamp(culture.Farming/850.0));
            if(buffered<.12)hazard=HarvestHazard.None;
            return D.Q(Math.Max(.25,weather),1000000);
        }
        HistoricalFoodYear HistoricalAnnualFood(PrehistoryState s,bool[] wars,CancellationToken token)
        {
            var h=s.History;var towns=s.Society.Settlements;var harvest=new long[towns.Length];var production=new long[towns.Length];var reserves=new long[towns.Length];
            var foodYear=new HistoricalFoodYear{Harvest=harvest,Received=new long[towns.Length]};
            for(int i=0;i<towns.Length;i++)
            {
                token.ThrowIfCancellationRequested();var town=towns[i];var record=h.Towns[i];var hc=h.Cultures[town.CultureId];
                double multiplier=1+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Irrigation)?.3:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Iron)?.2:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Mills)?.35:0);
                if(ModernHistory(s))multiplier*=HistoricalModernEffects.FoodMultiplier(hc,s.Years);
                town.FoodCapacity=Math.Max(RegionalHistory(s)?0:300,(long)(record.FoodBase*multiplier));
                double weather=HistoricalHarvestWeather(s,town,out var hazard);if(town.PolityId>=0&&wars[town.PolityId])weather*=.90;
                harvest[i]=(long)((decimal)town.FoodCapacity*D.M(weather*1000000)/1000000);
                if(hazard!=HarvestHazard.None&&s.Years-record.LastHarvestHazardYear>=10)
                {record.LastHarvestHazard=hazard;record.LastHarvestHazardYear=s.Years;HistoryEvent(s,"harvest-shock",hazard+" reduces the harvest around "+town.Name+"; reserves, food diversity and local infrastructure affect resilience.",town.Cell,town.CultureId,town.PolityId,town:town.Id);}
                long safety=town.Population*3/2;
                production[i]=Math.Max(0,harvest[i]-town.Population-Math.Max(0,safety-town.FoodReserve));
                reserves[i]=Math.Max(0,town.FoodReserve-safety)/4;
            }
            var need=towns.Select(t=>Math.Max(0,t.Population+t.Population/20-harvest[t.Id]-t.FoodReserve)).ToArray();
            var targets=towns.Where(t=>t.Population>=100&&need[t.Id]>0).OrderByDescending(t=>D.Q(need[t.Id]/(double)Math.Max(1,t.Population),1000000)).ThenBy(t=>t.Id).ToArray();
            foreach(var target in targets)
            {
                token.ThrowIfCancellationRequested();
                var candidates=towns.Where(t=>t.Id!=target.Id&&t.Population>=100&&production[t.Id]+reserves[t.Id]>0&&FoodExchangeAllowed(s,t,target)).OrderByDescending(t=>V.Dot(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[target.Cell].Position)).ThenBy(t=>t.Id).Take(32).ToArray();
                foreach(var source in candidates)
                {
                    if(need[target.Id]==0)break;
                    double distance=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[target.Cell].Position)*World.Recipe.RadiusKm;if(distance>1200&&!ModernShippingContact(s,source.Cell,target.Cell,source.CultureId))continue;
                    if(!ReachableHistoricalContact(s,source.Cell,target.Cell,source.CultureId,token))continue;
                    var hc=h.Cultures[source.CultureId];bool sea=HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)&&World.Surface.Cells[source.Cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j])&&World.Surface.Cells[target.Cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                    double reach=sea?HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?3500:2000:HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads)?1200:750;
                    if(ModernHistory(s))reach*=HistoricalModernEffects.LandTransport(hc,s.Years);
                    int retained=D.M(1000*D.Clamp(.97*Math.Exp(-distance/reach),.45,.97));
                    var shipment=HistoricalFoodEffects.Deliver(production[source.Id]+reserves[source.Id],need[target.Id],retained,Math.Max(1,Math.Min(source.FoodCapacity/4,target.Population/3)));
                    if(shipment.Received==0)continue;
                    long fresh=Math.Min(production[source.Id],shipment.Sent),stored=shipment.Sent-fresh;
                    production[source.Id]-=fresh;harvest[source.Id]-=fresh;reserves[source.Id]-=stored;source.FoodReserve-=stored;
                    harvest[target.Id]+=shipment.Received;foodYear.Received[target.Id]+=shipment.Received;need[target.Id]-=shipment.Received;
                    h.Towns[source.Id].ExportedFood+=shipment.Sent;h.Towns[source.Id].FoodTransportLoss+=shipment.Lost;h.Towns[target.Id].ImportedFood+=shipment.Received;
                    foodYear.Voyages.Add((Math.Min(source.Id,target.Id),Math.Max(source.Id,target.Id)));
                }
            }
            return foodYear;
        }
        bool FoodExchangeAllowed(PrehistoryState s,Settlement a,Settlement b)
        {
            if(a.PolityId<0||b.PolityId<0||a.PolityId==b.PolityId)return true;
            var relation=FindHistoricalRelation(s,a.PolityId,b.PolityId);
            return relation==null||relation.WarSince<0;
        }
    }
}

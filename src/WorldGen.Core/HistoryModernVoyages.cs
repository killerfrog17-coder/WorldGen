using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalModernVoyageEffects
    {
        public static int RangeM(HistoricalCulture culture,int year)
        {
            double ocean=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.OceanNavigation,year);
            double steam=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.SteamPower,year);
            return 3500000+(int)Math.Round(3000000*ocean/500000)*500000+(int)Math.Round(2500000*ocean*steam/500000)*500000;
        }
        public static int FounderScale(HistoricalCulture culture,int year)
        {
            double ocean=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.OceanNavigation,year),steam=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.SteamPower,year);
            return 1+(int)Math.Floor(4*ocean+15*ocean*steam);
        }
    }
    public sealed partial class Prehistory
    {
        sealed class ModernVoyageStaging {public int Cell,Depot;public long Rural,Residents;}
        long ModernLandingRoom(PrehistoryState s,int source,int cell)
        {
            int patch=CellPatch[cell];long room=Math.Max(0,Patches[patch].Capacity-s.Population[patch]);if(s.Population[patch]==0)return room;
            double fit=0;var crops=s.History.Agriculture[source];for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0&&crops.AdoptedYear[k]<=s.Years)fit=Math.Max(fit,CropTransferFit(s,source,cell,k));
            // The same baseline allocation and crop/season constraints used by
            // rural capacity bound new fields among existing inhabitants.
            if(fit>=.3&&ruralPatchWeight[patch]>0)
            {
                long baseline=AllocateRuralShare(Patches[patch].Capacity,ruralPrefix[cell],ruralWeight[cell],ruralPatchWeight[patch]);
                room+=(long)Math.Floor(baseline*Math.Min(600,s.Society.Cultures[source].Farming)/35.0*HistoricalAgriculturalYield(cell)*fit);
            }
            return room;
        }
        void AdvanceModernOceanSettlements(PrehistoryState s,CancellationToken token,double elapsedYears)
        {
            if(s.Society.Cultures.Length==0)return;int count=s.Society.Cultures.Length;
            var groups=CulturalCells(s).Where(i=>CellPatch[i]>=0&&(World.Hydrology.OceanConnected[i]||World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))).GroupBy(i=>s.Society.CellCulture[i]).OrderBy(g=>g.Key).ToArray();
            foreach(var group in groups)
            {
                token.ThrowIfCancellationRequested();int source=group.Key;if(source>=count)continue;var culture=s.Society.Cultures[source];var hc=s.History.Cultures[source];
                int adoption=hc.Technology[(int)HistoricalTechnology.Navigation];if(adoption<=0||s.Years-adoption+1<200)continue;
                // Qualify the provisioned departures BEFORE choosing staging
                // points. A busy real port must not be displaced by four rural
                // coasts with no depot at all. Urban founders are actual people.
                var shore=new List<ModernVoyageStaging>();
                foreach(int cell in group)
                {
                    if(CultureOpportunity(s,cell)<.2)continue;int market=s.History.CellMarket.Length>cell?s.History.CellMarket[cell]:-1;if(market<0)continue;
                    var town=s.Society.Settlements[market];if(town.CultureId!=source||town.Population<500||town.FoodReserve<town.Population/2||s.History.Towns[town.Id].DiseaseUntil>=s.Years)continue;
                    long localRural=CoastalStagingResidents(s,cell,source),residents=localRural+town.Population;if(residents<600)continue;
                    shore.Add(new ModernVoyageStaging{Cell=cell,Depot=market,Rural=localRural,Residents=residents});
                }
                if(shore.Count==0)continue;var staging=new List<ModernVoyageStaging>{shore.OrderByDescending(x=>x.Residents).ThenBy(x=>x.Cell).First()};
                while(staging.Count<4&&staging.Count<shore.Count)staging.Add(shore.Where(x=>staging.All(y=>y.Cell!=x.Cell)).OrderByDescending(x=>staging.Min(y=>D.Angle(World.Surface.Cells[x.Cell].Position,World.Surface.Cells[y.Cell].Position))).ThenBy(x=>x.Cell).First());
                int range=HistoricalModernVoyageEffects.RangeM(hc,s.Years);double vessels=1+HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)+HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years)*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years);
                Route best=null;ModernVoyageStaging departure=null;double probability=0;int bestSailing=0;
                foreach(var from in staging)
                {
                  int detours=0;foreach(var route in RemoteVoyageDestinations(from.Cell,token,range,50,64))
                  {
                    int target=route.B;if(s.Society.CellCulture[route.CellB]>=0||ModernLandingRoom(s,source,route.CellB)<50)continue;
                    int sailing=route.DistanceM;
                    if(range>3500000)
                    {
                        // The semantic investigation budget is independent of
                        // cache warmth, so cold/warm reconstructions agree.
                        if(!OpenOceanPassage(from.Cell,route.CellB,token)&&detours++>=4)continue;
                        sailing=ModernSeaDistance(from.Cell,route.CellB,range,token);if(sailing<=0)continue;
                    }
                    double chance=HistoricalVoyageEffects.SettlementChance(sailing,s.Years-adoption+1,Math.Max(250,culture.Fishing),CultureOpportunity(s,from.Cell),VoyageWindSupport(from.Cell,route.CellB),from.Residents,range,vessels);
                    // Larger coasts present a wider encounter opportunity, not
                    // foreknowledge of a destination's political future.
                    double encounter=Math.Max(.5,Math.Min(2,Math.Sqrt(Patches[target].RadiusKm/100.0)));chance=D.Q(1-Math.Pow(1-chance,encounter),1000000000);
                    if(chance>probability||chance==probability&&best!=null&&route.CellB<best.CellB){best=route;probability=chance;departure=from;bestSailing=sailing;}
                  }
                }
                if(best==null||HistoryRoll("remote-ocean-settlement-13",source,s.Years)>=ElapsedChance(probability,elapsedYears/50))continue;
                var depot=s.Society.Settlements[departure.Depot];
                long requested=(50+(int)(100*HistoryRoll("ocean-founders-13",source,s.Years)))*HistoricalModernVoyageEffects.FounderScale(hc,s.Years);
                var founders=HistoricalVoyageEffects.AllocateFounders(requested,ModernLandingRoom(s,source,best.CellB),departure.Rural,s.Population[best.A],depot.Population);long migrants=founders.Total;if(migrants<50)continue;
                long provisions=HistoricalVoyageEffects.Provisioning(migrants,bestSailing);if(depot.FoodReserve<depot.Population/2+provisions+(migrants>=500?migrants:0))continue;
                long rural=founders.Rural,urban=founders.Urban,priorResidents=s.Population[best.B];depot.FoodReserve-=provisions;depot.Population-=urban;
                s.Population[best.A]-=rural;s.Population[best.B]+=migrants;if(s.ArrivalYear[best.B]<0)s.ArrivalYear[best.B]=s.Years;
                for(int k=0;k<8;k++)s.Ancestry[best.B*8+k]=(int)Math.Round(((decimal)s.Ancestry[best.B*8+k]*priorResidents+(decimal)s.Ancestry[best.A*8+k]*migrants)/(priorResidents+migrants));
                var child=FoundCulture(s,best.B,best.CellB);InitializeHistoricalCulture(s,child);InheritHistoricalCommunity(s,child,source,true);
                // A large funded expedition is physically concentrated at its
                // landing, not immediately diluted across an entire rural patch.
                // The existing settlement path moves these people out of the
                // rural total; its initial reserve is paid by the departure port.
                long arrivalFood=0;if(migrants>=500)
                {
                    long before=s.Population[best.B];FoundHistoricalSettlement(s,child,best.CellB,migrants);arrivalFood=before-s.Population[best.B];depot.FoodReserve-=arrivalFood;
                }
                var records=s.History.OceanSettlements??Array.Empty<HistoricalOceanSettlement>();
                s.History.OceanSettlements=records.Concat(new[]{new HistoricalOceanSettlement{Id=records.Length,Year=s.Years,SourceCulture=source,DestinationCulture=child.Id,FromCell=best.CellA,ToCell=best.CellB,DistanceM=best.DistanceM,Migrants=migrants,Provisions=provisions,DepartureTownId=depot.Id,UrbanMigrants=urban,ArrivalFood=arrivalFood,SailingDistanceM=bestSailing>best.DistanceM?bestSailing:0}}).ToArray();
                HistoryEvent(s,"overseas-settlement",migrants+" voyagers depart from "+depot.Name+" and neighboring shores for a new coastal community approximately "+(best.DistanceM/1000)+" km away. The "+child.Name+" carry suitable seeds and technical traditions, but must establish local livelihoods and institutions.",best.CellB,child.Id,category:ChronicleCategory.Society);
            }
        }
    }
}

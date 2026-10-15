using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        int[] aggregateDomesticTaxa;
        int[] livingCoastalRegions;
        bool[] aggregateOceanRaster;
        const int OceanRasterWidth=256,OceanRasterHeight=128;
        void PrepareAggregateEnvironment(PrehistoryState s)
        {
            PrepareWildCrops();PrepareLandscape();
            if(LivingHistory(s)&&livingCoastalRegions==null)livingCoastalRegions=Enumerable.Range(0,Patches.Length).Where(i=>Patches[i].Island||Patches[i].Cells.Any(cell=>World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]))).ToArray();
            if(aggregateDomesticTaxa==null)aggregateDomesticTaxa=World.Ecology.Taxa.Where(t=>NativeLifePack.Domesticability(t.Traits)>=.15).Select(t=>t.Id).OrderBy(i=>i).ToArray();
            if(aggregateOceanRaster!=null){if(ConnectedHistory(s))PrepareConnectedEnvironment();if(CommunityHistory(s))PrepareCommunityShores();return;}
            foreach(var patch in Patches)if(patch.Island)CropHabitat(s,patch.Cell);
            // A fixed-size physical approximation, built once. Voyages query
            // 32 samples, never run a fine-cell flood fill or A* in a turn.
            var locator=new CellLocator(World.Surface.Cells);var raster=new bool[OceanRasterWidth*OceanRasterHeight];
            for(int y=0;y<OceanRasterHeight;y++)for(int x=0;x<OceanRasterWidth;x++)
            {double lat=Math.PI*(.5-(y+.5)/OceanRasterHeight),lon=2*Math.PI*((x+.5)/OceanRasterWidth-.5);var p=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));raster[y*OceanRasterWidth+x]=World.Hydrology.OceanConnected[locator.Nearest(p)];}aggregateOceanRaster=raster;
            if(ConnectedHistory(s))PrepareConnectedEnvironment();
            if(CommunityHistory(s))PrepareCommunityShores();
        }
        bool AggregateSeaLane(int from,int to)
        {
            var start=World.Surface.Cells[from].Position;var end=World.Surface.Cells[to].Position;int land=0;
            for(int k=1;k<32;k++)
            {var point=(start*(1-k/32.0)+end*(k/32.0)).Unit;int x=Math.Max(0,Math.Min(OceanRasterWidth-1,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*OceanRasterWidth))),y=Math.Max(0,Math.Min(OceanRasterHeight-1,(int)((.5-Math.Asin(point.Z)/Math.PI)*OceanRasterHeight)));if(!aggregateOceanRaster[y*OceanRasterWidth+x]&&++land>2)return false;}return true;
        }
        int AggregateCoastalSite(int patch,int salt)
        {
            var cells=Patches[patch].Cells;
            for(int trial=0;trial<12;trial++){int cell=cells[Math.Min(cells.Length-1,(int)(HistoryRoll("aggregate-port-site-"+trial,patch,salt)*cells.Length))];if(Patches[patch].Island||World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]))return cell;}return -1;
        }
        void AdvanceAggregateVoyage(PrehistoryState s,int cityId)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[cityId];var hc=h.Cultures[town.CultureId];
            if(town.Population<1200||town.FoodReserve<200||!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)||!World.Surface.Cells[town.Cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n])||HistoryRoll("aggregate-voyage-trial",cityId,s.Years)>.12)return;
            int patch=Math.Min(Patches.Length-1,(int)(HistoryRoll("aggregate-voyage-region",cityId,s.Years)*Patches.Length));if(!LivingHistory(s)&&(Patches[patch].Capacity<50||patch==CellPatch[town.Cell]))return;
            if(LivingHistory(s))
            {
                int best=-1;double score=double.MaxValue;
                for(int k=0;k<8;k++){int candidate=livingCoastalRegions[(int)(HistoryRoll("living-coastal-target-"+k,cityId,s.Years)*livingCoastalRegions.Length)];if(candidate==CellPatch[town.Cell]||Patches[candidate].Capacity<50)continue;double distance=D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[Patches[candidate].Cell].Position)*World.Recipe.RadiusKm;double value=distance*(.6+HistoryRoll("living-coastal-priority-"+k,cityId,s.Years));if(value<score){score=value;best=candidate;}}
                if(best<0)return;patch=best;
            }
            int destination=AggregateCoastalSite(patch,s.Years);if(destination<0)return;
            double km=D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[destination].Position)*World.Recipe.RadiusKm;
            int range=HistoricalModernVoyageEffects.RangeM(hc,s.Years);if(km*1000>range||km<70||!AggregateSeaLane(town.Cell,destination))return;
            int local=HistoricalCells.Culture(s,destination);int destinationTown=a.TownAt[destination];
            if(LivingHistory(s))
            {
                if(local<0)local=a.Regions[patch].Culture;
                var ports=NearbyLivingTowns(s,destination,280);foreach(int candidate in ports)if(candidate!=cityId&&a.Living.Cities[candidate].AbandonedYear<0){destinationTown=candidate;break;}
                if(local>=0){LivingCultureContact(s,town.CultureId,local);a.Living.Regions[patch].Teacher=town.CultureId;a.Living.Regions[patch].Exposure=Math.Min(1000,a.Living.Regions[patch].Exposure+200);}
                if(ProvincialHistory(s)){a.Provinces.RegionalKnowledge[patch]|=ProvincialKnowledge(hc);a.Living.Regions[patch].LastExchangeYear=s.Years;}
            }
            if(destinationTown<0&&local>=0&&a.Cultures[local].Cities.Count>0)
            {int candidate=a.Cultures[local].Cities.At((int)(HistoryRoll("aggregate-destination-port",cityId,s.Years)*a.Cultures[local].Cities.Count));var port=s.Society.Settlements[candidate];if(D.Angle(World.Surface.Cells[port.Cell].Position,World.Surface.Cells[destination].Position)*World.Recipe.RadiusKm<180&&World.Surface.Cells[port.Cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]))destinationTown=candidate;}
            if(LivingHistory(s)&&destinationTown==cityId)return;
            if(destinationTown>=0&&s.Society.Settlements[destinationTown].Population>=100)
            {
                var links=a.Cities[cityId].Neighbors;if(links.Contains(destinationTown))return;
                var newLinks=links.Concat(new[]{destinationTown}).Take(12).ToArray();if(links.Length>=12)newLinks[11]=destinationTown;a.Cities[cityId].Neighbors=newLinks;
                var peerLinks=a.Cities[destinationTown].Neighbors;var reverse=peerLinks.Concat(new[]{cityId}).Take(12).ToArray();if(peerLinks.Length>=12)reverse[11]=cityId;a.Cities[destinationTown].Neighbors=reverse;
                if(km>=700&&km<=9200&&(!LivingHistory(s)||aggregateShipping.Add((Math.Min(cityId,destinationTown),Math.Max(cityId,destinationTown)))))h.Modern.Shipping=h.Modern.Shipping.Concat(new[]{new HistoricalShippingLink{Id=h.Modern.Shipping.Length,EstablishedYear=s.Years,CultureA=s.Society.Settlements[Math.Min(cityId,destinationTown)].CultureId,CultureB=s.Society.Settlements[Math.Max(cityId,destinationTown)].CultureId,PortA=Math.Min(cityId,destinationTown),PortB=Math.Max(cityId,destinationTown),DistanceM=(int)Math.Round(km*1000)}}).ToArray();
                HistoryEvent(s,"ocean-exchange",town.Name+" establishes sea exchange with "+s.Society.Settlements[destinationTown].Name+".",town.Cell,town.CultureId,town.PolityId,town:cityId,category:ChronicleCategory.Discoveries);return;
            }
            long carrying=Patches[patch].Capacity;
            if(LivingHistory(s))carrying=(long)(carrying*(1+s.Society.Cultures[town.CultureId].Farming/80.0*LivingCropLandscape(s,town.CultureId,destination,false)*HistoricalModernEffects.FoodMultiplier(hc,s.Years)));
            if(local>=0||s.Population[patch]>carrying*.8||HistoryRoll("aggregate-voyage-landing",cityId,s.Years)>.25)return;
            long people=Math.Min(Math.Min(town.Population/20,149*HistoricalModernVoyageEffects.FounderScale(hc,s.Years)),Math.Min(town.FoodReserve/3,Math.Max(0,carrying-s.Population[patch])));if(people<50)return;
            town.FoodReserve-=people*2;ChangeAggregateCityPopulation(s,cityId,town.Population-people,a.Cities[cityId].Suburban,a.Cities[cityId].Rural);long previous=s.Population[patch];s.Population[patch]+=people;a.TotalPopulation+=people;if(s.ArrivalYear[patch]<0)s.ArrivalYear[patch]=s.Years;for(int g=0;g<8;g++)s.Ancestry[patch*8+g]=(int)((previous*s.Ancestry[patch*8+g]+people*s.Ancestry[CellPatch[town.Cell]*8+g])/Math.Max(1,s.Population[patch]));
            if(HistoricalCells.Culture(s,destination)<0)
            {
                FoundAggregateCulture(s,patch,destination);int child=h.Cultures.Length-1;
                if(CommunityHistory(s))InheritDevelopmentCommunity(s,child,town.CultureId,destination);
                else
                {
                var inherited=h.Cultures[child];inherited.ParentCultureId=town.CultureId;inherited.LanguageId=hc.LanguageId;inherited.Technology=(int[])hc.Technology.Clone();s.Society.Cultures[child].Farming=s.Society.Cultures[town.CultureId].Farming;
                var crops=h.Agriculture[child];var parent=h.Agriculture[town.CultureId];for(int k=0;k<6;k++)if(parent.AdoptedYear[k]>0){crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=town.CultureId;crops.Cultivars[k]=parent.Cultivars?[k]?.Copy();}
                crops.Animals=(parent.Animals??Array.Empty<HistoricalAnimalHusbandry>()).Where(animal=>animal.AdoptedYear>0&&Biogeography.Suitable(World.Ecology.Taxa[animal.TaxonId],CropHabitat(s,destination),World.ElevationM[destination])).Select(animal=>new HistoricalAnimalHusbandry{TaxonId=animal.TaxonId,Name=animal.Name,Practice=animal.Practice,AdoptedYear=s.Years+1,SourceCulture=town.CultureId,OriginCell=destination}).ToArray();
                }
                HistoryEvent(s,"ocean-settlement",town.Name+" sends "+people+" settlers to a new shore; their inherited practices still require local crops and support.",destination,child,town.PolityId,town:cityId,category:ChronicleCategory.Society);
            }
        }
        void AdvanceAggregateHusbandry(PrehistoryState s,int culture,int contact,int elapsed)
        {
            if(aggregateDomesticTaxa.Length==0||!HistoricalLocalEffects.AgricultureOpen(s.Years))return;var c=s.Society.Cultures[culture];if(s.Years-c.FoundedYear<500)return;
            int taxon=aggregateDomesticTaxa[(int)(HistoryRoll("aggregate-herding-candidate",culture,s.Years)*aggregateDomesticTaxa.Length)];var animal=World.Ecology.Taxa[taxon];var agriculture=s.History.Agriculture[culture];var records=agriculture.Animals??Array.Empty<HistoricalAnimalHusbandry>();var record=records.FirstOrDefault(r=>r.TaxonId==taxon);if(record?.AdoptedYear>0)return;
            int site=c.OriginCell;if(s.History.Aggregates.Cultures[culture].Cities.Count>0){var cities=s.History.Aggregates.Cultures[culture].Cities;site=s.Society.Settlements[cities.At((int)(HistoryRoll("aggregate-herding-site",culture,s.Years)*cities.Count))].Cell;}
            int source=contact>=0&&(s.History.Agriculture[contact].Animals?.Any(r=>r.TaxonId==taxon&&r.AdoptedYear>0)??false)?contact:-1;
            if(!Biogeography.Present(World.Ecology,site,taxon)&&(source<0||!Biogeography.Suitable(animal,CropHabitat(s,site),World.ElevationM[site])))return;
            if(record==null){record=new HistoricalAnimalHusbandry{TaxonId=taxon,OriginCell=site,Name=animal.Name,SourceCulture=source};agriculture.Animals=records.Concat(new[]{record}).OrderBy(r=>r.TaxonId).ToArray();}
            var traits=animal.Traits;int region=World.Ecology.Region[site];var form=region>=0?World.Ecology.Regions[region].Species.FirstOrDefault(p=>p.TaxonId==taxon)?.Variant:null;if(form!=null){traits=traits.Copy();traits.Docility=form.Docility;traits.PanicResistance=form.PanicResistance;}
            double gain=elapsed*(source>=0?.8:NativeLifePack.Domesticability(traits)*(.15+.85*c.Farming/1000.0)/6);int whole=(int)Math.Round(gain);
            if(CommunityHistory(s)){whole=(int)gain;if(HistoryRoll("community-husbandry-fraction-"+taxon,culture,s.Years)<gain-whole)whole++;}record.Practice=Math.Min(1000,record.Practice+whole);
            if(record.Practice>=600){record.AdoptedYear=s.Years+1;record.SourceCulture=source;HistoryEvent(s,"animal-domestication",c.Name+(source<0?" develops breeding traditions for ":" adopts management of ")+animal.Name+".",site,culture,category:ChronicleCategory.Discoveries);}
        }
        void AdvanceAggregateFaith(PrehistoryState s,int culture,int contact,int elapsed)
        {
            var h=s.History;var hc=h.Cultures[culture];var c=s.Society.Cultures[culture];var a=h.Aggregates.Cultures[culture];var faith=h.Religions[hc.ReligionId];
            if(a.CityCount>=2&&a.Urban>=5000&&faith.Reach==ReligiousReach.Local)faith.Reach=ReligiousReach.Portable;
            if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)&&a.Urban>=20000&&HistoryRoll("aggregate-portable-teaching",culture,s.Years)<.02)faith.Reach=ReligiousReach.Missionary;
            if(contact>=0&&h.Religions[h.Cultures[contact].ReligionId].Reach!=ReligiousReach.Local&&HistoryRoll("aggregate-faith-contact",culture,s.Years)<.03){hc.ReligionId=h.Cultures[contact].ReligionId;faith=h.Religions[hc.ReligionId];}
            int site=c.OriginCell;if(a.Cities.Count>0)site=s.Society.Settlements[a.Cities.At((int)(HistoryRoll("aggregate-faith-community",culture,s.Years)*a.Cities.Count))].Cell;
            if(faith.Reach!=ReligiousReach.Local){SetAggregateLayer(s,site,2,faith.Id);var neighbors=World.Surface.Cells[site].Neighbors;int neighbor=neighbors[(int)(HistoryRoll("aggregate-faith-neighbor",culture,s.Years)*neighbors.Length)];if(CellPatch[neighbor]>=0&&s.Population[CellPatch[neighbor]]>0&&HistoryRoll("aggregate-faith-transmission",culture,s.Years)<(faith.Reach==ReligiousReach.Missionary?.35:.1))SetAggregateLayer(s,neighbor,2,faith.Id);}
            if(a.Urban>=5000&&s.Years-faith.FoundedYear>=300&&HistoryRoll("aggregate-faith-reform",culture,s.Years)<.0005*elapsed/40)
            {int id=h.Religions.Length;h.Religions=h.Religions.Concat(new[]{new HistoricalReligion{Id=id,ParentId=faith.Id,CultureId=culture,HolyCell=site,FoundedYear=s.Years,Name="Reformed tradition of "+c.Name,Tradition="A reform rooted in "+faith.Name,Ethos=faith.Ethos,Ritual=faith.Ritual,Reach=ReligiousReach.Portable}}).ToArray();hc.ReligionId=id;SetAggregateLayer(s,site,2,id);HistoryEvent(s,"religious-reform","Teachers among "+c.Name+" establish a distinct reform of "+faith.Name+".",site,culture,religion:id);}
        }
    }
}

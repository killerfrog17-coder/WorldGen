using System;
using System.Linq;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool LocalSeedlotHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=14&&s.History.Aggregates.Empires.Connections.LocalSeeds!=null;
        public HistoricalTownSeeds LocalSeedsAt(PrehistoryState s,int town)=>LocalSeedlotHistory(s)?s.History.Aggregates.Empires.Connections.LocalSeeds.Towns[town]:null;
        void InitializeLocalSeedlots(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections;if(c.LocalSeeds!=null)return;
            c.LocalSeeds=new HistoricalLocalSeedlots{SinceYear=s.Years};
            // Explicit upgrade/setup only: no recurring town or terrain census.
            foreach(var town in s.Society.Settlements)if(s.History.Aggregates.Living.Cities[town.Id].AbandonedYear<0)
                c.LocalSeeds.Towns=c.LocalSeeds.Towns.With(town.Id,SnapshotLocalSeeds(s,town.Id,town.CultureId,town.Cell,-1));
        }
        HistoricalTownSeeds SnapshotLocalSeeds(PrehistoryState s,int town,int culture,int cell,int parent)
        {
            var inherited=parent>=0?LocalSeedsAt(s,parent):LocalSeedHolder(s,culture,cell);
            var crops=s.History.Agriculture[culture];var lots=new HistoricalLocalSeedlot[6];
            for(int k=0;k<6;k++)
            {
                var previous=inherited?[k];var profile=inherited!=null?previous?.Profile:crops.AdoptedYear[k]>0?crops.Cultivars?[k]:null;if(profile==null)continue;
                int practice=previous?.PracticeYears??(crops.HusbandryYears?[k]??0);
                if(parent>=0&&previous!=null)practice=(int)Math.Round(practice*HistoricalLandscapeEffects.LivelihoodContinuity(CropHabitat(s,inherited.FieldCell),CropHabitat(s,inherited.FieldCell),CropHabitat(s,cell)));
                lots[k]=new HistoricalLocalSeedlot(profile,s.Years,parent,parent>=0?s.Society.Settlements[parent].Cell:cell,practice,s.Years);
            }
            return new HistoricalTownSeeds(town,cell,lots);
        }
        void RegisterLocalTownSeeds(PrehistoryState s,int town,int parent)
        {
            if(!LocalSeedlotHistory(s))return;var t=s.Society.Settlements[town];var extension=s.History.Aggregates.Empires.Connections.LocalSeeds;
            extension.Towns=extension.Towns.With(town,SnapshotLocalSeeds(s,town,t.CultureId,t.Cell,parent));
        }
        void InheritLocalSeedCommunity(PrehistoryState s,int child,int parent,int site,int sourceCell,int sourceTown)
        {
            // Missing physical departure is not permission to copy the distant
            // origin's global profile into a new daughter farming community.
            if(sourceCell<0&&sourceTown<0)return;var source=LocalSeedHolder(s,parent,sourceCell>=0?sourceCell:s.Society.Settlements[sourceTown].Cell,sourceTown);
            var donor=s.History.Agriculture[parent];var crops=s.History.Agriculture[child];
            for(int k=0;k<6;k++)
            {
                var profile=LocalSeedProfile(s,parent,k,source);if(profile==null||CommunityTransferFit(s,site,profile)<.12)continue;
                crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=parent;crops.Cultivars[k]=profile.Copy();crops.Cultivars[k].ParentCulture=parent;crops.PracticeCell[k]=site;
                int field=source?.FieldCell??(sourceCell>=0?sourceCell:donor.PracticeCell[k]);
                crops.HusbandryYears[k]=(int)Math.Round(LocalSeedPractice(s,parent,k,source)*HistoricalLandscapeEffects.LivelihoodContinuity(CropHabitat(s,field),CropHabitat(s,field),CropHabitat(s,site)));
            }
        }
        bool LocalSeedTownLive(PrehistoryState s,int town,int culture)
            =>town>=0&&town<s.Society.Settlements.Length&&s.Society.Settlements[town].CultureId==culture&&s.Society.Settlements[town].Population+s.History.Aggregates.Cities[town].Rural>=10&&s.History.Aggregates.Living.Cities[town].AbandonedYear<0;
        HistoricalTownSeeds LocalSeedHolder(PrehistoryState s,int culture,int cell,int carrier=-1)
        {
            if(!LocalSeedlotHistory(s))return null;var a=s.History.Aggregates;
            if(carrier>=0)return carrier<a.Cities.Length&&a.Living.Cities[carrier].AbandonedYear<0?a.Empires.Connections.LocalSeeds.Towns[carrier]:null;
            int town=a.TownAt[cell];if(LocalSeedTownLive(s,town,culture))return a.Empires.Connections.LocalSeeds.Towns[town];
            int market=HistoricalCells.Market(s,cell),source=a.Empires.Territories.Sources[cell],owner=HistoricalCells.Polity(s,cell);
            if(LocalSeedTownLive(s,market,culture)&&LocalSeedService(s,market,cell,culture))return a.Empires.Connections.LocalSeeds.Towns[market];
            if(owner<0||!TerritorialNodeLive(s,owner,source))return null;
            for(int depth=0;source<=-2&&depth<8;depth++)source=a.Empires.Territories.Posts[-source-2].ParentSource;
            return LocalSeedTownLive(s,source,culture)&&LocalSeedService(s,source,cell,culture)?a.Empires.Connections.LocalSeeds.Towns[source]:null;
        }
        bool LocalSeedService(PrehistoryState s,int town,int cell,int culture)
        {
            int from=s.Society.Settlements[town].Cell;if(World.Landmass[from]!=World.Landmass[cell]||!InstitutionalRouteAllowed(s,from,cell,culture))return false;
            int owner=HistoricalCells.Polity(s,cell);double reach=owner>=0?TerritorialServiceRadius(s,owner,cell):280;
            return LivingTravelKm(s,from,cell,culture)<=reach;
        }
        HistoricalCultivar LocalSeedProfile(PrehistoryState s,int culture,int group,HistoricalTownSeeds holder)
            =>holder!=null?holder[group]?.Profile:s.History.Agriculture[culture].AdoptedYear[group]>0?s.History.Agriculture[culture].Cultivars?[group]:null;
        int LocalSeedPractice(PrehistoryState s,int culture,int group,HistoricalTownSeeds holder)
            =>holder!=null?holder[group]?.PracticeYears??0:s.History.Agriculture[culture].HusbandryYears?[group]??0;
        int LocalSeedPracticeAt(PrehistoryState s,int culture,int group,HistoricalTownSeeds holder,int cell,bool carried)
        {
            int years=LocalSeedPractice(s,culture,group,holder);
            return carried&&holder!=null?(int)Math.Round(years*HistoricalLandscapeEffects.LivelihoodContinuity(CropHabitat(s,holder.FieldCell),CropHabitat(s,holder.FieldCell),CropHabitat(s,cell))):years;
        }
        double CarriedCropLandscape(PrehistoryState s,int culture,int cell,bool stored,int parent,int sourceCell=-1)
            =>LocalSeedlotHistory(s)?CommunitySeededCropLandscape(s,culture,cell,stored,LocalSeedHolder(s,culture,sourceCell>=0?sourceCell:cell,parent),true):LivingCropLandscape(s,culture,cell,stored);
        double CarriedPhysicalYield(PrehistoryState s,int culture,int cell,int parent,int sourceCell=-1)
        {if(!LocalSeedlotHistory(s))return ProvincialPhysicalYield(s,culture,cell);var habitat=CropHabitat(s,cell);double fraction=Math.Min(1,Math.Max(.08,D.Clamp((habitat.AridityMilli-100)/550.0)+riverAccess[cell]));return D.Q(CarriedCropLandscape(s,culture,cell,false,parent,sourceCell)*fraction,1000000);}
        long CarriedFarmPotential(PrehistoryState s,int culture,int cell,int parent,int sourceCell=-1)
        {if(!LocalSeedlotHistory(s))return ProvincialFarmPotential(s,cell,culture);int region=CellPatch[cell];double free=1-D.Clamp(s.History.Aggregates.Living.Regions[region].CommittedAreaKm2/(double)Math.Max(1,s.History.Aggregates.Regions[region].AreaKm2));return (long)Math.Round(inhabitableArea[cell]*free*65*CarriedPhysicalYield(s,culture,cell,parent,sourceCell));}
        long CarriedFarmFrontierRoom(PrehistoryState s,int region,int cell,int farmer,long ordinary,int sourceCell)
        {
            if(!LocalSeedlotHistory(s))return FarmFrontierRoom(s,region,cell,farmer,ordinary);
            if(HistoricalCells.Polity(s,cell)>=0||s.Society.Cultures[farmer].Farming<250||CarriedCropLandscape(s,farmer,cell,false,-1,sourceCell)<.12)return Math.Max(0,ordinary);
            long fields=(long)(CarriedFarmPotential(s,farmer,cell,-1,sourceCell)*D.Clamp(s.Society.Cultures[farmer].Farming/600.0));int local=s.History.Aggregates.Regions[region].Culture;
            return HistoricalFarmFrontier.Room(ordinary,fields,AggregateRuralCellPopulation(s,cell),local>=0?s.Society.Cultures[local].Farming:0);
        }
        double CarriedCropSupport(PrehistoryState s,int culture,int cell,int parent)
        {if(!LocalSeedlotHistory(s))return AggregateCropSupport(s,culture,cell,false);var holder=LocalSeedHolder(s,culture,cell,parent);double best=0;for(int k=0;k<6;k++){var seed=LocalSeedProfile(s,culture,k,holder);if(seed!=null)best=Math.Max(best,CommunityTransferFit(s,cell,seed,HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation)));}return best;}
        int CarriedCropPractice(PrehistoryState s,int culture,int cell,int parent)
        {var holder=LocalSeedHolder(s,culture,cell,parent);int best=0;for(int k=0;k<6;k++)best=Math.Max(best,LocalSeedPracticeAt(s,culture,k,holder,cell,true));return best;}
        void AdvanceLocalTownSeeds(PrehistoryState s,int town)
        {
            if(!LocalSeedlotHistory(s))return;var a=s.History.Aggregates;var t=s.Society.Settlements[town];var city=a.Cities[town];
            if(a.Living.Cities[town].AbandonedYear>=0)return;
            bool cultivated=city.Rural>=10&&city.RuralCapacity>0&&t.CultivatedPermille>=50&&s.Society.Cultures[t.CultureId].Farming>=50;
            var extension=a.Empires.Connections.LocalSeeds;var record=extension.Towns[town];if(record==null){RegisterLocalTownSeeds(s,town,city.ParentCity);record=extension.Towns[town];}
            var crops=s.History.Agriculture[t.CultureId];bool managed=HistoricalTechnologies.Has(s.History.Cultures[t.CultureId],HistoricalTechnology.CropRotation);bool changed=false;
            for(int k=0;k<6;k++)
            {
                var lot=record[k];
                if(lot==null)
                {
                    if(!cultivated||crops.AdoptedYear[k]<=0||(CommunityNativeCropMask(s,t.Cell)&(1<<k))==0)continue;
                    var native=AggregateCultivar(s,t.CultureId,k,t.Cell);if(native==null||CommunityTransferFit(s,t.Cell,native,managed)<.12)continue;
                    record=record.With(k,new HistoricalLocalSeedlot(native,s.Years,-1,t.Cell,0,s.Years));changed=true;continue;
                }
                int elapsed=s.Years-lot.LastPracticeYear;if(elapsed<20)continue;
                // Review records time even in a failed field: no later arrears
                // are credited as if crops grew during abandonment or famine.
                double fit=cultivated?CommunityTransferFit(s,t.Cell,lot.Profile,managed):0;int years=fit<.08?0:HistoricalCropSelectionEffects.FractionalYears(D.Q(elapsed*Math.Min(1,s.Society.Cultures[t.CultureId].Farming/400.0)*fit,1000000000),HistoryRoll("town-seed-practice-14-"+k,town,s.Years));
                bool seasonalPhenology=s.History.Aggregates.Empires.Connections.Realms.Version>=17;
                double generations=seasonalPhenology&&cultivated?LocalSeedGenerations(s,town,k,lot,fit,elapsed):0;
                if(seasonalPhenology&&generations==0)years=0;
                var profile=lot.Profile.Copy();if(years>0){int target=(int)Math.Round(CropLatitude(t.Cell)*10),move=seasonalPhenology?0:HistoricalLocalSeedSelectionEffects.LatitudeMovement(profile.DaylengthLatitudeTenths,target,years,HistoryRoll("town-seed-latitude-14-"+k,town,s.Years));HistoricalLocalSeedSelectionEffects.Apply(profile,years,target,move);}
                if(seasonalPhenology&&cultivated)
                {int target=(int)Math.Round(CropLatitude(t.Cell)*10),move=HistoricalLocalSeedPhenology.Movement(lot.Profile.DaylengthLatitudeTenths,target,generations,HistoryRoll("local-seed-phenology-17-"+k,town,s.Years));profile.DaylengthLatitudeTenths+=Math.Sign(target-profile.DaylengthLatitudeTenths)*move;}
                record=record.With(k,new HistoricalLocalSeedlot(profile,lot.AcquiredYear,lot.SourceTown,lot.SourceCell,Math.Min(20000,lot.PracticeYears+years),s.Years));changed=true;
            }
            if(changed)extension.Towns=extension.Towns.With(town,record);
        }
        void RecordDeliveredLocalSeeds(PrehistoryState s,int sourceCell,int targetCell)
        {
            if(!LocalSeedlotHistory(s)||sourceCell<0||targetCell<0||sourceCell==targetCell)return;var a=s.History.Aggregates;
            int targetCulture=HistoricalCells.Culture(s,targetCell);if(targetCulture<0)return;
            int recipient=LocalSeedHolder(s,targetCulture,targetCell)?.TownId?? -1;if(recipient<0)return;var town=s.Society.Settlements[recipient];
            if(a.Living.Cities[recipient].AbandonedYear>=0||a.Cities[recipient].Rural<10||town.CultivatedPermille<100)return;
            int culture=HistoricalCells.Culture(s,sourceCell),source=a.TownAt[sourceCell];if(source>=0)culture=s.Society.Settlements[source].CultureId;if(culture<0||!InstitutionalRouteAllowed(s,sourceCell,targetCell,culture))return;
            var offer=LocalSeedHolder(s,culture,sourceCell);var local=LocalSeedsAt(s,recipient);if(offer==null||local==null)return;
            int group=(int)(HistoryRoll("delivered-seed-group-14",recipient,s.Years)*6);var lot=offer[group];if(lot==null)return;
            bool managed=HistoricalTechnologies.Has(s.History.Cultures[town.CultureId],HistoricalTechnology.CropRotation);
            double candidate=CommunityTransferFit(s,town.Cell,lot.Profile,managed),incumbent=local[group]==null?0:CommunityTransferFit(s,town.Cell,local[group].Profile,managed);
            if(candidate<.12||candidate<=incumbent*1.12+.02)return;
            a.Empires.Connections.LocalSeeds.Towns=a.Empires.Connections.LocalSeeds.Towns.With(recipient,local.With(group,new HistoricalLocalSeedlot(lot.Profile,s.Years,offer.TownId,sourceCell,0,s.Years)));
        }
        void ReceiveProvisionedSeedCargo(PrehistoryState s,int town,int supplier)
        {
            if(!LocalSeedlotHistory(s))return;var cargo=LocalSeedsAt(s,supplier);var held=LocalSeedsAt(s,town);if(cargo==null||held==null)return;
            int group=(int)(HistoryRoll("provisioned-seed-cargo-14",town,s.Years)*6);var seed=cargo[group];if(seed==null)return;
            // Actual paid delivery can replenish a harbor's carried inventory;
            // no local field, practice years or harvest are manufactured.
            var record=held.With(group,new HistoricalLocalSeedlot(seed.Profile,s.Years,supplier,s.Society.Settlements[supplier].Cell,0,s.Years));
            s.History.Aggregates.Empires.Connections.LocalSeeds.Towns=s.History.Aggregates.Empires.Connections.LocalSeeds.Towns.With(town,record);
        }
        HistoricalCropUse[] AssessLocalSeedCrops(PrehistoryState s,int cell,int culture)
        {
            int town=s.History.Aggregates.TownAt[cell];if(town>=0)culture=s.Society.Settlements[town].CultureId;else if(culture<0)culture=HistoricalCells.Culture(s,cell);
            if(culture<0||culture>=s.History.Cultures.Length||s.Society.Cultures[culture].Farming==0)return Array.Empty<HistoricalCropUse>();
            var holder=LocalSeedHolder(s,culture,cell);bool managed=HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation),pottery=HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.Pottery);
            var rows=Enumerable.Range(0,6).Select(k=>(Group:k,Seed:LocalSeedProfile(s,culture,k,holder))).Where(x=>x.Seed!=null).Select(x=>new HistoricalCropUse{Group=x.Group,CropId=x.Seed.CropId,Name=HistoricalCropCatalog.Crops[x.Seed.CropId].Name,OriginCulture=x.Seed.OriginCulture,SelectedYears=x.Seed.SelectedYears,Suitability=CommunityTransferFit(s,cell,x.Seed,managed),StorableSupport=D.Q(CommunityTransferFit(s,cell,x.Seed,managed)*HistoricalHusbandryEffects.StoragePermille(x.Group,x.Seed,pottery)/1000.0,1000000)}).Where(r=>r.Suitability>=.02).OrderByDescending(r=>r.Suitability).ThenBy(r=>r.CropId).ToArray();
            double sum=rows.Sum(r=>r.Suitability);int assigned=0;for(int i=0;i<rows.Length;i++){rows[i].SharePermille=i==rows.Length-1?1000-assigned:(int)Math.Floor(1000*rows[i].Suitability/sum);assigned+=rows[i].SharePermille;}return rows;
        }
        void ValidateLocalSeedlots(PrehistoryState s)
        {
            var extension=s.History.Aggregates.Empires.Connections.LocalSeeds;if(extension==null)return;
            if(!LocalSeedlotHistory(s)||extension.Version!=1||extension.SinceYear<0||extension.SinceYear>s.Years||extension.Towns==null)throw new ArgumentException("Invalid local seedlot extension.");
            if(!extension.Towns.AllPresentValues((town,record)=>
            {
                if(town>=s.Society.Settlements.Length||record.TownId!=town||record.FieldCell!=s.Society.Settlements[town].Cell||!record.ValidShape)return false;
                for(int k=0;k<6;k++)
                {var lot=record[k];if(lot==null)continue;var c=lot.Profile;if(c==null||c.CropId<0||c.CropId>=HistoricalCropCatalog.Crops.Length||HistoricalCropCatalog.Crops[c.CropId].Group!=k||c.SelectedYears<0||c.SelectedYears>20000||c.MaturityDays<=0||c.WaterRequirementPermille<=0||c.DaylengthLatitudeTenths<0||c.DaylengthLatitudeTenths>900||c.YieldPermille<0||c.YieldPermille>1500||lot.AcquiredYear<0||lot.AcquiredYear>s.Years||lot.LastPracticeYear<lot.AcquiredYear||lot.LastPracticeYear>s.Years||lot.PracticeYears<0||lot.PracticeYears>20000||lot.SourceTown< -1||lot.SourceTown>=s.Society.Settlements.Length||lot.SourceCell<0||lot.SourceCell>=CellPatch.Length)return false;}
                return true;
            }))throw new ArgumentException("Invalid local seedlot record.");
        }
    }
}

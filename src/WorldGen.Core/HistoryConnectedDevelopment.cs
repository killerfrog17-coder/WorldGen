using System;
using System.Linq;
namespace WorldGen.Core
{
    // Mechanistic game priors, not a fitted predictor of archaeological dates.
    // No Koppen label, continent ID or global date selects a favored people.
    public static class HistoricalDevelopmentEffects
    {
        public static double GrowingSeason(HabitatCell h)
            =>D.Q(D.Clamp(h.GrowingDays/300.0)*(1-h.PermafrostPermille/1000.0),1000000);
        public static double StorageSeason(HabitatCell h,double rainfallSeason)
        {
            double harvestSeason=Math.Max(D.Clamp(rainfallSeason),.35*D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/220.0));
            return D.Q(.45+.55*harvestSeason,1000000);
        }
        public static int FrontierBudget(HistoricalCulture c,int year,int centers,bool political=false)
        {
            double logistics=1+1.5*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Roads,year)
                +2*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Printing,year)
                +6*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Railways,year)
                +6*HistoricalModernEffects.Adoption(c,HistoricalTechnology.CombustionEngines,year);
            double support=1+Math.Min(4,Math.Sqrt(Math.Max(0,centers)));
            return Math.Max(1,Math.Min(64,(int)Math.Ceiling(logistics*support*(political?1:2))));
        }
        public static int ResearchCost(int technology)=>technology<15?100+technology*24:260+(technology-15)*22;
        public static double CoastRangeKm(HistoricalCulture c,int year,bool provisionedOceanCraft=false)
            =>c.Technical!=null?HistoricalTechnicalMigration.SeaRangeM(c,year,provisionedOceanCraft)/1000.0:HistoricalTechnologies.Has(c,HistoricalTechnology.OceanNavigation)?Math.Max(7000,HistoricalModernVoyageEffects.RangeM(c,year)/1000.0)
                :HistoricalTechnologies.Has(c,HistoricalTechnology.Navigation)?2500:HistoricalTechnologies.Has(c,HistoricalTechnology.Sailing)?650:120;
    }
    public sealed partial class Prehistory
    {
        public const string ConnectedHistoryVersion="history-19";
        public static bool IsAggregateHistoryVersion(string version)=>version==LivingWorldHistoryVersion||version==EnvironmentalHistoryVersion||version==AggregateHistoryVersion||version==LivingHistoryVersion||version==ConnectedHistoryVersion||version==RegionalHistoryVersion||version==CommunityHistoryVersion||version==DiffusionHistoryVersion||version==AgronomicHistoryVersion||version==TechnicalHistoryVersion||version==ProvincialFrontierHistoryVersion||version==ColonialHistoryVersion||version==CoastalHistoryVersion||version==TerritorialHistoryVersion||version==ResearchPacingHistoryVersion||version==LanguageHistoryVersion;
        static bool ConnectedHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==ConnectedHistoryVersion||ProvincialHistory(s);
        public static bool CanUpgradeHistory(PrehistoryState s)
        {
            string version=s?.History?.Version;
            return version==CurrentHistoryVersion?!RealmHistory(s)||s.History.Aggregates.Empires.Connections.Realms.ClaimRoutes?.SourceHeads==null||s.History.RegionalTraditions==null||s.History.Aggregates?.Empires?.Connections?.Geography?.Version!=2:IsAggregateHistoryVersion(version)||version=="history-9"||version=="history-10"||version=="history-11"||version=="history-12"||version=="history-13"||version=="history-14"||version=="history-15"||version=="history-16";
        }
        double[] developmentStored;
        public PrehistoryState UpgradeToConnectedHistory(PrehistoryState source)
        {
            Validate(source);if(ConnectedHistory(source))return source;
            if(!LivingHistory(source))source=UpgradeToLivingHistory(source);
            var s=source.Copy();s.History.Version=ConnectedHistoryVersion;PrepareConnectedEnvironment();
            // Setup/migration only; recurring events never enumerate these cells.
            foreach(var region in s.History.Aggregates.Regions)if(region.Culture<0)
            {
                int best=region.BestCell;double score=-1;
                foreach(int cell in Patches[region.Id].Cells)
                {double value=developmentStored[cell]+wildCropStorage[cell]*LandscapeCropFactor(cell)*HistoricalDevelopmentEffects.GrowingSeason(World.Ecology.Habitat[cell])*.25;if(value>score){score=value;best=cell;}}
                region.BestCell=best;
            }
            s.Seal();return s;
        }
        void PrepareConnectedEnvironment()
        {
            if(developmentStored!=null)return;PrepareLandscape();PrepareWildCrops();
            // Cultivar latitude data is a setup cache, never a lazy all-cell
            // allocation in the first crop-transfer or overseas-community turn.
            if(CellPatch.Length>0)CropLatitude(0);
            developmentStored=new double[CellPatch.Length];
            for(int cell=0;cell<developmentStored.Length;cell++)
            {
                var climate=World.Climate;var h=World.Ecology.Habitat[cell];
                double low=Math.Min(Math.Min(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Min(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
                double high=Math.Max(Math.Max(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Max(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
                double season=(high-low)/Math.Max(1,high);
                developmentStored[cell]=D.Q(landscapeYield[cell]*HistoricalDevelopmentEffects.GrowingSeason(h)*HistoricalDevelopmentEffects.StorageSeason(h,season),1000000);
            }
            PrepareDevelopmentShores();
        }
        int DevelopmentTeacher(PrehistoryState s,int culture,int technology,int fallback)
        {
            if(ProvincialHistory(s))return ProvincialTeacher(s,culture,technology);
            var c=s.Society.Cultures[culture];int best=-1,score=-1;
            foreach(int peer in c.Contacts.Concat(new[]{fallback}).Where(p=>p>=0&&p!=culture).Distinct().Take(17))
            {
                var hc=s.History.Cultures[peer];if(technology>=hc.Technology.Length||hc.Technology[technology]<=0||hc.Technology[technology]-1>=s.Years)continue;
                if(ProvincialHistory(s)&&!ProvincialHasOffer(s,culture,peer,technology))continue;
                int value=hc.Technology.Count(y=>y>0);if(value>score){best=peer;score=value;}
            }
            return best;
        }
        int DevelopmentEconomicSite(PrehistoryState s,int culture,int fallback)
        {
            int best=fallback;double score=LivingCropLandscape(s,culture,best,true);
            foreach(int site in s.History.Agriculture[culture].PracticeCell)
            {if(site==fallback||site==best)continue;double value=LivingCropLandscape(s,culture,site,true);if(value>score){best=site;score=value;}}
            return best;
        }
        bool DevelopmentResearchNetwork(PrehistoryState s,int culture,int technology,bool water)
        {
            var h=s.History;var c=s.Society.Cultures[culture];var hc=h.Cultures[culture];var own=h.Aggregates.Cultures[culture];
            if(technology<15||own.CivilizationSince<0||own.SupportedUrban<5000||own.SupportedCities<1||hc.Institution<550||hc.Surplus<350)return false;
            int centers=own.SupportedCities;long people=own.SupportedUrban;int peers=0;
            foreach(int peer in c.Contacts)
            {
                var other=h.Aggregates.Cultures[peer];if(other.CivilizationSince<0||other.SupportedCities<1)continue;
                if(ProvincialHistory(s)&&!ProvincialRecentContact(s,culture,peer,true))continue;
                centers+=Math.Min(2,other.SupportedCities);people+=Math.Min(50000,other.SupportedUrban)/3;if(++peers>=6)break;
            }
            int requiredCenters=technology>=27?4:technology>=20?3:2;long requiredPeople=technology>=27?100000:technology>=20?50000:15000;
            if(centers<requiredCenters||people<requiredPeople)return false;
            if(technology==(int)HistoricalTechnology.OceanNavigation&&!water)return false;
            if((technology==(int)HistoricalTechnology.CropRotation||technology==(int)HistoricalTechnology.ArtificialFertilizers||technology==(int)HistoricalTechnology.MechanizedAgriculture)&&!AgronomicResearchSupported(s,culture))return false;
            return true;
        }
        int DevelopmentCropDonor(PrehistoryState s,int culture,int group,int site,int fallback)
        {
            int best=-1;double score=.12;var h=World.Ecology.Habitat[site];double latitude=CropLatitude(site);
            foreach(int peer in s.Society.Cultures[culture].Contacts.Concat(new[]{fallback}).Where(p=>p>=0&&p!=culture).Distinct().Take(17))
            {
                var a=s.History.Agriculture[peer];if(a.AdoptedYear[group]<=0||a.AdoptedYear[group]-1>=s.Years||a.Cultivars?[group]==null)continue;
                if(ProvincialHistory(s)&&!ProvincialRecentContact(s,culture,peer,false))continue;
                if(LivingWorldHistory(s)&&a.AdoptedYear[group]-1>EnvironmentalContactYear(s,culture,peer))continue;
                double value=CommunityHistory(s)?CommunityTransferFit(s,site,a.Cultivars[group]):HistoricalCropCatalog.TransferFit(h,a.Cultivars[group],latitude,riverSites[site]);if(value>score){best=peer;score=value;}
            }
            return best;
        }
        void StudyDevelopmentCropSites(PrehistoryState s,int culture,int cell)
        {
            if(cell<0||CellPatch[cell]<0)return;var crops=s.History.Agriculture[culture];
            for(int k=0;k<6;k++)
            {
                if(crops.AdoptedYear[k]>0)continue;
                int mask=CommunityHistory(s)?CommunityNativeCropMask(s,cell):nativeCrops[cell];
                if((mask&(1<<k))!=0){crops.NativeMask|=1<<k;if(crops.WildOriginCell[k]<0)crops.WildOriginCell[k]=cell;}
                int old=crops.PracticeCell[k];double prior=(CommunityHistory(s)?CommunityWildCropFit(s,old,k):CropSuitability(old,k))*HistoricalDevelopmentEffects.GrowingSeason(World.Ecology.Habitat[old]);
                double value=(CommunityHistory(s)?CommunityWildCropFit(s,cell,k):CropSuitability(cell,k))*HistoricalDevelopmentEffects.GrowingSeason(World.Ecology.Habitat[cell]);
                if(value>prior)crops.PracticeCell[k]=cell;
            }
        }
        void AdvanceDevelopmentFrontier(PrehistoryState s,int polity,int elapsed)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var account=a.Polities[polity];var hc=h.Cultures[p.CultureId];
            if(p.Cohesion<450-(RealmHistory(s)?150*RealmBureaucracy(s,polity):0)||p.Treasury<100)return;
            int budget=HistoricalDevelopmentEffects.FrontierBudget(hc,s.Years,account.CityCount,true);
            int capital=s.Society.Settlements[p.CapitalId].Cell;
            bool colonialFronts=ProvincialFrontierHistory(s)&&(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)>.25||HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years)>.25||ColonialHistory(s)&&HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation));
            for(int trial=0;trial<budget;trial++)
            {
                int target=colonialFronts&&trial<budget/2?ProvincialFrontierTarget(s,polity,trial):-1;
                if(target<0)target=LocalFrontierTarget(s,account.Frontier,polity,"development-state-front-"+trial,1);if(target<0)continue;
                target=PreferTerritorialFrontier(s,polity,target,trial);
                int other=HistoricalCells.Polity(s,target);
                if(other>=0){if(other!=polity&&trial<3)ConsiderAggregateDiplomacy(s,polity,other,h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId],elapsed);continue;}
                if(ColonialHistory(s)&&RespectRecognizedClaim(s,polity,target))continue;
                int region=CellPatch[target];var habitat=World.Ecology.Habitat[target];
                double distance=LivingTravelKm(s,capital,target,p.CultureId);
                if(!TechnicalHistory(s)&&distance>account.ReachKm*(1+.15*Math.Log(1+account.CityCount)))continue;
                if(region<0&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways))continue;
                if(region>=0&&s.Population[region]+a.Living.Regions[region].SettledPopulation<25&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways))continue;
                double burden=1+habitat.ReliefM/700.0+(habitat.AridityMilli<150?2:0);
                long cost=Math.Max(50,(long)((TechnicalHistory(s)?TerritorialPoliticalArea(target):inhabitableArea[target])*.025*burden));if(p.Treasury<cost)continue;
                double chance=(.25+.5*p.Legitimacy/1000.0)/burden;
                int center=TechnicalHistory(s)?TerritorialServiceSource(s,polity,target):ProvincialHistory(s)?ProvincialServiceCenter(s,polity,target):-1;
                if(TechnicalHistory(s)&&center== -1)center=TryEstablishTerritorialPost(s,polity,target);
                if(ProvincialHistory(s)&&(TechnicalHistory(s)?center== -1:center<0))continue;
                if(TechnicalHistory(s)?!TerritorialIncorporationWorthwhile(s,polity,target,center):ProvincialHistory(s)&&!ProvincialIncorporationWorthwhile(s,polity,target,center))continue;
                if(HistoryRoll("development-incorporation-"+trial,polity,s.Years)>1-Math.Exp(-chance*elapsed/5.0))continue;
                ClaimAggregatePolity(s,target,polity);p.Treasury-=cost;
                if(ProvincialHistory(s))a.Provinces.Centers=a.Provinces.Centers.With(target,center>=0?center:p.CapitalId);
                if(TechnicalHistory(s))AssignTerritorialSource(s,target,center);
                int localCulture=HistoricalCells.Culture(s,target);if(localCulture>=0)LivingCultureContact(s,p.CultureId,localCulture);
            }
        }
    }
}

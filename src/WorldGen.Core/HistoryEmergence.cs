using System;
using System.Linq;
namespace WorldGen.Core
{
    // Explicit gameplay priors. No continent, latitude, racial ranking or global
    // quota of origin centers: independent transitions have local opportunity costs.
    public static class HistoricalEmergenceEffects
    {
        public static double EcologicalConcentration(HabitatCell h,double concentration,double storage,double aquatic)
        {
            double season=D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0);
            double soil=.15+.85*D.Clamp(h.SoilFertilityPermille/650.0);
            double plants=D.Clamp(storage/.4)*soil*HistoricalCropCatalog.LeachingPenalty(h,false);
            double resources=Math.Max(plants,D.Clamp(aquatic));
            return D.Q(.015+.985*Math.Pow(D.Clamp(resources*season*(.2+.8*D.Clamp(concentration))),2),1000000000);
        }
        public static double CultivationIncentive(double crowding,double cropFit,double concentration,double foragingAbundance)
        {
            double pressure=D.Clamp((crowding-.72)/.4);
            double advantage=D.Clamp((cropFit-.4)/.5);
            // Secure, abundant wild food competes with labor-intensive cultivation.
            return D.Q(D.Clamp((.08+.6*pressure+.45*advantage+.2*D.Clamp(concentration))/(1+.65*D.Clamp(foragingAbundance))),1000000);
        }
        public static double NativePractice(double domesticability,double incentive)
            =>D.Q((.002+.998*Math.Pow(D.Clamp(domesticability),4))*D.Clamp(incentive),1000000000);
        public static int FarmingLimit(double cropFit,double incentive)
            =>Math.Max(50,Math.Min(850,(int)Math.Round(50+800*D.Clamp(cropFit)*(.25+.75*D.Clamp(incentive)))));
        public static double OriginMultiplier(double fiscal,double stored,double coordination,double circumscription,double exchange,int examples)
        {
            double core=D.Clamp(fiscal/.7)*D.Clamp(stored/.55)*D.Clamp(.65*coordination+.55*circumscription+.25*exchange);
            double independent=.0001+.025*Math.Pow(core,6);
            // Learning helps establish a court, not make unsuitable land fertile.
            return D.Q(independent+(.65-independent)*D.Clamp(examples/800.0),1000000000);
        }
        public static double EarlyGovernmentScale(bool writing,bool roads,bool metallurgy)
            =>writing?(roads?1:.8):(metallurgy?.65:.5);
    }
    public sealed partial class Prehistory
    {
        static bool EmergenceHistory(PrehistoryState s)=>(s.History?.Version=="history-15"||s.History?.Version=="history-16"||(s.History?.Version==AggregateHistoryVersion||s.History?.Version==LivingHistoryVersion));
        double NativeDomesticability(int cell,int crop)
        {
            int province=World.Ecology.Province[cell];
            // Related cereal/pulse progenitors share a province-level opportunity;
            // gardens/orchards have other opportunities. These are replaceable
            // trait priors, not a claim our small life pack inventories wild crops.
            int group=crop<3||crop==5?0:crop;
            return D.Q(Math.Pow(Rng.Value(World.Recipe.Seed,"domestication-progenitor-15",unchecked(province*7+group)),2),1000000);
        }
        double CultivationIncentive(PrehistoryState s,int cell,double fit)
        {
            int patch=CellPatch[cell];double crowding=s.Population[patch]/(double)Math.Max(1,CapacityAt(patch,s.Years,false));
            double wild=ruralWeight[cell]/Math.Max(1,HistoricalLandArea(cell)*1000)/.15;
            return HistoricalEmergenceEffects.CultivationIncentive(crowding,fit,CommunityConcentration(cell),wild);
        }
        double StateOriginMultiplier(PrehistoryState s,Settlement town)
        {
            var context=CivicContext(s,town.Cell);
            if(RealismHistory(s))
            {
                double urbanCoordination=D.Clamp(Math.Log(1+town.Population/5000.0)/Math.Log(21));
                double core=D.Clamp(LocalFiscalPotential(s,town.CultureId,town.Cell)/.4)*D.Clamp(LocalStoredSupport(s,town.CultureId,town.Cell)/.35)*D.Clamp(.65*context.Coordination+.55*context.Circumscription+.25*LocalExchangeConcentration(s,town)+.25*urbanCoordination);
                double independent=.00001+.3*Math.Pow(core,8);
                return D.Q(independent+(.85-independent)*D.Clamp(s.History.Towns[town.Id].CivicExampleExposure/800.0),1000000000);
            }
            return HistoricalEmergenceEffects.OriginMultiplier(LocalFiscalPotential(s,town.CultureId,town.Cell),LocalStoredSupport(s,town.CultureId,town.Cell),context.Coordination,context.Circumscription,LocalExchangeConcentration(s,town),s.History.Towns[town.Id].CivicExampleExposure);
        }
        void LearnLocalInstitutions(PrehistoryState s,Settlement town,int[] neighbors)
        {
            var h=s.History;var record=h.Towns[town.Id];
            int source=neighbors.Select(id=>s.Society.Settlements[id]).Where(t=>t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0&&s.Years-h.Polities[t.PolityId].FoundedYear>=50&&t.Population>=1000&&t.FoodCapacity>=t.Population*.8).OrderBy(t=>t.PolityId).ThenBy(t=>t.Id).Select(t=>t.PolityId).DefaultIfEmpty(-1).First();
            if(town.PolityId>=0){record.CivicTeacherPolityId=town.PolityId;record.CivicExampleExposure=1000;return;}
            if(source>=0){record.CivicTeacherPolityId=source;record.CivicExampleExposure=Math.Min(1000,record.CivicExampleExposure+5);}
            else record.CivicExampleExposure=Math.Max(0,record.CivicExampleExposure-2);
        }
    }
}

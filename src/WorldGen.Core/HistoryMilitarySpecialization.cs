using System;
namespace WorldGen.Core
{
    // Operational priors, not Earth-nation templates or claims that terrain
    // alone decided any historical campaign. Existing supply, war exhaustion,
    // commanders, fortifications and political settlement still matter.
    public static class HistoricalMilitarySpecializationEffects
    {
        public static double MountedTerrain(HabitatCell habitat,HistoricalClimateAnomaly weather,double engineering,double dryForage=0)
        {
            bool wetland=(habitat.Habitat&Habitat.Wetland)!=0;
            double open=1-.9*D.Clamp(AnimalEcotypes.Canopy(habitat)/850.0),relief=1/(1+Math.Pow(Math.Max(0,habitat.ReliefM)/650.0,1.5));
            double wet=Math.Max(habitat.FloodFractionPermille/1000.0,wetland?.75:0);
            if(habitat.Soil==SoilKind.Alluvial||wetland)wet+=Math.Max(0,weather.PrecipitationPermille/1000.0-1)*.6;
            double waterlogged=1-.85*D.Clamp(wet);
            double pasture=D.Clamp((habitat.AridityMilli*weather.PrecipitationPermille/1000.0-35)/250.0);
            pasture+=D.Clamp(dryForage)*.65*(1-pasture);
            double frost=1-.5*D.Clamp((habitat.PermafrostPermille-200)/700.0);
            double terrain=open*relief*waterlogged*(.15+.85*pasture)*frost;
            // Roads and engineering help logistics but cannot turn a wet closed
            // forest into unrestricted grazing and mounted maneuver terrain.
            return D.Q(D.Clamp(terrain+.22*D.Clamp(engineering)*(1-terrain)),1000000);
        }
        public static double MountedFieldMultiplier(double mountedShare,double terrain,double motorization)
        {
            double share=D.Clamp(mountedShare)*(1-D.Clamp(motorization));
            return D.Q(1+share*(-.5+1.2*D.Clamp(terrain)),1000000);
        }
        public static double FortifiedMountedMultiplier(double mountedShare,double defendingOrganization,double siegeEngineering,double motorization)
            =>D.Q(1-.35*D.Clamp(mountedShare)*(1-D.Clamp(motorization))*D.Clamp(defendingOrganization)*(1-.8*D.Clamp(siegeEngineering)),1000000);
        public static bool PortSupply(bool sourceShore,bool frontShore,bool supported,long reserve,long residents,double sailing,double navigation,bool routeAllowed)
            =>sourceShore&&frontShore&&supported&&residents>=500&&reserve>=residents*.2&&sailing>=.2&&navigation>=.2&&routeAllowed;
    }
    public sealed partial class Prehistory
    {
        readonly struct MilitaryRidingAssets
        {
            public readonly double Practice,DryForage;
            public MilitaryRidingAssets(double practice,double dryForage){Practice=practice;DryForage=dryForage;}
        }
        double MilitaryRidingPractice(PrehistoryState s,int culture,int site)=>AssessMilitaryRiding(s,culture,site).Practice;
        MilitaryRidingAssets AssessMilitaryRiding(PrehistoryState s,int culture,int site)
        {
            var animals=s.History.Agriculture[culture].Animals;if(animals==null)return new MilitaryRidingAssets(0,0);
            double best=0,dryForage=0;var habitat=CropHabitat(s,site);
            // Fixed livestock portfolio, not all world taxa, culture members,
            // cells, herds or individuals. The authored pack is finite, and the
            //32-entry operational sample is an explicit approximation.
            for(int k=0;k<Math.Min(32,animals.Length);k++)
            {
                var managed=animals[k];if(managed.AdoptedYear<=0||managed.AdoptedYear>s.Years||managed.TaxonId<0||managed.TaxonId>=World.Ecology.Taxa.Length)continue;
                var taxon=World.Ecology.Taxa[managed.TaxonId];double riding=AnimalEcotypes.RidingSuitability(taxon)/1000.0;
                if(riding<=0||!Biogeography.Suitable(taxon,habitat,World.ElevationM[site]))continue;
                double continuity=D.Clamp((s.Years-managed.AdoptedYear+1)/150.0);
                double strength=riding*D.Clamp(managed.Practice/800.0)*(.25+.75*continuity);
                if(strength>best)
                {
                    best=strength;
                    dryForage=(taxon.Habitats&Habitat.Desert)!=0&&(taxon.Eats&Food.Browse)!=0?D.Clamp((500-taxon.MinAridityMilli)/500.0):0;
                }
            }
            return new MilitaryRidingAssets(best,dryForage);
        }
        double MilitarySpecialization(PrehistoryState s,int polity,int city,int front)
        {
            var h=s.History;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var town=s.Society.Settlements[city];
            var assets=AssessMilitaryRiding(s,p.CultureId,town.Cell);double riding=assets.Practice,dryForage=assets.DryForage;
            if(town.CultureId!=p.CultureId){var local=AssessMilitaryRiding(s,town.CultureId,town.Cell);if(local.Practice*.8>riding){riding=local.Practice*.8;dryForage=local.DryForage;}}
            double pastoral=D.Clamp(s.Society.Cultures[town.CultureId].Herding/400.0);
            double mountedShare=riding*(.25+.55*pastoral),motorization=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
            double engineering=.35*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)+.25*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Steel,s.Years)+.4*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Gunpowder,s.Years);
            double terrain=HistoricalMilitarySpecializationEffects.MountedTerrain(CropHabitat(s,front),LivingClimate(s,front),engineering,dryForage);
            double factor=HistoricalMilitarySpecializationEffects.MountedFieldMultiplier(mountedShare,terrain,motorization);
            int defendingCity=h.Aggregates.TownAt[front],owner=HistoricalCells.Polity(s,front);
            if(defendingCity>=0&&owner>=0&&HistoricalImperialHierarchy.Sovereign(s,owner)!=HistoricalImperialHierarchy.Sovereign(s,polity))
            {
                var defense=h.Cultures[h.Polities[owner].CultureId];
                double organization=D.Clamp((h.Dynamics.States[owner].PublicWorks/1000.0+HistoricalModernEffects.Adoption(defense,HistoricalTechnology.Writing,s.Years))*.5);
                factor*=HistoricalMilitarySpecializationEffects.FortifiedMountedMultiplier(mountedShare,organization,engineering,motorization);
            }
            return D.Q(factor,1000000);
        }
        bool MilitaryMaritimeSupply(PrehistoryState s,int polity,int city,int front)
        {
            var h=s.History;var town=s.Society.Settlements[city];var hc=h.Cultures[h.Polities[polity].CultureId];
            bool shore=DiffusionShore(town.Cell)&&DiffusionShore(front);
            if(!shore)return false;
            bool physical=InstitutionalRouteAllowed(s,town.Cell,front,h.Polities[polity].CultureId)&&DiffusionSeaLane(town.Cell,front);
            return HistoricalMilitarySpecializationEffects.PortSupply(true,true,h.Aggregates.Cities[city].Supported,town.FoodReserve,town.Population,
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),physical);
        }
    }
}

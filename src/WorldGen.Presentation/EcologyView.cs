using System;
using System.Globalization;
using System.Linq;
using System.Text;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class EcologyView
    {
        public static bool IsLayer(Layer layer) => layer>=Layer.Ecoregions && layer<=Layer.FirePotential;
        public static string Summary(World w,int cell)
        {
            if(w?.Ecology==null||cell<0||cell>=w.Surface.Cells.Length)return "Select an ecoregion";
            int id=w.Ecology.Region[cell];if(id<0)return "Open ocean. The current life pack describes terrestrial and freshwater communities, not a complete marine ecosystem.";
            var r=w.Ecology.Regions[id];var h=w.Ecology.Habitat[cell];var b=new StringBuilder();
            string F(double value)=>value.ToString("0.#",CultureInfo.InvariantCulture);
            b.Append(r.Name).Append("\n").Append(h.Biome).Append(" · ").Append(F(r.AreaM2/1e6)).Append(" km² across ").Append(r.PatchCount).Append(" connected habitat patches.\n\n");
            b.Append("At this location, the growing season lasts about ").Append(h.GrowingDays).Append(" days. Seasonal temperature estimates range from ").Append(F(h.ColdTenthsC/10.0)).Append(" to ").Append(F(h.WarmTenthsC/10.0)).Append(" °C. ");
            b.Append(h.AridityMilli<500?"Water is scarce relative to evaporative demand. ":h.AridityMilli>1200?"Rainfall generally exceeds evaporative demand. ":"Rainfall and evaporative demand are broadly balanced, with seasonal differences. ");
            b.Append("\n\nThe soil proxy is ").Append(h.Soil.ToString().ToLowerInvariant()).Append(", with a relative fertility index of ").Append(F(h.SoilFertilityPermille/10.0)).Append("%. ");
            if(h.FloodDaysPerYear>0)b.Append("Potential flooding affects ").Append(F(h.FloodFractionPermille/10.0)).Append("% of the cell for about ").Append(h.FloodDaysPerYear).Append(" days a year. ");
            if(h.PermafrostPermille>=100)b.Append("Permafrost potential is ").Append(F(h.PermafrostPermille/10.0)).Append("%. ");
            if(h.SalinityMilliPpt>1000)b.Append("Estimated water salinity is ").Append(F(h.SalinityMilliPpt/1000.0)).Append(" ppt. ");
            var species=r.Species.Select(p=>w.Ecology.Taxa[p.TaxonId]).ToArray();
            b.Append("\n\nThe representative life pack records ").Append(species.Length).Append(" taxa and ").Append(r.FoodWeb.Length).Append(" potential feeding links here. ");
            b.Append("Examples include ").Append(string.Join(", ",species.Where(t=>!t.BasalResource).OrderBy(t=>t.Id).Take(8).Select(t=>t.Name))).Append(".\n\nThese are habitat and food-web estimates, not a census of living animal populations.");return b.ToString();
        }
        static V Mix(V a,V b,double fraction) => a*(1-D.Clamp(fraction))+b*D.Clamp(fraction);
        public static V Color(World w,int i,Layer layer)
        {
            if(w.Ecology==null) return new V(.24,.24,.24);
            var e=w.Ecology; var a=e.Habitat[i];
            if(w.Hydrology.OceanConnected[i]) return new V(.025,.09,.17);
            switch(layer)
            {
                case Layer.Aridity:return Mix(new V(.78,.53,.21),new V(.05,.47,.49),a.AridityMilli/2000.0);
                case Layer.Floodplains:return Mix(new V(.22,.24,.2),new V(.15,.7,.87),a.FloodFractionPermille/500.0);
                case Layer.WaterTable:return Mix(new V(.08,.48,.7),new V(.67,.45,.2),a.WaterTableCm/2500.0);
                case Layer.Permafrost:return Mix(new V(.27,.36,.22),new V(.72,.84,.96),a.PermafrostPermille/1000.0);
                case Layer.Salinity:return Mix(new V(.14,.47,.64),new V(.91,.77,.84),a.SalinityMilliPpt/35000.0);
                case Layer.FirePotential:return Mix(new V(.22,.33,.22),new V(.93,.31,.1),a.FirePotentialPermille/1000.0);
                case Layer.Soils:uint soil=Rng.Mix((uint)a.Soil+879); return new V(.25+(soil&255)/510.0,.2+((soil>>8)&255)/650.0,.12+((soil>>16)&255)/800.0);
            }
            V color;
            switch(a.Biome)
            {
                case Biome.Ice:color=new V(.94,.97,.99);break;
                case Biome.Tundra: case Biome.Alpine:color=new V(.63,.64,.7);break;
                case Biome.BorealForest:color=new V(.21,.43,.4);break;
                case Biome.TemperateForest:color=new V(.35,.58,.28);break;
                case Biome.TemperateRainforest:color=new V(.18,.49,.32);break;
                case Biome.TropicalRainforest:color=new V(.08,.48,.19);break;
                case Biome.TropicalSeasonalForest:color=new V(.47,.62,.22);break;
                case Biome.Savanna:color=new V(.76,.74,.31);break;
                case Biome.Grassland:color=new V(.65,.7,.44);break;
                case Biome.Desert:color=new V(.85,.68,.43);break;
                case Biome.MediterraneanWoodland:color=new V(.7,.58,.35);break;
                case Biome.Freshwater:color=new V(.13,.47,.8);break;
                case Biome.SalineLake:color=new V(.47,.54,.85);break;
                default:color=new V(.26,.66,.63);break;
            }
            if(layer==Layer.Ecoregions && e.Region[i]>=0)
            {
                // Biome remains the dominant visual signal. Moisture/fertility
                // add modest habitat shading; region identity is only a tint.
                double moisture=D.Clamp(a.AridityMilli/1600.0),fertility=D.Clamp(a.SoilFertilityPermille/1000.0);
                color=color*(1.04-.08*moisture)+new V(0,.025*(fertility-.5),0);
                uint hash=Rng.Mix((uint)e.Region[i]+8109);color=Mix(color,new V((hash&255)/255.0,((hash>>8)&255)/255.0,((hash>>16)&255)/255.0),.08);
            }
            return color;
        }
        public static string Describe(World w,int cell)
        {
            if(w.Ecology==null) return "\nEcology: this older save has no ecoregion data. Regenerate, then save a new copy.";
            var e=w.Ecology; int id=e.Region[cell]; if(id<0) return "\nEcology: open-ocean communities are not included in the terrestrial starter pack.";
            var a=e.Habitat[cell]; var r=e.Regions[id]; var b=new StringBuilder(); var culture=CultureInfo.InvariantCulture;
            string F(double x)=>x.ToString("0.##",culture);
            string Mass(double kg)=>kg<.001 ? F(kg*1000000)+" mg" : kg<1 ? F(kg*1000)+" g" : F(kg)+" kg";
            b.Append("\n\nECOREGION ").Append(id+1).Append(": ").Append(r.Name).Append("\nBiome: ").Append(a.Biome).Append(" | ").Append(F(r.AreaM2/1e6)).Append(" km2 | ").Append(r.PatchCount).Append(" patches");
            b.Append("\nAridity P/PET: ").Append(F(a.AridityMilli/1000.0)).Append(" | PET ").Append(a.PetMm).Append(" mm/year | seasonal deficit ").Append(a.WaterDeficitMm).Append(" mm");
            b.Append("\nSoil proxy: ").Append(a.Soil).Append(" | fertility ").Append(F(a.SoilFertilityPermille/10.0)).Append("% | water-table estimate ").Append(F(a.WaterTableCm/100.0)).Append(" m below ground");
            b.Append("\nFlooding potential: ").Append(a.Flooding).Append(" | ").Append(F(a.FloodFractionPermille/10.0)).Append("% of cell | ").Append(a.FloodDaysPerYear).Append(" days/year | ").Append(a.FloodPulsesPerYear).Append(" pulses/year (proxy)");
            b.Append("\nSalinity estimate: ").Append(F(a.SalinityMilliPpt/1000.0)).Append(" ppt | permafrost likelihood ").Append(F(a.PermafrostPermille/10.0)).Append("% | fire suitability ").Append(F(a.FirePotentialPermille/10.0)).Append('%');
            b.Append("\nPotential productivity: ").Append(a.ProductivityGCM2Year).Append(" gC/m2/year (proxy)");
            b.Append("\n\nLIFE — ").Append(e.PackVersion).Append(" (representative; not exhaustive)");
            foreach(var presence in r.Species)
            {
                var t=e.Taxa[presence.TaxonId];
                b.Append("\n").Append(Biogeography.Present(e,cell,t.Id)?"* ":"  ").Append(t.Name).Append(" [").Append(t.Rank).Append("] ").Append(t.ScientificName);
                if(t.Lineage?.AdjacentSeaOnly==true)b.Append(" | adjacent-sea feeding habitat, not a land resident");
                b.Append(" | ").Append(F(presence.SuitableAreaPermille/10.0)).Append("% of region");
                if(!t.Producer && !t.BasalResource) b.Append(" | ").Append(Mass(t.MassKg)).Append(" | hunts ").Append(t.Hunts);
                if(presence.UsedHistoricalBridge) b.Append(" | historical-route access");
                if(presence.Variant!=null){var v=presence.Variant;b.Append("\n    ").Append(v.Name).Append(" · ").Append(Mass(v.MassKg)).Append(" · insulation ").Append(F(v.InsulationPermille/10.0)).Append('%');if(v.SeasonalWhiteningPermille>0)b.Append(" · seasonal white coat ").Append(F(v.SeasonalWhiteningPermille/10.0)).Append('%');}
                if(t.Traits!=null)b.Append(" | captive breeding ").Append(t.Traits.CaptiveBreeding/10).Append("% · docility ").Append(t.Traits.Docility/10).Append("% · panic resistance ").Append(t.Traits.PanicResistance/10).Append("% (authored trait indices)");
            }
            b.Append("\n* Suitable and accessible in this cell; other entries occur elsewhere in this ecoregion.");
            b.Append("\n\nFOOD WEB — resource -> consumer (potential feeding, not population simulation)");
            foreach(var link in r.FoodWeb)
                b.Append("\n").Append(e.Taxa[link.ResourceId].Name).Append(" -> ").Append(e.Taxa[link.ConsumerId].Name).Append(" | ").Append(link.Resource).Append(" | co-occurs on ").Append(F(link.OverlapPermille/10.0)).Append("% of region");
            return b.ToString();
        }
    }
}

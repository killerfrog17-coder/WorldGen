using System;
using System.Linq;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public static class CampWeathering
    {
        // Approximate gameplay service lives, not engineering guarantees.
        // Moisture, warmth, ground contact and exposure govern the accumulated
        // dose. A return after a year follows the same calculation as daily visits.
        public static Possession[] Advance(CampObject camp,double calendarDay,double annualRainMm,double meanC,double canopy,double windMps=3)
        {
            if(camp==null||!double.IsFinite(calendarDay)||calendarDay<0||!double.IsFinite(annualRainMm)||!double.IsFinite(meanC)||!double.IsFinite(canopy)||!double.IsFinite(windMps))throw new ArgumentException("Finite camp weather and calendar time are required.");
            if(camp.WeatheredThroughCalendarDay<0){camp.WeatheredThroughCalendarDay=calendarDay;return Array.Empty<Possession>();}
            double days=Math.Max(0,calendarDay-camp.WeatheredThroughCalendarDay);if(days<1)return Array.Empty<Possession>();
            camp.WeatheredThroughCalendarDay=calendarDay;
            if(camp.Kind==CampKind.Bedding){HumanCampLayout.AgeBedding(camp,days,annualRainMm);return Array.Empty<Possession>();}
            var plan=camp.Blueprint;if(plan==null)return Array.Empty<Possession>();
            double damp=.12+Math.Min(3,Math.Max(0,annualRainMm)/900),warm=Math.Max(.04,Math.Min(1.65,(meanC+5)/20));
            double exposure=(1-D.Clamp(canopy)*.65)*(.7+Math.Min(20,Math.Max(0,windMps))*.1);
            List<Possession> debris=null;bool changed=false;
            void Fall(CampPartState state,CampComponent part,bool rotten)
            {
                debris??=new List<Possession>();
                foreach(var material in state.Materials)
                {
                    if(rotten){material.MassKg*=part.Kind==CampPartKind.Cover?.25:.6;material.UseDamage=Math.Max(.85,material.UseDamage);if(WoodCrafting.Wood(material))material.WoodSoundness=Math.Min(.3,material.WoodSoundness);}
                    material.Place=PossessionPlace.Ground;material.Position=camp.Position;material.ContainerId="";material.CampOwner=null;material.CampMaterial=0;
                    material.GroundMoving=material.InFlight=false;material.GroundRotation=new V(90,0,0);material.GroundOffsetM=.03;debris.Add(material);
                }
                plan.Parts.Remove(state);changed=true;
            }
            var definitions=CampBlueprints.Components(plan);
            foreach(var part in definitions)
            {
                var state=CampBlueprints.State(plan,part);if(state==null||state.Materials.Count==0)continue;
                double life=part.Kind==CampPartKind.Cover?plan.Cover==CampCover.Brush?240:plan.Cover==CampCover.Reed?720:plan.Cover==CampCover.Hide?540:1200:
                    part.Kind==CampPartKind.Binding?900:part.Kind==CampPartKind.Post?1825:part.Kind==CampPartKind.Stonework?50000:part.Kind==CampPartKind.Bonework?12000:3650;
                uint hash=2166136261;foreach(char ch in camp.Id+":"+part.Key)hash=unchecked((hash^ch)*16777619);
                life*=.75+(hash%1001)/2000d;
                double contact=part.Kind==CampPartKind.Post?1.8:part.Kind==CampPartKind.Cover?1:.65;
                double dose=days*(damp*warm*contact+exposure*.12)/life;
                int before=(int)(state.Weathering*16);state.Weathering=Math.Min(1,state.Weathering+dose);
                changed|=before!=(int)(state.Weathering*16);
                if(state.Weathering<1)continue;
                // Fallen/rotted remains retain their identity and provenance;
                // decomposition loses mass, it never generates replacement stock.
                Fall(state,part,true);
            }
            var feet=definitions.Where(p=>p.Kind==CampPartKind.Post||p.Kind==CampPartKind.Rib&&p.Stage==0).ToArray();
            if(feet.Length>0&&!feet.Any(p=>CampBlueprints.HasMaterial(plan,p)))
                foreach(var remaining in plan.Parts.ToArray())
                {var part=definitions.First(p=>p.Key==remaining.Key);if(part.Kind!=CampPartKind.Stonework&&part.Kind!=CampPartKind.Bonework)Fall(remaining,part,false);}
            if(changed)plan.Revision++;
            return debris?.ToArray()??Array.Empty<Possession>();
        }
    }
}

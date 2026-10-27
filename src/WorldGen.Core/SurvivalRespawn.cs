using System;
using System.Linq;

namespace WorldGen.Core
{
    public static class SurvivalRespawn
    {
        static bool Point(V p)=>SurvivalMath.Finite(p.Length)&&Math.Abs(p.Length-1)<=.00001;
        public static bool TryApply(SurvivalState state,V deathPoint,V startingPoint,out V destination,out string message)
        {
            destination=startingPoint;message="There is no dead player to respawn.";
            if(state==null||!state.Dead)return false;
            if(!Point(deathPoint)||!Point(startingPoint)){message="Invalid respawn location.";return false;}
            var bed=state.Sites.Where(s=>s.Progress>=1&&(s.Kind=="bed"||s.Kind=="hide-bed"))
                .OrderBy(s=>(s.Position-deathPoint).Length).ThenBy(s=>s.Id).FirstOrDefault();
            if(bed!=null)destination=bed.Position;
            var sites=state.Sites;
            if(state.Inventory.Items.Length>0)
            {
                if(state.NextSite<1||state.NextSite==long.MaxValue){message="Camp identifiers are exhausted.";return false;}
                var contents=state.Inventory.Items.Select(i=>{var copy=i.Copy();copy.Worn=false;copy.Container=-1;copy.X=copy.Y=0;copy.Rotated=false;return copy;}).ToArray();
                sites=sites.Concat(new[]{new SurvivalSite{Id=state.NextSite,Kind="drop",Position=deathPoint,Contents=contents}}).ToArray();
            }
            // All fallible preparation precedes publication. Respawning grants
            // no starter kit and does not reset world time, camps or harvests.
            var inventory=new SurvivalInventory();
            if(sites.Length>state.Sites.Length)state.NextSite++;
            state.Sites=sites;state.Inventory=inventory;
            state.Health=state.Stamina=100;state.SprintExhausted=false;state.EnergyKcal=4000;
            state.WaterDeficitL=0;state.Fatigue=.1;state.CoreTemperatureC=37;
            state.Wetness=state.Bleeding=state.Illness=0;state.Dead=false;
            message="Respawned. Your equipment remains where you fell.";return true;
        }
    }
}

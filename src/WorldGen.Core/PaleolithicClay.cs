using System;

namespace WorldGen.Core
{
    public static class PaleolithicClay
    {
        public const double DrySeconds=86400, FiringSeconds=14400;
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.RawClay,Name="Clay",Form=PortableForm.Clay,Width=.16,Height=.12,Depth=.15,Mass=2,Aliases="clay|raw clay"},
            new PortableItem{Kind=PossessionKind.UnfiredClayPot,Name="Unfired clay pot",Form=PortableForm.Pot,Width=.25,Height=.22,Depth=.25,Mass=1.8,Aliases="unfired pot|green pot"},
            new PortableItem{Kind=PossessionKind.FiredClayPot,Name="Earthenware pot",Form=PortableForm.Pot,Width=.25,Height=.22,Depth=.25,Mass=1.5,Aliases="clay pot|earthenware|pot"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="shape-clay-pot",Name="Knead and coil a clay pot",Output=PossessionKind.UnfiredClayPot,Primary=new[]{PossessionKind.RawClay},Materials=Array.Empty<CraftMaterialNeed>(),Tools=Array.Empty<CraftCapability>(),WorkSeconds=1800,Yield=.85,WaterLitres=.1,MinMassKg=1.8}
        };
        public static bool Green(Possession p)=>p?.Kind==PossessionKind.UnfiredClayPot;
        // Current local weather supplies an analytic drying approximation for
        // elapsed time. There is no global per-pot simulation or day replay.
        public static void Dry(Possession p,double seconds,double rain,double humidity)
        {
            if(!Green(p)||p.Dried||seconds<=0||!double.IsFinite(seconds))return;
            double old=p.DryingSeconds;
            double rate=rain>.1?0:Math.Max(.15,1-D.Clamp(humidity)*.7);
            p.DryingSeconds=Math.Min(DrySeconds,old+seconds*rate);
            p.MassKg=p.MassKg/(1-.12*old/DrySeconds)*(1-.12*p.DryingSeconds/DrySeconds);
            if(p.DryingSeconds>=DrySeconds){p.Dried=true;p.DryingSeconds=DrySeconds;}
        }
        public static void CatchUp(Possession p,double solarDays,double rain,double humidity)
        {
            if(!Green(p)||!double.IsFinite(solarDays))return;
            if(p.ProcessClockSet)Dry(p,Math.Max(0,solarDays-p.ProcessSolarDays)*86400,rain,humidity);
            p.ProcessSolarDays=solarDays;p.ProcessClockSet=true;
        }
        public static void Fire(Possession p,double heatSeconds)
        {
            if(!Green(p)||!p.Dried||heatSeconds<=0)return;
            p.CookingSeconds=Math.Min(FiringSeconds,p.CookingSeconds+heatSeconds);
            if(p.CookingSeconds<FiringSeconds)return;
            p.Kind=PossessionKind.FiredClayPot;p.MassKg*=.95;p.Cooked=false;p.Burnt=false;
        }
        public static bool ClayGround(GroundProfile g)=>g.DepthM>=.15&&g.Clay>=.27&&g.Gravel<.55;
    }
}

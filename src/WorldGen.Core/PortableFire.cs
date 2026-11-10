using System;

namespace WorldGen.Core
{
    public static class PortableFire
    {
        public const double TorchSeconds=5400,EmberSeconds=28800;
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.Torch,Name="Wrapped bark torch",Form=PortableForm.Torch,Width=.075,Height=.75,Depth=.075,Mass=.7,Aliases="torch|bark torch"},
            new PortableItem{Kind=PossessionKind.EmberCarrier,Name="Bark ember carrier",Form=PortableForm.BarkBox,Width=.18,Height=.17,Depth=.16,Mass=.3,Aliases="ember carrier|fire carrier"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="wrap-torch",Name="Wrap and fuel a bark torch",Output=PossessionKind.Torch,Primary=new[]{PossessionKind.TrimmedPole},Materials=new[]{new CraftMaterialNeed(2,PossessionKind.BarkStrips),new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.SinewCord),new CraftMaterialNeed(1,PossessionKind.PlantResin,PossessionKind.AnimalFat)},Tools=new[]{CraftCapability.Cut},WorkSeconds=600,MinLength=.75,MinWidthM=.018,MaxThickness=.065,Yield=.9},
            new PortableRecipe{Id="ember-carrier",Name="Fit an insulated bark ember carrier",Output=PossessionKind.EmberCarrier,Primary=new[]{PossessionKind.BarkContainer},Materials=new[]{new CraftMaterialNeed(1,PossessionKind.BarkSheet),new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.RawhideStrips)},Tools=new[]{CraftCapability.Pierce},WorkSeconds=600,Yield=.85},
            new PortableRecipe{Id="refuel-torch",Name="Rewrap and refuel the torch",Output=PossessionKind.Torch,Primary=new[]{PossessionKind.Torch},Materials=new[]{new CraftMaterialNeed(2,PossessionKind.BarkStrips),new CraftMaterialNeed(1,PossessionKind.PlantResin,PossessionKind.AnimalFat)},Tools=Array.Empty<CraftCapability>(),WorkSeconds=240,Maintenance=true,RefuelFire=true}
        };
        public static bool Item(Possession p)=>p!=null&&(p.Kind==PossessionKind.Torch||p.Kind==PossessionKind.EmberCarrier);
        public static bool Burning(Possession p)=>Item(p)&&p.FireLit&&p.FireRemainingSeconds>0&&p.FireFuelKg>0;
        public static void Initialise(Possession p)
        {
            if(!Item(p))return;p.FireLit=false;p.ProcessClockSet=false;
            p.FireRemainingSeconds=p.Kind==PossessionKind.Torch?TorchSeconds:0;p.FireFuelKg=p.Kind==PossessionKind.Torch?p.MassKg*.4:0;
        }
        public static void Refuel(Possession p,double suppliedMass)
        {
            // Burnt fuel is replaced with real new wrapping/fuel, while a torch
            // still containing fuel cannot be repeatedly filled for free.
            p.FireLit=false;p.MassKg+=Math.Max(0,suppliedMass)*.8;p.FireFuelKg+=Math.Max(0,suppliedMass)*.8;
            p.FireRemainingSeconds=TorchSeconds;p.ProcessClockSet=false;
        }
        public static void Advance(Possession p,double solarDays,double rain,double wind,bool sheltered)
        {
            if(!Item(p)||!double.IsFinite(solarDays))return;
            double dt=p.ProcessClockSet?Math.Max(0,solarDays-p.ProcessSolarDays)*86400:0;p.ProcessSolarDays=solarDays;p.ProcessClockSet=true;
            if(!Burning(p))return;
            if(!sheltered&&rain>(p.Kind==PossessionKind.Torch?2:20)){p.FireLit=false;return;}
            double dose=dt*(p.Kind==PossessionKind.Torch?1+Math.Max(0,wind)*.035:1);
            double spent=Math.Min(dose,p.FireRemainingSeconds),fuel=p.FireFuelKg*spent/p.FireRemainingSeconds;
            p.MassKg=Math.Max(.001,p.MassKg-fuel);p.FireFuelKg=Math.Max(0,p.FireFuelKg-fuel);p.FireRemainingSeconds=Math.Max(0,p.FireRemainingSeconds-spent);
            if(p.FireRemainingSeconds<=0)p.FireLit=false;
        }
        public static bool Light(Possession p,bool source,double solarDays)
        {
            if(!source||!Item(p)||p.FireRemainingSeconds<=0||p.FireFuelKg<=0||p.FireLit)return false;
            p.FireLit=true;p.ProcessClockSet=true;p.ProcessSolarDays=solarDays;return true;
        }
        public static bool LoadEmbers(Possession carrier,CampObject fire,Possession tinder,double solarDays)
        {
            if(carrier?.Kind!=PossessionKind.EmberCarrier||Burning(carrier)||fire==null||fire.Kind!=CampKind.Fire||!(fire.Burning||fire.EmberSeconds>=300))return false;
            if(carrier.FireFuelKg>0&&carrier.FireRemainingSeconds>0)return Light(carrier,true,solarDays);
            if(fire.AshKg<.005||!CampRules.Tinder(tinder)||tinder.MassKg<.02)return false;
            fire.AshKg-=.005;tinder.MassKg-=.02;carrier.MassKg+=.025;carrier.FireFuelKg=.025;carrier.FireRemainingSeconds=EmberSeconds;
            return Light(carrier,true,solarDays);
        }
        public static bool Ignite(CampObject fire,Possession source)
        {
            if(!Burning(source)||CampRules.IgniteReason(fire,true).Length>0||source.Kind==PossessionKind.EmberCarrier&&source.FireFuelKg<.001)return false;
            if(source.Kind==PossessionKind.EmberCarrier){double before=source.FireFuelKg;source.FireFuelKg-=.001;source.MassKg-=.001;source.FireRemainingSeconds*=source.FireFuelKg/before;fire.AshKg+=.001;}
            CampRules.Ignite(fire,true);return true;
        }
        public static void Quench(Possession p)
        {
            if(!Item(p))return;p.FireLit=false;
            if(p.Kind==PossessionKind.EmberCarrier){p.MassKg=Math.Max(.001,p.MassKg-p.FireFuelKg);p.FireFuelKg=0;p.FireRemainingSeconds=0;}
        }
        public static double LightPower(Possession p)=>!Burning(p)?0:p.Kind==PossessionKind.Torch?.7:.025;
        public static void Validate(Possession p)
        {
            if(!double.IsFinite(p.FireRemainingSeconds)||p.FireRemainingSeconds<0||p.FireRemainingSeconds>EmberSeconds||!double.IsFinite(p.FireFuelKg)||p.FireFuelKg<0||p.FireFuelKg>p.MassKg||p.FireLit&&!Item(p))throw new ArgumentException("Invalid portable fire or fuel.");
        }
    }
}

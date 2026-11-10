using System;

namespace WorldGen.Core
{
    public static class PaleolithicWater
    {
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.Waterskin,Name="Hide waterskin",Form=PortableForm.Pouch,Width=.22,Height=.30,Depth=.15,Mass=.22,Wear=PossessionPlace.Shoulder,Aliases="waterskin|water skin|water flask"},
            new PortableItem{Kind=PossessionKind.BarkWaterVessel,Name="Laced bark water vessel",Form=PortableForm.BarkBox,Width=.22,Height=.24,Depth=.19,Mass=.3,Aliases="bark water vessel|bark vessel|water vessel"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="waterskin",Name="Cut and lace a waterskin",Output=PossessionKind.Waterskin,Primary=new[]{PossessionKind.WorkedHide},WorkSeconds=1800,Yield=.6,
                Materials=new[]{new CraftMaterialNeed(1,PossessionKind.Thread,PossessionKind.SinewCord),new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.RawhideStrips,PossessionKind.SinewCord)},Tools=new[]{CraftCapability.Cut,CraftCapability.Pierce}},
            new PortableRecipe{Id="bark-water-vessel",Name="Fold and lace a bark water vessel",Output=PossessionKind.BarkWaterVessel,Primary=new[]{PossessionKind.BarkSheet},WorkSeconds=900,Yield=.9,
                Materials=new[]{new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.RawhideStrips,PossessionKind.SinewCord)},Tools=new[]{CraftCapability.Cut,CraftCapability.Pierce}}
        };
        public static double Capacity(Possession p)=>p?.Kind==PossessionKind.FiredClayPot?3:p?.Kind==PossessionKind.Waterskin?2:p?.Kind==PossessionKind.BarkWaterVessel?1.5:0;
        public static bool Vessel(Possession p)=>Capacity(p)>0;
        public static double Space(Possession p)=>Math.Max(0,Capacity(p)-(p?.WaterLitres??0));
        public static WaterQuality Quality(Possession p)=>p!=null&&p.WaterLitres>0?new WaterQuality(p.StoredWaterKind,p.WaterSalinityPpt,p.WaterContamination,p.WaterTurbidity):default;
        public static double Fill(Possession p,WaterQuality quality,double litres,bool boiled=false)
        {
            if(p==null||!Vessel(p)||quality.Kind==LocalWaterKind.None||!double.IsFinite(litres)||litres<=0)return 0;
            if(!double.IsFinite(quality.SalinityPpt)||!double.IsFinite(quality.Contamination)||!double.IsFinite(quality.Turbidity))throw new ArgumentException("Invalid source water.");
            double added=Math.Min(Space(p),litres);if(added<=0)return 0;double before=p.WaterLitres,total=before+added;
            p.WaterSalinityPpt=(p.WaterSalinityPpt*before+quality.SalinityPpt*added)/total;
            p.WaterContamination=(p.WaterContamination*before+quality.Contamination*added)/total;
            p.WaterTurbidity=(p.WaterTurbidity*before+quality.Turbidity*added)/total;
            p.StoredWaterKind=before>0?p.StoredWaterKind:quality.Kind;p.WaterBoiled=boiled&&(before<=0||p.WaterBoiled);
            p.WaterLitres=total;p.MassKg+=added;p.WaterHeatSeconds=0;p.CookingSeconds=0;return added;
        }
        public static double Drain(Possession p,double litres)
        {
            if(p==null||!Vessel(p)||!double.IsFinite(litres)||litres<=0)return 0;
            double taken=Math.Min(p.WaterLitres,litres);p.WaterLitres-=taken;p.MassKg-=taken;
            if(p.WaterLitres<1e-9){p.WaterLitres=p.WaterSalinityPpt=p.WaterContamination=p.WaterTurbidity=p.WaterHeatSeconds=0;p.StoredWaterKind=LocalWaterKind.None;p.WaterBoiled=false;}
            return taken;
        }
        public static double Transfer(Possession from,Possession to,double litres)
        {
            if(from==null||to==null||ReferenceEquals(from,to)||!double.IsFinite(litres)||litres<=0)return 0;
            double moved=Fill(to,Quality(from),Math.Min(from.WaterLitres,litres),from.WaterBoiled);Drain(from,moved);return moved;
        }
        public static double FillCarried(PlayerInventory inventory,WaterQuality source)
        {
            foreach(var p in inventory.Items)
                if(Vessel(p)&&inventory.Carried(p)&&Space(p)>.001)return Fill(p,source,Space(p));
            return 0;
        }
        public static double Drink(Possession p,PlayerSurvival state,GameMode mode,double litres,double susceptibility)
        {
            if(state==null||state.Incapacitated||!Vessel(p)||!double.IsFinite(litres)||litres<=0)return 0;
            double taken=Math.Min(p.WaterLitres,litres);if(taken<=0)return 0;
            WaterDrinking.Drink(state,Quality(p),taken,susceptibility,mode);Drain(p,taken);return taken;
        }
        // Called by actual heat sources, never a context-menu purification cheat.
        // Salt remains after heating; subsequent untreated top-ups recontaminate.
        public static void Heat(Possession p,double temperatureC,double seconds)
        {
            if(!Vessel(p)||p.WaterLitres<=0||!double.IsFinite(temperatureC)||!double.IsFinite(seconds)||seconds<=0)return;
            if(temperatureC<95){p.WaterHeatSeconds=0;return;}
            p.WaterHeatSeconds=Math.Min(86400,p.WaterHeatSeconds+seconds);
            if(p.WaterHeatSeconds>=60&&!p.WaterBoiled){p.WaterContamination*=.001;p.WaterBoiled=true;}
        }
        public static void Validate(Possession p)
        {
            bool Range(double v,double max)=>double.IsFinite(v)&&v>=0&&v<=max;
            if(!Range(p.WaterLitres,Capacity(p)+1e-8)||!Range(p.WaterSalinityPpt,1000)||!Range(p.WaterContamination,1)||!Range(p.WaterTurbidity,1)||!Range(p.WaterHeatSeconds,86400)||!Enum.IsDefined(typeof(LocalWaterKind),p.StoredWaterKind)||p.WaterLitres>0&&(p.StoredWaterKind==LocalWaterKind.None||p.MassKg<=p.WaterLitres))throw new ArgumentException("Invalid stored water.");
        }
    }
}

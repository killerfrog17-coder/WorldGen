using System;
using System.Linq;
namespace WorldGen.Core
{
    // A physical weight-and-trigger trap. No random catch timer, animal factory,
    // population roll or offscreen meat award exists here.
    public static class Deadfalls
    {
        public static readonly PortableItem[] Items={new PortableItem{Kind=PossessionKind.DeadfallTrigger,Name="Carved deadfall trigger",Form=PortableForm.Trigger,Width=.3,Height=.32,Depth=.05,Mass=.16,Aliases="deadfall trigger|trap trigger"}};
        public static readonly PortableRecipe[] Recipes={new PortableRecipe{Id="carve-deadfall-trigger",Name="Carve a deadfall trigger",Output=PossessionKind.DeadfallTrigger,Primary=new[]{PossessionKind.Branch,PossessionKind.TrimmedPole},Materials=Array.Empty<CraftMaterialNeed>(),Tools=new[]{CraftCapability.Cut},MinLength=.9,MinWidthM=.015,MaxThickness=.045,SoundWood=true,WorkSeconds=600,Yield=.65}};
        public static bool Weight(Possession p)=>StoneCrafting.Rock(p)&&p.MassKg>=2&&p.MassKg<=12&&p.WidthM>=.15&&p.HeightM<=p.WidthM*1.1;
        public static Possession Stone(CampObject c)=>c?.Materials.FirstOrDefault(Weight);
        public static Possession Bait(CampObject c)=>c?.Materials.FirstOrDefault(Foraging.Food);
        public static bool Accepts(CampObject c,Possession p)=>c?.Kind==CampKind.Deadfall&&!c.TrapArmed&&p!=null&&(p.Kind==PossessionKind.DeadfallTrigger&&!c.Materials.Any(x=>x.Kind==p.Kind)||Weight(p)&&Stone(c)==null||Foraging.Food(p)&&Bait(c)==null);
        public static string SetReason(CampObject c)=>c?.Kind!=CampKind.Deadfall?"Choose a deadfall.":c.TrapArmed?"The deadfall is already set.":!c.Materials.Any(p=>p.Kind==PossessionKind.DeadfallTrigger)?"Add a carved wooden trigger.":Stone(c)==null?"Add a broad stone weighing 2 to 12 kg.":Bait(c)==null?"Add a little food as bait.":"";
        public static bool Arm(CampObject c){if(SetReason(c).Length>0)return false;c.TrapArmed=true;c.TrapSprung=false;return true;}
        public static double DropHeight(CampObject c)=>Math.Min(.4,Math.Max(.18,(Stone(c)?.WidthM??0)*.65));
        public static double Damage(CampObject c)=>Stone(c) is Possession rock?ItemUseRules.ImpactDamage(rock.MassKg,Math.Sqrt(2*9.81*DropHeight(c)),0)*1.15:0;
        public static double Radius(CampObject c)=>Math.Min(.3,Math.Max(.1,(Stone(c)?.WidthM??0)*.36));
        public static bool Eligible(double mass,string medium)=>medium=="ground"&&mass>=.03&&mass<=8;
        public static bool Crosses(double ax,double az,double bx,double bz,double radius)
        {double dx=bx-ax,dz=bz-az,den=dx*dx+dz*dz,t=den>0?Math.Max(0,Math.Min(1,-(ax*dx+az*dz)/den)):0;return Math.Pow(ax+dx*t,2)+Math.Pow(az+dz*t,2)<=radius*radius;}
        public static double Spring(CampObject c)
        {if(!c.TrapArmed)return 0;double damage=Damage(c);c.TrapArmed=false;c.TrapSprung=true;var bait=Bait(c);if(bait!=null)c.Materials.Remove(bait);return damage;}
        public static void Validate(CampObject c)
        {if(c.Kind!=CampKind.Deadfall)return;if(c.Materials.Count(p=>p.Kind==PossessionKind.DeadfallTrigger)>1||c.Materials.Count(Weight)>1||c.Materials.Count(Foraging.Food)>1||c.Materials.Any(p=>p.Kind!=PossessionKind.DeadfallTrigger&&!Weight(p)&&!Foraging.Food(p))||c.TrapArmed&&(c.TrapSprung||!c.Materials.Any(p=>p.Kind==PossessionKind.DeadfallTrigger)||Stone(c)==null||Bait(c)==null))throw new ArgumentException("Invalid physical deadfall materials or mechanism.");}
    }
}

using System;

namespace WorldGen.Core
{
    public enum ItemPrimaryUse { None,Blunt,Edge,Thrust,Bow,Sling,SpearThrower }
    public enum ItemSecondaryUse { None,Block,Throw,Dig,Fish,Net }
    public readonly struct ItemUseProfile
    {
        public readonly ItemPrimaryUse Primary;public readonly ItemSecondaryUse Secondary;
        public readonly double ReachM,EnergyJ,Penetration,RecoverySeconds,Stamina,BlockFraction;
        public ItemUseProfile(ItemPrimaryUse primary,ItemSecondaryUse secondary,double reach,double energy,double penetration,double recovery,double stamina,double block=.4)
        {Primary=primary;Secondary=secondary;ReachM=reach;EnergyJ=energy;Penetration=penetration;RecoverySeconds=recovery;Stamina=stamina;BlockFraction=block;}
        public bool Weapon=>Primary!=ItemPrimaryUse.None;
    }
    /// <summary>Gameplay health abstractions in physical units, not injury or
    /// lethality predictions. Actual projectiles use their current mass/velocity.</summary>
    public static class ItemUseRules
    {
        static ItemUseProfile P(ItemPrimaryUse p,ItemSecondaryUse s,double r,double e,double pierce,double recovery=.7,double stamina=4,double block=.4)=>new ItemUseProfile(p,s,r,e,pierce,recovery,stamina,block);
        public static ItemUseProfile Profile(Possession item)
        {
            if(item==null)return P(ItemPrimaryUse.Blunt,ItemSecondaryUse.Block,.68,8,0,.5,2,.18);
            switch(item.Kind)
            {
                case PossessionKind.SmallRock:return P(ItemPrimaryUse.Blunt,ItemSecondaryUse.Throw,.72,18,0,.62,3,.3);
                case PossessionKind.LargeRock:return P(ItemPrimaryUse.Blunt,ItemSecondaryUse.Block,.78,55,0,1.2,8,.6);
                case PossessionKind.StoneFlake:case PossessionKind.StoneBlade:case PossessionKind.StoneScraper:return P(ItemPrimaryUse.Edge,ItemSecondaryUse.Block,.72,13,.72,.5,2.5,.22);
                case PossessionKind.HaftedKnife:return P(ItemPrimaryUse.Edge,ItemSecondaryUse.Block,.85,20,.85,.52,3,.35);
                case PossessionKind.StoneChopper:case PossessionKind.Handaxe:return P(ItemPrimaryUse.Edge,ItemSecondaryUse.Block,.82,40,.45,.8,5,.48);
                case PossessionKind.StoneAxe:case PossessionKind.StoneAdze:return P(ItemPrimaryUse.Edge,ItemSecondaryUse.Block,1.1,65,.55,.95,6,.55);
                case PossessionKind.WoodenSpear:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Throw,Math.Max(1.1,item.HeightM*.78),55,.6,.85,5,.55);
                case PossessionKind.StoneTippedSpear:case PossessionKind.BoneTippedSpear:case PossessionKind.Harpoon:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Throw,Math.Max(1.1,item.HeightM*.78),65,.95,.85,5,.55);
                case PossessionKind.LooseStoneSpear:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Throw,Math.Max(1.1,item.HeightM*.78),35,.5,1,5,.25);
                case PossessionKind.Branch:case PossessionKind.TrimmedPole:case PossessionKind.NotchedShaft:case PossessionKind.DartShaft:return P(ItemPrimaryUse.Blunt,ItemSecondaryUse.Block,Math.Max(.8,Math.Min(1.7,item.HeightM*.75)),25,0,.85,4,.45);
                case PossessionKind.DiggingStick:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Dig,Math.Max(.8,item.HeightM*.8),25,.25,.8,4,.4);
                case PossessionKind.Bone:case PossessionKind.Antler:case PossessionKind.AntlerRetoucher:case PossessionKind.WoodHandle:case PossessionKind.BowStave:return P(ItemPrimaryUse.Blunt,ItemSecondaryUse.Block,.75,16,0,.7,3,.3);
                case PossessionKind.StonePoint:case PossessionKind.BonePoint:case PossessionKind.BarbedPoint:case PossessionKind.StoneBorer:case PossessionKind.BoneAwl:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Block,.72,10,.6,.6,2,.16);
                case PossessionKind.Arrow:case PossessionKind.SpearDart:return P(ItemPrimaryUse.Thrust,ItemSecondaryUse.Throw,Math.Max(.8,item.HeightM*.8),14,.7,.75,3,.1);
                case PossessionKind.Bow:return P(ItemPrimaryUse.Bow,ItemSecondaryUse.Block,1,0,0,.55,4,.2);
                case PossessionKind.Sling:return P(ItemPrimaryUse.Sling,ItemSecondaryUse.None,1,0,0,.8,4,0);
                case PossessionKind.SpearThrower:return P(ItemPrimaryUse.SpearThrower,ItemSecondaryUse.Block,.8,12,0,.8,5,.2);
                case PossessionKind.HandLine:return P(ItemPrimaryUse.None,ItemSecondaryUse.Fish,0,0,0,1,1,0);
                case PossessionKind.FibreNet:return P(ItemPrimaryUse.None,ItemSecondaryUse.Net,0,0,0,1,2,0);
                default:return default;
            }
        }
        public static double HealthForMass(double massKg)=>Math.Max(1,100*Math.Pow(Math.Max(.001,massKg)/70,.55));
        public static double KineticEnergy(double massKg,double speedMps)=>Finite(massKg)&&Finite(speedMps)&&massKg>0?.5*massKg*speedMps*speedMps:0;
        public static double ImpactDamage(double massKg,double speedMps,double penetration,double condition=1)
        {
            double energy=KineticEnergy(massKg,speedMps);if(energy<=.05)return 0;
            return 9*Math.Pow(energy/15,.7)*(1+Clamp(penetration))*(.3+.7*Clamp(condition));
        }
        public static double MeleeDamage(Possession item,double effort=1)
        {
            var profile=Profile(item);if(!profile.Weapon||profile.EnergyJ<=0)return 0;
            double materialMass=item==null?.3:Math.Max(.02,item.MassKg),energy=profile.EnergyJ*Math.Max(.15,Math.Min(1.8,Math.Sqrt(materialMass/.5)))*Clamp(effort);
            double sharp=item!=null&&(profile.Primary==ItemPrimaryUse.Edge)?Math.Max(.15,item.EdgeSharpness):1;
            return 9*Math.Pow(energy/15,.7)*(1+profile.Penetration*sharp)*Condition(item);
        }
        public static double ThrowSpeed(Possession item,double charge,double staminaFraction=1,bool sling=false,bool thrower=false)
        {
            if(item==null||item.MassKg<=0)return 0;bool spear=Profile(item).Primary==ItemPrimaryUse.Thrust;
            double maxSpeed=sling?42:thrower?32:spear?24:28,energy=sling?150:thrower?240:spear?180:90,impulse=sling?14:thrower?30:spear?24:11;
            double effort=(.2+.8*Clamp(charge))*(.35+.65*Clamp(staminaFraction));
            return Math.Min(maxSpeed,Math.Min(Math.Sqrt(2*energy/item.MassKg),impulse/item.MassKg))*Math.Sqrt(effort);
        }
        public static double BowSpeed(Possession arrow,double draw,double condition=1)
            =>arrow==null||arrow.MassKg<=0?0:Math.Min(65,Math.Sqrt(2*55*Clamp(draw)*Clamp(draw)*(.4+.6*Clamp(condition))/arrow.MassKg));
        public static double DragAcceleration(double massKg,double areaM2,double speedMps,double coefficient=.47)
            =>massKg>0?.5*1.225*Math.Max(0,coefficient)*Math.Max(0,areaM2)*speedMps*speedMps/massKg:0;
        public static double Condition(Possession item)=>item==null?1:Math.Max(0,1-item.UseDamage);
        public static double BlockDamage(double incoming,Possession guard,double facingCos,ref double stamina)
        {
            if(incoming<=0||facingCos<.35||stamina<=0)return Math.Max(0,incoming);
            double fraction=Profile(guard).BlockFraction*Condition(guard),wanted=incoming*fraction,cost=wanted*.55;
            double spent=Math.Min(Math.Max(0,stamina),cost);stamina=Math.Max(0,stamina-spent);
            return Math.Max(0,incoming-(cost>0?wanted*spent/cost:0));
        }
        public static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        public static double Clamp(double n)=>Finite(n)?Math.Max(0,Math.Min(1,n)):0;
    }
}

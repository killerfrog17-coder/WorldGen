using System;

namespace WorldGen.Core
{
    public static class PaleolithicBinders
    {
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.PlantGum,Name="Plant gum",Form=PortableForm.Clay,Width=.06,Height=.04,Depth=.05,Mass=.06,Aliases="gum|plant gum"},
            new PortableItem{Kind=PossessionKind.PlantResin,Name="Plant resin",Form=PortableForm.Clay,Width=.06,Height=.04,Depth=.05,Mass=.06,Aliases="resin|plant resin"},
            new PortableItem{Kind=PossessionKind.RawOchre,Name="Ochre-rich earth",Form=PortableForm.Clay,Width=.045,Height=.04,Depth=.04,Mass=.08,Aliases="ochre|raw ochre"},
            new PortableItem{Kind=PossessionKind.OchrePowder,Name="Ground ochre",Form=PortableForm.Clay,Width=.065,Height=.025,Depth=.05,Mass=.04,Aliases="ochre powder|pigment"},
            new PortableItem{Kind=PossessionKind.Charcoal,Name="Charcoal scraps",Form=PortableForm.Clay,Width=.055,Height=.035,Depth=.04,Mass=.03,Aliases="charcoal"},
            new PortableItem{Kind=PossessionKind.AdhesiveMixture,Name="Unheated adhesive mixture",Form=PortableForm.Clay,Width=.09,Height=.035,Depth=.07,Mass=.1,Aliases="adhesive mixture|glue mixture"},
            new PortableItem{Kind=PossessionKind.CompoundAdhesive,Name="Compound hafting adhesive",Form=PortableForm.Clay,Width=.08,Height=.03,Depth=.06,Mass=.08,Aliases="adhesive|compound adhesive|pitch|glue"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="grind-ochre",Name="Grind ochre into pigment",Output=PossessionKind.OchrePowder,Primary=new[]{PossessionKind.RawOchre},Materials=Array.Empty<CraftMaterialNeed>(),Tools=new[]{CraftCapability.GrindingSurface,CraftCapability.Strike},DistinctTools=true,WorkSeconds=300,Yield=.8,PreserveTint=true},
            new PortableRecipe{Id="gum-ochre-glue",Name="Mix plant gum and ground ochre",Output=PossessionKind.AdhesiveMixture,Primary=new[]{PossessionKind.PlantGum},Materials=new[]{new CraftMaterialNeed(1,PossessionKind.OchrePowder)},Tools=new[]{CraftCapability.Grind},WaterLitres=.02,WorkSeconds=300,Yield=.9},
            new PortableRecipe{Id="resin-charcoal-glue",Name="Crush charcoal into resin",Output=PossessionKind.AdhesiveMixture,Primary=new[]{PossessionKind.PlantResin},Materials=new[]{new CraftMaterialNeed(1,PossessionKind.Charcoal)},Tools=new[]{CraftCapability.Grind},WorkSeconds=300,Yield=.95}
        };
        public static V Colour(PossessionKind kind)=>kind==PossessionKind.RawOchre||kind==PossessionKind.OchrePowder?new V(.68,.24,.09):kind==PossessionKind.Charcoal?new V(.075,.065,.05):kind==PossessionKind.PlantGum?new V(.56,.3,.12):kind==PossessionKind.PlantResin?new V(.68,.39,.12):new V(.26,.14,.07);
        public static bool Material(Possession p)=>p!=null&&p.Kind>=PossessionKind.PlantGum&&p.Kind<=PossessionKind.CompoundAdhesive;
        public static bool OchreGround(GroundProfile ground)=>ground.DepthM>=.08&&ground.Iron>=.45&&ground.Clay>=.12&&ground.Gravel<.55;
        public static PossessionKind? Exudate(TreeSpecies s)
        {
            if(s.Form==TreeGrowthForm.NeedleCrown)return PossessionKind.PlantResin;
            if(s.Form==TreeGrowthForm.BroadCrown&&((s.MorphologyCode>>8)&16383)%4==0)return PossessionKind.PlantGum;
            return null;
        }
        public static void Heat(Possession p,double dose)
        {
            if(p?.Kind!=PossessionKind.AdhesiveMixture||dose<=0)return;
            p.CookingSeconds=Math.Min(900,p.CookingSeconds+dose);
            if(p.CookingSeconds<900)return;
            p.Kind=PossessionKind.CompoundAdhesive;p.MassKg*=p.AdhesiveGumBased?.8:.95;p.Tint=Colour(p.Kind);
        }
        public static bool Haft(Possession p)=>p!=null&&(p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.BoneTippedSpear||p.Kind==PossessionKind.Harpoon||p.Kind==PossessionKind.HaftedKnife||p.Kind==PossessionKind.StoneAxe||p.Kind==PossessionKind.StoneAdze||p.Kind==PossessionKind.Arrow||p.Kind==PossessionKind.UnfletchedArrow||p.Kind==PossessionKind.SpearDart);
        public static bool Bond(Possession target,Possession glue,bool heated)
        {
            if(!heated||!Haft(target)||target.HaftBonded||glue?.Kind!=PossessionKind.CompoundAdhesive||glue.MassKg+.0000001<.005)return false;
            double left=Math.Max(0,glue.MassKg-.005);if(left>1e-8)Foraging.Resize(glue,left);else glue.MassKg=0;target.MassKg+=.005;target.HaftBonded=true;return true;
        }
        public static bool Paintable(Possession p)=>p!=null&&(WoodCrafting.Wood(p)||StoneCrafting.Rock(p)||p.Kind==PossessionKind.StoneFlake||PortableTechnology.Item(p.Kind)?.Form==PortableForm.Pot||PortableTechnology.Item(p.Kind)?.Form==PortableForm.Bone||PortableTechnology.Item(p.Kind)?.Form==PortableForm.Garment||CampRules.Hide(p)||Haft(p));
        public static bool Paint(Possession target,Possession pigment,Possession water)
        {
            if(!Paintable(target)||target.PigmentMarked||pigment?.Kind!=PossessionKind.OchrePowder||pigment.MassKg+.0000001<.002||!PaleolithicWater.Vessel(water)||water.WaterLitres+.0000001<.005)return false;
            target.PigmentColour=pigment.Tint;target.PigmentMarked=true;target.MassKg+=.002;PaleolithicWater.Drain(water,.005);double left=Math.Max(0,pigment.MassKg-.002);if(left>1e-8)Foraging.Resize(pigment,left);else pigment.MassKg=0;return true;
        }
        public static Possession CollectCharcoal(CampObject fire,V at)
        {
            if(fire==null||fire.Kind!=CampKind.Fire||fire.Burning||fire.AshKg<.03)return null;
            fire.AshKg-=.03;return PortableTechnology.Create(PossessionKind.Charcoal,at);
        }
    }
}

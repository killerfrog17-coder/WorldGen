using System;
using System.Linq;

namespace WorldGen.Core
{
    // Garments are actual owned possessions. Packing one does not protect the
    // wearer, and wearing a cloak does not consume their pack attachment.
    public static class PaleolithicClothing
    {
        static PortableItem Garment(PossessionKind kind,string name,PossessionPlace place,double kg,double w,double h,double d,string aliases="")
            =>new PortableItem{Kind=kind,Name=name,Form=PortableForm.Garment,Mass=kg,Width=w,Height=h,Depth=d,Wear=place,Aliases=aliases};
        public static readonly PortableItem[] Items={
            Garment(PossessionKind.HideWrap,"Hide wrap",PossessionPlace.Torso,1.1,.3,.35,.12,"cloak|hide cloak|wrap"),
            Garment(PossessionKind.HideTunic,"Sewn hide tunic",PossessionPlace.Torso,1.6,.3,.4,.14,"tunic|hide tunic"),
            Garment(PossessionKind.FurCoat,"Layered fur coat",PossessionPlace.Torso,3.2,.4,.5,.2,"fur coat|coat|insulated clothing"),
            Garment(PossessionKind.HideLeggings,"Hide leggings",PossessionPlace.Legs,1.1,.25,.35,.1,"leggings"),
            Garment(PossessionKind.HideFootwear,"Hide footwear",PossessionPlace.Feet,.55,.2,.25,.1,"shoes|boots|footwear|moccasins"),
            Garment(PossessionKind.HideHood,"Hide hood",PossessionPlace.Head,.45,.2,.2,.1,"hood")
        };
        static PortableRecipe Sew(string id,string name,PossessionKind output,double seconds,int hides,int bindings,bool needle=false)
            =>new PortableRecipe{Id=id,Name=name,Output=output,Primary=new[]{PossessionKind.WorkedHide},WorkSeconds=seconds,Yield=.9,
                Materials=(hides>0?new[]{new CraftMaterialNeed(hides,PossessionKind.WorkedHide)}:Array.Empty<CraftMaterialNeed>()).Concat(new[]{new CraftMaterialNeed(bindings,PossessionKind.Thread,PossessionKind.SinewCord)}).ToArray(),
                Tools=new[]{CraftCapability.Cut,needle?CraftCapability.Sew:CraftCapability.Pierce}};
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="hide-wrap",Name="Cut and tie a hide wrap",Output=PossessionKind.HideWrap,Primary=new[]{PossessionKind.WorkedHide},WorkSeconds=480,Yield=.95,
                Materials=new[]{new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.RawhideStrips,PossessionKind.SinewCord)},Tools=new[]{CraftCapability.Cut}},
            Sew("hide-tunic","Cut and lace a fitted hide tunic",PossessionKind.HideTunic,3600,1,2),
            Sew("fur-coat","Sew a layered fur coat",PossessionKind.FurCoat,7200,3,3,true),
            Sew("hide-leggings","Cut and lace hide leggings",PossessionKind.HideLeggings,2400,1,2),
            Sew("hide-footwear","Cut and lace hide footwear",PossessionKind.HideFootwear,1800,0,1),
            Sew("hide-hood","Cut and lace a hide hood",PossessionKind.HideHood,1800,0,1)
        };
        public static bool Place(PossessionPlace place)=>place==PossessionPlace.Torso||place==PossessionPlace.Legs||place==PossessionPlace.Feet||place==PossessionPlace.Head;
        public static bool Garment(Possession p)=>p!=null&&p.Kind>=PossessionKind.HideWrap&&p.Kind<=PossessionKind.HideHood;
        // Whole-body effective protection used by the existing thermal model,
        // not literal clo values. The complete outfit remains below immunity.
        public static double Insulation(PlayerInventory inventory)
        {
            double protection=0;
            foreach(var p in inventory.Items)
            {
                if(!Garment(p)||PortableTechnology.Wear(p)!=p.Place)continue;
                double part=p.Kind==PossessionKind.HideWrap?.28:p.Kind==PossessionKind.HideTunic?.4:p.Kind==PossessionKind.FurCoat?.57:p.Kind==PossessionKind.HideLeggings?.16:p.Kind==PossessionKind.HideFootwear?.07:.10;
                protection+=part*(1-.5*D.Clamp(p.UseDamage));
            }
            return Math.Min(.9,protection);
        }
    }
}

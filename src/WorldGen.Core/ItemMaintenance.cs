using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Small material loss when renewing an edge; lashings/handles are
    /// real replacement ingredients, not a generic repair resource.</summary>
    public static class ItemMaintenance
    {
        public static IEnumerable<PortableRecipe> Recipes()
        {
            foreach(var kind in new[]{PossessionKind.StoneFlake,PossessionKind.StoneBlade,PossessionKind.StoneScraper,PossessionKind.StoneChopper,PossessionKind.Handaxe,PossessionKind.StonePoint,PossessionKind.Arrowhead,PossessionKind.StoneBorer})
                yield return Make(kind,"Retouch the stone edge",true,120,new[]{CraftCapability.Strike});
            foreach(var kind in new[]{PossessionKind.HaftedKnife,PossessionKind.StoneAxe,PossessionKind.StoneAdze})
                yield return Make(kind,"Renew the edge and replace the haft",true,420,new[]{CraftCapability.Grind},new CraftMaterialNeed(1,PossessionKind.WoodHandle),Binding());
            foreach(var kind in new[]{PossessionKind.WoodenSpear,PossessionKind.DiggingStick,PossessionKind.BonePoint,PossessionKind.BarbedPoint,PossessionKind.BoneAwl})
                yield return Make(kind,"Reshape the worn point",false,180,new[]{CraftCapability.Cut});
            foreach(var kind in new[]{PossessionKind.StoneTippedSpear,PossessionKind.BoneTippedSpear,PossessionKind.Harpoon,PossessionKind.Arrow,PossessionKind.UnfletchedArrow,PossessionKind.SpearDart})
                yield return Make(kind,"Tighten and renew the binding",false,150,Array.Empty<CraftCapability>(),Binding());
            foreach(var kind in new[]{PossessionKind.Bow,PossessionKind.Sling,PossessionKind.HandLine,PossessionKind.FibreNet})
                yield return Make(kind,"Replace the worn cordage",false,240,Array.Empty<CraftCapability>(),Binding());
        }
        static CraftMaterialNeed Binding()=>new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.SinewCord,PossessionKind.RawhideStrips);
        static PortableRecipe Make(PossessionKind kind,string title,bool edge,double seconds,CraftCapability[] tools,params CraftMaterialNeed[] materials)
            =>new PortableRecipe{Id="maintain-"+kind,Name=title,Primary=new[]{kind},Output=kind,Maintenance=true,RestoreEdge=edge,WorkSeconds=seconds,Tools=tools,Materials=materials};
        public static void Complete(Possession item,bool edge)
        {
            item.UseDamage=Math.Max(0,item.UseDamage-(edge?.85:.65));
            if(edge){item.EdgeSharpness=.9;item.MassKg*=.985;}
            else if(item.Kind==PossessionKind.WoodenSpear||item.Kind==PossessionKind.DiggingStick||item.Kind==PossessionKind.BonePoint||item.Kind==PossessionKind.BarbedPoint||item.Kind==PossessionKind.BoneAwl)
            {item.HeightM*=.99;item.MassKg*=.99;item.CellsHigh=Math.Max(1,(int)Math.Ceiling(item.HeightM/.1));}
        }
    }
}

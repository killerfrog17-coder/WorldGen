using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public static class AdvancedStonework
    {
        public static IEnumerable<PortableRecipe> Recipes()
        {
            foreach(var kind in new[]{PossessionKind.SmallRock,PossessionKind.LargeRock})
                yield return new PortableRecipe{Id="prepare-core-"+kind,Name="Prepare a striking platform and flaking face",Output=kind,Primary=new[]{kind},Materials=Array.Empty<CraftMaterialNeed>(),Tools=new[]{CraftCapability.Strike},WorkSeconds=900,Maintenance=true,PrepareCore=true,MinMassKg=.2,MinWidthM=.065};
            foreach(var kind in new[]{PossessionKind.StoneFlake,PossessionKind.StoneBlade,PossessionKind.StoneScraper,PossessionKind.StonePoint,PossessionKind.Arrowhead})
                yield return new PortableRecipe{Id="pressure-finish-"+kind,Name="Pressure-flake the edge with antler",Output=kind,Primary=new[]{kind},Materials=Array.Empty<CraftMaterialNeed>(),Tools=new[]{CraftCapability.Pressure},WorkSeconds=600,Maintenance=true,PressureFinish=true};
        }
        public static void Prepare(Possession p)
        {
            p.MassKg*=.86;double shrink=Math.Pow(.86,1d/3);p.WidthM*=shrink;p.HeightM*=shrink;p.DepthM*=shrink;
            p.FlakeRemovals+=6;p.CorePrepared=true;
            if(p.Kind==PossessionKind.Grindstone)p.Kind=StoneCrafting.Size(p)==RockSize.Large?PossessionKind.LargeRock:PossessionKind.SmallRock;
            p.CellsWide=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1));p.CellsHigh=Math.Max(1,(int)Math.Ceiling(p.HeightM/.1));
        }
        public static void PressureFinish(Possession p)
        {
            p.MassKg*=.98;p.EdgeSharpness=1;p.PressureFinished=true;p.UseDamage=Math.Max(0,p.UseDamage-.3);
            // Retouch removes small marginal flakes; it does not replace the
            // original tool, change its material, or undo a broken haft.
            if(p.Kind==PossessionKind.StoneFlake)p.HeightM*=.98;else p.DepthM*=.98;
        }
    }
}

using System;

namespace WorldGen.Core
{
    public static class PaleolithicCampcraft
    {
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.WovenMat,Name="Rolled reed mat",Form=PortableForm.Mat,Width=.65,Height=.2,Depth=.2,Mass=1.4,Aliases="reed mat|woven mat|mat"},
            new PortableItem{Kind=PossessionKind.BonePendant,Name="Bone pendant",Form=PortableForm.Necklace,Width=.2,Height=.3,Depth=.18,Mass=.025,Wear=PossessionPlace.Neck,Aliases="pendant|bone necklace|necklace"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="weave-mat",Name="Twine a reed sleeping mat",Output=PossessionKind.WovenMat,Primary=new[]{PossessionKind.WeavingStrips},Materials=new[]{new CraftMaterialNeed(7,PossessionKind.WeavingStrips),new CraftMaterialNeed(1,PossessionKind.Cordage,PossessionKind.SinewCord)},Tools=Array.Empty<CraftCapability>(),WorkSeconds=3600,Yield=.9},
            new PortableRecipe{Id="bone-pendant",Name="Carve, pierce and string a bone pendant",Output=PossessionKind.BonePendant,Primary=new[]{PossessionKind.Bone},Materials=new[]{new CraftMaterialNeed(1,PossessionKind.Thread,PossessionKind.SinewCord)},Tools=new[]{CraftCapability.Cut|CraftCapability.Scrape,CraftCapability.Pierce},WorkSeconds=1800,Yield=.3}
        };
    }
}

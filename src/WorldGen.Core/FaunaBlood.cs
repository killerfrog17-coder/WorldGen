namespace WorldGen.Core
{
    public enum FaunaBloodKind { Red,PaleHemolymph,CopperHemolymph }
    /// <summary>Classifies canonical model profiles, not words in evolved species
    /// names. Spider monkeys, wildebeest and giant pandas are vertebrates.</summary>
    public static class FaunaBlood
    {
        public static FaunaBloodKind Kind(string profile)
        {
            switch((profile??"").ToLowerInvariant())
            {
                case "butterflies-and-moths-guild":case "fireflies-derived-lineage":
                case "bees-guild":case "dragonflies-guild":
                case "native-detritivorous-flies-guild":case "grasshoppers-guild":
                case "native-litter-beetles-guild":return FaunaBloodKind.PaleHemolymph;
                case "orb-weaving-spiders-guild":case "coastal-squid-guild":return FaunaBloodKind.CopperHemolymph;
                default:return FaunaBloodKind.Red;
            }
        }
    }
}

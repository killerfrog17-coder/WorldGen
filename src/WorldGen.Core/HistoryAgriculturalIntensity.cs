namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        double EffectiveFoodMultiplier(PrehistoryState s,HistoricalCulture culture)
        {
            double value=HistoricalModernEffects.FoodMultiplier(culture,s.Years);if(!ColonialHistory(s))return value;
            // Supported practice raises actual yields and usable farm intensity;
            // hearing an invention does not create food or settle empty land.
            double irrigation=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Irrigation,s.Years);
            double rotation=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.CropRotation,s.Years);
            double fertilizer=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.ArtificialFertilizers,s.Years);
            double mechanization=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.MechanizedAgriculture,s.Years);
            return D.Q(value*(1+.5*irrigation+.35*rotation+1.5*fertilizer+1.5*mechanization),1000000);
        }
    }
}

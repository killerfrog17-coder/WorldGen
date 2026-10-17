using System;
namespace WorldGen.Core
{
    // A small farming settlement is not automatically a proto-city or state.
    // Keep a real production floor, but avoid a discontinuity at the old
    // comfortable-field threshold. The exponent is an authored game prior.
    public static class HistoricalFarmsteadEstablishment
    {
        public static bool FieldAllowed(int revision,int parent,double yield,int practice)
            =>yield>=.10||yield>=.02&&(practice>=10000||revision>=24&&parent<0);
        public static double Opportunity(double yield,int practice)
        {
            if(yield<.02)return 0;
            if(yield>=.10||practice>=10000)return 1;
            double ratio=yield/.10;return ratio*ratio*ratio*ratio;
        }
    }
}

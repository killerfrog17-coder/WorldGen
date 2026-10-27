using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Explicit calibration priors for the Earth-like scenario, not
    /// measured invention dates or deductions from inherited traits. The soft
    /// tails permit earlier/later outcomes; habitat, knowledge and transmission
    /// still determine local adoption. Open trajectories omit these weights.
    /// Parameters are pinned by HomininHistory.RulesVersion.</summary>
    public static class HomininChronology
    {
        static readonly Dictionary<LithicKnowledge,(double midpoint,double width)> windows=
            new Dictionary<LithicKnowledge,(double,double)>
        {
            [LithicKnowledge.Handaxes]=(1600000,50000),
            [LithicKnowledge.FireKeeping]=(800000,50000),
            [LithicKnowledge.FireMaking]=(350000,25000),
            [LithicKnowledge.Hafting]=(400000,25000),
            [LithicKnowledge.PreparedCores]=(350000,25000),
            [LithicKnowledge.Adhesives]=(150000,15000),
            [LithicKnowledge.Blades]=(200000,20000),
            [LithicKnowledge.Watercraft]=(150000,30000),
            [LithicKnowledge.Symbols]=(100000,10000),
            [LithicKnowledge.FoodStorage]=(150000,20000),
            [LithicKnowledge.Projectiles]=(65000,6000),
            [LithicKnowledge.Needles]=(35000,2500),
            [LithicKnowledge.Microliths]=(55000,5000),
            [LithicKnowledge.PlantManagement]=(20000,2000),
            [LithicKnowledge.Cultivation]=(11000,650),
            [LithicKnowledge.Herding]=(10000,500),
            [LithicKnowledge.Pottery]=(16000,1300),
            [LithicKnowledge.Copper]=(8000,500),
            [LithicKnowledge.MetalSmelting]=(5000,350),
            [LithicKnowledge.Bronze]=(3800,300)
        };
        public static double Weight(LithicKnowledge skill,int yearsBeforePresent,bool earthLike)
        {
            if(!earthLike||!windows.TryGetValue(skill,out var window))return 1;
            double exponent=(Math.Max(0,yearsBeforePresent)-window.midpoint)/window.width;
            return 1/(1+Math.Exp(Math.Max(-40,Math.Min(40,exponent))));
        }
    }
}

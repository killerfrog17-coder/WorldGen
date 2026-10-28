using System;

namespace WorldGen.Core
{
    public enum ToolStoneMaterial { Granite,FineBasalt,CoarseBasalt,Sandstone,SilicifiedSandstone,Limestone,Chert,Mudstone,Metamorphic,Quartzite,Quartz }
    public static class ToolStone
    {
        // New loose stones have independent material and shape; old saves retain
        // their broad family, without inferring minerals from a random shape.
        public static ToolStoneMaterial Material(int variant)
        {
            if(variant>=100&&variant<133)return (ToolStoneMaterial)((variant-100)/3);
            switch((RockFamily)(variant/3))
            {case RockFamily.Basalt:return ToolStoneMaterial.FineBasalt;case RockFamily.Sandstone:return ToolStoneMaterial.Sandstone;case RockFamily.Limestone:return ToolStoneMaterial.Limestone;case RockFamily.Mudstone:return ToolStoneMaterial.Mudstone;case RockFamily.Metamorphic:return ToolStoneMaterial.Metamorphic;default:return ToolStoneMaterial.Granite;}
        }
        public static int Encode(ToolStoneMaterial material,int shape)=>100+(int)material*3+Math.Abs(shape%3);
        public static RockFamily Family(int variant)
        {
            switch(Material(variant))
            {case ToolStoneMaterial.FineBasalt:case ToolStoneMaterial.CoarseBasalt:return RockFamily.Basalt;case ToolStoneMaterial.Sandstone:case ToolStoneMaterial.SilicifiedSandstone:return RockFamily.Sandstone;case ToolStoneMaterial.Limestone:case ToolStoneMaterial.Chert:return RockFamily.Limestone;case ToolStoneMaterial.Mudstone:return RockFamily.Mudstone;case ToolStoneMaterial.Metamorphic:case ToolStoneMaterial.Quartzite:return RockFamily.Metamorphic;default:return RockFamily.Granite;}
        }
        public static ToolStoneMaterial Source(GroundProfile ground,V position,int seed,double radiusM)
        {
            // Illustrative lithological occurrences, not surveyed geology. Stable
            // hundreds-of-metres source patches (compressed-day gameplay); biome and per-stone shape never decide.
            double deposit=.5+.5*SphereNoise.Sample(position*radiusM,seed^0x7621,.002);
            switch(ground.Bedrock)
            {case RockFamily.Granite:return deposit>.58?ToolStoneMaterial.Quartz:ToolStoneMaterial.Granite;
             case RockFamily.Basalt:return deposit>.3?ToolStoneMaterial.FineBasalt:ToolStoneMaterial.CoarseBasalt;
             case RockFamily.Sandstone:return deposit>.5?ToolStoneMaterial.SilicifiedSandstone:ToolStoneMaterial.Sandstone;
             case RockFamily.Limestone:return deposit>.45?ToolStoneMaterial.Chert:ToolStoneMaterial.Limestone;
             case RockFamily.Metamorphic:return deposit>.4?ToolStoneMaterial.Quartzite:ToolStoneMaterial.Metamorphic;
             default:return ToolStoneMaterial.Mudstone;}
        }
        public static string Name(int variant)
        {
            switch(Material(variant))
            {case ToolStoneMaterial.FineBasalt:return "Fine-grained basalt";case ToolStoneMaterial.CoarseBasalt:return "Coarse basalt";case ToolStoneMaterial.SilicifiedSandstone:return "Silicified sandstone";case ToolStoneMaterial.Chert:return "Chert";case ToolStoneMaterial.Quartzite:return "Quartzite";case ToolStoneMaterial.Quartz:return "Vein quartz";case ToolStoneMaterial.Limestone:return "Limestone";case ToolStoneMaterial.Mudstone:return "Mudstone";case ToolStoneMaterial.Metamorphic:return "Metamorphic rock";case ToolStoneMaterial.Sandstone:return "Sandstone";default:return "Granite";}
        }
        public static double Quality(int variant)
        {
            switch(Material(variant)){case ToolStoneMaterial.CoarseBasalt:return .25;case ToolStoneMaterial.FineBasalt:return .7;case ToolStoneMaterial.Chert:return .9;case ToolStoneMaterial.SilicifiedSandstone:return .55;case ToolStoneMaterial.Quartzite:return .65;case ToolStoneMaterial.Quartz:return .4;default:return 0;}
        }
        public static V Color(int variant,V original)
        {
            switch(Material(variant)){case ToolStoneMaterial.Chert:return new V(.35,.32,.26);case ToolStoneMaterial.Quartz:return new V(.82,.8,.73);case ToolStoneMaterial.FineBasalt:return new V(.25,.27,.28);case ToolStoneMaterial.CoarseBasalt:return new V(.42,.43,.41);case ToolStoneMaterial.Quartzite:return new V(.66,.58,.46);case ToolStoneMaterial.SilicifiedSandstone:return new V(.51,.34,.23);default:return original;}
        }
    }
}

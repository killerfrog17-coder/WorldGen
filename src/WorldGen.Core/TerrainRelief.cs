using System;

namespace WorldGen.Core
{
    /// <summary>Cheap derived landform controls shared by the map and local generator.
    /// Relief is an estimated detail envelope, not altitude or a measured local height range.</summary>
    public readonly struct TerrainRelief
    {
        public readonly double CoarseM, Plateau, Sediment, HillsM, MountainsM;
        public double DetailM => HillsM + MountainsM;
        public TerrainRelief(double elevation, double coarse, double fold, double plateau, double sediment)
        {
            CoarseM = coarse;
            Plateau = Math.Max(plateau, OrogenMorphology.Smooth(1800,4200,elevation)*(1-OrogenMorphology.Smooth(350,1500,coarse))*.85);
            Sediment = D.Clamp(sediment);
            double mountains = D.Clamp(Math.Max(fold,(coarse-180)/1900))*(1-.94*Plateau);
            HillsM = (8+Math.Min(100,coarse*.16))*(1-.92*Plateau)*(1-.94*Sediment);
            MountainsM = (240+Math.Min(2300,coarse*.55))*mountains*(1-.9*Sediment);
        }
        public static TerrainRelief For(World world,int cell)
        {
            double low=world.ElevationM[cell],high=low;
            foreach(int next in world.Surface.Cells[cell].Neighbors){low=Math.Min(low,world.ElevationM[next]);high=Math.Max(high,world.ElevationM[next]);}
            double At(int[] values)=>cell<values.Length?values[cell]/1000.0:0;
            var habitat=world.Ecology!=null&&cell<world.Ecology.Habitat.Length?world.Ecology.Habitat[cell]:null;
            double sediment=habitat==null?At(world.BasinPermille)/1.5:Math.Max(habitat.FloodFractionPermille/1000.0,habitat.Soil==SoilKind.Alluvial?.85:0);
            return new TerrainRelief(world.ElevationM[cell],high-low,At(world.FoldPermille),At(world.PlateauPermille),sediment);
        }
    }
}

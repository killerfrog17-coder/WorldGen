using System;

namespace WorldGen.Core
{
    public readonly struct HumanCampStyle
    {
        public readonly CampDesign Design;public readonly CampCover Cover;public readonly bool Available,CarryPoles;
        public HumanCampStyle(CampDesign design,CampCover cover,bool available,bool carryPoles){Design=design;Cover=cover;Available=available;CarryPoles=carryPoles;}
    }
    public static class HumanCampArchitecture
    {
        // A shared building tradition is moderated by the actual seasonal
        // climate, vegetation, residence pattern and available techniques.
        public static HumanCampStyle Select(ForagerCulture culture,ForagerDistrict district,HabitatCell habitat,bool[] techniques,int season,bool longStay)
        {
            bool Has(int k)=>k<techniques.Length&&techniques[k];
            double style=Rng.Value(culture.Seed,"camp-building-tradition",culture.Id);
            double cold=habitat.ColdTenthsC/10d,warm=habitat.WarmTenthsC/10d,temperature=season==3?cold:season==1?warm:(cold+warm)*.5;
            bool chilly=temperature<8,hides=Has(6)&&Has(35),mats=Has(36),framed=Has(7)&&(Has(34)||Has(31));
            bool wetland=habitat.Biome==Biome.Freshwater||habitat.Biome==Biome.Peatland||habitat.FloodFractionPermille>100||district.River>.4;
            CampCover cover=chilly&&hides?CampCover.Hide:wetland&&mats?CampCover.Reed:district.Aridity<.4&&hides?CampCover.Hide:district.Canopy>.5&&district.AnnualRainMm>900&&Has(3)?CampCover.Bark:CampCover.Brush;
            if(!framed)
            {
                bool stone=district.Stone>.65&&district.Canopy<.2;
                return new HumanCampStyle(stone?CampDesign.StoneWindbreak:CampDesign.Windbreak,cover,stone,false);
            }
            CampDesign design;
            if(chilly&&cover==CampCover.Hide)design=district.Wood>.25&&style>.58?CampDesign.BrushHut:CampDesign.ConicalTent;
            else if(longStay&&district.Canopy>.2)design=style>.6&&(culture.Residence==ForagerResidence.CollectorCamps||culture.Residence==ForagerResidence.AquaticResidence)?CampDesign.LongHut:CampDesign.BrushHut;
            else if(temperature>27&&district.AnnualRainMm<900&&style>.65)design=CampDesign.OpenCanopy;
            else if(district.AnnualRainMm>1200)design=style>.55?CampDesign.BrushHut:CampDesign.RidgeTent;
            else design=style>.75&&district.Canopy>.25?CampDesign.BrushHut:CampDesign.LeanTo;
            bool carry=district.Wood<.2||district.Canopy<.15;
            return new HumanCampStyle(design,cover,true,carry);
        }
    }
}

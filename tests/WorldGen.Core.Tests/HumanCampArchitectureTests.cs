using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

static class HumanCampArchitectureTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);n++;}
        bool[] tech=Enumerable.Repeat(true,ForagerTechnology.Count).ToArray();var seen=new HashSet<CampDesign>();
        for(int seed=1;seed<=80;seed++)
        {
            var c=new ForagerCulture{Seed=seed,Id=seed,Residence=ForagerResidence.CollectorCamps};
            var forest=new ForagerDistrict{Canopy=.8,Wood=.8,AnnualRainMm=1600,Aridity=.9};var temperate=new HabitatCell{ColdTenthsC=-120,WarmTenthsC=240,Biome=Biome.TemperateForest};
            var winter=HumanCampArchitecture.Select(c,forest,temperate,tech,3,false);Check(winter.Cover==CampCover.Hide&&winter.CarryPoles&&winter.Available,"Cold-season camps retain weather protection and valuable fitted frames");seen.Add(winter.Design);
            var summer=HumanCampArchitecture.Select(c,forest,temperate,tech,1,false);Check(summer.Cover!=CampCover.Hide,"The same forest need not use a winter skin tent throughout warm summer");seen.Add(summer.Design);
            var baseCamp=HumanCampArchitecture.Select(c,forest,temperate,tech,1,true);Check(baseCamp.Design==CampDesign.BrushHut||baseCamp.Design==CampDesign.LongHut,"Longer residence and shared building tradition allow round or elongated huts");seen.Add(baseCamp.Design);
            var desert=new ForagerDistrict{Wood=.05,Canopy=.02,Aridity=.12,AnnualRainMm=100};var hot=new HabitatCell{ColdTenthsC=190,WarmTenthsC=380,Biome=Biome.Desert};var dry=HumanCampArchitecture.Select(c,desert,hot,tech,1,false);
            Check(dry.CarryPoles&&dry.Cover==CampCover.Hide&&dry.Available,"Treeless dry-country camps conserve transported poles and usable skins rather than assuming local thatch");seen.Add(dry.Design);
            var wet=new HabitatCell{ColdTenthsC=150,WarmTenthsC=280,FloodFractionPermille=300,Biome=Biome.Freshwater};var marsh=HumanCampArchitecture.Select(c,new ForagerDistrict{River=.8,Canopy=.3,Wood=.4,Aridity=.8,AnnualRainMm=1200},wet,tech,1,true);
            Check(marsh.Cover==CampCover.Reed,"Wetland materials and mat-working knowledge can inform the covering");seen.Add(marsh.Design);
            var again=HumanCampArchitecture.Select(c.Copy(),forest,temperate,tech,3,false);Check(again.Design==winter.Design&&again.Cover==winter.Cover,"Building traditions remain deterministic for a culture and environment");
        }
        Check(seen.Count>=6,"The habitat/culture suite admits a genuine range of existing shelter families");
        var none=new bool[ForagerTechnology.Count];Check(!HumanCampArchitecture.Select(new ForagerCulture(),new ForagerDistrict{Canopy=.8},new HabitatCell(),none,0,false).Available,"A projected camp does not bypass missing construction techniques");
        Console.WriteLine("Camp architecture checks PASS: "+n+" / "+string.Join(", ",seen));
    }
}

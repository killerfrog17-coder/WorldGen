using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class PaleolithicClothingTests
{
    public static void Run()
    {
        int checks=0;void Check(bool condition,string why){checks++;if(!condition)throw new Exception(why);}
        var inventory=new PlayerInventory();var position=new V(1,0,0);
        foreach(var kind in new[]{PossessionKind.FurCoat,PossessionKind.HideLeggings,PossessionKind.HideFootwear,PossessionKind.HideHood,PossessionKind.HidePack})
        {
            var item=PortableTechnology.Create(kind,position,"reindeer",2,31,789);inventory.Items.Add(item);
            Check(inventory.Move(item.Id,PortableTechnology.Wear(item).Value,"",0,0,false,out _),"Distinct body attachment coexists with the pack: "+kind);
        }
        inventory.Validate();double insulation=PaleolithicClothing.Insulation(inventory);
        Check(insulation>=.85&&insulation<1,"Complete cold outfit protects without absolute thermal immunity");
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));saved.Validate();
        Check(PaleolithicClothing.Insulation(saved)==insulation&&saved.Items.All(p=>p.AppearanceSpecies=="reindeer"&&p.SourceAnimalId==789),"Actual worn clothing, source hides and protection survive save/load");
        var second=PortableTechnology.Create(PossessionKind.HideWrap,position);inventory.Items.Add(second);
        Check(!inventory.Move(second.Id,PossessionPlace.Torso,"",0,0,false,out _),"Another torso garment cannot silently displace the equipped coat");
        var bare=new PlayerSurvival();var dressed=bare.Copy();
        var cold=new SurvivalEnvironment{AirTemperatureC=-12,Humidity=.5,WindMps=4};var protectedCold=cold;protectedCold.Insulation=protectedCold.ClothingInsulation=insulation;
        SurvivalRules.Advance(bare,300,60,cold,0,false);SurvivalRules.Advance(dressed,300,60,protectedCold,0,false);
        Check(dressed.CoreTemperatureC>bare.CoreTemperatureC+1&&!dressed.Dead,"Crafted and worn outfit substantially reduces cold exposure in actual survival rules");
        var hotBare=new PlayerSurvival();var hotClothed=hotBare.Copy();var hot=new SurvivalEnvironment{AirTemperatureC=33,Humidity=.6,Sunlight=.8};var hotCoat=hot;hotCoat.Insulation=hotCoat.ClothingInsulation=insulation;
        SurvivalRules.Advance(hotBare,120,60,hot,4,false);SurvivalRules.Advance(hotClothed,120,60,hotCoat,4,false);
        Check(hotClothed.CoreTemperatureC>hotBare.CoreTemperatureC,"Heavy fur has a heat cost during warm-weather exertion");
        foreach(var item in inventory.Items.Where(PaleolithicClothing.Garment))item.Place=PossessionPlace.Ground;
        Check(PaleolithicClothing.Insulation(inventory)==0,"Owning or carrying garments does not provide protection unless worn");
        Console.WriteLine($"PASS {checks} Paleolithic garment equipment/provenance/thermal checks; cold naked={bare.CoreTemperatureC:F2}C, dressed={dressed.CoreTemperatureC:F2}C");
    }
}

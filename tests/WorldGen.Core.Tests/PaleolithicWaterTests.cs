using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicWaterTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var point=new V(1,0,0);var fresh=new WaterQuality(LocalWaterKind.River,.08,.2,.1);var salt=new WaterQuality(LocalWaterKind.Ocean,35,.04,.3);
        var skin=PortableTechnology.Create(PossessionKind.Waterskin,point,"elk",2,31,710);double dry=skin.MassKg;
        Check(PaleolithicWater.Fill(skin,fresh,20)==2&&PaleolithicWater.Fill(skin,fresh,1)==0,"Filling is bounded by the actual two-litre vessel");
        Check(Math.Abs(skin.MassKg-dry-2)<1e-10,"Water contributes to actual carried/projectile mass");
        var inventory=new PlayerInventory{Items={skin}};inventory.Move(skin.Id,PossessionPlace.Shoulder,"",0,0,false,out _);inventory.Validate();
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));saved.Validate();var restored=saved.Find(skin.Id);
        Check(restored.WaterLitres==2&&restored.WaterContamination==.2&&restored.AppearanceSpecies=="elk"&&restored.SourceAnimalId==710,"Water volume/quality and source-hide provenance survive save/load");
        var person=new PlayerSurvival{WaterDeficitLitres=1,MetabolismVersion=1};
        Check(PaleolithicWater.Drink(restored,person,GameMode.Survival,.25,.99)==.25&&restored.WaterLitres==1.75&&person.WaterDeficitLitres<1,"A real mouthful consumes finite water and hydrates");
        var original=skin.Copy();PaleolithicWater.Drain(skin,1);PaleolithicWater.Fill(skin,salt,1);
        Check(Math.Abs(skin.WaterSalinityPpt-17.54)<1e-10&&Math.Abs(skin.WaterContamination-.12)<1e-10,"Mixing retains salt and contamination by actual volume");
        double salinity=skin.WaterSalinityPpt;PaleolithicWater.Heat(skin,100,59);
        Check(!skin.WaterBoiled&&Math.Abs(skin.WaterContamination-.12)<1e-12,"A brief warm-up does not instantly purify a vessel");PaleolithicWater.Heat(skin,100,1);
        Check(skin.WaterBoiled&&Math.Abs(skin.WaterContamination-.00012)<1e-12&&skin.WaterSalinityPpt==salinity,"Sustained heating reduces biological hazard while leaving salt");
        var thirsty=new PlayerSurvival{WaterDeficitLitres=1,MetabolismVersion=1};PaleolithicWater.Drink(skin,thirsty,GameMode.Survival,.25,.99);
        Check(thirsty.WaterDeficitLitres>1,"Boiled salt water remains dehydrating");
        PaleolithicWater.Fill(skin,fresh,.25);Check(!skin.WaterBoiled&&skin.WaterContamination>.01,"Untreated top-ups can recontaminate treated contents");
        var bark=PortableTechnology.Create(PossessionKind.BarkWaterVessel,point);double before=skin.WaterLitres+bark.WaterLitres,mass=skin.MassKg+bark.MassKg;
        Check(PaleolithicWater.Transfer(skin,bark,100)==1.5&&Math.Abs(skin.WaterLitres+bark.WaterLitres-before)<1e-9&&Math.Abs(skin.MassKg+bark.MassKg-mass)<1e-9,"Pouring between vessels conserves water, quality and total mass");
        Check(PaleolithicWater.Transfer(bark,bark,1)==0,"A vessel cannot pour into itself");
        PaleolithicWater.Drain(skin,100);Check(skin.WaterLitres==0&&skin.StoredWaterKind==LocalWaterKind.None&&Math.Abs(skin.MassKg-dry)<1e-9,"Emptying restores dry mass and clears obsolete contents");
        var hands=new PlayerInventory();var spear=HumanEquipment.Item(PossessionKind.StoneTippedSpear,1,"r",point);var blade=PortableTechnology.Create(PossessionKind.StoneBlade,point);hands.Items.AddRange(new[]{spear,blade,original});
        hands.Move(spear.Id,PossessionPlace.RightHand,"",0,0,false,out _);hands.Move(blade.Id,PossessionPlace.LeftHand,"",0,0,false,out _);hands.Move(original.Id,PossessionPlace.Shoulder,"",0,0,false,out _);
        PaleolithicWater.Drink(original,new PlayerSurvival(),GameMode.Survival,.25,.99);hands.Validate();
        Check(hands.AtHand(PossessionPlace.RightHand)==spear&&hands.AtHand(PossessionPlace.LeftHand)==blade&&hands.Items.Count==3,"Drinking never displaces or deletes the occupied spear/blade hands");
        var nonVessel=PortableTechnology.Create(PossessionKind.HidePouch,point);Check(PaleolithicWater.Fill(nonVessel,fresh,1)==0,"An ordinary inventory pouch is not silently treated as watertight");
        var tiny=PortableTechnology.Create(PossessionKind.Waterskin,point);for(int i=0;i<20;i++)PaleolithicWater.Fill(tiny,fresh,.1);
        Check(Math.Abs(tiny.WaterLitres-2)<1e-9&&Math.Abs(tiny.WaterContamination-.2)<1e-9,"Repeated small fills cannot overflow or erase contamination");
        var npcStock=new PlayerInventory{Items={PortableTechnology.Create(PossessionKind.Waterskin,point)}};
        Check(PaleolithicWater.FillCarried(npcStock,fresh)==0&&npcStock.Items[0].WaterLitres==0,"Fetching water cannot magically fill a vessel left on the ground at camp");
        npcStock.Move(npcStock.Items[0].Id,PossessionPlace.RightHand,"",0,0,false,out _);
        Check(PaleolithicWater.FillCarried(npcStock,fresh)==2&&npcStock.Items.Count==1&&npcStock.Items[0].WaterLitres==2,"An NPC carrying an actual vessel fills that same object without creating goods");
        Console.WriteLine($"PASS {checks} finite water, salinity, source provenance, thermal and ownership checks");
    }
}

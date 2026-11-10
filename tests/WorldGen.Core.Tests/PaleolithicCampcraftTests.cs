using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicCampcraftTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        var at=new V(1,0,0);var inventory=new PlayerInventory();var settings=new PlaySettings();
        for(int i=0;i<8;i++)inventory.Items.Add(PortableTechnology.Create(PossessionKind.WeavingStrips,at));
        inventory.Items.Add(PortableTechnology.Create(PossessionKind.Cordage,at));var first=inventory.Items[0];double mass=inventory.Items.Sum(p=>p.MassKg);
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("weave-mat"),first.Id,out var plan,out var reason),"Eight real reed-strip bundles and a binding can be woven: "+reason);
        var mat=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),100000,settings);inventory.Validate();
        Check(mat.Kind==PossessionKind.WovenMat&&mat.MassKg<=mass&&inventory.Items.Count==1&&mat.CellsWide==7,"Weaving retains the same item, consumes the actual strips and preserves a long rolled packing footprint");
        var bed=new CampObject{Kind=CampKind.Bedding,Position=at};double bare=CampRules.BedComfort(bed);inventory.Items.Remove(mat);CampRules.Add(bed,mat);
        Check(CampRules.Bedding(mat)&&bed.BeddingDepthM>0&&CampRules.BedComfort(bed)>bare&&CampRules.BedInsulation(bed)>0,"Spreading a real woven mat provides modest bedding comfort and insulation");
        var second=mat.Copy();second.Id="second-mat";double depth=bed.BeddingDepthM;CampRules.Add(bed,second);Check(bed.BeddingDepthM>depth,"Layering real mats increases the actual bed depth");
        var shelter=CampBlueprints.Create(CampDesign.LeanTo,2,2,CampCover.Reed);Check(CampBlueprints.CoverMaterial(shelter,mat),"Woven mat can supply an actual reed shelter covering");
        Check(!CampBlueprints.CoverMaterial(CampBlueprints.Create(CampDesign.LeanTo,2,2,CampCover.Hide),mat),"A reed mat does not silently count as animal hide");
        var bone=PortableTechnology.Create(PossessionKind.Bone,at,"deer",1);var thread=PortableTechnology.Create(PossessionKind.Thread,at);var blade=PortableTechnology.Create(PossessionKind.StoneBlade,at);var awl=PortableTechnology.Create(PossessionKind.StoneBorer,at);inventory.Items.AddRange(new[]{bone,thread,blade,awl});
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("bone-pendant"),bone.Id,out plan,out reason),"Bone pendant requires actual carving, piercing tools and thread: "+reason);
        var pendant=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),100000,settings);inventory.Validate();
        Check(pendant.Kind==PossessionKind.BonePendant&&pendant.AppearanceSpecies=="deer"&&inventory.Find(thread.Id)==null&&inventory.Find(blade.Id)==blade&&inventory.Find(awl.Id)==awl,"Adornment retains animal provenance, consumes thread and keeps the tools");
        Check(inventory.Move(pendant.Id,PossessionPlace.Neck,"",0,0,false,out reason)&&inventory.Carried(pendant),"Pendant wears on a real independent neck attachment: "+reason);
        var coat=PortableTechnology.Create(PossessionKind.FurCoat,at);inventory.Items.Add(coat);Check(inventory.Move(coat.Id,PossessionPlace.Torso,"",0,0,false,out _)&&pendant.ContainerWidth==0,"Jewellery coexists with clothing without granting inventory space");
        var spare=pendant.Copy();spare.Id="spare-pendant";spare.Place=PossessionPlace.Ground;inventory.Items.Add(spare);Check(!inventory.CanMove(spare.Id,PossessionPlace.Neck,"",0,0,false,out _),"The same physical neck attachment cannot hold two separate equipped necklaces");
        var restored=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));restored.Validate();var camps=WorldJson.Read<CampState>(WorldJson.Write(new CampState{Objects={bed}}));camps.Validate();Check(restored.Find(pendant.Id).Place==PossessionPlace.Neck&&camps.Objects[0].Materials.Count==2,"Worn jewellery and layered camp mats survive full inventory/camp saves");
        Console.WriteLine($"PASS {count} reed weaving, bedding/cover use, bone jewellery, equipment and ownership checks");
    }
}

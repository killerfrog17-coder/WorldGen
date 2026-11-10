using WorldGen.Core;
using WorldGen.Serialization;
static class PrimitiveWatercraftTests
{
 public static void Run()
 {
  int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
  var at=new V(1,0,0);
  foreach(var design in new[]{CampDesign.ReedRaft,CampDesign.SkinBoat})
  {
   var plan=CampBlueprints.Create(design,2,3,design==CampDesign.SkinBoat?CampCover.Hide:CampCover.Reed);var c=new CampObject{Kind=CampKind.Blueprint,Blueprint=plan,Position=at};var inventory=new PlayerInventory();double supplied=0;
   Check(!PrimitiveWatercraft.Seaworthy(c,75),"An unbuilt outline has no buoyancy: "+design);
   foreach(var part in CampBlueprints.Components(plan))
   {
    PossessionKind kind=part.Kind==CampPartKind.Reedwork?PossessionKind.ReedBundle:part.Kind==CampPartKind.Seal?PossessionKind.CompoundAdhesive:part.Kind==CampPartKind.Binding?PossessionKind.Cordage:part.Kind==CampPartKind.Cover?PossessionKind.WorkedHide:PossessionKind.Branch;
    var item=kind==PossessionKind.Branch?WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=at,WidthM=part.LengthM,HeightM=part.WidthM*1.1}):PortableTechnology.Create(kind,at,"deer",1);item.Id="boat-"+Guid.NewGuid().ToString("N");item.NaturalId=0;if(part.Kg>0)item.MassKg=part.Kg;supplied+=item.MassKg;inventory.Items.Add(item);
    Check(CampBlueprints.Supply(plan,part.Key,inventory,item.Id,true,out _,out var why),"Actual fitting material supplies "+part.Name+": "+why);CampBlueprints.Work(plan,part.Key,part.Seconds);
   }
   Check(CampBlueprints.Complete(plan)&&inventory.Items.Count==0&&Math.Abs(c.MassKg-supplied)<1e-7,"Construction retains exactly the supplied mass in the physical hull");
   Check(PrimitiveWatercraft.Seaworthy(c,100)&&PrimitiveWatercraft.Draft(c,100)>PrimitiveWatercraft.Draft(c,0),"An adult and carried gear displace more water than an empty craft");
   Check(!PrimitiveWatercraft.Seaworthy(c,10000),"Finite hull volume rejects an impossible load");
   c.Heading=0;var still=PrimitiveWatercraft.Step(c,.1,1,0,0,0,false);Check(still.Length==0,"A craft without a paddle cannot conjure propulsion");var row=PrimitiveWatercraft.Step(c,.1,1,0,0,0,true);Check(row.Z>0&&row.X==0,"A real paddle propels the current hull heading");
   var drift=PrimitiveWatercraft.Step(c,.1,0,0,1,0,false);Check(drift.X>0,"Water current moves the active craft without fake paddle effort");
   var session=new PlaySession{Camp=new CampState{Objects={c}},WatercraftId=c.Id};session.Validate();var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();Check(saved.WatercraftId==c.Id&&saved.Camp.Objects[0].BoatVelocityEast==c.BoatVelocityEast,"Boarded craft, all constituent goods, heading and motion persist");
   if(design==CampDesign.SkinBoat)Check(plan.Parts.SelectMany(p=>p.Materials).Any(p=>p.AppearanceSpecies=="deer")&&plan.Parts.Any(p=>p.Materials.Any(m=>m.Kind==PossessionKind.CompoundAdhesive)),"Skin hull retains source hides and actual waterproofing adhesive");
  }
  var pole=WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=at,WidthM=1.5,HeightM=.025});pole.Id="thin";var inv=new PlayerInventory{Items={pole,PortableTechnology.Create(PossessionKind.StoneAxe,at),PortableTechnology.Create(PossessionKind.StoneBlade,at)}};
  Check(!PortableCrafting.Plan(inv,inv.Items,PortableCrafting.Recipe("hew-paddle"),pole.Id,out _,out _),"A thin stick cannot grow a broad wooden paddle blade");pole.WidthM=pole.DepthM=.14;pole.MassKg=12;Check(pole.Hands==2&&PortableCrafting.Plan(inv,inv.Items,PortableCrafting.Recipe("hew-paddle"),pole.Id,out var craft,out _),"An actual thick limb and cutting/chopping tools support paddle hewing");
  PortableCrafting.Plan(inv,inv.Items,PortableCrafting.Recipe("hew-paddle"),pole.Id,out var finalPlan,out _);var paddle=StoneCrafting.Advance(inv,PortableCrafting.Start(inv,finalPlan),100000,new PlaySettings());Check(paddle.Kind==PossessionKind.Paddle&&paddle.MassKg<=12,"Hewing yields a real paddle while discarding wood offcuts");
  Console.WriteLine($"PASS {n} material-backed watercraft, buoyancy, paddling, current, provenance and save checks");
 }
}

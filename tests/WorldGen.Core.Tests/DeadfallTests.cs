using WorldGen.Core;
using WorldGen.Serialization;
static class DeadfallTests
{
 public static void Run()
 {
  int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
  var at=new V(1,0,0);var inventory=new PlayerInventory();var branch=WoodCrafting.Branch(new ScatterInstance{Id=1,Kind=ScatterKind.Branch,Position=at,WidthM=1.2,HeightM=.03});branch.NaturalId=0;branch.Id="branch";var knife=PortableTechnology.Create(PossessionKind.StoneBlade,at);inventory.Items.AddRange(new[]{branch,knife});
  Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("carve-deadfall-trigger"),branch.Id,out var plan,out var why),"Sound wood and a real cutting edge permit notched trigger carving: "+why);
  var trigger=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),100000,new PlaySettings());Check(trigger.Kind==PossessionKind.DeadfallTrigger&&inventory.Find(knife.Id)==knife,"Carving consumes the actual branch and retains the blade");
  var c=new CampObject{Kind=CampKind.Deadfall,Position=at};inventory.Items.Remove(trigger);CampRules.Add(c,trigger);Check(!Deadfalls.Arm(c),"A wooden trigger alone is not a functioning trap");
  var rock=new Possession{Id="weight",Kind=PossessionKind.LargeRock,Position=at,WidthM=.28,HeightM=.15,DepthM=.25,MassKg=10,CellsWide=3,CellsHigh=2,Variant=ToolStone.Encode(ToolStoneMaterial.Granite,0)};rock.MassKg=10;CampRules.Add(c,rock);Check(!Deadfalls.Arm(c),"An unbaited trap is not armed silently");
  var bait=PortableTechnology.Create(PossessionKind.Nuts,at);Foraging.Resize(bait,.025);CampRules.Add(c,bait);double mass=c.MassKg;Check(Deadfalls.Arm(c)&&Deadfalls.SetReason(c).Length>0,"Complete physical materials arm a single-use trigger");
  Check(!Deadfalls.Accepts(c,rock),"A set weight cannot receive extra materials");Check(Deadfalls.Eligible(3,"ground")&&!Deadfalls.Eligible(70,"ground")&&!Deadfalls.Eligible(.1,"air"),"Only actual small ground animals fit the bounded deadfall");
  Check(Deadfalls.Crosses(-1,0,1,0,.2)&&!Deadfalls.Crosses(-1,.3,1,.3,.2),"Swept contact catches a real crossing but not a nearby pass");
  double damage=Deadfalls.Damage(c);Check(damage>10&&damage<40,"A ten kilogram falling stone gives finite impact damage, not universal instant death");
  var saved=WorldJson.Read<CampState>(WorldJson.Write(new CampState{Objects={c}}));saved.Validate();Check(saved.Objects[0].TrapArmed&&saved.Objects[0].Materials.Select(p=>p.Id).SequenceEqual(c.Materials.Select(p=>p.Id)),"Armed mechanism and all material identities persist across save/load");
  Check(Deadfalls.Spring(c)==damage&&c.TrapSprung&&!c.TrapArmed&&c.Materials.Count==2&&Math.Abs(c.MassKg-(mass-bait.MassKg))<1e-8,"Springing consumes only actual bait and retains recoverable stone and trigger");Check(Deadfalls.Spring(c)==0,"The same trap cannot hit twice without a deliberate reset");
  Check(!Deadfalls.Arm(c),"Reset needs new physical bait");var second=bait.Copy();second.Id="second-bait";CampRules.Add(c,second);Check(Deadfalls.Arm(c)&&!c.TrapSprung,"New bait and resetting reuse the same original stone and wood");
  var state=new CampState{Objects={c}};state.Validate();CampRules.Advance(c,86400,0,.5,3,.5);Check(c.TrapArmed&&c.Materials.Count==3,"Elapsed time never fabricates a catch or removes the trap's real materials");
  var heavy=rock.Copy();heavy.MassKg=25;Check(Deadfalls.Weight(heavy),"A genuinely carryable twenty-five kilogram stone can supply a heavier deadfall");heavy.MassKg=26;Check(!Deadfalls.Weight(heavy),"A heavier inaccessible boulder cannot be conjured into this portable deadfall");
  Console.WriteLine($"PASS {n} physical deadfall crafting, swept contacts, impact, reset and save checks");
 }
}

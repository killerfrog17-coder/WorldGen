using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class NpcCampWorkTests
{
    public static void Run()
    {
        int n=0;void Check(bool value,string text){if(!value)throw new Exception(text);n++;}
        var shelter=new CampObject{Id="shelter",Kind=CampKind.Blueprint,NpcGroup=7,NpcHousehold=11,Position=new V(1,0,0),Blueprint=CampBlueprints.Create(CampDesign.LeanTo,2,2,CampCover.Hide)};
        HumanCampLayout.FitShelter(shelter,new double[9]);var ids=shelter.OwnedMaterials().Select(p=>p.Id).ToHashSet();double mass=shelter.MassKg;
        var hide=shelter.OwnedMaterials().First(CampRules.Hide);hide.AppearanceSpecies="Elk";hide.SourceAnimalId=914;string hideId=hide.Id;
        var cargo=new List<Possession>();int count=0;
        while(NpcCampWork.NextPacking(shelter.Blueprint,false) is CampComponent part)
        {cargo.AddRange(NpcCampWork.Remove(shelter,part.Key));if(++count>1000)throw new Exception("Packing did not terminate");}
        Check(cargo.Any(CampRules.Hide)&&cargo.Any(p=>FrameRules.Binding(p)),"Portable hide coverings and lashings are recovered");
        Check(shelter.Blueprint.Parts.Any(s=>s.Materials.Any(WoodCrafting.Wood)),"A wooded temporary camp retains its actual reusable frame");
        Check(Math.Abs(shelter.MassKg+cargo.Sum(p=>p.MassKg)-mass)<1e-9,"Dismantling conserves total material mass");
        Check(ids.SetEquals(shelter.OwnedMaterials().Concat(cargo).Select(p=>p.Id)),"Dismantling conserves exact identities");
        Check(cargo.First(p=>p.Id==hideId).AppearanceSpecies=="Elk"&&cargo.First(p=>p.Id==hideId).SourceAnimalId==914,"Packing preserves the animal provenance of a covering");
        Check(cargo.All(p=>p.CampMaterial==7&&!NpcMaterialUse.Fuel(p)),"Tent materials are not automatically consumed as fire fuel");
        Check(NpcCampWork.Remove(shelter,"not-there").Length==0,"The same component cannot be recovered repeatedly");
        var inventory=new PlayerInventory{Items=cargo};var replacement=CampBlueprints.Create(CampDesign.LeanTo,2,2,CampCover.Hide);
        foreach(var part in shelter.Blueprint.Parts)replacement.Parts.Add(part.Copy());
        for(int k=0;k<100;k++)
        {
            int stage=CampBlueprints.Stage(replacement);var part=CampBlueprints.Components(replacement).FirstOrDefault(c=>c.Stage==stage&&!CampBlueprints.Done(replacement,c));if(part==null)break;
            var item=inventory.Items.FirstOrDefault(p=>CampBlueprints.Suitable(replacement,part,p));
            if(!CampBlueprints.HasMaterial(replacement,part)){Check(item!=null&&CampBlueprints.Supply(replacement,part.Key,inventory,item.Id,true,out _,out _),"An existing recovered contribution supplies the replacement shelter");}
            CampBlueprints.Work(replacement,part.Key,part.Seconds);
        }
        Check(CampBlueprints.Complete(replacement)&&inventory.Items.Count==0,"Actual supplies and work rebuild the shelter without a replacement factory");
        Check(ids.SetEquals(replacement.Parts.SelectMany(p=>p.Materials).Select(p=>p.Id)),"Reassembly preserves all original item identities");
        var clock=new HumanLifeClock(500*SimulationTime.Day,50,60,10,.1,25);var delayed=NpcCampWork.ItineraryClock(clock,12);
        Check(delayed.CalendarDay==488&&Math.Abs(delayed.SolarDays-48.8)<1e-8&&delayed.CalendarRate==clock.CalendarRate,"Only an observed group's itinerary shifts by the recorded local delay");
        var travel=new NpcCampTravel{DelayCalendarDays=12,HoldCalendarDay=488,DepartingSite=6,RebuildRequired=true};var state=new HumanGroupMemory{Group=7,Travel=travel};var copy=state.Copy();copy.Travel.DelayCalendarDays=13;
        Check(travel.DelayCalendarDays==12,"Saved local travel state deep-copies independently");travel.Validate();
        // Camp identities are full signed hashes: a later migration may leave
        // a site whose identifier is negative even though elapsed days cannot be.
        travel.DepartingSite=-5602618401484172396L;travel.Validate();
        var negativeSite=WorldJson.Read<HumanGroupMemory>(WorldJson.Write(state));negativeSite.Travel.Validate();
        Check(negativeSite.Travel.DepartingSite==travel.DepartingSite,"A real negative hashed camp identity survives itinerary validation and serialization");
        var hauling=new PlayerInventory();
        for(int k=0;k<3;k++){var pole=new Possession{Id="pole-"+k,Kind=PossessionKind.Branch,Position=new V(1,0,0),HeightM=2,WidthM=.03,DepthM=.03,MassKg=1,CampMaterial=7,OwnerGroup=7,OwnerHousehold=11,WoodSoundness=.9,CellsHigh=20,NaturalId=(ulong)k+1};hauling.Items.Add(pole);hauling.CollectedNatural.Add(pole.NaturalId);}
        var rope=PortableTechnology.Create(PossessionKind.Cordage,new V(1,0,0));rope.CampMaterial=rope.OwnerGroup=7;rope.OwnerHousehold=11;hauling.Items.Add(rope);
        var original=hauling.Items.Select(p=>p.Id).ToHashSet();double kg=hauling.Items.Sum(p=>p.MassKg);var bundle=NpcCampWork.Bundle(hauling,7,11,new V(1,0,0));
        Check(bundle!=null&&bundle.Place==PossessionPlace.Shoulder&&bundle.BundledMaterials.Count==4&&Math.Abs(bundle.MassKg-kg)<1e-9,"Existing rope binds real poles into a wearable load without adding or losing mass");hauling.Validate();
        var destination=new PlayerInventory();NpcBelongings.TransferTree(hauling,destination,bundle);destination.Validate();Check(destination.CollectedNatural.Count==3,"A transferred load carries the actual natural identity ledger with its contents");
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(destination));saved.Validate();Check(saved.Items.Single().BundledMaterials.Count==4&&Math.Abs(saved.CarriedMassKg-kg)<1e-9,"The bound load and original contributions survive an actual inventory serialization round trip");
        var separate=NpcCampWork.Unbundle(destination,bundle,new V(1,0,0));destination.Validate();Check(original.SetEquals(separate.Select(p=>p.Id))&&Math.Abs(destination.Items.Sum(p=>p.MassKg)-kg)<1e-9,"Opening a bound load recovers exactly its original rope and poles");
        foreach(var route in new[]{(-4d,0d,4d,0d),(0d,4d,0d,-4d),(-4d,-3d,4d,3d),(3d,-4d,-3d,4d),(0d,2.7,0d,-4d)})
        {
            double x=route.Item1,z=route.Item2,gx=route.Item3,gz=route.Item4;int steps=0;
            while(Math.Sqrt((gx-x)*(gx-x)+(gz-z)*(gz-z))>.15&&steps++<20)
            {
                NpcShelterRouting.OutsideWaypoint(x,z,gx,gz,1.5,1.8,out double nx,out double nz);
                for(int k=1;k<=30;k++){double t=k/30d,px=x+(nx-x)*t,pz=z+(nz-z)*t;Check(Math.Abs(px)>=1.5||Math.Abs(pz)>=1.8,"An outside route does not cut back through the shelter");}
                x=nx;z=nz;
            }
            Check(steps<20,"Corner routing reaches the far side without returning endlessly to the doorway");
        }
        for(int i=0;i<100;i++)NpcShelterRouting.OutsideWaypoint(-4,0,4,0,1.5,1.8,out _,out _);
        long beforeRouting=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<20000;i++)NpcShelterRouting.OutsideWaypoint(-4,0,4,0,1.5,1.8,out _,out _);long routingBytes=GC.GetAllocatedBytesForCurrentThread()-beforeRouting;
        Check(routingBytes==0,"Twenty thousand corner-routing queries allocate no managed memory after warmup");
        var offlineOrigin=new HumanCampMemory{Site=91,Group=7,Position=new V(1,0,0),Objects=new List<CampObject>{shelter.Copy()}};
        HumanCampLayout.FitShelter(offlineOrigin.Objects[0],new double[9]);
        var target=offlineOrigin.Objects[0].Copy();target.Id="next-shelter";target.NpcSite=92;target.Position=new V(0,1,0);target.Blueprint.Parts.Clear();
        var offlineTarget=new HumanCampMemory{Site=92,Group=7,Position=new V(0,1,0),Objects=new List<CampObject>{target}};
        var roster=Enumerable.Range(1,4).Select(i=>new ForagerPersonSample{Id=i,Household=11,AgeYears=25}).ToArray();
        var people=roster.Select(r=>new HumanPersonMemory{Person=r.Id,Group=7,Inventory=new PlayerInventory()}).ToArray();
        var cutter=PortableTechnology.Create(PossessionKind.StoneBlade,offlineOrigin.Position);people[0].Inventory.Items.Add(cutter);people[0].Inventory.Hold(cutter.Id,out _);
        var looseStock=new PlayerInventory();double beforeMass=offlineOrigin.Objects.Sum(o=>o.MassKg)+cutter.MassKg;
        NpcCampCatchUp.Apply(offlineOrigin,offlineTarget,people,roster,looseStock,true,1);
        Check(target.Blueprint.Parts.Count==0&&offlineOrigin.Objects[0].Blueprint.Parts.Count>0,"Insufficient unobserved work cannot instantly move and rebuild a tent");
        NpcCampCatchUp.Apply(offlineOrigin,offlineTarget,people,roster,looseStock,true,100000);
        Check(CampBlueprints.Complete(target.Blueprint),"A known unloaded band rebuilds with its actual transported materials");
        Possession[] OfflineGoods()=>offlineOrigin.Objects.Concat(offlineTarget.Objects).SelectMany(o=>o.OwnedMaterials()).Concat(looseStock.Items).Concat(people.SelectMany(p=>p.Inventory.Items)).SelectMany(p=>p.BundledMaterials??new List<Possession>{p}).ToArray();
        var allOffline=OfflineGoods();Check(Math.Abs(allOffline.Sum(p=>p.MassKg)-beforeMass)<1e-7,"Unobserved work conserves material mass including actual cutting offcuts");
        Check(allOffline.Select(p=>p.Id).Distinct().Count()==allOffline.Length,"Offscreen transport and assembly do not duplicate actual goods");
        int countBefore=allOffline.Length;NpcCampCatchUp.Apply(offlineOrigin,offlineTarget,people,roster,looseStock,true,100000);
        Check(OfflineGoods().Length==countBefore&&Math.Abs(OfflineGoods().Sum(p=>p.MassKg)-beforeMass)<1e-7,"Repeated catch-up does not replenish a visited band's supplies");
        var stripped=offlineTarget.Copy();stripped.Objects[0].Blueprint.Parts.Clear();
        NpcCampCatchUp.Apply(stripped,stripped,people,roster,new PlayerInventory(),true,100000);
        Check(stripped.Objects[0].Blueprint.Parts.Count==0,"A player's removed shelter materials cannot be regenerated by catch-up");
        Console.WriteLine("NPC camp material checks PASS: "+n);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

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
        var hauling=new PlayerInventory();
        for(int k=0;k<3;k++){var pole=new Possession{Id="pole-"+k,Kind=PossessionKind.Branch,Position=new V(1,0,0),HeightM=2,WidthM=.03,DepthM=.03,MassKg=1,CampMaterial=7,OwnerGroup=7,OwnerHousehold=11,WoodSoundness=.9,CellsHigh=20,NaturalId=(ulong)k+1};hauling.Items.Add(pole);hauling.CollectedNatural.Add(pole.NaturalId);}
        var rope=PortableTechnology.Create(PossessionKind.Cordage,new V(1,0,0));rope.CampMaterial=rope.OwnerGroup=7;rope.OwnerHousehold=11;hauling.Items.Add(rope);
        var original=hauling.Items.Select(p=>p.Id).ToHashSet();double kg=hauling.Items.Sum(p=>p.MassKg);var bundle=NpcCampWork.Bundle(hauling,7,11,new V(1,0,0));
        Check(bundle!=null&&bundle.Place==PossessionPlace.Shoulder&&bundle.BundledMaterials.Count==4&&Math.Abs(bundle.MassKg-kg)<1e-9,"Existing rope binds real poles into a wearable load without adding or losing mass");hauling.Validate();
        var destination=new PlayerInventory();NpcBelongings.TransferTree(hauling,destination,bundle);destination.Validate();Check(destination.CollectedNatural.Count==3,"A transferred load carries the actual natural identity ledger with its contents");
        var separate=NpcCampWork.Unbundle(destination,bundle,new V(1,0,0));destination.Validate();Check(original.SetEquals(separate.Select(p=>p.Id))&&Math.Abs(destination.Items.Sum(p=>p.MassKg)-kg)<1e-9,"Opening a bound load recovers exactly its original rope and poles");
        Console.WriteLine("NPC camp material checks PASS: "+n);
    }
}

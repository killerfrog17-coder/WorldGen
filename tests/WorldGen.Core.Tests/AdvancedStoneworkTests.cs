using WorldGen.Core;
using WorldGen.Serialization;

static class AdvancedStoneworkTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        var at=new V(1,0,0);var settings=new PlaySettings();
        var core=new Possession{Id="test-core",Kind=PossessionKind.SmallRock,Position=at,WidthM=.18,HeightM=.1,DepthM=.15,MassKg=1,CellsWide=2,CellsHigh=1,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};
        var hammer=HumanEquipment.Item(PossessionKind.SmallRock,357,"r",at);var state=new PlayerInventory{Items={core,hammer}};var plain=state.Copy();
        var ordinaryJob=StoneCrafting.Start(plain,core.Id,hammer.Id,out _);double ordinaryTime=ordinaryJob.RequiredSeconds;var ordinary=StoneCrafting.Advance(plain,ordinaryJob,1000,settings);
        var recipe=PortableCrafting.Recipe("prepare-core-SmallRock");
        Check(PortableCrafting.Plan(state,state.Items,recipe,core.Id,out var plan,out var reason),"Prepare an actual knappable ordinary stone: "+reason);
        double mass=core.MassKg;var job=PortableCrafting.Start(state,plan);StoneCrafting.Advance(state,job,.1,settings);
        var partial=WorldJson.Read<PlayerInventory>(WorldJson.Write(state));partial.Validate();Check(partial.Crafts[0].WorkSeconds>0,"Core preparation retains timed partial progress in saves");
        var result=StoneCrafting.Advance(state,job,1000,settings);Check(result==core&&core.CorePrepared&&core.Name=="Small rock"&&core.MassKg<mass&&state.Find(hammer.Id)==hammer,"Preparation retains ordinary stone identity and actual reusable hammer while removing chips");
        Check(!PortableCrafting.Plan(state,state.Items,recipe,core.Id,out _,out _),"Repeated preparation cannot stack phantom improvements");
        double before=state.Items.Sum(p=>p.MassKg);job=StoneCrafting.Start(state,core.Id,hammer.Id,out _);Check(job.RequiredSeconds<ordinaryTime,"Prepared face gives a short controlled removal after the longer preparation");
        var flake=StoneCrafting.Advance(state,job,1000,settings);state.Validate();
        Check(flake.WidthM>ordinary.WidthM&&flake.HeightM<=ordinary.HeightM&&flake.Variant==core.Variant,"Controlled removal produces a broader, thinner blank in the same stone material");
        Check(!core.CorePrepared&&Math.Abs(state.Items.Sum(p=>p.MassKg)-before)<1e-10,"The face must be prepared again; the useful flake comes out of actual remaining core mass");
        var retoucher=PortableTechnology.Create(PossessionKind.AntlerRetoucher,at);state.Items.Add(retoucher);
        Check(PortableCrafting.Plan(state,state.Items,PortableCrafting.Recipe("pressure-finish-StoneFlake"),flake.Id,out plan,out reason)&&plan.Tools.Contains(retoucher.Id),"Pressure finishing requires the actual antler tool: "+reason);
        double edge=flake.EdgeSharpness,flakeMass=flake.MassKg;var finished=StoneCrafting.Advance(state,PortableCrafting.Start(state,plan),1000,settings);
        Check(finished==flake&&flake.PressureFinished&&flake.EdgeSharpness>=edge&&flake.MassKg<flakeMass,"Pressure work refines the original edge and removes material");
        var coarse=PortableTechnology.Create(PossessionKind.StonePoint,at);coarse.Variant=ToolStone.Encode(ToolStoneMaterial.CoarseBasalt,0);state.Items.Add(coarse);
        Check(!PortableCrafting.Plan(state,state.Items,PortableCrafting.Recipe("pressure-finish-StonePoint"),coarse.Id,out _,out _),"A coarse-grained piece does not silently gain the same pressure technique");state.Items.Remove(coarse);
        var shaft=PortableTechnology.Create(PossessionKind.NotchedShaft,at);var tip=PortableTechnology.Create(PossessionKind.StonePoint,at);AdvancedStonework.PressureFinish(tip);var a=PortableTechnology.Create(PossessionKind.Cordage,at);var b=PortableTechnology.Create(PossessionKind.Cordage,at);state.Items.AddRange(new[]{shaft,tip,a,b});
        Check(PortableCrafting.Plan(state,state.Items,PortableCrafting.Recipe("seat-point"),shaft.Id,out plan,out reason),"Prepared point can be seated: "+reason);StoneCrafting.Advance(state,PortableCrafting.Start(state,plan),1000,settings);
        Check(shaft.PressureFinished,"Seating retains the actual tip finish");Check(PortableCrafting.Plan(state,state.Items,PortableCrafting.Recipe("bind-spear"),shaft.Id,out plan,out reason),"Refined spear can be bound: "+reason);StoneCrafting.Advance(state,PortableCrafting.Start(state,plan),1000,settings);
        Check(shaft.Kind==PossessionKind.StoneTippedSpear&&shaft.PressureFinished&&ItemUseRules.EffectivePenetration(shaft)>ItemUseRules.Profile(shaft).Penetration,"Finished tip quality reaches the assembled weapon and combat calculation");
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(state));saved.Validate();Check(saved.Find(shaft.Id).PressureFinished,"Finishing provenance survives the full inventory save");
        Console.WriteLine($"PASS {count} prepared-core timing, material/mass, antler pressure work, hafting and save checks");
    }
}

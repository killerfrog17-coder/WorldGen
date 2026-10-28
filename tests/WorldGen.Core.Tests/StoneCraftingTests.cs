using System;
using System.Linq;
using System.Text.Json.Nodes;
using WorldGen.Core;
using WorldGen.Serialization;

static class StoneCraftingTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
        Possession Rock(string id,int variant,double mass)=>new Possession{Id=id,Kind=PossessionKind.SmallRock,Place=PossessionPlace.Ground,Position=new V(1,0,0),Variant=variant,WidthM=.14,HeightM=.1,DepthM=.12,MassKg=mass,CellsWide=2,CellsHigh=1};
        PlayerInventory Fixture(){var inventory=PlayerInventory.Starter();inventory.Items.Add(Rock("work",3,.8));inventory.Items.Add(Rock("tool",0,.5));return inventory;}
        var settings=new PlaySettings();var state=Fixture();state.Validate();
        Check(StoneCrafting.PairReason(state.Find("work"),state.Find("tool"))=="","Suitable ordinary stones can perform both roles");
        Check(StoneCrafting.PairReason(state.Find("work"),state.Find("work"))!="","Cannot strike a stone with itself");
        Check(StoneCrafting.WorkpieceReason(state.Find("tool"))!="","Coarse granite does not become a cutting flake recipe");
        var thin=Rock("thin",0,.5);thin.HeightM=.01;Check(!StoneCrafting.StrikingStone(thin),"Thin shape is unsuitable for striking");
        var large=Rock("large",0,3);large.Kind=PossessionKind.LargeRock;Check(!StoneCrafting.StrikingStone(large),"Two-handed stone cannot be the handheld striker");
        var job=StoneCrafting.Start(state,"work","tool",out var reason);
        Check(job!=null&&reason==""&&state.Items.Count==3,"Starting work does not consume or convert input stones");
        Check(state.Find("work").Name=="Small rock"&&state.Find("tool").Name=="Small rock","Neither role renames a stone");
        double originalSeconds=StoneCrafting.RealSecondsLeft(job,settings);
        Check(originalSeconds>1&&originalSeconds<15,"Default flaking cycle is visible but short at one-hour days");
        var slower=settings.Copy();slower.ItemCraftingTimeMultiplier=2;Check(Math.Abs(StoneCrafting.RealSecondsLeft(job,slower)-originalSeconds*2)<1e-9,"Crafting multiplier doubles real duration");
        var day=settings.Copy();day.RealMinutesPerDay*=2;Check(Math.Abs(StoneCrafting.RealSecondsLeft(job,day)-originalSeconds*2)<1e-9,"Day length scales work with solar time");
        var calendar=settings.Copy();calendar.CalendarDaysPerDay=365;Check(StoneCrafting.RealSecondsLeft(job,calendar)==originalSeconds,"Calendar compression does not accelerate craft work");
        Check(StoneCrafting.Advance(state,job,originalSeconds*.3,settings)==null&&Math.Abs(job.Fraction-.3)<1e-9,"Partial work advances without premature output");
        var copy=state.Copy();copy.Crafts[0].WorkSeconds+=1;Check(copy.Crafts[0].WorkSeconds!=job.WorkSeconds,"Deep copy isolates work progress");
        var restored=WorldJson.Read<PlayerInventory>(WorldJson.Write(state));restored.Validate();Check(Math.Abs(restored.Crafts[0].Fraction-.3)<1e-9,"Partial project survives JSON serialization");
        job.Running=false;double before=job.WorkSeconds;Check(StoneCrafting.Advance(state,job,99,settings)==null&&job.WorkSeconds==before,"Paused work does not advance");
        var resumed=StoneCrafting.Start(state,"work","tool",out _);Check(ReferenceEquals(job,resumed)&&job.WorkSeconds==before,"Resume retains the same workpiece progress");
        double total=state.Items.Sum(p=>p.MassKg);var output=StoneCrafting.Advance(state,job,originalSeconds,settings);state.Validate();
        Check(output!=null&&output.Kind==PossessionKind.StoneFlake&&output.EdgeSharpness>0,"Completion produces a real cutting flake");
        Check(state.Crafts.Count==0&&state.Find("work").FlakeRemovals==1&&state.Find("tool").Name=="Small rock","Completion retains worked stone and unchanged striking tool");
        Check(Math.Abs(state.Items.Sum(p=>p.MassKg)-total)<1e-12,"Output mass is removed from the workpiece");
        Check(output.Place==PossessionPlace.Ground&&output.NaturalId==0&&output.WidthM>0&&output.HeightM>0&&output.DepthM>0,"Output has physical dimensions and no duplicate natural identity");
        var old=JsonNode.Parse(WorldJson.Write(new PlaySettings()));old.AsObject().Remove("ItemCraftingTimeMultiplier");var legacy=WorldJson.Read<PlaySettings>(old.ToJsonString());legacy.Validate();Check(legacy.ItemCraftingTimeMultiplier==1,"Old settings default to crafting times one");
        foreach(double bad in new[]{0d,-1,21,double.NaN,double.PositiveInfinity}){var invalid=settings.Copy();invalid.ItemCraftingTimeMultiplier=bad;Reject(invalid.Validate,"Reject invalid crafting multiplier "+bad);}
        foreach(int fps in new[]{4,20,60,144})
        {
            var a=Fixture();var b=Fixture();var ja=StoneCrafting.Start(a,"work","tool",out _);var jb=StoneCrafting.Start(b,"work","tool",out _);double elapsed=0;Possession fa=null,fb=null;
            while(fa==null){fa=StoneCrafting.Advance(a,ja,1.0/fps,settings);elapsed+=1.0/fps;}
            double elapsed2=0;while(fb==null){fb=StoneCrafting.Advance(b,jb,1.0/fps,slower);elapsed2+=1.0/fps;}
            Check(Math.Abs(elapsed-originalSeconds)<=1.0/fps+.001,"Duration stable at "+fps+" fps");
            Check(Math.Abs(elapsed2-originalSeconds*2)<=1.0/fps+.001,"Times two duration stable at "+fps+" fps");
            a.Validate();b.Validate();
        }
        var multiple=Fixture();multiple.Items.Add(Rock("other",4,.6));var first=StoneCrafting.Start(multiple,"work","tool",out _);StoneCrafting.Advance(multiple,first,.5,settings);var second=StoneCrafting.Start(multiple,"other","tool",out _);
        Check(!first.Running&&second.Running&&multiple.Crafts.Count==2&&first.WorkSeconds>0,"One worker can keep multiple paused projects without concurrent work");multiple.Validate();
        var corrupt=multiple.Copy();corrupt.Crafts[0].WorkSeconds=double.NaN;Reject(corrupt.Validate,"Reject nonfinite saved progress");
        corrupt=multiple.Copy();corrupt.Crafts[0].ToolId="missing";Reject(corrupt.Validate,"Reject dangling work input");
        Console.WriteLine("PASS "+count+" stone crafting, timing, roles and persistence checks");
    }
}

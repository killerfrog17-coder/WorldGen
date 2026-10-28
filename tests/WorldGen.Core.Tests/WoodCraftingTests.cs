using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class WoodCraftingTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        var settings=new PlaySettings();
        PlayerInventory Fixture(double length=1.9,int variant=0)
        {
            var p=WoodCrafting.Branch(new ScatterInstance{Id=771,Kind=ScatterKind.Branch,Variant=variant,Position=new V(1,0,0),WidthM=length,HeightM=.03});
            var s=new PlayerInventory();s.Items.Add(p);s.CollectedNatural.Add(p.NaturalId);
            s.Items.Add(new Possession{Id="edge",Kind=PossessionKind.StoneFlake,Place=PossessionPlace.LeftHand,WidthM=.1,HeightM=.01,DepthM=.06,MassKg=.08,EdgeSharpness=.9,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)});return s;
        }
        Possession Finish(PlayerInventory s,ItemCraftAction action)
        {var job=WoodCrafting.Start(s,s.Items[0].Id,"edge",action,out var error);Check(job!=null&&error=="","Eligible work starts: "+action);var result=StoneCrafting.Advance(s,job,10000,settings);s.Validate();return result;}
        foreach(ToolStoneMaterial material in Enum.GetValues(typeof(ToolStoneMaterial)))
        {double quality=ToolStone.Quality(ToolStone.Encode(material,0));Check(Enumerable.Range(0,3).All(shape=>ToolStone.Quality(ToolStone.Encode(material,shape))==quality),"Shape never determines suitability: "+material);}
        Check(ToolStone.Quality(11)==0,"Old limestone shape is not silently reclassified as chert");
        Check(ToolStone.Quality(ToolStone.Encode(ToolStoneMaterial.Chert,2))>ToolStone.Quality(ToolStone.Encode(ToolStoneMaterial.CoarseBasalt,1)),"Usable materials can differ in quality");
        var ground=new GroundProfile{Bedrock=RockFamily.Limestone};V point=new V(1,0,0);
        Check(ToolStone.Source(ground,point,5,600000)==ToolStone.Source(ground,point,5,600000),"Geological source classification is reproducible");
        var state=Fixture();var wood=state.Items[0];var id=wood.Id;double mass=wood.MassKg;
        Check(wood.HeightM==1.9&&wood.WidthM==.03&&wood.CellsHigh==19,"Natural dimensions become physical length and packing");
        Check(WoodCrafting.Reason(wood,ItemCraftAction.WoodenSpear).Length>0,"Untrimmed branch cannot skip preparation");
        var job=WoodCrafting.Start(state,id,"edge",ItemCraftAction.TrimBranch,out _);
        Check(StoneCrafting.Advance(state,job,1,settings)==null&&job.Fraction>0,"Timed woodwork shares crafting progression");
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(state));saved.Validate();Check(saved.Crafts[0].Action==ItemCraftAction.TrimBranch&&saved.Crafts[0].WorkSeconds==job.WorkSeconds,"Action, physical traits and partial work survive saves");
        var copy=state.Copy();copy.Items[0].WoodStraightness=.1;Check(wood.WoodStraightness==1,"Copied traits are independent");
        job.Running=false;double work=job.WorkSeconds;Check(StoneCrafting.Advance(state,job,100,settings)==null&&job.WorkSeconds==work,"Pause does not process wood");
        var resumed=WoodCrafting.Start(state,id,"edge",ItemCraftAction.TrimBranch,out _);Check(ReferenceEquals(job,resumed),"Resume retains same job");
        StoneCrafting.Advance(state,job,10000,settings);state.Validate();
        Check(wood.Kind==PossessionKind.TrimmedPole&&wood.Id==id&&state.Items.Count==2&&wood.MassKg<mass,"Trimming transforms the same piece, producing no duplicated loot");
        Check(WoodCrafting.Reason(wood,ItemCraftAction.WoodenSpear)==""&&!wood.BarkRemoved,"Bark removal is optional for a wooden point");
        var spear=Finish(state,ItemCraftAction.WoodenSpear);Check(spear.Kind==PossessionKind.WoodenSpear&&spear.HeightM==1.9,"Pointed spear preserves shaft dimensions");
        Check(state.Find("edge").EdgeSharpness<.9,"Woodwork wears its actual cutting flake");
        var shortState=Fixture(.9);Finish(shortState,ItemCraftAction.TrimBranch);Check(WoodCrafting.Reason(shortState.Items[0],ItemCraftAction.WoodenSpear).Length>0,"Short pole cannot become long spear");
        Finish(shortState,ItemCraftAction.StripBark);Check(shortState.Items[0].BarkRemoved,"Stripping persists bare-wood state");
        Finish(shortState,ItemCraftAction.DiggingStick);Check(shortState.Items[0].Kind==PossessionKind.DiggingStick,"Short sound pole becomes digging stick");
        var crooked=Fixture(1.9,3);Finish(crooked,ItemCraftAction.TrimBranch);Check(WoodCrafting.Reason(crooked.Items[0],ItemCraftAction.WoodenSpear).Length>0,"Crooked shaft cannot become straight spear");
        var rotten=Fixture(1.9,4);Finish(rotten,ItemCraftAction.TrimBranch);Check(WoodCrafting.Reason(rotten.Items[0],ItemCraftAction.WoodenSpear).Length>0,"Weathered piece cannot produce durable spear");
        state.Find("edge").EdgeSharpness=.1;Check(!WoodCrafting.Cutter(state.Find("edge")),"Worn edge stops being an adequate cutting tool");
        Console.WriteLine("PASS "+count+" material, woodworking and persistence checks");
    }
}

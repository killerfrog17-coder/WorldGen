using System;
using System.Linq;
using System.Text.Json.Nodes;
using WorldGen.Core;
using WorldGen.Serialization;

static class PlayerInventoryTests
{
    public static void Run()
    {
        int count=0;void Check(bool p,string text){count++;if(!p)throw new Exception(text);}
        void Reject(Action a,string text){bool rejected=false;try{a();}catch(ArgumentException){rejected=true;}Check(rejected,text);}
        Possession Rock(string id,bool large=false,int w=1,int h=1)=>new Possession{Id=id,Kind=large?PossessionKind.LargeRock:PossessionKind.SmallRock,Place=PossessionPlace.Ground,Position=new V(1,0,0),CellsWide=w,CellsHigh=h,MassKg=large?8:.4};
        var s=PlayerInventory.Starter();s.Validate();Check(s.Items.Count==1&&s.Bag!=null,"Only test bag starts equipped");
        s.Items.Add(Rock("a"));s.Items.Add(Rock("b"));s.Items.Add(Rock("large",true,3,2));
        Check(s.Hold("a",out _)&&s.AtHand(PossessionPlace.RightHand)?.Id=="a","First pickup uses active right hand");
        Check(s.Hold("b",out _)&&s.AtHand(PossessionPlace.LeftHand)?.Id=="b","Second pickup uses free left hand");
        Check(!s.Hold("large",out _)&&s.Find("large").Place==PossessionPlace.Ground,"Two handed pickup atomic when hands full");
        Check(s.Stow("a",out _)&&s.Stow("b",out _),"Stow finds distinct free space");
        Check(s.Hold("large",out _)&&s.AtHand(PossessionPlace.RightHand)==s.AtHand(PossessionPlace.LeftHand),"One large item reserves both hands");
        Check(!s.Hold("a",out _),"Both-hand hold excludes another one-hand item");
        Check(s.Stow("large",out _),"Large item fits an appropriate bag");s.Validate();
        var a=s.Find("a");int oldX=a.X,oldY=a.Y;
        var big=s.Find("large");Check(!s.Move("a",PossessionPlace.Container,s.Bag.Id,big.X,big.Y,false,out _)&&a.X==oldX&&a.Y==oldY,"Overlapping transfer preserves prior placement");
        Check(!s.Move("a",PossessionPlace.Container,s.Bag.Id,6,0,false,out _),"Reject outside container");
        Check(!s.Move(s.Bag.Id,PossessionPlace.Container,s.Bag.Id,0,0,false,out _),"Reject bag cycle");
        double loaded=s.CarriedMassKg;var bag=s.Bag;
        Check(s.Move(bag.Id,PossessionPlace.Ground,"",0,0,false,out _),"Loaded bag can be put down");bag.Position=new V(1,0,0);s.Validate();
        Check(s.CarriedMassKg==0&&s.Find("a").ContainerId==bag.Id,"Dropped pack retains contents, removes their load");
        Check(!s.Stow("a",out _),"No invisible main inventory without worn bag");
        Check(s.Move(bag.Id,PossessionPlace.Back,"",0,0,false,out _)&&Math.Abs(s.CarriedMassKg-loaded)<1e-10,"Re-wearing restores actual carried mass");
        var slim=PlayerInventory.Starter();slim.Items.Add(Rock("long",false,7,1));
        Check(slim.Stow("long",out _)&&slim.Find("long").Rotated,"Packing tries rotation for long item");
        slim.Items.Add(Rock("too-long",false,8,1));Check(!slim.Stow("too-long",out _)&&slim.Find("too-long").Place==PossessionPlace.Ground,"Physical length cannot fit despite free area");
        var full=PlayerInventory.Starter();
        for(int i=0;i<42;i++){full.Items.Add(Rock("r"+i));Check(full.Stow("r"+i,out _),"Independent item packs "+i);}
        full.Items.Add(Rock("overflow"));Check(!full.Stow("overflow",out _),"Capacity follows occupied cells");full.Validate();
        Check(full.LoadSpeedScale<1&&full.CarriedMassKg>16,"Real combined mass affects load");
        var copied=full.Copy();copied.Find("r0").MassKg=99;Check(full.Find("r0").MassKg==.4,"Copy isolates item state");
        full.CollectedNatural.Add(1234567890123456789UL);full.PlayerName="Test person";
        var session=new PlaySession{Inventory=full};var restored=WorldJson.Read<PlaySession>(WorldJson.Write(session));restored.Validate();
        Check(restored.Inventory.Items.Count==44&&restored.Inventory.CollectedNatural.Single()==1234567890123456789UL&&restored.Inventory.PlayerName=="Test person","Full packing and ulong depletion save round trip");
        var old=JsonNode.Parse(WorldJson.Write(new PlaySession()));old.AsObject().Remove("Inventory");var legacy=WorldJson.Read<PlaySession>(old.ToJsonString());legacy.Validate();Check(legacy.Inventory.Items.Count==1,"Old saves get exactly one test bag");
        var corrupt=PlayerInventory.Starter();corrupt.Items.Add(null);Reject(corrupt.Validate,"Reject null items");
        corrupt=full.Copy();corrupt.Items.Add(corrupt.Items[0].Copy());Reject(corrupt.Validate,"Reject duplicate identities");
        corrupt=full.Copy();corrupt.Find("r0").X=-1;Reject(corrupt.Validate,"Reject corrupt packing coordinates");
        corrupt=PlayerInventory.Starter();var missing=Rock("missing");missing.NaturalId=999;corrupt.Items.Add(missing);Reject(corrupt.Validate,"Reject missing depletion marker that would duplicate a rock");
        Console.WriteLine("PASS "+count+" inventory packing/ownership/save checks");
    }
}

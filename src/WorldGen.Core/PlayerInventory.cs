using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum PossessionPlace { Ground,RightHand,LeftHand,Container,Back }
    public enum PossessionKind { SmallRock,LargeRock,TestBag }
    [Serializable]
    public sealed class Possession
    {
        public string Id="",ContainerId="";
        public PossessionKind Kind;
        public PossessionPlace Place;
        public int X,Y,CellsWide=1,CellsHigh=1,Variant;
        public bool Rotated;
        public ulong NaturalId;
        public double WidthM=.1,HeightM=.1,DepthM=.1,MassKg=.5;
        public V Position,Tint=new V(.6,.6,.6);
        public string Name=>Kind==PossessionKind.SmallRock?"Small rock":Kind==PossessionKind.LargeRock?"Large rock":"Testing bag";
        public int Hands=>Kind==PossessionKind.LargeRock?2:1;
        public int PackedWidth=>Rotated?CellsHigh:CellsWide;
        public int PackedHeight=>Rotated?CellsWide:CellsHigh;
        public int ContainerWidth=>Kind==PossessionKind.TestBag?6:0;
        public int ContainerHeight=>Kind==PossessionKind.TestBag?7:0;
        public Possession Copy()=>(Possession)MemberwiseClone();
    }
    /// <summary>Physical ownership, independently of UI/terrain LOD or historical world state.</summary>
    [Serializable]
    public sealed class PlayerInventory
    {
        public string PlayerName="Survivor";
        public List<Possession> Items=new List<Possession>();
        public List<ulong> CollectedNatural=new List<ulong>();
        public PossessionPlace ActiveHand=PossessionPlace.RightHand;
        public static PlayerInventory Starter()
        {
            var s=new PlayerInventory();s.Items.Add(new Possession{Id="testing-bag",Kind=PossessionKind.TestBag,Place=PossessionPlace.Back,CellsWide=6,CellsHigh=7,WidthM=.6,HeightM=.7,DepthM=.22,MassKg=.6});return s;
        }
        public PlayerInventory Copy()=>new PlayerInventory{PlayerName=PlayerName,ActiveHand=ActiveHand,Items=Items.Select(i=>i.Copy()).ToList(),CollectedNatural=new List<ulong>(CollectedNatural)};
        public Possession Find(string id)=>Items.FirstOrDefault(i=>i.Id==id);
        public Possession AtHand(PossessionPlace hand)=>Items.FirstOrDefault(i=>i.Place==hand||i.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand));
        public Possession Bag=>Items.FirstOrDefault(i=>i.Place==PossessionPlace.Back);
        public bool Carried(Possession item)
        {
            var seen=new HashSet<string>();
            while(item!=null&&seen.Add(item.Id))
            {if(item.Place==PossessionPlace.Ground)return false;if(item.Place!=PossessionPlace.Container)return true;item=Find(item.ContainerId);}
            return false;
        }
        public double CarriedMassKg=>Items.Where(Carried).Sum(i=>i.MassKg);
        // Bags organize the load, not its actual mass. Smooth effort, no per-item count cap.
        public double LoadSpeedScale=>1/(1+Math.Pow(Math.Max(0,CarriedMassKg-8)/25,1.5));
        public double LoadEffort=>1+CarriedMassKg/30;
        public bool CanMove(string id,PossessionPlace place,string container,int x,int y,bool rotated,out string reason)
        {
            reason="";var item=Find(id);if(item==null){reason="Item is no longer available.";return false;}
            if(!Enum.IsDefined(typeof(PossessionPlace),place)){reason="Invalid destination.";return false;}
            if(place==PossessionPlace.Back)
            {if(item.Kind!=PossessionKind.TestBag){reason="This item needs suitable carrying equipment.";return false;}if(Bag!=null&&Bag!=item){reason="Your back is occupied.";return false;}}
            if(place==PossessionPlace.RightHand||place==PossessionPlace.LeftHand)
            {
                if(Items.Any(i=>i!=item&&(i.Place==place||i.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand)||item.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand))))
                {reason=item.Hands==2?"Both hands must be free.":"That hand is occupied.";return false;}
            }
            if(place==PossessionPlace.Container)
            {
                var bag=Find(container);if(bag==null||bag.ContainerWidth==0){reason="No container there.";return false;}
                var ancestor=bag;var seen=new HashSet<string>();
                while(ancestor!=null&&seen.Add(ancestor.Id)){if(ancestor==item){reason="A bag cannot contain itself.";return false;}ancestor=ancestor.Place==PossessionPlace.Container?Find(ancestor.ContainerId):null;}
                int w=rotated?item.CellsHigh:item.CellsWide,h=rotated?item.CellsWide:item.CellsHigh;
                if(x<0||y<0||x+w>bag.ContainerWidth||y+h>bag.ContainerHeight){reason="Item does not fit here.";return false;}
                if(Items.Any(i=>i!=item&&i.Place==PossessionPlace.Container&&i.ContainerId==container&&x<i.X+i.PackedWidth&&x+w>i.X&&y<i.Y+i.PackedHeight&&y+h>i.Y))
                {reason="That space is occupied.";return false;}
            }
            return true;
        }
        public bool Move(string id,PossessionPlace place,string container,int x,int y,bool rotated,out string reason)
        {
            if(!CanMove(id,place,container,x,y,rotated,out reason))return false;
            var item=Find(id);item.Place=place;item.ContainerId=place==PossessionPlace.Container?container:"";item.X=x;item.Y=y;item.Rotated=rotated;return true;
        }
        public bool Stow(string id,out string reason)
        {
            reason="No worn bag with room for this item.";var bag=Bag;var item=Find(id);if(bag==null||item==null)return false;
            for(int r=0;r<2;r++)for(int y=0;y<bag.ContainerHeight;y++)for(int x=0;x<bag.ContainerWidth;x++)
                if(CanMove(id,PossessionPlace.Container,bag.Id,x,y,r==1,out _))return Move(id,PossessionPlace.Container,bag.Id,x,y,r==1,out reason);
            return false;
        }
        public bool Hold(string id,out string reason)
        {
            if(Move(id,ActiveHand,"",0,0,false,out reason))return true;
            var other=ActiveHand==PossessionPlace.RightHand?PossessionPlace.LeftHand:PossessionPlace.RightHand;
            if(!Move(id,other,"",0,0,false,out reason))return false;ActiveHand=other;return true;
        }
        public void Validate()
        {
            if(Items==null||Items.Any(i=>i==null)||CollectedNatural==null||PlayerName==null||PlayerName.Length>48||ActiveHand!=PossessionPlace.LeftHand&&ActiveHand!=PossessionPlace.RightHand)throw new ArgumentException("Invalid inventory state.");
            var ids=new HashSet<string>();var natural=new HashSet<ulong>();
            foreach(var item in Items)
            {
                bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
                if(item==null||string.IsNullOrWhiteSpace(item.Id)||!ids.Add(item.Id)||!Enum.IsDefined(typeof(PossessionKind),item.Kind)||!Enum.IsDefined(typeof(PossessionPlace),item.Place)||item.CellsWide<1||item.CellsHigh<1||item.CellsWide>10000||item.CellsHigh>10000||!Finite(item.MassKg)||item.MassKg<=0||!Finite(item.WidthM)||item.WidthM<=0||!Finite(item.HeightM)||item.HeightM<=0||!Finite(item.DepthM)||item.DepthM<=0||!Finite(item.Tint.X)||!Finite(item.Tint.Y)||!Finite(item.Tint.Z)||item.Variant<0||item.Variant>1000)throw new ArgumentException("Invalid inventory item.");
                if(item.NaturalId!=0&&!natural.Add(item.NaturalId))throw new ArgumentException("Duplicate natural item.");
                if(item.NaturalId!=0&&!CollectedNatural.Contains(item.NaturalId))throw new ArgumentException("Missing collected ground identity.");
                if(item.Place==PossessionPlace.Ground&&(!Finite(item.Position.Length)||Math.Abs(item.Position.Length-1)>1e-5))throw new ArgumentException("Invalid dropped item position.");
                if(!CanMove(item.Id,item.Place,item.ContainerId,item.X,item.Y,item.Rotated,out string reason))throw new ArgumentException(reason);
            }
            if(new HashSet<ulong>(CollectedNatural).Count!=CollectedNatural.Count)throw new ArgumentException("Duplicate collected identities.");
        }
    }
}

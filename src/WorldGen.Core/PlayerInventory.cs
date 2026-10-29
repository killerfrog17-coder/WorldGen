using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum PossessionPlace { Ground,RightHand,LeftHand,Container,Back,Shoulder,Waist,SlungBack,Corpse }
    public enum PossessionKind { SmallRock,LargeRock,TestBag,StoneFlake,Branch,TrimmedPole,DiggingStick,WoodenSpear,RawFibre,PreparedFibre,Cordage,Thread,BarkStrips,ReedBundle,RawHide,ScrapedHide,WorkedHide,RawhideStrips,Sinew,SinewCord,Feather,Bone,Antler,StoneBlade,StoneScraper,StoneChopper,Handaxe,StonePoint,Arrowhead,StoneBorer,BoneAwl,BoneNeedle,BonePoint,BarbedPoint,FishingGorge,Fishhook,AntlerRetoucher,WoodHandle,NotchedShaft,LooseStoneSpear,StoneTippedSpear,BoneTippedSpear,Harpoon,HaftedKnife,StoneAxe,StoneAdze,ArrowShaft,UnfletchedArrow,Arrow,BowStave,Bow,SpearThrower,DartShaft,SpearDart,Sling,FishingLine,HandLine,FibreNet,HidePouch,BarkContainer,WovenBasket,HideSatchel,HidePack,CarryFrame,Quiver,FibreBelt,HideBelt,NeedleCase,KnifeSheath,BarkSheet,WeavingStrips,Berries,Fruit,Nuts,Mushroom,EdibleRoot,RawMeat,RawFish,AnimalFat,DryGrass,LeafLitter,TinderBundle,KindlingBundle,HandDrillSet }
    [Serializable] public sealed class NaturalToolRecord
    {
        public string Id="";public double Work;public int Bark,Branches,Digging;
        [System.Runtime.Serialization.OptionalField] public int HarvestYear,Harvests;
        public NaturalToolRecord Copy()=>(NaturalToolRecord)MemberwiseClone();
    }
    [Serializable]
    public sealed class Possession
    {
        public string Id="",ContainerId="";
        [System.Runtime.Serialization.OptionalField] public string BoundContainerId;
        public PossessionKind Kind;
        public PossessionPlace Place;
        public int X,Y,CellsWide=1,CellsHigh=1,Variant;
        public bool Rotated;
        public ulong NaturalId;
        public double WidthM=.1,HeightM=.1,DepthM=.1,MassKg=.5;
        public V Position,Tint=new V(.6,.6,.6);
        [System.Runtime.Serialization.OptionalField] public double GroundOffsetM;
        [System.Runtime.Serialization.OptionalField] public bool GroundMoving;
        [System.Runtime.Serialization.OptionalField] public V GroundRotation;
        [System.Runtime.Serialization.OptionalField] public V GroundVelocity,GroundSpin;
        [System.Runtime.Serialization.OptionalField] public bool InFlight;
        [System.Runtime.Serialization.OptionalField] public ulong AttachedAnimal;
        [System.Runtime.Serialization.OptionalField] public int AttachedHuman,AttachedBone;
        [System.Runtime.Serialization.OptionalField] public string AttachedBoneName;
        [System.Runtime.Serialization.OptionalField] public V AttachedLocalPoint,AttachedDirection;
        [System.Runtime.Serialization.OptionalField] public int FlakeRemovals;
        [System.Runtime.Serialization.OptionalField] public double WoodStraightness,WoodSoundness;
        [System.Runtime.Serialization.OptionalField] public bool BarkRemoved;
        [System.Runtime.Serialization.OptionalField] public double EdgeSharpness;
        [System.Runtime.Serialization.OptionalField] public double UseDamage;
        [System.Runtime.Serialization.OptionalField] public string AppearanceSpecies;
        [System.Runtime.Serialization.OptionalField] public int AppearanceCoat,AppearanceSeed;
        [System.Runtime.Serialization.OptionalField] public ulong SourceAnimalId;
        [System.Runtime.Serialization.OptionalField] public List<MaterialOrigin> MaterialOrigins;
        [System.Runtime.Serialization.OptionalField] public ulong CorpseOwner;
        [System.Runtime.Serialization.OptionalField] public string FoodSpecies;
        [System.Runtime.Serialization.OptionalField] public double FoodKcalPerKg,FoodWaterPerKg,FoodToxicity,FoodDelayHours;
        public string Name=>PortableTechnology.Name(this)??(Kind==PossessionKind.Branch?"Branch":Kind==PossessionKind.TrimmedPole?"Trimmed pole":Kind==PossessionKind.DiggingStick?"Digging stick":Kind==PossessionKind.WoodenSpear?"Wooden spear":Kind==PossessionKind.SmallRock?"Small rock":Kind==PossessionKind.LargeRock?"Large rock":Kind==PossessionKind.StoneFlake?"Stone flake":"Testing bag");
        public int Hands=>Kind==PossessionKind.LargeRock?2:PortableTechnology.Item(Kind)?.Hands??1;
        public int PackedWidth=>Rotated?CellsHigh:CellsWide;
        public int PackedHeight=>Rotated?CellsWide:CellsHigh;
        public int ContainerWidth=>Kind==PossessionKind.TestBag?6:PortableTechnology.Item(Kind)?.StorageWidth??0;
        public int ContainerHeight=>Kind==PossessionKind.TestBag?7:PortableTechnology.Item(Kind)?.StorageHeight??0;
        public Possession Copy(){var p=(Possession)MemberwiseClone();p.MaterialOrigins=MaterialOrigins?.Select(x=>x.Copy()).ToList();return p;}
    }
    /// <summary>Physical ownership, independently of UI/terrain LOD or historical world state.</summary>
    [Serializable]
    public sealed class PlayerInventory
    {
        public string PlayerName="Survivor";
        public List<Possession> Items=new List<Possession>();
        public List<ulong> CollectedNatural=new List<ulong>();
        [System.Runtime.Serialization.OptionalField] public List<ItemCraftJob> Crafts=new List<ItemCraftJob>();
        [System.Runtime.Serialization.OptionalField] public List<NaturalToolRecord> ToolResources=new List<NaturalToolRecord>();
        [System.Runtime.Serialization.OptionalField] public CraftEquipmentState ToolWorkEquipment;
        public PossessionPlace ActiveHand=PossessionPlace.RightHand;
        public PlayerInventory Copy()=>new PlayerInventory{PlayerName=PlayerName,ActiveHand=ActiveHand,Items=Items.Select(i=>i.Copy()).ToList(),CollectedNatural=new List<ulong>(CollectedNatural),Crafts=(Crafts??new List<ItemCraftJob>()).Select(j=>j.Copy()).ToList(),ToolResources=(ToolResources??new List<NaturalToolRecord>()).Select(r=>r.Copy()).ToList(),ToolWorkEquipment=ToolWorkEquipment?.Copy()};
        public Possession Find(string id)=>Items.FirstOrDefault(i=>i.Id==id);
        public Possession AtHand(PossessionPlace hand)=>Items.FirstOrDefault(i=>i.Place==hand||i.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand));
        public IEnumerable<Possession> WornContainers=>Items.Where(i=>PortableTechnology.Worn(i.Place)&&i.ContainerWidth>0).OrderBy(i=>(int)i.Place);
        public Possession Bag=>Items.FirstOrDefault(i=>i.Place==PossessionPlace.Back);
        public bool Carried(Possession item)
        {
            var seen=new HashSet<string>();
            while(item!=null&&seen.Add(item.Id))
            {if(item.Place==PossessionPlace.Ground||item.Place==PossessionPlace.Corpse)return false;if(item.Place!=PossessionPlace.Container)return true;item=Find(item.ContainerId);}
            return false;
        }
        public bool InWornEquipment(Possession item)
        {
            var seen=new HashSet<string>();
            while(item!=null&&seen.Add(item.Id))
            {if(item.Place!=PossessionPlace.Container)return PortableTechnology.Worn(item.Place);item=Find(item.ContainerId);}
            return false;
        }
        public IEnumerable<Possession> StorageCarriers(Possession item)=>Items.Where(p=>p!=item&&p.ContainerWidth>0&&InWornEquipment(p))
            .OrderBy(p=>p.Id==item.BoundContainerId?0:PortableTechnology.Item(p.Kind)?.Form==PortableForm.Case?1:PortableTechnology.Belt(p)?2:3);
        public double CarriedMassKg=>Items.Where(Carried).Sum(i=>i.MassKg);
        // Bags organize the load, not its actual mass. Smooth effort, no per-item count cap.
        public double LoadSpeedScale=>1/(1+Math.Pow(Math.Max(0,CarriedMassKg-8)/25,1.5));
        public double LoadEffort=>1+CarriedMassKg/30;
        public bool CanMove(string id,PossessionPlace place,string container,int x,int y,bool rotated,out string reason)
        {
            reason="";var item=Find(id);if(item==null){reason="Item is no longer available.";return false;}
            if(!Enum.IsDefined(typeof(PossessionPlace),place)){reason="Invalid destination.";return false;}
            if(place==PossessionPlace.Corpse&&(item.Place!=PossessionPlace.Corpse||item.CorpseOwner==0)){reason="Items cannot be put into a corpse.";return false;}
            if(PortableTechnology.Worn(place))
            {if(PortableTechnology.Wear(item)!=place){reason="This item does not attach there.";return false;}if(Items.Any(p=>p!=item&&p.Place==place)){reason="That attachment is occupied.";return false;}}
            if(place==PossessionPlace.RightHand||place==PossessionPlace.LeftHand)
            {
                if(Items.Any(i=>i!=item&&(i.Place==place||i.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand)||item.Hands==2&&(i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.RightHand))))
                {reason=item.Hands==2?"Both hands must be free.":"That hand is occupied.";return false;}
            }
            if(place==PossessionPlace.Container)
            {
                var bag=Find(container);if(bag==null||bag.ContainerWidth==0){reason="No container there.";return false;}
                if(!PortableTechnology.Holds(bag,item)){reason="That carrier is not suitable for this item.";return false;}
                if(item.ContainerWidth>0&&!PortableTechnology.Belt(bag)&&Items.Any(p=>p.ContainerId==item.Id)){reason="Empty the container before packing it inside another.";return false;}
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
            var item=Find(id);
            if(place==PossessionPlace.Container){var carrier=Find(container);if(PortableTechnology.Belt(carrier)||PortableTechnology.Item(carrier.Kind)?.Form==PortableForm.Case||carrier.Kind==PossessionKind.Quiver)item.BoundContainerId=container;}
            if(place!=PossessionPlace.Ground){item.InFlight=false;item.AttachedAnimal=0;item.AttachedHuman=0;}
            item.Place=place;item.ContainerId=place==PossessionPlace.Container?container:"";item.X=x;item.Y=y;item.Rotated=rotated;return true;
        }
        public bool Stow(string id,out string reason)
        {
            reason="No worn carrier with room for this item.";var item=Find(id);if(item==null)return false;
            foreach(var bag in StorageCarriers(item).ToArray())for(int r=0;r<2;r++)for(int y=0;y<bag.ContainerHeight;y++)for(int x=0;x<bag.ContainerWidth;x++)
                if(CanMove(id,PossessionPlace.Container,bag.Id,x,y,r==1,out _))return Move(id,PossessionPlace.Container,bag.Id,x,y,r==1,out reason);
            return false;
        }

        public void SwapHands()
        {
            if(AtHand(PossessionPlace.RightHand)?.Hands==2)return;
            foreach(var item in Items)
                if(item.Place==PossessionPlace.RightHand)item.Place=PossessionPlace.LeftHand;
                else if(item.Place==PossessionPlace.LeftHand)item.Place=PossessionPlace.RightHand;
        }
        public Possession QuickHeld=>AtHand(PossessionPlace.RightHand)??AtHand(PossessionPlace.LeftHand);
        public bool Hold(string id,out string reason)
        {
            if(Move(id,PossessionPlace.RightHand,"",0,0,false,out reason))return true;
            return Move(id,PossessionPlace.LeftHand,"",0,0,false,out reason);
        }
        public void Validate()
        {
            if(Items==null||Items.Any(i=>i==null)||CollectedNatural==null||PlayerName==null||PlayerName.Length>48||ActiveHand!=PossessionPlace.LeftHand&&ActiveHand!=PossessionPlace.RightHand)throw new ArgumentException("Invalid inventory state.");
            var ids=new HashSet<string>();var natural=new HashSet<ulong>();
            foreach(var item in Items)
            {
                bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
                if(item==null||string.IsNullOrWhiteSpace(item.Id)||!ids.Add(item.Id)||!Enum.IsDefined(typeof(PossessionKind),item.Kind)||!Enum.IsDefined(typeof(PossessionPlace),item.Place)||item.CellsWide<1||item.CellsHigh<1||item.CellsWide>10000||item.CellsHigh>10000||!Finite(item.MassKg)||item.MassKg<=0||!Finite(item.WidthM)||item.WidthM<=0||!Finite(item.HeightM)||item.HeightM<=0||!Finite(item.DepthM)||item.DepthM<=0||!Finite(item.Tint.X)||!Finite(item.Tint.Y)||!Finite(item.Tint.Z)||item.Variant<0||item.Variant>1000)throw new ArgumentException("Invalid inventory item.");
                if(item.AppearanceSpecies!=null&&(item.AppearanceSpecies.Length>80||item.AppearanceSpecies.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'))||item.AppearanceCoat<0||item.AppearanceCoat>4095||item.MaterialOrigins!=null&&item.MaterialOrigins.Any(o=>o==null||o.Species==null||o.Species.Length>80||o.Coat<0||o.Coat>4095))throw new ArgumentException("Invalid material provenance.");
                if(!Finite(item.UseDamage)||item.UseDamage<0||item.UseDamage>1)throw new ArgumentException("Invalid item wear.");
                Foraging.ValidateFood(item);
                if(!Finite(item.WoodStraightness)||item.WoodStraightness<0||item.WoodStraightness>1||!Finite(item.WoodSoundness)||item.WoodSoundness<0||item.WoodSoundness>1)throw new ArgumentException("Invalid wood traits.");
                if(!Finite(item.GroundVelocity.Length)||!Finite(item.GroundSpin.Length)||!Finite(item.AttachedLocalPoint.Length)||!Finite(item.AttachedDirection.Length)||item.AttachedBone<0||item.AttachedBone>=128||item.AttachedHuman<0)throw new ArgumentException("Invalid projectile pose.");
                if(!Finite(item.GroundOffsetM)||Math.Abs(item.GroundOffsetM)>1e7||!Finite(item.GroundRotation.X)||!Finite(item.GroundRotation.Y)||!Finite(item.GroundRotation.Z))throw new ArgumentException("Invalid dropped item pose.");
                if(item.NaturalId!=0&&!natural.Add(item.NaturalId))throw new ArgumentException("Duplicate natural item.");
                if(item.NaturalId!=0&&!CollectedNatural.Contains(item.NaturalId))throw new ArgumentException("Missing collected ground identity.");
                if(item.Place==PossessionPlace.Ground&&(!Finite(item.Position.Length)||Math.Abs(item.Position.Length-1)>1e-5))throw new ArgumentException("Invalid dropped item position.");
                if(!CanMove(item.Id,item.Place,item.ContainerId,item.X,item.Y,item.Rotated,out string reason))throw new ArgumentException(reason);
            }
            if(new HashSet<ulong>(CollectedNatural).Count!=CollectedNatural.Count)throw new ArgumentException("Duplicate collected identities.");
            Crafts=Crafts??new List<ItemCraftJob>();StoneCrafting.Validate(this);
            ToolResources=ToolResources??new List<NaturalToolRecord>();if(ToolResources.Any(r=>r==null||string.IsNullOrEmpty(r.Id)||!ItemUseRules.Finite(r.Work)||r.Work<0||r.Bark<0||r.Branches<0||r.Digging<0)||ToolResources.Select(r=>r.Id).Distinct().Count()!=ToolResources.Count)throw new ArgumentException("Invalid natural tool-work ledger.");
            ToolWorkEquipment?.Validate();
        }
    }
}

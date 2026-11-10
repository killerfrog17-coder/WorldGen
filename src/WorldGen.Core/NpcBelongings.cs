using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public static class NpcOwnership
    {
        public static bool Allowed(Possession item,long person,long group,long household,double sharing=0)
        {
            if(item.OwnerPerson==0&&item.OwnerGroup==0)return true;
            if(person>0&&item.OwnerPerson==person)return true;
            if(group<=0||item.OwnerGroup!=group)return false;
            if(item.OwnerPerson==0&&(item.OwnerHousehold==0||item.OwnerHousehold==household))return true;
            return sharing>.65&&Foraging.Food(item);
        }
        public static void Assign(Possession item,ForagerPersonSample person,long group)
        {item.OwnerPerson=person.Id;item.OwnerGroup=group;item.OwnerHousehold=Math.Max(0,person.Household);item.OwnerName=person.Name;}
        public static bool BedAllowed(CampObject bed,long group,long household)
            =>bed.NpcGroup==0||group>0&&bed.NpcGroup==group&&(bed.NpcHousehold==0||bed.NpcHousehold==household);
    }
    // Initial conditions are generated once. Tasks rearrange these same goods.
    public static class NpcBelongings
    {
        public static void PrepareInitial(PlayerInventory stock,string role,V home)
        {
            foreach(var item in ForTask(stock,role))stock.Hold(item.Id,out _);
            foreach(var item in stock.Items.Where(p=>p.Place==PossessionPlace.Ground).OrderBy(p=>p.Kind==PossessionKind.StoneFlake?0:p.Kind==PossessionKind.HandDrillSet?1:2).ToArray())
            {
                if(Pack(stock,item))continue;
                if((item.Kind==PossessionKind.StoneFlake||item.Kind==PossessionKind.HandDrillSet)&&stock.Hold(item.Id,out _))continue;
                item.Position=home;
            }
        }
        public static Possession[] TransferTree(PlayerInventory source,PlayerInventory destination,Possession root)
        {
            if(source==destination||!source.Items.Contains(root))throw new ArgumentException("Missing source possession.");
            var ids=new HashSet<string>{root.Id};var items=new List<Possession>{root};
            for(int i=0;i<items.Count;i++)foreach(var child in source.Items)
                if(child.Place==PossessionPlace.Container&&child.ContainerId==items[i].Id&&ids.Add(child.Id))items.Add(child);
            if(destination.Items.Any(p=>ids.Contains(p.Id)))throw new ArgumentException("Possession already exists at destination.");
            source.Crafts.RemoveAll(j=>ids.Any(id=>PortableCrafting.Affects(j,id)));
            foreach(var item in items)
            {source.Items.Remove(item);destination.Items.Add(item);foreach(var material in new[]{item}.Concat(item.BundledMaterials??Enumerable.Empty<Possession>()))if(material.NaturalId!=0&&!destination.CollectedNatural.Contains(material.NaturalId))destination.CollectedNatural.Add(material.NaturalId);}
            return items.ToArray();
        }
        public static bool TaskItem(Possession p,string role)
            =>role=="WorkKnapping"?StoneCrafting.Rock(p):role=="SpearReady1"?p.Kind==PossessionKind.WoodenSpear||p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.BoneTippedSpear:role=="CarryLarge"||role=="FuelTend"?p.Kind==PossessionKind.Branch||p.Kind==PossessionKind.KindlingBundle||p.Kind==PossessionKind.BarkStrips||p.Kind==PossessionKind.TinderBundle:role=="ToolReady"?p.Kind==PossessionKind.WovenBasket||p.Kind==PossessionKind.DiggingStick:role=="Eat"?Foraging.Food(p):role=="Drink"||role=="WaterCarry"?PaleolithicWater.Vessel(p):false;
        public static PlayerInventory Seed(ForagerPersonSample person,ForagerBandSample band,V position)
        {
            var stock=new PlayerInventory{PlayerName=person.Name};bool Has(int k)=>k<band.Techniques.Length&&band.Techniques[k];
            uint hash=Rng.Mix((uint)person.Id^(uint)(person.Id>>32));
            Possession Add(PossessionKind kind,string slot="stock")
            {var item=HumanEquipment.Item(kind,person.Id,slot,position);NpcOwnership.Assign(item,person,band.Id);item.Place=PossessionPlace.Ground;stock.Items.Add(item);return item;}
            // Use this band's actual routine practice, rather than clothing
            // every culture identically because a distant specialist knows it.
            bool Wears(int k)=>Has(k)&&k<band.RoutineTechniques.Length&&band.RoutineTechniques[k];
            void Garment(PossessionKind kind)
            {
                var p=Add(kind,"clothing");p.Place=PortableTechnology.Wear(p).Value;
                double size=D.Clamp((person.AgeYears+3)/20,.35,1);p.MassKg*=size*size;p.WidthM*=size;p.HeightM*=size;p.DepthM*=size;
                p.CellsWide=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1));p.CellsHigh=Math.Max(1,(int)Math.Ceiling(p.HeightM/.1));
            }
            if(person.AgeYears>=1)
            {
                if(Wears(14))Garment(PossessionKind.FurCoat);else if(Wears(13))Garment(PossessionKind.HideTunic);else if(Wears(8))Garment(PossessionKind.HideWrap);
                if(Wears(13)){Garment(PossessionKind.HideLeggings);Garment(PossessionKind.HideFootwear);}
                if(Wears(14))Garment(PossessionKind.HideHood);
            }
            if(person.AgeYears<7)return stock;
            if(person.AgeYears>=12)
            {
                if(Has(0)){Add(PossessionKind.StoneFlake);if(hash%3!=0){Add(PossessionKind.SmallRock,"l");Add(PossessionKind.SmallRock,"r");}}
                if(Has(3)&&hash%5<3)Add(Has(4)?PossessionKind.StoneTippedSpear:PossessionKind.WoodenSpear);
                if(Has(3)&&hash%3==0)Add(PossessionKind.DiggingStick);
                if(Has(28)&&hash%3!=0)Add(PossessionKind.WovenBasket);
                if(Has(29))Add(Has(6)?PossessionKind.Waterskin:PossessionKind.BarkWaterVessel,"water");
                if(Has(9)&&(hash%5==0||(band.Members??Array.Empty<ForagerPersonSample>()).Where(p=>p.AgeYears>=14).OrderByDescending(p=>p.Skills.Length>5?p.Skills[5]:0).ThenBy(p=>p.Id).FirstOrDefault()?.Id==person.Id))Add(PossessionKind.HandDrillSet);
                if(Has(6)&&Has(7)&&hash%3==0)
                {var carrier=Add(hash%2==0?PossessionKind.HidePack:PossessionKind.HideSatchel,"carrier");carrier.Place=PortableTechnology.Wear(carrier).Value;}
                if(Has(6)&&Has(7))
                {var pouch=stock.Items.FirstOrDefault(p=>p.Kind==PossessionKind.HidePouch)??Add(PossessionKind.HidePouch);pouch.Place=PossessionPlace.Waist;}
            }
            return stock;
        }
        public static Possession[] ForTask(PlayerInventory inventory,string role)
        {
            IEnumerable<Possession> items=inventory.Items;
            if(role=="WorkKnapping")return items.Where(p=>StoneCrafting.Rock(p)).OrderByDescending(p=>p.MassKg).Take(2).ToArray();
            if(role=="SpearReady1")return items.Where(p=>p.Kind==PossessionKind.WoodenSpear||p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.BoneTippedSpear).Take(1).ToArray();
            if(role=="CarryLarge"||role=="FuelTend")return items.Where(p=>p.CampMaterial==0&&(p.Kind==PossessionKind.Branch||p.Kind==PossessionKind.KindlingBundle||p.Kind==PossessionKind.BarkStrips||p.Kind==PossessionKind.TinderBundle)).Take(1).ToArray();
            if(role=="ToolReady")return items.Where(p=>p.Kind==PossessionKind.WovenBasket||p.Kind==PossessionKind.DiggingStick).OrderBy(p=>p.Kind==PossessionKind.WovenBasket?0:1).Take(1).ToArray();
            if(role=="Eat")return items.Where(Foraging.Food).Take(1).ToArray();
            if(role=="Drink"||role=="WaterCarry")return items.Where(PaleolithicWater.Vessel).Take(1).ToArray();
            return Array.Empty<Possession>();
        }
        public static bool Pack(PlayerInventory inventory,Possession item)
        {
            if(inventory.Stow(item.Id,out _))return true;
            foreach(var bag in inventory.Items.Where(b=>b!=item&&b.ContainerWidth>0&&(b.Place==PossessionPlace.RightHand||b.Place==PossessionPlace.LeftHand)).ToArray())
                for(int r=0;r<2;r++)for(int y=0;y<bag.ContainerHeight;y++)for(int x=0;x<bag.ContainerWidth;x++)
                    if(inventory.Move(item.Id,PossessionPlace.Container,bag.Id,x,y,r==1,out _))return true;
            return false;
        }
        public static int PackingPriority(Possession p)=>WoodCrafting.Cutter(p)?0:p.Kind==PossessionKind.HandDrillSet?1:p.ContainerWidth>0?2:p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.WoodenSpear||p.Kind==PossessionKind.BoneTippedSpear?3:4;
        // Preview only: preserve the actual item's place, identity and contents.
        public static bool CanTake(PlayerInventory stock,Possession item)
        {
            bool external=!stock.Items.Contains(item);if(external)stock.Items.Add(item);
            try
            {
                var wear=PortableTechnology.Wear(item);
                if(wear.HasValue&&stock.CanMove(item.Id,wear.Value,"",0,0,false,out _)||stock.CanMove(item.Id,PossessionPlace.RightHand,"",0,0,false,out _)||stock.CanMove(item.Id,PossessionPlace.LeftHand,"",0,0,false,out _))return true;
                foreach(var bag in stock.Items)
                    if(bag!=item&&bag.ContainerWidth>0&&(stock.InWornEquipment(bag)||bag.Place==PossessionPlace.RightHand||bag.Place==PossessionPlace.LeftHand))
                        for(int r=0;r<2;r++)for(int y=0;y<bag.ContainerHeight;y++)for(int x=0;x<bag.ContainerWidth;x++)
                            if(stock.CanMove(item.Id,PossessionPlace.Container,bag.Id,x,y,r==1,out _))return true;
                return false;
            }
            finally{if(external)stock.Items.Remove(item);}
        }
        public static void Equip(PlayerInventory inventory,IEnumerable<Possession> requested,V position)
        {
            var wanted=requested.ToArray();
            foreach(var held in inventory.Items.Where(p=>(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)&&!wanted.Contains(p)).ToArray())
            {
                var wear=PortableTechnology.Wear(held);if(wear.HasValue&&inventory.Move(held.Id,wear.Value,"",0,0,false,out _))continue;
                if(!Pack(inventory,held)){held.Place=PossessionPlace.Ground;held.ContainerId="";held.Position=position;if(WoodCrafting.Wood(held)||PortableTechnology.Item(held.Kind)?.Long==true){held.GroundRotation=new V(90,0,0);held.GroundOffsetM=.04;}}
            }
            foreach(var item in wanted)
            {
                if(!inventory.Items.Contains(item)||item.Place==PossessionPlace.Ground)continue;
                if(item.Place==PossessionPlace.RightHand||item.Place==PossessionPlace.LeftHand)continue;
                inventory.Hold(item.Id,out _);
            }
        }
    }
}

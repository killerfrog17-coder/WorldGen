using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public static class HumanEquipment
    {
        public static string Role(ForagerPersonSample person,HumanActivitySample routine,HumanActivity activity,bool[] techniques)
        {
            bool Has(int k)=>k<techniques.Length&&techniques[k];
            if(person.AgeYears<12||routine.Activity==HumanActivity.Sleep||routine.Activity==HumanActivity.Carried)return null;
            if(routine.Role=="FuelOutbound")return null;
            if(routine.Role=="FuelReturn")return "CarryLarge";
            if(routine.Role.StartsWith("Water"))return Has(29)?activity==HumanActivity.FetchWater?"Drink":"WaterCarry":null;
            if(activity==HumanActivity.TendFire)return "FuelTend";
            if(activity==HumanActivity.Craft)return "WorkKnapping";
            if(activity==HumanActivity.PrepareFood)return "Eat";
            if(activity==HumanActivity.FetchWater)return "Drink";
            if(activity==HumanActivity.GatherFuel)return "CarryLarge";
            uint hash=Rng.Mix((uint)person.Id^(uint)(person.Id>>32));
            if(activity==HumanActivity.Travel&&Has(3)&&hash%4!=0)return "SpearReady1";
            if(activity==HumanActivity.GatherPlants&&Has(28))return "ToolReady";
            return null;
        }
        public static Possession Item(PossessionKind kind,long person,string side,V position)
        {
            var item=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,position):new Possession{Kind=kind,Position=position,WoodSoundness=.9,WoodStraightness=.8};
            item.Id="npc-tool-"+person.ToString("x")+"-"+side+"-"+kind;
            if(kind==PossessionKind.SmallRock){item.MassKg=side=="l"?.6:.16;item.WidthM=side=="l"?.095:.065;item.HeightM=side=="l"?.11:.07;item.DepthM=side=="l"?.085:.055;item.Variant=ToolStone.Encode(side=="l"?ToolStoneMaterial.Chert:ToolStoneMaterial.Granite,(int)(Rng.Mix((uint)person^(uint)(person>>32))%12));}
            if(kind==PossessionKind.WoodenSpear){item.MassKg=.9;item.WidthM=.025;item.DepthM=.025;item.HeightM=1.8;item.CellsWide=1;item.CellsHigh=18;}
            return item;
        }
        public static List<Possession> Held(ResidentEntry entry)
        {
            var result=new List<Possession>();var p=entry.Identity.Person;
            void Add(PossessionKind kind,string side){var item=Item(kind,p.Id,side,entry.Position);item.Place=side=="r"?PossessionPlace.RightHand:PossessionPlace.LeftHand;result.Add(item);}
            string role=Role(p,entry.Activity,entry.Activity.Activity,entry.Identity.Techniques);
            if(role=="WorkKnapping"){Add(PossessionKind.SmallRock,"l");Add(PossessionKind.SmallRock,"r");}
            else if(role=="SpearReady1")Add(PossessionKind.WoodenSpear,"r");
            else if(role=="CarryLarge"||role=="FuelTend")Add(PossessionKind.KindlingBundle,"r");
            else if(role=="ToolReady")Add(PossessionKind.WovenBasket,"r");
            else if(role=="Eat")Add(PossessionKind.EdibleRoot,"r");
            else if((role=="Drink"||role=="WaterCarry")&&entry.Identity.Techniques.Length>29&&entry.Identity.Techniques[29])Add(entry.Identity.Techniques[6]?PossessionKind.HidePouch:PossessionKind.BarkContainer,"r");
            return result;
        }
    }
}

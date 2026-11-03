using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcProp{public Possession Item;public GameObject Root;public string Side;public bool Back;}
    void ClearNpcProps(HumanActor actor){foreach(var prop in actor.NpcProps)DestroyInventoryVisual(prop.Root);actor.NpcProps.Clear();actor.PropRole="";}
    string NpcCarryRole(HumanActor actor,LivingGroup group)
    {
        if(actor.Resident.AgeYears<12||actor.Routine.Activity==HumanActivity.Sleep||actor.Routine.Activity==HumanActivity.Carried)return null;
        var routine=actor.CurrentActivity;
        if(actor.Routine.Role=="FuelOutbound")return null;
        if(actor.Routine.Role=="FuelReturn")return "CarryLarge";
        if(actor.Routine.Role.StartsWith("Water"))return group.BandHas(29)?routine==HumanActivity.FetchWater?"Drink":"WaterCarry":null;
        if(routine==HumanActivity.TendFire)return "FuelTend";
        if(routine==HumanActivity.Craft)return "WorkKnapping";
        if(routine==HumanActivity.PrepareFood)return "Eat";
        if(routine==HumanActivity.FetchWater)return "Drink";
        if(routine==HumanActivity.GatherFuel)return "CarryLarge";
        if(routine==HumanActivity.Travel&&group.BandHas(3)&&HumanHash(actor.ResidentId)%4!=0)return "SpearReady1";
        if(routine==HumanActivity.GatherPlants&&group.BandHas(28))return "ToolReady";
        return null;
    }
    void UpdateNpcProps(HumanActor actor,LivingGroup group,string role)
    {
        if(actor.Speed<.1f&&Time.unscaledTimeAsDouble>=actor.WorkSoundAt)
        {
            uint rhythm=HumanHash(actor.ResidentId^(long)(Time.unscaledTime/3));actor.WorkSoundAt=Time.unscaledTimeAsDouble+1.1+(rhythm%100)/100d*2;
            if((actor.Root.transform.position-view.transform.position).sqrMagnitude<25*25)
            {
                string sound=role=="WorkKnapping"?"stone-contact":actor.CurrentActivity==HumanActivity.BuildCamp||actor.CurrentActivity==HumanActivity.PackCamp||actor.CurrentActivity==HumanActivity.RepairShelter||actor.CurrentActivity==HumanActivity.GatherPlants?"fibre-handle":null;
                if(sound!=null)PlayItemEffect(sound,actor.Root.transform.position+transform.up*.65f,sound=="stone-contact"?.12f:.07f,fromPlayer:false);
            }
        }
        role=role??"";
        if(actor.Bones==null)actor.Bones=actor.Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        if(actor.PropRole!=role)
        {
            ClearNpcProps(actor);actor.PropRole=role;
            void Add(PossessionKind kind,string side,bool back=false)
            {
                var item=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,actor.Planet):new Possession{Kind=kind,Position=actor.Planet,WoodSoundness=.9,WoodStraightness=.8};
                item.Id="npc-tool-"+actor.ResidentId.ToString("x")+"-"+side+"-"+kind;
                if(kind==PossessionKind.SmallRock){item.MassKg=side=="l"?.6:.16;item.WidthM=side=="l"?.095:.065;item.HeightM=side=="l"?.11:.07;item.DepthM=side=="l"?.085:.055;item.Variant=ToolStone.Encode(side=="l"?ToolStoneMaterial.Chert:ToolStoneMaterial.Granite,(int)(HumanHash(actor.ResidentId)%12));}
                if(kind==PossessionKind.WoodenSpear){item.MassKg=.9;item.WidthM=.025;item.DepthM=.025;item.HeightM=1.8;}
                var root=CreatePossessionVisual(item,30);root.transform.SetParent(actor.Root.transform,true);actor.NpcProps.Add(new NpcProp{Item=item,Root=root,Side=side,Back=back});
            }
            if(role=="WorkKnapping"){Add(PossessionKind.SmallRock,"l");Add(PossessionKind.SmallRock,"r");}
            else if(role=="SpearReady1")Add(PossessionKind.WoodenSpear,"r");
            else if(role=="CarryLarge")Add(PossessionKind.KindlingBundle,"r");
            else if(role=="FuelTend")Add(PossessionKind.KindlingBundle,"r");
            else if(role=="ToolReady")Add(PossessionKind.WovenBasket,"r");
            else if(role=="Eat")Add(PossessionKind.EdibleRoot,"r");
            else if((role=="Drink"||role=="WaterCarry")&&group.BandHas(29))Add(group.BandHas(6)?PossessionKind.HidePouch:PossessionKind.BarkContainer,"r");
        }
        foreach(var prop in actor.NpcProps)
        {
            string side=prop.Side;var hand=actor.Bones["hand_"+side];var middle=actor.Bones["middle_01_"+side];
            Vector3 grip=Vector3.Lerp(hand.position,middle.position,.7f),axis=(actor.Bones["index_01_"+side].position-actor.Bones["pinky_01_"+side].position)*(side=="r"?-1:1);axis.Normalize();
            if(role=="CarryLarge")grip=(Vector3.Lerp(actor.Bones["hand_l"].position,actor.Bones["middle_01_l"].position,.7f)+grip)*.5f;
            var normal=Vector3.Cross(middle.position-hand.position,axis).normalized;
            prop.Root.transform.rotation=normal.sqrMagnitude>.001f?Quaternion.LookRotation(normal,axis):Quaternion.FromToRotation(Vector3.up,axis);
            float fraction=StoneCrafting.Rock(prop.Item)?.5f:prop.Item.Kind==PossessionKind.WoodenSpear?.35f:.18f;
            prop.Root.transform.position=grip-axis*(float)prop.Item.HeightM*fraction;
        }
    }
}

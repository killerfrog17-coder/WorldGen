using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcProp{public Possession Item;public GameObject Root;public string Side,PoseRole;public bool Back;public PossessionKind Kind;public int Variant;public double PoseAge=double.NaN;}
    void ClearNpcProps(HumanActor actor){foreach(var prop in actor.NpcProps)DestroyInventoryVisual(prop.Root);actor.NpcProps.Clear();actor.PropRole="";actor.GearVisualHash=int.MinValue;}
    string NpcCarryRole(HumanActor actor,LivingGroup group)
    {
        return ActualNpcCarryRole(actor);
    }
    void UpdateNpcProps(HumanActor actor,LivingGroup group,string role)
    {
        if(!actor.TaskWaiting&&actor.Speed<.1f&&Time.unscaledTimeAsDouble>=actor.WorkSoundAt)
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
        int signature=17,count=0;
        if(actor.Belongings!=null)foreach(var item in actor.Belongings.Items)if(item.Place==PossessionPlace.RightHand||item.Place==PossessionPlace.LeftHand||PortableTechnology.Worn(item.Place))
        {signature=unchecked(signature*31+item.Id.GetHashCode()*7+(int)item.Place*13+(int)item.Kind+(item.Kind==PossessionKind.CampBundle?item.Variant*17:0));count++;}
        if(actor.GearVisualHash!=signature||actor.NpcProps.Count!=count)
        {
            // A hand transfer or one gathered item must not destroy/rebuild
            // every otherwise unchanged basket, pouch and weapon on this person.
            for(int i=actor.NpcProps.Count-1;i>=0;i--)
            {
                var prop=actor.NpcProps[i];var item=actor.Belongings?.Find(prop.Item.Id);
                bool visible=item!=null&&(item.Place==PossessionPlace.RightHand||item.Place==PossessionPlace.LeftHand||PortableTechnology.Worn(item.Place));
                if(!visible||item.Kind!=prop.Kind||item.Kind==PossessionKind.CampBundle&&item.Variant!=prop.Variant)
                {DestroyInventoryVisual(prop.Root);actor.NpcProps.RemoveAt(i);continue;}
                prop.Item=item;prop.Side=item.Place==PossessionPlace.LeftHand?"l":"r";prop.Back=PortableTechnology.Worn(item.Place);prop.PoseAge=double.NaN;
            }
            actor.GearVisualHash=signature;actor.PropRole=role;
            foreach(var item in actor.Belongings?.Items??Enumerable.Empty<Possession>())
            {
                if(item.Place!=PossessionPlace.RightHand&&item.Place!=PossessionPlace.LeftHand&&!PortableTechnology.Worn(item.Place))continue;
                bool exists=false;foreach(var old in actor.NpcProps)if(old.Item.Id==item.Id){exists=true;break;}if(exists)continue;
                var root=CreatePossessionVisual(item,30);root.transform.SetParent(actor.Root.transform,true);actor.NpcProps.Add(new NpcProp{Item=item,Root=root,Kind=item.Kind,Variant=item.Variant,Side=item.Place==PossessionPlace.LeftHand?"l":"r",Back=PortableTechnology.Worn(item.Place)});
            }
        }
        foreach(var prop in actor.NpcProps)
        {
            // Roots are parented to the actor: its motion already moves them.
            // Refit only when the skeleton/hold pose actually changed.
            if(prop.PoseAge==actor.PoseAge&&prop.PoseRole==role)continue;
            prop.PoseAge=actor.PoseAge;prop.PoseRole=role;
            if(prop.Back){PosePortableWear(prop.Root,prop.Item,actor.Root.transform,actor.Bones);continue;}
            string side=prop.Side;var hand=actor.Bones[side=="l"?"hand_l":"hand_r"];var middle=actor.Bones[side=="l"?"middle_01_l":"middle_01_r"];
            Vector3 grip=Vector3.Lerp(hand.position,middle.position,.7f),axis=(actor.Bones[side=="l"?"index_01_l":"index_01_r"].position-actor.Bones[side=="l"?"pinky_01_l":"pinky_01_r"].position)*(side=="r"?-1:1);axis.Normalize();
            if(role=="CarryLarge")grip=(Vector3.Lerp(actor.Bones["hand_l"].position,actor.Bones["middle_01_l"].position,.7f)+grip)*.5f;
            var normal=Vector3.Cross(middle.position-hand.position,axis).normalized;
            prop.Root.transform.rotation=normal.sqrMagnitude>.001f?Quaternion.LookRotation(normal,axis):Quaternion.FromToRotation(Vector3.up,axis);
            float fraction=StoneCrafting.Rock(prop.Item)?.5f:WoodCrafting.Wood(prop.Item)||PortableTechnology.Item(prop.Item.Kind)?.Long==true?.35f:.18f;
            prop.Root.transform.position=grip-axis*(float)prop.Item.HeightM*fraction;
        }
    }
}

using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class ItemUseTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        V ground=new V(1,0,0);
        var light=new Possession{Id="light",Kind=PossessionKind.SmallRock,MassKg=.05,Position=ground};var heavy=light.Copy();heavy.Id="heavy";heavy.MassKg=.8;
        Check(ItemUseRules.ThrowSpeed(light,1)>ItemUseRules.ThrowSpeed(heavy,1),"Small rocks launch faster than large rocks");
        Check(ItemUseRules.ImpactDamage(.8,20,0)>ItemUseRules.ImpactDamage(.05,20,0),"At a fixed speed heavier projectiles cause more damage");
        Check(ItemUseRules.KineticEnergy(.5,20)==100,"Kinetic energy uses physical mass and squared speed");
        Check(ItemUseRules.ThrowSpeed(light,0)<ItemUseRules.ThrowSpeed(light,1),"Charge changes launch work");
        Check(ItemUseRules.ThrowSpeed(light,1,0)<ItemUseRules.ThrowSpeed(light,1,1),"Fatigue limits launch effort");
        for(double mass=.001;mass<20;mass*=1.7)
        {var p=light.Copy();p.MassKg=mass;double v=ItemUseRules.ThrowSpeed(p,1);Check(v<=28&&ItemUseRules.KineticEnergy(mass,v)<=90+1e-9&&mass*v<=11+1e-9,"Hand-thrown stones obey velocity/work/impulse budgets");}
        Check(ItemUseRules.DragAcceleration(.05,.002,20)>ItemUseRules.DragAcceleration(.8,.002,20),"Air drag depends on mass as well as frontal area");
        var arrow=PortableTechnology.Create(PossessionKind.Arrow,ground);Check(ItemUseRules.BowSpeed(arrow,.5)<ItemUseRules.BowSpeed(arrow,1),"Bow draw affects arrow energy");
        Check(ItemUseRules.KineticEnergy(arrow.MassKg,ItemUseRules.BowSpeed(arrow,1))<=55+1e-8,"Bow energy is bounded");
        var spear=new Possession{Id="spear",Kind=PossessionKind.WoodenSpear,Position=ground,HeightM=1.9,MassKg=.9};Check(ItemUseRules.Profile(spear).ReachM>ItemUseRules.Profile(light).ReachM,"Spear reach differs from a stone in hand");
        Check(ItemUseRules.Profile(spear).Secondary==ItemSecondaryUse.Throw,"Spear secondary throws");
        Check(ItemUseRules.Profile(PortableTechnology.Create(PossessionKind.Bone,ground)).Secondary==ItemSecondaryUse.Block,"Bone secondary blocks rather than becoming an unexplained missile");
        foreach(var kind in new[]{PossessionKind.RawFibre,PossessionKind.Cordage,PossessionKind.RawHide,PossessionKind.WorkedHide,PossessionKind.Feather,PossessionKind.BoneNeedle,PossessionKind.HidePack})Check(!ItemUseRules.Profile(PortableTechnology.Create(kind,ground)).Weapon,"Nonweapon component/carrier does not become a generic club: "+kind);
        double stamina=30,unblocked=40;double front=ItemUseRules.BlockDamage(unblocked,spear,1,ref stamina);Check(front<unblocked&&stamina<30,"Facing block absorbs damage and consumes stamina");
        stamina=30;Check(ItemUseRules.BlockDamage(unblocked,spear,-1,ref stamina)==unblocked&&stamina==30,"Guard does not stop an attack from behind");
        stamina=0;Check(ItemUseRules.BlockDamage(unblocked,spear,1,ref stamina)==unblocked,"Exhausted guard cannot grant immunity");
        Check(ItemUseRules.HealthForMass(500)>ItemUseRules.HealthForMass(50)&&ItemUseRules.HealthForMass(.01)>=1,"Animal health scales smoothly with body mass");
        var record=new WildlifeIndividual{Id=88,Species="deer",Position=ground,Health=0,MaximumHealth=80,Coat=1,Parts=new[]{15,0,0,0},CorpsePose=new double[16]};
        var session=new PlaySession();session.Wildlife.Individuals.Add(record);var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();Check(saved.Wildlife.Individuals[0].Dead&&saved.Wildlife.Individuals[0].Id==88,"Killed identity and corpse state survive full session serialization");
        var copy=session.Copy();copy.Wildlife.Individuals[0].Parts[0]=31;copy.Wildlife.Individuals[0].CorpsePose[0]=2;Check(record.Parts[0]==15&&record.CorpsePose[0]==0,"Wildlife copies do not alias mutable corpse data");
        var legacyJson=System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(new PlaySession())).AsObject();legacyJson.Remove("Wildlife");var legacy=WorldJson.Read<PlaySession>(legacyJson.ToJsonString());legacy.Validate();Check(legacy.Wildlife!=null,"Legacy sessions initialize optional wildlife state during validation");
        Console.WriteLine("PASS "+checks+" item-use, physical launch, block and wildlife persistence checks");
    }
}

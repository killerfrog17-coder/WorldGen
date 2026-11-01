using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class FrameStructureTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string text){count++;if(!ok)throw new Exception(text);}
        V at=new V(1,0,0);
        Possession Wood(string id,int variant=2,double width=.035)=>new Possession{Id=id,Kind=PossessionKind.Branch,Position=at,Variant=variant,WidthM=width,DepthM=width,HeightM=2,MassKg=.9,WoodSoundness=.95};
        CampObject Post(string id,double embed=.35){var c=new CampObject{Id=id,Kind=CampKind.Stake,Position=at,EmbedM=embed,SoilDepthM=.6};CampRules.Add(c,Wood(id+"-wood"));return c;}
        CampObject Beam(string id,string left,string right)
        {var c=new CampObject{Id=id,Kind=CampKind.FrameWood,Position=at,FrameAxis=new V(1,0,0),FrameHeightM=1.5};CampRules.Add(c,Wood(id+"-wood",0));c.FrameJoints.Add(new FrameJoint{ParentId=left,ParentT=.9,SelfT=.08,Kind=FrameJointKind.ForkRest});c.FrameJoints.Add(new FrameJoint{ParentId=right,ParentT=.9,SelfT=.85,Kind=FrameJointKind.ForkRest});return c;}
        var state=new CampState();var a=Post("a");var b=Post("b");var ridge=Beam("ridge",a.Id,b.Id);state.Objects.AddRange(new[]{a,b,ridge});state.Validate();
        var result=FrameRules.Analyse(state);Check(result.Nodes[ridge.Id].Supported,"Two driven fork posts support a loose ridgepole");
        Check(result.Order.IndexOf(a)<result.Order.IndexOf(ridge),"Supports are evaluated before dependent wood");
        double supported=result.Nodes[a.Id].LoadKg-a.MassKg;Check(Math.Abs(supported-ridge.MassKg*.5)<1e-9,"Ridge weight is divided between the actual supports");
        Check(FrameRules.Fork(a.Materials[0])&&!FrameRules.Fork(new Possession{Kind=PossessionKind.TrimmedPole,Variant=2}),"Trimming removes the useful fork");
        a.EmbedM=.02;result=FrameRules.Analyse(state);Check(!result.Nodes[ridge.Id].Supported,"Shallow footing cannot magically carry a frame");a.EmbedM=.35;
        var rib=new CampObject{Id="rib",Kind=CampKind.FrameWood,Position=at,FrameAxis=new V(0,.7,.714142842854285),FrameGrounded=true,FrameHeightM=.02};CampRules.Add(rib,Wood("rib-wood",0));rib.FrameJoints.Add(new FrameJoint{ParentId=ridge.Id,ParentT=.5,SelfT=.85,Kind=FrameJointKind.CrossRest});state.Objects.Add(rib);state.Validate();
        result=FrameRules.Analyse(state);Check(result.Nodes[rib.Id].Supported,"A ground foot and a supported ridge carry a leaning rib");
        Check(Math.Abs(result.Nodes[ridge.Id].LoadKg-ridge.MassKg-rib.MassKg*.5)<1e-9,"The ground carries its share of the rib instead of loading the ridge twice");
        var inventory=new PlayerInventory();var cord=PortableTechnology.Create(PossessionKind.Cordage,at);cord.MassKg=.12;cord.Place=PossessionPlace.RightHand;inventory.Items.Add(cord);
        double before=cord.MassKg;var joint=rib.FrameJoints[0];
        Check(FrameRules.Lash(rib,ridge,joint,inventory,new[]{cord.Id},out _),"A real coil lashes the chosen joint");
        Check(joint.Lashed&&inventory.Find(cord.Id)!=null&&inventory.Find(cord.Id).MassKg<before,"Lashing consumes only the necessary part of a coil");
        Check(Math.Abs(inventory.Items.Sum(p=>p.MassKg)+joint.Bindings.Sum(p=>p.MassKg)-before)<1e-9,"Partial lashing conserves material mass");
        Check(joint.Bindings.All(p=>p.Id!=cord.Id&&p.Place==PossessionPlace.Ground&&p.ContainerId==""),"Binding ownership has independent IDs and no stale hand/container state");
        state.Validate();inventory.Validate();var session=new PlaySession{Camp=state,Inventory=inventory};session.Validate();
        var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();Check(saved.Camp.Objects.Single(p=>p.Id==rib.Id).FrameJoints[0].Lashed,"Actual save format retains links and exact lashing materials");
        var copy=session.Copy();copy.Camp.Objects.Single(p=>p.Id==rib.Id).FrameJoints[0].Bindings[0].MassKg=2;Check(joint.Bindings[0].MassKg<.1,"Detached snapshots do not share binding material objects");
        double tied=FrameRules.Analyse(state).Nodes[rib.Id].Stability;var returned=FrameRules.Untie(joint);Check(returned.Count>0&&!joint.Lashed&&FrameRules.Analyse(state).Nodes[rib.Id].Stability<tied,"Untying returns material and restores weaker resting support");
        joint.Kind=FrameJointKind.Lash;Check(!FrameRules.Analyse(state).Nodes[rib.Id].Supported,"A side joint cannot remain suspended without its binding");
        var tooLittle=PortableTechnology.Create(PossessionKind.RawhideStrips,at);tooLittle.MassKg=.001;inventory.Items.Add(tooLittle);double untouched=tooLittle.MassKg;
        Check(!FrameRules.Lash(rib,ridge,joint,inventory,new[]{tooLittle.Id},out _)&&tooLittle.MassKg==untouched&&!joint.Lashed,"Insufficient binding does not consume material or create a fake joint");
        Check(FrameRules.Lash(rib,ridge,joint,inventory,new[]{cord.Id},out _),"A proper side lashing makes the contact usable");Check(FrameRules.Analyse(state).Nodes[rib.Id].Supported,"Secured side contact supports the grounded rib");
        state.Objects.Remove(a);result=FrameRules.Analyse(state);Check(!result.Nodes[ridge.Id].Supported&&!result.Nodes[rib.Id].Supported,"Removing a post propagates failure through the dependent frame");state.Objects.Insert(0,a);
        ridge.Materials[0].MassKg=100;Check(!FrameRules.Analyse(state).Nodes[ridge.Id].Supported,"Overloading a thin frame is rejected");ridge.Materials[0].MassKg=.9;
        bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        var bad=state.Copy();bad.Objects.Single(c=>c.Id==ridge.Id).FrameJoints[0].ParentId="missing";Check(Reject(bad.Validate),"Save validation rejects dangling supports");
        bad=state.Copy();bad.Objects.Single(c=>c.Id==ridge.Id).FrameJoints[0].ParentId=rib.Id;Check(Reject(bad.Validate),"A floating dependency cycle cannot support itself");
        bad=state.Copy();bad.Objects.Single(c=>c.Id==ridge.Id).FrameAxis=new V(double.NaN,0,0);Check(Reject(bad.Validate),"Nonfinite frame poses cannot enter saves");
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(new PlaySession{Camp=new CampState{Objects={a.Copy()}}}));
        foreach(string field in new[]{"FrameAxis","FrameHeightM","FrameRoll","FrameGrounded","FrameJoints"})legacy["Camp"]["Objects"][0].AsObject().Remove(field);
        var old=WorldJson.Read<PlaySession>(legacy.ToJsonString());old.Validate();Check(old.Camp.Objects.Count==1&&old.Camp.Objects[0].FrameJoints.Count==0,"Older saved stakes without structure fields still load");
        Console.WriteLine("PASS "+count+" structural support, mass, ownership and save checks");
    }
}

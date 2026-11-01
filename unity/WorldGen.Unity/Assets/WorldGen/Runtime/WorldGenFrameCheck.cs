using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckFrameFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FRAME "+text);report.Add("PASS "+text);Debug.Log("FRAME PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();paused=true;TestHumanView(0);
        CheckPlayerComfort(Check);
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
        double sx=motion.X,sz=motion.Z;
        void Aim(Vector3 local)
        {UpdateHumans();UpdateCamera();var direction=(local-view.transform.localPosition).normalized;heading=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;UpdateCamera();view.transform.LookAt(transform.TransformPoint(local));campGroundAt=0;}
        Possession Wood(string id,double length,double width,bool fork)
        {
            var p=WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=Position,WidthM=length,HeightM=width,Variant=fork?2:0});
            p.Id=id;p.NaturalId=0;if(!fork)p.Kind=PossessionKind.TrimmedPole;Inventory.Items.Add(p);inventoryNearbyAt=0;return p;
        }
        CampObject Post(string id,double x,double z)
        {
            var p=Wood(id,2.1,.04,true);Aim(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ)));BeginCampPlacement(p,CampKind.Stake);
            Check(campPlaceValid,"Dry ground accepts an actual fork post: "+campPlaceReason);ConfirmCampPlacement();var post=Camp.Objects.Last();post.Heading=90;post.SoilDepthM=.6;
            var hammer=new Possession{Id="frame-hammer",Kind=PossessionKind.SmallRock,MassKg=.6,WidthM=.08,HeightM=.07};for(int i=0;i<40;i++)CampRules.Drive(post,hammer);
            InvalidateCampVisual(post);frameDirty=true;campUpdateAt=0;UpdateCamp();Physics.SyncTransforms();return post;
        }
        var left=Post("frame-left",sx-.65,sz+1.4);var right=Post("frame-right",sx+.65,sz+1.4);
        Check(FrameRules.Fork(left.Materials[0])&&left.EmbedM>.2,"Real fork geometry and driven depth remain on the saved posts");
        var ridgeWood=Wood("frame-ridge",2.25,.03,false);BeginCampPlacement(ridgeWood,CampKind.FrameWood);
        Aim(FramePoint(left,.93));UpdateCampPlacement();Check(frameHover?.Parent==left,"The first placement ray selects the fork post");FramePlacementClick();Check(frameFirst?.Parent==left,"First click retains an actual structural contact");
        Aim(FramePoint(right,.93));UpdateCampPlacement();Check(campPlaceValid&&frameDraft!=null,"A full-length ridge spans the two posts: "+campPlaceReason);
        Check(campGhost!=null&&campGhost.activeSelf&&campGhostMaterial.GetColor("_BaseColor").a<.5f,"Placement shows a translucent full wood ghost");
        FramePlacementClick();var ridge=Camp.Objects.Last();Check(ridge.Kind==CampKind.FrameWood&&ridge.Materials[0].Id==ridgeWood.Id&&Inventory.Find(ridgeWood.Id)==null,"Placing transfers the exact wood from inventory into the frame");
        Check(ridge.FrameJoints.All(j=>j.Kind==FrameJointKind.ForkRest&&!j.Lashed),"Aligned forks support the ridge without imaginary rope");
        Check(Math.Abs(ridge.Materials[0].HeightM-2.25)<1e-8,"A beam is not stretched to fit its supports");
        Check(campVisuals[ridge.Id].Root.GetComponent<CapsuleCollider>()!=null,"Placed wood has a real solid collider");
        Aim(FramePoint(ridge,.5));Check(AimedCamp(out _)==ridge,"The horizontal beam can be targeted at its actual surface");
        var ribWood=Wood("frame-rib",2.25,.028,false);BeginCampPlacement(ribWood,CampKind.FrameWood);
        bool ribReady=false,groundFoot=false;
        for(int i=0;i<16&&!ribReady;i++)
        {
            frameFirst=null;float angle=i*Mathf.PI/8;double x=sx+Math.Sin(angle),z=sz+1.4-Math.Cos(angle);
            Aim(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ)));UpdateCampPlacement();
            if(frameHover==null||frameHover.Parent!=null)continue;groundFoot=true;FramePlacementClick();
            Aim(FramePoint(ridge,.5));UpdateCampPlacement();ribReady=campPlaceValid;
        }
        Check(groundFoot,"A leaning rib can start on reachable dry ground");Check(ribReady,"A ground foot and ridge accept the leaning rib: "+campPlaceReason);FramePlacementClick();var rib=Camp.Objects.Last();
        Check(rib.Kind==CampKind.FrameWood&&rib.FrameGrounded&&rib.FrameJoints.Single().ParentId==ridge.Id,"The rib records both its ground foot and ridge contact");
        Check(frameAnalysis.Nodes[rib.Id].Supported,"The supported lean-to rib remains standing");
        var joint=rib.FrameJoints.Single();var coil=PortableTechnology.Create(PossessionKind.Cordage,Position);Inventory.Items.Add(coil);double cordMass=coil.MassKg;
        PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;Vitals.Health=100;Vitals.Stamina=100;
        LashFrameJoint(rib,joint);Check(worldToolWork!=null&&!joint.Lashed,"Lashing starts real timed work rather than consuming on menu click");paused=false;
        double deadline=Time.unscaledTimeAsDouble+12;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(joint.Lashed&&Inventory.Find(coil.Id)!=null&&Inventory.Find(coil.Id).MassKg<cordMass,"Completed lashing consumes part of the actual coil");
        campUpdateAt=0;UpdateCamp();Check(campVisuals[rib.Id].Root.GetComponentsInChildren<LineRenderer>().Any(r=>r.name=="Lashing"),"A visible rope wrap marks the chosen joint");
        var copy=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));copy.Validate();Check(copy.Camp.Objects.Any(c=>c.Id==ridge.Id)&&copy.Camp.Objects.Single(c=>c.Id==rib.Id).FrameJoints[0].Lashed,"The real player save round-trips the frame and its lashing");
        var root=campVisuals[ridge.Id].Root.transform;var contact=FramePoint(left,ridge.FrameJoints[0].ParentT)+LocalFrameVector(left.Position,ridge.FrameJoints[0].Offset);
        Check(Vector3.Distance(root.localPosition+FrameAxis(ridge)*(float)(ridge.FrameJoints[0].SelfT*ridge.Materials[0].HeightM),contact)<.005,"Rendered ridge and actual fork support meet within5mm");
        var previous=view.targetTexture;var oldActive=RenderTexture.active;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;
        var focus=FramePoint(ridge,.5);view.transform.localPosition=focus+new Vector3(3,1.1f,-4);view.transform.LookAt(transform.TransformPoint(focus-Vector3.up*.45f));view.Render();RenderTexture.active=target;
        var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"frame-with-lashing.png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=oldActive;view.targetTexture=previous;target.Release();Destroy(target);UpdateCamera();
        double original=left.EmbedM;CampRules.Drive(left,new Possession{Kind=PossessionKind.SmallRock,MassKg=.7});frameDirty=true;campUpdateAt=0;UpdateCamp();Check(left.EmbedM>=original&&Camp.Objects.Contains(ridge),"A small support adjustment re-seats the connected frame");
        string woodId=ridge.Materials[0].Id,ribId=rib.Materials[0].Id;double bindingMass=joint.Bindings.Sum(p=>p.MassKg);var bindings=joint.Bindings.Select(p=>p.Id).ToArray();
        RemoveCamp(left);Check(!Camp.Objects.Contains(ridge)&&!Camp.Objects.Contains(rib),"Removing a supporting post brings down dependent wood");
        Check(Inventory.Find(woodId)?.GroundMoving==true&&Inventory.Find(ribId)?.GroundMoving==true,"Fallen pieces enter the existing item physics system");
        Check(Math.Abs(bindings.Sum(id=>Inventory.Find(id)?.MassKg??0)-bindingMass)<1e-9,"Collapse preserves the binding material instead of deleting it");
        PlayerRules.Validate();Check(Camp.Objects.All(c=>c.Kind!=CampKind.FrameWood),"The collapsed state has no dangling saved joints");
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=1;paused=false;
    }
    void CheckPlayerComfort(Action<bool,string> check)
    {
        bool oldReview=humanReviewPose,oldMode=HumanAnimationOptions.Kimodo;
        humanReviewPose=true;HumanAnimationOptions.Kimodo=true;
        try
        {
            var actor=humanPlayer;var shared=actor.Shared;
            check(shared!=null,"Player has the shared native human animation graph");
            var head=actor.Root.GetComponentsInChildren<Transform>().First(t=>t.name=="head");
            foreach(string role in new[]{"Empty","ToolReady","CarryLarge"})
            {
                check(shared.Clips.ContainsKey(role),"Idle comfort samples a real "+role+" clip");
                Vector3 anchor=default;Quaternion angle=default;float worst=0;
                for(int i=0;i<120;i++)
                {
                    shared.Locomotion(0,false,false,false,1f/30);
                    shared.Action(role,i/30d,role!="Empty",role=="CarryLarge",null,0,false,1f/30);actor.Graph.Evaluate(0);
                    if(i==60){anchor=head.position;angle=head.rotation;}
                    if(i>60)worst=Mathf.Max(worst,Vector3.Distance(anchor,head.position),Quaternion.Angle(angle,head.rotation));
                }
                check(worst<.001f,"Idle "+role+" no longer rocks the head or upper body: "+worst);
            }
            TestHumanView(0);pitch=0;sharedCameraEyeValid=false;
            foreach(float speed in new[]{1.5f,3.4f,6f})
            {
                Vector3 cameraStart=default,footStart=default;float cameraMotion=0,footMotion=0;
                var foot=actor.Root.GetComponentsInChildren<Transform>().First(t=>t.name=="foot_l");
                for(int i=0;i<100;i++)
                {
                    shared.Locomotion(speed,false,false,false,1f/30);shared.Action("Empty",0,false,false,null,0,false,1f/30);actor.Graph.Evaluate(0);UpdateHumanCamera();
                    if(i==30){cameraStart=view.transform.localPosition;footStart=foot.position;}
                    if(i>30){cameraMotion=Mathf.Max(cameraMotion,Vector3.Distance(cameraStart,view.transform.localPosition));footMotion=Mathf.Max(footMotion,Vector3.Distance(footStart,foot.position));}
                }
                check(cameraMotion<.001f,"First-person camera has no animated gait bob at "+speed+"m/s: "+cameraMotion);
                check(footMotion>.01f,"Body locomotion remains animated at "+speed+"m/s");
            }
            var effort=new ExertionState{Breath=.8,Burst=1,Fatigue=0};
            double fresh=BreathingFeedback.Intensity(effort);effort.Burst=.1;effort.Fatigue=.6;
            check(fresh<.025&&BreathingFeedback.Intensity(effort)>fresh*20,"Fresh breathing is quiet and sustained exhaustion gradually raises it");
        }
        finally{humanReviewPose=oldReview;HumanAnimationOptions.Kimodo=oldMode;sharedCameraEyeValid=false;UpdateHumans();UpdateCamera();}
    }
}

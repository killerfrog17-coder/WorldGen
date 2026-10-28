using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class WorldToolWork
    {
        public string Label;public Possession Tool;public double X,Z,Done,Required;public CraftEquipmentState Equipment;public Action Complete,Cancelled;
        public Animal Fish;public LineRenderer Line;
    }
    WorldToolWork worldToolWork;
    NaturalToolRecord ToolResource(string id)
    {
        var record=Inventory.ToolResources.FirstOrDefault(r=>r.Id==id);if(record==null){record=new NaturalToolRecord{Id=id};Inventory.ToolResources.Add(record);}return record;
    }
    string GroundWorkKey(V position)=>"ground-"+Math.Round(position.X*1000000)+"-"+Math.Round(position.Y*1000000)+"-"+Math.Round(position.Z*1000000);
    void BeginWorldToolWork(Possession tool,string label,double seconds,Action complete,Action cancelled=null)
    {
        CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);var equipment=PrepareWorkHands(new[]{tool.Id});
        if(!Inventory.Move(tool.Id,PossessionPlace.RightHand,"",0,0,false,out var reason)){RestoreWorkHands(new ItemCraftJob{Equipment=equipment});InventoryFeedback(reason);return;}
        RemoveDroppedBody(tool.Id);Inventory.ToolWorkEquipment=equipment;worldToolWork=new WorldToolWork{Tool=tool,Label=label,X=motion.X,Z=motion.Z,Required=seconds,Equipment=equipment,Complete=complete,Cancelled=cancelled};
        InventoryFeedback(label+"...");PlayItemEffect(label.StartsWith("Digging",StringComparison.Ordinal)?"soil-dig":"fibre-handle",view.transform.position,.4f,true);craftSoundAt=Time.unscaledTimeAsDouble+1;
    }
    void CancelWorldToolWork()
    {var work=worldToolWork;if(work==null)return;worldToolWork=null;Inventory.ToolWorkEquipment=null;if(work.Line!=null)Destroy(work.Line.gameObject);work.Cancelled?.Invoke();RestoreWorkHands(new ItemCraftJob{Equipment=work.Equipment});if(stoneCraftVoice!=null)stoneCraftVoice.Stop();}
    void UpdateWorldToolWork()
    {
        var work=worldToolWork;if(work==null)return;
        if(work.Line!=null&&work.Fish?.Root!=null)
        {var start=view.transform.TransformPoint(new Vector3(.23f,-.28f,.55f));var end=work.Fish.Renderer.bounds.center;for(int i=0;i<6;i++){float t=i/5f;work.Line.SetPosition(i,Vector3.Lerp(start,end,t)-Vector3.up*(.2f*t*(1-t)));}}
        if(DeathVisible||Spectating||Inventory.Find(work.Tool.Id)!=work.Tool||work.Tool.Place!=PossessionPlace.RightHand||Math.Pow(motion.X-work.X,2)+Math.Pow(motion.Z-work.Z,2)>.2)
        {CancelWorldToolWork();return;}
        if(paused||mapOpen||ecologyOpen||chatOpen||!Ready||!Application.isFocused&&!inventoryReviewMode)return;
        if(!SpendCombatStamina(Math.Min(.25,Time.unscaledDeltaTime)*.5)){CancelWorldToolWork();return;}
        work.Done+=Math.Min(.25,Time.unscaledDeltaTime)*86400/(PlayerRules.Settings.RealMinutesPerDay*60*PlayerRules.Settings.ItemCraftingTimeMultiplier);
        if(Time.unscaledTimeAsDouble>=craftSoundAt){PlayItemEffect(work.Label.StartsWith("Digging",StringComparison.Ordinal)?"soil-dig":"fibre-handle",view.transform.position,.4f,true);craftSoundAt=Time.unscaledTimeAsDouble+1;}
        if(work.Done<work.Required)return;worldToolWork=null;Inventory.ToolWorkEquipment=null;if(work.Line!=null)Destroy(work.Line.gameObject);ApplyItemImpactWear(work.Tool,12,false);RestoreWorkHands(new ItemCraftJob{Equipment=work.Equipment});work.Complete();inventoryNearbyAt=0;
    }
    void ReceiveToolMaterial(Possession output)
    {
        Inventory.Items.Add(output);if(!Inventory.Stow(output.Id,out _)&&!Inventory.Hold(output.Id,out _))ReleasePossession(output,true);
        InventoryFeedback("Collected "+output.Name.ToLowerInvariant()+".");
    }
    partial void ToolHitTree(Possession tool,CombatObstacle obstacle,double energy)
    {
        if(tool==null||tool.UseDamage>=1)return;var capability=PortableTechnology.Capabilities(tool);if((capability&(CraftCapability.Cut|CraftCapability.Chop))==0)return;
        var tree=obstacle.Source;var record=ToolResource("tree-"+tree.Id.ToString("x16"));bool chop=(capability&CraftCapability.Chop)!=0;
        if(chop&&record.Branches>=3||!chop&&record.Bark>=2){CombatNotice(chop?"No more reachable branches on this tree.":"This accessible patch of bark has already been stripped.");return;}
        record.Work+=energy;if(record.Work<(chop?180:65)){CombatNotice(chop?"Cutting a reachable branch...":"Loosening a patch of bark...");return;}record.Work=0;
        if(chop)
        {
            record.Branches++;var branch=new ScatterInstance{Kind=ScatterKind.Branch,Position=tree.Position,WidthM=Math.Min(2,Math.Max(.6,tree.HeightM*.14)),HeightM=.026+record.Branches*.004,Variant=0};
            var output=WoodCrafting.Branch(branch);output.Id="cut-branch-"+Guid.NewGuid().ToString("N");output.NaturalId=0;output.Position=Position;ReceiveToolMaterial(output);
        }
        else{record.Bark++;ReceiveToolMaterial(PortableTechnology.Create(PossessionKind.BarkSheet,Position));}
        PlayCraftSound(ItemCraftAction.TrimBranch);
    }
    partial void BeginDigging(Possession item)
    {
        if(item==null||HeightAboveGround>3){CombatNotice("Stand on the ground to dig.");return;}
        var sample=walkingPatch.Sample(Position);if(sample.Underwater||sample.Ground.DepthM<.12||CurrentWeather.TemperatureC< -2){CombatNotice("This ground is submerged, rocky or frozen.");return;}
        var record=ToolResource(GroundWorkKey(Position));if(record.Digging>=2){CombatNotice("This small patch has already been worked. Try another patch.");return;}
        if(sample.Ground.RootSupport<.12){CombatNotice("There are no useful roots in this bare soil.");return;}
        BeginWorldToolWork(item,"Digging out fibrous roots",120,()=>{record.Digging++;ReceiveToolMaterial(PortableTechnology.Create(PossessionKind.RawFibre,Position));});
    }
    partial void BeginFishing(Possession item)
    {
        if(item==null)return;bool net=item.Kind==PossessionKind.FibreNet;float reach=net?2.8f:8;
        var fish=animals.Values.Where(a=>!a.Dead&&!a.FishingTethered&&a.Spawn.Profile.medium=="water"&&Vector3.Distance(a.Renderer.bounds.center,view.transform.position)<reach&&a.MassKg<(net?5:12)&&Vector3.Dot(view.transform.forward,(a.Root.transform.position-view.transform.position).normalized)>.15f).OrderBy(a=>(a.Planet-Position).Length).FirstOrDefault();
        if(fish==null){CombatNotice(net?"Bring the net close to a small fish in shallow water.":"Cast near a fish within eight metres of shore.");return;}
        var sample=walkingPatch.Sample(fish.Planet);if(sample.WaterKind==LocalWaterKind.None){CombatNotice("There is no fishable water here.");return;}
        if(net&&!double.IsNaN(sample.WaterSurfaceM)&&frame.SurfaceY(fish.Planet,sample.WaterSurfaceM)-fish.Root.transform.localPosition.y>1.2){CombatNotice("That fish is too deep for this hand net.");return;}
        BeginWorldToolWork(item,net?"Gathering the net":"Working the hand-line",net?90:240,()=>
        {
            fish.FishingTethered=false;
            if(fish.Root==null||fish.Dead||(fish.Planet-Position).Length*frame.RadiusM>reach+1){InventoryFeedback("The fish escaped.");return;}
            // Land the actual animal, not a free replacement fish or meat drop.
            // Processing remains explicitly deferred to the owner's later pass.
            // Bring the catch back toward the angler, not forward into the water.
            fish.X=motion.X-Math.Sin(heading*Math.PI/180)*.65;fish.Z=motion.Z-Math.Cos(heading*Math.PI/180)*.65;fish.Planet=frame.Position(fish.X,fish.Z);fish.GroundY=Ground(fish.X,fish.Z);
            fish.Root.transform.localPosition=new Vector3((float)(fish.X-originX),(float)fish.GroundY+.15f,(float)(fish.Z-originZ));
            fish.CorpseHeightOffset=.15;fish.Health=0;BeginAnimalCorpse(fish,Vector3.zero);RememberAnimal(fish,true);InventoryFeedback("Fish landed whole. Processing is not implemented yet.");
        },()=>fish.FishingTethered=false);
        if(worldToolWork?.Tool==item)
        {
            double vertical=fish.Renderer.bounds.center.y-view.transform.position.y;
            fish.FishingTethered=true;fish.FishingAnchor=Position;fish.FishingReach=Math.Sqrt(Math.Max(.04,reach*reach-vertical*vertical));worldToolWork.Fish=fish;
            if(!net)
            {if(tackleLineMaterial==null)tackleLineMaterial=new Material(Resources.Load<Shader>("WorldGenTackleLine"));var line=new GameObject("Hand-line",typeof(LineRenderer));line.transform.SetParent(transform,false);worldToolWork.Line=line.GetComponent<LineRenderer>();worldToolWork.Line.sharedMaterial=tackleLineMaterial;worldToolWork.Line.positionCount=6;worldToolWork.Line.startWidth=worldToolWork.Line.endWidth=.002f;worldToolWork.Line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;worldToolWork.Line.receiveShadows=false;}
        }
    }
    Material tackleLineMaterial;
}

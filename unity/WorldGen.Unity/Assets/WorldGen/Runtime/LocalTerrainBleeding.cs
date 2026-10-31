using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<ulong,double> bloodDropAt=new Dictionary<ulong,double>();
    double remoteBleedingAt,bloodMeshAt,bloodMeshOriginX,bloodMeshOriginZ,bloodMeshExpiryAt;bool bloodMeshDirty;V bloodMeshObserver;int bloodGroundRevision;
    LocalTerrainFrame bloodMeshFrame;GameObject bloodTraceRoot;Mesh bloodTraceMesh;Material bloodTraceMaterial;
    readonly Dictionary<string,bool[]> bleedingTorsoBones=new Dictionary<string,bool[]>();
    static bool TorsoBone(string name)
    {name=(name??"").ToLowerInvariant();return name.Contains("spine")||name.Contains("chest")||name.Contains("rib")||name.Contains("pelvis")||name.Contains("abdomen")||name=="body"||name=="hip";}
    bool TorsoContact(CombatHit hit)
    {
        int triangle=hit.Face*3;if(triangle<0||triangle+2>=hit.Triangles.Length)return false;
        if(hit.Animal!=null)
        {
            var animal=hit.Animal;var skin=AnimalSurface(animal);if(!wildlifeSkeletons.TryGetValue(animal.Spawn.Profile.slug,out var skeleton))return false;
            if(!bleedingTorsoBones.TryGetValue(skeleton.slug,out var torsoBones))
            {
                var bones=skeleton.bones;torsoBones=bones.Select(b=>TorsoBone(b.name)).ToArray();
                int head=Array.FindIndex(bones,b=>b.name.IndexOf("head",StringComparison.OrdinalIgnoreCase)>=0);var axial=new HashSet<int>();
                for(int at=head,guard=0;at>=0&&at<bones.Length&&guard++<bones.Length;at=bones[at].parent)axial.Add(at);
                // Generic Joint_XX rigs still expose the shoulder through the
                // two limb branches from their pelvis-to-head chain.
                int shoulder=-1;
                for(int at=head>=0?bones[head].parent:-1,guard=0;at>=0&&at<bones.Length&&guard++<bones.Length;at=bones[at].parent)
                    if(Enumerable.Range(0,bones.Length).Count(i=>bones[i].parent==at&&!axial.Contains(i))>=2){shoulder=at;break;}
                for(int at=shoulder,guard=0;at>=0&&at<bones.Length&&guard++<bones.Length;at=bones[at].parent)torsoBones[at]=true;
                bleedingTorsoBones[skeleton.slug]=torsoBones;
            }
            var weights=new float[skeleton.bones.Length];
            for(int k=0;k<3;k++){int vertex=hit.Triangles[triangle+k];for(int j=0;j<4;j++){int a=(int)skin.Joints[vertex][j],b=(int)skin.ExtraJoints[vertex][j];if(a>=0&&a<weights.Length)weights[a]+=skin.Weights[vertex][j];if(b>=0&&b<weights.Length)weights[b]+=skin.ExtraWeights[vertex][j];}}
            int owner=0;for(int i=1;i<weights.Length;i++)if(weights[i]>weights[owner])owner=i;return weights.Length>0&&torsoBones[owner];
        }
        var renderer=hit.HumanRenderer;if(renderer==null||renderer.sharedMesh==null)return false;
        var humanBones=renderer.bones;var humanWeights=renderer.sharedMesh.boneWeights;float torso=0,total=0;
        if(hit.Triangles.Skip(triangle).Take(3).Any(i=>i<0||i>=humanWeights.Length))return false;
        for(int k=0;k<3;k++)
        {
            var w=humanWeights[hit.Triangles[triangle+k]];
            void Add(int bone,float weight){total+=weight;if(bone>=0&&bone<humanBones.Length&&TorsoBone(humanBones[bone].name))torso+=weight;}
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
        }
        return torso>total*.5;
    }
    void RegisterCombatWound(CombatHit hit,Possession weapon,double damage,Vector3 direction)
    {
        var profile=ItemUseRules.Profile(weapon);if(profile.Penetration<.15||profile.Primary==ItemPrimaryUse.Blunt||hit?.Vertices==null)return;
        if(hit.Animal!=null&&FaunaBlood.Kind(hit.Animal.Spawn.Profile.slug)!=FaunaBloodKind.Red)return;
        if(hit.Human==humanPlayer&&!SurvivalActive)return;
        double maximum=hit.Animal?.MaximumHealth??100;
        double depth=profile.Primary==ItemPrimaryUse.Thrust?.04+profile.Penetration*.2:.02+profile.Penetration*.1*Math.Min(2,profile.EnergyJ/30);
        var inside=new Ray(hit.Point+direction.normalized*.006f,direction.normalized);
        for(int i=0;i<hit.Triangles.Length;i+=3)
        {
            int a=hit.Triangles[i],b=hit.Triangles[i+1],c=hit.Triangles[i+2];
            if(FaunaCollisionSurface.RayTriangle(inside,hit.Vertices[a],hit.Vertices[b],hit.Vertices[c],(float)depth,out float d,out _,out _)&&d>.001)depth=d;
        }
        BleedingState wound;
        if(hit.Animal!=null)wound=hit.Animal.Bleeding??(hit.Animal.Bleeding=new BleedingState());
        else if(hit.Human==humanPlayer)wound=Vitals.Bleeding??(Vitals.Bleeding=new BleedingState());
        else wound=hit.Human.Bleeding??(hit.Human.Bleeding=new BleedingState());
        BleedingRules.Wound(wound,damage/Math.Max(1,maximum),profile.Penetration,Math.Max(.05,-Vector3.Dot(direction.normalized,hit.Normal)),depth,TorsoContact(hit));wound.LastUpdatedDays=PlayerRules.SolarDays;
        if(hit.Animal!=null)hit.Animal.Wanted=true;
    }
    void AdvanceWound(BleedingState wound,double seconds,double activity)
    {BleedingRules.Advance(wound,seconds,activity);wound.LastUpdatedDays=PlayerRules.SolarDays;}
    void UpdateCombatBleeding()
    {
        if(!Ready||paused||mapOpen||ecologyOpen||chatOpen)return;
        double dt=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime)),now=Time.unscaledTimeAsDouble;
        if(SurvivalActive&&!Vitals.Dead&&Vitals.Bleeding!=null)
        {
            AdvanceWound(Vitals.Bleeding,dt,huntingSpeed/6);Vitals.Health=Math.Min(Vitals.Health,BleedingRules.HealthLimit(Vitals.Bleeding,100));
            if(Vitals.Dead)Vitals.DeathCause=PlayerDeathCause.Combat;
            DropBloodSign(ulong.MaxValue,Vitals.Bleeding,Position,0,now);
        }
        foreach(var animal in animals.Values)
        {
            if(animal.Dead||animal.Bleeding==null)continue;
            AdvanceWound(animal.Bleeding,dt,animal.Speed/Math.Max(.1,animal.Spawn.Profile.runMps));
            double health=BleedingRules.HealthLimit(animal.Bleeding,animal.MaximumHealth);
            if(health<=0)DamageAnimal(animal,animal.Health+.001,animal.Renderer.bounds.center,Vector3.up,0,false);else animal.Health=Math.Min(animal.Health,health);
            DropBloodSign(animal.Spawn.Id,animal.Bleeding,animal.Planet,(int)FaunaBlood.Kind(animal.Spawn.Profile.slug),now);
        }
        foreach(var human in humanNpcs)
        {
            if(human.Health<=0||human.Bleeding==null)continue;
            AdvanceWound(human.Bleeding,dt,human.Speed/6);double health=BleedingRules.HealthLimit(human.Bleeding,100);
            if(health<=0)DamageHuman(human,human.Health+.001,null,Vector3.up);else human.Health=Math.Min(human.Health,health);
            human.TargetSpeed=Math.Min(human.TargetSpeed,6*(float)BleedingRules.Mobility(human.Bleeding));
            DropBloodSign((ulong)(0xf000000000000000UL+(uint)human.Id),human.Bleeding,human.Planet,0,now);
        }
        if(now>=remoteBleedingAt&&wildlifeRecords!=null)
        {
            remoteBleedingAt=now+1;
            foreach(var record in wildlifeRecords.Values)
            {
                if(record.Dead||record.Bleeding==null||animals.ContainsKey(record.Id))continue;
                double elapsed=Math.Max(0,(PlayerRules.SolarDays-record.Bleeding.LastUpdatedDays)*PlayerRules.Settings.RealMinutesPerDay*60);
                AdvanceWound(record.Bleeding,Math.Min(1e9,elapsed),0);record.Health=Math.Min(record.Health,BleedingRules.HealthLimit(record.Bleeding,record.MaximumHealth));
            }
        }
    }
    void DropBloodSign(ulong id,BleedingState wound,V at,int kind,double now)
    {
        if(wound.Rate<.000025||bloodDropAt.TryGetValue(id,out double due)&&now<due)return;
        bloodDropAt[id]=now+Math.Max(.6,Math.Min(3,.003/Math.Max(.0001,wound.Rate)));
        Wildlife.BloodSigns=Wildlife.BloodSigns??new List<BloodSign>();
        var sign=new BloodSign{Position=at,SizeM=Math.Max(.035,Math.Min(.22,.025+Math.Sqrt(wound.Rate)*1.6)),CreatedDays=PlayerRules.SolarDays,Kind=kind};
        // Stationary bleeding pools into the last spot rather than accumulating
        // thousands of identical overlapping decals.
        var prior=Wildlife.BloodSigns.LastOrDefault();
        if(prior!=null&&(prior.Position-at).Length*frame.RadiusM<.15&&prior.Kind==kind){prior.SizeM=Math.Max(prior.SizeM,sign.SizeM);prior.CreatedDays=sign.CreatedDays;}
        else Wildlife.BloodSigns.Add(sign);bloodMeshDirty=true;
    }
    void UpdateBloodSigns()
    {
        if(!Ready)return;double now=Time.unscaledTimeAsDouble;
        bool moved=!ReferenceEquals(bloodMeshFrame,frame)||bloodMeshOriginX!=originX||bloodMeshOriginZ!=originZ;
        if(!moved&&!bloodMeshDirty&&bloodGroundRevision==scatterGroundRevision&&(Position-bloodMeshObserver).Length*frame.RadiusM<8&&now<bloodMeshExpiryAt)return;
        if(!moved&&now<bloodMeshAt)return;bloodMeshAt=now+.5;bloodMeshExpiryAt=now+20;
        var signs=Wildlife.BloodSigns;if(signs==null||signs.Count==0){bloodTraceMesh?.Clear();return;}
        signs.RemoveAll(s=>PlayerRules.SolarDays-s.CreatedDays>3);
        // Keep the logical trail; only nearby display geometry has a budget.
        var visible=signs.Where(s=>(s.Position-Position).Length*frame.RadiusM<65).OrderBy(s=>(s.Position-Position).Length).Take(1024).ToArray();
        if(bloodTraceRoot==null)
        {
            bloodTraceRoot=new GameObject("Ground blood signs",typeof(MeshFilter),typeof(MeshRenderer));bloodTraceRoot.layer=30;bloodTraceRoot.transform.SetParent(transform,false);
            bloodTraceMesh=new Mesh{name="Nearby blood trail"};bloodTraceRoot.GetComponent<MeshFilter>().sharedMesh=bloodTraceMesh;
            bloodTraceMaterial=new Material(Resources.Load<Shader>("WorldGenBloodTrace"));var renderer=bloodTraceRoot.GetComponent<MeshRenderer>();renderer.sharedMaterial=bloodTraceMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var vertices=new Vector3[visible.Length*4];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[visible.Length*6];
        for(int i=0;i<visible.Length;i++)
        {
            var sign=visible[i];TerrainHandoff.Coordinates(frame,sign.Position,out double x,out double z);double floor=VisibleGround(x,z,Ground(x,z));
            double gx=VisibleGround(x+.1,z,floor)-VisibleGround(x-.1,z,floor),gz=VisibleGround(x,z+.1,floor)-VisibleGround(x,z-.1,floor);
            var normal=new Vector3((float)-gx,.2f,(float)-gz).normalized;var right=Vector3.Cross(Vector3.forward,normal).normalized* (float)sign.SizeM;var forward=Vector3.Cross(normal,right).normalized*(float)sign.SizeM;
            var center=new Vector3((float)(x-originX),(float)floor+.012f,(float)(z-originZ));var pigment=BloodPigment(sign.Kind==0?"human":sign.Kind==1?"bees-guild":"orb-weaving-spiders-guild");
            for(int k=0;k<4;k++){vertices[i*4+k]=center+right*(k%2==0?-1:1)+forward*(k<2?-1:1);uv[i*4+k]=new Vector2(k%2,k/2);colors[i*4+k]=pigment;}
            int a=i*4,t=i*6;triangles[t]=a;triangles[t+1]=a+2;triangles[t+2]=a+1;triangles[t+3]=a+1;triangles[t+4]=a+2;triangles[t+5]=a+3;
        }
        bloodTraceMesh.Clear();bloodTraceMesh.vertices=vertices;bloodTraceMesh.uv=uv;bloodTraceMesh.colors=colors;bloodTraceMesh.triangles=triangles;bloodTraceMesh.RecalculateBounds();bloodMeshDirty=false;bloodMeshFrame=frame;bloodMeshOriginX=originX;bloodMeshOriginZ=originZ;bloodMeshObserver=Position;bloodGroundRevision=scatterGroundRevision;
    }
    void DisposeBloodSigns()
    {if(bloodTraceRoot!=null)Destroy(bloodTraceRoot);if(bloodTraceMesh!=null)Destroy(bloodTraceMesh);if(bloodTraceMaterial!=null)Destroy(bloodTraceMaterial);bloodTraceRoot=null;bloodTraceMesh=null;bloodTraceMaterial=null;bloodDropAt.Clear();}
}

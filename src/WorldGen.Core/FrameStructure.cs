using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum FrameJointKind { ForkRest,EndRest,CrossRest,Lash }
    [Serializable] public sealed class FrameJoint
    {
        public string ParentId="";
        public double ParentT,SelfT;
        public V Offset;
        public FrameJointKind Kind;
        public List<Possession> Bindings=new List<Possession>();
        public bool Lashed=>Bindings!=null&&Bindings.Count>0;
        public FrameJoint Copy(){var j=(FrameJoint)MemberwiseClone();j.Bindings=Bindings.Select(p=>p.Copy()).ToList();return j;}
    }
    public sealed class FrameSupport
    {
        public CampObject Member;public bool Supported;public double Stability,LoadKg,CapacityKg;public string Reason="";
    }
    public sealed class FrameAnalysis
    {
        public readonly Dictionary<string,FrameSupport> Nodes=new Dictionary<string,FrameSupport>();
        public readonly List<CampObject> Order=new List<CampObject>();
        public bool Cycle;
    }
    /// <summary>A bounded directed support graph, evaluated after edits, not a
    /// permanent rigidbody simulation. Capacities are conservative game tuning.</summary>
    public static class FrameRules
    {
        public const double ForkT=.78;
        public static bool Fork(Possession p)=>p!=null&&p.Kind==PossessionKind.Branch&&p.Variant%4==2;
        public static bool Material(Possession p)=>WoodCrafting.Wood(p)&&p.HeightM>=.35&&p.WidthM<=.12&&p.WoodSoundness>=.35;
        public static bool Structural(CampObject c)=>c.Kind==CampKind.Stake||c.Kind==CampKind.FrameWood;
        public static bool Binding(Possession p)=>p!=null&&p.MassKg>0&&p.UseDamage>=0&&p.UseDamage<=.5&&(p.Kind==PossessionKind.Cordage||p.Kind==PossessionKind.RawhideStrips||p.Kind==PossessionKind.SinewCord);
        public static double BindingKg(CampObject child,CampObject parent)=>Math.Max(.02,Math.Min(.08,.012+(child.Materials[0].WidthM+parent.Materials[0].WidthM)*.12));
        public static string JointName(FrameJoint j)=>j.Lashed?"Lashed joint":j.Kind==FrameJointKind.ForkRest?"Resting in fork":j.Kind==FrameJointKind.EndRest?"Balanced on post":j.Kind==FrameJointKind.CrossRest?"Resting on wood":"Needs lashing";
        public static bool Lash(CampObject child,CampObject parent,FrameJoint joint,PlayerInventory inventory,IEnumerable<string> available,out string reason)
        {
            reason="";if(joint.Lashed){reason="This joint is already lashed.";return false;}
            var ids=new HashSet<string>(available);var inputs=inventory.Items.Where(p=>ids.Contains(p.Id)&&Binding(p)&&!inventory.Crafts.Any(j=>PortableCrafting.Affects(j,p.Id))).ToArray();
            double required=BindingKg(child,parent);if(inputs.Sum(p=>p.MassKg)+1e-8<required){reason="Requires more cordage, sinew cord or rawhide strips.";return false;}
            foreach(var source in inputs)
            {
                double take=Math.Min(source.MassKg,required);var part=source.Copy();part.MassKg=take;part.NaturalId=0;part.Place=PossessionPlace.Ground;part.ContainerId="";part.BoundContainerId=null;part.GroundMoving=part.InFlight=false;
                if(take>=source.MassKg-1e-8)inventory.Items.Remove(source);
                else{source.MassKg-=take;part.Id="lash-"+Guid.NewGuid().ToString("N");}
                joint.Bindings.Add(part);required-=take;if(required<1e-8)break;
            }
            return true;
        }
        public static List<Possession> Untie(FrameJoint joint)
        {var result=joint.Bindings; joint.Bindings=new List<Possession>();foreach(var p in result)p.UseDamage=Math.Min(1,p.UseDamage+.05);return result;}
        public static FrameAnalysis Analyse(CampState state)
        {
            var a=new FrameAnalysis();var children=new Dictionary<string,List<string>>();var pending=new Dictionary<string,int>();var ready=new Queue<CampObject>();
            foreach(var c in state.Objects.Where(Structural))a.Nodes.Add(c.Id,new FrameSupport{Member=c,LoadKg=c.MassKg});
            foreach(var node in a.Nodes.Values)
            {
                var c=node.Member;children[c.Id]=new List<string>();pending[c.Id]=0;
                if(c.Kind==CampKind.Stake)ready.Enqueue(c);
            }
            foreach(var c in a.Nodes.Values.Select(n=>n.Member).Where(c=>c.Kind==CampKind.FrameWood))
            {
                foreach(var j in c.FrameJoints??new List<FrameJoint>())if(a.Nodes.ContainsKey(j.ParentId)){pending[c.Id]++;children[j.ParentId].Add(c.Id);}
                if(pending[c.Id]==0)ready.Enqueue(c);
            }
            while(ready.Count>0)
            {
                var c=ready.Dequeue();a.Order.Add(c);var n=a.Nodes[c.Id];var p=c.Materials.FirstOrDefault();
                if(p==null){n.Reason="No structural wood.";}
                else if(c.Kind==CampKind.Stake)
                {n.Stability=c.Stability;n.CapacityKg=12000*p.WidthM*p.WidthM*p.WoodSoundness*n.Stability;n.Supported=n.Stability>=.12;n.Reason=n.Supported?"":"Stake is not driven deeply enough.";}
                else
                {
                    int contacts=c.FrameJoints.Count+(c.FrameGrounded?1:0);n.Stability=1;n.Supported=contacts>=2;
                    if(!n.Supported)n.Reason="Requires two separated supports.";
                    double low=c.FrameGrounded?0:1,high=0;
                    foreach(var j in c.FrameJoints)
                    {
                        low=Math.Min(low,j.SelfT);high=Math.Max(high,j.SelfT);
                        if(!a.Nodes.TryGetValue(j.ParentId,out var support)||!support.Supported){n.Supported=false;n.Reason="A supporting piece is missing or unstable.";continue;}
                        bool secured=j.Lashed&&j.Bindings.Sum(b=>b.MassKg*(1-b.UseDamage))>=BindingKg(c,support.Member)*.5;
                        if(j.Kind==FrameJointKind.Lash&&!secured){n.Supported=false;n.Reason="Side joint needs lashing.";}
                        double grip=secured?1:j.Kind==FrameJointKind.ForkRest?.9:j.Kind==FrameJointKind.CrossRest?.65:.45;
                        n.Stability=Math.Min(n.Stability,support.Stability*grip);
                    }
                    if(high-low<.25){n.Supported=false;n.Reason="Supports are too close together along the wood.";}
                    n.Stability*=Math.Min(1,p.WoodSoundness/.65);n.CapacityKg=7000*p.WidthM*p.WidthM*p.WoodSoundness*Math.Min(1.5,1.8/p.HeightM)*Math.Max(.2,n.Stability);
                    if(n.Stability<.045){n.Supported=false;if(n.Reason.Length==0)n.Reason="The loose connections cannot support this piece.";}
                }
                foreach(string id in children[c.Id])if(--pending[id]==0)ready.Enqueue(a.Nodes[id].Member);
            }
            a.Cycle=a.Order.Count!=a.Nodes.Count;
            foreach(var c in a.Order.AsEnumerable().Reverse())
            {
                var n=a.Nodes[c.Id];if(c.Kind!=CampKind.FrameWood||!n.Supported)continue;
                double share=n.LoadKg/Math.Max(1,c.FrameJoints.Count+(c.FrameGrounded?1:0));
                foreach(var j in c.FrameJoints)if(a.Nodes.TryGetValue(j.ParentId,out var parent))parent.LoadKg+=share;
            }
            foreach(var c in a.Order)
            {
                var n=a.Nodes[c.Id];double supportedWeight=c.Kind==CampKind.Stake?Math.Max(0,n.LoadKg-c.MassKg):n.LoadKg;
                if(supportedWeight>n.CapacityKg){n.Supported=false;n.Reason="Too much weight for this wood or its footing.";}
                if(c.Kind==CampKind.FrameWood&&c.FrameJoints.Any(j=>!a.Nodes.TryGetValue(j.ParentId,out var s)||!s.Supported))
                {n.Supported=false;n.Reason="A supporting piece has failed.";}
            }
            return a;
        }
        public static void Validate(CampState state)
        {
            var byId=state.Objects.ToDictionary(c=>c.Id);
            foreach(var c in state.Objects)
            {
                if(c.Kind!=CampKind.FrameWood){if(c.FrameJoints.Count!=0)throw new ArgumentException("Joints must belong to frame wood.");continue;}
                if(c.Materials.Count!=1||!Material(c.Materials[0])||!double.IsFinite(c.FrameAxis.Length)||Math.Abs(c.FrameAxis.Length-1)>.001||!double.IsFinite(c.FrameHeightM)||Math.Abs(c.FrameHeightM)>1e5||!double.IsFinite(c.FrameRoll)||c.FrameJoints.Count<1||c.FrameJoints.Count>2||c.FrameJoints.Count+(c.FrameGrounded?1:0)!=2)throw new ArgumentException("Invalid frame member.");
                if(c.FrameJoints.Select(j=>j.ParentId).Distinct().Count()!=c.FrameJoints.Count)throw new ArgumentException("A frame needs distinct supports.");
                foreach(var j in c.FrameJoints)
                {
                    if(j.ParentId==c.Id||!byId.TryGetValue(j.ParentId,out var parent)||!Structural(parent)||!Enum.IsDefined(typeof(FrameJointKind),j.Kind)||!double.IsFinite(j.ParentT)||j.ParentT<0||j.ParentT>1||!double.IsFinite(j.SelfT)||j.SelfT<0||j.SelfT>1||!double.IsFinite(j.Offset.Length)||j.Offset.Length>.5||j.Bindings.Any(p=>!Binding(p)))throw new ArgumentException("Invalid structural connection.");
                }
            }
            if(Analyse(state).Cycle)throw new ArgumentException("Frame supports cannot form a floating dependency cycle.");
        }
    }
}

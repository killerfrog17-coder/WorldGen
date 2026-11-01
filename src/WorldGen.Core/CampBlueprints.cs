using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public enum CampDesign { LeanTo,Windbreak,DryingRack,MaterialStore,BrushHut }
    public enum CampCover { Brush,Hide,Bark }
    public enum CampStore { Wood,Stone,Vegetation }
    public enum CampPartKind { Post,Beam,Rib,Binding,Cover }
    [Serializable] public sealed class CampPartState
    {
        public string Key=""; public double WorkSeconds;
        public List<Possession> Materials=new List<Possession>();
        public CampPartState Copy()=>new CampPartState{Key=Key,WorkSeconds=WorkSeconds,Materials=Materials.Select(p=>p.Copy()).ToList()};
    }
    [Serializable] public sealed class CampBlueprint
    {
        public int Version=1,Revision;public CampDesign Design;public CampCover Cover;public CampStore Store;
        public double WidthM=2,DepthM=2;public List<CampPartState> Parts=new List<CampPartState>();
        [System.Runtime.Serialization.OptionalField] public double[] FloorHeights;
        public CampBlueprint Copy(){var p=(CampBlueprint)MemberwiseClone();p.Parts=Parts.Select(s=>s.Copy()).ToList();p.FloorHeights=FloorHeights==null?null:(double[])FloorHeights.Clone();return p;}
    }
    public sealed class CampComponent
    {
        public string Key,Name;public CampPartKind Kind;public int Stage;public V[] Points;
        public double LengthM,WidthM,Kg,Seconds;
    }
    public readonly struct CampProtection
    {
        public readonly double Rain,Wind,Shade;
        public CampProtection(double rain,double wind,double shade){Rain=rain;Wind=wind;Shade=shade;}
    }
    public static class CampBlueprints
    {
        public static string Name(CampDesign d)=>d==CampDesign.LeanTo?"Lean-to":d==CampDesign.Windbreak?"Windbreak":d==CampDesign.DryingRack?"Drying rack":d==CampDesign.MaterialStore?"Material store":"Brush hut";
        public static (double minW,double maxW,double minD,double maxD) Limits(CampDesign d)=>d==CampDesign.Windbreak?(1,6,.25,.25):d==CampDesign.DryingRack?(1,3,.7,.7):d==CampDesign.MaterialStore?(1,4,.75,3):d==CampDesign.BrushHut?(2,4,2,4):(1.5,4,1.5,3);
        public static CampBlueprint Create(CampDesign d,double width,double depth,CampCover cover=CampCover.Brush,CampStore store=CampStore.Wood)
        {
            var l=Limits(d);double Snap(double x)=>Math.Round(x*4)/4;
            width=Math.Max(l.minW,Math.Min(l.maxW,Snap(width)));depth=Math.Max(l.minD,Math.Min(l.maxD,Snap(depth)));
            return new CampBlueprint{Design=d,WidthM=width,DepthM=d==CampDesign.BrushHut?width:depth,Cover=cover,Store=store};
        }
        public static double Height(CampBlueprint p)=>(p.Design==CampDesign.Windbreak?1.35:p.Design==CampDesign.DryingRack?1.15:p.Design==CampDesign.MaterialStore?.65:p.Design==CampDesign.BrushHut?Math.Max(1.6,Math.Min(2.2,p.WidthM*.65)):Math.Min(2,p.DepthM*.5+.7))+Math.Max(0,p.FloorHeights?.Max()??0);
        public static double FloorAt(CampBlueprint p,double x,double z)
        {
            if(p.FloorHeights==null)return 0;double u=Math.Max(0,Math.Min(2,(x/p.WidthM+.5)*2)),v=Math.Max(0,Math.Min(2,(z/p.DepthM+.5)*2));int a=Math.Min(1,(int)u),b=Math.Min(1,(int)v);u-=a;v-=b;
            var f=p.FloorHeights;return (f[a*3+b]*(1-u)+f[(a+1)*3+b]*u)*(1-v)+(f[a*3+b+1]*(1-u)+f[(a+1)*3+b+1]*u)*v;
        }
        public static List<CampComponent> Components(CampBlueprint p)
        {
            var list=new List<CampComponent>();double w=p.WidthM,d=p.DepthM,h=Height(p),front=.35+Math.Max(0,p.FloorHeights?.Max()??0);int sequence=0;
            void Wood(CampPartKind kind,V[] points,double width,int stage=1)
            {
                double length=0;for(int i=1;i<points.Length;i++)length+=(points[i]-points[i-1]).Length;
                list.Add(new CampComponent{Key="p"+sequence++,Name=kind==CampPartKind.Post?"Support":kind==CampPartKind.Rib?"Roof rib":"Crosspiece",Kind=kind,Stage=stage,Points=points,LengthM=length+(kind==CampPartKind.Post?.12:0),WidthM=width,Kg=0,Seconds=kind==CampPartKind.Post?120:90});
            }
            void Bar(V a,V b,double width=.025,CampPartKind kind=CampPartKind.Beam,int stage=1){if(kind==CampPartKind.Post)a=new V(a.X,FloorAt(p,a.X,a.Z)-.1,a.Z);Wood(kind,new[]{a,b},width,stage);}
            void Bind(V at)=>list.Add(new CampComponent{Key="p"+sequence++,Name="Lashing",Kind=CampPartKind.Binding,Stage=2,Points=new[]{at},Kg=.02,Seconds=90});
            void Panel(V a,V b,V c,V e)
            {
                double area=V.Cross(b-a,e-a).Length;
                list.Add(new CampComponent{Key="p"+sequence++,Name=p.Cover==CampCover.Hide?"Hide covering":p.Cover==CampCover.Bark?"Bark covering":"Vegetation covering",Kind=CampPartKind.Cover,Stage=3,Points=new[]{a,b,c,e},Kg=Math.Max(.15,area*(p.Cover==CampCover.Hide?.8:p.Cover==CampCover.Bark?.55:1.2)),Seconds=120});
            }
            if(p.Design==CampDesign.BrushHut)
            {
                int n=(int)Math.Ceiling(w*2.5);if(n%2!=0)n++;
                for(int i=0;i<n;i++)
                {
                    double a=(i-1)*Math.PI*2/n;var path=new V[7];
                    for(int j=0;j<7;j++){double t=j/6d*Math.PI/2;double x=Math.Sin(a)*w*.5*Math.Cos(t),z=Math.Cos(a)*w*.5*Math.Cos(t);path[j]=new V(x,h*Math.Sin(t)+FloorAt(p,x,z)*(1-Math.Sin(t)),z);}
                    if(i!=1){Wood(CampPartKind.Rib,path,.018,0);Bind(path[5]);}
                    // Two lower sectors form a usable doorway, with the roof
                    // continuing overhead and no rib across the entrance.
                    {
                        double b=i*Math.PI*2/n;
                        for(int j=0;j<3;j++)
                        {
                            if(i<2&&j<2)continue;
                            V P(double angle,double t){double x=Math.Sin(angle)*w*.5*Math.Cos(t),z=Math.Cos(angle)*w*.5*Math.Cos(t);return new V(x,h*Math.Sin(t)+FloorAt(p,x,z)*(1-Math.Sin(t)),z);}
                            double lo=j*Math.PI/6,hi=(j+1)*Math.PI/6;
                            Panel(P(a,lo),P(b,lo),P(b,hi),P(a,hi));
                        }
                    }
                }
                // A deliberately open entrance between ribs 0 and 1.
            }
            else if(p.Design==CampDesign.LeanTo)
            {
                int bays=(int)Math.Ceiling(w/1.7);
                for(int i=0;i<=bays;i++)
                {
                    double x=-w*.5+w*i/bays;Bar(new V(x,-.1,-d*.5),new V(x,h,-d*.5),.035,CampPartKind.Post,0);Bar(new V(x,-.1,d*.5),new V(x,front,d*.5),.025,CampPartKind.Post,0);
                    Bind(new V(x,h,-d*.5));Bind(new V(x,front,d*.5));
                }
                for(int i=0;i<bays;i++)
                {double a=-w*.5+w*i/bays,b=-w*.5+w*(i+1)/bays;Bar(new V(a,h,-d*.5),new V(b,h,-d*.5),.035);Bar(new V(a,front,d*.5),new V(b,front,d*.5));}
                int ribs=(int)Math.Ceiling(w/.45);
                for(int i=0;i<=ribs;i++){double x=-w*.5+w*i/ribs;Bar(new V(x,h,-d*.5),new V(x,front,d*.5),.018,CampPartKind.Rib);if(i<ribs){double b=-w*.5+w*(i+1)/ribs;Panel(new V(x,h,-d*.5),new V(b,h,-d*.5),new V(b,front,d*.5),new V(x,front,d*.5));}}
            }
            else
            {
                int bays=(int)Math.Ceiling(w/1.4);bool screen=p.Design==CampDesign.Windbreak;
                for(int i=0;i<=bays;i++)for(int side=0;side<(screen?1:2);side++)
                {double x=-w*.5+w*i/bays,z=screen?0:(side==0?-d*.5:d*.5);Bar(new V(x,-.1,z),new V(x,h,z),.03,CampPartKind.Post,0);Bind(new V(x,h,z));}
                for(int i=0;i<bays;i++)
                {
                    double a=-w*.5+w*i/bays,b=-w*.5+w*(i+1)/bays;
                    for(int side=0;side<(screen?1:2);side++){double z=screen?0:(side==0?-d*.5:d*.5);Bar(new V(a,h,z),new V(b,h,z));if(p.Design==CampDesign.MaterialStore)Bar(new V(a,.2,z),new V(b,.2,z));}
                    if(screen)Panel(new V(a,FloorAt(p,a,0),0),new V(b,FloorAt(p,b,0),0),new V(b,h,0),new V(a,h,0));
                }
                if(!screen)for(int i=0;i<=bays;i++){double x=-w*.5+w*i/bays;Bar(new V(x,h,-d*.5),new V(x,h,d*.5));}
            }
            return list.OrderBy(c=>c.Stage).ToList();
        }
        public static CampPartState State(CampBlueprint p,CampComponent c)=>p.Parts.FirstOrDefault(s=>s.Key==c.Key);
        public static double Supplied(CampBlueprint p,CampComponent c)=>State(p,c)?.Materials.Sum(m=>m.MassKg)??0;
        public static bool HasMaterial(CampBlueprint p,CampComponent c)=>c.Kg>0?Supplied(p,c)+1e-8>=c.Kg:State(p,c)?.Materials.Count==1;
        public static bool Done(CampBlueprint p,CampComponent c)=>HasMaterial(p,c)&&(State(p,c)?.WorkSeconds??0)+1e-8>=c.Seconds;
        public static bool Complete(CampBlueprint p)=>Components(p).All(c=>Done(p,c));
        public static int Stage(CampBlueprint p)=>Components(p).Where(c=>!Done(p,c)).Select(c=>c.Stage).DefaultIfEmpty(4).Min();
        public static bool CoverMaterial(CampBlueprint p,Possession item)=>p.Cover==CampCover.Hide?CampRules.Hide(item):p.Cover==CampCover.Bark?item.Kind==PossessionKind.BarkSheet:item.Kind==PossessionKind.DryGrass||item.Kind==PossessionKind.LeafLitter||item.Kind==PossessionKind.ReedBundle;
        public static bool Suitable(CampBlueprint p,CampComponent c,Possession item)
        {
            if(item==null||item.MassKg<=0)return false;
            if(c.Kind==CampPartKind.Binding)return FrameRules.Binding(item);
            if(c.Kind==CampPartKind.Cover)return CoverMaterial(p,item);
            return (item.Kind==PossessionKind.Branch||item.Kind==PossessionKind.TrimmedPole)&&item.UseDamage<.8&&item.WoodSoundness>=.45&&item.HeightM+.035>=c.LengthM&&item.WidthM>=c.WidthM&&item.WidthM<=Math.Max(.085,c.WidthM*2.8)&&
                (p.Design!=CampDesign.BrushHut||item.Kind==PossessionKind.Branch&&item.WidthM<=.04);
        }
        public static string Requirement(CampComponent c)=>c.Kg>0?c.Name+" · "+c.Kg.ToString("0.00")+" kg":c.Name+" · wood ≥"+c.LengthM.ToString("0.00")+" m × "+(c.WidthM*100).ToString("0.0")+" cm";
        public static bool Supply(CampBlueprint p,string key,PlayerInventory inventory,string itemId,bool canCut,out Possession offcut,out string reason)
        {
            offcut=null;reason="";var c=Components(p).FirstOrDefault(c=>c.Key==key);var source=inventory.Find(itemId);
            if(c==null||c.Stage!=Stage(p)||HasMaterial(p,c)||!Suitable(p,c,source)){reason="Material does not fit the current component.";return false;}
            if(inventory.Crafts.Any(j=>PortableCrafting.Affects(j,itemId))){reason="This material is being worked.";return false;}
            if(c.Kg==0&&source.HeightM>c.LengthM+.1&&!canCut){reason="A cutting tool is needed to shorten this wood.";return false;}
            var s=State(p,c);if(s==null){s=new CampPartState{Key=key};p.Parts.Add(s);}
            var piece=source.Copy();piece.Place=PossessionPlace.Ground;piece.ContainerId="";piece.CampOwner=null;piece.BoundContainerId=null;piece.GroundMoving=piece.InFlight=false;
            if(c.Kg>0)
            {
                double take=Math.Min(source.MassKg,Math.Max(0,c.Kg-Supplied(p,c)));
                piece.MassKg=take;piece.NaturalId=0;
                if(take>=source.MassKg-1e-8)inventory.Items.Remove(source);
                else{source.MassKg-=take;piece.Id="build-"+Guid.NewGuid().ToString("N");}
            }
            else
            {
                inventory.Items.Remove(source);
                if(source.HeightM>c.LengthM+.1)
                {
                    double ratio=c.LengthM/source.HeightM;piece.HeightM=c.LengthM;piece.MassKg=source.MassKg*ratio;piece.NaturalId=0;piece.CellsHigh=Math.Max(1,(int)Math.Ceiling(piece.HeightM/.1));
                    offcut=source.Copy();offcut.Id="offcut-"+Guid.NewGuid().ToString("N");offcut.NaturalId=0;offcut.Kind=PossessionKind.Branch;offcut.HeightM=source.HeightM-c.LengthM;offcut.MassKg=source.MassKg-piece.MassKg;offcut.Place=PossessionPlace.Ground;offcut.ContainerId="";offcut.CampOwner=null;offcut.BoundContainerId=null;offcut.GroundMoving=offcut.InFlight=false;offcut.CellsHigh=Math.Max(1,(int)Math.Ceiling(offcut.HeightM/.1));inventory.Items.Add(offcut);
                }
            }
            s.Materials.Add(piece);p.Revision++;return true;
        }
        public static double Work(CampBlueprint p,string key,double seconds)
        {
            if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentException("Invalid construction work.");
            var c=Components(p).FirstOrDefault(c=>c.Key==key);if(c==null||c.Stage!=Stage(p)||!HasMaterial(p,c))return 0;
            var s=State(p,c);double work=Math.Min(seconds,c.Seconds-s.WorkSeconds);s.WorkSeconds+=work;if(work>0&&s.WorkSeconds>=c.Seconds)p.Revision++;return work;
        }
        public static double CoverFraction(CampBlueprint p)
        {var covers=Components(p).Where(c=>c.Kind==CampPartKind.Cover).ToArray();return covers.Length==0?0:covers.Sum(c=>Done(p,c)?Math.Min(c.Kg*1.2,Supplied(p,c)):0)/covers.Sum(c=>c.Kg);}
        public static bool Thicken(CampBlueprint p,PlayerInventory inventory,string id,out string reason)
        {
            reason="";var source=inventory.Find(id);if(source==null||!CoverMaterial(p,source)||inventory.Crafts.Any(j=>PortableCrafting.Affects(j,id))){reason="Suitable loose covering is needed.";return false;}
            var part=Components(p).Where(c=>c.Kind==CampPartKind.Cover&&Done(p,c)&&Supplied(p,c)<c.Kg*2.5-.00001).OrderBy(c=>Supplied(p,c)/c.Kg).FirstOrDefault();
            if(part==null){reason="This covering is already substantial.";return false;}
            double take=Math.Min(source.MassKg,part.Kg*2.5-Supplied(p,part));var piece=source.Copy();piece.MassKg=take;piece.NaturalId=0;piece.Place=PossessionPlace.Ground;piece.ContainerId="";piece.CampOwner=null;
            if(take>=source.MassKg-1e-8)inventory.Items.Remove(source);else{source.MassKg-=take;piece.Id="cover-"+Guid.NewGuid().ToString("N");}
            State(p,part).Materials.Add(piece);p.Revision++;return true;
        }
        public static CampProtection Protection(CampBlueprint p,double x,double z,double windX,double windZ,double knownCover=-1)
        {
            double cover=knownCover>=0?knownCover:CoverFraction(p);if(cover<=0)return default;
            if(p.Design==CampDesign.Windbreak)
            {double down=Math.Sign(windZ);if(down==0||z*down<0||z*down>Height(p)*3||Math.Abs(x-z*windX/Math.Max(.1,Math.Abs(windZ)))>p.WidthM*.5)return default;return new CampProtection(0,Math.Min(.95,.8*cover)*(1-z*down/(Height(p)*3)),0);}
            if(Math.Abs(x)>p.WidthM*.5||Math.Abs(z)>p.DepthM*.5)return default;
            if(p.Design==CampDesign.LeanTo)return new CampProtection(Math.Min(.96,cover*.8),Math.Min(.9,cover*(windZ>0?.65:.22)),Math.Min(.96,cover*.8));
            if(p.Design==CampDesign.BrushHut&&x*x+z*z<=p.WidthM*p.WidthM*.25)return new CampProtection(Math.Min(.98,cover*.92),Math.Min(.95,cover*.85),Math.Min(.98,cover*.92));
            return default;
        }
        public static bool StoreAccepts(CampBlueprint p,Possession item)
        {if(item==null)return false;if(p.Design==CampDesign.DryingRack)return CampRules.Cookable(item)&&!item.Burnt&&!item.Dried;
            return p.Design==CampDesign.MaterialStore&&(p.Store==CampStore.Wood?WoodCrafting.Wood(item):p.Store==CampStore.Stone?StoneCrafting.Rock(item):item.Kind==PossessionKind.DryGrass||item.Kind==PossessionKind.LeafLitter||item.Kind==PossessionKind.ReedBundle||item.Kind==PossessionKind.BarkSheet);}
        public static double Volume(Possession p)=>WoodCrafting.Wood(p)?p.MassKg/220:StoneCrafting.Rock(p)?p.MassKg/1200:p.MassKg/55;
        public static bool Fits(CampBlueprint p,IEnumerable<Possession> contents,Possession item)
        {
            if(!Complete(p)||!StoreAccepts(p,item))return false;var all=contents.Where(c=>c.Id!=item.Id).ToArray();
            if(p.Design==CampDesign.DryingRack)
            {if(item.MassKg>1.5)return false;double left=0,right=0;foreach(var span in all.Concat(new[]{item}).Select(c=>Math.Max(.15,c.WidthM+.08)).OrderByDescending(v=>v)){if(left+span<=p.WidthM)left+=span;else if(right+span<=p.WidthM)right+=span;else return false;}return true;}
            return Math.Max(item.HeightM,Math.Max(item.WidthM,item.DepthM))<=Math.Max(p.WidthM,p.DepthM)+.15&&all.Sum(Volume)+Volume(item)<=p.WidthM*p.DepthM*.6;
        }
        public static void Dry(Possession item,double seconds,double rain,double humidity,double wind,double temperature,bool smoke)
        {
            if(item.Dried||item.Burnt||!CampRules.Cookable(item))return;
            double rate=rain>.2||humidity>.93||temperature<0?0:(.2+Math.Max(0,wind)*.15)*(1-Math.Min(.95,humidity))*(smoke?2.5:1);
            item.DryingSeconds=Math.Max(0,item.DryingSeconds+seconds*rate-(rain>.2?seconds*.15:0));
            if(smoke&&rate>0)item.Smoked=true;
            if(item.DryingSeconds>=21600)
            {item.Dried=true;item.FoodKcalPerKg/=.45;Foraging.Resize(item,item.MassKg*.45);item.FoodWaterPerKg=.12;item.Tint*=.65;}
        }
        public static bool Deposit(CampObject camp,PlayerInventory inventory,string id,out string reason)
        {
            reason="";var p=inventory.Find(id);var plan=camp?.Blueprint;
            if(plan==null||p==null||inventory.Crafts.Any(j=>PortableCrafting.Affects(j,id))||!Fits(plan,inventory.Items.Where(i=>i.Place==PossessionPlace.CampStorage&&i.CampOwner==camp.Id),p)){reason="This item does not fit this store or rack.";return false;}
            p.Place=PossessionPlace.CampStorage;p.CampOwner=camp.Id;p.Position=camp.Position;p.ContainerId="";p.InFlight=p.GroundMoving=false;p.AttachedAnimal=0;p.AttachedHuman=0;p.GroundVelocity=p.GroundSpin=new V();plan.Revision++;return true;
        }
        public static void ValidateContents(CampState camp,PlayerInventory inventory)
        {
            var stores=camp.Objects.Where(c=>c.Blueprint!=null).ToDictionary(c=>c.Id);
            foreach(var item in inventory.Items.Where(i=>i.Place==PossessionPlace.CampStorage))
                if(item.CampOwner==null||!stores.TryGetValue(item.CampOwner,out var owner)||!Complete(owner.Blueprint)||(owner.Blueprint.Design==CampDesign.DryingRack?!CampRules.Cookable(item):!StoreAccepts(owner.Blueprint,item))||!double.IsFinite(item.Position.Length)||Math.Abs(item.Position.Length-1)>.00001)throw new ArgumentException("Invalid camp storage owner.");
        }
        public static void Validate(CampObject c)
        {
            var p=c.Blueprint;if(c.Kind!=CampKind.Blueprint){if(p!=null)throw new ArgumentException("Unexpected construction plan.");return;}
            if(p==null||p.Version!=1||!Enum.IsDefined(typeof(CampDesign),p.Design)||!Enum.IsDefined(typeof(CampCover),p.Cover)||!Enum.IsDefined(typeof(CampStore),p.Store)||p.Parts==null||p.Revision<0||c.Materials.Count>0||(c.CookingItems?.Count??0)>0)throw new ArgumentException("Invalid construction plan.");
            var l=Limits(p.Design);if(!double.IsFinite(p.WidthM)||!double.IsFinite(p.DepthM)||p.WidthM<l.minW||p.WidthM>l.maxW||p.DepthM<l.minD||p.DepthM>l.maxD||p.Design==CampDesign.BrushHut&&p.DepthM!=p.WidthM)throw new ArgumentException("Invalid construction dimensions.");
            if(p.FloorHeights!=null&&(p.FloorHeights.Length!=9||p.FloorHeights.Any(f=>!double.IsFinite(f)||Math.Abs(f)>2)))throw new ArgumentException("Invalid construction footing survey.");
            var defs=Components(p).ToDictionary(d=>d.Key);var keys=new HashSet<string>();
            foreach(var s in p.Parts)
            {if(s==null||!keys.Add(s.Key)||!defs.TryGetValue(s.Key,out var d)||s.Materials==null||!double.IsFinite(s.WorkSeconds)||s.WorkSeconds<0||s.WorkSeconds>d.Seconds+.001||s.Materials.Any(i=>!Suitable(p,d,i))||d.Kg==0&&s.Materials.Count>1||d.Kg>0&&s.Materials.Sum(i=>i.MassKg)>d.Kg*(d.Kind==CampPartKind.Cover?2.5:1)+.001)throw new ArgumentException("Invalid construction contribution.");}
        }
    }
}

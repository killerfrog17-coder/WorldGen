using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Flags] public enum CampFunction { None=0,Shelter=1,Screen=2,Drying=4,Storage=8,Sleeping=16,HideWorking=32 }
    /// <summary>Shared by construction, placement and future settlement generation.
    /// No NPC-specific copy of a building or its material costs is needed.</summary>
    public sealed class CampDesignDefinition
    {
        public CampDesign Design { get; }
        public string Name { get; }
        public string Description { get; }
        public CampFunction Functions { get; }
        public (double minW,double maxW,double minD,double maxD) Limits { get; }
        public bool Round { get; }
        public bool Line=>Limits.minD==Limits.maxD;
        public CampDesignDefinition(CampDesign design,string name,string description,CampFunction functions,double minW,double maxW,double minD,double maxD,bool round=false)
        {Design=design;Name=name;Description=description;Functions=functions;Limits=(minW,maxW,minD,maxD);Round=round;}
    }
    public static partial class CampBlueprints
    {
        static readonly CampDesignDefinition[] catalogue={
            new CampDesignDefinition(CampDesign.LeanTo,"Lean-to","One sloping roof; open front. Width and depth are adjustable.",CampFunction.Shelter,1.5,4,1.5,3),
            new CampDesignDefinition(CampDesign.Windbreak,"Windbreak","A tied vegetation, hide or bark screen. Join ends to enclose a camp.",CampFunction.Screen,1,6,.25,.25),
            new CampDesignDefinition(CampDesign.DryingRack,"Drying rack","Two hanging rails for meat and fish; nearby fire assists smoking.",CampFunction.Drying,1,3,.7,.7),
            new CampDesignDefinition(CampDesign.MaterialStore,"Material store","A low retaining frame for wood, stones or plant bundles.",CampFunction.Storage,1,4,.75,3),
            new CampDesignDefinition(CampDesign.BrushHut,"Dome hut","Bent-branch dome; brush, reeds, bark or hides. Low entrance faces the placement direction.",CampFunction.Shelter,2,4,2,4,true),
            new CampDesignDefinition(CampDesign.ConicalTent,"Conical tent","Lashed poles, fitted covering and stone anchors; front doorway and crown vent.",CampFunction.Shelter,2,6,2,6,true),
            new CampDesignDefinition(CampDesign.RidgeTent,"Ridge tent / A-frame","Two sloping roofs with a closed rear and an open front doorway.",CampFunction.Shelter,1.5,5,2,6),
            new CampDesignDefinition(CampDesign.LongHut,"Oval / long hut","Repeated bent-branch bays for an oval hut or communal shelter.",CampFunction.Shelter,2,4,2,8),
            new CampDesignDefinition(CampDesign.OpenCanopy,"Open canopy","A raised roof over an open work or sleeping area.",CampFunction.Shelter,1.5,5,1.5,5),
            new CampDesignDefinition(CampDesign.BoneLodge,"Bone-supported lodge","A heavy bone footing supports a lashed wooden roof; entrance remains clear.",CampFunction.Shelter,2,6,2,6,true),
            new CampDesignDefinition(CampDesign.StoneShelter,"Stone-founded hut","Dry-laid low walls with a wooden roof and organic covering.",CampFunction.Shelter,2,6,2,6,true),
            new CampDesignDefinition(CampDesign.RaisedBed,"Sleeping platform","A low branch deck with replaceable grass, reed, bark or hide bedding.",CampFunction.Sleeping,1,3,1.75,3),
            new CampDesignDefinition(CampDesign.HideFrame,"Hide-working frame","A braced upright frame; hang one actual hide to work or dry it.",CampFunction.HideWorking|CampFunction.Storage,1,3,.7,.7),
            new CampDesignDefinition(CampDesign.RaisedCache,"Raised cache","An elevated, roofed food and equipment store; does not make food predator-proof.",CampFunction.Storage,1,3,1,2.5),
            new CampDesignDefinition(CampDesign.StoneWindbreak,"Stone windbreak","A low dry-laid stone screen; no bindings or roof required.",CampFunction.Screen,1,6,.5,.5)
        };
        public static IReadOnlyList<CampDesignDefinition> Catalogue=>Array.AsReadOnly(catalogue);
        public static CampDesignDefinition Definition(CampDesign design)
        {int i=(int)design;if(i<0||i>=catalogue.Length)throw new ArgumentOutOfRangeException(nameof(design));return catalogue[i];}
        public static bool Round(CampDesign d)=>Definition(d).Round;
        public static bool Line(CampDesign d)=>Definition(d).Line;
        public static bool Has(CampDesign d,CampFunction function)=>(Definition(d).Functions&function)!=0;
        public static string CoverName(CampCover c)=>c==CampCover.Hide?"Hide covering":c==CampCover.Bark?"Bark covering":c==CampCover.Reed?"Reed thatch":"Vegetation covering";
        public static double CoverKg(CampCover c)=>c==CampCover.Hide?.8:c==CampCover.Bark?.55:c==CampCover.Reed?1.4:1.2;
        public static double NominalHeight(CampBlueprint p)
        {
            switch(p.Design)
            {
                case CampDesign.Windbreak:return p.ScreenHeightM>0?Math.Max(.6,Math.Min(2.2,p.ScreenHeightM)):1.35;
                case CampDesign.StoneWindbreak:return p.ScreenHeightM>0?Math.Max(.5,Math.Min(1.5,p.ScreenHeightM)):1;
                case CampDesign.DryingRack:return p.Version>=2?1.65:1.15;
                case CampDesign.MaterialStore:return .65;
                case CampDesign.RaisedBed:return .45;
                case CampDesign.HideFrame:return 1.8;
                case CampDesign.RaisedCache:return 2.7;
                case CampDesign.ConicalTent:return Math.Max(2,Math.Min(2.8,p.WidthM*.72));
                case CampDesign.RidgeTent:return Math.Max(1.7,Math.Min(2.5,p.WidthM*.65));
                case CampDesign.OpenCanopy:return 2.1;
                case CampDesign.BrushHut:case CampDesign.LongHut:return Math.Max(1.6,Math.Min(2.2,p.WidthM*.65));
                case CampDesign.StoneShelter:case CampDesign.BoneLodge:return Math.Max(1.8,Math.Min(2.5,p.WidthM*.65));
                default:return Math.Min(2,p.DepthM*.5+.7);
            }
        }
        // Metres in local plan coordinates. Openings face +Z. Future camp layout
        // uses this rather than guessing an entrance from mesh bounds.
        public static V Entrance(CampBlueprint p)
        {double z=p.DepthM*.5*(p.Design==CampDesign.LeanTo?-1:1);return new V(0,FloorAt(p,0,z),z);}
        public static double EntranceHeight(CampBlueprint p)
        {
            double floor=Entrance(p).Y;
            return Math.Max(.35,p.Design==CampDesign.RidgeTent?1.5-floor:p.Design==CampDesign.ConicalTent?1.45-floor:p.Design==CampDesign.BrushHut||p.Design==CampDesign.LongHut?NominalHeight(p)*Math.Sin(Math.PI/3):NominalHeight(p));
        }
        public static int SleepingPlaces(CampBlueprint p)=>!Has(p.Design,CampFunction.Shelter)&&!Has(p.Design,CampFunction.Sleeping)?0:Math.Max(1,(int)Math.Floor(p.WidthM*p.DepthM*(Round(p.Design)?.68:.75)/1.7));
        public static bool HasStorage(CampDesign d)=>Has(d,CampFunction.Storage)||Has(d,CampFunction.Drying);
        public static bool HasCover(CampDesign d)=>Has(d,CampFunction.Shelter)||d==CampDesign.Windbreak||d==CampDesign.RaisedBed||d==CampDesign.RaisedCache;

        static List<CampComponent> ShelterComponents(CampBlueprint p)
        {
            var result=new List<CampComponent>();int sequence=0;
            double w=p.WidthM,d=p.DepthM,h=Height(p),rise=Math.Max(0,p.FloorHeights?.Max()??0);
            void Member(V a,V b,double thickness=.03,CampPartKind kind=CampPartKind.Beam,int stage=1)
            {
                if(kind==CampPartKind.Post)a=new V(a.X,FloorAt(p,a.X,a.Z)-.1,a.Z);
                result.Add(new CampComponent{Key="s"+sequence++,Name=kind==CampPartKind.Post?"Support":kind==CampPartKind.Rib?"Roof rib":"Crosspiece",Kind=kind,Stage=stage,Points=new[]{a,b},LengthM=(b-a).Length,WidthM=thickness,Seconds=kind==CampPartKind.Post?120:90});
            }
            void Bind(V at)=>result.Add(new CampComponent{Key="s"+sequence++,Name="Lashing",Kind=CampPartKind.Binding,Stage=2,Points=new[]{at},Kg=.025,Seconds=90});
            void Bulk(CampPartKind kind,V a,V b,double width,double height,double kg,int stage)
            {result.Add(new CampComponent{Key="s"+sequence++,Name=kind==CampPartKind.Stonework?"Stone course":kind==CampPartKind.Bonework?"Bone footing":"Branch decking",Kind=kind,Stage=stage,Points=new[]{a,b},WidthM=width,LengthM=height,Kg=kg,Seconds=kind==CampPartKind.Stonework?240:180});}
            void Panel(V a,V b,V c,V e)
            {
                // Two triangles, including triangular end panels (c == e).
                double area=(V.Cross(b-a,c-a).Length+V.Cross(c-a,e-a).Length)*.5;
                if(area<.00001)return;
                result.Add(new CampComponent{Key="s"+sequence++,Name=CoverName(p.Cover),Kind=CampPartKind.Cover,Stage=3,Points=new[]{a,b,c,e},Kg=Math.Max(.08,area*CoverKg(p.Cover)),Seconds=120});
            }
            void Ballast(V at,double kg=3)=>Bulk(CampPartKind.Stonework,at,new V(at.X+.2,at.Y,at.Z),.18,.12,kg,2);
            V Ground(double x,double z)=>new V(x,FloorAt(p,x,z),z);

            if(p.Design==CampDesign.StoneWindbreak)
            {
                int bays=(int)Math.Ceiling(w/.65);
                for(int level=0;level<4;level++)for(int i=0;i<bays;i++)
                {double x=-w*.5+w*i/bays,y=level*h/4;Bulk(CampPartKind.Stonework,Ground(x,0)+new V(0,y,0),Ground(x+w/bays,0)+new V(0,y,0),.45,h/4,w/bays*.45*h/4*1550,level);}
            }
            else if(p.Design==CampDesign.ConicalTent)
            {
                int sides=Math.Max(10,(int)Math.Ceiling(w*3));if(sides%2!=0)sides++;
                double Angle(int i)=>i*Math.PI*2/sides;
                V At(double a,double t){double x=Math.Sin(a)*w*.5*(1-t),z=Math.Cos(a)*w*.5*(1-t);return new V(x,FloorAt(p,x,z)*(1-t)+h*t,z);}
                // A small crown opening is left above the covering. Support poles
                // extend past the lashing; no central post blocks floor space.
                for(int i=1;i<sides;i++)
                {var foot=At(Angle(i),0);Member(foot,At(Angle(i),1.025),.035,CampPartKind.Post,0);Bind(At(Angle(i),.92));Ballast(foot);}
                for(int i=0;i<sides;i++)for(int band=0;band<3;band++)
                {
                    double lo=band/3d*.96,hi=(band+1)/3d*.96;
                    // Front sectors open to a crouch-height doorway, not a full
                    // roof-height slit or a pole through the entrance.
                    if(i==0||i==sides-1)lo=Math.Max(lo,1.45/h);
                    if(lo<hi)Panel(At(Angle(i),lo),At(Angle(i+1),lo),At(Angle(i+1),hi),At(Angle(i),hi));
                }
            }
            else if(p.Design==CampDesign.LongHut||p.Design==CampDesign.StoneShelter||p.Design==CampDesign.BoneLodge)
            {
                bool round=Round(p.Design);int bays=round?0:Math.Max(1,(int)Math.Ceiling((d-w)/1.4));
                // Elliptical domes for the heavy-footed lodges; a barrel vault
                // with rounded ends for the elongated brush/reed family.
                int sectors=Math.Max(12,(int)Math.Ceiling(w*3));if(sectors%2!=0)sectors++;
                V Dome(double a,double t,double offset=0)
                {double x=Math.Sin(a)*w*.5*Math.Cos(t),z=Math.Cos(a)*Math.Min(w,d)*.5*Math.Cos(t)+offset;return new V(x,h*Math.Sin(t)+FloorAt(p,x,z)*(1-Math.Sin(t)),z);}
                if(round)
                {
                    double baseH=.55+rise;
                    for(int i=0;i<sectors;i++)
                    {
                        double a=(i-1)*Math.PI*2/sectors,b=i*Math.PI*2/sectors;
                        if(i>1)
                        {
                            V g0=Ground(Math.Sin(a)*w*.5,Math.Cos(a)*d*.5),g1=Ground(Math.Sin(b)*w*.5,Math.Cos(b)*d*.5);
                            var kind=p.Design==CampDesign.StoneShelter?CampPartKind.Stonework:CampPartKind.Bonework;
                            Bulk(kind,g0,g1,.32,.55,(g1-g0).Length*(kind==CampPartKind.Stonework?260:35),0);
                        }
                        if(i!=1)
                        {var bottom=Dome(a,0);var top=Dome(a,Math.PI/2*.96);var middle=Dome(a,Math.PI/4);Member(bottom,middle,.035,CampPartKind.Rib);Member(middle,top,.028,CampPartKind.Rib);Bind(middle);Bind(top);}
                        for(int j=0;j<4;j++)
                        {double lo=j*Math.PI/8,hi=(j+1)*Math.PI/8;if(i<2)lo=Math.Max(lo,Math.Asin(Math.Min(.99,1.45/h)));if(lo>=hi)continue;Panel(Dome(a,lo),Dome(b,lo),Dome(b,hi),Dome(a,hi));}
                    }
                }
                else
                {
                    double body=Math.Max(0,d-w),half=body*.5;int rings=Math.Max(1,(int)Math.Ceiling(body/1.3));
                    V Arch(double a,double z){double x=Math.Cos(a)*w*.5;return new V(x,h*Math.Sin(a)+FloorAt(p,x,z)*(1-Math.Sin(a)),z);}
                    for(int k=0;k<=rings;k++)
                    {
                        double z=-half+body*k/rings;
                        for(int j=0;j<6;j++){V a=Arch(j*Math.PI/6,z),b=Arch((j+1)*Math.PI/6,z);Member(a,b,.022,CampPartKind.Rib);if(j%2==0)Bind(b);
                            if(k<rings&&body>.001){double z2=-half+body*(k+1)/rings;Member(a,Arch(j*Math.PI/6,z2),.022);Panel(a,Arch(j*Math.PI/6,z2),Arch((j+1)*Math.PI/6,z2),b);}}
                    }
                    // Hemispherical ends. Doorway occupies two front sectors.
                    for(int end=0;end<2;end++)for(int i=0;i<sectors/2;i++)
                    {
                        double a=(end==0?Math.PI/2:-Math.PI/2)+i*Math.PI/(sectors/2),b=a+Math.PI/(sectors/2),offset=end==0?-half:half;
                        for(int j=0;j<3;j++)
                        {double lo=j*Math.PI/6,hi=(j+1)*Math.PI/6;bool door=end==1&&(i==sectors/4-1||i==sectors/4)&&j<2;if(door)continue;Panel(Dome(a,lo,offset),Dome(b,lo,offset),Dome(b,hi,offset),Dome(a,hi,offset));}
                        if(!(end==1&&i==sectors/4)){Member(Dome(a,0,offset),Dome(a,Math.PI/4,offset),.022,CampPartKind.Rib);Member(Dome(a,Math.PI/4,offset),Dome(a,Math.PI/2,offset),.022,CampPartKind.Rib);}
                    }
                }
            }
            else if(p.Design==CampDesign.RidgeTent)
            {
                int bays=(int)Math.Ceiling(d/1.5);
                for(int k=0;k<=bays;k++)
                {
                    double z=-d*.5+d*k/bays;
                    // Paired end supports form an A, leaving the centre open.
                    for(int side=-1;side<=1;side+=2){Member(Ground(side*w*.5,z),new V(0,h,z),.035,CampPartKind.Post,0);Ballast(Ground(side*w*.5,z));}
                    Bind(new V(0,h,z));
                    if(k<bays){double next=-d*.5+d*(k+1)/bays;Member(new V(0,h,z),new V(0,h,next),.035);
                        for(int side=-1;side<=1;side+=2)Panel(Ground(side*w*.5,z),Ground(side*w*.5,next),new V(0,h,next),new V(0,h,z));}
                }
                Panel(Ground(-w*.5,-d*.5),new V(0,h,-d*.5),Ground(w*.5,-d*.5),Ground(w*.5,-d*.5));
                // Narrow front wings leave a human-sized centre opening.
                double gap=Math.Min(.6,w*.28),door=1.5;
                for(int side=-1;side<=1;side+=2){var outer=Ground(side*w*.5,d*.5);var inner=Ground(side*gap,d*.5);double y=h*(1-gap/(w*.5));Panel(outer,inner,new V(inner.X,y,d*.5),outer);}
                if(h>door){double topGap=w*.5*(1-door/h);Panel(new V(-topGap,door,d*.5),new V(topGap,door,d*.5),new V(0,h,d*.5),new V(0,h,d*.5));}
                if(p.Version>=2)
                {
                    var covers=result.Where(c=>c.Kind==CampPartKind.Cover).ToArray();int folds=h>door?3:2;
                    var wings=covers.Skip(covers.Length-folds).ToArray();
                    foreach(var wing in wings){double x=wing.Points.Average(v=>v.X);var roof=covers.Take(covers.Length-folds-1).Where(c=>c.Points.Average(v=>v.X)*x>=0).OrderByDescending(c=>c.Points.Average(v=>v.Z)).First();roof.Points=roof.Points.Concat(wing.Points).ToArray();roof.Kg+=wing.Kg;result.Remove(wing);}
                }
            }
            else if(p.Design==CampDesign.OpenCanopy||p.Design==CampDesign.RaisedCache||p.Design==CampDesign.RaisedBed)
            {
                bool bed=p.Design==CampDesign.RaisedBed,cache=p.Design==CampDesign.RaisedCache;
                double deck=bed?h:cache?1.25+rise:0,eave=cache?2.15+rise:h-.25;
                int nx=(int)Math.Ceiling(w/1.5),nz=(int)Math.Ceiling(d/1.5);
                for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++)
                {
                    if(!bed&&x>0&&x<nx&&z>0&&z<nz)continue;
                    double a=-w*.5+w*x/nx,b=-d*.5+d*z/nz;Member(Ground(a,b),new V(a,bed?deck:eave,b),.04,CampPartKind.Post,0);Bind(new V(a,bed?deck:eave,b));
                }
                void Grid(double y,bool decking)
                {
                    for(int x=0;x<=nx;x++)for(int z=0;z<nz;z++)Member(new V(-w*.5+w*x/nx,y,-d*.5+d*z/nz),new V(-w*.5+w*x/nx,y,-d*.5+d*(z+1)/nz),.03);
                    for(int z=0;z<=nz;z++)for(int x=0;x<nx;x++)Member(new V(-w*.5+w*x/nx,y,-d*.5+d*z/nz),new V(-w*.5+w*(x+1)/nx,y,-d*.5+d*z/nz),.03);
                    if(decking)for(int x=0;x<nx;x++)Bulk(CampPartKind.Deck,new V(-w*.5+w*x/nx,y,-d*.5),new V(-w*.5+w*(x+1)/nx,y,d*.5),.025,.03,w/nx*d*8,2);
                }
                if(bed||cache)Grid(deck,true);
                if(bed)Panel(new V(-w*.5,deck+.035,-d*.5),new V(w*.5,deck+.035,-d*.5),new V(w*.5,deck+.035,d*.5),new V(-w*.5,deck+.035,d*.5));
                else
                {
                    Grid(eave,false);
                    for(int x=0;x<nx;x++)
                    {double a=-w*.5+w*x/nx,b=-w*.5+w*(x+1)/nx;for(int side=-1;side<=1;side+=2){Member(new V(a,eave,side*d*.5),new V(a,h,0),.025,CampPartKind.Rib);Panel(new V(a,h,0),new V(b,h,0),new V(b,eave,side*d*.5),new V(a,eave,side*d*.5));}Member(new V(a,h,0),new V(b,h,0),.03);}
                    if(cache)for(int side=-1;side<=1;side+=2){Member(new V(side*w*.5,deck,-d*.5),new V(side*w*.5,eave,d*.5),.025);Member(new V(-w*.5,deck,side*d*.5),new V(w*.5,eave,side*d*.5),.025);}
                }
            }
            else if(p.Design==CampDesign.HideFrame)
            {
                for(int side=-1;side<=1;side+=2){Member(Ground(side*w*.5,0),new V(side*w*.5,h,0),.03,CampPartKind.Post,0);Member(Ground(side*w*.5,-d*.5),new V(side*w*.5,h*.7,0),.025);Member(Ground(side*w*.5,d*.5),new V(side*w*.5,h*.7,0),.025);Bind(new V(side*w*.5,h,0));Bind(new V(side*w*.5,.2+rise,0));}
                Member(new V(-w*.5,h,0),new V(w*.5,h,0),.03);Member(new V(-w*.5,.2+rise,0),new V(w*.5,.2+rise,0),.03);
            }
            return result.OrderBy(c=>c.Stage).ToList();
        }
        static CampProtection ShelterProtection(CampBlueprint p,double x,double z,double windX,double windZ,double cover)
        {
            if(!Has(p.Design,CampFunction.Shelter))return default;
            if(Round(p.Design)&&x*x/(p.WidthM*p.WidthM*.25)+z*z/(p.DepthM*p.DepthM*.25)>1)return default;
            if(p.Design==CampDesign.LongHut)
            {double r=p.WidthM*.5,tip=Math.Max(0,Math.Abs(z)-Math.Max(0,(p.DepthM-p.WidthM)*.5));if(x*x+tip*tip>r*r)return default;}
            double wind=p.Design==CampDesign.OpenCanopy?.12:windZ<0?.58:.88;
            return new CampProtection(Math.Min(.98,cover*.9),Math.Min(.96,cover*wind),Math.Min(.98,cover*.94));
        }
    }
}

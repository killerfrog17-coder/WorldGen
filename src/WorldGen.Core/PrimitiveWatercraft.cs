using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
 public static class PrimitiveWatercraft
 {
  public static readonly PortableItem[] Items={new PortableItem{Kind=PossessionKind.Paddle,Name="Carved wooden paddle",Form=PortableForm.Paddle,Width=.12,Height=1.4,Depth=.035,Mass=1.1,Aliases="paddle"}};
  public static readonly PortableRecipe[] Recipes={new PortableRecipe{Id="hew-paddle",Name="Hew a wooden paddle",Output=PossessionKind.Paddle,Primary=new[]{PossessionKind.Branch},MinLength=1.4,MinWidthM=.12,MaxThickness=.25,SoundWood=true,Materials=Array.Empty<CraftMaterialNeed>(),Tools=new[]{CraftCapability.Chop,CraftCapability.Cut},WorkSeconds=7200,Yield=.5}};
  public static bool Design(CampDesign d)=>d==CampDesign.ReedRaft||d==CampDesign.SkinBoat;
  public static bool Boat(CampObject c)=>c?.Kind==CampKind.Blueprint&&c.Blueprint!=null&&Design(c.Blueprint.Design);
  public static List<CampComponent> Components(CampBlueprint p)
  {
   var list=new List<CampComponent>();int id=0;double w=p.WidthM,d=p.DepthM;
   void Part(string name,CampPartKind kind,int stage,V[] points,double width=0,double mass=0,double seconds=180)
   {double length=0;for(int i=1;i<points.Length;i++)length+=(points[i]-points[i-1]).Length;list.Add(new CampComponent{Key="boat"+id++,Name=name,Kind=kind,Stage=stage,Points=points,WidthM=width,LengthM=length,Kg=mass,Seconds=seconds});}
   if(p.Design==CampDesign.ReedRaft)
   {
    foreach(double z in new[]{-d*.3,d*.3})Part("Crosspiece",CampPartKind.Beam,0,new[]{new V(-w*.5,.34,z),new V(w*.5,.34,z)},.045);
    int floats=(int)Math.Ceiling(w/.38);for(int i=0;i<floats;i++){double x=-w*.5+(i+.5)*w/floats;Part("Bound reed float",CampPartKind.Reedwork,1,new[]{new V(x,.19,-d*.5),new V(x,.19,d*.5)},w/floats,30*w*d/(1.5*2.5)/floats,900);}
    for(int i=0;i<floats;i++)foreach(double z in new[]{-d*.3,d*.3})Part("Float lashing",CampPartKind.Binding,2,new[]{new V(-w*.5+(i+.5)*w/floats,.36,z)},mass:.05,seconds:180);
   }
   else
   {
    for(int i=0;i<8;i++){double a=i*Math.PI/8;var path=new V[9];for(int k=0;k<9;k++){double t=(k/8d-.5)*Math.PI;path[k]=new V(Math.Cos(a)*Math.Sin(t)*w*.5,.05+.5*(1-Math.Cos(t)),Math.Sin(a)*Math.Sin(t)*d*.5);}Part("Bent hull rib",CampPartKind.Rib,0,path,.02,seconds:300);}
    for(int i=0;i<12;i++){double a=i*Math.PI/6,b=(i+1)*Math.PI/6;Part("Gunwale",CampPartKind.Beam,1,new[]{new V(Math.Sin(a)*w*.5,.55,Math.Cos(a)*d*.5),new V(Math.Sin(b)*w*.5,.55,Math.Cos(b)*d*.5)},.025);Part("Hull lashing",CampPartKind.Binding,2,new[]{new V(Math.Sin(a)*w*.5,.55,Math.Cos(a)*d*.5)},mass:.025);}
    for(int i=0;i<12;i++)
    {double a=i*Math.PI/6,b=(i+1)*Math.PI/6;V P(double angle,double r,double y)=>new V(Math.Sin(angle)*w*.5*r,y,Math.Cos(angle)*d*.5*r);
     Part("Fitted hide hull",CampPartKind.Cover,3,new[]{P(a,1,.55),P(b,1,.55),P(b,.48,.07),P(a,.48,.07),P(a,.48,.07),P(b,.48,.07),new V(0,.035,0),new V(0,.035,0)},mass:Math.PI*w*d*.25*1.2/12,seconds:300);}
    Part("Sealed hull seams",CampPartKind.Seal,4,new[]{new V(0,.03,0)},mass:.12*w*d/4,seconds:1800);
   }
   return list.OrderBy(c=>c.Stage).ToList();
  }
  public static double Height(CampObject c)=>c.Blueprint.Design==CampDesign.ReedRaft?.4:.55;
  public static double Waterplane(CampObject c)=>c.Blueprint.WidthM*c.Blueprint.DepthM*(c.Blueprint.Design==CampDesign.ReedRaft?.72:.55);
  public static double Draft(CampObject c,double loadKg)=>Math.Max(.035,(c.MassKg+Math.Max(0,loadKg))/(1000*Waterplane(c)));
  public static double CapacityKg(CampObject c)=>Math.Max(0,1000*Waterplane(c)*(Height(c)-.12)-c.MassKg);
  public static bool Seaworthy(CampObject c,double loadKg)=>Boat(c)&&CampBlueprints.Complete(c.Blueprint)&&loadKg<=CapacityKg(c)&&c.Blueprint.Parts.All(p=>p.Weathering<.65);
  public static V Step(CampObject c,double seconds,double forward,double turn,double flowEast,double flowNorth,bool paddle)
  {
   seconds=Math.Max(0,Math.Min(.25,seconds));c.Heading+=Math.Max(-1,Math.Min(1,turn))*45*seconds;
   double speed=paddle?(c.Blueprint.Design==CampDesign.ReedRaft?1.6:2.2)*Math.Max(-.4,Math.Min(1,forward)):0,a=c.Heading*Math.PI/180,relax=1-Math.Exp(-seconds/.8);
   c.BoatVelocityEast+=(flowEast+Math.Sin(a)*speed-c.BoatVelocityEast)*relax;c.BoatVelocityNorth+=(flowNorth+Math.Cos(a)*speed-c.BoatVelocityNorth)*relax;
   return new V(c.BoatVelocityEast*seconds,0,c.BoatVelocityNorth*seconds);
  }
 }
}

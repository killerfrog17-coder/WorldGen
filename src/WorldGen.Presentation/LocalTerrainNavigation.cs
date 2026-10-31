using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public readonly struct MotionWater
    {
        public readonly double Surface,FlowX,FlowZ;
        public MotionWater(double surface,double flowX=0,double flowZ=0){Surface=surface;FlowX=flowX;FlowZ=flowZ;}
    }
    public sealed class TerrainMotion
    {
        public const double EyeHeightM=1.65, Gravity=9.81,WalkSpeedMps=1.5,JogSpeedMps=3.4,RunSpeedMps=6,JumpHeightM=.45;
        public double X,Y,Z,VerticalSpeed,FlySpeed=200;
        public bool Flying,Grounded,NoClip;
        public bool Swimming {get;private set;}
        public double SwimEffort {get;private set;}
        public double WaterEyeOffset;
        public Func<double,double,MotionWater>? Water;
        // Optional host collision solver; invoked for every bounded movement step.
        // Terrain-only hosts keep their existing analytic movement unchanged.
        public Action<TerrainMotion,double,double,double>? SolidContact;
        public void LandOnSolid(double impactSpeed)
        {LandingSpeedMps=Math.Max(LandingSpeedMps,Math.Max(0,impactSpeed));VerticalSpeed=0;Grounded=true;}
        // Largest actual landing this Move call. Consumed by the owning player,
        // never derived from terrain LOD changes or a remembered takeoff height.
        public double LandingSpeedMps {get;private set;}
        public void Place(double x,double z,double ground,bool fly=false,double aboveGround=40)
        {X=x;Z=z;Flying=fly;Swimming=false;Y=ground+(fly?aboveGround:EyeHeightM);VerticalSpeed=0;LandingSpeedMps=0;Grounded=!fly;}
        public void Move(double dt,double forward,double right,double up,double heading,double pitch,bool sprint,bool jump,Func<double,double,double> ground,bool walk=false,double speedScale=1)
        {
            LandingSpeedMps=0;dt=Math.Max(0,Math.Min(.25,dt));if(dt<=0)return;
            int steps=(int)Math.Ceiling(dt/.02);double step=dt/steps;
            for(int i=0;i<steps;i++)
            {
                double x=X,y=Y,z=Z;
                MoveStep(step,forward,right,up,heading,pitch,sprint,jump&&i==0,ground,walk,speedScale);
                if(!NoClip)SolidContact?.Invoke(this,x,y,z);
            }
        }
        void MoveStep(double dt,double forward,double right,double up,double heading,double pitch,bool sprint,bool jump,Func<double,double,double> ground,bool walk,double speedScale)
        {
            double yaw=heading*Math.PI/180,tilt=pitch*Math.PI/180;
            double dx=Math.Sin(yaw)*forward+Math.Cos(yaw)*right,dz=Math.Cos(yaw)*forward-Math.Sin(yaw)*right;
            double length=Math.Sqrt(dx*dx+dz*dz);if(length>1){dx/=length;dz/=length;}
            double rate=(Flying?FlySpeed*(sprint?3:1):walk?WalkSpeedMps:sprint?RunSpeedMps:JogSpeedMps)*dt*Math.Max(0,Math.Min(1,speedScale));
            if(!Flying&&!NoClip&&Water!=null)
            {
                var water=Water(X,Z);double bed=ground(X,Z),feet=Y-EyeHeightM;
                if(double.IsFinite(water.Surface)&&water.Surface-bed>(Swimming?.75:1.15)&&feet<water.Surface-(Swimming?.35:.65))
                {if(!Swimming){LandingSpeedMps=Math.Max(LandingSpeedMps,Math.Max(0,-VerticalSpeed-5));VerticalSpeed=Math.Max(-3,Math.Min(2,VerticalSpeed));}Swimming=true;SwimStep(dt,forward,right,up,yaw,tilt,sprint,ground,speedScale,water);return;}
            }
            Swimming=false;SwimEffort=0;
            if(Flying)
            {
                double cp=Math.Cos(tilt),fx=Math.Sin(yaw)*forward*cp+Math.Cos(yaw)*right,fz=Math.Cos(yaw)*forward*cp-Math.Sin(yaw)*right,fy=-Math.Sin(tilt)*forward+up;
                double magnitude=Math.Sqrt(fx*fx+fy*fy+fz*fz);if(magnitude>1){fx/=magnitude;fy/=magnitude;fz/=magnitude;}
                X+=fx*rate;Z+=fz*rate;Y+=fy*rate;Grounded=false;VerticalSpeed=0;if(!NoClip)Y=Math.Max(Y,ground(X,Z)+EyeHeightM);return;
            }
            double oldGround=ground(X,Z),oldX=X,oldZ=Z,tx=X+dx*rate,tz=Z+dz*rate;
            // Height queries are millimetre quantized. At very high FPS a real
            // movement is shorter than that quantum: probe a fixed minimum span
            // for grade, rather than treating each rounding edge as a cliff.
            const double contactQuantum=.00101,contactSkin=.005,gradeProbe=.1;
            double Grade(double a,double b,double distance)
            {if(distance<=1e-12)return 0;double span=Math.Max(gradeProbe,distance);return (ground(X+(a-X)*span/distance,Z+(b-Z)*span/distance)-oldGround)/span;}
            // The terrain is a continuous height field, not stairs. A per-frame step allowance
            // must not be reused as permission to climb an arbitrary steep continuous grade.
            bool Step(double a,double b)
            {
                double distance=Math.Sqrt((a-X)*(a-X)+(b-Z)*(b-Z)),target=ground(a,b);
                return Grounded?target-oldGround<=distance+contactSkin&&Grade(a,b,distance)<=1+contactQuantum/gradeProbe:target<=Y-EyeHeightM+1e-6;
            }
            if(Grounded)
            {
                double horizontal=rate*Math.Min(1,length),grade=Grade(tx,tz,horizontal);
                if(horizontal>1e-9&&Math.Abs(grade)<=1+contactQuantum/gradeProbe)
                {double factor=1/Math.Sqrt(1+grade*grade);tx=X+(tx-X)*factor;tz=Z+(tz-Z)*factor;}
            }
            if(Step(tx,tz)){X=tx;Z=tz;}else if(Step(tx,Z))X=tx;else if(Step(X,tz))Z=tz;
            double floor=ground(X,Z)+EyeHeightM,travel=Math.Sqrt((X-oldX)*(X-oldX)+(Z-oldZ)*(Z-oldZ));
            if(Grounded&&!jump&&oldGround-(floor-EyeHeightM)<=travel+contactSkin)
            {Y=floor;VerticalSpeed=0;return;}
            if(jump&&Grounded){VerticalSpeed=Math.Sqrt(2*Gravity*JumpHeightM);Grounded=false;}
            double beforeY=Y,beforeSpeed=VerticalSpeed;
            Y+=VerticalSpeed*dt-.5*Gravity*dt*dt;VerticalSpeed-=Gravity*dt;
            if(Y<=floor)
            {
                // Solve at the contact height, rather than using the end-of-step
                // speed (which overestimates at low FPS). A rising floor cannot
                // manufacture impact energy while the player is stationary.
                if(beforeY>=floor&&VerticalSpeed<0)
                    LandingSpeedMps=Math.Max(LandingSpeedMps,Math.Sqrt(Math.Max(0,beforeSpeed*beforeSpeed+2*Gravity*(beforeY-floor))));
                Y=floor;VerticalSpeed=0;Grounded=true;
            }
            else Grounded=false;
        }
        void SwimStep(double dt,double forward,double right,double up,double yaw,double tilt,bool sprint,Func<double,double,double> ground,double scale,MotionWater water)
        {
            double strokes=Math.Min(1,Math.Sqrt(forward*forward+right*right+up*up));
            SwimEffort=.22+strokes*(sprint?.63:.28); // Drift is not a swimming stroke.
            double dx=Math.Sin(yaw)*Math.Cos(tilt)*forward+Math.Cos(yaw)*right,dz=Math.Cos(yaw)*Math.Cos(tilt)*forward-Math.Sin(yaw)*right;
            double vertical=-Math.Sin(tilt)*forward+up,magnitude=Math.Sqrt(dx*dx+dz*dz+vertical*vertical);
            if(magnitude>1){dx/=magnitude;dz/=magnitude;vertical/=magnitude;}
            double speed=(sprint?2.7:1.45)*Math.Max(.15,Math.Min(1,scale));
            double oldX=X,oldZ=Z,tx=X+(dx*speed+Math.Max(-4,Math.Min(4,water.FlowX)))*dt,tz=Z+(dz*speed+Math.Max(-4,Math.Min(4,water.FlowZ)))*dt;
            bool Pass(double a,double b)=>ground(a,b)<=Math.Max(Y-EyeHeightM+.24,water.Surface-.55);
            if(Pass(tx,tz)){X=tx;Z=tz;}else if(Pass(tx,Z))X=tx;else if(Pass(X,tz))Z=tz;
            double target=water.Surface+.12-WaterEyeOffset,wanted=Math.Abs(vertical)>.08?vertical*1.6:Math.Max(-1.2,Math.Min(1.2,(target-Y)*3));
            if(Y>target&&wanted>0)wanted=0;
            VerticalSpeed+=(wanted-VerticalSpeed)*(1-Math.Exp(-dt*7));Y+=VerticalSpeed*dt;
            if(Y>target&&VerticalSpeed>0){Y=target;VerticalSpeed=0;}
            double floor=ground(X,Z)+EyeHeightM;if(Y<floor){Y=floor;VerticalSpeed=Math.Max(0,VerticalSpeed);}
            Grounded=water.Surface-(floor-EyeHeightM)<.85;
            if(Grounded){Swimming=false;VerticalSpeed=0;}
        }
    }

    public struct TerrainTileKey : IEquatable<TerrainTileKey>
    {
        public int X,Z,Level;public double Scale;
        public TerrainTileKey(int x,int z,int level,double scale=1){X=x;Z=z;Level=level;Scale=scale;}
        public double SizeM=>TerrainLod.BaseSizeM*Scale*(1<<Level);
        public bool Equals(TerrainTileKey b)=>X==b.X&&Z==b.Z&&Level==b.Level&&Scale==b.Scale;
        public override bool Equals(object o)=>o is TerrainTileKey k&&Equals(k);
        public override int GetHashCode()=>unchecked(X*73856093^Z*19349663^Level*83492791^Scale.GetHashCode());
    }
    public static class TerrainLod
    {
        // Smaller work units, not lower detail: 1024/64 retains the old
        // 2048/128 vertex spacing while completing a nearby tile in 1/4 the work.
        public const double BaseSizeM=1024;
        public static double ReplanDistance(double scale)=>BaseSizeM*scale*.25;
        public static double DistanceSquared(TerrainTileKey k,double x,double z)
        {
            double a=k.X*k.SizeM,b=k.Z*k.SizeM;
            double dx=Math.Max(Math.Max(a-x,0),x-a-k.SizeM),dz=Math.Max(Math.Max(b-z,0),z-b-k.SizeM);
            return dx*dx+dz*dz;
        }
        public static bool Immediate(TerrainTileKey k,double x,double z)=>DistanceSquared(k,x,z)<=2.25*BaseSizeM*BaseSizeM*k.Scale*k.Scale;
        public static double[] EdgeSteps(TerrainTileKey tile,IReadOnlyDictionary<TerrainTileKey,int> leaves)
        {
            var steps=new double[4];double x=tile.X*tile.SizeM,z=tile.Z*tile.SizeM;
            foreach(var p in leaves)
            {
                var k=p.Key;double a=k.X*k.SizeM,b=k.Z*k.SizeM,step=k.SizeM/p.Value;
                if(Math.Max(z,b)<Math.Min(z+tile.SizeM,b+k.SizeM))
                {if(a+k.SizeM==x)steps[0]=Math.Max(steps[0],step);if(a==x+tile.SizeM)steps[1]=Math.Max(steps[1],step);}
                if(Math.Max(x,a)<Math.Min(x+tile.SizeM,a+k.SizeM))
                {if(b+k.SizeM==z)steps[2]=Math.Max(steps[2],step);if(b==z+tile.SizeM)steps[3]=Math.Max(steps[3],step);}
            }
            return steps;
        }
        public static Dictionary<TerrainTileKey,int> Plan(double x,double z,double aboveGround,double distanceM=200000,double scale=1)
        {
            if(scale<.01||scale>2||double.IsNaN(scale))throw new ArgumentOutOfRangeException(nameof(scale));
            var result=new Dictionary<TerrainTileKey,int>();int root=Math.Max(0,(int)Math.Floor(Math.Log(128/scale,2)));double size=BaseSizeM*scale*(1<<root);
            void Visit(TerrainTileKey k)
            {
                double d=Math.Sqrt(DistanceSquared(k,x,z));if(d>distanceM)return;
                if(k.Level>0&&(Immediate(k,x,z)||Math.Max(d,aboveGround*.8)<k.SizeM*1.6))
                    for(int j=0;j<2;j++)for(int i=0;i<2;i++)Visit(new TerrainTileKey(k.X*2+i,k.Z*2+j,k.Level-1,scale));
                else result.Add(k,k.Level==0?64:k.Level==1?(d<3000*scale?128:64):k.Level==2?64:k.Level==3?32:k.Level<6?16:8);
            }
            for(int b=(int)Math.Floor((z-distanceM)/size);b<=(int)Math.Floor((z+distanceM)/size);b++)
                for(int a=(int)Math.Floor((x-distanceM)/size);a<=(int)Math.Floor((x+distanceM)/size);a++)Visit(new TerrainTileKey(a,b,root,scale));
            return result;
        }
    }

    public sealed class TerrainMapViewport
    {
        public double Longitude,Latitude,LongitudeSpan=360;
        public double LatitudeSpan=>Math.Min(180,LongitudeSpan*.5);
        public TerrainMapViewport Clone()=>(TerrainMapViewport)MemberwiseClone();
        public static double Wrap(double longitude)=>(longitude+180)%360<0?(longitude+180)%360+180:(longitude+180)%360-180;
        public void Pan(double horizontalFraction,double verticalFraction)
        {Longitude=Wrap(Longitude-horizontalFraction*LongitudeSpan);Latitude=ClampLatitude(Latitude+verticalFraction*LatitudeSpan);}
        double ClampLatitude(double latitude)=>Math.Max(-90+LatitudeSpan/2,Math.Min(90-LatitudeSpan/2,latitude));
        public void Zoom(double factor,double u=.5,double v=.5)
        {
            double lon=Longitude+(u-.5)*LongitudeSpan,lat=Latitude+(.5-v)*LatitudeSpan;
            LongitudeSpan=Math.Max(1e-9,Math.Min(360,LongitudeSpan*factor));
            Longitude=Wrap(lon-(u-.5)*LongitudeSpan);Latitude=ClampLatitude(lat-(.5-v)*LatitudeSpan);
        }
        public V Position(double u,double v)=>FromDegrees(Latitude+(.5-v)*LatitudeSpan,Longitude+(u-.5)*LongitudeSpan);
        public static V FromDegrees(double latitude,double longitude)
        {double a=latitude*Math.PI/180,b=longitude*Math.PI/180;return new V(Math.Cos(a)*Math.Cos(b),Math.Cos(a)*Math.Sin(b),Math.Sin(a));}
        public static void Degrees(V p,out double latitude,out double longitude)
        {latitude=Math.Asin(Math.Max(-1,Math.Min(1,p.Z)))*180/Math.PI;longitude=Math.Atan2(p.Y,p.X)*180/Math.PI;}
    }

    public sealed class TerrainMapImage
    {
        public byte[] Rgba=Array.Empty<byte>();public int Width,Height;public TerrainMapViewport View=new TerrainMapViewport();
        public static TerrainMapImage Build(LocalTerrain terrain,TerrainMapViewport view,int width=384,int height=192,CancellationToken token=default,Layer layer=Layer.Terrain,float[]? cellColors=null,World? world=null)
            =>TerrainMapSampling.Build(terrain,view,width,height,token,layer,cellColors,world);
    }
}

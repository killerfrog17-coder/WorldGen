using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class TerrainMotion
    {
        public const double EyeHeightM=1.65, Gravity=9.81,WalkSpeedMps=1.5,RunSpeedMps=5,JumpHeightM=.45;
        public double X,Y,Z,VerticalSpeed,FlySpeed=200;
        public bool Flying,Grounded;
        public void Place(double x,double z,double ground,bool fly=false,double aboveGround=40)
        {X=x;Z=z;Flying=fly;Y=ground+(fly?aboveGround:EyeHeightM);VerticalSpeed=0;Grounded=!fly;}
        public void Move(double dt,double forward,double right,double up,double heading,double pitch,bool sprint,bool jump,Func<double,double,double> ground)
        {
            dt=Math.Max(0,Math.Min(.25,dt));if(dt<=0)return;
            int steps=(int)Math.Ceiling(dt/.02);double step=dt/steps;
            for(int i=0;i<steps;i++)MoveStep(step,forward,right,up,heading,pitch,sprint,jump&&i==0,ground);
        }
        void MoveStep(double dt,double forward,double right,double up,double heading,double pitch,bool sprint,bool jump,Func<double,double,double> ground)
        {
            double yaw=heading*Math.PI/180,tilt=pitch*Math.PI/180;
            double dx=Math.Sin(yaw)*forward+Math.Cos(yaw)*right,dz=Math.Cos(yaw)*forward-Math.Sin(yaw)*right;
            double length=Math.Sqrt(dx*dx+dz*dz);if(length>1){dx/=length;dz/=length;}
            double rate=(Flying?FlySpeed*(sprint?3:1):sprint?RunSpeedMps:WalkSpeedMps)*dt;
            if(Flying)
            {
                double cp=Math.Cos(tilt),fx=Math.Sin(yaw)*forward*cp+Math.Cos(yaw)*right,fz=Math.Cos(yaw)*forward*cp-Math.Sin(yaw)*right,fy=-Math.Sin(tilt)*forward+up;
                double magnitude=Math.Sqrt(fx*fx+fy*fy+fz*fz);if(magnitude>1){fx/=magnitude;fy/=magnitude;fz/=magnitude;}
                X+=fx*rate;Z+=fz*rate;Y+=fy*rate;Grounded=false;VerticalSpeed=0;Y=Math.Max(Y,ground(X,Z)+EyeHeightM);return;
            }
            double oldGround=ground(X,Z),oldX=X,oldZ=Z,tx=X+dx*rate,tz=Z+dz*rate;
            // The terrain is a continuous height field, not stairs. A per-frame step allowance
            // must not be reused as permission to climb an arbitrary steep continuous grade.
            bool Step(double a,double b)
            {
                double distance=Math.Sqrt((a-X)*(a-X)+(b-Z)*(b-Z)),target=ground(a,b);
                return Grounded?target-oldGround<=distance+1e-6:target<=Y-EyeHeightM+1e-6;
            }
            if(Grounded)
            {
                double rise=ground(tx,tz)-oldGround,horizontal=rate*Math.Min(1,length);
                if(horizontal>1e-9&&Math.Abs(rise)<=horizontal+1e-6)
                {double surface=Math.Sqrt(horizontal*horizontal+rise*rise);tx=X+(tx-X)*horizontal/surface;tz=Z+(tz-Z)*horizontal/surface;}
            }
            if(Step(tx,tz)){X=tx;Z=tz;}else if(Step(tx,Z))X=tx;else if(Step(X,tz))Z=tz;
            double floor=ground(X,Z)+EyeHeightM,travel=Math.Sqrt((X-oldX)*(X-oldX)+(Z-oldZ)*(Z-oldZ));
            if(Grounded&&!jump&&oldGround-(floor-EyeHeightM)<=travel+1e-6)
            {Y=floor;VerticalSpeed=0;return;}
            if(jump&&Grounded){VerticalSpeed=Math.Sqrt(2*Gravity*JumpHeightM);Grounded=false;}
            Y+=VerticalSpeed*dt-.5*Gravity*dt*dt;VerticalSpeed-=Gravity*dt;
            if(Y<=floor){Y=floor;VerticalSpeed=0;Grounded=true;}else Grounded=false;
        }
    }

    public struct TerrainTileKey : IEquatable<TerrainTileKey>
    {
        public int X,Z,Level;
        public TerrainTileKey(int x,int z,int level){X=x;Z=z;Level=level;}
        public double SizeM=>2048.0*(1<<Level);
        public bool Equals(TerrainTileKey b)=>X==b.X&&Z==b.Z&&Level==b.Level;
        public override bool Equals(object o)=>o is TerrainTileKey k&&Equals(k);
        public override int GetHashCode()=>unchecked(X*73856093^Z*19349663^Level*83492791);
    }
    public static class TerrainLod
    {
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
        public static Dictionary<TerrainTileKey,int> Plan(double x,double z,double aboveGround,double distanceM=200000)
        {
            var result=new Dictionary<TerrainTileKey,int>();const int root=6;double size=2048*(1<<root);
            double Distance(TerrainTileKey k){double a=k.X*k.SizeM,b=k.Z*k.SizeM,dx=Math.Max(Math.Max(a-x,0),x-a-k.SizeM),dz=Math.Max(Math.Max(b-z,0),z-b-k.SizeM);return Math.Sqrt(dx*dx+dz*dz);}
            void Visit(TerrainTileKey k)
            {
                double d=Distance(k);if(d>distanceM)return;
                if(k.Level>0&&Math.Max(d,aboveGround*.8)<k.SizeM*1.6)
                    for(int j=0;j<2;j++)for(int i=0;i<2;i++)Visit(new TerrainTileKey(k.X*2+i,k.Z*2+j,k.Level-1));
                else result.Add(k,k.Level==0?(d<3000?128:64):k.Level==1?64:k.Level==2?32:k.Level<5?16:8);
            }
            for(int b=(int)Math.Floor((z-distanceM)/size);b<=(int)Math.Floor((z+distanceM)/size);b++)
                for(int a=(int)Math.Floor((x-distanceM)/size);a<=(int)Math.Floor((x+distanceM)/size);a++)Visit(new TerrainTileKey(a,b,root));
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
        public static TerrainMapImage Build(LocalTerrain terrain,TerrainMapViewport view,int width=384,int height=192,CancellationToken token=default)
        {
            var image=new TerrainMapImage{Width=width,Height=height,View=view.Clone(),Rgba=new byte[width*height*4]};
            var center=view.Position(.5,.5);double extent=D.Angle(center,view.Position(0,0))*terrain.RadiusM*1.12+20;
            var patch=view.LongitudeSpan<1&&extent<100000?terrain.Patch(center,Math.Max(100,extent)):null;
            for(int y=0;y<height;y++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<width;x++)
                {
                    V p=view.Position((x+.5)/width,1-(y+.5)/height);double h=patch==null?terrain.Profile(terrain.NearestCell(p)).ElevationM:patch.Sample(p).ElevationM;
                    V color=GlobeView.Elevation((int)h);int i=(y*width+x)*4;
                    image.Rgba[i]=(byte)(255*D.Clamp(color.X));image.Rgba[i+1]=(byte)(255*D.Clamp(color.Y));image.Rgba[i+2]=(byte)(255*D.Clamp(color.Z));image.Rgba[i+3]=255;
                }
            }
            return image;
        }
    }
}

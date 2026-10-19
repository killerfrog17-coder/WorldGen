using System;

namespace WorldGen.Core
{
    public struct LocalRockProfile
    {
        public double Exposure,LooseRock,Outcrop,JointSpacingM,Roundness;
        public RockFamily Family;
    }

    /// <summary>Replaceable local lithology/rock exposure over accepted heights.
    /// Planet-addressed metre noise, no per-chunk seeds or changes to historical soils.</summary>
    public static class LocalGeology
    {
        public const string Version="local-geology-1";
        static double Smooth(double a,double b,double x){double t=D.Clamp((x-a)/(b-a));return t*t*(3-2*t);}
        public static LocalRockProfile Describe(GroundProfile ground,V position,int seed,double radiusM,double up=1)
        {
            V m=position*radiusM;
            double province=.5+.5*SphereNoise.Sample(m,seed^0x1b37,.00035);
            double joints=.5+.5*SphereNoise.Sample(m,seed^0x5e73,.006);
            double barePatch=Smooth(.55,.78,province)*Smooth(.38,.72,joints);
            double burial=D.Clamp((ground.DepthM-.2)/1.2)*(1-Smooth(.08,.4,ground.Gravel));
            double slope=D.Clamp((1-up-.02)/.45);
            double hard=1-.65*ground.RockWeight((int)RockFamily.Mudstone);
            double exposure=D.Clamp((barePatch*.9*(1-burial)+slope*.85+(1-D.Clamp(ground.DepthM/.3))*.45)*hard);
            double loose=D.Clamp((.05+ground.Gravel*.8+slope*.45+barePatch*.45)*(1-burial)*(.2+.8*Smooth(.20,.66,joints)));
            // Deep alluvium/dune sand can genuinely have no loose rocks or outcrops.
            loose*=Smooth(.035,.16,ground.Gravel+slope*.2);
            double special=Smooth(.68,.84,province)*Smooth(.46,.7,joints)*(1-burial)*hard;
            double round=ground.RockWeight(0)*.85+ground.RockWeight(3)*.45+ground.Weathering*.25;
            return new LocalRockProfile{Exposure=exposure,LooseRock=loose,Outcrop=special,Family=ground.Bedrock,
                JointSpacingM=1.2+6*(1-ground.Weathering)+5*ground.RockWeight(0),Roundness=D.Clamp(round)};
        }
        public static GroundSurface Surface(GroundProfile ground,V position,int seed,double elevation,double up,double ocean,double ice,double radiusM)
        {
            var surface=GroundEnvironment.Surface(ground,position,seed,elevation,up,ocean,ice,radiusM);
            var geology=Describe(ground,position,seed,radiusM,up);
            double rock=geology.Exposure*(1-ice)*(1-D.Clamp(ocean*10)*D.Clamp((8-Math.Abs(elevation))/6));
            double change=Math.Max(0,rock-surface.Rock),sum=1-surface.Rock-surface.Ice;
            double keep=sum>0?Math.Max(0,1-change/sum):1;
            surface.Soil*=keep;surface.Sand*=keep;surface.Gravel*=keep;surface.Clay*=keep;
            surface.Organic*=keep;surface.Salt*=keep;surface.Rock+=change;
            surface.Color=ground.SoilColor*surface.Soil+ground.SandColor*surface.Sand+ground.RockColor*(surface.Rock+.85*surface.Gravel)+ground.SoilColor*(.94*surface.Clay)+new V(.18,.14,.10)*surface.Organic+new V(.78,.75,.65)*surface.Salt+new V(.86,.89,.91)*surface.Ice;
            return surface;
        }
    }
}

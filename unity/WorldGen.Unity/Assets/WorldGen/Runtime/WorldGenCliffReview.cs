using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool ReviewCliffWall(out Vector3 target,out Vector3 outward)
    {
        target=outward=default;
        // Inspect actual resident ground, independent of synthetic cliff masses.
        // Uploaded height grids supply cheap candidate slopes; only the nearest
        // candidates require exact physical terrain and clear-camera checks.
        var candidates=new System.Collections.Generic.List<(double X,double Z,double Distance)>();
        foreach(var pair in tiles)
        {
            var tile=pair.Value;int segments=tile.Segments,side=segments+1;
            if(segments<4||tile.Heights==null||tile.Heights.Length<side*side)continue;
            double spacing=pair.Key.SizeM/segments;if(spacing>12)continue;
            for(int z=1;z<segments;z+=2)for(int x=1;x<segments;x+=2)
            {
                int index=z*side+x;
                double sx=(tile.Heights[index+1]-tile.Heights[index-1])/(2*spacing);
                double sz=(tile.Heights[index+side]-tile.Heights[index-side])/(2*spacing);
                if(sx*sx+sz*sz<1.4)continue;
                double px=pair.Key.X*pair.Key.SizeM+x*spacing,pz=pair.Key.Z*pair.Key.SizeM+z*spacing,distance=px*px+pz*pz;
                if(distance<1200*1200)candidates.Add((px,pz,distance));
            }
        }
        foreach(var source in candidates.OrderBy(p=>p.Distance).ThenBy(p=>p.X).ThenBy(p=>p.Z).Take(128))
        {
            V at=frame.Position(source.X,source.Z);
            var patch=terrain.Patch(at,12000);var sample=patch.Sample(at);if(sample.Underwater)continue;
            V e=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,n=V.Cross(at,e).Unit;double step=4/(terrain.RadiusM*HorizontalScale);
            double sx=(patch.Elevation((at+e*step).Unit)-patch.Elevation((at-e*step).Unit))/8;
            double sz=(patch.Elevation((at+n*step).Unit)-patch.Elevation((at-n*step).Unit))/8;
            V normal=(at-e*sx-n*sz).Unit;double up=V.Dot(normal,at);if(up>.65)continue;
            var material=LocalGeology.Surface(sample.Ground,at,terrain.SourceSeed,sample.ElevationM,up,sample.OceanInfluence,0,terrain.RadiusM*HorizontalScale,sample.Province.ExposedRock);
            if(material.Rock<.6)continue;
            TerrainHandoff.Coordinates(frame,at,out double x,out double z);
            var point=new Vector3((float)(x-originX),(float)frame.SurfaceY(at,sample.ElevationM)+.5f,(float)(z-originZ));
            var face=new Vector3((float)V.Dot(normal,frame.East),(float)V.Dot(normal,frame.Origin),(float)V.Dot(normal,frame.North)).normalized;
            var eye=point+face*70+Vector3.up*15;bool clear=true;
            for(int i=0;i<16;i++)
            {
                var q=Vector3.Lerp(eye,point,i/16f);var p=frame.Position(q.x+originX,q.z+originZ);
                if(q.y-frame.SurfaceY(p,patch.Elevation(p))<.8){clear=false;break;}
            }
            if(!clear)continue;target=point;outward=face;return true;
        }
        return false;
    }
}

using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class CombatObstacle
    {public float Distance;public Vector3 Point,Normal;public bool Tree;public ScatterInstance Source;}
    CombatObstacle TraceCombatObstacle(Ray worldRay,float reach,Collider ignore=null,bool checkGround=true)
    {
        CombatObstacle hit=null;float closest=reach;
        var ray=new Ray(transform.InverseTransformPoint(worldRay.origin),transform.InverseTransformDirection(worldRay.direction));
        foreach(var physical in Physics.RaycastAll(worldRay,reach,~(1<<29),QueryTriggerInteraction.Ignore))
            if(physical.collider!=ignore&&physical.distance<=closest){closest=physical.distance;hit=new CombatObstacle{Distance=closest,Point=physical.point,Normal=physical.normal};}
        for(float d=.08f;checkGround&&d<closest;d+=.18f)
        {
            var p=ray.GetPoint(d);double floor=VisibleGround(p.x+originX,p.z+originZ,Ground(p.x+originX,p.z+originZ));
            if(p.y<floor){closest=d;hit=new CombatObstacle{Distance=d,Point=worldRay.GetPoint(d),Normal=Vector3.up};break;}
        }
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(ray.origin)>closest*closest+4)continue;
            if(!chunk.AllBounds.IntersectRay(ray,out float entry)||entry>closest)continue;
            foreach(var group in chunk.Groups)
            {
                if(!group.Bounds.IntersectRay(ray,out entry)||entry>closest)continue;
                foreach(int index in group.Indices)
                {
                    var p=chunk.Source.Instances[index];bool tree=p.Kind==ScatterKind.GeneratedTree||p.Kind==ScatterKind.Broadleaf||p.Kind==ScatterKind.Conifer||p.Kind==ScatterKind.SavannaTree;
                    if(!tree&&p.Kind!=ScatterKind.Boulder&&p.Kind!=ScatterKind.Outcrop&&p.Kind!=ScatterKind.Stone)continue;
                    if(!tree&&CollectedRock(p))continue;Vector3 normal;float distance;
                    if(tree)
                    {
                        float radius=(float)p.WidthM*(p.Kind==ScatterKind.GeneratedTree?(float)new TreeMorphology(p.Variant).TrunkRadius:.035f);
                        if(!RayTrunk(ray,chunk.Roots[index],Mathf.Max(.015f,radius),(float)p.HeightM*.88f,closest,out distance,out normal))continue;
                    }
                    else
                    {
                        var bounds=new Bounds(chunk.Roots[index]+Vector3.up*(float)p.HeightM*.5f,new Vector3((float)p.WidthM,(float)p.HeightM,(float)p.WidthM*.86f));
                        if(!bounds.IntersectRay(ray,out distance)||distance>closest)continue;normal=-ray.direction;
                    }
                    closest=distance;hit=new CombatObstacle{Distance=distance,Point=worldRay.GetPoint(distance),Normal=transform.TransformDirection(normal),Tree=tree,Source=p};
                }
            }
        }
        Ground(motion.X,motion.Z);return hit;
    }
    static bool RayTrunk(Ray ray,Vector3 root,float radius,float height,float maximum,out float distance,out Vector3 normal)
    {
        distance=maximum;normal=Vector3.up;bool found=false;var o=ray.origin-root;var d=ray.direction;
        float a=d.x*d.x+d.z*d.z,b=2*(o.x*d.x+o.z*d.z),c=o.x*o.x+o.z*o.z-radius*radius;
        if(a>1e-10f)
        {
            float discriminant=b*b-4*a*c;
            if(discriminant>=0)
            {
                float square=Mathf.Sqrt(discriminant);
                for(int side=0;side<2;side++)
                {
                    float t=(-b+(side==0?-square:square))/(2*a),y=o.y+d.y*t;
                    if(t>=0&&t<=distance&&y>=0&&y<=height){distance=t;normal=new Vector3(o.x+d.x*t,0,o.z+d.z*t).normalized;found=true;}
                }
            }
        }
        if(Mathf.Abs(d.y)>1e-8f)for(int side=0;side<2;side++)
        {
            float t=((side==0?0:height)-o.y)/d.y;var p=o+d*t;
            if(t>=0&&t<=distance&&p.x*p.x+p.z*p.z<=radius*radius){distance=t;normal=side==0?Vector3.down:Vector3.up;found=true;}
        }
        return found;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    sealed class CombatHit
    {
        public Animal Animal;public HumanActor Human;public SkinnedMeshRenderer HumanRenderer;
        public float Distance;public int Face;public Vector3 Point,Normal;public Vector2 Uv;
        public Vector3[] Vertices;public Vector2[] Uvs;public int[] Triangles;
    }
    sealed class HumanCombatSurface
    {public Mesh Mesh;public int Frame=-1;public Vector3[] Vertices;public Vector2[] Uvs;public int[] Triangles;}
    readonly Dictionary<SkinnedMeshRenderer,HumanCombatSurface> humanCombatSurfaces=new Dictionary<SkinnedMeshRenderer,HumanCombatSurface>();
    FaunaCollisionSurface AnimalSurface(Animal animal)=>animal.Model.Collision??(animal.Model.Collision=new FaunaCollisionSurface(animal.Model.Asset.Slug,animal.Model.Asset.SourceSha256,animal.Model.Asset.BoneCount));
    Vector3[] PosedAnimal(Animal animal)
    {
        var surface=AnimalSurface(animal);if(animal.CollisionVertices==null)animal.CollisionVertices=new Vector3[surface.Vertices.Length];
        if(animal.CollisionFrame!=Time.frameCount)
        {surface.Pose(animalBoneData,animal.Slot*AnimalBones,animal.Root.transform.localToWorldMatrix,animal.Props.GetFloat("_Morph"),animal.CollisionVertices);animal.CollisionFrame=Time.frameCount;}
        return animal.CollisionVertices;
    }
    HumanCombatSurface PosedHuman(SkinnedMeshRenderer renderer)
    {
        if(!humanCombatSurfaces.TryGetValue(renderer,out var surface)){surface=new HumanCombatSurface{Mesh=new Mesh{name="Temporary combat skin pose"}};humanCombatSurfaces.Add(renderer,surface);}
        if(surface.Frame!=Time.frameCount)
        {
            renderer.BakeMesh(surface.Mesh);surface.Vertices=surface.Mesh.vertices;for(int i=0;i<surface.Vertices.Length;i++)surface.Vertices[i]=renderer.transform.TransformPoint(surface.Vertices[i]);
            surface.Uvs=surface.Mesh.uv;surface.Triangles=surface.Mesh.triangles;surface.Frame=Time.frameCount;
        }
        return surface;
    }
    CombatHit TraceLiving(Ray ray,float reach,bool includePlayer=false)
    {
        CombatHit hit=null;float closest=reach;
        foreach(var animal in animals.Values)
        {
            if(animal.Dead||animal.Fade<.15f)continue;var bounds=FaunaCollisionSurface.WorldBounds(animal.Model.Asset.Mesh.bounds,animal.Root.transform.localToWorldMatrix);bounds.Expand(.04f);
            if(!bounds.IntersectRay(ray,out float entry)||entry>closest)continue;var surface=AnimalSurface(animal);var vertices=PosedAnimal(animal);
            if(surface.Raycast(ray,vertices,animal.Parts,closest,out float distance,out int face,out Vector3 normal,out Vector2 uv))
            {closest=distance;hit=new CombatHit{Animal=animal,Face=face,Distance=distance,Point=ray.GetPoint(distance),Normal=normal,Uv=uv,Vertices=vertices,Uvs=surface.Uv,Triangles=surface.Triangles};}
        }
        IEnumerable<HumanActor> humans=humanNpcs;if(includePlayer&&humanPlayer!=null)humans=humans.Concat(new[]{humanPlayer});
        foreach(var human in humans)
        {
            if(human==humanPlayer?DeathVisible:human.Health<=0)continue;
            foreach(var renderer in human.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.bounds.IntersectRay(ray,out float entry)||entry>closest)continue;var surface=PosedHuman(renderer);
                for(int t=0;t<surface.Triangles.Length;t+=3)
                {
                    int a=surface.Triangles[t],b=surface.Triangles[t+1],c=surface.Triangles[t+2];
                    if(!FaunaCollisionSurface.RayTriangle(ray,surface.Vertices[a],surface.Vertices[b],surface.Vertices[c],closest,out float distance,out float u,out float v))continue;
                    Vector3 normal=Vector3.Cross(surface.Vertices[b]-surface.Vertices[a],surface.Vertices[c]-surface.Vertices[a]).normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;
                    closest=distance;hit=new CombatHit{Human=human,HumanRenderer=renderer,Distance=distance,Point=ray.GetPoint(distance),Normal=normal,Uv=surface.Uvs[a]*(1-u-v)+surface.Uvs[b]*u+surface.Uvs[c]*v,Vertices=surface.Vertices,Uvs=surface.Uvs,Triangles=surface.Triangles};
                }
            }
        }
        return hit;
    }
    void DisposeHumanCombatSurface(HumanActor actor)
    {
        foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            if(humanCombatSurfaces.TryGetValue(renderer,out var surface)){Destroy(surface.Mesh);humanCombatSurfaces.Remove(renderer);}
        ClearHumanBlood(actor);
    }
    partial void ClearHumanBlood(HumanActor actor);
}

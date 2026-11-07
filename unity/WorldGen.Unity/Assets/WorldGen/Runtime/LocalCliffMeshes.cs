using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldGen.Core;

static class LocalCliffMeshes
{
    // Nine small immutable templates for the session. Art owns its clones, so
    // ordinary model eviction cannot destroy another lithology's shared source.
    static readonly Dictionary<int,Mesh> templates=new Dictionary<int,Mesh>();
    static Mesh Template(int source,int lod)
    {
        int key=source*3+lod;if(templates.TryGetValue(key,out var cached))return cached;
        var asset=Resources.Load<TextAsset>("CliffRockV1/cliff-"+source+"-l"+lod);
        if(asset==null)throw new InvalidOperationException("Required cliff geometry is missing");
        Mesh mesh=null;
        try
        {
            using(var input=new BinaryReader(new MemoryStream(asset.bytes,false)))
            {
                if(input.ReadUInt32()!=0x31435257)throw new InvalidDataException("Cliff geometry version mismatch");
                int count=input.ReadInt32(),indices=input.ReadInt32();
                if(count<4||count>16384||indices<12||indices>12000||indices%3!=0)throw new InvalidDataException("Cliff geometry exceeds its bounded budget");
                var positions=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var tangents=new Vector4[count];
                for(int i=0;i<count;i++)
                {
                    positions[i]=new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());normals[i]=new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());
                    uv[i]=new Vector2(input.ReadSingle(),input.ReadSingle());var tangent=Vector3.Cross(Math.Abs(normals[i].y)<.9f?Vector3.up:Vector3.right,normals[i]).normalized;
                    tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,1);
                }
                var triangles=new int[indices];for(int i=0;i<indices;i++){triangles[i]=input.ReadInt32();if(triangles[i]<0||triangles[i]>=count)throw new InvalidDataException("Invalid cliff vertex index");}
                if(input.BaseStream.Position!=input.BaseStream.Length)throw new InvalidDataException("Trailing cliff data");
                mesh=new Mesh{name="CC0 cliff template "+source+" L"+lod};mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.tangents=tangents;
                var colours=new Color[count];for(int i=0;i<count;i++)colours[i]=Color.white;mesh.colors=colours;mesh.triangles=triangles;mesh.RecalculateBounds();
            }
        }
        catch{if(mesh!=null)UnityEngine.Object.Destroy(mesh);throw;}
        finally{Resources.UnloadAsset(asset);}
        templates.Add(key,mesh);return mesh;
    }
    public static Mesh Build(int variant,int lod)
    {
        var mesh=UnityEngine.Object.Instantiate(Template(CliffRocks.Model(variant),Math.Max(0,Math.Min(2,lod))));
        if((variant&4)!=0)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;var triangles=mesh.triangles;
            for(int i=0;i<vertices.Length;i++){vertices[i].x=-vertices[i].x;normals[i].x=-normals[i].x;tangents[i].x=-tangents[i].x;tangents[i].w=-tangents[i].w;}
            for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.triangles=triangles;mesh.RecalculateBounds();
        }
        mesh.name="Natural cliff mass V"+variant+" L"+lod;return mesh;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<Mesh,Vector3[]> birdPerchGeometry=new Dictionary<Mesh,Vector3[]>();
    bool FindBirdPerch(Animal animal,out V position,out double height)
    {
        position=default;height=0;double nearest=25*25;Vector3 chosen=default;bool found=false;var origin=new Vector3((float)(animal.X-originX),(float)animal.GroundY,(float)(animal.Z-originZ));
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(origin)>nearest)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(origin)>nearest)continue;
                foreach(int index in group.Indices)
                {
                    var tree=chunk.Source.Instances[index];if(tree.Kind!=ScatterKind.GeneratedTree||tree.HeightM<3||(chunk.Roots[index]-origin).sqrMagnitude>nearest)continue;
                    var art=Art(tree.Kind,tree.Variant,0);foreach(var part in art.Parts)
                    {
                        if(!birdPerchGeometry.TryGetValue(part.Mesh,out var points))
                        {
                            var selected=new List<Vector3>();if(part.Mesh.isReadable)
                            {
                                var masks=new List<Vector2>();part.Mesh.GetUVs(1,masks);var vertices=part.Mesh.vertices;var normals=part.Mesh.normals;
                                for(int i=0;i<vertices.Length;i+=3)if(masks.Count==vertices.Length&&masks[i].y<.3f&&vertices[i].y>.23f&&vertices[i].y<.8f&&vertices[i].x*vertices[i].x+vertices[i].z*vertices[i].z>.003f&&normals[i].y>.45f)selected.Add(vertices[i]);
                            }
                            points=selected.Where((p,i)=>i%Math.Max(1,selected.Count/12)==0).ToArray();birdPerchGeometry[part.Mesh]=points;
                        }
                        foreach(var point in points)
                        {
                            var p=(chunk.Matrices[index]*part.Local).MultiplyPoint3x4(point);double distance=Math.Pow(p.x-origin.x,2)+Math.Pow(p.z-origin.z,2);
                            if(distance>=nearest)continue;nearest=distance;chosen=p;found=true;
                        }
                    }
                }
            }
        }
        if(!found)return false;position=frame.Position(chosen.x+originX,chosen.z+originZ);double support=VisibleGround(chosen.x+originX,chosen.z+originZ,animal.GroundY);
        if(!double.IsNaN(animal.AirWaterM))support=Math.Max(support,frame.SurfaceY(position,animal.AirWaterM));height=chosen.y-support;return height>.5;
    }
    bool UpdateBirdBehaviour(Animal animal,WildlifeBehaviourProfile behaviour)
    {
        if(wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var group)&&group.Intent==WildlifeIntent.Migrate)
        {animal.PerchSelected=animal.Perched=false;animal.AirTargetHeight=35+animal.Spawn.GroupIndex*.15;var p=GroupMemberPosition(animal.Spawn,group);TerrainHandoff.Coordinates(frame,p,out double x,out double z);SetAnimalTravel(animal,x,z,false);return true;}
        if(animal.PerchSelected)
        {
            TerrainHandoff.Coordinates(frame,animal.PerchPosition,out double x,out double z);double distance=Math.Sqrt(Math.Pow(x-animal.X,2)+Math.Pow(z-animal.Z,2));animal.AirTargetHeight=animal.PerchHeight;
            if(distance<.15&&Math.Abs(animal.AirHeight-animal.AirTargetHeight)<.15)
            {
                bool arrived=!animal.Perched;animal.X=x;animal.Z=z;animal.Planet=animal.PerchPosition;animal.TargetSpeed=0;animal.Speed=0;animal.Perched=true;AnimalRole(animal,"idle");
                if(animal.Life>animal.PerchUntil){animal.PerchSelected=animal.Perched=false;animal.AirTargetHeight=animal.Spawn.HeightM+2;animal.NextDecision=animal.Life;}
                else if(arrived)
                {
                    double activity=WildlifeBehaviour.Activity(behaviour,LocalSolarHour,WildlifeBehaviour.IsNocturnal(animal.Spawn.Profile.slug,animal.Spawn.Profile.nocturnal),CurrentWeather.RainMmPerHour,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),SkyFrame?.Daylight??double.NaN);
                    if(activity<.3||group!=null&&(group.Intent==WildlifeIntent.Rest||group.Intent==WildlifeIntent.Shelter))
                    {AnimalActivity(animal,"sleep",Math.Min(20,animal.PerchUntil-animal.Life));return true;}
                    var diet=AnimalTaxon(animal)?.Eats??Food.None;
                    if(animal.PerchHeight<.1&&WildlifeNaturalHistory.GroundForaging(diet)&&animal.Random.Unit()<.65)
                    {AnimalActivity(animal,"eat",Math.Min(8,animal.PerchUntil-animal.Life));return true;}
                }
            }
            else {SetAnimalTravel(animal,x,z,false);animal.TargetSpeed=Math.Min(animal.TargetSpeed,(float)Math.Max(.15,distance*.9));}
            if(animal.PerchHeight<.1&&animal.AirHeight<.2)
            {
                if(animal.Perched&&animal.Life<animal.PerchUntil)
                {double a=(animal.Life+animal.Spawn.Hash%23)*1.7;animal.PerchPosition=frame.Position(animal.X+Math.Sin(a)*1.3,animal.Z+Math.Cos(a)*1.3);animal.Perched=false;}
                if(!animal.Perched){animal.TargetSpeed=Math.Min(animal.TargetSpeed,.6f);AnimalRole(animal,"walk");}
            }
            return true;
        }
        if(animal.Life<animal.NextPerch)return false;animal.NextPerch=animal.Life+18+animal.Spawn.Hash%27;
        bool canIdle=animal.Model.Asset.Clips.Any(c=>c.role=="idle");if(!canIdle)return false;
        if(behaviour.Perches&&FindBirdPerch(animal,out var perch,out double height))
        {animal.PerchSelected=true;animal.PerchPosition=perch;animal.PerchHeight=height;animal.PerchUntil=animal.Life+25+animal.Spawn.Hash%30;animal.AirTargetHeight=height;return true;}
        if(animal.Model.Asset.Clips.Any(c=>c.role=="walk"))
        {
            var sample=walkingPatch.Sample(animal.Planet);if(!sample.Underwater){animal.PerchSelected=true;animal.PerchPosition=animal.Planet;animal.PerchHeight=.03;animal.PerchUntil=animal.Life+8+animal.Spawn.Hash%9;animal.AirTargetHeight=.03;}
        }
        return false;
    }
}

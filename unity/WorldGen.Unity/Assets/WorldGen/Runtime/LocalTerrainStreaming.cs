using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class RetiredTerrain
    {
        public GameObject Root;public LocalTerrainFrame Frame;public double X,Z,SafeRange;public V Center;public int Tiles;
    }
    readonly List<RetiredTerrain> retiredTerrain=new List<RetiredTerrain>();
    Dictionary<TerrainTileKey,int> coverageWanted=new Dictionary<TerrainTileKey,int>();
    readonly MaterialPropertyBlock lodProperties=new MaterialPropertyBlock();
    double streamFog=180000,nextFogCheck,terrainDetailRange=1;int terrainQuality=1;
    public int ReanchorCount {get;private set;}
    public int RetainedTileCount=>retiredTerrain.Sum(t=>t.Tiles);
    public int VisibleTerrainTiles=>RetainedTileCount+tiles.Values.Count(t=>t.Root.activeSelf);
    public double ProtectiveFogM=>streamFog;
    public string RockSceneSummary=>string.Join("; ",scatterChunks.Values.SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Stone||p.Kind==ScatterKind.Outcrop).GroupBy(p=>p.Kind).Select(g=>g.Key+" "+g.Count()+" (largest "+g.Max(p=>p.HeightM).ToString("F1")+"m)"));
    public bool TestRockFocus()
    {
        var rocks=scatterChunks.Values.SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Outcrop).OrderByDescending(p=>p.HeightM).ToArray();
        if(rocks.Length==0)return false;var rock=rocks[0];double dot=V.Dot(rock.Position,frame.Origin);
        double x=V.Dot(rock.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(rock.Position,frame.North)*frame.RadiusM/dot;
        double distance=Math.Max(8,rock.WidthM*1.3);motion.Place(x-distance*.6,z-distance*.8,Ground(x-distance*.6,z-distance*.8),true,3);
        heading=(float)(Math.Atan2(.6,.8)*180/Math.PI);pitch=(float)(Math.Atan2(motion.Y-(frame.SurfaceY(rock.Position,rock.GroundM)+rock.HeightM*.35),distance)*180/Math.PI);
        RefreshWanted();UpdateCamera();return true;
    }

    void ApplyTerrainOptions()
    {
        var options=TerrainVideoOptions.Current;
        double distance=Math.Min(options.DistanceKm*1000,MaximumViewDistance);
        if(Math.Abs(distance-viewDistance)<1&&terrainQuality==options.Quality&&terrainDetailRange==options.DetailRange)return;
        viewDistance=distance;terrainQuality=options.Quality;terrainDetailRange=options.DetailRange;
        view.farClipPlane=(float)(viewDistance*1.3+30000);RefreshWanted();
    }
    bool DesiredReady(TerrainTileKey key,int segments)=>tiles.TryGetValue(key,out var tile)&&tile.Segments==segments&&tile.Edges.SequenceEqual(wantedEdges[key]);
    void ReconcileTerrain()
    {
        // Switch a complete parent/children coverage group atomically. Never expose
        // fine children over a still-rendered coarse parent (or remove it early).
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)).ToArray())
        {
            var replacements=wanted.Where(w=>Overlaps(key,w.Key)).ToArray();
            if(replacements.Length==0||replacements.All(w=>DesiredReady(w.Key,w.Value)))
            {DestroyTile(tiles[key]);tiles.Remove(key);}
        }
        foreach(var pair in wanted)
        {
            if(!DesiredReady(pair.Key,pair.Value))continue;var tile=tiles[pair.Key];if(tile.Root.activeSelf)continue;
            var covered=tiles.Where(p=>!p.Key.Equals(pair.Key)&&p.Value.Root.activeSelf&&Overlaps(pair.Key,p.Key)).ToArray();
            if(covered.Any(p=>p.Key.SizeM>pair.Key.SizeM))continue;
            foreach(var old in covered){DestroyTile(old.Value);tiles.Remove(old.Key);}
            tile.Root.SetActive(true);tile.FadeAt=Time.realtimeSinceStartupAsDouble;
        }
        bool settled=true;double now=Time.realtimeSinceStartupAsDouble;
        foreach(var tile in tiles.Values)
        {
            if(!tile.Root.activeSelf){settled=false;continue;}
            if(tile.FadeFinished)continue;
            float t=(float)Math.Max(0,Math.Min(1,(now-tile.FadeAt)/TerrainVideoOptions.Current.TransitionSeconds));
            t=t*t*(3-2*t);lodProperties.SetFloat("_LodBlend",t);
            foreach(var renderer in tile.Renderers)renderer.SetPropertyBlock(lodProperties);
            if(t>=1)tile.FadeFinished=true;
            if(t<1)settled=false;
        }
        if(retiredTerrain.Count>0&&settled&&wanted.All(p=>DesiredReady(p.Key,p.Value)))ClearRetiredTerrain();
        if(now>=nextFogCheck)
        {
            nextFogCheck=now+.15;double safe=viewDistance*.96;
            foreach(var pair in wanted)
                {
                    var k=pair.Key;bool covered=false;
                    for(int level=k.Level;level<=16;level++)
                    {
                        double size=2048*HorizontalScale*(1<<level);
                        var parent=new TerrainTileKey((int)Math.Floor(k.X*k.SizeM/size),(int)Math.Floor(k.Z*k.SizeM/size),level,HorizontalScale);
                        if(tiles.TryGetValue(parent,out var tile)&&tile.Root.activeSelf){covered=true;break;}
                    }
                    if(covered)continue;
                    double dx=Math.Max(Math.Max(k.X*k.SizeM-motion.X,0),motion.X-(k.X+1)*k.SizeM);
                    double dz=Math.Max(Math.Max(k.Z*k.SizeM-motion.Z,0),motion.Z-(k.Z+1)*k.SizeM);
                    safe=Math.Min(safe,Math.Sqrt(dx*dx+dz*dz)*.8);
                }
            double retainedSafe=0;
            foreach(var retired in retiredTerrain)retainedSafe=Math.Max(retainedSafe,retired.SafeRange-D.Angle(retired.Center,Position)*frame.RadiusM);
            safe=Math.Max(250,Math.Min(viewDistance*.96,Math.Max(safe,retainedSafe*.85)));
            // Hide missing coverage immediately; reveal newly loaded distance gently.
            streamFog=safe<streamFog?safe:Math.Min(safe,streamFog+Math.Max(1000,viewDistance*.07));SetFog();
        }
    }
    void RetainTerrain(LocalTerrainFrame previous,double x,double z)
    {
        ReanchorCount++;
        var group=new GameObject("Retained terrain coverage");group.transform.SetParent(transform,false);int count=0;
        foreach(var tile in tiles.Values)
        {
            if(tile.Root.activeSelf){tile.Root.transform.SetParent(group.transform,false);count++;}
            else DestroyTile(tile);
        }
        if(count>0)retiredTerrain.Add(new RetiredTerrain{Root=group,Frame=previous,X=x,Z=z,Tiles=count,SafeRange=streamFog,Center=previous.Position(motion.X,motion.Z)});else Destroy(group);
        // Bounded transition cache, not an accumulating second terrain world.
        while(retiredTerrain.Count>3){DestroyRetired(retiredTerrain[0]);retiredTerrain.RemoveAt(0);}
        ProjectRetiredTerrain();
    }
    void ProjectRetiredTerrain()
    {
        foreach(var retired in retiredTerrain)
        {
            var old=retired.Frame;
            V delta=old.Origin*(old.RadiusM+old.AltitudeM)+old.East*retired.X+old.North*retired.Z-frame.Origin*(frame.RadiusM+frame.AltitudeM);
            Vector3 Project(V p)=>new Vector3((float)V.Dot(p,frame.East),(float)V.Dot(p,frame.Origin),(float)V.Dot(p,frame.North));
            retired.Root.transform.localPosition=Project(delta)-new Vector3((float)originX,0,(float)originZ);
            retired.Root.transform.localRotation=Quaternion.LookRotation(Project(old.North),Project(old.Origin));
        }
    }
    void DestroyRetired(RetiredTerrain retired)
    {if(retired.Root==null)return;retired.Root.SetActive(false);foreach(var filter in retired.Root.GetComponentsInChildren<MeshFilter>())Destroy(filter.sharedMesh);Destroy(retired.Root);}
    void ClearRetiredTerrain(){foreach(var retired in retiredTerrain)DestroyRetired(retired);retiredTerrain.Clear();}
}

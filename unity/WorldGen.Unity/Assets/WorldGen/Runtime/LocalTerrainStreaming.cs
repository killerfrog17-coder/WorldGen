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
        public Dictionary<TerrainTileKey,Tile> Products;
    }
    readonly List<RetiredTerrain> retiredTerrain=new List<RetiredTerrain>();
    readonly MaterialPropertyBlock lodProperties=new MaterialPropertyBlock();
    double streamFog=180000,nextFogCheck,terrainDetailRange=1;int terrainQuality=1;
    int coverageRevision;
    public int TerrainBuildCount {get;private set;}
    public int ReanchorCount {get;private set;}
    public int RetainedTileCount=>retiredTerrain.Where(t=>t.Root.activeSelf).Sum(t=>t.Tiles);
    public int VisibleTerrainTiles=>RetainedTileCount+tiles.Values.Count(t=>t.Root.activeSelf);
    public double ProtectiveFogM=>streamFog;
    public double RenderedFootClearance=>TryVisibleGround(motion.X,motion.Z,out double floor,0)?motion.Y-floor:double.NaN;
    public bool NearGroundReady=>TryVisibleGround(motion.X,motion.Z,out _,0);
    public void TestRevisit(V point,double height=800)
    {
        double above=height;motion.Flying=true;PlaceFrame(point,true);motion.Y=Ground(motion.X,motion.Z)+above;RefreshWanted();UpdateCamera();
    }
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
    bool DesiredReady(TerrainTileKey key,int segments)=>tiles.TryGetValue(key,out var tile)&&tile.Segments>=segments;
    void ReconcileTerrain()
    {
        // Fine products become visible immediately, not after a continent-sized
        // fallback finishes every descendant. Coarse coverage masks only the
        // portions for which finer geometry is actually resident.
        foreach(var pair in tiles)
        {
            bool active=wanted.ContainsKey(pair.Key)||wanted.Any(w=>Overlaps(pair.Key,w.Key)&&!DesiredReady(w.Key,w.Value));
            if(active)pair.Value.UsedAt=Time.realtimeSinceStartupAsDouble;
            if(pair.Value.Root.activeSelf!=active){pair.Value.Root.SetActive(active);coverageRevision++;}
        }
        Vector4 footing=Vector4.zero;
        foreach(var pair in tiles.Where(p=>p.Key.Level==0&&p.Value.Root.activeSelf))
        {var k=pair.Key;if(motion.X>=k.X*k.SizeM&&motion.X<(k.X+1)*k.SizeM&&motion.Z>=k.Z*k.SizeM&&motion.Z<(k.Z+1)*k.SizeM)footing=new Vector4((float)(k.X*k.SizeM-originX),(float)(k.Z*k.SizeM-originZ),(float)((k.X+1)*k.SizeM-originX),(float)((k.Z+1)*k.SizeM-originZ));}
        Shader.SetGlobalVector("_TerrainFootingRect",footing);
        bool settled=true;double now=Time.realtimeSinceStartupAsDouble;
        foreach(var pair in tiles)
        {
            var tile=pair.Value;
            if(!tile.Root.activeSelf)continue;
            bool coverChanged=tile.CoverRevision!=coverageRevision;
            if(coverChanged)UpdateTerrainCover(pair.Key,tile);
            if(tile.FadeFinished&&!coverChanged)continue;
            float t=(float)Math.Max(0,Math.Min(1,(now-tile.FadeAt)/TerrainVideoOptions.Current.TransitionSeconds));
            t=t*t*(3-2*t);lodProperties.Clear();lodProperties.SetFloat("_LodBlend",t);
            lodProperties.SetFloat("_RetiredTerrain",0);lodProperties.SetFloat("_CoverEnabled",tile.Cover==null?0:1);
            if(tile.Cover!=null)lodProperties.SetTexture("_TerrainCover",tile.Cover);
            lodProperties.SetFloat("_TileSize",(float)pair.Key.SizeM);
            foreach(var renderer in tile.Renderers)renderer.SetPropertyBlock(lodProperties);
            if(t>=1)tile.FadeFinished=true;
            if(t<1)settled=false;
        }
        if(retiredTerrain.Count>0&&settled&&wanted.All(p=>DesiredReady(p.Key,p.Value)))foreach(var retired in retiredTerrain)retired.Root.SetActive(false);
        // Keep recently visited products, including their higher resolution.
        // Only inactive, out-of-plan products may be evicted by this bounded LRU.
        int excess=tiles.Count-512;
        if(excess>0)foreach(var old in tiles.Where(p=>!p.Value.Root.activeSelf).OrderBy(p=>p.Value.UsedAt).Take(excess).ToArray())
        {DestroyTile(old.Value);tiles.Remove(old.Key);}
        if(now>=nextFogCheck)
        {
            nextFogCheck=now+.15;double safe=viewDistance*.96;
            var coverage=new TerrainCoverage(tiles.Where(p=>p.Value.Root.activeSelf).Select(p=>p.Key));
            foreach(var pair in wanted)
                {
                    var k=pair.Key;if(coverage.Covers(k))continue;
                    double dx=Math.Max(Math.Max(k.X*k.SizeM-motion.X,0),motion.X-(k.X+1)*k.SizeM);
                    double dz=Math.Max(Math.Max(k.Z*k.SizeM-motion.Z,0),motion.Z-(k.Z+1)*k.SizeM);
                    safe=Math.Min(safe,Math.Sqrt(dx*dx+dz*dz)*.8);
                }
            double retainedSafe=0;
            foreach(var retired in retiredTerrain)if(retired.Root.activeSelf)retainedSafe=Math.Max(retainedSafe,retired.SafeRange-D.Angle(retired.Center,Position)*frame.RadiusM);
            safe=Math.Max(250,Math.Min(viewDistance*.96,Math.Max(safe,retainedSafe*.85)));
            // Hide missing coverage immediately; reveal newly loaded distance gently.
            streamFog=safe<streamFog?safe:Math.Min(safe,streamFog+Math.Max(1000,viewDistance*.07));SetFog();
        }
    }
    void UpdateTerrainCover(TerrainTileKey key,Tile tile)
    {
        tile.CoverRevision=coverageRevision;
        var finer=tiles.Where(p=>p.Key.Level<key.Level&&p.Value.Root.activeSelf&&Overlaps(key,p.Key)).Select(p=>p.Key).ToArray();
        if(finer.Length==0){if(tile.Cover!=null){Destroy(tile.Cover);tile.Cover=null;}return;}
        int resolution=1<<Math.Min(12,key.Level-finer.Min(k=>k.Level));
        if(tile.Cover==null||tile.Cover.width!=resolution)
        {if(tile.Cover!=null)Destroy(tile.Cover);tile.Cover=new Texture2D(resolution,resolution,TextureFormat.R8,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
        var pixels=new byte[resolution*resolution];
        foreach(var k in finer)
        {
            int a=Math.Max(0,(int)Math.Round((k.X*k.SizeM-key.X*key.SizeM)/key.SizeM*resolution));
            int b=Math.Max(0,(int)Math.Round((k.Z*k.SizeM-key.Z*key.SizeM)/key.SizeM*resolution));
            int c=Math.Min(resolution,(int)Math.Round(((k.X+1)*k.SizeM-key.X*key.SizeM)/key.SizeM*resolution));
            int d=Math.Min(resolution,(int)Math.Round(((k.Z+1)*k.SizeM-key.Z*key.SizeM)/key.SizeM*resolution));
            for(int z=b;z<d;z++)for(int x=a;x<c;x++)pixels[z*resolution+x]=255;
        }
        tile.Cover.LoadRawTextureData(pixels);tile.Cover.Apply(false,false);
    }
    RetiredTerrain FindTerrainFrame(V point)
    {
        double limit=Math.Min(65536,frame.RadiusM*.04)*.7;
        return retiredTerrain.Where(r=>D.Angle(r.Frame.Origin,point)*frame.RadiusM<limit)
            .OrderBy(r=>D.Angle(r.Frame.Origin,point)).FirstOrDefault();
    }
    void RestoreTerrainFrame(RetiredTerrain cached)
    {
        foreach(var p in cached.Products)
        {var tile=p.Value;tile.Root.transform.SetParent(transform,false);tile.Root.transform.localPosition=new Vector3((float)(p.Key.X*p.Key.SizeM),0,(float)(p.Key.Z*p.Key.SizeM));tile.CoverRevision=-1;tiles[p.Key]=tile;}
        Destroy(cached.Root);coverageRevision++;
    }
    void RetainTerrain(LocalTerrainFrame previous,double x,double z)
    {
        ReanchorCount++;
        var group=new GameObject("Retained terrain coverage");group.transform.SetParent(transform,false);int count=0;
        foreach(var pair in tiles)
        {
            var tile=pair.Value;
            tile.Root.transform.SetParent(group.transform,false);if(tile.Root.activeSelf)count++;
            var properties=new MaterialPropertyBlock();properties.SetFloat("_LodBlend",1);properties.SetFloat("_RetiredTerrain",1);
            properties.SetFloat("_CoverEnabled",tile.Cover==null?0:1);if(tile.Cover!=null)properties.SetTexture("_TerrainCover",tile.Cover);
            properties.SetFloat("_TileSize",(float)pair.Key.SizeM);
            foreach(var renderer in tile.Renderers)renderer.SetPropertyBlock(properties);
        }
        if(tiles.Count>0)retiredTerrain.Add(new RetiredTerrain{Root=group,Frame=previous,X=x,Z=z,Tiles=count,SafeRange=streamFog,Center=previous.Position(double.IsNaN(plannedX)?motion.X:plannedX,plannedZ),Products=new Dictionary<TerrainTileKey,Tile>(tiles)});else Destroy(group);
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
    {if(retired.Root==null)return;retired.Root.SetActive(false);foreach(var tile in retired.Products.Values)DestroyTile(tile);Destroy(retired.Root);}
    void ClearRetiredTerrain(){foreach(var retired in retiredTerrain)DestroyRetired(retired);retiredTerrain.Clear();}
}

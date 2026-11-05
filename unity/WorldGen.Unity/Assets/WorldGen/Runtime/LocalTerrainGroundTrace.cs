using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    [Flags] enum GroundOrigin {Unknown=0,Upload=1,CacheRestore=2,Retirement=4}
    enum GroundStageBypass {Unknown,Disabled,Backdrop,Pending,PreviousChanges,NoRoots,UnpreparedGroups,Unchanged,Staged}
    GroundStageBypass groundStageBypass;
    readonly Dictionary<TerrainTileKey,(GroundOrigin origin,GroundStageBypass bypass)> groundOrigins=new Dictionary<TerrainTileKey,(GroundOrigin,GroundStageBypass)>();
    struct GroundCorrectionTrace {public double Time,Ms,X,Z;public int Frame,Keys;public TerrainTileKey Key;public GroundOrigin Origin;public GroundStageBypass Bypass;}
    readonly List<GroundCorrectionTrace> groundCorrectionTrace=new List<GroundCorrectionTrace>(512);
    void NoteGroundChange(TerrainTileKey key,GroundOrigin origin)
    {
        if(!WorldGenPerformance.TraceEnabled)return;
        groundOrigins.TryGetValue(key,out var previous);groundOrigins[key]=(previous.origin|origin,origin==GroundOrigin.Retirement?GroundStageBypass.Unknown:groundStageBypass);
    }
    void RecordGroundCorrection(double ms)
    {
        if(!WorldGenPerformance.TraceEnabled||ms<2)return;
        GroundCorrectionTrace Row(TerrainTileKey key)
        {groundOrigins.TryGetValue(key,out var cause);return new GroundCorrectionTrace{Time=UnityEngine.Time.realtimeSinceStartupAsDouble,Ms=ms,X=motion.X,Z=motion.Z,Frame=UnityEngine.Time.frameCount,Keys=scatterGroundChanges.Count,Key=key,Origin=cause.origin,Bypass=cause.bypass};}
        if(scatterGroundChanges.Count==0)groundCorrectionTrace.Add(Row(default));
        else foreach(var key in scatterGroundChanges)groundCorrectionTrace.Add(Row(key));
    }
    public void PerformanceBeginGroundTrace(){groundCorrectionTrace.Clear();}
    public void PerformanceWriteGroundTrace(string path)
    {
        using var writer=new StreamWriter(path);writer.WriteLine("absolute_time_s,frame,correction_ms,player_x,player_z,changed_keys,key_x,key_z,key_level,key_scale,origin,stage_bypass");
        foreach(var r in groundCorrectionTrace)writer.WriteLine(string.Join(",",r.Time.ToString("R",CultureInfo.InvariantCulture),r.Frame,r.Ms.ToString("R",CultureInfo.InvariantCulture),r.X.ToString("R",CultureInfo.InvariantCulture),r.Z.ToString("R",CultureInfo.InvariantCulture),r.Keys,r.Key.X,r.Key.Z,r.Key.Level,r.Key.Scale.ToString("R",CultureInfo.InvariantCulture),r.Origin.ToString().Replace(", ","|"),r.Bypass));
    }
}

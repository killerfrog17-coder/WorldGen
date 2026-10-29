using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class AnimalSpawnBatch
    {
        public FaunaPrototypeProfile Profile;public int Count,Index,Unsafe;
        public double X,Z,Yaw,AirHeight;public V Anchor;
        public LocalTerrainFrame Frame;public LocalTerrain.LocalTerrainPatch Patch;
    }
    FaunaPrototypeProfile[] animalProfiles=Array.Empty<FaunaPrototypeProfile>();
    readonly List<FaunaPrototypeSpawn> animalManualWanted=new List<FaunaPrototypeSpawn>();
    readonly Queue<AnimalSpawnBatch> animalManualRequests=new Queue<AnimalSpawnBatch>();
    ulong animalManualSequence=0xfaf0000000000000UL;
    PlayCommandResult ExecuteAnimalSpawnCommand(string text)
    {
        var request=FaunaSpawnCommands.Parse(text,animalProfiles);
        if(request.Action=="help")return new PlayCommandResult(request.Error.Length==0,request.Error.Length>0?request.Error:FaunaSpawnCommands.Help);
        if(request.Action=="clear"&&request.Error.Length==0)
        {
            var ids=animals.Where(p=>p.Value.Manual).Select(p=>p.Key).ToArray();
            long count=ids.Length+animalManualWanted.Count+animalManualRequests.Sum(b=>(long)b.Count-b.Index)+ClearSpawnedHumans();
            animalManualWanted.Clear();animalManualRequests.Clear();foreach(ulong id in ids)RemoveAnimal(id);
            // Explicitly clearing command actors also clears their saved bodies;
            // otherwise the next regional plan would restore them immediately.
            var manualIds=new HashSet<ulong>(Wildlife.Individuals.Where(p=>p.Manual).Select(p=>p.Id));
            Inventory.Items.RemoveAll(p=>p.Place==PossessionPlace.Corpse&&manualIds.Contains(p.CorpseOwner));
            var lootKeys=new HashSet<string>(manualIds.Select(id=>"loot-"+id.ToString("x16")));Inventory.ToolResources.RemoveAll(r=>lootKeys.Contains(r.Id));
            Wildlife.Individuals.RemoveAll(p=>p.Manual);if(wildlifeRecords!=null)foreach(var id in manualIds)wildlifeRecords.Remove(id);
            animalWanted.RemoveAll(p=>manualIds.Contains(p.Id));
            return new PlayCommandResult(true,"Cleared "+count+" command-spawned/queued animals and humans. Regional wildlife is retained.");
        }
        if(animalDisabled)return new PlayCommandResult(false,"Animals disabled by --worldgen-no-animals.");
        if(!Ready||animalPlanner==null||!CenterReady)return new PlayCommandResult(false,"Wait for nearby terrain and wildlife to initialize.");
        if(request.Error.Length>0)return new PlayCommandResult(false,request.Error);
        if(request.Action=="list")return FaunaSpawnCommands.List(animalProfiles,request);
        var profile=request.Profile;animalLoadFailures.Remove(profile.slug);
        animalManualRequests.Enqueue(new AnimalSpawnBatch{Profile=profile,Count=request.Count,X=motion.X,Z=motion.Z,Yaw=heading*Math.PI/180,
            Frame=frame,Anchor=Position,AirHeight=Math.Max(profile.tiny?.5:1.8,HeightAboveGround),Patch=terrain.Patch(Position,2048/HorizontalScale)});
        return new PlayCommandResult(true,"Queued "+request.Count+" "+profile.name+". Spawning is spread across frames; /spawn clear cancels pending spawns.");
    }
    void PrepareManualAnimalSpawns()
    {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();int prepared=0;
        // Preparation window, not a population limit: the full request is a count.
        while(animalManualRequests.Count>0&&animalManualWanted.Count<32&&prepared++<16)
        {
            var b=animalManualRequests.Peek();var profile=b.Profile;
            if((b.Anchor-Position).Length*frame.RadiusM>Math.Max(200,profile.radiusM*1.5))
            {animalManualRequests.Dequeue();PostExplorerChat("Cancelled pending "+profile.name+": you moved away from the spawn area.");continue;}
            int i=b.Index++;double spacing=Math.Max(profile.tiny?.25:1,profile.lengthM*.85);
            // Compact deterministic spiral; explicit requests may crowd together.
            double radius=Math.Min(profile.radiusM*.35,Math.Sqrt(b.Count)*spacing*.55),a=i*2.399963229728653;
            double r=radius*Math.Sqrt((i+.5)/b.Count),ahead=Math.Max(profile.tiny?2:4,profile.lengthM*1.5)+radius;
            double across=Math.Cos(a)*r,forward=ahead+Math.Sin(a)*r;
            double x=b.X+Math.Sin(b.Yaw)*forward+Math.Cos(b.Yaw)*across,z=b.Z+Math.Cos(b.Yaw)*forward-Math.Sin(b.Yaw)*across;
            V point=b.Frame.Position(x,z);var sample=b.Patch.Sample(point);double water=sample.WaterSurfaceM;
            if(sample.Underwater&&double.IsNaN(water)&&sourceWorld.Hydrology.OceanConnected[sample.Cell])water=0;
            bool wet=sample.Underwater||!double.IsNaN(water)&&water>sample.ElevationM+.03;
            if(profile.medium=="ground"&&wet||profile.medium=="water"&&(!wet||double.IsNaN(water)||water-sample.ElevationM<Math.Max(.3,profile.lengthM*.65)))b.Unsafe++;
            else
            {
                ulong id=++animalManualSequence;var habitat=sourceWorld.Ecology.Habitat[sample.Cell];uint hash=Rng.Mix((uint)id^(uint)(id>>32));
                animalManualWanted.Add(new FaunaPrototypeSpawn{Id=id,Hash=hash,Position=point,Profile=profile,Region=sourceWorld.Ecology.Region[sample.Cell],Taxon=-1,
                    CoatSeed=unchecked((int)hash),GroundM=sample.ElevationM,WaterM=water,HeightM=profile.medium=="air"?b.AirHeight:profile.medium=="water"?-.6:0,
                    ColdC=habitat.ColdTenthsC/10.0,Aridity=habitat.AridityMilli/1000.0});
            }
            if(b.Index>=b.Count){animalManualRequests.Dequeue();if(b.Unsafe>0)PostExplorerChat("Skipped "+b.Unsafe+" unsuitable wet/dry spawn spots for "+profile.name+".");}
            if((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
        }
    }
    void UpdateManualAnimalSpawns()
    {
        PrepareManualAnimalSpawns();if(animalManualWanted.Count==0)return;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();int created=0;
        for(int i=animalManualWanted.Count-1;i>=0;i--)
        {
            var spawn=animalManualWanted[i];
            if(animalLoadFailures.Contains(spawn.Profile.slug)){animalManualWanted.RemoveAt(i);continue;}
            if(!animalModels.TryGetValue(spawn.Profile.slug,out var model))continue;
            if(!AddAnimal(spawn,model,true))break;animalManualWanted.RemoveAt(i);created++;
            if(created>=8||(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
        }
    }
}

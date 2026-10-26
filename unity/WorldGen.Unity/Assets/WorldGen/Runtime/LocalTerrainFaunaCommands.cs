using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    const int ManualAnimalLimit=64;
    FaunaPrototypeProfile[] animalProfiles=Array.Empty<FaunaPrototypeProfile>();
    readonly List<FaunaPrototypeSpawn> animalManualWanted=new List<FaunaPrototypeSpawn>();
    ulong animalManualSequence=0xfaf0000000000000UL;

    PlayCommandResult ExecuteAnimalSpawnCommand(string text)
    {
        var request=FaunaSpawnCommands.Parse(text,animalProfiles);
        if(request.Action=="help")return new PlayCommandResult(request.Error.Length==0,request.Error.Length>0?request.Error:FaunaSpawnCommands.Help,"Example: /spawn deer 3. /spawn list searches all available animals; Tab completes names.");
        if(request.Action=="clear"&&request.Error.Length==0)
        {
            var ids=animals.Where(p=>p.Value.Manual).Select(p=>p.Key).ToArray();int count=ids.Length+animalManualWanted.Count;
            animalManualWanted.Clear();foreach(ulong id in ids)RemoveAnimal(id);
            return new PlayCommandResult(true,"Cleared "+count+" command-spawned/queued animals. Regional wildlife is retained.");
        }
        if(animalDisabled)return new PlayCommandResult(false,"Animals are disabled by --worldgen-no-animals. Relaunch without that option to spawn them.");
        if(!Ready||animalPlanner==null||!CenterReady)return new PlayCommandResult(false,"Wait for nearby terrain and wildlife to finish initializing.");
        if(request.Error.Length>0)return new PlayCommandResult(false,request.Error);
        if(request.Action=="list")return FaunaSpawnCommands.List(animalProfiles,request);
        if(HeightAboveGround>=1200)return new PlayCommandResult(false,"Move within 1,200 metres of the ground before spawning animals.");
        if(animals.Values.Count(a=>a.Manual)+animalManualWanted.Count+request.Count>ManualAnimalLimit)
            return new PlayCommandResult(false,"At most 64 command-spawned/queued animals at once. Use /spawn clear first.");
        var profile=request.Profile;
        int types=animals.Values.Where(a=>a.Manual).Select(a=>a.Spawn.Profile.slug).Concat(animalManualWanted.Select(s=>s.Profile.slug)).Concat(new[]{profile.slug}).Distinct().Count();
        if(types>AnimalModelLimit)return new PlayCommandResult(false,"At most "+AnimalModelLimit+" command-spawned animal types at once. Use /spawn clear first.");
        int columns=(int)Math.Ceiling(Math.Sqrt(request.Count));double spacing=Math.Max(profile.tiny?.25:1,profile.lengthM*.85),distance=Math.Max(profile.tiny?2:4,profile.lengthM*1.5);
        double angle=heading*Math.PI/180,forwardX=Math.Sin(angle),forwardZ=Math.Cos(angle),rightX=Math.Cos(angle),rightZ=-Math.Sin(angle);
        var patch=terrain.Patch(Position,Math.Max(256,distance+columns*spacing+64)/HorizontalScale);
        var spawns=new List<FaunaPrototypeSpawn>();int unsafePlaces=0;
        for(int i=0;i<request.Count;i++)
        {
            int row=i/columns,column=i%columns;int rowCount=Math.Min(columns,request.Count-row*columns);
            double across=(column-(rowCount-1)*.5)*spacing,ahead=distance+row*spacing;
            double x=motion.X+forwardX*ahead+rightX*across,z=motion.Z+forwardZ*ahead+rightZ*across;
            V point=frame.Position(x,z);var sample=patch.Sample(point);double water=sample.WaterSurfaceM;
            if(sample.Underwater&&double.IsNaN(water)&&sourceWorld.Hydrology.OceanConnected[sample.Cell])water=0;
            bool wet=sample.Underwater||!double.IsNaN(water)&&water>sample.ElevationM+.03;
            if(profile.medium=="ground"&&wet||profile.medium=="water"&&(!wet||double.IsNaN(water)||water-sample.ElevationM<Math.Max(.3,profile.lengthM*.65)))
            {unsafePlaces++;continue;}
            ulong id;do{id=++animalManualSequence;}while(animals.ContainsKey(id)||animalManualWanted.Any(s=>s.Id==id));
            var habitat=sourceWorld.Ecology.Habitat[sample.Cell];uint hash=Rng.Mix((uint)id^(uint)(id>>32));
            double airHeight=Math.Max(profile.tiny?.5:1.8,view.transform.localPosition.y-frame.SurfaceY(point,sample.ElevationM));
            spawns.Add(new FaunaPrototypeSpawn{Id=id,Hash=hash,Position=point,Profile=profile,Region=sourceWorld.Ecology.Region[sample.Cell],Taxon=-1,
                CoatSeed=unchecked((int)hash),GroundM=sample.ElevationM,WaterM=water,HeightM=profile.medium=="air"?airHeight:profile.medium=="water"?-.6:0,
                ColdC=habitat.ColdTenthsC/10.0,Aridity=habitat.AridityMilli/1000.0});
        }
        if(spawns.Count==0)return new PlayCommandResult(false,profile.medium=="water"?"No deep enough water just ahead. Move over water and try again.":"No dry ground just ahead. Move onto land and try again.");
        animalLoadFailures.Remove(profile.slug);animalManualWanted.AddRange(spawns);
        return new PlayCommandResult(true,"Queued "+spawns.Count+" "+profile.name+" nearby"+(animalModels.ContainsKey(profile.slug)?".":"; loading its model.")+(unsafePlaces>0?" Skipped "+unsafePlaces+" unsuitable spots.":""));
    }
    void UpdateManualAnimalSpawns()
    {
        if(animalManualWanted.Count==0)return;
        var created=new Dictionary<string,int>();var failed=new HashSet<string>();
        for(int i=animalManualWanted.Count-1;i>=0;i--)
        {
            var spawn=animalManualWanted[i];
            if(animalLoadFailures.Contains(spawn.Profile.slug)){failed.Add(spawn.Profile.slug);animalManualWanted.RemoveAt(i);continue;}
            if((spawn.Position-Position).Length*frame.RadiusM>spawn.Profile.radiusM)
            {PostExplorerChat("Cancelled queued "+spawn.Profile.name+": you moved away.");animalManualWanted.RemoveAt(i);continue;}
            if(!animalModels.TryGetValue(spawn.Profile.slug,out var model))continue;
            if(animalSlots.Count==0)
            {
                var old=animals.Where(p=>!p.Value.Manual).OrderByDescending(p=>(p.Value.Planet-Position).Length).FirstOrDefault();
                if(old.Value==null)break;RemoveAnimal(old.Key);
            }
            AddAnimal(spawn,model,true);animalManualWanted.RemoveAt(i);
            if(!created.ContainsKey(spawn.Profile.name))created.Add(spawn.Profile.name,0);created[spawn.Profile.name]++;
        }
        foreach(var pair in created)PostExplorerChat("Spawned "+pair.Value+" "+pair.Key+". /spawn clear removes command spawns.");
        foreach(string slug in failed)PostExplorerChat("Could not load animal model '"+slug+"'. Its queued spawns were cancelled.",true);
    }
    bool EvictWildAnimalModel()
    {
        var old=animalModels.Where(p=>!animals.Values.Any(a=>a.Manual&&ReferenceEquals(a.Model,p.Value))).OrderBy(p=>p.Value.LastUse).FirstOrDefault();
        if(old.Value==null)return false;
        foreach(ulong id in animals.Where(p=>ReferenceEquals(p.Value.Model,old.Value)).Select(p=>p.Key).ToArray())RemoveAnimal(id);
        Destroy(old.Value.Material);old.Value.Bundle.Unload(true);animalModels.Remove(old.Key);return true;
    }
}

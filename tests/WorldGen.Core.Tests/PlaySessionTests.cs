using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class PlaySessionTests
{
    public static void Run(Action<bool,string> check)
    {
        var s=new PlaySession();s.Validate();check(s.Settings.HorizontalScale==.1&&s.Settings.RealMinutesPerDay==60&&s.Settings.CalendarDaysPerDay==1,"New games default to tenth-scale horizontal geography and a one-hour day");
        s.Accumulate(3600);check(s.SolarDays==1&&s.PendingHistorySeconds==SimulationTime.Day,"One real hour advances one visual and historical day");
        var week=new PlaySession();week.Settings.CalendarDaysPerDay=7;week.Accumulate(3600);check(week.SolarDays==1&&week.PendingHistorySeconds==7*SimulationTime.Day,"Calendar compression changes historical time, not the solar cycle");
        var month=new PlaySession();month.Settings.CalendarDaysPerDay=SimulationTime.Year/(12d*SimulationTime.Day);month.Accumulate(12*3600);check(month.SolarDays==12&&Math.Abs(month.PendingHistorySeconds-SimulationTime.Year)<1e-6,"Twelve one-hour monthly days span exactly one simulation year");
        var copy=month.Copy();copy.Settings.HorizontalScale=1;check(month.Settings.HorizontalScale==.1,"Save copies cannot mutate the live gameplay settings");
        copy.PendingHistorySeconds=.35;var restored=WorldJson.Read<PlaySession>(WorldJson.Write(copy));restored.Validate();check(restored.PendingHistorySeconds==.35&&restored.SolarDays==copy.SolarDays,"Fractional clock credit survives cold JSON reconstruction");
        restored.Consume(.1);check(Math.Abs(restored.PendingHistorySeconds-.25)<1e-9,"Only actually committed history is removed from pending time");
        check(Math.Abs(s.StructureCountFactor-.01)<1e-12,"Tenth-scale density is area-corrected, not accidentally a hundredfold increase");
        check(PlaySession.Legacy().Settings.HorizontalScale==1,"Old saves without game metadata preserve their previous scale");
        foreach(var bad in new[]{double.NaN,double.PositiveInfinity,0,-1,3}){var p=new PlaySession();p.Settings.HorizontalScale=bad;bool rejected=false;try{p.Validate();}catch(ArgumentException){rejected=true;}check(rejected,"Invalid scales are rejected before terrain allocation");}
        var origin=TerrainMapViewport.FromDegrees(35,40);LocalTerrainFrame a=new LocalTerrainFrame(origin,6371000,1500),b=new LocalTerrainFrame(origin,6371000,1500,.1);
        check((a.Position(1000,500)-b.Position(100,50)).Length<1e-12,"Horizontal scale changes ground travel distances by the requested factor");
        check(Math.Abs(a.SurfaceY(origin,1600)-a.SurfaceY(origin,1500)-100)<1e-6&&Math.Abs(b.SurfaceY(origin,1600)-b.SurfaceY(origin,1500)-100)<1e-6,"Vertical metres are not shrunk");
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);string hash=StateHash.Compute(world);var terrain=new LocalTerrain(world);
        int mountain=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>500).OrderByDescending(i=>terrain.Profile(i).MountainHeightM).First();
        var site=world.Surface.Cells[mountain].Position;var original=terrain.Patch(site,50000);var scaledTerrain=new LocalTerrain(world,horizontalScale:.1);var scaledPatch=scaledTerrain.Patch(site,50000);
        double oldGradient=0,newGradient=0,oldResidual=0,newResidual=0;bool sameBase=true;
        for(int i=0;i<101;i++)
        {
            var point=original.Position(-25000+i*500,0);var a0=original.Sample(point);var b0=scaledPatch.Sample(point);
            double a1=a0.ElevationM-a0.BaseElevationM,b1=b0.ElevationM-b0.BaseElevationM;
            if(i>0){oldGradient+=Math.Pow(a1-oldResidual,2);newGradient+=Math.Pow(b1-newResidual,2);}oldResidual=a1;newResidual=b1;sameBase&=a0.BaseElevationM==b0.BaseElevationM;
        }
        check(sameBase,"Physical local-noise wavelengths do not alter macro height anchors or elevation scale");
        check(newGradient<oldGradient*.5,"Tenth-scale local detail no longer compresses kilometre landforms into needle-like slopes");
        Console.WriteLine("Local residual slope RMS ratio at tenth scale: "+Math.Sqrt(newGradient/oldGradient).ToString("F3"));
        foreach(double scale in new[]{.01,.1,1d,2d})
        {
            var plan=TerrainLod.Plan(0,0,2,Math.Min(200000,terrain.RadiusM*scale*.3),scale);check(plan.Count<1600,"Scaled horizon remains a bounded LOD plan");check(plan.Keys.All(k=>k.SizeM<=131072.001),"Scaled distant tiles retain a valid bounded physical size");
            var frame=new LocalTerrainFrame(origin,terrain.RadiusM,terrain.Patch(origin).Sample(0,0).ElevationM,scale);var key=plan.Keys.OrderByDescending(k=>k.Level).First();var mesh=LocalTerrainMesh.Build(terrain,frame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,8);
            check(mesh.Positions.All(float.IsFinite),"Distant scaled tiles use bounded source subpatches without invalid heights");
            var scatter=new LocalScatter(terrain,scale);check(scatter.Around(origin,1800).Length<1200,"Vegetation neighbourhood size does not explode on a smaller planet");
            check(scatter.Build(scatter.Address(origin)).Instances.All(p=>p.HeightM>0),"Scaled scatter retains physical object dimensions");
        }
        string root=Path.Combine(Path.GetTempPath(),"worldgen-play-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var library=new WorldLibrary(root);var play=new PlaySession{HasPlayer=true,PlayerPosition=origin,Flying=true,Heading=45,Pitch=10,HeightAboveGround=123,SolarDays=12.5,PendingHistorySeconds=.3};play.Settings.CalendarDaysPerDay=7;
            var first=library.SaveNew("Playable world",world,play:play);var cold=WorldLibrary.ReadPlaySession(first.SnapshotPath);cold.Validate();
            check(WorldJson.Write(cold)==WorldJson.Write(play),"Atomic saves restore game rules, clock credit, player position and view exactly");
            var next=play.Copy();next.PlayerPosition=TerrainMapViewport.FromDegrees(-10,20);var second=library.SaveAdditional(first.GroupId,world,play:next);
            check(WorldLibrary.ReadPlaySession(first.SnapshotPath).PlayerPosition.X==origin.X&&WorldLibrary.ReadPlaySession(second.SnapshotPath).PlayerPosition.X==next.PlayerPosition.X,"Each save has its own player position and settings");
            check(StateHash.Compute(library.Load(second.Id))==hash&&StateHash.Compute(world)==hash,"Gameplay metadata does not mutate generation or historical world bytes");
            var legacy=library.SaveNew("Legacy",world);check(WorldLibrary.ReadPlaySession(legacy.SnapshotPath).Settings.HorizontalScale==1,"Legacy library entries do not silently shrink");
        }
        finally{Directory.Delete(root,true);}
    }
}

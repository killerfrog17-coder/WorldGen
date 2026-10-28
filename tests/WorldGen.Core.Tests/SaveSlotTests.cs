using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WorldGen.Core;
using WorldGen.Serialization;

static class SaveSlotTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        var root=Path.Combine(Path.GetTempPath(),"worldgen-slot-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var recipe=Recipe.Preset("Earth");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);string hash=world.Stages.Last().Hash;
        var play=new PlaySession();play.Survival.Blood=new List<PlayerBloodLayer>{new PlayerBloodLayer{Renderer="body",Mask=new byte[65536]}};play.Survival.Blood[0].Mask[99]=217;
        play.Wildlife.Individuals.Add(new WildlifeIndividual{Id=9,Species="deer",Position=world.Surface.Cells[0].Position,Health=0,MaximumHealth=100,BloodMask=new byte[65536],CorpsePose=new double[16]});play.Wildlife.Individuals[0].BloodMask[43]=200;
        play.Inventory.Items.Add(new Possession{Id="owned-rock",Kind=PossessionKind.SmallRock,Place=PossessionPlace.RightHand,Position=world.Surface.Cells[0].Position});play.Validate();
        var library=new WorldLibrary(root);var timer=Stopwatch.StartNew();var first=library.SaveNamed(null,"Camp",world,play);timer.Stop();double cold=timer.Elapsed.TotalMilliseconds;
        Check(library.Entries().Length==1&&first.Kind=="manual","First named save publishes one manual slot");
        var baseFile=Directory.GetFiles(Path.Combine(root,".bases"),"*.world").Single();var baseTime=File.GetLastWriteTimeUtc(baseFile);long baseBytes=new FileInfo(baseFile).Length;
        Check(new FileInfo(first.SnapshotPath).Length<4096&&baseBytes>4096,"Slot references a separate shared full planet");
        Check(StateHash.Compute(library.Load(first.Id))==hash,"Cold shared-base load preserves exact planetary identity");
        var restored=WorldLibrary.ReadPlaySession(first.SnapshotPath);
        Check(restored.Survival.Blood[0].Mask.SequenceEqual(play.Survival.Blood[0].Mask)&&restored.Wildlife.Individuals[0].BloodMask.SequenceEqual(play.Wildlife.Individuals[0].BloodMask),"Both player and animal blood masks round-trip losslessly");
        Check(restored.Wildlife.Individuals[0].Dead&&restored.Inventory.Items.Single().Id=="owned-rock","Corpses and inventory round-trip through compact session storage");
        Check(new FileInfo(Path.Combine(Path.GetDirectoryName(first.SnapshotPath)!,"play-session.bin")).Length<65536,"Binary compressed runtime state avoids the old blood-mask JSON expansion");
        play.SolarDays=3;timer.Restart();var overwrite=library.SaveNamed(first.GroupId,"Camp",world,play);timer.Stop();double warm=timer.Elapsed.TotalMilliseconds;
        Check(overwrite.Id==first.Id&&overwrite.Revision!=first.Revision&&library.Entries().Length==1,"Same name replaces the same slot rather than appending a save");
        Check(WorldLibrary.ReadPlaySession(overwrite.SnapshotPath).SolarDays==3,"Overwrite loads the latest gameplay state");
        Check(File.GetLastWriteTimeUtc(baseFile)==baseTime&&new FileInfo(baseFile).Length==baseBytes,"Warm save does not rewrite/recompress unchanged planetary data");
        Check(File.Exists(first.SnapshotPath),"Immediately previous revision survives as a recovery copy");
        var separate=library.SaveNamed(first.GroupId,"Hunting",world,play);
        Check(separate.Id!=first.Id&&separate.GroupId==first.GroupId&&library.Entries().Length==2,"New name creates another slot within the same playthrough");
        var quick=library.SaveNamed(first.GroupId,"Hunting",world,play,true,separate.Id);play.SolarDays=4;
        var quick2=library.SaveNamed(first.GroupId,"Hunting",world,play,true,separate.Id);
        Check(quick.Id==quick2.Id&&quick2.Kind=="quick"&&quick2.ParentSaveId==separate.Id,"Quicksaves update one separate linked slot");
        Check(WorldLibrary.ReadPlaySession(quick2.SnapshotPath).SolarDays==4&&WorldLibrary.ReadPlaySession(separate.SnapshotPath).SolarDays==3,"Quicksaving does not overwrite manual saves");
        Check(Directory.GetFiles(Path.Combine(root,".bases"),"*.world").Length==1,"Different names and quicksaves share one planet");
        for(int i=0;i<3;i++)overwrite=library.SaveNamed(first.GroupId,"Camp",world,play);
        Check(Directory.GetDirectories(Path.Combine(root,first.Id,"revisions")).Length==2,"Overwrite revision retention stays bounded to current and previous");
        var revision=overwrite.Revision;using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{library.SaveNamed(first.GroupId,"Camp",world,play,cancellation:cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(library.Entries().Single(e=>e.Id==first.Id).Revision==revision,"Cancelled overwrite leaves the published slot intact");
        var invalid=play.Copy();invalid.Inventory.Items.Add(invalid.Inventory.Items[0].Copy());try{library.SaveNamed(first.GroupId,"Camp",world,invalid);throw new Exception("Invalid state accepted");}catch(ArgumentException){}
        Check(library.Entries().Single(e=>e.Id==first.Id).Revision==revision,"Invalid state cannot replace a healthy save");
        var legacyDir=Path.Combine(root,"legacy-fixture");Directory.CreateDirectory(legacyDir);string legacyPath=Path.Combine(legacyDir,"snapshot.world");File.WriteAllBytes(legacyPath,WorldFile.Write(world));File.WriteAllText(Path.Combine(legacyDir,"play-session.json"),WorldJson.Write(play));
        Check(new FileInfo(Path.Combine(legacyDir,"play-session.json")).Length>65536,"Legacy fixture reproduces a valid session above the obsolete64KB cap");
        Check(WorldLibrary.ReadPlaySession(legacyPath).Wildlife.Individuals[0].BloodMask[43]==200&&StateHash.Compute(WorldFile.ReadFile(legacyPath))==hash,"Old full worlds and oversized JSON sessions remain readable");
        var engine=new Prehistory(world);world.Prehistory=engine.Prepare();var historic=library.SaveNamed(first.GroupId,"History",world,play);string historyHash=world.Prehistory.Hash;
        Check(library.Load(historic.Id).Prehistory.Hash==historyHash,"Historical state round-trips separately from shared geography");
        world.Prehistory=engine.Start(world.Prehistory);var progressed=library.SaveNamed(first.GroupId,"History",world,play);
        var latestHistory=library.Load(progressed.Id).Prehistory;
        Check(latestHistory.Started&&latestHistory.Hash==world.Prehistory.Hash,"Published historical overwrite loads latest state");
        Check(WorldFile.ReadFile(historic.SnapshotPath).Prehistory.Hash==historyHash,"Previous historical revision remains an exact checkpoint");
        Check(Directory.GetFiles(Path.Combine(root,".bases"),"*.world").Length==1&&File.GetLastWriteTimeUtc(baseFile)==baseTime,"Changing history leaves the shared planet untouched");
        var orphan=library.SaveNamed(null,"Early quick",world,play,true);var manual=library.SaveNamed(orphan.GroupId,"Early quick",world,play);
        Check(manual.Id!=orphan.Id&&manual.GroupId==orphan.GroupId,"F5 before a manual save still permits a separate later manual slot");
        library.DeleteSave(separate.Id);Check(WorldLibrary.ReadPlaySession(quick2.SnapshotPath).SolarDays==4,"Deleting a linked manual slot does not delete its quicksave or shared planet");
        Check(StateHash.Compute(world)==hash,"Saving never mutates the immutable planetary state");
        WorldLibraryTests.Run(Check);
        Console.WriteLine("Save timing 5000-cell planet: first="+cold.ToString("F1")+"ms, warm="+warm.ToString("F1")+"ms, base="+baseBytes+" bytes; fixture "+root);
        Console.WriteLine("PASS "+checks+" named/quicksave, atomic revision, legacy, blood and shared-history checks");
        // Retain the small diagnostic fixture for inspection; never remove owner saves.
    }
}

using WorldGen.Presentation;

static class WildlifeSoundFileIndexTests
{
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine("artifacts","sound-index-tests",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(root);
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        string manifest=Path.Combine(root,"library.tsv"),wave=Path.Combine(root,"cricket.wav");
        string row="cricket\t1\t1\tcricket.wav\t"+new string('0',64)+"\t1\tambient\n";
        var empty=WildlifeSoundFileIndex.Scan(root);Check(empty.Catalogue.Takes.Count==0&&ReferenceEquals(empty,WildlifeSoundFileIndex.Scan(root,empty)),"Unchanged missing library was reconstructed");
        File.WriteAllBytes(wave,new byte[256]);File.WriteAllText(manifest,row);
        var loaded=WildlifeSoundFileIndex.Scan(root,empty);Check(loaded.Catalogue.Takes.Count==1&&loaded.Missing==0,"New manifest did not load its valid file");
        Check(ReferenceEquals(loaded,WildlifeSoundFileIndex.Scan(root,loaded)),"Unchanged catalogue was reparsed");
        DateTime stamp=File.GetLastWriteTimeUtc(wave);File.WriteAllBytes(wave,new byte[32]);File.SetLastWriteTimeUtc(wave,stamp);
        var truncated=WildlifeSoundFileIndex.Scan(root,loaded);Check(truncated.Missing==1&&truncated.Catalogue.Takes.Count==0,"Same-timestamp size change was missed");
        File.WriteAllBytes(wave,new byte[256]);File.SetLastWriteTimeUtc(wave,stamp);
        var restored=WildlifeSoundFileIndex.Scan(root,truncated);Check(restored.Catalogue.Takes.Count==1,"Repaired audio was not discovered");
        File.Delete(wave);var removed=WildlifeSoundFileIndex.Scan(root,restored);
        Check(removed.Missing==1&&removed.Catalogue.Takes.Count==0,"Deleted bad audio remained in the catalogue");
        File.WriteAllBytes(wave,new byte[256]);var returned=WildlifeSoundFileIndex.Scan(root,removed);
        Check(returned.Catalogue.Takes.Count==1,"A returned missing file stayed unavailable");
        var forced=WildlifeSoundFileIndex.Scan(root);Check(!ReferenceEquals(returned,forced)&&forced.Catalogue.Takes.Count==1,"Explicit forced reload did not rebuild");
        File.AppendAllText(manifest,"frog\t1\t1\tfrog.wav\t"+new string('1',64)+"\t1\tambient\n");
        var updated=WildlifeSoundFileIndex.Scan(root,returned);Check(updated.Missing==1&&updated.Catalogue.Takes.Count==1,"Manifest changes were missed");
        File.WriteAllBytes(Path.Combine(root,"frog.wav"),new byte[256]);var newTake=WildlifeSoundFileIndex.Scan(root,updated);
        Check(newTake.Missing==0&&newTake.Catalogue.Takes.Count==2,"Newly created manifest audio was missed");
        File.Delete(manifest);var noManifest=WildlifeSoundFileIndex.Scan(root,newTake);Check(noManifest.Catalogue.Takes.Count==0,"Removed manifest retained an old library");
        Console.WriteLine($"PASS {checks} sound index checks: unchanged reuse, edits, same-time truncation, deletion, recovery, new files, missing manifest and explicit reload");
    }
}

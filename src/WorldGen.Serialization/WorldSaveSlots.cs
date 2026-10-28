using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    [Serializable] internal sealed class SharedWorldReference
    {public int Version=1;public string BaseId="",HistoryHash="";}
    public static partial class WorldFile
    {
        internal const string SlotMagic="WGWSLOT1";
        internal static bool ValidBase(string id)=>id!=null&&id.Length==64&&id.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        public static World ReadFile(string path,CancellationToken token=default,Action<string> status=null)
        {
            token.ThrowIfCancellationRequested();path=Path.GetFullPath(path);if(new FileInfo(path).Length>MaximumFileBytes)throw new InvalidDataException("World snapshot exceeds the supported file size.");var header=new byte[8];using(var stream=File.OpenRead(path))stream.Read(header,0,8);
            if(Encoding.ASCII.GetString(header)!=SlotMagic)return Read(File.ReadAllBytes(path),token,status);
            if(new FileInfo(path).Length>4096)throw new InvalidDataException("Invalid world reference size.");
            var reference=WorldJson.Read<SharedWorldReference>(Encoding.UTF8.GetString(File.ReadAllBytes(path),8,(int)new FileInfo(path).Length-8));
            if(reference==null||reference.Version!=1||!ValidBase(reference.BaseId))throw new InvalidDataException("Invalid world reference.");
            var revision=new DirectoryInfo(Path.GetDirectoryName(path));var revisions=revision.Parent;var slot=revisions?.Parent;var library=slot?.Parent;
            if(library==null||!Guid.TryParseExact(revision.Name,"N",out _)||revisions.Name!="revisions"||!(slot.Name=="earth"||Guid.TryParseExact(slot.Name,"N",out _)))throw new InvalidDataException("World reference is outside its save library.");
            status?.Invoke("Loading shared planetary data");string basePath=Path.Combine(library.FullName,".bases",reference.BaseId+".world");
            if(!File.Exists(basePath))throw new FileNotFoundException("This save's shared planetary data is missing.",basePath);
            if(new FileInfo(basePath).Length>MaximumFileBytes)throw new InvalidDataException("Shared world exceeds the supported file size.");
            var world=Read(File.ReadAllBytes(basePath),token,status);if(world.Stages.Last().Hash!=reference.BaseId)throw new InvalidDataException("Shared world identity mismatch.");
            if(!string.IsNullOrEmpty(reference.HistoryHash))
            {
                status?.Invoke("Loading saved history");world.Prehistory=SaveStateFile.Read<PrehistoryState>(Path.Combine(revision.FullName,"history.bin"),"WGHIST01",4000000000L,token);
                if(world.Prehistory.Hash!=reference.HistoryHash)throw new InvalidDataException("Saved historical identity mismatch.");
                new Prehistory(world,token).Validate(world.Prehistory);
            }
            return world;
        }
    }
    public sealed partial class WorldLibrary
    {
        readonly object slotGate=new object();readonly Dictionary<string,Prehistory> saveValidators=new Dictionary<string,Prehistory>();
        public WorldLibraryEntry SaveNamed(string groupId,string name,World world,PlaySession play=null,bool quick=false,string parentSaveId=null,CancellationToken cancellation=default)
        {
            lock(slotGate)
            {
                name=(name??"").Trim();if(name.Length<1||name.Length>80||name.Any(char.IsControl))throw new ArgumentException("Use a save name of 1-80 characters.");
                cancellation.ThrowIfCancellationRequested();play?.Validate();
                groupId=groupId??Guid.NewGuid().ToString("N");Folder(groupId);
                var siblings=Entries().Where(e=>e.GroupId==groupId&&e.Error.Length==0).ToArray();
                var previous=siblings.Where(e=>quick?e.Kind=="quick":e.Kind!="quick"&&string.Equals(e.Name,name,StringComparison.OrdinalIgnoreCase)).OrderByDescending(e=>e.SavedUtc).FirstOrDefault();
                string id=previous?.Id??(siblings.Length==0&&!Directory.Exists(Folder(groupId))?groupId:Guid.NewGuid().ToString("N"));
                string baseId=world?.Stages?.LastOrDefault()?.Hash;if(!WorldFile.ValidBase(baseId))throw new InvalidDataException("World has no valid planetary checkpoint.");
                string baseRoot=Path.Combine(Root,".bases"),basePath=Path.Combine(baseRoot,baseId+".world");Directory.CreateDirectory(baseRoot);
                if(!File.Exists(basePath))
                {
                    var ground=new World();foreach(var field in typeof(World).GetFields(BindingFlags.Public|BindingFlags.Instance))field.SetValue(ground,field.GetValue(world));ground.Prehistory=null;
                    WorldJson.ValidateWorld(ground,cancellation);byte[] packed;
                    try{packed=WorldFile.Write(ground,cancellation,CompressionLevel.Fastest);}catch(ArgumentException){packed=WorldFile.Write(ground,cancellation);}
                    string pendingBase=Path.Combine(baseRoot,".pending-"+Guid.NewGuid().ToString("N"));File.WriteAllBytes(pendingBase,packed);cancellation.ThrowIfCancellationRequested();File.Move(pendingBase,basePath);
                }
                if(world.Prehistory!=null)
                {
                    string key=baseId+"/"+world.Prehistory.Version+"/"+world.Prehistory.History?.Version;
                    if(!saveValidators.TryGetValue(key,out var validator)){validator=new Prehistory(world,cancellation);saveValidators.Add(key,validator);}validator.Validate(world.Prehistory);
                }
                string revision=Guid.NewGuid().ToString("N"),slotRoot=Folder(id),pending=Path.Combine(Root,".pending-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(pending);
                string groupName=siblings.Select(e=>e.GroupName).FirstOrDefault(n=>!string.IsNullOrWhiteSpace(n))??siblings.FirstOrDefault()?.Name??name;
                var entry=new WorldLibraryEntry{Id=id,GroupId=groupId,GroupName=groupName,Name=quick?"Quicksave - "+name:name,ManualName=name,Kind=quick?"quick":"manual",ParentSaveId=quick?parentSaveId??"":"",Revision=revision,
                    AlgorithmVersion=world.Recipe.AlgorithmVersion,TerrainSource=world.Recipe.TerrainSource.ToString(),Detail=world.Surface.Cells.Length,SavedUtc=DateTime.UtcNow.ToString("O")};
                var reference=new SharedWorldReference{BaseId=baseId,HistoryHash=world.Prehistory?.Hash??""};
                byte[] json=Encoding.UTF8.GetBytes(WorldJson.Write(reference)),magic=Encoding.ASCII.GetBytes(WorldFile.SlotMagic);
                using(var stream=new FileStream(Path.Combine(pending,"snapshot.world"),FileMode.CreateNew,FileAccess.Write)){stream.Write(magic,0,magic.Length);stream.Write(json,0,json.Length);stream.Flush(true);}
                if(world.Prehistory!=null)SaveStateFile.Write(Path.Combine(pending,"history.bin"),"WGHIST01",world.Prehistory,4000000000L,cancellation,CompressionLevel.Optimal);
                if(play!=null)SaveStateFile.Write(Path.Combine(pending,"play-session.bin"),"WGPLAY01",play,1073741824,cancellation);
                string metadata=WorldJson.Write(entry);cancellation.ThrowIfCancellationRequested();
                if(!Directory.Exists(slotRoot))
                {
                    string container=Path.Combine(Root,".pending-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(container,"revisions"));Directory.Move(pending,Path.Combine(container,"revisions",revision));
                    WriteSlotMetadata(Path.Combine(container,"metadata.json"),metadata);cancellation.ThrowIfCancellationRequested();Directory.Move(container,slotRoot);
                }
                else
                {
                    if((File.GetAttributes(slotRoot)&FileAttributes.ReparsePoint)!=0)throw new IOException("Cannot replace saves through directory links.");
                    string revisionsRoot=Path.Combine(slotRoot,"revisions");Directory.CreateDirectory(revisionsRoot);
                    if((File.GetAttributes(revisionsRoot)&FileAttributes.ReparsePoint)!=0)throw new IOException("Cannot replace save revisions through directory links.");
                    Directory.Move(pending,Path.Combine(revisionsRoot,revision));
                    string next=Path.Combine(slotRoot,".metadata-"+Guid.NewGuid().ToString("N"));WriteSlotMetadata(next,metadata);cancellation.ThrowIfCancellationRequested();
                    File.Replace(next,Path.Combine(slotRoot,"metadata.json"),null);
                }
                entry.SnapshotPath=Path.Combine(slotRoot,"revisions",revision,"snapshot.world");
                // Retain the immediately previous revision as recovery, not an
                // unbounded series of duplicate quicksaves. Never touch bases.
                foreach(string old in Directory.GetDirectories(Path.Combine(slotRoot,"revisions")))
                    if(Guid.TryParseExact(Path.GetFileName(old),"N",out _)&&Path.GetFileName(old)!=revision&&Path.GetFileName(old)!=previous?.Revision&&(File.GetAttributes(old)&FileAttributes.ReparsePoint)==0)
                        try
                        {
                            // Only known payloads in our own revision directory.
                            // Unexpected files/subdirectories are retained.
                            foreach(string file in new[]{"snapshot.world","history.bin","play-session.bin"})File.Delete(Path.Combine(old,file));
                            Directory.Delete(old,false);
                        }
                        catch(IOException){}catch(UnauthorizedAccessException){}
                return entry;
            }
        }
        static void WriteSlotMetadata(string path,string metadata)
        {var bytes=Encoding.UTF8.GetBytes(metadata);using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
    }
}

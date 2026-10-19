using System;
using System.IO;
using System.Linq;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    [Serializable]
    public sealed class WorldLibraryEntry
    {
        public string Id = "", GroupId = "", Name = "", AlgorithmVersion = "", TerrainSource = "", SavedUtc = "", Error = "", SnapshotPath = "";
        public int Detail;
    }

    /// <summary>Named, immutable full-world saves. Filesystem metadata is not generation state.</summary>
    public sealed class WorldLibrary
    {
        public string Root { get; }
        public WorldLibrary(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A world library folder is required.");
            Root = Path.GetFullPath(root); Directory.CreateDirectory(Root);
        }
        static bool ValidId(string id) => id == "earth" || (id != null && Guid.TryParseExact(id, "N", out _));
        string Folder(string id)
        {
            if (!ValidId(id)) throw new ArgumentException("Invalid world identifier.");
            return Path.Combine(Root, id);
        }
        public WorldLibraryEntry[] Entries()
        {
            return Directory.GetDirectories(Root).Where(p => ValidId(Path.GetFileName(p))).Select(p =>
            {
                string id = Path.GetFileName(p);
                try
                {
                    var entry = WorldJson.Read<WorldLibraryEntry>(File.ReadAllText(Path.Combine(p, "metadata.json")));
                    if (entry == null || entry.Id != id || string.IsNullOrWhiteSpace(entry.Name)) throw new InvalidDataException("Invalid save metadata.");
                    if (!ValidId(entry.GroupId)) entry.GroupId = id; // Legacy snapshots are individual worlds.
                    entry.SnapshotPath = Path.Combine(p, "snapshot.world");
                    entry.Error = File.Exists(entry.SnapshotPath) ? "" : "Snapshot is missing.";
                    return entry;
                }
                catch (Exception e) { return new WorldLibraryEntry { Id = id, GroupId = id, Name = id == "earth" ? "Earth" : "Unreadable save", Error = e.Message }; }
            }).OrderBy(e => e.Id == "earth" ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        }
        public World Load(string id, CancellationToken cancellation = default)
        {
            string path = Path.Combine(Folder(id), "snapshot.world");
            cancellation.ThrowIfCancellationRequested();
            if (new FileInfo(path).Length > WorldFile.MaximumFileBytes) throw new InvalidDataException("World snapshot is too large.");
            return WorldFile.Read(File.ReadAllBytes(path), cancellation);
        }
        public WorldLibraryEntry SaveNew(string name, World world, CancellationToken cancellation = default,PlaySession play=null)
            => Save(Guid.NewGuid().ToString("N"), name, world, cancellation,play:play);
        public WorldLibraryEntry SaveAdditional(string groupId, World world, CancellationToken cancellation = default,PlaySession play=null)
        {
            Folder(groupId);
            var previous = Entries().FirstOrDefault(e => e.GroupId == groupId && e.Error.Length == 0)
                ?? throw new ArgumentException("The world no longer exists.");
            return Save(Guid.NewGuid().ToString("N"), previous.Name, world, cancellation, groupId,play);
        }
        public static PlaySession ReadPlaySession(string snapshotPath)
        {
            string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(snapshotPath)),"play-session.json");
            if(!File.Exists(path))return PlaySession.Legacy();
            if(new FileInfo(path).Length>65536)throw new InvalidDataException("Play session is too large.");
            var session=WorldJson.Read<PlaySession>(File.ReadAllText(path))??throw new InvalidDataException("Missing play session.");session.Validate();return session;
        }
        // Recoverable deletion. Never follow a user-created directory junction outside the library.
        public void DeleteSave(string id)
        {
            string folder = Folder(id);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Save no longer exists.");
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot delete saves through a directory link.");
            string trash = Path.Combine(Root, ".trash");
            Directory.CreateDirectory(trash);
            if ((File.GetAttributes(trash) & FileAttributes.ReparsePoint) != 0) throw new IOException("Trash must not be a directory link.");
            Directory.Move(folder, Path.Combine(trash, id + "-" + Guid.NewGuid().ToString("N")));
        }
        public void DeleteWorld(string groupId)
        {
            Folder(groupId);
            foreach (var entry in Entries().Where(e => e.GroupId == groupId)) DeleteSave(entry.Id);
        }
        public void InstallEarth(string bundledSnapshot, CancellationToken cancellation = default)
        {
            if (Directory.Exists(Folder("earth"))) return; // Never overwrite a user's existing library entry.
            Save("earth", "Earth", WorldFile.Read(File.ReadAllBytes(bundledSnapshot), cancellation), cancellation);
        }
        WorldLibraryEntry Save(string id, string name, World world, CancellationToken cancellation, string groupId = null,PlaySession play=null)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80 || name.Any(char.IsControl)) throw new ArgumentException("Use a world name of 1–80 characters.");
            cancellation.ThrowIfCancellationRequested();
            WorldJson.ValidateWorld(world, cancellation);
            play?.Validate();
            string pending = Path.Combine(Root, ".pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending);
            // Publish by directory rename only after every file is complete. On failure
            // leave the hidden staging directory recoverable; never expose a partial save.
            var entry = new WorldLibraryEntry { Id = id, GroupId = groupId ?? id, Name = name, AlgorithmVersion = world.Recipe.AlgorithmVersion,
                TerrainSource = world.Recipe.TerrainSource.ToString(), Detail = world.Surface.Cells.Length, SavedUtc = DateTime.UtcNow.ToString("O") };
            File.WriteAllBytes(Path.Combine(pending, "snapshot.world"), WorldFile.Write(world, cancellation));
            cancellation.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(pending, "recipe.json"), WorldJson.Write(world.Recipe));
            File.WriteAllBytes(Path.Combine(pending, "heightmap.pgm"), WorldJson.Heightmap(world));
            File.WriteAllText(Path.Combine(pending, "metadata.json"), WorldJson.Write(entry));
            if(play!=null)File.WriteAllText(Path.Combine(pending,"play-session.json"),WorldJson.Write(play));
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(pending, Folder(id));
            entry.SnapshotPath = Path.Combine(Folder(id), "snapshot.world");
            return entry;
        }
    }
}

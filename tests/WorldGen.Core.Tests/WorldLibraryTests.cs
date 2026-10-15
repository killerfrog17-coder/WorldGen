using WorldGen.Core;
using WorldGen.Serialization;

static class WorldLibraryTests
{
    public static void Run(Action<bool, string> check)
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "worldgen-library-tests-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(testRoot, "world");
        Directory.CreateDirectory(testRoot);
        try
        {
            var library = new WorldLibrary(root);
            check(library.Entries().Length == 0, "Missing world folder initializes as an empty library");
            var recipe = Recipe.Preset("Earth"); recipe.Detail = 5000;
            World earth = new WorldPipeline().Generate(recipe);
            string original = StateHash.Compute(earth);
            var first = library.SaveNew("Earth test", earth);
            var second = library.SaveNew("Earth test", earth);
            check(first.Id != second.Id, "Duplicate display names create distinct immutable saves");
            check(library.Entries().Length == 2, "World browser lists both saves");
            check(first.Name == "Earth test" && first.AlgorithmVersion == earth.Recipe.AlgorithmVersion && first.Detail == 5000 && first.TerrainSource == "Earth", "Browser metadata describes saved world");
            check(string.IsNullOrEmpty(first.Error), "Valid save has no browser error");
            string directory = Path.GetDirectoryName(first.SnapshotPath)!;
            check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Snapshot resides in the selected world library");
            check(File.Exists(first.SnapshotPath) && File.Exists(Path.Combine(directory, "recipe.json")) && File.Exists(Path.Combine(directory, "metadata.json")) && File.Exists(Path.Combine(directory, "heightmap.pgm")), "Published world contains snapshot, recipe, metadata and heightmap");
            check(File.ReadAllBytes(Path.Combine(directory, "heightmap.pgm")).SequenceEqual(WorldJson.Heightmap(earth)), "Stored heightmap matches saved terrain export");
            check(StateHash.Compute(WorldJson.ReadRecipe(File.ReadAllText(Path.Combine(directory, "recipe.json")))) == StateHash.Compute(recipe), "Saved recipe is complete and exact");
            var reopened = new WorldLibrary(root);
            var loaded = reopened.Load(first.Id);
            check(StateHash.Compute(loaded) == original, "Reopening the library restores all saved state exactly without regenerating");
            check(loaded.ElevationM.SequenceEqual(earth.ElevationM), "Saved world retains exact height values");
            check(StateHash.Compute(loaded.Climate) == StateHash.Compute(earth.Climate), "Saved world retains all seasonal and monthly calculated climate values");
            check(StateHash.Compute(loaded.Hydrology) == StateHash.Compute(earth.Hydrology), "Saved world retains drainage and fractional lake data");
            check(StateHash.Compute(earth) == original, "Saving does not mutate authoritative world state");
            var bundled = new WorldLibrary(Path.Combine(testRoot, "bundled-world"));
            bundled.InstallEarth(first.SnapshotPath);
            check(bundled.Entries().Single().Id == "earth" && bundled.Entries().Single().Name == "Earth", "Bundled Earth is discoverable by name without a file path");
            check(StateHash.Compute(bundled.Load("earth")) == original, "Bundled Earth installs exact calculated world state");
            bundled.InstallEarth(Path.Combine(testRoot, "does-not-exist.world"));
            check(bundled.Entries().Length == 1 && StateHash.Compute(bundled.Load("earth")) == original, "Repeated bundled installation does not overwrite an existing Earth save");
            Directory.CreateDirectory(Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N")));
            check(library.Entries().Length == 2, "Incomplete pending directories never appear in the world browser");

            foreach (string id in new[] { "../escape", "..\\escape", "..", "", "/outside", "C:\\outside", first.Id + "/child" })
                Throws<ArgumentException>(() => reopened.Load(id), check, "Library rejects unsafe save ID: " + id);
            Throws<ArgumentException>(() => library.SaveNew("", earth), check, "Blank display name rejected");
            Throws<ArgumentException>(() => library.SaveNew("bad\nname", earth), check, "Control characters in display name rejected");
            Throws<ArgumentException>(() => library.SaveNew(new string('x', 1000), earth), check, "Unbounded display name rejected");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => library.SaveNew("Cancelled save", earth, cancellation.Token), check, "Cancelled save is rejected");
                Throws<OperationCanceledException>(() => library.Load(first.Id, cancellation.Token), check, "Cancelled load is rejected");
            }
            check(library.Entries().Length == 2, "Invalid or cancelled saves publish no partial entries");

            var extra = library.SaveAdditional(first.GroupId, earth);
            check(extra.Id != first.Id && extra.GroupId == first.Id, "Additional saves share a world identity, not a filename");
            check(library.Entries().Count(e => e.GroupId == first.Id) == 2, "Multiple saves group under one world");
            check(StateHash.Compute(library.Load(extra.Id)) == original, "Additional save restores exact state");
            library.DeleteSave(extra.Id);
            check(library.Entries().Length == 2 && File.Exists(first.SnapshotPath), "Deleting one save preserves the other saves");
            check(Directory.GetDirectories(Path.Combine(root, ".trash")).Length == 1, "Deleted snapshot remains recoverable in trash");
            var separate = library.SaveNew("Delete test", earth);
            var another = library.SaveAdditional(separate.Id, earth);
            library.DeleteSave(separate.Id);
            var continued = library.SaveAdditional(separate.Id, earth);
            check(continued.GroupId == separate.Id, "World identity survives deletion of its first save");
            library.DeleteWorld(separate.Id);
            check(library.Entries().Length == 2 && !File.Exists(another.SnapshotPath), "Deleting a world removes all its remaining saves, not other worlds");
            Throws<ArgumentException>(() => library.DeleteSave("../escape"), check, "Delete rejects unsafe IDs");
            Throws<ArgumentException>(() => library.DeleteWorld("../escape"), check, "Group deletion rejects unsafe IDs");

            // Corruption in one save must not prevent the browser opening healthy saves.
            string secondDirectory = Path.GetDirectoryName(second.SnapshotPath)!;
            File.WriteAllText(Path.Combine(secondDirectory, "metadata.json"), "{ broken metadata");
            var entries = reopened.Entries();
            check(entries.Length == 2 && entries.Any(e => e.Id == second.Id && !string.IsNullOrEmpty(e.Error)), "Malformed metadata is isolated to an unavailable browser entry");
            check(StateHash.Compute(reopened.Load(first.Id)) == original, "Another corrupt save does not affect loading a healthy world");
            File.WriteAllBytes(first.SnapshotPath, new byte[] { 0, 1, 2 });
            ThrowsAny(() => reopened.Load(first.Id), check, "Corrupt snapshot rejected rather than regenerated silently");
        }
        finally
        {
            // The exact, freshly allocated test directory owns every file removed here.
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    static void Throws<T>(Action action, Action<bool, string> check, string message) where T : Exception
    {
        bool caught = false;
        try { action(); } catch (T) { caught = true; }
        check(caught, message);
    }

    static void ThrowsAny(Action action, Action<bool, string> check, string message)
    {
        bool caught = false;
        try { action(); } catch (Exception e) when (e is ArgumentException || e is IOException || e is InvalidOperationException) { caught = true; }
        check(caught, message);
    }
}

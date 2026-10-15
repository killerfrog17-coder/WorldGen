using WorldGen.Core;

public static class PlateShapeTests
{
    private static readonly int[] Seeds = { 73421, 42, 0, 17, 98765 };

    public static void Run(Action<bool, string> check)
    {
        foreach (int count in new[] { 80, 500 })
        {
            double medianAxisSum = 0;
            foreach (int seed in Seeds)
            {
                var recipe = Recipe.Preset("Preview"); recipe.Seed = seed; recipe.MicroplateCount = count;
                var surface = SphericalMesh.BuildCoarse(recipe, default);
                var first = new World { Recipe = recipe, Surface = surface };
                PlateHierarchy.Microplates(first, default);

                check(first.Microplates.Length == count, $"P{count} seed {seed} retains requested plate count");
                check(first.CellMicroplate.Length == SphericalMesh.CanonicalCellCount && first.CellMicroplate.All(p => p >= 0 && p < count), $"P{count} seed {seed} classifies every coarse cell");
                check(first.Microplates.All(p => p.Cells.Length > 0), $"P{count} seed {seed} has no empty plates");
                check(first.Microplates.All(p => Connected(surface, p.Cells)), $"P{count} seed {seed} keeps every plate connected");

                var repeat = new World { Recipe = recipe.Clone(), Surface = surface };
                PlateHierarchy.Microplates(repeat, default);
                check(first.CellMicroplate.SequenceEqual(repeat.CellMicroplate), $"P{count} seed {seed} plate growth repeats exactly");
                check(StateHash.Compute(first.Microplates) == StateHash.Compute(repeat.Microplates), $"P{count} seed {seed} plate records repeat exactly");

                var shape = PlateShapeDiagnostics.Assess(first);
                medianAxisSum += shape.MedianAxisRatio;
                check(shape.DisconnectedPlates == 0, $"P{count} seed {seed} diagnostic confirms connectivity");
                check(shape.P90Q < 8, $"P{count} seed {seed} plate perimeter remains broadly bounded");
            }
            check(medianAxisSum / Seeds.Length >= 1.45, $"P{count} plates retain reviewed broad directional shape");
        }

        // Exercise the connected fine-projection repair at the highest supported
        // plate count on the inexpensive 5k terrain mesh.
        var fineRecipe = Recipe.Preset("Preview"); fineRecipe.Seed = Seeds[0]; fineRecipe.MicroplateCount = 500; fineRecipe.Detail = 5000;
        var fine = new WorldPipeline().Generate(fineRecipe);
        check(fine.Microplates.Length == 500 && fine.CellMicroplate.Length == 5000, "500-plate minimum-detail projection retains requested sizes");
        check(fine.Microplates.All(p => p.Cells.Length > 0 && Connected(fine.Surface, p.Cells)), "500-plate minimum-detail projection keeps every plate connected");
    }

    private static bool Connected(Surface surface, int[] members)
    {
        if (members.Length == 0) return false;
        var membership = members.ToHashSet();
        var visited = new HashSet<int> { members[0] };
        var queue = new Queue<int>(); queue.Enqueue(members[0]);
        while (queue.Count > 0)
            foreach (int neighbor in surface.Cells[queue.Dequeue()].Neighbors)
                if (membership.Contains(neighbor) && visited.Add(neighbor)) queue.Enqueue(neighbor);
        return visited.Count == members.Length;
    }
}

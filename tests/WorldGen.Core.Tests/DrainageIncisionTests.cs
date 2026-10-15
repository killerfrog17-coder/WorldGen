using WorldGen.Core;

static class DrainageIncisionTests
{
    public static void Run(Action<bool, string> check)
    {
        var surface = new Surface { Cells = Enumerable.Range(0, 4).Select(i => new Cell
        {
            Id = i, Neighbors = Enumerable.Range(0, 4).Where(j => Math.Abs(i - j) == 1).ToArray(),
            DistanceM = Enumerable.Repeat(50000, i == 0 || i == 3 ? 1 : 2).ToArray()
        }).ToArray() };
        int[] original = { -10, 15, 35, 20 };
        int[] Cut(int cap, bool ice = false, int spacing = 50000)
        {
            foreach (var cell in surface.Cells) Array.Fill(cell.DistanceM, spacing);
            var h = (int[])original.Clone(); var glacial = new bool[4]; glacial[3] = ice;
            Erosion.BreachCoastalBarriers(surface, h, new bool[4], glacial, new int[4], cap, default);
            return h;
        }
        var cut = Cut(30);
        check(cut[2] < cut[3] && cut[1] < cut[2], "Shallow near-coastal sill is physically cut to a descending outlet");
        check(cut[0] == original[0] && cut[3] == original[3] && cut[2] >= 1 && original[2] - cut[2] <= 30, "Incision preserves pit/ocean and stays within local depth limit");
        check(Cut(10).SequenceEqual(original), "High barrier not breached beyond incision cap");
        check(Cut(30, true).SequenceEqual(original), "Glacial pit is not indiscriminately drained");
        check(Cut(30, false, 200000).SequenceEqual(original), "Wide barrier without short proof path is preserved");
        check(Cut(0).SequenceEqual(original), "Zero hydraulic incision changes no heights");
        var shared = new Surface { Cells = new[] {
            new Cell { Id=0, Neighbors=new[]{1}, DistanceM=new[]{50000} },
            new Cell { Id=1, Neighbors=new[]{0,2}, DistanceM=new[]{50000,50000} },
            new Cell { Id=2, Neighbors=new[]{1,3,4}, DistanceM=new[]{50000,50000,50000} },
            new Cell { Id=3, Neighbors=new[]{2}, DistanceM=new[]{50000} },
            new Cell { Id=4, Neighbors=new[]{2}, DistanceM=new[]{50000} }
        } };
        var sharedHeights = new[] { -10, 20, 50, 40, 30 };
        Erosion.BreachCoastalBarriers(shared, sharedHeights, new bool[5], new bool[5], new int[5], 15, default);
        check(sharedHeights[2] == 39, "Overlapping pit routes cannot cumulatively exceed the original-height incision cap");
    }
}

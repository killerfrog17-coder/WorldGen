using WorldGen.Core;

// Mechanism fixtures deliberately bypass random plate allocation: changing a
// boundary regime must change its geology, not merely its diagnostic label.
static class TectonicTests
{
    public static void Run(Action<bool, string> check)
    {
        var recipe = Recipe.Preset("Preview"); recipe.Detail = 5000; recipe.Seed = 421;
        var surface = SphericalMesh.Build(recipe, default);
        World Fixture(int[] crust, BoundaryKind? forced = null)
        {
            int count = crust.Length, n = surface.Cells.Length;
            var labels = surface.Cells.Select(c => count == 2 ? (c.Position.X < 0 ? 0 : 1) : c.Position.X < -.15 ? 0 : c.Position.X > .15 ? 2 : 1).ToArray();
            var world = new World
            {
                Recipe = recipe.Clone(), Surface = surface, CellMicroplate = labels,
                Continent = labels.Select(p => crust[p]).ToArray(), MantlePermille = new int[n],
                Microplates = new Microplate[count], Parents = new ParentPlate[count]
            };
            for (int p = 0; p < count; p++)
            {
                var members = Enumerable.Range(0, n).Where(i => labels[i] == p).ToArray();
                V motion = new V(0, p % 2 == 0 ? 1 : -1, 0);
                world.Microplates[p] = new Microplate
                {
                    Id = p, ParentAffinityId = p, ActiveActorId = p, SeedCell = members[0],
                    ContinentId = crust[p], Cells = members, AreaM2 = members.Sum(i => surface.Cells[i].AreaM2),
                    // Intentionally stale/reversed density: crust, not density,
                    // must decide mixed ocean/continent subduction polarity.
                    DensityKgM3 = crust[p] < 0 ? 2700 : 3400,
                    Motion = motion, OriginalMotion = motion
                };
                world.Parents[p] = new ParentPlate { Id = p, Microplates = new[] { p }, Motion = motion, AreaM2 = world.Microplates[p].AreaM2, ContinentalFraction = crust[p] < 0 ? 0 : 1 };
            }
            Tectonics.Build(world, default);
            if (forced.HasValue)
                foreach (var b in world.Boundaries)
                {
                    b.Kind = forced.Value;
                    b.NormalMotion = forced.Value == BoundaryKind.Transform ? 0 : forced.Value == BoundaryKind.Divergent ? -1800 : 1800;
                    b.TangentialMotion = forced.Value == BoundaryKind.Transform ? 1800 : 0;
                    if (forced.Value != BoundaryKind.Convergent) { b.SubductingMicroplate = -1; b.OverridingMicroplate = -1; }
                    else if (crust.All(c => c < 0))
                    { b.SubductingMicroplate = Math.Min(b.MicroA, b.MicroB); b.OverridingMicroplate = Math.Max(b.MicroA, b.MicroB); b.EndDistanceM = int.MaxValue; }
                }
            return world;
        }

        bool Arc(Feature f) => f.Kind == FeatureKind.IslandArc || f.Kind == FeatureKind.VolcanicArc;
        void Ownership(World world)
        {
            foreach (var f in world.Features.Where(f => f.OwnerParent >= 0))
            {
                bool plume=f.Kind==FeatureKind.Hotspot || f.Kind==FeatureKind.RidgeHotspot || f.Kind==FeatureKind.IslandChain || f.Kind==FeatureKind.LargeIgneousProvince;
                if(plume && world.Recipe.AlgorithmVersion==Recipe.CurrentAlgorithm)
                {check(f.Cells.All(i=>D.Angle(f.Center,world.Surface.Cells[i].Position)*world.Recipe.RadiusKm*1000<=f.WidthM*1.21),"Plume feet have local physical support without artificial plate cuts");continue;}
                check(f.Cells.All(i => world.Microplates[world.CellMicroplate[i]].ParentAffinityId == f.OwnerParent), "Feature influence stays on its owner parent: " + f.Kind);
            }
            foreach (var f in world.Features.Where(Arc))
            {
                var b = world.Boundaries[f.BoundaryId];
                check(b.Kind == BoundaryKind.Convergent && b.SubductingMicroplate >= 0, "Arc has eligible subduction provenance");
                check(f.OwnerParent == world.Microplates[b.OverridingMicroplate].ParentAffinityId, "Arc belongs to overriding parent");
                check(f.Cells.All(i => world.Microplates[world.CellMicroplate[i]].ParentAffinityId != world.Microplates[b.SubductingMicroplate].ParentAffinityId), "Arc cannot leak onto downgoing side");
            }
        }

        var mixed = Fixture(new[] { -1, 0 });
        var subduction = mixed.Boundaries.Where(b => b.Kind == BoundaryKind.Convergent).ToArray();
        check(subduction.Length > 0, "Mixed synthetic fixture exercises convergence");
        foreach (var b in subduction)
        {
            check(b.SubductingMicroplate == 0 && b.OverridingMicroplate == 1, "Ocean underthrusts continent despite stale density");
            check(b.BeltId >= 0 && b.AlongM >= 0 && b.EndDistanceM >= 0, "Ordered belt coordinate retained");
        }
        Terrain.Build(mixed, default); Ownership(mixed);
        check(mixed.Features.Any(f => f.Kind == FeatureKind.Cordillera), "Ocean-continent convergence produces a cordillera");
        check(!mixed.Features.Any(f => f.Kind == FeatureKind.CollisionBelt), "Subduction is not mislabeled continental collision");

        // Identical eligible kinematics need not mean identical accumulated
        // shortening: most margins should be modest, while a few can remain
        // Andes-class. Sample owned history streams, not world-height quotas.
        var cordilleraPeaks = new List<int>();
        for (int historySeed = 0; historySeed < 24; historySeed++)
        {
            var margin = Fixture(new[] { -1, 0 }, BoundaryKind.Convergent);
            margin.Recipe.Seed = historySeed;
            // This mechanism threshold predates the user-requested sixfold UI baseline.
            // Compare coupling histories at its original forcing, not at old 600%.
            margin.Recipe.MountainStrengthPercent=100.0/6;
            foreach (var b in margin.Boundaries)
            { b.SubductingMicroplate = 0; b.OverridingMicroplate = 1; }
            Terrain.Build(margin, default);
            cordilleraPeaks.Add(margin.Features.Where(f => f.Kind == FeatureKind.Cordillera).Select(f => f.AmplitudeM).DefaultIfEmpty().Max());
        }
        check(cordilleraPeaks.Count(p => p < 1800) > cordilleraPeaks.Count(p => p >= 1800), "Most convergent continental margins avoid massive cordilleras");
        check(cordilleraPeaks.Max() > 2500 && cordilleraPeaks.Min() < 800, "Margin history retains both strong and subdued mountain classes");
        var weakMargin = Fixture(new[] { -1, 0 });
        foreach (var b in weakMargin.Boundaries) b.NormalMotion = Math.Min(80, b.NormalMotion);
        Terrain.Build(weakMargin, default);
        check(!weakMargin.Features.Any(f => f.Kind == FeatureKind.Cordillera), "Marginal convergence does not manufacture a continental cordillera");

        var ocean = Fixture(new[] { -1, -1, -1 }); Terrain.Build(ocean, default); Ownership(ocean);
        check(ocean.Features.Any(f => f.Kind == FeatureKind.IslandArc && f.Cells.Length > 0), "Ocean convergence supports island arcs");
        foreach (var pair in ocean.Boundaries.Where(b => b.Kind == BoundaryKind.Convergent).GroupBy(b => b.BeltId))
            check(pair.Select(b => ocean.Microplates[b.SubductingMicroplate].ParentAffinityId).Distinct().Count() == 1, "Oceanic polarity is coherent along a belt");

        // Opposite member densities must not reverse polarity along one parent
        // margin. The parent-level area-weighted crust is the coherent slab.
        var fragmented = Fixture(new[] { -1, -1 });
        fragmented.Microplates = new Microplate[4];
        for (int i = 0; i < surface.Cells.Length; i++)
            if (surface.Cells[i].Position.Y > 0) fragmented.CellMicroplate[i] += 2;
        for (int p = 0; p < 4; p++)
        {
            int parent = p % 2;
            var cells = Enumerable.Range(0, surface.Cells.Length).Where(i => fragmented.CellMicroplate[i] == p).ToArray();
            fragmented.Microplates[p] = new Microplate
            {
                Id = p, ParentAffinityId = parent, ContinentId = -1, Cells = cells,
                AreaM2 = cells.Sum(i => surface.Cells[i].AreaM2), DensityKgM3 = p == 0 || p == 3 ? 3400 : 2700,
                Motion = fragmented.Parents[parent].Motion, OriginalMotion = fragmented.Parents[parent].Motion
            };
        }
        fragmented.Parents[0].Microplates = new[] { 0, 2 }; fragmented.Parents[1].Microplates = new[] { 1, 3 };
        Tectonics.Build(fragmented, default);
        check(fragmented.Boundaries.Where(b => b.Kind == BoundaryKind.Convergent).Select(b => fragmented.Microplates[b.SubductingMicroplate].ParentAffinityId).Distinct().Count() == 1, "Contrasting microplate densities do not flip a parent margin's slab polarity");

        foreach (var regime in new[] { BoundaryKind.Transform, BoundaryKind.Divergent })
        {
            var controlled = Fixture(new[] { -1, -1 }, regime); Terrain.Build(controlled, default); Ownership(controlled);
            check(!controlled.Features.Any(Arc), "Pure " + regime + " boundaries do not produce volcanic arcs");
            check(!controlled.Features.Any(f => f.Kind == FeatureKind.CollisionBelt || f.Kind == FeatureKind.Plateau), "Pure " + regime + " does not produce collision relief");
        }

        var collision = Fixture(new[] { 0, 1 }); Terrain.Build(collision, default); Ownership(collision);
        var quiet = Fixture(new[] { 0, 1 }, BoundaryKind.Transform); Terrain.Build(quiet, default);
        var beltCells = collision.Features.Where(f => f.Kind == FeatureKind.CollisionBelt).SelectMany(f => f.Cells).Distinct().ToArray();
        check(beltCells.Length > 0, "Continental collision has spatial support");
        check(collision.Features.Any(f => f.Kind == FeatureKind.Plateau), "Continental collision has a plateau distinct from its mountain front");
        check(beltCells.Average(i => collision.TectonicM[i] - quiet.TectonicM[i]) > 0, "Collision metadata corresponds to actual uplift over transform control");
        check(!collision.Features.Any(Arc), "Continental collision is not an oceanic volcanic arc");

        var plateauCells = collision.Features.Where(f => f.Kind == FeatureKind.Plateau).SelectMany(f => f.Cells).Distinct().ToArray();
        var forelandCells = collision.Features.Where(f => f.Kind == FeatureKind.ForelandBasin).SelectMany(f => f.Cells).Distinct().ToArray();
        check(forelandCells.Length > 0, "Collision has an actual foreland footprint");
        int plateauOwner = collision.Features.First(f => f.Kind == FeatureKind.Plateau).OwnerParent;
        var frontOnPlateauSide = beltCells.Where(i => collision.Microplates[collision.CellMicroplate[i]].ParentAffinityId == plateauOwner).ToArray();
        long Area(IEnumerable<int> cells) => cells.Sum(i => surface.Cells[i].AreaM2);
        check(Area(plateauCells) > Area(frontOnPlateauSide), "Collision plateau extends over more area than its narrow mountain front");
        check(plateauCells.Average(i => collision.TectonicM[i] - quiet.TectonicM[i]) > forelandCells.Average(i => collision.TectonicM[i] - quiet.TectonicM[i]), "Actual plateau uplift exceeds foreland uplift, not just feature labels");
        check(frontOnPlateauSide.Average(i => collision.TectonicM[i]) > forelandCells.Average(i => collision.TectonicM[i]), "Mountain-front elevations stand above opposing foreland");

        bool Magmatic(Feature f) => f.Kind == FeatureKind.Hotspot || f.Kind == FeatureKind.RidgeHotspot || f.Kind == FeatureKind.IslandChain || f.Kind == FeatureKind.LargeIgneousProvince;
        var ridgeWorld = Fixture(new[] { -1, -1 }, BoundaryKind.Divergent); Terrain.Build(ridgeWorld, default);
        var magmaticCells = ridgeWorld.Features.Where(Magmatic).SelectMany(f => f.Cells).ToHashSet();
        var ordinaryRidge = ridgeWorld.Features.Where(f => f.Kind == FeatureKind.MidOceanRidge).SelectMany(f => f.Cells).Distinct().Where(i => !magmaticCells.Contains(i)).ToArray();
        check(ordinaryRidge.Length > 0, "Controlled divergence exercises ordinary ocean ridge cells");
        check(ordinaryRidge.All(i => ridgeWorld.TectonicM[i] < 0), "Ordinary deep-ocean ridge remains submerged without hotspot overlap");
        Erosion.Apply(ridgeWorld, default);
        check(ordinaryRidge.All(i => ridgeWorld.ElevationM[i] < 0), "Ordinary ridge remains submerged after sculpting");

        // Exclude independent hotspots: an arc must itself be capable of
        // emerging from deep ocean in an eligible strong-convergence case.
        bool emergentArc = false, survivingArc = false;
        foreach (int seed in new[] { 421, 42, 73421, 17, 99 })
        {
            var strong = Fixture(new[] { -1, -1 }, BoundaryKind.Convergent); strong.Recipe.Seed = seed;
            Terrain.Build(strong, default);
            var independent = strong.Features.Where(Magmatic).SelectMany(f => f.Cells).ToHashSet();
            emergentArc |= strong.Features.Where(f => f.Kind == FeatureKind.IslandArc).SelectMany(f => f.Cells).Any(i => !independent.Contains(i) && strong.TectonicM[i] > 0);
            Erosion.Apply(strong, default);
            survivingArc |= strong.Features.Where(f => f.Kind == FeatureKind.IslandArc).SelectMany(f => f.Cells).Any(i => !independent.Contains(i) && strong.ElevationM[i] > 0);
        }
        check(emergentArc, "Strong eligible subduction can create actual deep-ocean arc islands without hotspot assistance");
        check(survivingArc, "Eligible deep-ocean arc islands survive default sculpting without hotspot assistance");
        Erosion.Apply(collision, default);
        check(frontOnPlateauSide.Average(i => collision.ElevationM[i]) > forelandCells.Average(i => collision.ElevationM[i]), "Mountain-front versus foreland relief survives default sculpting");

        // Trail age and geometry are authoritative provenance, not merely names.
        foreach (var hot in ocean.Features.Where(f => f.Kind == FeatureKind.Hotspot || f.Kind == FeatureKind.RidgeHotspot))
        {
            var trail = ocean.Features.Skip(hot.Id + 1).TakeWhile(f => f.Kind == FeatureKind.IslandChain).ToArray();
            int lastAge = hot.AgeMyr; double lastDistance = 0;V lastCenter=hot.Center;
            foreach (var old in trail)
            {
                double distance = D.Angle(hot.Center, old.Center);
                if(ocean.Recipe.AlgorithmVersion==Recipe.CurrentAlgorithm)
                {
                    double step=D.Angle(lastCenter,old.Center)*ocean.Recipe.RadiusKm*1000;
                    check(old.AgeMyr>lastAge && step>=89000 && step<=221000,"Curved trail advances through age at bounded physical spacing");
                    check(old.AmplitudeM>=0 && old.AmplitudeM<=8500,"Abyssal construction remains bounded independently of summit age");
                }
                else
                {
                    check(old.AgeMyr > lastAge && distance > lastDistance, "Hotspot trail progresses in age and distance");
                    check(V.Dot(old.Center - hot.Center, hot.Axis) > 0, "Older hotspot edifices lie in the plate-motion direction");
                    check(old.AmplitudeM <= hot.AmplitudeM, "Older hotspot edifices are not taller than the active source");
                }
                lastCenter=old.Center;
                lastAge = old.AgeMyr; lastDistance = distance;
            }
        }
        var repeated = Fixture(new[] { -1, -1, -1 }); Terrain.Build(repeated, default);
        check(StateHash.Compute(new object[] { ocean.Boundaries, ocean.Features, ocean.TectonicM }) == StateHash.Compute(new object[] { repeated.Boundaries, repeated.Features, repeated.TectonicM }), "Synthetic tectonic fields and records are deterministic");
    }
}

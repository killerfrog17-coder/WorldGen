using System;
using System.Collections.Generic;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Small UI state: the browser never deserializes the authoritative mesh.
    public sealed class WorldSummary
    {
        public Recipe Recipe = new Recipe(); public int CellCount;
        public List<StageRecord> Stages = new List<StageRecord>(); public List<Metric> Metrics = new List<Metric>(); public List<string> Warnings = new List<string>();
        public static WorldSummary From(World world) => new WorldSummary { Recipe = world.Recipe, CellCount = world.Surface.Cells.Length, Stages = world.Stages, Metrics = world.Metrics, Warnings = world.Warnings };
    }
}

using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    public static class WorldJson
    {
        public static string Write<T>(T value)
        { using (var s = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(s, value); return Encoding.UTF8.GetString(s.ToArray()); } }
        public static T Read<T>(string json)
        { if (json == null || json.Length > 250000000) throw new ArgumentException("Invalid document size."); using (var s = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(s); }
        public static Recipe ReadRecipe(string json)
        {
            var expected = new System.Collections.Generic.HashSet<string>(System.Linq.Enumerable.Select(typeof(Recipe).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public), f => f.Name));
            var found = new System.Collections.Generic.HashSet<string>();
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), System.Xml.XmlDictionaryReaderQuotas.Max))
                while (reader.Read()) if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Depth == 1) { if (!expected.Contains(reader.LocalName) || !found.Add(reader.LocalName)) throw new ArgumentException("Unknown or duplicated recipe setting: " + reader.LocalName); }
            if (!found.Contains(nameof(Recipe.ClimateWeights))) expected.Remove(nameof(Recipe.ClimateWeights));
            if(!found.Contains(nameof(Recipe.LandShape)))expected.Remove(nameof(Recipe.LandShape));
            if(!found.Contains(nameof(Recipe.EcologyProfile)))expected.Remove(nameof(Recipe.EcologyProfile));
            if(!found.Contains(nameof(Recipe.InteriorRainMm)))expected.Remove(nameof(Recipe.InteriorRainMm));
            if (!found.Contains(nameof(Recipe.MountainStrengthPercent))) expected.Remove(nameof(Recipe.MountainStrengthPercent));
            if (!expected.SetEquals(found)) throw new ArgumentException("A recipe must contain every versioned setting. Save a complete recipe from either interface.");
            var r = Read<Recipe>(json); if (r == null) throw new ArgumentException("Missing recipe.");
            if ((((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.SubcellAlgorithm) || r.AlgorithmVersion == Recipe.FeatureBlendAlgorithm || r.AlgorithmVersion == Recipe.RangeControlsAlgorithm || r.AlgorithmVersion == Recipe.EcologyAlgorithm) || r.AlgorithmVersion == Recipe.TerrainControlsAlgorithm) && !found.Contains(nameof(Recipe.MountainStrengthPercent))) throw new ArgumentException("Current recipes must include mountain strength.");
            r.Validate(); return r;
        }
        public static World ReadWorld(string json)
        { return ValidateWorld(Read<World>(json)); }
        public static World ValidateWorld(World w, System.Threading.CancellationToken cancellation = default)
            => ValidateWorld(w,cancellation,null);
        public static World ValidateWorld(World w, System.Threading.CancellationToken cancellation, Action<string> status)
        {
            if (w == null || w.SchemaVersion != 3 || w.Recipe == null) throw new ArgumentException("Unsupported result version.");
            //3.23 snapshots have the same field contract. Validate their settings
            // without rewriting the saved recipe or its authoritative state hash.
            var validationRecipe = w.Recipe.Clone();
            if (validationRecipe.AlgorithmVersion == "worldgen-3.23.0") validationRecipe.AlgorithmVersion = ClimateCalibration.UntrainedAlgorithm;
            validationRecipe.Validate();
            if((w.Recipe.AlgorithmVersion == Recipe.CurrentAlgorithm || w.Recipe.AlgorithmVersion == Recipe.SubcellAlgorithm))SubcellGeography.Validate(w);
            if(((w.Recipe.AlgorithmVersion == Recipe.CurrentAlgorithm || w.Recipe.AlgorithmVersion == Recipe.SubcellAlgorithm) || w.Recipe.AlgorithmVersion==Recipe.FeatureBlendAlgorithm || w.Recipe.AlgorithmVersion==Recipe.RangeControlsAlgorithm || w.Recipe.AlgorithmVersion==Recipe.EcologyAlgorithm))
            {
                var e=w.Ecology; int n=w.Recipe.Detail;
                if(e==null || e.Habitat==null || e.Habitat.Length!=n || e.Region==null || e.Region.Length!=n || e.Province==null || e.Province.Length!=n || e.Taxa==null || e.Regions==null || e.SpeciesBits==null || e.SpeciesBits.LongLength!=(long)n*((e.Taxa.Length+31L)/32))
                    throw new ArgumentException("Current world snapshot is missing its complete ecological state.");
            }
            status?.Invoke("Checking generated terrain integrity");
            if (w.Surface == null || w.Surface.Cells == null || w.Surface.Cells.Length != w.Recipe.Detail || w.Stages == null || w.Stages.Count == 0 || StateHash.Compute(w, cancellation) != w.Stages[w.Stages.Count - 1].Hash) throw new ArgumentException("Result integrity hash mismatch.");
            if(w.Prehistory!=null)
            {
                status?.Invoke("Preparing historical geography");
                var engine=new Prehistory(w,cancellation);
                status?.Invoke("Checking historical fingerprint and population accounts");
                engine.Validate(w.Prehistory);
            }
            return w;
        }
        public static string Csv(World w)
        {
            var b = new StringBuilder("cell,x,y,z,area_m2,microplate,parent,continent,elevation_m,spring_temp_c,summer_temp_c,autumn_temp_c,winter_temp_c,spring_precip_mm,summer_precip_mm,autumn_precip_mm,winter_precip_mm,lake_fraction,lake_area_m2,lake_volume_m3,ecoregion,biome,pet_mm,aridity_p_pet,flood_fraction,flood_days_per_year,water_table_depth_m,permafrost_likelihood,salinity_ppt,soil_proxy,fire_potential\n");
            var seasons = new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter };
            foreach (var c in w.Surface.Cells)
            {
                int i = c.Id; var culture = System.Globalization.CultureInfo.InvariantCulture;
                double fraction = i < w.Hydrology.LakeCoverageFraction.Length ? w.Hydrology.LakeCoverageFraction[i] : 0;
                double volume = i < w.Hydrology.LakeWaterVolumeM3.Length ? w.Hydrology.LakeWaterVolumeM3[i] : 0;
                b.Append(i).Append(',').Append(c.Position.X.ToString("R", culture)).Append(',').Append(c.Position.Y.ToString("R", culture)).Append(',').Append(c.Position.Z.ToString("R", culture)).Append(',').Append(c.AreaM2).Append(',').Append(w.CellMicroplate[i]).Append(',').Append(w.Microplates[w.CellMicroplate[i]].ParentAffinityId).Append(',').Append(w.Continent[i]).Append(',').Append(w.ElevationM[i]);
                foreach (var season in seasons) b.Append(',').Append((season.TemperatureTenthsC[i] / 10.0).ToString("R", culture));
                foreach (var season in seasons) b.Append(',').Append(season.PrecipitationMm[i]);
                b.Append(',').Append(fraction.ToString("R", culture)).Append(',').Append((fraction * c.AreaM2).ToString("R", culture)).Append(',').Append(volume.ToString("R", culture));
                if(w.Ecology!=null)
                {
                    var a=w.Ecology.Habitat[i];
                    b.Append(',').Append(w.Ecology.Region[i]).Append(',').Append(a.Biome).Append(',').Append(a.PetMm).Append(',').Append((a.AridityMilli/1000.0).ToString("R",culture)).Append(',').Append((a.FloodFractionPermille/1000.0).ToString("R",culture)).Append(',').Append(a.FloodDaysPerYear).Append(',').Append((a.WaterTableCm/100.0).ToString("R",culture)).Append(',').Append((a.PermafrostPermille/1000.0).ToString("R",culture)).Append(',').Append((a.SalinityMilliPpt/1000.0).ToString("R",culture)).Append(',').Append(a.Soil).Append(',').Append((a.FirePotentialPermille/1000.0).ToString("R",culture));
                }
                else for(int field=0;field<11;field++) b.Append(',');
                b.Append('\n');
            }
            return b.ToString();
        }
        // 16-bit big-endian PGM, equirectangular nearest-cell sampling; linear -5000..6000 m.
        public static byte[] Heightmap(World w, int width = 1024, int height = 512)
        {
            using (var s = new MemoryStream())
            {
                var header = Encoding.ASCII.GetBytes("P5\n# linear metres: -5000 + sample * 11000 / 65535\n" + width + " " + height + "\n65535\n"); s.Write(header, 0, header.Length); int near = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                { double lat = Math.PI * (.5 - (y + .5) / height), lon = 2 * Math.PI * ((x + .5) / width - .5); var p = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat)); bool moved = true; while (moved) { moved = false; double best = V.Dot(p, w.Surface.Cells[near].Position); foreach (int n in w.Surface.Cells[near].Neighbors) { double score = V.Dot(p, w.Surface.Cells[n].Position); if (score > best) { best = score; near = n; moved = true; } } } int value = D.M(D.Clamp((w.ElevationM[near] + 5000) / 11000.0) * 65535); s.WriteByte((byte)(value >> 8)); s.WriteByte((byte)value); }
                return s.ToArray();
            }
        }
    }
}

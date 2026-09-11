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
            if (!expected.SetEquals(found)) throw new ArgumentException("A recipe must contain every versioned setting. Save a complete recipe from either interface.");
            var r = Read<Recipe>(json); if (r == null) throw new ArgumentException("Missing recipe."); r.Validate(); return r;
        }
        public static World ReadWorld(string json)
        { var w = Read<World>(json); if (w == null || w.SchemaVersion != 1) throw new ArgumentException("Unsupported result version."); w.Recipe.Validate(); if (w.Stages.Count == 0 || StateHash.Compute(w) != w.Stages[w.Stages.Count - 1].Hash) throw new ArgumentException("Result integrity hash mismatch."); return w; }
        public static string Csv(World w)
        {
            var b = new StringBuilder("cell,x,y,z,area_m2,microplate,parent,continent,elevation_m,receiver,watershed,water_body,contributing_area_m2\n");
            foreach (var c in w.Surface.Cells) { int i = c.Id; b.Append(i).Append(',').Append(c.Position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(c.Position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(c.Position.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(c.AreaM2).Append(',').Append(w.CellMicroplate[i]).Append(',').Append(w.Microplates[w.CellMicroplate[i]].ParentAffinityId).Append(',').Append(w.Continent[i]).Append(',').Append(w.ElevationM[i]).Append(',').Append(w.Hydrology.Receiver[i]).Append(',').Append(w.Hydrology.Watershed[i]).Append(',').Append(w.Hydrology.WaterBody[i]).Append(',').Append(w.Hydrology.ContributingAreaM2[i]).Append('\n'); }
            return b.ToString();
        }
        // 16-bit big-endian PGM, equirectangular nearest-cell sampling; linear -12000..16000 m.
        public static byte[] Heightmap(World w, int width = 1024, int height = 512)
        {
            using (var s = new MemoryStream())
            {
                var header = Encoding.ASCII.GetBytes("P5\n# linear metres: -12000 + sample * 28000 / 65535\n" + width + " " + height + "\n65535\n"); s.Write(header, 0, header.Length); int near = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                { double lat = Math.PI * (.5 - (y + .5) / height), lon = 2 * Math.PI * ((x + .5) / width - .5); var p = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat)); bool moved = true; while (moved) { moved = false; double best = V.Dot(p, w.Surface.Cells[near].Position); foreach (int n in w.Surface.Cells[near].Neighbors) { double score = V.Dot(p, w.Surface.Cells[n].Position); if (score > best) { best = score; near = n; moved = true; } } } int value = D.M(D.Clamp((w.ElevationM[near] + 12000) / 28000.0) * 65535); s.WriteByte((byte)(value >> 8)); s.WriteByte((byte)value); }
                return s.ToArray();
            }
        }
    }
}

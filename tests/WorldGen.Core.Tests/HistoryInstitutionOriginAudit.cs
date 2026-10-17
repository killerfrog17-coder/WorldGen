using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline saved-state evidence only: no replay, rule upgrade, or simulation writes.
public static class HistoryInstitutionOriginAudit
{
    public static void Run(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new institutional-origin audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var s=world.Prehistory;
        string before=s.ComputeHash();var h=s.History;var a=h.Aggregates;
        var origins=h.Archive.Where(e=>e.Kind=="independent-institutions").OrderBy(e=>e.Year).ThenBy(e=>e.Id).ToArray();
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
            Landmass=world.Landmass[cell],Climate=world.Climate.Classification[cell].ToString()};
        int[] Chain(int culture)
        {
            var ids=new List<int>();var seen=new HashSet<int>();
            while(culture>=0)
            {
                if(culture>=h.Cultures.Length||!seen.Add(culture))throw new Exception("Invalid or cyclic culture ancestry.");
                ids.Add(culture);culture=h.Cultures[culture].ParentCultureId;
            }
            return ids.ToArray();
        }
        var rows=origins.Select(e=>
        {
            int culture=e.CultureId;var chain=Chain(culture);var family=new HashSet<int>(chain);
            var prior=h.Archive.Where(x=>x.Year<=e.Year&&family.Contains(x.CultureId)).OrderBy(x=>x.Year).ThenBy(x=>x.Id).ToArray();
            var town=e.SettlementId>=0?s.Society.Settlements[e.SettlementId]:null;
            var townMemory=e.SettlementId>=0?h.Towns[e.SettlementId]:null;
            return new{e.Id,e.Year,e.CultureId,e.PolityId,e.SettlementId,e.Text,FoundingPlace=Place(e.Cell),
                FounderTown=town==null?null:new{town.Id,town.Name,town.FoundedYear,CurrentCulture=town.CultureId,CurrentPolity=town.PolityId},
                CurrentTownMemory=townMemory==null?null:new{townMemory.CivicTeacherPolityId,townMemory.CivicExampleExposure,townMemory.CivicPreparationYears},
                Ancestry=chain.Select(id=>
                {
                    var c=s.Society.Cultures[id];var hc=h.Cultures[id];var continuity=a.Empires.Connections.Cultures[id];var crop=h.Agriculture[id];
                    var birth=h.Archive.Where(x=>x.CultureId==id&&x.Kind=="migrant-community").OrderBy(x=>x.Year).FirstOrDefault();
                    return new{c.Id,c.Name,c.FoundedYear,hc.ParentCultureId,Origin=Place(c.OriginCell),
                        a.Cultures[id].CivilizationSince,continuity.CivilizationRoot,continuity.InstitutionalOriginCulture,continuity.ColonialSponsor,
                        CurrentCivicOrigin=a.Living.CivicOrigins[id],crop.NativeMask,crop.SourceCulture,crop.AdoptedYear,
                        MigrantBirth=birth==null?null:new{birth.Year,birth.Text,Place=Place(birth.Cell)}};
                }).ToArray(),
                EarlierInstitutionEvents=prior.Where(x=>x.Year<e.Year&&(x.Kind=="independent-institutions"||x.Kind=="institutional-diffusion"||x.Kind=="institutional-restoration"))
                    .Select(x=>new{x.Id,x.Year,x.Kind,x.CultureId,x.PolityId,x.SettlementId,x.Text}).ToArray(),
                PriorEventKinds=prior.GroupBy(x=>x.Kind).OrderBy(g=>g.Key).Select(g=>new{Kind=g.Key,Count=g.Count(),FirstYear=g.Min(x=>x.Year)}).ToArray(),
                RecentPriorEvents=prior.Where(x=>x.Year<e.Year&&x.Kind!="birth"&&x.Kind!="death").TakeLast(35)
                    .Select(x=>new{x.Id,x.Year,x.Kind,x.CultureId,x.PolityId,x.OtherPolityId,x.Text}).ToArray()};
        }).ToArray();
        if(s.ComputeHash()!=before)throw new Exception("Read-only institutional-origin audit changed its checkpoint.");
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,
            Realm=a.Empires.Connections.Realms.Version,CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
            Origins=rows,Notes=new[]{
                "independent-institutions is the archived founding event label: it means no active teacher in that foundation branch, not proof of historically pristine invention.",
                "Founding place and event culture are historical archive fields. Present town affiliation, teacher memory, cultural continuity and crop inventories are final-snapshot evidence, not reconstructed founding conditions.",
                "ParentCultureId ancestry is inspected explicitly. CivilizationRoot also follows language-family roots and is not by itself a record of an institutional transmission.",
                "A prior institutional event in the founder's actual ancestor is positive evidence against pristine origin. Missing archived contact is not proof of isolation; the archive is not an exhaustive transmission ledger.",
                "Pure saved-state inspection; no advance, upgrade, generated world, authoritative mutation or save write."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Institution origins {s.Years}: {rows.Length} archived origins; preserved {before}");
    }
}

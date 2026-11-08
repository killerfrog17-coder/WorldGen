using System.Runtime.Serialization;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

static class WorldEntryTests
{
    public static void Run(string input,string output)
    {
        var world=WorldFile.ReadFile(input);var play=new PlaySession();int checks=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        var entries=new List<object>();
        for(int seed=1;seed<=12;seed++)
        {
            var entry=PlayerWorldEntry.RandomResident(world,play,seed);var repeated=PlayerWorldEntry.RandomResident(world,play,seed);
            var id=entry.Identity;id.Validate();
            Check(id.Person.Id==repeated.Identity.Person.Id&&id.Group==repeated.Identity.Group,"Selection must be stable for a seed");
            Check(HumanLocalLife.SpatialAdmission(world.Recipe.Seed,id.Group,play.Settings.HorizontalScale),"Chosen community must exist at the played scale");
            Check(id.Person.AgeYears>=18&&id.Family.Any(f=>f.Id==id.Person.Id),"Adult identity must be present in the family's actual record");
            play.Identity=id;play.PlayerHumanVariant=PlayerWorldEntry.Variant(id.Person);play.Inventory=new PlayerInventory();
            play.Inventory.Items=HumanEquipment.Held(entry);play.Validate();checks++;
            var copy=play.Copy();copy.Identity.Person.Appearance[0]+=.01;
            Check(copy.Identity.Person.Appearance[0]!=play.Identity.Person.Appearance[0],"Copied identity must not alias original appearance");
            using var stream=new MemoryStream();var serializer=new DataContractSerializer(typeof(PlaySession));serializer.WriteObject(stream,play);stream.Position=0;
            var restored=(PlaySession)serializer.ReadObject(stream)!;restored.Validate();
            Check(restored.Identity.Person.Id==id.Person.Id&&restored.Identity.Group==id.Group,"Binary-contract roundtrip must retain identity");
            Check(restored.Identity.Person.Mother==id.Person.Mother&&restored.Identity.Person.Partner==id.Person.Partner&&restored.Identity.Person.Languages.Length==id.Person.Languages.Length,"Family/language links must survive reload");
            Check(restored.Inventory.Items.Select(p=>p.Id).SequenceEqual(play.Inventory.Items.Select(p=>p.Id)),"Actual held-item IDs must survive reload");
            play.Humans.People.Add(new HumanPersonMemory{Person=id.Person.Id,Group=id.Group,Health=0,Dead=true});
            var other=PlayerWorldEntry.RandomResident(world,play,seed);
            Check(other.Identity.Person.Id!=id.Person.Id,"A dead resident cannot be selected");
            play.Humans.People.Clear();
            entries.Add(new{seed,id.Group,id.Person.Id,id.Person.Name,id.Person.AgeYears,id.Person.Mother,id.Person.Father,id.Person.Partner,id.BandName,id.CultureName,id.LanguageName,Variant=play.PlayerHumanVariant,Items=play.Inventory.Items.Select(p=>p.Kind.ToString()).ToArray()});
        }
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"core-entry.json"),JsonSerializer.Serialize(new{checks,entries},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"World entry: {checks} identity, equipment, deterministic selection and save checks passed");
    }
}

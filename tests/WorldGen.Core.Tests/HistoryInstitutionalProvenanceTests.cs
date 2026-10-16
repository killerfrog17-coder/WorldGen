using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryInstitutionalProvenanceTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var original=world.Prehistory;string hash=original.ComputeHash();var s=engine.UpgradeToLivingWorldHistory(original).Copy();
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        var a=s.History.Aggregates;
        Settlement target=null!;int localCulture=-1,source=-1;
        foreach(var candidate in s.Society.Settlements.Where(t=>t.Population>=500&&a.Living.Cities[t.Id].AbandonedYear<0&&(bool)Call("DiffusionShore",t.Cell)))
        {
            var carrier=s.History.Cultures[candidate.CultureId];
            for(int k=0;k<HistoricalTechnicalGraph.LegacyCount;k++){carrier.Technology[k]=1;carrier.Technical.Levels[k]=8;carrier.Technical.Deployment[k]=1000;carrier.Technical.LevelYear[k]=0;}
            foreach(int cell in Enumerable.Range(0,world.Surface.Cells.Length))
            {
                double km=D.Angle(world.Surface.Cells[cell].Position,world.Surface.Cells[candidate.Cell].Position)*world.Recipe.RadiusKm;
                if(engine.CellPatch[cell]<0||km<350||km>3000||!(bool)Call("DiffusionShore",cell)||!(bool)Call("DiffusionSeaLane",cell,candidate.Cell))continue;
                if((bool)Call("InstitutionalRouteAllowed",s,cell,candidate.Cell,candidate.CultureId)){source=cell;target=candidate;localCulture=candidate.CultureId;break;}
            }
            if(source>=0)break;
        }
        check(source>=0,"Controlled relay fixture has a genuine reachable350-3000km coastal route, not an assumed continent shortcut");
        int teacher=Array.FindIndex(s.History.Polities,p=>p.CultureId!=localCulture);
        if(teacher<0)throw new InvalidOperationException("Provenance fixture needs two distinct cultural traditions.");
        var local=s.History.Cultures[localCulture];var distant=s.History.Cultures[s.History.Polities[teacher].CultureId];
        foreach(var k in new[]{HistoricalTechnology.Sailing,HistoricalTechnology.Navigation,HistoricalTechnology.OceanNavigation})
        {
            int id=(int)k;local.Technology[id]=1;local.Technical.Levels[id]=8;local.Technical.Deployment[id]=1000;local.Technical.LevelYear[id]=0;
            distant.Technology[id]=0;distant.Technical.Levels[id]=0;distant.Technical.Deployment[id]=0;
        }
        // Controlled source occupancy: no simulated historical claim is made.
        a.Cells=a.Cells.With(source*5,localCulture).With(source*5+1,-1).With(target.Cell*5,localCulture);
        var memory=a.Provinces.Exchanges[localCulture];memory.Peers=Array.Empty<int>();memory.LastYear=memory.LastDirectYear=Array.Empty<int>();memory.Capabilities=Array.Empty<long>();
        double delivered=(double)Call("InstitutionalLocalExample",s,teacher,source,target.Cell);
        check(delivered>0,"A locally practiced coastal tradition can teach its own people without a personal direct meeting with the distant founding court");
        check(s.History.Polities[teacher].CultureId!=localCulture&&original.ComputeHash()==hash,"Origin provenance remains distinct from the local carrier and the loaded checkpoint is unchanged");
    }
}

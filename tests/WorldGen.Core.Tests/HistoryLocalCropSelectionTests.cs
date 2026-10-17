using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryLocalCropSelectionTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args);
    public static void Run(Action<bool,string> check)
    {
        bool legacy=true;
        foreach(int id in Enumerable.Range(0,HistoricalCropCatalog.Crops.Length))foreach(int selected in new[]{0,3995,6500,19990})foreach(int years in new[]{1,17,101})
        {
            var c=new HistoricalCultivar{CropId=id,SelectedYears=selected,DaylengthLatitudeTenths=250};var expected=c.Copy();var definition=HistoricalCropCatalog.Crops[id];
            expected.SelectedYears=Math.Min(20000,expected.SelectedYears+years);double selection=D.Clamp(expected.SelectedYears/6000.0);
            expected.MaturityDays=(int)Math.Round(definition.MaturityDays*(1-.3*selection));expected.WaterRequirementPermille=(int)Math.Round(definition.MinimumWaterPermille*(1-.15*selection));expected.MinimumWarmTenthsC=definition.MinimumWarmC*10-(int)Math.Round(40*selection);expected.YieldPermille=650+(int)Math.Round(650*D.Clamp(expected.SelectedYears/8000.0));
            int move=(int)Math.Round(Math.Min(Math.Abs(470-expected.DaylengthLatitudeTenths),years*.02));expected.DaylengthLatitudeTenths+=Math.Sign(470-expected.DaylengthLatitudeTenths)*move;
            HistoricalCropSelectionEffects.Apply(c,years,470);legacy&=StateHash.Compute(c)==StateHash.Compute(expected);
        }
        check(legacy,"Shared legacy trait arithmetic exactly matches216 literal preceding species/year cases, not regenerated expected hashes.");
        var inherited=new HistoricalCultivar{CropId=0,OriginCulture=28,ParentCulture=49,SelectedYears=4000,MaturityDays=105,MinimumWarmTenthsC=100,DaylengthLatitudeTenths=316,WaterRequirementPermille=350,YieldPermille=1450};
        HistoricalCropSelectionEffects.Apply(inherited,100,494,true,2);
        check(inherited.SelectedYears==4100&&inherited.DaylengthLatitudeTenths==318,"Positive actual selection can continue beyond4000 and shift toward a local field without instantly matching its latitude.");
        check(inherited.MaturityDays<=105&&inherited.MinimumWarmTenthsC<=100&&inherited.YieldPermille>=1450,"Selecting an inherited aggregate seed lot never undoes stronger already recorded phenotypes.");
        check(inherited.OriginCulture==28&&inherited.ParentCulture==49,"Local selection retains the actual seed's recorded origin and immediate donor.");
        var bounded=new HistoricalCultivar{CropId=0,DaylengthLatitudeTenths=100};HistoricalCropSelectionEffects.Apply(bounded,20000,500);
        check(bounded.SelectedYears==20000&&bounded.MaturityDays==98&&bounded.WaterRequirementPermille==298&&bounded.MinimumWarmTenthsC==100&&bounded.YieldPermille==1300,"Long selection retains finite maturity, warmth and water limits rather than making crops grow in any environment.");
        HistoricalCropSelectionEffects.Apply(bounded,100,550,true,2);check(bounded.SelectedYears==20000&&bounded.DaylengthLatitudeTenths==502,"The general selection cap does not prevent a mature seed lineage from gradually retuning to another actual field.");
        string unchanged=StateHash.Compute(bounded);HistoricalCropSelectionEffects.Apply(bounded,0,900,true,20);check(StateHash.Compute(bounded)==unchanged,"No effective cultivation years produce no trait change.");
        check(HistoricalCropSelectionEffects.FractionalYears(.08,.01)==1&&HistoricalCropSelectionEffects.FractionalYears(.08,.9)==0&&HistoricalCropSelectionEffects.FractionalYears(0,0)==0,"Named fractional draws permit slow practice without an invented minimum annual gain.");
        int shifts=Enumerable.Range(0,10000).Sum(i=>HistoricalCropSelectionEffects.LatitudeMovement(300,500,1,i/10000.0));
        check(shifts==200&&HistoricalCropSelectionEffects.LatitudeMovement(499,500,100,0)==1,"Tiny aggregate-event latitude shifts retain their expected rate and cannot overshoot the local target.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();
        var s=engine.UpgradeRealmLifecycles(source);engine.Validate(s);check(s.History.Aggregates.Empires.Connections.Realms.Version>=10,"Only explicit revision10 enables the bounded active local selection branch.");
        check(StateHash.Compute(source.History.Agriculture)==StateHash.Compute(s.History.Agriculture),"Upgrade itself preserves every actual crop identity, trait and dated practice record.");
        var candidates=from culture in s.Society.Cultures where s.History.Cultures[culture.Id].ParentCultureId>=0&&culture.Farming>=50 from cropGroup in Enumerable.Range(0,6) let crops=s.History.Agriculture[culture.Id] let seed=crops.Cultivars[cropGroup] let cell=crops.PracticeCell[cropGroup] where crops.AdoptedYear[cropGroup]>0&&seed!=null&&seed.SelectedYears>=4000&&Math.Abs(seed.DaylengthLatitudeTenths-Math.Abs(Math.Asin(world.Surface.Cells[cell].Position.Z))*180/Math.PI*10)>=5&&(bool)Call(engine,"LocalCropSelectionFieldOccupied",s,culture.Id,cell)&&(double)Call(engine,"CommunityTransferFit",s,cell,seed,false)>=.12 select(culture.Id,@group:cropGroup,cell);
        var selected=candidates.First();var probe=s.Copy();var p=probe.History.Agriculture[selected.Id];var seedBefore=p.Cultivars[selected.group].Copy();
        int native=p.NativeMask,sourceCulture=p.SourceCulture[selected.group],target=(int)Math.Round(Math.Abs(Math.Asin(world.Surface.Cells[selected.cell].Position.Z))*180/Math.PI*10);
        long people=probe.History.Aggregates.TotalPopulation,food=probe.Society.Settlements.Sum(t=>t.FoodReserve),money=probe.History.Polities.Sum(t=>t.Treasury);
        for(int k=0;k<50;k++){probe.Years+=20;Call(engine,"AdvanceAggregateLocalCropSelection",probe,selected.Id,selected.group,20,selected.cell);}
        var after=p.Cultivars[selected.group];
        check(after.SelectedYears>seedBefore.SelectedYears&&Math.Abs(after.DaylengthLatitudeTenths-target)<Math.Abs(seedBefore.DaylengthLatitudeTenths-target),"An actual inhabited descendant's copied crop continues selection and gradually approaches its own occupied field, not the distant seed origin.");
        check(after.CropId==seedBefore.CropId&&after.OriginCulture==seedBefore.OriginCulture&&after.ParentCulture==seedBefore.ParentCulture&&p.SourceCulture[selected.group]==sourceCulture&&p.NativeMask==native,"Field selection invents no native species, donor, domestication or lineage replacement.");
        check(probe.History.Aggregates.TotalPopulation==people&&probe.Society.Settlements.Sum(t=>t.FoodReserve)==food&&probe.History.Polities.Sum(t=>t.Treasury)==money,"The physiological update allocates no residents, harvest stock or treasury; it uses existing cultivated practice.");
        var negative=s.Copy();negative.History.Aggregates.Empires.Connections.Realms.Version=9;string before=StateHash.Compute(negative.History.Agriculture);Call(engine,"AdvanceAggregateLocalCropSelection",negative,selected.Id,selected.group,100,selected.cell);
        check(StateHash.Compute(negative.History.Agriculture)==before,"A saved revision9 world cannot silently acquire later breeding behavior.");
        negative=s.Copy();negative.Society.Cultures[selected.Id].Farming=0;before=StateHash.Compute(negative.History.Agriculture);Call(engine,"AdvanceAggregateLocalCropSelection",negative,selected.Id,selected.group,100,selected.cell);
        check(StateHash.Compute(negative.History.Agriculture)==before,"Knowledge without current cultivation produces no free adaptation.");
        int empty=Enumerable.Range(0,HistoricalCells.Count(s)).First(cell=>engine.CellPatch[cell]>=0&&HistoricalCells.Culture(s,cell)<0&&s.History.Aggregates.TownAt[cell]<0);
        negative=s.Copy();negative.History.Agriculture[selected.Id].PracticeCell[selected.group]=empty;before=StateHash.Compute(negative.History.Agriculture);Call(engine,"AdvanceAggregateLocalCropSelection",negative,selected.Id,selected.group,100,empty);
        check(StateHash.Compute(negative.History.Agriculture)==before,"An empty frontier with no actual local cultivating community cannot breed a crop into habitability.");
        var unusable=(from town in s.Society.Settlements where town.Population>=100&&s.History.Aggregates.Living.Cities[town.Id].AbandonedYear<0&&s.Society.Cultures[town.CultureId].Farming>=50 from cropGroup in Enumerable.Range(0,6) let crops=s.History.Agriculture[town.CultureId] where crops.AdoptedYear[cropGroup]>0&&crops.Cultivars[cropGroup]!=null&&(double)Call(engine,"CommunityTransferFit",s,town.Cell,crops.Cultivars[cropGroup],true)==0 select(town.CultureId,town.Cell,Group:cropGroup)).First();
        negative=s.Copy();negative.History.Agriculture[unusable.CultureId].PracticeCell[unusable.Group]=unusable.Cell;before=StateHash.Compute(negative.History.Agriculture);Call(engine,"AdvanceAggregateLocalCropSelection",negative,unusable.CultureId,unusable.Group,100,unusable.Cell);
        check(StateHash.Compute(negative.History.Agriculture)==before,"A real inhabited farmer town cannot select an environmentally zero-fit known crop into viability without cultivation.");
        negative=s.Copy();before=StateHash.Compute(negative.History.Agriculture);Call(engine,"AdvanceAggregateLocalCropSelection",negative,selected.Id,selected.group,0,selected.cell);
        check(StateHash.Compute(negative.History.Agriculture)==before,"An occupied field with no elapsed practice receives no selection.");
        world.Prehistory=s;var restoredWorld=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(restoredWorld);var restored=restoredWorld.Prehistory;cold.Validate(restored);
        check(restored.Hash==s.Hash&&StateHash.Compute(restored.History.Agriculture)==StateHash.Compute(s.History.Agriculture),"Full-world save/load retains selected local seed traits and field provenance.");
        var full=engine.AdvanceYears(s,12);var split=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);engine.Validate(full);check(full.Hash==split.Hash,"Actual12-year crop-calendar continuation equals cold5+7-year publication partitions.");
        check(source.ComputeHash()==original,"Selection fixtures, upgrade and real continuations leave the saved input unchanged.");
        Console.WriteLine($"Local crop selection: actual descendant culture{selected.Id}, group{selected.group}, field{selected.cell}, selected {seedBefore.SelectedYears}->{after.SelectedYears}, latitude {seedBefore.DaylengthLatitudeTenths}->{after.DaylengthLatitudeTenths}, target{target}.");
    }
}

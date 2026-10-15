using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryColonialTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string oldHash=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToColonialHistory(source);engine.Validate(s);var a=s.History.Aggregates;var connections=a.Empires.Connections;
        check(source.ComputeHash()==oldHash&&s.History.Version==Prehistory.ColonialHistoryVersion,"An explicit colonial upgrade preserves its source");
        check(connections.RegionAreas.Sum()>0&&connections.Titles.Length==0&&a.TotalPopulation==source.History.Aggregates.TotalPopulation,"Claim geography initializes without inventing population or territorial titles");
        check(s.History.Archive.Count==source.History.Archive.Count&&s.Society.Cultures.Select(c=>c.Name).SequenceEqual(source.Society.Cultures.Select(c=>c.Name)),"Setup does not rewrite past names or events");
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args);
        var farmProbe=s.Copy();farmProbe.Society.Cultures[0].Farming=600;farmProbe.Society.Cultures[0].Population=1000000;farmProbe.History.Aggregates.Cultures[0].Urban=5000;farmProbe.History.Aggregates.Cultures[0].Rural=0;
        check((double)Call("TechnicalAgriculturalWorkforce",farmProbe,0)==995000,"Remote agricultural residents count toward deployed farming demand even without city catchment farmers");
        farmProbe.History.Version=Prehistory.TechnicalHistoryVersion;
        check((double)Call("TechnicalAgriculturalWorkforce",farmProbe,0)==0,"The corrected demand does not silently rewrite older technical rules");
        var technical=s.History.Cultures[0].Technical;var technicalCopy=technical.Copy();
        check(StateHash.Compute(technical)==StateHash.Compute(technicalCopy)&&typeof(HistoricalTechnicalPractice).GetFields().Where(f=>f.FieldType.IsArray).All(f=>!ReferenceEquals(f.GetValue(technical),f.GetValue(technicalCopy))),"Allocation-efficient technical copying preserves all values and detaches every public array");
        var sailor=s.History.Cultures[0].Copy();Array.Clear(sailor.Technical.Deployment);Array.Clear(sailor.Technical.Levels);int navigation=(int)HistoricalTechnology.OceanNavigation;sailor.Technical.Deployment[navigation]=1000;sailor.Technical.Levels[navigation]=1;
        check(HistoricalTechnicalMigration.SeaRangeM(sailor,s.Years)==2750000&&HistoricalTechnicalMigration.SeaRangeM(sailor,s.Years,true)==5000000,"Provisioned blue-water craft can cross an ocean without waiting for industrial engines; old range rules remain unchanged");
        sailor.Technical.Deployment[navigation]=0;sailor.Technical.Levels[navigation]=0;
        check(HistoricalTechnicalMigration.SeaRangeM(sailor,s.Years)==HistoricalTechnicalMigration.SeaRangeM(sailor,s.Years,true),"The ocean provisioning correction does not give primitive voyagers ocean-going ships");
        var early=s.Copy();early.Years=10000;var middle=s.Copy();middle.Years=115000;var late=s.Copy();late.Years=170000;
        double start=(double)Call("CulturalRecognitionMaturity",early,0)!,peak=(double)Call("CulturalRecognitionMaturity",middle,0)!,end=(double)Call("CulturalRecognitionMaturity",late,0)!;
        check(start<.001&&peak>start&&end>.97,"Recognizable traditions have a soft late-prehistory opportunity window, not an early universal unlock");
        int first=0,second=Math.Min(1,s.Society.Cultures.Length-1),region=0;
        if(first!=second)
        {
            Call("RecordCoastChart",s,first,region,first);Call("ShareCoastCharts",s,first,second);
            check(StateHash.Compute(connections.Cultures[first].Charts)==StateHash.ComputeHistoricalReference(connections.Cultures[first].Charts),"Typed chart hashing preserves the exact reference canonical bytes");
            check(connections.Cultures[second].Charts.Any(c=>c.Region==region&&c.SourceCulture==first&&c.Year==s.Years),"A delivered contact shares a dated chart with its actual source");
            var branch=s.Copy();branch.History.Aggregates.Empires.Connections.Cultures[second].Charts[0].Region=1;
            check(connections.Cultures[second].Charts[0].Region==region,"Chart records are isolated between published branches");
        }
        var live=s.History.Polities.Where(p=>p.EndedYear<0).Take(2).ToArray();
        if(live.Length==2)
        {
            int cell=Enumerable.Range(0,engine.CellPatch.Length).First(i=>engine.CellPatch[i]>=0&&HistoricalCells.Polity(s,i)<0),r=engine.CellPatch[cell];
            var seller=live[0];var buyer=live[1];buyer.Treasury=100000;long beforeTreasury=seller.Treasury+buyer.Treasury,beforePopulation=a.TotalPopulation;int beforeCulture=HistoricalCells.Culture(s,cell);
            connections.Titles=new[]{new HistoricalRecognizedTitle{Id=0,Region=r,Holder=seller.Id,Recognizer=buyer.Id,EstablishedYear=s.Years,LastTransferYear=s.Years,AreaKm2=connections.RegionAreas[r],Basis="Test bilateral agreement"}};connections.TitleAt=connections.TitleAt.With(r,0);connections.Governments[seller.Id].Titles.Add(0);s.Seal();
            check(HistoricalRecognizedClaims.Holder(s,cell)==seller.Id&&HistoricalCells.Polity(s,cell)<0,"Recognized title is separate from actual administration");
            check(SocietyView.OverlayId(s,cell,HistoryOverlay.States)==HistoricalImperialHierarchy.Sovereign(s,seller.Id)&&SocietyView.OverlayId(s,cell,HistoryOverlay.Territories)<0,"Political maps show recognized titles without inventing local courts");
            var profile=HistoryAtlas.Selection(s,cell,HistoryOverlay.States);check(profile?.Kind=="claim"&&HistoryAtlas.Summary(s,profile).Contains("not a census"),"Claim selection explicitly distinguishes title from settlers");
            Call("TransferRecognizedTitle",s,0,buyer.Id,2000L);
            check(seller.Treasury+buyer.Treasury==beforeTreasury&&a.TotalPopulation==beforePopulation&&HistoricalCells.Culture(s,cell)==beforeCulture&&HistoricalCells.Polity(s,cell)<0,"A title sale conserves money and people without replacing local identity or administration");
            check(connections.Titles[0].Holder==buyer.Id&&connections.Governments[seller.Id].Titles.Count==0&&connections.Governments[buyer.Id].Titles.Count==1,"A sale moves the dated legal title and its indexes together");
            var broken=s.Copy();broken.History.Aggregates.Empires.Connections.Governments[buyer.Id].Titles.Remove(0);bool rejected=false;
            try{Call("ValidateColonialConnections",broken);}catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException){rejected=true;}
            check(rejected,"Untrusted saves cannot silently lose a recognized title's ownership index");
            long beforeSale=seller.Treasury+buyer.Treasury;Call("TransferRecognizedTitle",s,0,seller.Id,long.MaxValue);
            check(seller.Treasury+buyer.Treasury==beforeSale&&connections.Titles[0].Holder==buyer.Id,"An unaffordable title sale cannot mint money or transfer title");
        }
        s.Seal();engine.Validate(s);var three=engine.AdvanceYears(s,3);engine.Validate(three);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(three.Hash==split.Hash,"Colonial events remain publication-partition deterministic");
        world.Prehistory=three;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==three.Hash,"Charts, titles, continuity and family roots survive saved-world round trips");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(three,2).Hash,"Saved colonial actors continue identically");
        check(source.ComputeHash()==oldHash,"All colonial branches leave the old checkpoint unchanged");
    }
}

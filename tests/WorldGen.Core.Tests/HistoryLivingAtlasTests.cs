using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryLivingAtlasTests
{
    public static void ProvisionedWorld(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var s=world.Prehistory;engine.Validate(s);string hash=s.ComputeHash();
        var ledger=s.History.Aggregates?.Empires?.Connections?.ProvisionedPorts??throw new Exception("Harbor atlas fixture needs recorded provisioned ports.");
        var port=ledger.Ports.FirstOrDefault(p=>p.ReceivedFood>0)??throw new Exception("Harbor atlas fixture needs an actual delivered shipment.");
        var town=s.Society.Settlements[port.TownId];var document=HistoryAtlas.Read(s,"city",town.Id,world,engine);
        var supply=document.Sections.Single(x=>x.Title=="Harbor and supply");
        check(supply.Links.Single().Id==port.SupplierTownId&&supply.Links.Single().Kind=="city","Harbor page links its recorded real supplier, not an invented local farming economy");
        check(supply.Text.Contains("not a new farming hinterland")&&supply.Text.Contains("not a guarantee"),"Harbor page distinguishes installed infrastructure from food actually available");
        check(supply.Facts.Any(f=>f.Label=="Total cargo received")&&supply.Facts.Any(f=>f.Label=="Freight paid")&&supply.Facts.Any(f=>f.Label=="Last received cargo"),"Actual cargo, freight and review interval are visible together");
        check(document.Facts.Any(f=>f.Label=="Installed harbor capacity")&&!document.Facts.Any(f=>f.Label=="Food-support capacity"),"Harbor headline does not mistake handling capacity for actual food supply");
        var footprint=document.Sections.Single(x=>x.Title=="Settlement history and hinterland");
        check(footprint.Facts.Any(f=>f.Label=="Reserved harbor footprint")&&!footprint.Facts.Any(f=>f.Label=="Supply area"),"Reserved port land is not described as farming hinterland");
        var provisioning=document.Sections.Single(x=>x.Title=="Provisioning and resilience");
        check(provisioning.Facts.Any(f=>f.Label=="Latest recorded food imports")&&!provisioning.Facts.Any(f=>f.Label=="Lifetime food imports"),"Aggregate food flow labels do not misrepresent reset-per-update accounts as lifetime totals");
        check(provisioning.Facts.Any(f=>f.Label=="Latest outgoing transport losses")&&provisioning.Text.Contains("incoming route losses"),"Sender-side cargo losses are not misleadingly described as the harbor's incoming shipping losses");
        check(HistoryAtlas.PlainText(document).Contains("Harbor and supply")&&s.ComputeHash()==hash,"Reading and copying actual harbor information never mutates saved cargo or history");
        Console.WriteLine("Harbor atlas town "+town.Id+" · "+town.Name+" · "+port.ReceivedFood+" recorded person-years received");
    }
    public static void ContinuityWorld(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var s=world.Prehistory;engine.Validate(s);string hash=s.ComputeHash();
        var system=s.History.International??throw new Exception("Continuity atlas fixture needs international continuity records.");
        var successor=s.History.Polities.FirstOrDefault(p=>p.ParentId>=0&&system.Accounts[p.ParentId].ContinuitySuccessor==p.Id)??throw new Exception("Continuity atlas fixture needs an actual recorded regime replacement.");
        var article=HistoryAtlas.Read(s,"state",successor.Id,world,engine);
        check(article.Facts.Any(f=>f.Label=="Regime established")&&!article.Facts.Any(f=>f.Label=="Founded"),"Actual successor article dates the regime rather than refounding its country's entire history");
        check(article.Introduction.Contains("not the first appearance of the country"),"Actual institutional continuity is explained in ordinary historical language");
        var link=HistoryAtlas.Directory(s,"state",past:true).Single(x=>x.Id==successor.Id);
        check(link.Context.StartsWith("Regime since ",StringComparison.Ordinal),"Directory and article distinguish the same regime date consistently");
        var body=system.Organizations.FirstOrDefault();
        if(body!=null)check(HistoryAtlas.Read(s,"organization",body.Id,world,engine).Introduction.Contains("government then ruling"),"Organization founding refers to the historical government, not a later same-named regime");
        check(s.ComputeHash()==hash,"Reading actual regime and organization continuity cannot modify saved history");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var s=engine.UpgradeToLivingWorldHistory(world.Prehistory);string hash=s.ComputeHash();
        int state=Array.FindIndex(s.History.Polities,p=>p.EndedYear<0);
        if(state<0)throw new InvalidOperationException("Atlas fixture needs a state.");
        var document=HistoryAtlas.Read(s,"state",state,world,engine);
        check(document.Sections.Single(x=>x.Title=="Local resource potential").Links.Length==13,"Atlas displays all thirteen resources as potentials rather than unrecorded reserves");
        var resourcePanel=document.Sections.Single(x=>x.Title=="Local resource potential");
        check(resourcePanel.CompactGrid&&resourcePanel.Text.StartsWith("Near ",StringComparison.Ordinal),"Resource grid is compact and explicitly local, not an empire-wide mineral census");
        var interests=document.Sections.Single(x=>x.Title=="Political interests");
        check(interests.Links.Length==5&&interests.Links.Sum(x=>x.Value)==1000&&interests.Text.Contains("not measured"),"Government infographic distinguishes five derived interests from actual party membership");
        check(document.Sections.Single(x=>x.Title=="Public priorities").Links.Sum(x=>x.Value)==1000,"Budget infographic preserves the finite four-way allocation");
        check(document.Sections.Single(x=>x.Title=="Public priorities").Links.All(x=>x.Detail.Contains("% of the funded program")),"Public-priority shares are readable numeric values, not unlabeled bars");
        int culture=s.History.Polities[state].CultureId;var people=HistoryAtlas.Read(s,"people",culture,world,engine);
        var appearance=people.Sections.Single(x=>x.Title=="People and appearance");
        check(appearance.Facts.Count(x=>x.Swatch.Length==7&&x.Swatch.StartsWith("#",StringComparison.Ordinal))==3,"People page uses actual skin, hair and iris swatches, not an invented ethnic portrait");
        check(appearance.Text.Contains("no effect on intelligence"),"Appearance remains separate from historical capability");
        check(document.Sections.Any(x=>x.Title=="Climate and livelihoods")&&HistoryAtlas.PlainText(document).Contains("Local resource potential"),"Climate/resource explanations are available in the article and copied text");
        check(s.ComputeHash()==hash,"Opening all living-world infographics does not mutate history");
        var illustrated=s.Copy();
        var body=new HistoricalInternationalOrganization{Id=0,Name="River Compact",FounderPolity=state,FoundedYear=s.Years,LastActionYear=s.Years,Kind=HistoricalOrganizationKind.TradeLeague,Reserve=800,Contributions=1000,ServiceSpending=200};body.Members.Add(state);
        illustrated.History.International.Organizations=new[]{body};illustrated.History.International.Finances=null;illustrated.History.International.Accounts[state].TradeOrganization=0;
        var arsenal=illustrated.History.Nuclear.States[state];arsenal.ProgramYear=s.Years;arsenal.FirstWeaponYear=s.Years;arsenal.Produced=arsenal.Stockpile=2;arsenal.ReadinessMilli=600;arsenal.Investment=500000;
        string illustratedHash=illustrated.ComputeHash();
        var illustratedState=HistoryAtlas.Read(illustrated,"state",state,world,engine);
        check(illustratedState.Sections.Single(x=>x.Title=="Diplomatic institutions").Links.Single().Name=="River Compact","State page links its actual recorded organization rather than inventing a global institution");
        check(illustratedState.Sections.Single(x=>x.Title=="Nuclear capabilities").Facts.Single(x=>x.Label=="Weapons in custody").Value=="2","Nuclear infographic distinguishes actual physical inventory from design knowledge");
        var organization=HistoryAtlas.Read(illustrated,"organization",0,world,engine);
        check(organization.Title=="River Compact"&&organization.Sections.Single(x=>x.Title=="Member governments").Links.Single().Id==state,"Diplomatic organization opens its own roster and linked government");
        check(organization.Sections.Single(x=>x.Title=="Common finances").Facts.Single(x=>x.Label=="Unspent reserve").Value=="800","Organization article uses the actual funded reserve");
        check(!organization.Facts.Any(x=>x.Label=="Accepted mediations"),"Trade-league overview does not misleadingly present a zero peace-congress mediation statistic");
        check(HistoryAtlas.Directory(illustrated,"organization").Single().Kind=="organization"&&HistoryAtlas.ProfileLabel(new AtlasLink{Kind="organization"})=="Organization","New diplomatic records are discoverable and correctly labeled in the atlas directory");
        check(illustrated.ComputeHash()==illustratedHash,"Reading nuclear and organization records does not mutate their accounts");
        var budgeted=illustrated.Copy();budgeted.History.International.Finances=new HistoricalInternationalFinances{Accounts=illustrated.History.International.Accounts.Select(_=>new HistoricalInternationalFinanceAccount()).ToArray(),Organizations=new[]{new HistoricalInternationalOperatingBudget{AnnualAssessments=200,UnfundedWork=50}}};
        string budgetedHash=budgeted.ComputeHash();var budgetArticle=HistoryAtlas.Read(budgeted,"organization",0,world,engine);
        var operating=budgetArticle.Sections.Single(x=>x.Title=="Operating budget");
        check(operating.Facts.Single(x=>x.Label=="Annual operating budget").Value=="200 treasury units"&&operating.Facts.Single(x=>x.Label=="Reserve coverage").Value=="4 years","Organization article distinguishes annual services and reserve years from cumulative contributions");
        check(operating.Facts.Single(x=>x.Label=="Unpaid operating work").Value=="50 treasury units"&&budgeted.ComputeHash()==budgetedHash,"Unpaid diplomatic services are visible without mutating the finance ledger");
    }
}

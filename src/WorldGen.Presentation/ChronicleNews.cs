using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // News is an observer's editorial view, never a replacement for the archive.
    public sealed class ChronicleNews
    {
        readonly PrehistoryState state;
        readonly int[] firstYear,firstCulture;
        readonly long significant;
        public ChronicleNews(PrehistoryState state)
        {
            this.state=state;int count=HistoricalTechnologies.Names.Length;
            firstYear=Enumerable.Repeat(int.MaxValue,count).ToArray();firstCulture=Enumerable.Repeat(-1,count).ToArray();
            significant=Math.Max(2500,SocietyView.Population(state)/100);
            if(state.History==null)return;
            // Actor records only; no terrain census or lifetime-archive scan.
            foreach(var c in state.History.Cultures)for(int k=0;k<Math.Min(count,c.Technology.Length);k++)
                if(c.Technology[k]>0&&(c.Technology[k]-1<firstYear[k]||c.Technology[k]-1==firstYear[k]&&c.Id<firstCulture[k]))
                {firstYear[k]=c.Technology[k]-1;firstCulture[k]=c.Id;}
        }
        string People(HistoricalEvent e)=>e.CultureId>=0&&e.CultureId<state.Society.Cultures.Length?state.Society.Cultures[e.CultureId].Name:"An early community";
        string Court(int id)=>id>=0&&id<state.History.Polities.Length?state.History.Polities[id].Name:"A state";
        bool Major(int id)=>id>=0&&id<state.History.Polities.Length&&Math.Max(state.History.Polities[id].Population,
            state.History.Dynamics?.States.Length>id?state.History.Dynamics.States[id].PeakPopulation:0)>=significant;
        bool MajorReligiousCommunity(HistoricalEvent e)=>Major(e.PolityId)||e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length&&state.Society.Settlements[e.SettlementId].Population>=Math.Max(5000,significant/5);
        public ChronicleEvent? Project(HistoricalEvent e)
        {
            string text="";
            if(e.Kind=="technology")
            {
                // Inherited/adopted knowledge retains its local history, but
                // isn't another world-first invention.
                for(int k=0;k<firstYear.Length;k++)if(firstYear[k]==e.Year&&firstCulture[k]==e.CultureId&&e.Text.IndexOf(HistoricalTechnologies.Name(state.History.Version,k),StringComparison.OrdinalIgnoreCase)>=0)
                {text=People(e)+" discovered "+HistoricalTechnologies.Name(state.History.Version,k)+".";break;}
            }
            else if((e.Kind=="domestication"||e.Kind=="crop-domestication")&&firstYear[(int)HistoricalTechnology.Cultivation]==e.Year&&firstCulture[(int)HistoricalTechnology.Cultivation]==e.CultureId)
                text=People(e)+" discovered cultivation.";
            else if(e.Category==ChronicleCategory.Discoveries)return null;
            else switch(e.Kind)
            {
                case "war":if(Major(e.PolityId)||Major(e.OtherPolityId))text=Court(e.PolityId)+" went to war with "+Court(e.OtherPolityId)+".";break;
                case "peace":if(Major(e.PolityId)||Major(e.OtherPolityId))text=PeaceOutcome(e);break;
                case "collapse":if(Major(e.PolityId))text=Court(e.PolityId)+" fell apart.";break;
                case "state":case "independent-institutions":case "state-founded":case "state-foundation":case "empire-founded":if(e.PolityId==0||Major(e.PolityId))text=Court(e.PolityId)+" was founded.";break;
                case "settlement":if(e.SettlementId==0&&state.Society.Settlements.Length>0)text=state.Society.Settlements[0].Name+" became the first recorded settlement.";break;
                case "overseas-charter":if(Major(e.PolityId))text=Court(e.PolityId)+(state.History.Version==Prehistory.LivingWorldHistoryVersion?" established an overseas office.":" established an overseas colony.");break;
                case "sponsored-port":if(Major(e.PolityId))text=e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length?Court(e.PolityId)+" settled a new overseas port at "+state.Society.Settlements[e.SettlementId].Name+".":Sentence(e.Text);break;
                case "international-foundation":case "international-mediation":case "treaty-breach":case "coalition-war":
                case "colonial-council":case "colonial-protectorate":case "political-repression":case "domestic-office-contest":
                    if(Major(e.PolityId)||Major(e.OtherPolityId))text=Sentence(e.Text);break;
                case "nuclear-strike":text=Sentence(e.Text);break;
                case "nuclear-arsenal":if(Major(e.PolityId))text=Court(e.PolityId)+" commissioned its first nuclear arsenal.";break;
                case "colonial-transfer":case "title-transfer":if(Major(e.PolityId)||Major(e.OtherPolityId))text=Sentence(e.Text);break;
                case "imperial-independence":case "secession":case "provincial-revolt":
                case "great-ruler-death":
                    if(Major(e.PolityId)||Major(e.OtherPolityId))text=Sentence(e.Text);break;
                case "great-conquest":if(Major(e.PolityId)||Major(e.OtherPolityId))text=e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length?Court(e.PolityId)+" captured "+state.Society.Settlements[e.SettlementId].Name+".":Sentence(e.Text);break;
                case "succession-crisis":if(Major(e.PolityId))text="A succession crisis shook "+Court(e.PolityId)+".";break;
                case "realm-succession-crisis":if(Major(e.PolityId))text="Regional courts contested the imperial succession of "+Court(e.PolityId)+".";break;
                case "regime-change":if(Major(e.PolityId)||Major(e.OtherPolityId))text=e.OtherPolityId>=0?Court(e.PolityId)+" succeeded "+Court(e.OtherPolityId)+".":Sentence(e.Text);break;
                case "dynasty-change":if(Major(e.PolityId))text=Court(e.PolityId)+" came under a new ruling household.";break;
                case "religion":case "religion-founded":case "schism":case "religious-reform":text=e.ReligionId>=0&&e.ReligionId<state.History.Religions.Length?state.History.Religions[e.ReligionId].Name+" emerged as a religious tradition.":Sentence(e.Text);break;
                case "religious-schism":case "religious-synthesis":case "religious-renewal":if(MajorReligiousCommunity(e))text=Sentence(e.Text);break;
                case "religious-policy":if(Major(e.PolityId))text=Sentence(e.Text);break;
                case "culture":if(e.CultureId==0)text=People(e)+" formed the first recorded advanced culture.";else if(MajorPeople(e.CultureId))text=People(e)+" emerged as a distinct advanced culture.";break;
                case "cultural-transformation":if(MajorPeople(e.CultureId)){int split=e.Text.IndexOf(" develops into",StringComparison.Ordinal);string successor=Between(e.Text,"now called "," as ");text=split>=0&&successor.Length>0?successor+" emerged from the "+e.Text.Substring(0,split)+" tradition.":Sentence(e.Text);}break;
                case "political-reform":if(Major(e.PolityId))text=Court(e.PolityId)+" broadened local political participation.";break;
                case "court-conversion":case "public-works":if(Major(e.PolityId))text=Sentence(e.Text);break;
                case "major-epidemic":case "food-shortage":
                    if(e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length&&state.Society.Settlements[e.SettlementId].Population>=Math.Max(10000,significant/5))text=Sentence(e.Text);break;
            }
            return text.Length==0?null:new ChronicleEvent{Year=e.Year,CultureId=e.CultureId,Cell=e.Cell,Category=e.Category,Text=text};
        }
        public static int Window(ChronicleCategory category)=>category==ChronicleCategory.Politics?5:category==ChronicleCategory.Society?20:1;
        // A genuinely extraordinary development can replace a lesser lead in
        // the same editorial window. This never removes its archival record.
        public static int Priority(string kind)=>kind=="nuclear-strike"?100:kind=="coalition-war"||kind=="treaty-breach"?60:kind=="collapse"||kind=="great-conquest"?40:kind=="international-foundation"||kind=="international-mediation"||kind=="nuclear-arsenal"?20:kind=="religious-schism"||kind=="religious-synthesis"||kind=="religious-renewal"?10:0;
        bool MajorPeople(int id)=>id>=0&&id<state.Society.Cultures.Length&&state.Society.Cultures[id].Population>=Math.Max(5000,significant/10);
        string PeaceOutcome(HistoricalEvent e)
        {
            int a=e.PolityId,b=e.OtherPolityId;string pair=Court(a)+" and "+Court(b),war="The war between "+Court(a)+" and "+Court(b);
            bool Valid(int id)=>id>=0&&id<state.History.Polities.Length;
            // Dated facts only. A country's later fall or later imperial patron
            // must not rewrite the meaning of an earlier peace announcement.
            bool fellA=Valid(a)&&state.History.Polities[a].EndedYear==e.Year,fellB=Valid(b)&&state.History.Polities[b].EndedYear==e.Year;
            if(fellA&&fellB)return war+" ended with both governments falling.";
            if(fellA||fellB)
            {
                int fallen=fellA?a:b,other=fellA?b:a;var p=state.History.Polities[fallen];
                var capture=Prehistory.SovereigntyTransferAt(state,p.CapitalId);
                if(capture!=null&&capture.Cause==HistoricalSovereigntyCause.Conquest&&capture.From==fallen&&capture.To==other&&capture.Year<=e.Year&&capture.Year>=e.Year-1)
                    return Court(other)+" defeated "+Court(fallen)+", ending its independent government.";
                return Court(fallen)+"'s government fell, ending its war with "+Court(other)+".";
            }
            string outcome=e.Text??"";int end=outcome.IndexOf(" ends. ",StringComparison.Ordinal);
            if(end>=0)outcome=outcome.Substring(end+7).Trim();
            bool Has(string words)=>outcome.IndexOf(words,StringComparison.OrdinalIgnoreCase)>=0;
            if(Has("constituent")||Has("tributary settlement"))
            {
                var governments=state.History.Aggregates?.Empires?.Governments;
                if(governments!=null)foreach(int client in new[]{a,b})if(Valid(client)&&client<governments.Length)
                {var g=governments[client];int patron=client==a?b:a;if(g.Patron==patron&&g.SinceYear==e.Year)return Court(client)+" submitted to "+Court(patron)+", retaining its local government.";}
                return war+" ended in imperial submission, with the defeated court retained rather than annexed.";
            }
            if(Has("same imperial settlement"))return pair+" stopped fighting after entering the same imperial settlement.";
            if(Has("tribute"))return pair+" ended their war with a tribute settlement.";
            if(Has("trade route"))return pair+" settled their war by reopening the contested trade route.";
            if(Has("limited frontier"))return pair+" ended their war with a limited frontier settlement.";
            if(Has("Neither side can sustain"))return pair+" abandoned their war after neither side could keep its army supplied.";
            if(Has("ceasefire"))return pair+" agreed to a ceasefire after losses and stalled offensives.";
            if(Has("exhaustion")||Has("costly"))return pair+" negotiated peace after a costly war.";
            // Retain unusual recorded terms rather than erase them. Old events
            // without terms remain honest about what the archive actually knows.
            if(end>=0&&outcome.Length>0)return war+" ended. "+outcome.TrimEnd('.')+".";
            return pair+" agreed to end hostilities; no detailed terms were recorded.";
        }
        static string Sentence(string text){int end=text.IndexOf(". ",StringComparison.Ordinal);return end>=0?text.Substring(0,end+1):text;}
        static string Between(string text,string start,string end){int first=text.IndexOf(start,StringComparison.Ordinal);if(first<0)return "";first+=start.Length;int last=text.IndexOf(end,first,StringComparison.Ordinal);return last<0?"":text.Substring(first,last-first);}
    }
}

using System;
using System.Linq;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public const string LanguageHistoryVersion="history-30";
        static bool LanguageHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==LanguageHistoryVersion||EnvironmentalHistory(s);
        public PrehistoryState UpgradeToLanguageHistory(PrehistoryState source)
        {
            Validate(source);if(LanguageHistory(source))return source;
            source=UpgradeToResearchPacingHistory(source);var s=source.Copy();s.History.Version=LanguageHistoryVersion;
            var profiles=new LanguageProfile[s.History.Cultures.Length];
            for(int i=0;i<profiles.Length;i++)
            {
                var hc=s.History.Cultures[i];int parent=hc.LanguageId>=0&&hc.LanguageId<i?hc.LanguageId:-1;
                profiles[i]=LanguageGenerator.Create(World.Recipe.Seed,i,s.Society.Cultures[i].FoundedYear,parent>=0?profiles[parent]:null);
            }
            s.History.Languages=new HistoricalLanguages{Profiles=profiles};
            // An explicit upgrade preserves all already recorded proper names,
            // people, events and dates. Only future names use these profiles.
            s.Seal();return s;
        }
        void AppendLanguage(PrehistoryState s,int culture)
        {
            if(!LanguageHistory(s))return;var h=s.History;int lineage=h.Cultures[culture].LanguageId;
            int direct=h.Cultures[culture].ParentCultureId;
            var parent=lineage>=0&&lineage<culture?h.Languages.Profiles[direct>=0&&direct<culture&&h.Cultures[direct].LanguageId==lineage?direct:lineage]:null;
            var profile=LanguageGenerator.Create(World.Recipe.Seed,culture,s.Years,parent);
            Array.Resize(ref h.Languages.Profiles,culture+1);h.Languages.Profiles[culture]=profile;
            s.Society.Cultures[culture].Name=LanguageGenerator.Capitalize(LanguageGenerator.Name(profile,LanguageNameKind.People,culture).Text);
            h.Religions[h.Cultures[culture].ReligionId].Name="Tradition of "+s.Society.Cultures[culture].Name;
        }
        void InheritLanguage(PrehistoryState s,int child,int parent)
        {
            if(!LanguageHistory(s))return;var h=s.History;
            h.Languages.Profiles[child]=LanguageGenerator.Create(World.Recipe.Seed,child,s.Years,h.Languages.Profiles[parent]);
            s.Society.Cultures[child].Name=LanguageGenerator.Capitalize(LanguageGenerator.Name(h.Languages.Profiles[child],LanguageNameKind.People,child).Text);
            // Only its own newly founded ancestral tradition is renamed.
            var religion=h.Religions[h.Cultures[child].ReligionId];if(religion.CultureId==child)religion.Name="Tradition of "+s.Society.Cultures[child].Name;
        }
        string LanguageName(PrehistoryState s,int culture,LanguageNameKind kind,int identity,int cell=-1)
        {
            if(!LanguageHistory(s))return HistoricalName(kind==LanguageNameKind.Settlement?"town":kind==LanguageNameKind.Person?"leader-name":"people",identity);
            string feature=null;
            if(cell>=0){if(HistoricalRiverAccess(cell)>.03)feature="river";else if(World.Surface.Cells[cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i]))feature="coast";else if(World.ElevationM[cell]>1200)feature="mountain";}
            return LanguageGenerator.Capitalize(LanguageGenerator.Name(s.History.Languages.Profiles[culture],kind,identity,feature).Text);
        }
        string RenewLanguageName(PrehistoryState s,int culture)
        {
            var profiles=s.History.Languages.Profiles;profiles[culture]=LanguageGenerator.Evolve(profiles[culture],s.Years);
            return LanguageName(s,culture,LanguageNameKind.People,culture);
        }
        string LanguageStateName(PrehistoryState s,Settlement capital)
        {
            var p=s.History.Languages.Profiles[capital.CultureId];int identity=unchecked(capital.Cell+capital.Id*7919);
            string root=LanguageGenerator.SpecificConcept(p,LanguageNameKind.Settlement,identity);
            return LanguageGenerator.Capitalize(LanguageGenerator.Compound(p,root,"land").Text);
        }
        void LanguageContact(PrehistoryState s,int recipient,int source)
        {
            if(!LanguageHistory(s)||source<0||source==recipient)return;
            var profiles=s.History.Languages.Profiles;var p=profiles[recipient];if(p.Loans.Length>=LanguageGenerator.MaximumLoans||p.FamilySeed==profiles[source].FamilySeed)return;
            if(HistoryRoll("language-contact-loan-v1",recipient,s.Years)>=.03)return;
            var concepts=new[]{"ship","writing","iron","market","king","temple"};var techs=new[]{3,6,9,10,6,6};
            int at=(int)(HistoryRoll("language-contact-domain-v1",recipient,s.Years)*concepts.Length);
            if(s.History.Cultures[source].Technology[techs[at]]>0)profiles[recipient]=LanguageGenerator.Borrow(p,profiles[source],concepts[at],s.Years);
        }
        void ValidateLanguages(PrehistoryState s)
        {
            var languages=s.History.Languages;
            if(!LanguageHistory(s)){if(languages!=null)throw new ArgumentException("Legacy history has language records.");return;}
            if(languages?.Profiles?.Length!=s.History.Cultures.Length)throw new ArgumentException("Language/culture alignment.");
            for(int i=0;i<languages.Profiles.Length;i++)
            {
                var p=languages.Profiles[i];if(!LanguageGenerator.Valid(p)||p.Id!=i||p.FamilyId>i||p.ChangedYear>s.Years||p.Loans.Any(x=>x.SourceId>=languages.Profiles.Length||x.Year>s.Years))throw new ArgumentException("Invalid language profile.");
                if(p.ParentId>=0&&(p.FamilySeed!=languages.Profiles[p.ParentId].FamilySeed||p.FamilyId!=languages.Profiles[p.ParentId].FamilyId))throw new ArgumentException("Invalid language family provenance.");
            }
        }
    }
}

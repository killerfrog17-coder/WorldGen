using System;
namespace WorldGen.Presentation
{
    public static class AtlasNavigation
    {
        public static readonly (string Kind,string Name)[] Categories={
            ("state","States"),("province","Provinces"),("city","Cities"),("people","Peoples"),
            ("ethnicity","Ethnicities"),("language","Languages"),("religion","Religions"),
            ("religious-body","Ritual bodies"),("technology","Technology"),("ruler","Rulers"),
            ("notable","Notable people"),("organization","Organizations")};
        public static bool IsDirectory(string kind)=>kind.StartsWith("directory:",StringComparison.Ordinal);
        public static string DirectoryKind(string kind)=>IsDirectory(kind)?kind.Substring(10):kind;
        public static string CategoryName(string kind)
        {foreach(var category in Categories)if(category.Kind==DirectoryKind(kind))return category.Name;return "Records";}
        public static AtlasDocument Location(SelectionCard card,int cell,int year,int epoch)
            =>new AtlasDocument{Kind="location",Id=cell,Year=year,EpochYear=epoch,
                Title=card.Title,Subtitle=card.Subtitle,Facts=card.Facts,Note=card.Note};
        public static readonly string[] Groups={"Overview","People & place","Power & relations","Economy & knowledge","History"};
        public static string Group(string title)
        {
            if(title=="Overview")return "Overview";
            if(title=="Domestic power groups"||title=="Institutional bargaining")return "Power & relations";
            if(title=="Family and household"||title=="Abilities and interests"||title=="Person and family")return "People & place";
            if(title=="Court household and notable people"||title=="Recorded public office"||title=="Career and affiliations")return "Power & relations";
            if(title=="Recorded achievements")return "History";
            if(title=="Identity and belonging"||title=="Inherited traditions"||title=="Inherited and emerging identities"||title=="Recorded communities")return "People & place";
            if(title=="Local historical memory")return "History";
            if(title=="Allegiance and sovereignty")return "Power & relations";
            if(title=="Founding context")return "People & place";
            if(title=="Spoken languages here"||title=="Sounds and reusable roots"||title=="Related speech communities"||title=="Recorded speech communities")return "People & place";
            if(title=="Social teachings"||title=="Observance and sacred obligations"||title=="Beliefs and sources of authority"||title=="Branches and doctrinal relationships")return "People & place";
            if(title=="Affiliated ritual bodies"||title=="Public worship and household practice"||title=="Ritual community and institutions")return "Power & relations";
            if(title.IndexOf("history",StringComparison.OrdinalIgnoreCase)>=0||title.Contains("timeline")||title.Contains("succession")||title.Contains("lineage")||title=="Campaigns"||title.Contains("Rulers"))return "History";
            if(title.Contains("Crop")||title.Contains("crop")||title.Contains("food")||title.Contains("Food")||title.Contains("knowledge")||title.Contains("Research")||title.Contains("Livelihood")||title.Contains("living"))return "Economy & knowledge";
            if(title.StartsWith("People,",StringComparison.Ordinal)||title.Contains("Origins")||title=="Language and names"||title=="Names for the homeland")return "People & place";
            if(title.Contains("Diploma")||title.Contains("Empire")||title.Contains("court")||title.Contains("Court")||title.Contains("Instituti")||title.Contains("Political")||title.Contains("administrat")||title.Contains("Constituent")||title.Contains("territori")||title.Contains("title")||title.Contains("Connections")||title.Contains("organization")||title=="Member governments"||title=="Mandate and limits"||title=="Founding initiative"||title=="Nuclear capabilities")return "Power & relations";
            if(title.Contains("setting")||title.Contains("Belief")||title.Contains("Origins")||title.Contains("tradition")||title.Contains("Settlements")||title=="Cities"||title.Contains("People")||title.Contains("Population"))return "People & place";
            return "Economy & knowledge";
        }
    }
}

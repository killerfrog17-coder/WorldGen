using System;
namespace WorldGen.Presentation
{
    public static class AtlasNavigation
    {
        public static readonly string[] Groups={"Overview","People & place","Power & relations","Economy & knowledge","History"};
        public static string Group(string title)
        {
            if(title=="Overview")return "Overview";
            if(title.IndexOf("history",StringComparison.OrdinalIgnoreCase)>=0||title.Contains("timeline")||title.Contains("succession")||title.Contains("lineage")||title=="Campaigns"||title.Contains("Rulers"))return "History";
            if(title.Contains("Crop")||title.Contains("crop")||title.Contains("food")||title.Contains("Food")||title.Contains("knowledge")||title.Contains("Research")||title.Contains("Livelihood")||title.Contains("living"))return "Economy & knowledge";
            if(title.StartsWith("People,",StringComparison.Ordinal)||title.Contains("Origins"))return "People & place";
            if(title.Contains("Diploma")||title.Contains("Empire")||title.Contains("court")||title.Contains("Court")||title.Contains("Instituti")||title.Contains("Political")||title.Contains("administrat")||title.Contains("Constituent")||title.Contains("territori")||title.Contains("title")||title.Contains("Connections"))return "Power & relations";
            if(title.Contains("setting")||title.Contains("Belief")||title.Contains("Origins")||title.Contains("tradition")||title.Contains("Settlements")||title=="Cities"||title.Contains("People")||title.Contains("Population"))return "People & place";
            return "Economy & knowledge";
        }
    }
}

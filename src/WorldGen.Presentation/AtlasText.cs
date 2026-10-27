using System;
using System.Text;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        public static string PlainText(AtlasDocument document,AtlasTimeline? page=null)
        {
            var text=new StringBuilder().AppendLine(document.Title).AppendLine(document.Subtitle).AppendLine("Snapshot: year "+SimulationClockView.YearNumber(document.Year,document.EpochYear)).AppendLine().AppendLine(document.Introduction);
            foreach(var fact in document.Facts)text.AppendLine(fact.Label+": "+fact.Value);
            foreach(var section in document.Sections)
            {
                text.AppendLine().AppendLine(section.Title).AppendLine(section.Text);
                foreach(var fact in section.Facts)text.AppendLine(fact.Label+": "+fact.Value);
                foreach(var project in section.Technical)text.AppendLine(project.Name+" — level "+project.Level+"; research share "+(project.AllocationBasisPoints/100.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%; next level "+(project.ResearchPermille/10.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%; practical deployment "+(project.DeploymentPermille/10.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"%. "+project.Source+". "+project.Prerequisites);
                foreach(var link in section.Links)text.AppendLine(link.Name+(link.Detail.Length>0?": "+link.Detail:""));
                var records=section.Title=="Recorded history"&&page!=null?page.Records:section.Timeline;
                foreach(var record in records)text.AppendLine(record.Label+": "+record.Value);
                if(section.Title=="Recorded history"&&page!=null)text.AppendLine("This copied page includes "+N(records.Length)+" of "+N(page.Total)+" matching records.");
            }
            return text.AppendLine().AppendLine(document.Note).ToString();
        }
    }
}

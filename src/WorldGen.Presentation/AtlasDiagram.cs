using System;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace WorldGen.Presentation
{
    public static class AtlasDiagram
    {
        public static string RelationColor(string relationship)=>relationship.StartsWith("At war",StringComparison.Ordinal)?"#df8272":relationship.Contains("Alliance")?"#8cb9df":relationship.Contains("Trading")?"#d4bd79":"#b3bbb9";
        public static string DiplomaticSvg(AtlasDocument document,AtlasSection section)
        {
            var links=section.Links.Take(8).ToArray();if(links.Length==0)return "";bool imperial=section.Title.StartsWith("Empire",StringComparison.Ordinal);
            int height=Math.Max(140,links.Length*44+30);double center=height/2.0;
            string E(string text)=>SecurityElement.Escape(text)??"";
            var b=new StringBuilder("<svg class=\"atlas-network\" role=\"img\" aria-label=\"Recorded diplomatic neighborhood\" viewBox=\"0 0 760 "+height+"\" xmlns=\"http://www.w3.org/2000/svg\">");
            for(int i=0;i<links.Length;i++)
            {
                var link=links[i];int y=30+i*44;string color=RelationColor(link.Detail);
                b.Append("<path d=\"M 165 ").Append(center.ToString(CultureInfo.InvariantCulture)).Append(" Q 310 ").Append(y).Append(" 415 ").Append(y).Append("\" fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"2\"/>");
                b.Append("<circle cx=\"415\" cy=\"").Append(y).Append("\" r=\"5\" fill=\"").Append(color).Append("\"/><text x=\"432\" y=\"").Append(y-3).Append("\" fill=\"#f1f3ee\" font-size=\"14\">").Append(E(link.Name)).Append("</text><text x=\"432\" y=\"").Append(y+13).Append("\" fill=\"").Append(color).Append("\" font-size=\"11\">").Append(E(link.Detail)).Append("</text>");
            }
            b.Append("<circle cx=\"165\" cy=\"").Append(center.ToString(CultureInfo.InvariantCulture)).Append("\" r=\"7\" fill=\"").Append(SocietyView.ColorHex(document.Id)).Append("\"/><text x=\"10\" y=\"").Append((center-16).ToString(CultureInfo.InvariantCulture)).Append("\" fill=\"#f1f3ee\" font-size=\"15\">").Append(E(SocietyView.Short(document.Title,22))).Append("</text><text x=\"10\" y=\"").Append(height-4).Append("\" fill=\"#b3bbb9\" font-size=\"11\">").Append(imperial?"Lines connect overarching and constituent governments, not geographic positions.":"Red: war · Blue: alliance · Gold: trade · Gray: contact").Append("</text></svg>");
            return b.ToString();
        }
    }
}

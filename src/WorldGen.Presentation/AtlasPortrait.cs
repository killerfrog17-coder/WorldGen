using System;
using System.Globalization;
using System.Text;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class AtlasPortrait
    {
        public static string Hex(V c)=>"#"+((int)Math.Round(c.X*255)).ToString("X2")+((int)Math.Round(c.Y*255)).ToString("X2")+((int)Math.Round(c.Z*255)).ToString("X2");
        public static HumanAppearance[] Samples(HumanAppearance profile)
        {var result=new HumanAppearance[6];for(int i=0;i<result.Length;i++)result[i]=HistoryHumanVariation.Sample(profile,17361,i);return result;}
        public static string Svg(HumanAppearance profile)
        {
            if(profile?.Available!=true)return "";
            string Num(double v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
            var b=new StringBuilder("<svg class=\"atlas-portraits\" viewBox=\"0 0 720 160\" role=\"img\" aria-label=\"Six illustrative individuals sampled from regional appearance variation\" xmlns=\"http://www.w3.org/2000/svg\">");
            int i=0;foreach(var p in Samples(profile))
            {
                double x=60+i++*120,width=23+p.FacialVariation/10000.0*11,shoulder=31+p.BodyCompactness/10000.0*17,nose=2+p.NasalVariation/10000.0*5;
                b.Append("<g><path d=\"M ").Append(Num(x-shoulder)).Append(" 146 Q ").Append(Num(x-shoulder)).Append(" 101 ").Append(Num(x)).Append(" 100 Q ").Append(Num(x+shoulder)).Append(" 101 ").Append(Num(x+shoulder)).Append(" 146 Z\" fill=\"#597987\"/>");
                b.Append("<ellipse cx=\"").Append(Num(x)).Append("\" cy=\"68\" rx=\"").Append(Num(width)).Append("\" ry=\"40\" fill=\"").Append(Hex(p.SkinColor)).Append("\"/>");
                b.Append("<path d=\"M ").Append(Num(x-width)).Append(" 58 Q ").Append(Num(x-width-4)).Append(" 17 ").Append(Num(x)).Append(" 25 Q ").Append(Num(x+width+4)).Append(" 17 ").Append(Num(x+width)).Append(" 58 Q ").Append(Num(x)).Append(" 30 ").Append(Num(x-width)).Append(" 58 Z\" fill=\"").Append(Hex(p.HairColor)).Append("\"/>");
                foreach(int side in new[]{-1,1})b.Append("<ellipse cx=\"").Append(Num(x+side*width*.42)).Append("\" cy=\"66\" rx=\"6\" ry=\"3\" fill=\"#eee6d7\"/><circle cx=\"").Append(Num(x+side*width*.42)).Append("\" cy=\"66\" r=\"2.8\" fill=\"").Append(Hex(p.IrisColor)).Append("\"/>");
                b.Append("<path d=\"M ").Append(Num(x)).Append(" 70 L ").Append(Num(x-nose)).Append(" 83 L ").Append(Num(x+nose)).Append(" 83 M ").Append(Num(x-8)).Append(" 94 Q ").Append(Num(x)).Append(" 98 ").Append(Num(x+8)).Append(" 94\" fill=\"none\" stroke=\"#4c333277\" stroke-width=\"2\"/></g>");
            }
            return b.Append("</svg>").ToString();
        }
    }
}

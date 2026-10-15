using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class ClimateColors
    {
        static V Mix(V a, V b, double t) { t = D.Clamp(t); return a * (1 - t) + b * t; }
        public static V Classification(Koppen k)
        {
            switch (k)
            {
                case Koppen.Ocean: return new V(.045, .14, .24);
                case Koppen.Af: return new V(.05, .25, .8);
                case Koppen.Am: return new V(.12, .5, .92);
                case Koppen.Aw: return new V(.35, .72, .95);
                case Koppen.BWh: return new V(.94, .2, .1);
                case Koppen.BWk: return new V(.97, .58, .5);
                case Koppen.BSh: return new V(.98, .65, .15);
                case Koppen.BSk: return new V(.98, .84, .4);
                case Koppen.ET: return new V(.68, .7, .74);
                case Koppen.EF: return new V(.94, .97, 1);
            }
            int code = (int)k;
            if (code <= (int)Koppen.Cfc)
            {
                int group = (code - (int)Koppen.Csa) / 3, heat = (code - (int)Koppen.Csa) % 3;
                return Mix(group == 0 ? new V(.95, .9, .15) : group == 1 ? new V(.35, .75, .25) : new V(.08, .55, .35), new V(.75, .91, .65), heat / 3.0);
            }
            int family = (code - (int)Koppen.Dsa) / 4, severity = (code - (int)Koppen.Dsa) % 4;
            return Mix(family == 0 ? new V(.75, .25, .7) : family == 1 ? new V(.4, .3, .75) : new V(.18, .48, .7), new V(.68, .82, .93), severity / 4.0);
        }
        public static V Satellite(World w, int i)
        {
            int h = w.ElevationM[i]; if (w.Hydrology.OceanConnected[i]) return GlobeView.Elevation(h);
            var climate = w.Climate; var kind = climate.Classification[i]; V color;
            switch (kind)
            {
                case Koppen.Af: color = new V(.08, .23, .09); break;
                case Koppen.Am: color = new V(.17, .32, .12); break;
                case Koppen.Aw: color = new V(.48, .49, .22); break;
                case Koppen.BWh: case Koppen.BWk: color = new V(.76, .64, .43); break;
                case Koppen.BSh: case Koppen.BSk: color = new V(.58, .56, .31); break;
                case Koppen.ET: color = new V(.46, .48, .4); break;
                case Koppen.EF: color = new V(.92, .95, .94); break;
                default: color = (int)kind >= (int)Koppen.Dsa ? new V(.19, .3, .22) : new V(.28, .4, .18); break;
            }
            double hot = Math.Max(Math.Max(climate.NorthernSpring.TemperatureTenthsC[i], climate.NorthernAutumn.TemperatureTenthsC[i]), Math.Max(climate.NorthernSummer.TemperatureTenthsC[i], climate.NorthernWinter.TemperatureTenthsC[i])) / 10.0;
            color = Mix(color, new V(.52, .5, .46), D.Clamp((h - 1800) / 2200.0) * .65);
            return Mix(color, new V(.94, .96, .97), D.Clamp((3 - hot) / 8.0));
        }
        public static V Temperature(int tenths) => Mix(new V(.2, .25, .7), new V(.95, .35, .12), (tenths + 450) / 900.0);
        public static V Rain(int mm) => Mix(new V(.6, .4, .2), new V(.12, .58, .74), mm / 900.0);
    }
}

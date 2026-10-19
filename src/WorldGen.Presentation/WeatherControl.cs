using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Session-only visual weather testing. Never edits climate, recorded water or history.</summary>
    public sealed class WeatherControl
    {
        public static readonly string[] Modes={"auto","blizzard","clear","cloudy","overcast","rain","snow","storm"};
        public string Mode {get;private set;}="auto";
        public int Revision {get;private set;}
        public bool Set(string mode)
        {
            mode=(mode??"").ToLowerInvariant();if(Array.IndexOf(Modes,mode)<0)return false;
            if(Mode!=mode){Mode=mode;Revision++;}return true;
        }
        public WeatherFrame Apply(WeatherFrame f,double groundM=0)
        {
            if(Mode=="auto")return f;
            bool snow=Mode=="snow"||Mode=="blizzard",storm=Mode=="storm"||Mode=="blizzard";
            double water=Mode=="rain"?4:Mode=="storm"?12:snow?storm?4:1.2:0;
            double cover=Mode=="clear"?0:Mode=="cloudy"?.6:1;
            double baseM=Math.Max(groundM+350,Math.Min(f.CloudBaseM,1200)),top=Math.Max(f.CloudBaseM+f.CloudThicknessM,baseM+1800);
            return new WeatherFrame(snow?Math.Min(-3,f.TemperatureC):water>0?Math.Max(5,f.TemperatureC):f.TemperatureC,
                water>0?.95:f.Humidity,snow?0:water,snow?water:0,cover,Mode=="clear"?0:.2,
                baseM,top-baseM,f.CirrusBaseM,storm?16:4,storm?7:1,storm?28:7,Mode=="storm"?1:0);
        }
    }
}

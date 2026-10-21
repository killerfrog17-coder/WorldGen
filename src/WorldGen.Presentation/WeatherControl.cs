using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Session-only visual weather testing. Never edits climate, recorded water or history.</summary>
    public sealed class WeatherControl
    {
        public static readonly string[] Modes={"auto","blizzard","clear","cloudy","downpour","fog","overcast","rain","snow","storm"};
        public string Mode {get;private set;}="auto";
        public int Revision {get;private set;}
        public bool Set(string mode)
        {
            mode=(mode??"").ToLowerInvariant();if(Array.IndexOf(Modes,mode)<0)return false;
            if(Mode!=mode){Mode=mode;Revision++;}return true;
        }
        public WeatherFrame Apply(WeatherFrame f,double groundM=0)=>ApplyMode(Mode,f,groundM);
        public static WeatherFrame ApplyMode(string mode,WeatherFrame f,double groundM=0)
        {
            if(mode=="auto")return f;
            bool snow=mode=="snow"||mode=="blizzard",storm=mode=="storm"||mode=="blizzard";
            double water=mode=="rain"?4:mode=="storm"?25:mode=="downpour"?50:snow?storm?4:1.2:0;
            double cover=mode=="clear"||mode=="fog"?0:mode=="cloudy"?.6:1;
            double baseM=Math.Max(groundM+350,Math.Min(f.CloudBaseM,1200)),top=Math.Max(f.CloudBaseM+f.CloudThicknessM,baseM+1800);
            return new WeatherFrame(snow?Math.Min(-3,f.TemperatureC):water>0?Math.Max(5,f.TemperatureC):f.TemperatureC,
                water>0?.95:f.Humidity,snow?0:water,snow?water:0,cover,mode=="clear"?0:.2,
                baseM,top-baseM+(water>10?2500:0),f.CirrusBaseM,storm?16:mode=="fog"?.1:4,storm?7:mode=="fog"?.05:1,storm?28:mode=="fog"?2:7,mode=="storm"?1:0,mode=="fog"?1:0,storm?.9:mode=="fog"?.15:.4,storm?24:9,storm?.9:.25);
        }
    }
}

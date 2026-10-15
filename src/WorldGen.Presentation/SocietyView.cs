using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class SocietyView
    {
        public static V Location(World world,int cell)
        {
            if(world.Hydrology.OceanConnected[cell])
            {
                var group=SubcellGeography.Find(world,cell);if(group!=null)
                {
                    SmallIsland? largest=null;for(int i=group.FirstIsland;i<group.FirstIsland+group.IslandCount;i++){var island=world.Subcells.Islands[i];if(largest==null||island.AreaM2>largest.AreaM2)largest=island;}
                    if(largest!=null)return largest.Center;
                }
            }
            return world.Surface.Cells[cell].Position;
        }
        public static string Short(string text,int length=42){text=(text??"Select a location").Replace("\r","").Replace("\n"," · ");return text.Length<=length?text:text.Substring(0,length-1)+"…";}
        public static V CivilizationColor(int id)
        {
            double h=((id*.61803398875+.06)%1)*6,f=h-Math.Floor(h),v=.82,p=.26,q=v-(v-p)*f,t=p+(v-p)*f;
            switch((int)h){case 0:return new V(v,t,p);case 1:return new V(q,v,p);case 2:return new V(p,v,t);case 3:return new V(p,q,v);case 4:return new V(t,p,v);default:return new V(v,p,q);}
        }
        public static string ColorHex(int id){var c=CivilizationColor(id);return "#"+((int)Math.Round(c.X*255)).ToString("X2")+((int)Math.Round(c.Y*255)).ToString("X2")+((int)Math.Round(c.Z*255)).ToString("X2");}
        public static void ColorPalette(PrehistoryState state,byte[] bytes,int offset,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(state==null)return;
            var h=state.Society;if(h==null)return;
            var display=HistoricalMapSnapshot.For(state);
            for(int i=0;i<HistoricalCells.Count(state);i++){int owner=display.ColorId(i,overlay);if(owner<0)continue;var c=CivilizationColor(owner);int at=offset+i*4;bytes[at]=(byte)Math.Round(c.X*255);bytes[at+1]=(byte)Math.Round(c.Y*255);bytes[at+2]=(byte)Math.Round(c.Z*255);}
        }
        // Selection/borders identify the court; political tint identifies its ruling
        // culture. Local identity survives conquest and is exposed only in Cultures.
        public static int OverlayColorId(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            int owner=OverlayId(s,cell,overlay);
            return owner>=0&&overlay==HistoryOverlay.States&&s.History!=null?s.History.Polities[owner].CultureId:owner;
        }
        public static int OverlayId(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(s==null)return -1;
            if(overlay==HistoryOverlay.Territories)return s.History==null?-1:HistoricalCells.Polity(s,cell);
            if(overlay==HistoryOverlay.Provinces)return HistoricalProvinceIdentity.Of(s,cell);
            if(s.History?.Aggregates?.Empires!=null)return HistoricalMapSnapshot.For(s).OverlayId(cell,overlay);
            var c=Culture(s,cell);var h=s.History;if(h!=null&&cell>=0&&cell<HistoricalCells.Count(s)){if(overlay==HistoryOverlay.States)return HistoricalCells.Polity(s,cell);if(overlay==HistoryOverlay.Religions)return HistoricalCells.Religion(s,cell);if(c==null&&HistoricalCells.Polity(s,cell)>=0)c=s.Society.Cultures[h.Polities[HistoricalCells.Polity(s,cell)].CultureId];}if(c==null)return -1;
            if(h==null)return c.Stage==SocietyStage.Civilization?c.Id:-1;
            if(overlay==HistoryOverlay.Cultures)return c.Id;
            if(overlay==HistoryOverlay.Religions)return HistoricalCells.Religion(s,cell);
            if(overlay==HistoryOverlay.States)return HistoricalCells.Polity(s,cell);
            if(overlay==HistoryOverlay.Technology)return h.Cultures[c.Id].Technology.Count(t=>t>0);
            int polity=HistoricalCells.Polity(s,cell);return polity>=0?h.Polities[polity].CultureId:-1;
        }
        public static long Population(PrehistoryState s)=>s?.History?.Aggregates?.TotalPopulation??(s==null?0:s.Population.Sum()+(s.Society?.Settlements.Sum(t=>t.Population)??0));
        public static string Stage(SocietyStage s)=>s==SocietyStage.AdvancedCulture?"Advanced culture":s==SocietyStage.EmbryonicCivilization?"Embryonic civilization":s==SocietyStage.Civilization?"Civilization":"Foragers";
        public static CultureIdentity[] Ranked(PrehistoryState s,string sort="Default")=>s?.Society==null?Array.Empty<CultureIdentity>():s.Society.Cultures.OrderByDescending(c=>c.Stage).ThenByDescending(c=>sort=="Population"?c.Population:sort=="Land area"?c.AreaKm2:c.Population+Math.Sqrt(c.AreaKm2)*100).ThenBy(c=>c.Id).ToArray();
public static CultureIdentity? Culture(PrehistoryState? s,int cell)=>s?.Society!=null&&cell>=0&&cell<HistoricalCells.Count(s)&&HistoricalCells.Culture(s,cell)>=0?s.Society.Cultures[HistoricalCells.Culture(s,cell)]:null;
        public static string Describe(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(s==null)return "";
            var c=Culture(s,cell);if(c==null&&s.History!=null&&cell>=0&&cell<HistoricalCells.Count(s)&&HistoricalCells.Polity(s,cell)>=0)c=s.Society.Cultures[s.History.Polities[HistoricalCells.Polity(s,cell)].CultureId];if(c==null)return "";var towns=s.Society.Settlements.Where(t=>t.CultureId==c.Id).ToArray();
            string text=$"{c.Name}\n{Stage(c.Stage)}\n{c.Population:N0} people · {c.AreaKm2:N0} km²\n{c.Subsistence}\nLocal resources: {c.Resources}\n{towns.Length} settlements · {c.Contacts.Length} cultural contacts";
            var town=towns.FirstOrDefault(t=>t.Cell==cell);if(town!=null)text+=$"\n\n{town.Name} · {town.Government}\n{town.Population:N0} residents\nFood reserve: {town.FoodReserve:N0} person-years\nHealth {town.Health/10}% · stability {town.Stability/10}%";
            if(s.History!=null)text=HistoricalDescription(s,cell,overlay,text);
            return text;
        }
    }
}

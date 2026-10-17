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
        // Observer-era colors are a readable categorical key, not political identities.
        static readonly int[] EraColors={0xa6adb5,0xd2c488,0xc78c4a,0x938aa9,0x6384b9,0x3ca8a4,0xd4b347,0xd36d58,0x9d65bb};
        static V EraColor(int era){int rgb=EraColors[Math.Max(0,Math.Min(EraColors.Length-1,era-2))];return new V((rgb>>16)/255.0,((rgb>>8)&255)/255.0,(rgb&255)/255.0);}
        public static V OverlayColor(PrehistoryState state,int id,HistoryOverlay overlay)=>overlay==HistoryOverlay.Technology&&state.History?.Aggregates?.Empires!=null?EraColor(id):CivilizationColor(id);
        public const string PoliticalClaimLegend="Colors identify states; recognized claims share their holder's color.";
        public const string PoliticalClaimShortLegend="Colors: individual states";
        public const string PoliticalClaimNote="Recognized claim only; no local state administration is recorded here.";
        // Bounded click/inspection query: do not build the cell-wide map snapshot.
        public static bool ClaimOnly(PrehistoryState state,int cell)
        {
            if(state?.History==null||cell<0||cell>=HistoricalCells.Count(state))return false;
            int actual=HistoricalCells.Polity(state,cell);
            if(actual>=0&&HistoricalImperialHierarchy.Sovereign(state,actual)>=0)return false;
            int holder=HistoricalRecognizedClaims.Holder(state,cell);
            return holder>=0&&HistoricalImperialHierarchy.Sovereign(state,holder)>=0;
        }
        // Claims retain their holder's color. Inspection still distinguishes control from title.
        public static V OverlayColor(PrehistoryState state,int id,HistoryOverlay overlay,bool claimOnly)
            =>OverlayColor(state,id,overlay);
        public static V LegendColor(HistoryLegendEntry entry)=>entry.EraColor?EraColor(entry.ColorId):CivilizationColor(entry.ColorId);
        public static string LegendColorHex(HistoryLegendEntry entry){var c=LegendColor(entry);return "#"+((int)Math.Round(c.X*255)).ToString("X2")+((int)Math.Round(c.Y*255)).ToString("X2")+((int)Math.Round(c.Z*255)).ToString("X2");}
        public static void ColorPalette(PrehistoryState state,byte[] bytes,int offset,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(state==null)return;
            var h=state.Society;if(h==null)return;
            var display=HistoricalMapSnapshot.For(state);
            for(int i=0;i<HistoricalCells.Count(state);i++){int owner=display.ColorId(i,overlay);if(owner<0)continue;var c=OverlayColor(state,owner,overlay,display.ClaimOnly(i));int at=offset+i*4;bytes[at]=(byte)Math.Round(c.X*255);bytes[at+1]=(byte)Math.Round(c.Y*255);bytes[at+2]=(byte)Math.Round(c.Z*255);}
        }
        // States identify sovereign political actors, independently of culture.
        public static int OverlayColorId(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            int owner=OverlayId(s,cell,overlay);
            return owner;
        }
        // Selection identity is not a palette index: unrelated peoples can share
        // a technology-era tint. -2-culture identifies a pre-state gray culture;
        // -1 alone means no subject. This read never builds a world-sized export.
        public static int SelectionId(PrehistoryState? s,int cell,HistoryOverlay overlay)
        {
            if(s?.Society==null||s.History==null||cell<0||cell>=HistoricalCells.Count(s))return -1;
            int p=HistoricalCells.Polity(s,cell),culture=HistoricalCells.Culture(s,cell);
            if(p>=0&&s.History.Polities[p].EndedYear>=0)p=-1;
            if(overlay==HistoryOverlay.Provinces)return HistoricalProvinceIdentity.Of(s,cell);
            if(overlay==HistoryOverlay.Religions)return HistoricalCells.Religion(s,cell);
            if(overlay==HistoryOverlay.ReligiousInstitutions)return HistoricalMapSnapshot.ReligiousInstitutionIdentity(s,cell);
            if(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations)
            {
                int holder=p>=0?p:HistoricalRecognizedClaims.Holder(s,cell);
                int sovereign=holder>=0?HistoricalImperialHierarchy.Sovereign(s,holder):-1;
                if(sovereign>=0)return overlay==HistoryOverlay.States?sovereign:HistoricalRecognizedClaims.Civilization(s,s.History.Polities[sovereign].CultureId);
            }
            if(overlay==HistoryOverlay.Territories&&p>=0)return p;
            if(overlay==HistoryOverlay.Cultures||overlay==HistoryOverlay.Technology)
                return culture>=0?culture:p>=0?s.History.Polities[p].CultureId:-1;
            return culture>=0?-2-culture:-1;
        }
        public static bool PopulationFocus(HistoryOverlay overlay)=>overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Cultures||overlay==HistoryOverlay.Territories;
        public static int OverlayId(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(s==null)return -1;
            if(s.History!=null)return HistoricalMapSnapshot.For(s).OverlayId(cell,overlay);
            var culture=Culture(s,cell);return culture?.Stage==SocietyStage.Civilization?culture.Id:-1;
        }
        public static long Population(PrehistoryState s)=>s?.History?.Aggregates?.TotalPopulation??(s==null?0:s.Population.Sum()+(s.Society?.Settlements.Sum(t=>t.Population)??0));
        public static string Stage(SocietyStage s)=>s==SocietyStage.AdvancedCulture?"Advanced culture":s==SocietyStage.EmbryonicCivilization?"Embryonic civilization":s==SocietyStage.Civilization?"Civilization":"Foragers";
        // Regional traditions remain advanced cultures even when a local center has matured.
        public static string LocalStage(PrehistoryState s,int cell,SocietyStage fallback)=>
            s.History?.RegionalTraditions!=null?Stage((SocietyStage)HistoricalCells.Stage(s,cell)):Stage(fallback);
        public static CultureIdentity[] Ranked(PrehistoryState s,string sort="Default")=>s?.Society==null?Array.Empty<CultureIdentity>():s.Society.Cultures.OrderByDescending(c=>c.Stage).ThenByDescending(c=>sort=="Population"?c.Population:sort=="Land area"?c.AreaKm2:c.Population+Math.Sqrt(c.AreaKm2)*100).ThenBy(c=>c.Id).ToArray();
public static CultureIdentity? Culture(PrehistoryState? s,int cell)=>s?.Society!=null&&cell>=0&&cell<HistoricalCells.Count(s)&&HistoricalCells.Culture(s,cell)>=0?s.Society.Cultures[HistoricalCells.Culture(s,cell)]:null;
        public static string Describe(PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            if(s==null)return "";
            var c=Culture(s,cell);if(c==null&&s.History!=null&&cell>=0&&cell<HistoricalCells.Count(s)&&HistoricalCells.Polity(s,cell)>=0)c=s.Society.Cultures[s.History.Polities[HistoricalCells.Polity(s,cell)].CultureId];if(c==null)return s.History!=null?HistoricalDescription(s,cell,overlay,""):"";var towns=s.Society.Settlements.Where(t=>t.CultureId==c.Id).ToArray();
            string text=$"{c.Name}\n{LocalStage(s,cell,c.Stage)}\n{c.Population:N0} people · {c.AreaKm2:N0} km²\n{c.Subsistence}\nLocal resources: {c.Resources}\n{towns.Length} settlements · {c.Contacts.Length} cultural contacts";
            var town=towns.FirstOrDefault(t=>t.Cell==cell);if(town!=null)text+=$"\n\n{town.Name} · {town.Government}\n{town.Population:N0} residents\nFood reserve: {town.FoodReserve:N0} person-years\nHealth {town.Health/10}% · stability {town.Stability/10}%";
            if(s.History!=null)text=HistoricalDescription(s,cell,overlay,text);
            return text;
        }
    }
}

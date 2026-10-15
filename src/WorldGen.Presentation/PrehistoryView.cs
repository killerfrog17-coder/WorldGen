#nullable disable
using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class PrehistoryView
    {
        // Alpha codes are data, not transparency: water 0, land85, occupied170, selected255.
        public static byte[] Codes(World w,Prehistory engine=null,PrehistoryState state=null,int selectedCell=-1,HistoryOverlay overlay=HistoryOverlay.Civilizations,byte[] destination=null,bool optimized=true)
        {
            var result=destination??new byte[w.Surface.Cells.Length];if(result.Length!=w.Surface.Cells.Length)throw new ArgumentException("Overlay buffer must match the surface.",nameof(destination));var clusters=engine!=null&&state!=null?engine.Clusters(state):null;
            var display=optimized&&engine!=null&&state!=null?HistoricalMapSnapshot.For(state):null;
            int selected=selectedCell>=0&&selectedCell<result.Length&&engine!=null?engine.CellPatch[selectedCell]:-1;
            int cluster=selected>=0&&clusters!=null?clusters[selected]:-1;var selectedCulture=SocietyView.Culture(state,selectedCell);
            for(int i=0;i<result.Length;i++)
            {
                bool water=w.Hydrology.OceanConnected[i]||w.Hydrology.LakeCoverageFraction.Length>i&&w.Hydrology.LakeCoverageFraction[i]>=.9;
                result[i]=(byte)(water?0:85);if(engine==null||state==null)continue;
                int patch=engine.CellPatch[i];if(patch<0)continue;
                if(!state.Started){if(patch==state.Origin)result[i]=(byte)(water?65:255);}
                else if(state.Population[patch]>0||state.History?.Aggregates!=null&&((state.History.Aggregates.Living?.Regions[patch].SettledPopulation??0)>0||(display!=null?display.RemotePopulation(i):RemotePopulation(state,i))>0))
                {
                    int local=display!=null?display.Culture(i):HistoricalCells.Culture(state,i);var culture=local>=0?state.Society.Cultures[local]:null;bool chosen=selectedCulture!=null?culture?.Id==selectedCulture.Id:culture==null&&cluster>=0&&clusters[patch]==cluster;
                    result[i]=(byte)(chosen?(water?65:culture?.Stage==SocietyStage.Civilization?245:255):water?42:culture==null?170:195+20*((int)culture.Stage-1));
                }
            }
            if(engine!=null&&state?.History!=null&&state.Started){int selection=display!=null?display.OverlayId(selectedCell,overlay):SocietyView.OverlayId(state,selectedCell,overlay);for(int i=0;i<result.Length;i++){if(engine.CellPatch[i]<0&&(display!=null?display.Polity(i):HistoricalCells.Polity(state,i))<0)continue;bool island=w.Hydrology.OceanConnected[i];int id=display!=null?display.OverlayId(i,overlay):SocietyView.OverlayId(state,i,overlay);if(id>=0)result[i]=(byte)(island?(selection>=0&&id==selection?60:50):selection>=0&&id==selection?245:235);else if(!island&&(display!=null?display.Culture(i)>=0:SocietyView.Culture(state,i)!=null))result[i]=(byte)((display!=null?display.Stage(i):HistoricalCells.Stage(state,i))>=2?215:195);}}
            return result;
        }
        static long RemotePopulation(PrehistoryState state,int cell)
        {var a=state.History.Aggregates;int raw=a.Cells[cell*5+1];return raw>=0?a.Polities[a.PopulationCohort(raw)].Remote:0;}
        public static string Lifestyle(ForagerLifestyle style)=>style+" foragers";
        public static string Status(Prehistory engine,PrehistoryState state)=>state==null?"Preparing human habitat…":!state.Started?"Homo sapiens · "+engine.PatchName(state.Origin):
            $"{SimulationClockView.Year(state,state.Years)} · {SocietyView.Population(state):N0} people · {SocietyView.Stage(state.Society?.Phase??SocietyStage.Foragers)}";
        public static string Describe(Prehistory engine,PrehistoryState state,int cell,HistoryOverlay overlay=HistoryOverlay.Civilizations)
        {
            string society=SocietyView.Describe(state,cell,overlay);if(society.Length>0)return society;
            if(engine==null||state==null||cell<0||cell>=engine.CellPatch.Length)return "Select a human population";
            int p=engine.CellPatch[cell];if(p<0)return "Uninhabited water";
            var patch=engine.Patches[p];string name=engine.PatchName(p);
            if(!state.Started)return name+(p==state.Origin?" · Homo sapiens starting region":" · click Move here to change the starting region");
            if(state.Population[p]==0)return name+" · uninhabited";
            var clusters=engine.Clusters(state);long total=0;int count=0;for(int i=0;i<clusters.Length;i++)if(clusters[i]==clusters[p]){total+=state.Population[i];count++;}
            return $"{name} · {Lifestyle(patch.Lifestyle)} · {state.Population[p]:N0} people here · connected lifestyle group: {count} patches, {total:N0} people";
        }
    }
}

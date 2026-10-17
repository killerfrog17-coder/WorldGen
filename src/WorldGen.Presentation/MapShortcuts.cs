using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class MapShortcuts
    {
        public static readonly HistoryOverlay[] Overlays={HistoryOverlay.States,HistoryOverlay.Civilizations,HistoryOverlay.Cultures,HistoryOverlay.Religions,HistoryOverlay.Technology,HistoryOverlay.Territories,HistoryOverlay.Provinces,HistoryOverlay.Languages};
        public static readonly Layer[] Backgrounds={Layer.Satellite,Layer.Terrain,Layer.Climate,Layer.Heightmap,Layer.Ecoregions,Layer.Microplates,Layer.WaterBodies};
        public const string Help="1 States · 2 Civilizations · 3 Cultures · 4 Religions · 5 Technology · 6 Territories · 7 Provinces · 8 Languages\nShift + 1 Satellite · 2 Terrain · 3 Climate · 4 Height · 5 Ecoregions · 6 Plates · 7 Water\nR Rivers · C City markers\n+/− Societies opacity · Shift +/− Background opacity · 25% steps";
    }
}

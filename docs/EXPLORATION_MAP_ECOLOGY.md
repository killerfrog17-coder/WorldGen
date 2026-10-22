# Exploration map and ecology reference /22 October 2026

This updates the exploration map (M), not the simulation globe's renderer.
Terrain geometry, selection/loading, vegetation appearance/placement rules,
saved ecology/food webs and world/save schemas remain unchanged.

## Map cache, ecology graph and debug HUD follow-up

Map menus show layer names without implementation suffixes. A worker-owned
TerrainMapContext keeps the selected layer's eight most recent views, capped
at 6 MiB RGBA total. Reopening an identical pan/zoom reuses the raster. Switching
layers drops rasters, but retains up to 4,096 footprint-banded, canonical
subpixel terrain samples. Twelve coloured-material patch contexts and 128
height-root contexts are bounded separately. These are transient RAM caches,
not save files. Point inspection is still an unquantized full-detail query.

Height-only layers bypass material/cover blending and patch scaffolding above
128 m footprint, using the existing amplified terrain hierarchy directly.
Coloured layers use map-only patches which skip unused cubic/scaffold arrays;
their geographic patch size follows pixel footprint. Nearby map pixels share
spatial setup. This is not strictly O(1): nearest-cell lookup uses a spatial
index and refinement traverses the terrain hierarchy. A complete new image
still costs work proportional to its pixel count, plus hierarchy preparation.
No map-latency or FPS improvement has been measured under compile-only limits.

Ecology Overview embeds the same FoodWebDiagram and shared FoodWebGui renderer
as simulation World Information. Recorded links are not replaced by generated
plant relationships. The separate full consumer articles remain available.

F3 defaults to FPS, coordinates, facing, elevation/clearance, temperature and
local time/playable calendar date. Shift+F3 switches simplified/full and opens
the overlay if hidden. Movement fields publish at up to 30 Hz; FPS retains its
half-second averaging. Elevation reuses the movement ground sample rather than
querying terrain for the overlay. Full diagnostics publish at 4 Hz only when
full F3 is visible; OnGUI draws cached strings. Wide windows use separated
columns; narrow windows stack panels, with full information clipped/scrollable.

Full F3 also identifies nearby aimed-at generated scatter by species/type.
The 4 Hz broad phase gathers nearby candidates; 30 Hz aiming checks at most
256 cached body envelopes within 80 m. This is approximate envelope selection,
not pixel-perfect leaf picking. Resident terrain grids reject occluded targets;
missing terrain is unknown. At most one new species catalogue resolution per
quarter second is allowed, and 256 identity strings are cached. Species names
resolve against the source-region catalogue/morphology used by placement.
GPU grass blades do not have individual picked identities. A lightweight
WorldGenSpeciesIdentity bridge supports future collider-backed entities; no
entity simulation or mass collider creation was added.

Only source inspection and compilation/package checks are authorized here.
No tests, game/world launches, captures, profiling or visual acceptance.

## Map layers

| Display | Layers | Meaning |
| --- | --- | --- |
| Continuous terrain | Terrain, Satellite, Heightmap, Relief, Final Elevation, Bathymetry | Same local terrain hierarchy as the playable world, sampled at map-pixel footprint. Satellite mixes existing geology/ground cover/mid/forest summaries; terrain and relief use height-derived hillshade. Heightmap stays unlit. |
| Continuous source scalar | Tectonic Elevation, Pre-Erosion Elevation, Erosion Delta | Spherical triangle interpolation of the recorded stage's scalar, not today's terrain noise applied to historical geology. |
| Continuous placement field | Tree Density | Expected generated trees/hectare, including stand noise, blended habitat support, species viability and slope admission. |
| Recorded cells | Climate classification, seasonal temperature/precipitation/wind/currents, continentality; biomes, ecoregions, soils and abiotic ecology proxies; plates/continents/crust, boundaries/stress/mantle; water IDs, watershed, contributing area and river source cells | No fabricated interpolation of source assignments. Nearest-cell colours and point filtering retain discrete regions. The existing optional waterways overlay still estimates sub-cell routes. |

Bathymetry's sampler is supported; the exploration menu retains the existing
visible-layer list plus Tree Density. Existing hidden diagnostic layers are not
all newly exposed. No numerical layer IDs are shifted; TreeDensity is appended.

Wide continuous maps use128×64 samples, increasing to192×96,256×128 and384×192
with zoom. Sampling is cancellable/background, never OnGUI. Up to32 bounded
geographical patches are retained per map job. Display-only SampleMap stops
refinement at its physical pixel footprint, suppresses unresolved channel work
above128m and the sub-metre beauty pass above8m. Lakes/oceans retain hierarchy
ownership/levels. This is a coarse preview, not pixel-exact full-detail geometry
at every world-map zoom; zooming reveals additional real hierarchy detail.

Normal Sample uses spacing0, retaining its original refinement, riverscape and
surface-detail calls. No new terrain algorithm version or generation route is
introduced. Historical source-field interpolation is a read-only display query.

Tree density shares the exact .034/m² candidate rate, habitat/slope admission and
species/colony weight helpers with LocalGeneratedTrees. Random streams, draw
order, thresholds and placement definitions are unchanged. The map is expected
ecological density, not a census of random instances or a graphics-setting draw
count. Overview slope/height are footprint estimates; point inspection uses the
same1.5m four-neighbour slope as tree placement, full-detail height and species
viability. No new painted forest mask is substituted for generation.

## Navigation and inspection

- White crosshair is the persistent selection. Gold square/“You” is the player.
- Click selects and starts an asynchronous layer-specific inspection; it never
  teleports. Arrow keys move selection. Drag pans; wheel zooms.
- P or Teleport moves to the selection explicitly. Player centres the viewport;
  Whole world resets view without changing the selection.
- Continuous terrain readouts query full detail, including actual height/water;
  historical maps report their interpolated stage. Seasonal climate and other
  cell layers report the exact recorded cell value, with that provenance stated.
- Readouts use the session's official meridian, matching F3/teleport commands.
  Stale/cancelled selection jobs cannot replace newer selections.

## Ecology (hold F3, press E)

The independent panel captures the current region; Refresh current location
recaptures it. Escape/chord/× closes. Gameplay movement/mouse-look and chat are
blocked while browsing; cursor is free. Region assembly is asynchronous; GUI
draws cached data, with virtualised article lists and cached text heights.

Categories cover recorded fauna, recorded producers/basal resources, generated
ground cover, flowers/herbs, fungi, mid-height plants, trees and food web.
Search examines titles and complete article bodies within the active category.
The overview includes habitat, soil, region/province/area/patch and neighbour data.

Recorded taxa include all available public taxon, regional presence/variant,
lineage/source/ecotype and domestication traits, plus incoming/outgoing feeding
opportunities. Generated plants include all their public fields and computed
anatomy/reproduction/motion metadata. Existing link.xml already preserves these
assemblies for reflection under IL2CPP. Unqualified fields can be dimensionless
or provisional indices; records are not a scientific calibration.

Food web groups EVERY recorded regional link by consumer, not a sampled subset;
full resource names, resource class and overlap are retained. Derived procedural
plants remain explicitly separate: no new saved feeding links, populations,
dynamic phenology, harvesting or toxicology rules are fabricated. Relative plant
hazard indices do not establish doses, edibility or animal-specific tolerance.

## Verification limits

Owner compile/package-only direction remains: no test suites, game launches,
world loads, captures or profiling. Shared projects compile; initial malformed
new meta GUID caught by Unity import and corrected before packaging. Existing
map fixture sources now require selection followed by explicit teleport; they
are updated, NOT executed. Raster quality/latency, input, article layout and
actual-world continuity/performance require owner review.

Release IL2CPP compile/package exits0, final log free of C#/shader/GUID errors;
BuiltUtc22Oct05:06:28Z. Shortcut selects separate real C:
`artifacts/player-continuous-map-ecology/WorldGen.exe`. Owner's running game,
world saves and preferences remain untouched.

# Exploration map and ecology reference /22 October 2026

This updates the exploration map (M), not the simulation globe's renderer.
Terrain geometry, selection rules, vegetation appearance/placement rules,
saved ecology/food webs and world/save schemas remain unchanged.

Stutter follow-up: the Windows player now streams progressive map tiles from its
existing external terrain worker, keeping allocation-heavy sampling outside
Unity's collector. The same four-worker/cache limits and exact tile pixels are
retained; non-Windows/missing-worker fallback still runs in process. Map scratch
buffers and distant material queries also avoid temporary clones. See
[timing investigation and limits](TERRAIN_MAP_STUTTER.md). `/seed` prints the
current source recipe seed without changing the world.

## Progressive map and transparent corner HUD revision

The exploration runtime no longer calls the fixed whole-image raster path.
Opening, panning or zooming starts a cancellable ProgressiveTerrainMap request:

1. A cheap 16×8 source-scale preview is published first (adaptive 4–32 pixels
   wide thereafter, targeting 150 ms computation). This is explicitly a preview,
   not a claim that full-detail terrain has already been sampled. It uses saved
   scalar interpolation/mean cover; cell layers use their exact source colours.
2. Actual terrain tiles refine the view centre-out, through doubling resolutions
   starting at 32 pixels wide. Each finished tile is published immediately; no
   whole-image completion barrier before displaying its results. A complete pass
   precedes the next resolution so the whole view gains coverage.
3. Refinement reaches the next power of two covering the map's displayed width,
   bounded only by the GPU's texture-size limit. No old 384-pixel/zoom-tier cap.
   Pixel footprints still filter unresolved terrain detail. Zooming further
   requests new, finer physical sampling; idle work does not increase resolution
   indefinitely beyond the display's useful pixel density.

Up to four background workers each own independent sampling contexts. Tile
batches adapt between 4×4 and 32×32 from measured computation cost. Sixteen queued
updates provide backpressure; Unity consumes at most twelve updates per frame.
A one-pixel halo supplies matching cross-tile height gradients. Old complete
coverage is upscaled on each new pass; partial tiles replace regions by GPU copy,
not a full texture re-upload. The old view remains reprojected during navigation
until the new source preview arrives. Stale revisions are rejected; closing the
map cancels refinement. Diagnostic captures explicitly read the GPU surface;
normal rendering has no GPU readback.

The small tile LRU retains up to 16 MiB RGBA / 4,096 tiles across eight recent
view keys. Only one full selected cell-colour palette is retained separately.
Each worker retains the earlier bounded terrain sample/context caches. These
are transient RAM, not saves/disk. GlobeView.CellValues can now colour selected
cells without constructing a full-world palette for the first preview. Boundary
lookup arrays are no longer constructed for layers that never use them.

While the map is open, new terrain/prediction/reanchor/scatter requests and tree
bake/forest-field updates wait. Existing jobs finish and can install, weather,
coverage and the visible world continue. Closing the map resumes normal work.
This changes scheduling priority only, not terrain/vegetation output or distances.

Full F3 now uses transparent corner sections: position/ground top-left,
vegetation top-right, weather/sky bottom-left, terrain/system bottom-right.
Thin horizontal rules separate headings; right-hand text is right-aligned.
No solid panels, central boxes or debug scroll views. Most content stays at the
top. Text scales from 14 to 10 px if needed, then clips within disjoint corner
rectangles on cramped windows rather than overlapping. Simplified F3 and the
30 Hz / 4 Hz refresh split remain. No terrain queries occur in HUD drawing.

The half-second first-image target is a scheduling/adaptation goal, NOT a
measured host guarantee. Owner restrictions permit compilation/package only:
no agent player launches, tests, captures, world loads or profiling. GPU region
copy, actual loading latency, sharpness and HUD appearance await owner review.

## Map cache, ecology graph and debug HUD follow-up

The following describes the preceding checkpoint. Progressive runtime maps and
the transparent corner HUD above supersede its whole-raster/panel presentation.

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

The preceding fixed-raster checkpoint used128×64 samples, increasing to192×96,256×128 and384×192
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

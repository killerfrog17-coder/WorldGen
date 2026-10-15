# Shared settings - version 3

**Lake amount (%)** and **Mountain strength (%)** now share Terrain sculpting; there is no separate Hydrology section. Both default to100 and allow0–100,000%, including exact0.0001% numeric input. Their sliders use logarithmic travel to make this wide range useful. Zero disables lakes or the scaled major-range uplift contributions respectively; smaller background relief can remain. Mountain strength affects procedural continental ranges, not measured Earth. Lake amount changes retention/delivery/loss, not a literal percentage of lake area.100% preserves the accepted baseline. Regenerate to apply; settings persist and participate in hashes. Seasonal diagnostics use northern-calendar quarter temperatures and rainfall totals.

Both faces enumerate `Recipe.Settings`. Detail and Irregularity control the actual terrain mesh and require regeneration. The separate plate/continent base remains exactly 20,000 cells.

| Setting | Default | Range |
|---|---:|---:|
| Seed | 73421 | 0–16,777,215 |
| Detail | 204,000 | 5,000–2,560,000 |
| Irregularity | .75 | 0–1 |
| Plates | 80 | 4–500 (reference UI ends at 120) |
| Continents | 4 | 1–10, no more than plates |
| Interconnectedness | 1 | Integer 1–50; exact number of candidate assignments |
| Roughness | .40 | 0–.5 |
| Continent size variety | .35 | 0–1 |
| Land coverage | .30 | 0–1; whole-plate allocation is approximate |
| Terrain warp | .75 | 0–1 |
| Smoothing | .10 | 0–1 |
| Glacial erosion | .50 | 0–1 |
| Hydraulic erosion | .50 | 0–1 |
| Thermal erosion | .10 | 0–1 |
| Ridge sharpening | .50 | 0–1 |
| Mountain strength (%) | 100 | 0–100,000; step0.0001 |
| Lake amount (%) | 100 | 0–100,000; step0.0001 |
| Temperature offset | 0 °C | −15–15 |
| Precipitation offset | 0 | −1–1 |

World types and their allocation constraints are deferred. Only continent count and the Interconnectedness search budget control this selection, alongside the existing land coverage and size variety settings.

The former Hydrology controls and sea-level preview remain removed. Persistent drainage is generated automatically from terrain and four-season climate; Show rivers is display-only. Water/lake/river diagnostics expose the generated state. From 3.19, whole-cell lake display requires at least 90% estimated coverage; smaller lakes remain stored and inspectable with fractional area/storage, without becoming blue tiles. Custom Map2 controls remain deferred. Earth is a built-in starting recipe (`TerrainSource = "Earth"`), not a world-type constraint preset; measured relief bypasses procedural sculpting. World save/load preserves snapshots, while recipe save/load preserves generation inputs.

Interconnectedness evaluates exactly the selected number of deterministic land/sea plate assignments and keeps the highest-scoring candidate. Its Earthlikeness score rewards narrow or near connections while penalizing broad shared borders. It does not sculpt bridges or guarantee final coastline topology. More iterations extend the same candidate sequence; they cannot lower the selected score. One iteration preserves the pre-redesign default allocation path. Complex count, composition, membership and the complex layer are removed. Continent identity and final connected landmass remain distinct. Current recipes require worldgen-3.22.0.

Orogen defaults uses the table above. Preview uses 20,000 terrain cells. Ocean world, Continental, Glacial and 500 plates alter explicit recipe values. The 500-plate preset remains a stress-test extension; the reference UI ends at 120 plates.

Both faces expose Globe/Map, Terrain/Satellite/Climate/Heightmap, wireframe, Show plates, and grid spacing Off/15/30 degrees (30 default). Show plates adds borders in every layer and plate colors only in Terrain. Diagnostic layers remain available. Center longitude changes map translation without regenerating geometry. View controls are outside the recipe; they cannot change terrain hashes.

Unity: drag to orbit/pan, scroll for exponential zoom, F11 or Fullscreen for borderless fullscreen. Globe pitch stops at +/-89 degrees, with no roll. The window is resizable and the sidebar divider is draggable. Browser has the same view controls and a draggable sidebar on desktop.

Generation and presentation remain cancellable. Recipe save/load, full result, cell CSV and heightmap exports are shared adapters. Schema-1/2 recipes are rejected explicitly; they are never silently reinterpreted as v3.

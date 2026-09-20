# Shared settings - version 3

Both faces enumerate `Recipe.Settings`. Detail and Irregularity control the actual terrain mesh and require regeneration. The separate plate/continent base remains exactly 20,000 cells.

| Setting | Default | Range |
|---|---:|---:|
| Seed | 73421 | 0–16,777,215 |
| Detail | 204,000 | 5,000–2,560,000 |
| Irregularity | .75 | 0–1 |
| Plates | 80 | 4–500 (reference UI ends at 120) |
| Continents | 4 | 1–10, no more than plates |
| Roughness | .40 | 0–.5 |
| Continent size variety | .35 | 0–1 |
| Land coverage | .30 | 0–1 |
| Terrain warp | .75 | 0–1 |
| Smoothing | .10 | 0–1 |
| Glacial erosion | .50 | 0–1 |
| Hydraulic erosion | .50 | 0–1 |
| Thermal erosion | .10 | 0–1 |
| Ridge sharpening | .50 | 0–1 |
| Temperature offset | 0 °C | −15–15 |
| Precipitation offset | 0 | −1–1 |

The former Hydrology controls and sea-level preview have been removed. Persistent lake/river generation and custom Map2 controls are deferred.

Orogen defaults uses the table above. Preview uses 20,000 terrain cells. Ocean world, Continental, Glacial and 500 plates alter explicit recipe values. The 500-plate preset remains a stress-test extension; the reference UI ends at 120 plates.

Both faces expose Globe/Map, Terrain/Satellite/Climate/Heightmap, wireframe, Show plates, and grid spacing Off/15/30 degrees (30 default). Show plates adds borders in every layer and plate colors only in Terrain. Diagnostic layers remain available. Center longitude changes map translation without regenerating geometry. View controls are outside the recipe; they cannot change terrain hashes.

Unity: drag to orbit/pan, scroll for exponential zoom, F11 or Fullscreen for borderless fullscreen. Globe pitch stops at +/-89 degrees, with no roll. The window is resizable and the sidebar divider is draggable. Browser has the same view controls and a draggable sidebar on desktop.

Generation and presentation remain cancellable. Recipe save/load, full result, cell CSV and heightmap exports are shared adapters. Schema-1/2 recipes are rejected explicitly; they are never silently reinterpreted as v3.

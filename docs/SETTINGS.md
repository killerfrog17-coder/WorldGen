# Shared settings - version 3

## Unified vegetation presets /22October2026 UTC

This supersedes the metre-slider UI documented below. Grass debug and Vegetation
are now one Vegetation tab: overall five-level quality, optional separate grass /
small-plant / mid-height / tree quality, individual layer switches, horizon forest
and wind switches. Levels are Very low, Low, Medium, High, Ultra. Advanced A-B
controls (density, rebuilt/mesh-only, culling, shadows, direction-only wind) remain
collapsed by default. No raw LOD-distance sliders or separate grass tab remain.

Medium is calibrated to the owner's saved20/50/300m trees,20/40/500m mid-height,
5/100/20m herbs/fungi. Grass was session-only, so Medium uses the previously
reported30m close /300m total, density1, detail2 rather than claiming to recover
unsaved controls. New quality selections persist in vegetation-quality-v2-*;
old layer/density/shadow preferences survive. No registry/preferences are modified
by agent packaging; migration is read-only on load until the owner changes a setting.

| Preset | Grass close/total (m) | Grass density/detail | Herbs near/range; fungi (m) | Mid near/far/range (m) | Trees near/far; variant sharing (m) |
|---|---|---|---|---|---|
| Very low |12/120|.60/0|3/45;12|10/22/240|8/24;144|
| Low |18/210|.80/1|4/70;16|14/30/360|12/35;210|
| Medium |30/300|1/2|5/100;20|20/40/500|20/50;300|
| High |36/350|1.10/2|7/120;28|25/55/650|26/70;420|
| Ultra |42/400|1.20/3|9/140;35|30/70/800|32/90;540|

Tree mesh thresholds retain .72-1.35 crown-size variation and existing distance
crossfades. Quality does not randomly remove trees. The former maximum model draw
cap now controls a gradual transition (70-100% of budget) from exact structural
variant views to a shared same-species view, preserving root/yaw/size/tint. Cheap
individual views continue to1400m, with the shared950-1400m overlap into horizon
stands. Thus300m is NOT a population cutoff or a claim that full meshes draw there.
Mesh-only/legacy A-B modes retain a true local draw cap for diagnostic safety.

Advanced density still hides only a stable local subset, not the ecological
horizon summary. Quality controls drawing, not placement jobs, art-cache memory
or authoritative ecology. Full near models remain available at every preset;
lower presets switch earlier rather than removing near foliage. Preset values
are restrained budgets, not certified imperceptibility at1080p or FPS guarantees.
See TREE_FLORA.md for retention fixes, current bake budget and remaining limits.

## Historical metre-slider controls

## Local vegetation quality /21October2026

Game settings → Vegetation exposes live, machine-local persistent rendering
controls, separate from the world recipe/save/ecology. Defaults preserve the
preceding tree-coverage-culling renderer. Grass retains its existing debug tab;
the non-grass enable switches are also mirrored there for quick comparisons.

| Layer | Quality ceiling | LOD handoff ends (defaults; adjustable range) | Draw cap (default; range) |
|---|---|---|---|
| Flowers/herbs/ferns/fungi | High / Low |22m;5–60m, blending from12/22 of the chosen end | Herbs140m;10–140m. Fungi65m;5–65m |
| Mid-height plants | High / Medium / Low | High→middle55m;10–100m. Middle→low200m; twice first end through500m |1400m;50–1400m |
| Trees | High / Medium / Low | High→middle120m;20–240m. Middle→low450m; twice first end through1000m |1400m;100–1400m |

Handoffs retain distance-only complementary fades. Middle/far starts are shown
in the UI (mid:28/55 and110/200 of respective ends; trees:1/2 and260/450).
Lower quality caps skip the more expensive model(s), disabling inapplicable
handoff controls. The original three-argument Presentation blend API retains
its default behaviour; the renderer passes the explicit overrides.

Each layer has stable0–100% visible population thinning, default100%, keyed
from existing instance IDs. This does NOT add/remove authoritative plants,
reseed ecology, force regeneration or thin grass. Decreasing then increasing
density reuses the same deterministic subset, not newly randomized colonies.
Trees/mid plants have independent shadow-casting switches; the global scatter
shadow switch overrides both. Herbs/grass still do not cast shadows. Frustum,
hill-occlusion and separate distant-forest switches remain available.
Only mid LOD0 and tree LOD0/1 currently cast shadows, so reducing quality can
already remove the shadow cost even when its layer checkbox remains enabled.

Distances cap existing size-dependent retirement; a small plant may disappear
earlier. These sliders intentionally cannot expand the streamed scatter extent
beyond the preceding default maximum. Density/near draw caps do not alter the
distant ecological canopy/cover summaries: sparse local settings can disagree
with those summaries, and shorter tree ranges can expose a gap before the
existing950–1400m aggregate canopy handoff. This is an explicit performance/
appearance tradeoff, not a new matched far-proxy implementation.

Changes save `vegetation-*` PlayerPrefs, invalidate only render batches and
apply without terrain jobs/recipe changes. Data preparation/catalogue jobs and
cached art remain: these are drawing-cost controls, not generation/memory caps.
Reset non-grass vegetation restores the preceding defaults without changing
grass, terrain, audio or world settings. F3 counts and same-camera settled FPS
are owner diagnostics, not an agent benchmark or promised speedup.

Overview now shows Master followed immediately by Music volume, both mirrored
in Audio. Each has an independent persistent Mute checkbox which retains its
slider value; unmuting restores that level. Music pauses in exploration and
world-simulation pause menus (including their subpages) and resumes the held
track, not a new song. See EXPLORATION_MUSIC.md. New games default sun/moon visual
exaggeration to3×; explicit saved values and legacy fallback remain unchanged.

Only compile/package verification is authorized for this checkpoint; no agent
runtime/UI/audio/FPS tests or player launches. Owner reviews the controls.

## World generation and map controls

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

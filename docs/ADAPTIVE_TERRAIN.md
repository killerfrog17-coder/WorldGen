# Adaptive terrain and specialized settings

Presentation-only continuation of the owner-approved coverage/reuse work.
Local terrain remains Hyper11. No world generation, hydrology, history, saved
player position, terrain noise or vegetation definitions change.

## Selection and quality

- A protected full-detail radius uses the existing finest 1024*scale/64 mesh
  spacing. Radius is 1.5-8 finest-tile widths, displayed in playable metres for
  the current world scale. This is bounded, not arbitrary kilometre-sized fine
  meshes on a tiny world.
- Beyond that radius, a quadtree chooses tile sizes and 16/32/64 segment meshes
  from estimated projected error, not a fixed list of concentric rings. Coarse
  profile residuals, local hill/mountain amplitudes, geographical wavelengths
  and planetary curvature estimate unresolved shape. Nine geographically
  addressed profiles per metadata tile are cached (8192 entry ceiling).
- These are inexpensive estimates, NOT rigorous maximum errors for the canonical
  amplified heightfield. Thin channels/small ridges can escape coarse metadata.
  No full detailed mesh is generated just to choose a LOD, and no metadata is
  written into the world save. Error planning is cancellable background work.
- Low/Medium/High/Ultra/Extreme target estimated 16/8/4/2/1 pixel differences.
  Rendering height is capped at2160 for selection. FOV means camera zoom, NOT
  facing direction: all directions retain coverage. Discrete hierarchy levels,
  separate refinement/coarsening thresholds and existing exact edge stitching
  avoid gratuitous level churn and cracks. This is not a claim that all temporal
  popping or visual/material seams are eliminated; no near-player height morph
  or ground/water adjustment is introduced.
- Detail selection has a refinement safeguard around1800 accumulated leaves;
  this is a soft work ceiling, NOT a strict resident-memory or vertex cap. Fine
  radius work, traversal siblings, active fallbacks and cached products add cost.
  Extreme plus long distance and a wide fine radius can be expensive.

## Loading and coverage

- Nearby ground first, then sparse 8-segment coarse backdrop tiles for quick
  distance coverage, then adaptive detail. Existing resident coverage masks
  suppress coarse surfaces exactly where finer surfaces are ready. Coarse
  products remain until all their replacements are resident.
- Streaming haze reveals only the conservatively covered radius and eases
  outward. Refinement behind an existing ancestor does not retract haze.
  This is additional loading concealment, not a weather simulation change or a
  promise that every fast-travel/child-only coverage configuration is invisible.
- Loading effort is independent of final quality. Low/Balanced/High/Maximum
  permit1/2/3/4 mesh/scatter lanes (also limited by detected CPU count with two
  logical processors reserved where possible), at most1/1/2/4 terrain installs
  per frame, and nominal1.5/3/5/8ms accumulated terrain-install allowances.
  The first atomic upload cannot be preempted: these are scheduling budgets,
  NOT hard worst-frame guarantees. Low spaces terrain requests by25ms.
- The bundled CoreCLR worker has a four-lane hard ceiling; the player controls
  admission. Scatter remains limited to one outstanding patch. Existing
  prediction, cancellation,96MiB/128-product inactive cache and deferred
  destruction remain. Handoff starts with bounded coverage, then recalculates
  adaptive detail for the adopted chart; it never reuses projected old vertices.

## Settings and fullscreen

- Shared Overview, Display, Terrain, Audio and Atmosphere categories are used
  in the main menu, exploration pause and simulation pause. Overview repeats
  master volume, fullscreen/FPS and primary terrain quality/loading choices.
  Audio additionally contains music, footsteps, rain and thunder; weather-audio
  sliders are no longer misplaced under video effects. World-specific scale,
  time and sky configuration remain in World settings.
- New terrain options are machine-local PlayerPrefs, not world/history data.
  Defaults are High/Balanced,200km requested distance and1.5-tile fine radius.
  Existing planetary view-distance caps still apply.
- Fullscreen requests Screen.mainWindowDisplayInfo width/height in borderless
  FullScreenWindow mode instead of retaining the window's old dimensions.
  Startup, the checkbox and F11 share that path; F11 also runs during exploration.
  Leaving fullscreen restores remembered window dimensions. Explicit command-line
  windowed diagnostics do not overwrite the owner's fullscreen preference.

## References and evidence

Screen-error refinement/loading tradeoffs: Cesium Globe maximumScreenSpaceError
and loadingDescendantLimit documentation, https://cesium.com/learn/cesiumjs/ref-doc/Globe.html.
Nested LOD/stitching concepts: GPU Gems2 chapter2,
https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry.
These are design references, not copied implementations or a replacement GPU
clipmap renderer. See VALIDATION.md for actual native evidence and limits.

1298 focused adaptive checks pass (four scales, rough versus flat, five quality
levels, protected near radius, all-direction coverage, error projection,
cancellation, indexed stitching and unchanged actual source). Actual mountain
metadata plans at60km take5.7/5.6/18.1ms CLR for Low/High/Extreme and select
323/689/1904 tiles. These are background-planning diagnostics, not native FPS.
The prior6241 exact reuse and13059 coverage/navigation checks still pass.
Final packaged-worker127639 exact float/index/bounds/cancellation comparisons
and native production-shader coverage/cache checks pass (red/green image inspected).
Scatter probes have zero instances, not new vegetation-placement coverage.

Native IL2CPP03:42:45Z saved-world0.1scale review at1280x800,60km requested range,
High/Balanced: near11.610s including terrain-worker startup, covered-horizon
reveal15.016s, full320-tile requested detail46.395s. Fixed-camera loading interval
38856 frames: median1.03ms, p992.10ms, worst35.01ms, no >100ms frames. Four final
native/CLR probes match exactly; owner snapshot hash is unchanged.

Same binary/saved view/range with diagnostic --worldgen-detail-baseline restores
the preceding fixed planner and fixed fog: near10.907s, full416 tiles36.376s;
worst29.44ms. The reported baseline2.191s "horizon reveal" is only the old fog
already being set to distance with ZERO loaded tiles: it is NOT coverage
readiness and must not be compared to the new coverage-driven reveal time.
The new High detail costs MORE total loading time in this scene, not less.
These are separate runs while the owner's existing game remains open, not an
isolated-machine benchmark or full200km/mountain/travel acceptance.

All five shared pause-settings tabs execute without GUI exceptions, and live
Low/Low changes are observed in F3. Hidden ScreenCapture images are black and
are NOT settings-layout evidence. The actual terrain RenderTexture capture is
inspected but rain/fog-obscured and offers limited distant-quality evidence.
The monitor API reports1920x1080; actual fullscreen switching is left for owner
review, not certified merely by reading the desired monitor dimensions.

During native compilation the historical generated controls4 player was moved
intact to E:/WorldGenBuilds/relocated-player-local-terrain-controls4, with an
original-path junction and verified executable hash, to free C: build space.
No save/source/rollback content was deleted.

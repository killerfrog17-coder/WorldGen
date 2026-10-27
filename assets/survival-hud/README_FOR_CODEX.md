# Unity survival HUD implementation handoff

## Files and source of truth
- `User_Approved_Layout_Original.svg` is the **user-supplied layout reference**. Preserve the geometry and shape positions. Its bar directions and thermal clipping are older behavior, so implement the corrections specified below.
- `HUD_*_Reference.svg` and `.png` are corrected **static visual examples** under multiple game states, not a functioning HUD. Do not simply display a screenshot as the HUD.
- `HUD_Configuration.json` contains the finalized geometry, fade thresholds, palettes, and an example state. It is a handoff configuration, not an existing Unity system.

## Implement in Unity
Create a scalable dynamic HUD using Unity UI (uGUI or UI Toolkit, according to the project), the SVG/vector shapes above, and procedural geometry or shader masks where needed. Preserve the 600 x 600 reference composition as a uniformly scaled anchored HUD group. Expose normalized runtime properties for health, energy, stamina, hunger/fullness, hydration, and body-temperature stress, plus a feels-like temperature in Celsius. Prefer a reusable `SurvivalHudView` component with SetValues(...) and serialized references to the UI graphics and materials. Hook to existing game systems rather than inventing new survival logic.

## Layout — do not rearrange
- Health arc on left, red (#FF3038); energy on right, dark blue (#2946FF), and stamina inside energy on right, cyan (#00E5F4).
- Outer arc centerline radius 231 with 10px stroke (outermost extent = 236 from HUD center). Stamina arc radius 214. Both right and left arcs stop 5 degrees shy of vertical at either end, resulting in roughly 10-degree gaps, including at 100% full.
- The 600x600 SVG center is (300,300). Stomach and droplet have fixed positions and independent nonuniform scaling from `HUD_Configuration.json`. Use the exact stomach path from the supplied SVG, with thin white border, medium grey empty fill, and white fullness fill. No yellow.
- Water uses a blue fill (#249CFF) in the droplet, grey empty area, and pale blue outline. Align its fill to the **actual lowest point of the water-drop shape** so there is no gap under the blue at any nonzero amount.

## Stat direction
ALL five resource indicators must visually drain from the **top**. At 50% value, bottom half is filled, top half empty. At 0%, fully empty; at 100%, fully filled. Right-hand arcs are populated from bottom toward top; left-hand arc is populated from bottom toward top. Mask stomach and water internal fills to their exact silhouettes.

## Temperature symbols
Temperature indicator is centered on HUD center, always six-way symmetrical; only one is visible at a time and it can also be entirely absent:
- Neutral range 19–24 Celsius inclusive: **neither** symbol is shown.
- Below 19: a single connected six-arm snowflake fades in smoothly as temperature falls, becoming fully visible by 8 Celsius. Mild chilly cyan #83DDEB transitions smoothly to stronger blue #258BEB by about -12 Celsius. The snowflake's **inner branch distance is 170** (user explicitly corrected this), outer arm length 200, outer prongs 28, inner prongs 20, line width 10.
- Above 24: a **single** central sun (one circle radius 160 plus six attached radial rays length 40) fades in smoothly, full opacity by 33 Celsius. It starts warm orange #F5A33B and transitions gradually toward hot red #EF4444 by about 47 Celsius. Warm orange remains fully opaque during comfortably warm conditions.
- All rays must be physically connected to their central circle. Snowflake branch prongs must physically connect to their stems. Neither geometry may shift or stretch off-center.
- Smoothstep fade, no direct sun↔snowflake crossfade.

## Body temperature stress glow
Use a soft, center-originating **radial gradient over the full HUD**, with fixed outer radius 236, clipped to the outer edge of the health/energy bars. Use continuous opacity AND spread as stress increases; no hard-edged disk and absolutely no abrupt shader/texture replacement at 100%. The 99% and 100% appearances should be almost identical. Glow tint should follow the cold/heat palette. Temperature symptoms and accumulated thermal stress are *independent inputs*: a sudden environment change changes icon response before accumulated body stress becomes dangerous.

### Suggested GPU shader expression
Let p = saturate(bodyStress / 100), d = saturate(distance(uv, center)/outerRadius); alpha = min(1, 0.99 * pow(p, 0.72) * exp(-13 * pow(1-p, 1.7) * d*d) * (1 - 0.06*d*d)). At p=0, alpha=0. Calculate inside circular radius 236 and hard-mask outside only, never the interior. Adjust alpha in linear vs gamma color space to match references. This is a preview formula, not a physically calibrated heat model.

## Color & fade math
`Smoothstep(x) = t*t*(3-2*t)` with `t=clamp01(x)`.
- Snow opacity = smoothstep((19-tempC)/11) for tempC < 19, else zero.
- Sun opacity = smoothstep((tempC-24)/9) for tempC > 24, else zero.
- Cold color blend factor = smoothstep((8-tempC)/20).
- Hot color blend factor = smoothstep((tempC-33)/14).
- Interpolate between the specified colors using a well-defined RGB blending space.

## Acceptance tests
1. At 22 C: no sun/snowflake at all. Glow may still be visible if bodyStress > 0.
2. At 13 C: pale cyan snowflake partially faded in; sun invisible.
3. At 8 C: snowflake fully opaque; at -12 C or colder near deep blue.
4. At 31 C: orange sun partially visible; at 33 C sun fully opaque, at 47 C hot red.
5. Health=energy=stamina=100: still 10-degree top and bottom separation between red and blue bars.
6. Each stat at 50: bottom half remains full; water reaches droplet bottom. At 0 no fill. At 100 entire shape filled.
7. With bodyStress=99 then 100, almost no visible jump. At 100 glow reaches to outer perimeter of HUD, clipped at radius 236.
8. Layout and symbol dimensions match the supplied SVG, and changing gameplay values does not alter any hardcoded layout geometry.

## Codex request
Inspect my existing Unity project and implement this HUD in the UI system already in use. Add any necessary shaders, procedural graphics, sprite imports, scene/prefab changes, and a small debug/demo controller to test all values. Avoid changing gameplay mechanics without need. Explain which game state properties need binding. Validate scale, masks, overlapping symbols, arcs, and fill direction in the Unity Game view.

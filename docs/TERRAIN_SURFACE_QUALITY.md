# Distance-budgeted terrain surface shading /22 October 2026

The owner's Terrain lab comparisons isolate a large surface-sampling cost while
lighting, shadows, geometry and vegetation remain. This replacement removes the
old fourteen-potential-layer/triplanar texture path through850m. It does not
change terrain heights, mesh LOD selection, water, ecology, vegetation, or saves.

Terrain now has separate **geometry quality** and **surface quality** controls.
Surface changes are immediate camera-scoped uniforms, not per-tile material
rewrites, terrain rebuilds or vegetation revisions. Existing SRP material layout,
per-tile coverage/transition masks and the304-byte scene buffer are retained.
No per-frame texture generation/upload, shader keyword per quality, or new
per-tile textures are introduced. Settings are machine-local PlayerPrefs;
existing worlds default to Medium surface quality without changing geometry.

| Surface quality | Full-detail radius | Last detail texture | Last ground normal | Last extra layer/projection | Maximum layers / projections |
| --- | ---: | ---: | ---: | ---: | ---: |
| Very low | 3m | 18m | Disabled | 6m | 1 /1 |
| Low | 6m | 40m | 12m | 12m | 1 /1 |
| Medium | 12m | 90m | 25m | 24m | 2 /2 |
| High | 18m | 120m | 35m | 36m | 2 /2 |
| Ultra | 24m | 160m | 50m | 48m | 3 /3 |

Ranges are physical rendered metres, not multiplied by world scale. Extra
material weights and projection axes fade to the dominant choice before their
reads stop. Normal/mask variation fades separately; High/Ultra alone sample fine
roughness/AO/metal masks nearby. Other paths use bounded rough nonmetallic means,
with a smoother ice mean. Outside detail range there is NO ground-material loop,
triplanar ground stack or grass/moss/lichen detail-mat sampling. Ecological
weight/tint grids remain a few independent reads to preserve habitat appearance.

Explicit texture gradients are computed before material/projection selection,
avoiding implicit mip derivatives across neighboring pixels choosing different
layers/axes. Existing textures remain mipmapped128px ground/256px ground mats.
The change reduces the NUMBER of sampling operations; it is not just a smaller
source texture or a mip-bias adjustment. Shader grain runs only within16m.

PBR ground lighting, shadow reception, ambient/night/secondary celestial light,
atmospheric fog and existing ecological leafy lighting remain. Cover overlays
share a fetched main light rather than each fetching their own shadows. An
independent Ground textures toggle provides a permanently lit/shadowed fast
fallback equivalent in purpose to the owner's preferred no-surface-read mode;
it also skips ecological texture grids, using existing vertex cover/palette.

Very low favors speed, including single-axis/layer compromises. Medium targets
ordinary play; Ultra spends more only nearby, not at850m. These are initial
bounded1080p-oriented presets, NOT certification that every scene is visually
indistinguishable or that Ultra cannot be improved. Palette/near blend appearance
intentionally changes; no pixel-identical old-shader claim.

Terrain lab switches remain available against the new path; they no longer
measure the removed850m shader. Its instrumented shader variant is distinct from
normal production rendering, so small cross-variant negative gains still cannot
be treated as isolated feature costs. The protected-radius switch now only caps
the selected surface-quality range, never expands it.

Owner requests quick implementation without deep/heavy testing. This checkpoint
uses compile/package validation only: no agent player launches, profiling or
benchmark campaign. Owner compares appearance/FPS, all five levels, texture-off,
night shadows, cliff projection transitions and distant habitat colours.

Release IL2CPP `player-terrain-surface-budget`, BuiltUtc18:17:24Z, compiles and
packages successfully for the shortcut. No new FPS or visual acceptance is
claimed from compilation; the previous terrain-lab player remains available.

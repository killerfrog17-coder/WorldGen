# Distant ground shading and cached F3 /22 October 2026 UTC

This is rendering/UI work only. Geometry, terrain LOD selection, generation,
water equations, world data and vegetation placement/geometry remain unchanged.
The preceding SRP-compatible material bindings and constant-buffer layout stay.

## Ground shading

The former shader sampled triplanar albedo, normals and material masks before
multiplying fine detail by a fade that reached zero at850m. It now explicitly
branches before that work:

- Below450m the same detailed ground equations remain.
- Between450–850m AO/roughness/metal converge smoothly to per-layer mask means;
  the preceding120–850m albedo/normal fade remains. This transition band adds one
  mean-mask sample per active layer; it is not claimed cheaper at every distance.
- At850m and beyond, skip all ground albedo/normal sampling and fine triplanar
  masks. Use the existing ecological/mineral vertex palette, geometric normal,
  and one coarsest-mip mask sample per contributing layer (instead of nine
  albedo/normal/mask texture sample instructions). Generated arrays have complete
  mip chains; the clamped highest LOD provides the actual layer mean, not a new
  uniform soil colour or hardcoded material roughness.
- Grass/moss/lichen ground-material grain also converges450–850m to its generated
  mean-neutral RGB, then skips those triplanar texture reads entirely. Actual
  grass/plant rendering is untouched. Local cover weights, distant shrub/forest
  summaries, PBR/leafy lighting, shadows and atmospheric shading remain.

Transitions use physical camera distance, not tile boundaries. There are no new
keywords, textures, material updates or shader constant-buffer fields. This is a
lower-sampling far path, not removal of all far-ground lighting/shadow cost.
Visual agreement is intended at unresolved distances, not pixel identity with
the former microscopic pattern. Custom art without mip chains has no certified
mean-mask match; the shipping arrays are generated with mipmaps.

## F3

Update refreshes the whole readout at most4Hz while F3 is open, with an immediate
refresh on opening. Closed F3 does no diagnostic terrain queries. OnGUI simply
draws the stored string, including on repeated Layout/Repaint events. Terrain
clearance is queried once per snapshot rather than twice. Footstep/controller
terrain queries retain their independent gameplay cadence. TestDebugText remains
an explicit on-demand test helper, not the overlay draw path.

## Verification

Source comparison: water/coverage prefix and distant forest/mid/air tail are exact;
OnGUI contains no terrain/material debug queries. Compile/package only for this
checkpoint: no game launch, world load, captures, profiling or FPS acceptance.
Owner tests distant colour/transition continuity and F3 responsiveness.

Release IL2CPP compile/package succeeded, no shader errors, BuiltUtc22Oct04:31:25Z.
Shortcut selects separate `artifacts/player-terrain-cheap-far/WorldGen.exe`;
owner's running previous build, preferences and saves are untouched.

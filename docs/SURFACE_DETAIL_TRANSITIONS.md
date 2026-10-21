# Fine terrain and opaque LOD transitions

Owner-authorized steps6/7. The active derived surface is Hyper12; source world
generation, hydrological graph, history and saved-world bytes are unchanged.
Existing worlds need no regeneration. This is a visual checkpoint, not a claim
of complete terrain realism, new erosion or all-scale/fly-speed acceptance.

## Shared fine height surface

The canonical eight-metre amplified substrate used linear terminal triangles.
The0.1-scale near mesh already samples at1.6m, so more render vertices alone
would repeat its facets. The new evaluator reconstructs a bounded cubic Bezier
height patch using shared endpoint heights and cached tangent gradients. Edge
controls are shared and endpoint-range limited; shape correction is capped at
1.5m. This is continuous C0 reconstruction, not a mathematically C1 interpolant.

Two planet-addressed simplex octaves at27m/8m add small irregularities. Maximum
combined amplitude is about0.31m and normally much less, varying with relief and
sediment. These are physical wavelengths, not cell/tile-size waves. No additional
subdivision level, global fine raster, extra world-preparation pass or saved
heightmap is introduced. Gradients add on-demand CPU work and small cached
vertex fields; rendering and controller queries use the SAME derived evaluator.
The existing finest render spacing is unchanged (1.6m at0.1scale,16m at1scale);
this pass does not promise metre-resolution geometry on a full-size world.

Bare substrate queries used to solve river profiles remain literal Hyper11.
Water levels/gradients are never smoothed or moved to the new ground. Actual
river beds are excluded, and suppression blends across the finite channel
collar using its existing compact kernel. Incision and the ten-to-thirty-metre
vertical shoreline clearance envelope further protect carved terrain and
lake/ocean shores. Wet standing-water geometry therefore remains unchanged.
An initial abrupt incision-threshold gate was replaced by smooth suppression
before publication; it is not the final bank behavior.

## Terrain transitions

- Three-second smoothstep opaque dither crossfades, not transparent terrain or
  moving/morphing heights. A small12m-minimum player safety bubble immediately
  selects fine ground so the player is not visually buried by fallback geometry.
- Existing sparse partial-coverage trees now carry leaf timing references.
  The coarse fragment uses the exact complement of the fine fragment's screen
  threshold. GPU time evaluates the fade; coverage textures do not rebuild every
  animation frame. Nested descendant fade ranges are bounded by ancestors.
- Existing coarse surfaces stay resident through fine arrival; finer children
  fade out when coarsening. Same-key resolution/edge replacements keep one
  previous product. At most32 such previous products are retained, in addition
  to active coverage and the separate96MiB inactive cache. Interrupted same-key
  transitions can finish/restart to enforce that bound; zero popping in every
  churn/fast-flight case is not promised.
- Coarsening does not first fade through a much coarser grandparent. Replacing
  a parent does not suppress already-ready children when its previous product
  still provides coverage. Floating-origin shifts include previous products;
  teleport/chart adoption clears transitions through the normal bounded cache
  retirement path. Hidden chart adoption itself retains its existing immediate
  frame switch rather than attempting to fade different projected coordinate frames.
- No old terrain is faded away when no replacement exists, and initial terrain
  without fallback is solid. Loading haze still conceals uncovered distance.
  The small ordered-dither pattern can be perceptible during a transition;
  this is not TAA/transparency or a promise of invisible geometric differences.

## Scatter transitions

Trees blend near/middle over65-105m and middle/far over280-420m. Rocks/bushes and
other scatter blend around their existing split distances. The last15% of each
kind's draw range fades rather than abruptly cutting off. Newly installed
scatter patches fade in over three seconds. Stable spatial LOD bands work in
both travel directions; batches refresh at most every0.1s when otherwise stable.

The existing instanced draw path carries complementary pixel intervals and
patch appearance time per instance. Both LODs draw only inside transition bands.
Forward, depth and shadow passes use the same clipping/wind convention. This
adds temporary draw/instance work and more frequent batch refreshes, NOT a free
performance improvement. Placement, species, densities and generated assets are
unchanged; this is not step3 distant vegetation support or the grass/tree overhaul.
Materials using the current custom scatter shader support this. Unadapted custom
third-party prefab shaders retain hard LOD selection instead of being double-drawn
incorrectly; future vegetation assets must support the transition property.

## References and validation

Unity's standard approach uses opaque dithering/crossfading for LOD transitions:
https://docs.unity.cn/6000.3/Documentation/Manual/lod/lod-transitions-lod-group.html
and the primary URP implementation:
https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl.
Our manual streamed/instanced renderer supplies its own corresponding timing and
coverage data; this is not automatic LODGroup ownership of the planet renderer.

8874 focused checks cover complementary fade policy, partial/nested timed trees,
all scatter kinds' continuous bounded LOD/range curves,1681 exact overlapping
patch points, restrained actual mountain changes and unchanged world hash.
The actual mountain grid sees178 changed points with maximum0.629m correction;
3362 samples take about1.5s CLR during native compilation, not a native FPS claim.
243 existing water coupling checks,13059 coverage/navigation checks and an
actual saved-world6-lake/1934-edge audit pass. The original saved test world is
now in the owner's recoverable trash; tests read it there without restoring or
changing it. Final native evidence and timing limits are recorded in VALIDATION.md.

Final native IL2CPP21Oct04:51:06Z uses the final bank suppression and nested
transition fixes. GPU half incoming/outgoing images preserve coverage, including
same-key parent replacement and coarsening. Packaged-worker127639 values and8
native/CLR height probes match. Actual0.1x30km mountain/plain near readiness is
8.225/1.160s after main sampler initialization; fixed-camera worst21.95/23.31ms,
no >100ms. Only18/1819 and16/298 distant tiles are resident: no complete-distance
or sustained-travel claim. Images are inspected, but mountain is river-side and
plain is mostly standing water; dry-flatland/finished-art review is left to the
owner. Visible transition grain and existing placeholder/water artifacts are
not hidden by these numerical passes. All owned test processes are stopped.

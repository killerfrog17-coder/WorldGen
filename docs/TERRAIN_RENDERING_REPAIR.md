# Terrain boundary and mouse capture repair

Follow-up: the owner reported the loading fog jumping inward at chunk/LOD changes
and distant silhouettes remaining visible through it. Requested-detail readiness
can legitimately drop to zero when nearby tiles request finer meshes, even while
their old surface remains visible. Applying that instantaneous result as a hard
cap on the existing reveal caused the whole horizon to retreat and expand again.

`TerrainStreamingReveal` now uses readiness only to permit additional outward
reveal. An already revealed distance is retained through terrain/scatter/forest
replans and chart handoffs. Explicit shorter view-distance settings still cap
it, and teleports/new areas reset to 8 m. Readiness remains quality-aware: an
initial coarse backdrop cannot certify a complete horizon. During travel, a late
replacement may become visible through its local LOD transition; it no longer
retracts the entire scene to hide that work. This trades a global fog rewind for
the existing local product transitions, as requested by the owner.

The old air shader already reached full opacity, but its single horizon colour
could differ from the actual sky gradient behind a mountain. Clouds also stopped
at that mountain's depth after the mountain was visually fogged out. A shared
sky-air colour function now supplies the same directional background to sky and
fully fogged surfaces, with matching colour space, observer density and airglow.
The final loading fade spans 72–100% of the reveal distance and returns only
background colour at/beyond the limit. Weather uses reconstructed ray distance
and smoothly treats fog-hidden surfaces as open sky, preventing cloud/ray
occlusion from reintroducing their outlines. Natural aerial perspective and
weather fog remain separate from this loading frontier.

Focused `--streaming-fog-only` checks cover initial quality gating, refinement
and forest-readiness regressions, chart continuity, shorter view distances,
teleport resets, and bounded outward reveal at 15/30/60/144 FPS. All 5,990 pure
checks pass. Release IL2CPP package `artifacts/player-stable-loading-fog`, BuiltUtc
`2026-10-26T20:24:47.4220390Z`, completed with no C#/shader errors. All 127 runtime/
shader source hashes, staged shared DLL hashes, 177 packaged fauna bundles and
177 native animal sources verified unchanged during packaging. The existing
shortcut selects this package. No world fixture, player, capture or profiling
was run; visual opacity, cloud transitions and travel remain owner-review pending.

The owner reported enormous flat surfaces/vertical curtains during forest loading,
plus a mouse pointer escaping to other monitors during first-person movement.

The terrain height sampler was not changed to match trees. Two render-product
faults could expose the wrong apparent surface during loading:

- Area-only readiness counted coarse horizon backdrops as finished nearby land.
  The owner's screenshot reported 58.8 km of ground coverage with only 32 of 560
  planned tiles installed. That figure certified area, not requested mesh detail.
- Every terrain tile extruded opaque perimeter skirts at least 80 metres deep,
  increasing with the tile's height range. Interior coarse fallback boundaries
  could therefore manufacture huge vertical faces. They were render geometry,
  not part of the authoritative walking-height field.

`TerrainQualityFootprint` now checks the resident grid spacing against each
requested leaf. Fine unions and sufficiently dense ancestors still count;
coarse backdrops do not certify fine land. Resolution changes invalidate the
index. The reveal distance remains limited by both terrain and tree readiness,
with an 8 m minimum rather than exposing an unsupported 100 m ring. Tree roots
continue to project onto installed land. No trees change the land's heights.

The regular terrain mesh now emits the sampled surface without perimeter-wall
vertices or triangles. Existing planned edge collapse handles neighboring LODs;
fallback coverage/crossfades remain. Channel refinement and its perimeter audit
were updated to remove the obsolete skirt-index assumptions. The underlying
planet height function, its samples, tree identities and placement are unchanged.
Transient edge gaps/LOD transitions still need visual checking in-game.

Shared scene fog colours are already linear. Terrain, tree, scatter and fauna
paths no longer convert that shared colour a second time; legacy material-only
paths retain their existing conversion. This makes the loading frontier use the
same fog colour across terrain and vegetation.

The owner's Player.log also recorded repeated `Thread group size must be above
zero` failures in distant-forest drawing. Compaction could remove a batch's final
dead roots after its initial nonempty check. A second check now skips that batch
before compute dispatch or indirect drawing.

Mouse capture is reconciled during Update and LateUpdate while the game has
focus. Gameplay locks/hides the cursor; pause, map, chat, ecology overlays and
focus loss release it. Returning focus explicitly re-establishes the native clip.
Movement/look input is ignored while the application lacks focus. Passive
diagnostic modes retain their existing capture bypass.

Validation includes shared compilation and pure quadtree/topology checks:
coarse-only false readiness, resolution replacement, finer unions, partial gaps,
negative coordinates, legitimate dense ancestors, and surface winding/boundaries
at 4–128 segments. No player, world fixture, gameplay capture or profiling was
launched under the existing compile/package-only restriction. Runtime appearance,
edge transitions and actual multi-monitor mouse behavior require owner review.

Release IL2CPP build: `artifacts/player-terrain-seam-cursor`, BuiltUtc
`2026-10-26T19:41:09.4330066Z`. Compile/package completed without C#/shader errors,
and the existing shortcut selects this build. All 177 animal bundles and spawn
commands remain included. Source/DLL and packaged animal hashes were checked.

# Terrain boundary and mouse capture repair

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

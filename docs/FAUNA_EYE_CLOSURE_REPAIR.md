# Animal eye openings in the shipped assets

The owner reported visible holes around animal eyes in the game. This pass
targets the exported assets used by the game, rather than another set of
appearance-only Blender copies.

## Confirmed cause

Twenty-six selected natives replace original orbital faces with eyelid bridges
and buried closed eye globes. Their replacement lid meshes intentionally keep
a constant `UVMap`, with the actual paint driven by another named UV map and
mesh attributes. Constant UVs are legitimate inputs for flat shading and some
attribute-based materials. They are not usable destination UVs for a texture
bake.

The runtime exporter used those constant source UVs as destination UVs. Every
lid triangle collapsed to a single atlas coordinate, received no raster samples,
and sampled an unpainted opacity pixel. The game's fauna shader clips pixels
whose mask green channel is below 0.35. It consequently discarded the entire
replacement eyelid, exposing the cut opening and leaving an isolated eyeball.
Some lashes/whiskers had the same problem. Unpainted zero-opacity atlas gaps also
caused unwanted clipping at UV borders.

This was reproduced from the exact old exported mesh, material atlas and alpha
threshold. In the raccoon and wolf, both lids' UVs collapsed to one point and
every lid vertex sampled alpha zero. Fox, rabbit, horse and buffalo exports
showed the same class of defect. Before/after renders with a bright background
make the lost eyelids and repaired coverage visible.

## Repair

`fauna_runtime_uv.py` separates shading UVs from destination bake UVs. It keeps
the original named and render-active shading maps. A collapsed destination uses
a nondegenerate alternate native map where available, or an independent export
unwrap for flat-material parts. Alternate maps are fitted to their assigned
atlas rectangle. Native geometry, skin weights, shape keys and rig data are
not edited.

The CPU atlas evaluator starts unpainted opacity pixels as opaque and unpainted
normal pixels as neutral, while still evaluating actual source opacity. Thin
valid triangles receive a conservative shading sample when no pixel centre
falls inside them. Padding remains around rasterized islands. The regression
fixture verifies that an opaque constant-UV lid survives and a genuinely
transparent source region remains transparent.

All 177 exports are regenerated under a signature that includes the UV helper.
Their original per-triangle geometry/normal/skin streams and motion payloads
must match the pinned previous exports exactly. Destination UVs and baked
runtime material data change. The approved deer remains unchanged.

A final geometric audit also identified small genuine corner gaps on ten
stitched mammals: hare, rabbit, wolf, badger, jaguar, leopard, lion, ocelot,
puma and tiger. Their canonical selected natives receive a blind interior socket
lining attached to the exact inner lid rim, with the same rim coordinates and
weights. Each gains 192 interior triangles; the original meshes, skin, shape
keys, rig and actions remain exact. The lining does not move or enlarge the
visible eye. The finite repair is recorded in `native-lining-repairs.json` and
the native source pins are updated before export. Its profile flag and shared
`fauna_eye_socket_lining.py` helper preserve the repair on future appearance
rebuilds. Other 167 natives retain their previous file hashes.

The final five-direction ray audit has no remaining central-region misses on
any of the 26 stitched animals. Source-relative silhouette rays are not treated
as holes to fill. All ten added linings' rim seams match exactly through five
phases of every shipped movement clip (maximum normalized gap 0.0). These checks
do not claim arbitrary-angle global watertightness or perfect animal anatomy.

## Delivery and verification

`check-fauna-runtime-eye-repair.py` requires complete current receipts, exact
native identity or explicitly proven append-only lining additions,
original geometry/normal/skin/motion identity, valid lid destination UVs, and no
discarded bilinear opacity samples on source-opaque parts. Numerical checks do
not certify every source head shape or eye position.

`render-fauna-runtime-eye-closure.py` reconstructs the exported game mesh and
atlas, applies the game's 0.35 alpha threshold, and produces paired before/after
side, oblique and upper eye close-ups. It uses CPU rendering. These are exported
asset renders, not screenshots claimed to be from a running player.

`prepare-fauna-repair-bundle-project.py` creates an isolated, asset-only Unity
converter. It uses the original game asset script and its exact `.meta` GUID;
it first proved compatibility by loading existing game bundles. It avoids
compiling unrelated live game source edits. The repaired bundles are loaded
and checked against the generated source assets, including pose and compressed
mask/normal payload identity.

`install-fauna-eye-repair.py` requires successful export and bundle validation,
then updates canonical `Assets/StreamingAssets/fauna` and explicitly selected
existing player data folders. It verifies all installed hashes and leaves player
executables unchanged. It refuses to install while a player or live game project
editor is running. The current launcher target must be checked at installation;
do not assume an earlier player package is still selected. No full game copy
is created by this repair.

Evidence is under ignored `artifacts/fauna-eye-closure-repair`: baseline
identity, regression, export validation, paired renders, bundle validation and
installation receipts. A source checkpoint alone does not back up these binary
game assets. The canonical bundles remain inputs to subsequent main-thread
player builds. Asset-only changes take effect when the game reloads the bundles;
already loaded old assets cannot be retroactively changed in a running player.

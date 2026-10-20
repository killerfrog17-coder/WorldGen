# Water containment: source review and checkpoints

## Bounded paired river checkpoint (Hyper9)

See RIVERSCAPES.md: explicit finite channel/junction meshes replace Hyper8's
unbounded, nearest-reach potential meshing. Shared profiles shape both bed and
water, with horizontal-scale-aware widths. Accepted lake7/ocean hierarchy rules
are restored. Lake dry collars exclude independent river overlays. This is a
playable checkpoint, not completed waterfalls or full hydraulic integration.

## River primitive review (Hyper8)

OWNER-REJECTED for blue walls and excessive flooding. Historical evidence below
must not be treated as realism acceptance.

The current derived river integration is documented in RIVER_PRIMITIVES.md.
Near channel tessellation uses the amplified network; bed envelopes are cut-only,
river datums persist through dry collars and shared render edges, and receiving
standing corners retain their fixed lake/sea level. Terrain-driven water warping
does not return. Existing lake7 and ocean rules remain. This is a first playable
review, not complete waterfalls, hydraulics, scale or all-LOD morphology acceptance.

## Connected basin integration (hyper7)

See LAKE_BASINS.md for the implemented coarse connectivity/shared-refinement
contract, measurements and explicit limits. Lake coverage no longer follows
vanishing inherited IDs. Shared dry divides constrain finer terrain; each basin
retains its own constant datum through its dry shore collar. Lake meshing uses
actual land intersections and the accepted two-metre standing-water underlap.
Uncontained source candidates are rejected, and unverified random hillside
lake spawning is removed. Oceans retain their approved coverage/underlap rules.
Rivers and detailed outlet transitions remain separate and unfinished.

This is NOT the full fine-grid adaptive flood discussed during research. It
uses the existing canonical terrain hierarchy to preserve connected coarse
basins without a large new heightmap. Small fractional-cell lakes, validated
tiny ponds, fine internal subbasin connectivity and complete volume/level
reconciliation are not completed by this checkpoint.

## Ocean shoreline underlap (presentation only)

After owner approval of hyper6's ocean level/coverage, the owner requested a
less exact mesh edge to hide small shoreline gaps. Ocean-adjacent triangles now
extend their water polygon two horizontal metres beneath the dry land instead
of ending exactly at the land/sea intersection. The allowance is calculated
from the triangle's depth gradient, not a fixed vertical flood tolerance that
would extend far across shallow coasts. Ocean footprint tags do not cut this
small overlap before the terrain intersection. Entirely dry triangles still
create no water; no global underground ocean layer is introduced.

Ocean-only presentation lift is zero, replacing the previous2.5cm lifted edge.
The sea stays at its original curved zero-elevation surface. Inland meshing,
lake/river levels, terrain, source/history data and streaming are unchanged.
125 focused checks cover shallow/steep two-metre overlap, unchanged sea height,
dry-triangle rejection and existing no-warp/separate-body cases. No new broad
visual/performance acceptance or inland-hydrology fix is implied.

## Owner-requested unwarped experiment (hyper6)

The owner rejected hyper5 after finding steeply warped water on lakes AND
oceans. Its successful edge-containment checks and selected captures did NOT
establish physically valid water surfaces; that review is superseded.

Hyper6 removes terrain-driven water-height retirement altogether. Existing
cut-only terrain profiles are retained, but they no longer move water. River
levels again come from positions along actual amplified channel reaches, not
blended dry-terrain potentials. Standing lakes take one exact basin datum,
including mixed refinement faces. Source-connected oceans use the hyper4
sea-level rule. The shared-potential rendering bypass is removed: standing
bodies are always rendered independently at their own level and clipped against
drawn terrain; different lake levels never form an interpolated ramp.

This is the owner's isolation experiment, NOT completed river generation or a
new promise that every basin closes. Independent river footprints can again end
above the bed, and disconnected/different-level interfaces may expose seams.
Those must be fixed by appropriate terrain/drainage geometry later, not by
bending water toward the terrain. Scale, tributaries, LOD scheduling, historical
rules and source-world data are not retuned here. Hyper5's broad closure audit
is not used as acceptance for this experiment.

## Library assessment

Owner chose the available open-source reference on20October2026. No commercial
package was purchased/installed and no rendering pipeline was switched.

R.A.M3's public81-page River and Lava3 manual was read, specifically simulation,
terrain and API sections (pages19-21,28,30-31,36-37). It documents runtime river/
lake generation, collider snapping, terrain carving and an API demo. Snapping
water onto arbitrary colliders is NOT evidence that carving modifies arbitrary
streamed mesh height functions. The public API lists TerrainManager.CarveTerrain;
the workflow describes terrain height/splat data. No verified spherical streaming
adapter is supplied by this review. Without a licensed package, source inspection
and an executable compatibility test are not possible. This is a documented
candidate, not a measured integration rejection or claim that integration cannot
be done.

- Product: https://naturemanufacture.com/river-auto-material-3/
- Manual link maintained by vendor: https://naturemanufacture.com/support/
- Crest water-body/level integration: https://docs.crest.waveharmonic.com/Manual/Basics/WaterBodies.html

## Open reference and the integration defect

MIT reference revision b3341c336b2b3070def911fd0c5df196de2bfc64:
https://github.com/Arches-Team/Real-Time-Hyper-Amplification-of-Planets

profiles.comp treats submerged geometry by elevation, uses paired bed/water
attributes and constructs bed/bank profiles. It also raises terrain above the
water potential outside river locations. Our adaptation replaced that bank
enclosure with cut-only profiles AND independent footprint limits; that is not
an equivalent containment guarantee. Reintroducing arbitrary large raised
banks would restore the owner's earlier rejected embankment problem, so the
inland integration remains under review.

A correct lateral water boundary meets terrain at its surface level, joins
another water surface, or has an intentional waterfall. Checking only that
water is above the bed does not establish this.

## Hyper4 ocean-only checkpoint

Previously the final refined face had to retain an Ocean-typed vertex. Ordinary
subdivision terrain could still be2000m below sea level but lose that tag and
therefore lose the ocean. Sea potential now follows the source-connected coarse
sea domain; its actual visible shore is the level-zero/terrain intersection.
Closed inland below-sea controls are not globally reclassified as ocean.

The synthetic island regression fails the previous implementation at -67.1m
on its submerged slope. The correction passes106 focused checks,459 generated/
341 Earth submerged coastal points and unchanged-world hashes. A native generated
shore/shelf review uses actual camera images and completes both492-tile views.
See artifacts/water-library-review/ocean-native for host-local diagnostics.
IL2CPP build16:31:14Z is player-ocean-containment. The owner inspected this
checkpoint and reported no remaining ocean issue; retain it as a rollback.

This checkpoint changes no inland footprints, mountains, loading planner,
controller or historical rules. It does not fix the remaining river/pond
uncontained lateral edges. Those require coupled bank/profile/surface logic,
not another material upgrade or simply hiding the exposed underside.

## Hyper5 inland profile integration

The open reference's key contract is now retained: bed/bank and water are paired
at the same shared subdivision vertices, then interpolated on the same terminal
triangles. Completed-world samples explicitly identify this contract; older
independent-potential fixtures retain the conservative body-aware mesher.
For paired fields the renderer intersects water with its ACTUAL displayed land,
without clipping a second time at a radius or a winning body ID. Wet/dry body
labels no longer choose different water interpolation schemes across a shared
edge, nor can an Ocean label arbitrarily zero a positive inland water field.

An initial reference-style max(terrain, water) bank adaptation was rejected:
actual-world samples raised terrain by up to149m. The adapted collar instead
blends the carved terrain back into its original height, AND retires its water
potential below that dry terrain. Both are continuous vertex fields; there is
no independently truncated water blanket. The bed is cut-only. Standing basins
are deliberately exempt from the river collar: a failed native lake-bank view
caught water ramping from281m to324m over its submerged shore. Inherited lake
vertices now retain the exact supplied lake datum until terrain meets that
level, including the submerged periphery. Two added regression checks reproduce
that datum-retirement failure. This is not a fluid solver or a globally
equilibrated standing-water basin solver; transitions between different basins
and distant linear tessellation remain approximations requiring owner review.

Core evidence:109 focused water checks;200k generated/Earth meshes examine
12232/13848 water vertices and1546/796 genuinely exposed interior shoreline
edges. Those edges meet their displayed terrain within5.5cm, including the
2.5cm presentation lift. Sampled water vertices have zero terrain raising.
Both worlds pass13110 hierarchy/continuity/cache/source/lake/downhill-channel
checks;745 existing controller/LOD checks pass. Generated submerged-sea audit
retains459 generated/340 Earth ocean points after the inland changes. No source/history mutation,
new saved fine map or change to the LOD scheduler/controller is introduced.

Limits: linear coarse LOD can deviate substantially from the canonical water
field (worst sampled27m generated/110m Earth, on distant3277m tiles, not near
tiles);16/152 sampled intersections are outside the exact canonical wet field.
Geometric containment against the drawn mesh is a different claim from exact
hydraulic/LOD accuracy. Dynamic flood levels, physically correct every-lake
planarity, tiny tributary/scale tuning and finished water rendering remain open.
Native shoreline review/publication is recorded separately in VALIDATION.md.

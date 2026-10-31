Revised human appearances, approved by the owner for game integration31Oct.

body-hair-mask.png: built-in image_gen output, exact prompt in generation.json.
Replacement after owner rejected thick branching marks. Fine, unbranched hairs
follow limb-aligned cylindrical UVs (12cm axial repeat), with separate offsets
for left/right limbs and each person. Five shifted alpha-over samples increase
growth density while retaining shaft width, taper and contrast. No old 4x ink
gain, enlarged 2.8x box projection, or 0.8mm hair relief. Separate hair surface
uses 45-micrometre bump distance. Original skin UV is retained and checked
bit-exact between each before/after pair. Per-person density and subtle dirt
remain. No body-hair meshes or simulation; source skin textures are not edited.

recipes.json records six before/after identities and MPFB parameter/target sets.
scripts/build-human-physique-review.py records the reduced chest-bulk preset
(the discarded local pectoral sculpt is NOT used); male copies also exclude
MPFB female breast corrections and have no breast-firmness setting. Beard material/outline
corrections in reproducible code. Extra hair
and full beards use MakeHuman community CC0 packs, downloaded by the isolated
fetch-human-physique-assets.py. Existing source modelling assets stay intact.

Close-up/front/oblique legs, full-body, torso, walk/run renders are made by
scripts/render-human-hair-detail.py before presentation. Review output and audit are under
artifacts/human-physique-review. Open with scripts/open-human-physique-review.py.
Original source SHA0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229
was checked unchanged. Original art, rigs, Legacy clips and Kimodo bank remain.

Runtime export: scripts/export-human-appearance.py, run by Blender in background.
Source: assets-source/humans/appearance-review/Human physique comparison.blend.
Exports: assets-source/humans/appearance-runtime/exports/human-a through human-f.
Unity import: -executeMethod HumanAppearanceBuilder.Build.
New library: Resources/HumanAppearance/Library; original Humans/HumanLibrary
remains the fallback. Meshes are rebound to the original production skeletons,
using their original bind transforms, rather than replacing the animation rig.
Body hair and dirt are baked into4096px albedo; no strand geometry or simulation.
Stable game identity maps to preview E,A,D,B,F,C respectively for game A..F.
This mapping preserves each saved person's sex, face, skin and animation IDs.

Native checks: scripts/run-human-appearance-check.ps1 -Mode Appearance, Kimodo
or Legacy, each with a fresh -OutputName. Build only through unity-check.ps1
-BuildPlayer, in the inactive fixed slot. Final verification/selection status is
recorded in assets/verification/human-appearance-verification.json. Scratch
captures are disposable. Large authoring/runtime assets use game-assets.lock.json
and immutable free GitHub Release packs, not Git or scratch as asset storage.

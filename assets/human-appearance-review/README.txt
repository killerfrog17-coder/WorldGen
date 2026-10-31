Experimental human appearance direction; not imported or selected in the game.

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
was checked unchanged. No player build or production character replacement.

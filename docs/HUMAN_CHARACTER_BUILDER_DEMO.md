# Human character builder appearance audition

Owner requested a small Blender comparison of MPFB and UMA before deciding on
animation or game integration. This is a separate appearance experiment; it does
not modify the game, accepted animal assets or the first-person controller.

## Deliverable

`artifacts/human-builder-demo/Human - Character Builder Comparison.blend`
contains six adults from each builder, with packed image maps. Open with:

```powershell
& E:/AI/WorldGen3D/rig-venv/Scripts/python.exe scripts/open-human-builder-demo.py
```

The Character Builder Comparison panel selects method, person, face/three-quarter/profile/body
view, side-by-side comparison, and a six-person lineup. Keys 1–5 provide the same
controls. Close-up comparisons align eye height; full-body views preserve stature.

The six matching appearance briefs are in `scripts/human_demo_recipes.json`.
Population labels are broad artistic references, not scientific ancestry
classifications. Each brief is one individual, not a claim to represent everyone
in that population. Pigmentation and shape are independently configurable.

## Actual sources

- MPFB 2.0.18, commit `d0a32e57a7f915cb2f2b95410e2117648c7bbb7e`:
  <https://github.com/makehumancommunity/mpfb2>.
  GPL tool; core base mesh, modelling targets, rigs and selected system assets
  are CC0. These are existing parametrically deformed meshes, not AI generations.
- Selected files from the official CC0 MakeHuman system asset pack:
  <https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html>.
  Exact download URL and per-file hashes are retained in ignored
  `mpfb-pack-provenance.json`. The downloader requests only selected ZIP entries
  using byte ranges, rather than retaining the full pack.
- UMA source commit `9e96975fe118e07edb8d0cde0fbcc8db536e8b71`:
  <https://github.com/umasteeringgroup/UMA>. MIT licence retained with the tool.
  Real UMA Human Female/Male 3.0 bodies, native DNA controls, wardrobe assets,
  material colours and generated textures are used.

MPFB keeps its fitted eyes/lids, skin, clothes, hair, modelling shape keys and
53-bone game-engine rig. UMA is generated in the isolated downloaded
`artifacts/tools/UMA/UMAProject`, using the installed Unity 6000.6.0f1. This project
is independent of `unity/WorldGen.Unity`. The exporter uses native UMA generation
and saves the appearance recipe and applied DNA alongside the frozen mesh.

## Scope and limitations

This audition compares existing foundations. It is not an implemented in-game
character creator, an animation acceptance, a genetics simulation or a finished
fantasy species library. No AI image, model or paid service calls are used.

The Blender preview uses ordinary Eevee lighting and Principled materials.
UMA's bespoke Unity skin/hair shaders are approximated; this is not a pixel-exact
Unity rendering comparison. Supplied normal/mask maps remain available in the
export, but packed Unity normals are not incorrectly wired as ordinary RGB
normals. The lash/brow atlas is matched to the selected hair colour. The
redundant second hair rendering pass is omitted in Blender to
avoid drawing the same surface twice with incompatible pass settings. Baked
source normals are preserved. Stock hair and clothing quality varies; the
supplied UMA VeryShort recipe yields bald-looking B/F previews in this export.
MPFB F demonstrates grey hair; UMA F demonstrates grey brows and ageing detail.

UMA appearance meshes in Blender are frozen snapshots, not the final animated
runtime asset. Its source rig and rebuildable recipe remain in the isolated
UMA project/exports. MPFB's rig and loaded modelling shapes remain in the native
file. Animation retargeting is deliberately deferred until the owner chooses a
foundation. No automatic appearance approval is inferred.

## Build and checks

1. `fetch-human-demo-assets.py` retrieves the selected CC0 MPFB asset entries.
2. `build-mpfb-human-demo.py` builds the six MPFB variants and preserves their
   fitted rigs/shape keys without saving global Blender preferences.
3. Copy `HumanBuilderDemoExport.cs` into the isolated UMA project's
   `Assets/HumanDemo/Editor` directory, then run Unity batch mode with
   `-executeMethod HumanBuilderDemoExport.Export`. The downloaded sparse checkout
   supplies UMA Core/Editor, native races/DNA, skin/hair shaders, selected wardrobe
   meshes and their texture dependencies. Its package manifest and sparse paths
   are recorded in the ignored tool manifest. Do not use the game project here.
4. `assemble-human-demo.py` imports actual UMA snapshots, builds common lighting,
   packs maps, saves the comparison, and renders all 12 faces/profiles plus both
   lineups. `check-human-builder-demo.py` checks all 12 saved entries for finite,
   nonempty geometry and packed image maps, records the native SHA, and renders
   paired front/profile/body views. Root visual inspection is separate from the
   technical check.
5. `clean-human-demo-temp.ps1` removes only this experiment's aborted downloads,
   obsolete diagnostic native, old probe, unreferenced texture exports, and the
   isolated UMA project's regenerable Library/Temp caches. It rejects cleanup
   while that project is running and validates absolute paths. It retains both
   tools, selected assets, current native, recipes and export maps.

Generated assets, downloaded tools, diagnostic images and native files remain
under ignored `artifacts`; source checkpoints do not back them up.

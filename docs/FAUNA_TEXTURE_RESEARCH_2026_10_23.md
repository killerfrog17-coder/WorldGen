# Animal material and species-variant plan

23 October 2026. Research and existing-source inspection only. No new generation,
model downloads/installations, texture replacement, Blender launch, game changes
or owner application closure. The owner accepts the original-rig trial and the
new eating correction, but wants a more convincing material on the same mesh.

## Recommendation

### Owner's quality/time shortlist

The owner asks for original-Skyrim-or-better visual quality, not merely valid
files. None of the untested generators has passed that owner review here. The
assessments below are prioritization judgments, not measured quality scores or
promises that every output reaches that standard. TRELLIS.2 painter is NOT tested.
Times marked estimate are deliberately broad warm-run planning budgets on the
4060/8GB, with installed/downloaded weights, excluding installation, reference
creation, retopology, manual fixes and rerolls. Do not repeat them as benchmarks.

| Method | Animal material suitability | Weapons/armour/props suitability | Planning time | Decision |
|---|---|---|---|---|
| TRELLIS.2 native painting | Strong candidate; fur/eyes need actual review | Strong candidate for material-aware surfaces | Several minutes, roughly3-10min texture-only ESTIMATE | Test first on accepted deer |
| MV-Adapter SD2.1 low-memory | Plausible; coordinated views but lower detail than SDXL | Plausible; may need roughness/material finishing | Roughly1-5min texture/bake ESTIMATE | Test second |
| Sequential RealVis/SDXL | Plausible high-detail colour; consistency/lighting still require care | Useful colour/detail painter, needs correct material maps | Roughly4-10min ESTIMATE | Fallback/comparison |
| Pixal3D low-memory | Strong integrated model/material candidate, not proven existing-mesh painter | Strong integrated asset candidate | Roughly6-15min FULL MODEL+TEXTURE ESTIMATE | Consider for new assets |
| Anatomy masks/palette baking | Preserves approved base quality, not a replacement for good base | Excellent for controlled recolours/wear of approved materials | Seconds-scale TARGET, unmeasured | Use for cheap variation |
| Current thirteen independent RealVis views | Owner rejects current final appearance | Not validated; same lighting/consistency risks | 536.88s paint+54.03s delight MEASURED, plus CPU bake | Retire current recipe |
| Hunyuan paint | Technically viable;2.1 PBR worth comparing technically | Technically viable PBR candidate | Older2.0 paint44s MEASURED;2.1 unmeasured | Exclude chosen release route for output-license reasons |

Do not discard a research method as incapable of Skyrim-level quality merely
because it is untested. Paint3D/SyncMVD/FlashTex are deferred for lower practical
priority, not proven visual incapability; no credible4060 timing measured here.
TEXGen is excluded from the immediate8GB shortlist for its official24GB setup,
not a claim that it cannot produce sufficiently good textures. A sword's crisp
edge/engraving and armour construction also depend on geometry/normals: a good
colour map alone cannot guarantee the final asset's quality.

Relevant external timing evidence: a community TRELLIS/Pixal-derived pipeline
reports6.5-13min full jobs on4060 Laptop8GB and about60% spent on texture; this is
not the official standalone painter or our PC. Pixal3D Studio reports6-8min full
jobs on4070 SUPER12GB. These motivate broad budgets, not exact speed predictions.
[4060 Laptop report](https://github.com/Quentincls/localmesh-engine),
[Pixal3D Studio report](https://github.com/PrimeEcto/PIXAL-3D).

Follow-up after the owner asks specifically about dedicated/native texture
generation: **first trial should be TRELLIS.2 native existing-mesh painting via
an 8 GB Windows implementation**, followed by MV-Adapter as the lower-memory
alternative. This supersedes the initial MV-Adapter-first ordering below.
The full integrated TRELLIS.2 model+material path is also relevant for new animals;
the accepted TripoSG deer does not need to be discarded to test native painting.
Neither TRELLIS route has been run/benchmarked on this PC. Community 8 GB support
is evidence of plausibility, not a successful local texture-stage claim.

The roughly ten-minute figure applies only to the current thirteen-view RealVis
workflow. Prior recorded Hunyuan3D-2 paint already ran here in **43.96 s** for six
512px views (not including all preprocessing/baking). Its texture was superseded
because of output licensing, not because it was intrinsically slow. Hunyuan2.1
is a newer PBR painter; the older timing is NOT its benchmark. The paint license's
section5c explicitly restricts Outputs outside its licensed territory. Existing
private experiments are not permission to admit those maps into a shared game.

Separate expensive base-coat creation from cheap species/individual variation.
Use an existing-mesh, view-consistent texture method for a small approved base
bank. Bake anatomy-aware colours and markings from that bank, driven by the
existing ecological traits. Keep the accepted mesh, rig and animations literal.
Do not regenerate the whole animal for a coat or run diffusion at spawn time.

First generator to evaluate: **MV-Adapter's geometry-conditioned SD2.1 route**,
which the authors explicitly provide for less than 10 GB VRAM. Use our existing
mesh renderer and UV baker to preserve the accepted geometry/UVs, rather than
admitting its re-exported/normalized mesh. Evaluate fur realism as well as seams:
better view consistency alone does not prove a better deer texture. The SDXL
route officially needs more memory; fitting it on 8 GB would be experimental.

Practical comparison/fallback: **sequential SDXL inpainting with the installed
RealVis model**. Project already accepted surface pixels into the next view;
generate only unseen/untrusted areas with a consistent appearance reference.
Use a few strong body views and a close head view, then coverage-driven patch
passes. That differs from generating thirteen unrelated full photographs and
trying to reconcile them afterward. It retains our already proven single-view
8 GB route, though the new combined pipeline is not yet benchmarked.

## What the current pipeline actually does

`paint-deer-sdxl.py` requests full wildlife photographs independently per view.
It has depth guidance but no shared generation state or previously painted
surface constraint. The best-facing source view already supplies most fine
detail in `deer-sdxl-texture-cpu.py`; simply disabling averaging is not a new fix.
Broad colour uses a weighted median; face detail still includes some averaging.

The original photo, UV atlas and CPU rendered deer were inspected. The source
contains strong hair contrast and illumination; the assembled coat can look
coarse and differently coloured between regions. Marigold is only applied as
a bounded broad luminance correction, retaining source chromaticity and fine
detail. It therefore does not guarantee illumination-free fur colour. These
are plausible contributors, not an isolated proof that one step alone causes
the entire appearance problem.

The 2K atlas has 1,991,469 occupied pixels, **47.48%** of its area. Padding and
many islands consume useful resolution. Coverage within occupied texels is
97.58%; these are different metrics. Better packing/texel allocation may help,
but can require rebaking UV-dependent maps. Do not silently change UVs in the
first comparison or pretend that a nominal 4K export restores absent detail.

The 1K normal map comes from the high/low shape difference, not generated hair
microstructure. Roughness is fixed matte. Improving regional surface response
is a separate useful task from increasing image resolution. Do not convert
dark markings directly into height, which would emboss the coat pattern.

## Methods considered

| Route | Relevant benefit | Decision on this machine |
|---|---|---|
| MV-Adapter | Geometry-conditioned, coordinated views; low-memory SD2.1 option | First new-model comparison; 8 GB quality/time still unmeasured |
| Sequential RealVis/SDXL | Preserve good pixels; inpaint only uncovered areas | Strong practical comparison using installed weights |
| SyncMVD | Shares content between views during denoising | Relevant alternative; older SD1.5/PyTorch3D/Linux setup adds integration work |
| Paint3D | Lighting-less coarse-to-fine texture generation | Relevant alternative, not yet tested here |
| FlashTex | Lighting-conditioned relightable material optimization | Interesting for material quality; additional optimization/setup rather than cheapest variants |
| Hunyuan3D 2.1 | Dedicated PBR paint | Official paint requirement 21 GB; previous output-territory restrictions also remain unsuitable for the chosen route |
| TRELLIS.2 texturing | Native texture-only existing-mesh pipeline, including roughness | Official 24 GB/Linux baseline; low-memory community ports require separate verification, not categorically impossible |
| TEXGen (CVMI/VAST) | Diffuses albedo directly on mesh UV domain | Official inference baseline24GB; useful research, poor first choice for8GB without further engineering |
| Pixal3D | Integrated image-conditioned shape/PBR generation, low-memory community implementations | Candidate for NEW model+texture comparisons; not verified as a drop-in existing-deer painter |

Pixal3D's community Studio reports ~6.8GB peak for draft on a4070 SUPER12GB,
32GB RAM, with a6-10GB low-memory tier. These are publisher figures, not our4060
results; its roughly43GB download/~60GB disk requirement makes it a larger trial.
Its official project is separate from Hunyuan and publishes MIT code, with
dependency terms retained. Do not apply one Tencent model's license to every
other Tencent project. Do not assume any original high-memory requirement is
proof that community offloading cannot work.

TripoSG is the released shape generator used here, not the full current Tripo
cloud product. Textured Tripo service demonstrations do not establish that its
texture backend is released for free local execution. Paid/cloud Tripo/Meshy
services remain outside the owner's current no-payment/local requirements.

Code licenses and base/checkpoint/output terms remain distinct. MV-Adapter code
and its model card are Apache 2.0; selected diffusion checkpoints keep their own
terms. StableGen documents sequential/reference-guided workflows, but its GPL
addon code is not being copied into WorldGen. No hosted/paid service is required.

## Species specification and cheap variants

Existing `EcologyModel.cs` already has `AnimalEcotype.CoatPattern`, regional
`CoatLightnessPermille`, `SeasonalWhiteningPermille` and `InsulationPermille`,
plus stable lineage/taxon/ecoregion identities. These are a useful bridge, not
proof that all desired visual traits or an animal renderer are implemented.

Proposed material recipe:

1. One good neutral base coat initially; eventually a small approved bank per
   anatomy family, not a unique AI texture for every individual.
2. Semantic region masks: main coat, underside/throat, face, ears, lower legs,
   hooves, eyes/nose. Keep them separate from fur lighting/detail.
3. Species-level, seeded palette and permitted markings. Ecoregional traits
   constrain regional lightness/seasonal changes. Individual seeds produce
   modest variation within that species' bounds, not unrelated random colours.
4. Create spots/stripes/patch boundaries in object/surface coordinates before
   UV baking so they do not restart at each UV island. Placement must follow
   species traits/anatomical masks, not generic noise everywhere.
5. Bake colour/pattern variants offline or into a bounded cache and reuse the
   same normal maps when coat structure is unchanged. No per-frame noise stack
   or unique full material/texture per animal is necessary.
6. Use fresh AI painting only for a genuinely different surface appearance that
   the base bank cannot supply. Shape differences such as antlers, ear size and
   long-fur silhouettes remain separate mesh/shape work.

This can support many inexpensive coats without pretending recolouring will fix
a poor base texture. Establish one convincing base in neutral and changing
lighting first; then test three visibly distinct plausible coats and several
individual variants on the SAME animated deer.

## Runtime budget and generation cost

Target ordinary opaque material: colour with packed smoothness, plus a normal
map. Our bundled URP `LitInput.hlsl` already reads smoothness from base-map alpha
under `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`; this can avoid an extra texture
lookup for regional roughness. It still requires proper Blender/export/import
mapping, opaque alpha handling, compression and material configuration.

At BC7, one 2K RGBA coat costs about **5.33 MiB including mipmaps**; six resident
coats about **32 MiB**, plus one shared 1K BC5 normal at about **1.33 MiB**.
These are format/mip calculations, not measured engine allocations. Compared
with a BC1 RGB texture, preserving smoothness alpha can increase memory. Texture
diversity can also affect binding/batching; use a bounded shared material bank
or arrays when eventually integrating fauna. No zero-cost/unmeasured-FPS claim.

Historical measured cost on this PC: thirteen RealVis views 536.88 s and broad
delighting 54.03 s, excluding setup/rejected work. That is the old method, not a
benchmark of MV-Adapter or the new sequential workflow. Full base-coat creation
should be planned in minutes until measured. Palette/mask baking avoids diffusion
and should be much cheaper (seconds-scale target, not measured here). No promise
that thousands of unrelated detailed coat patterns can all be AI-generated free
of storage/time cost. All AI work remains offline.

## Evidence sources

- [MV-Adapter official usage and low-memory texture option](https://github.com/huanngzh/MV-Adapter#usage-texture-generation)
- [MV-Adapter weights/model card](https://huggingface.co/huanngzh/mv-adapter)
- [StableGen sequential/reference workflow documentation](https://github.com/sakalond/StableGen)
- [SyncMVD official implementation](https://github.com/LIU-Yuxin/SyncMVD)
- [Paint3D official implementation](https://github.com/OpenTexture/Paint3D)
- [FlashTex official implementation](https://github.com/Roblox/FlashTex)
- [Hunyuan3D 2.1 official memory requirements](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1#-models-zoo)
- [TRELLIS.2 official requirements](https://github.com/microsoft/TRELLIS.2#-installation)
- [TRELLIS.2 existing-mesh texturing source](https://github.com/microsoft/TRELLIS.2/blob/main/trellis2/pipelines/trellis2_texturing.py)
- [Unity URP Lit source](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl)
- [8GB Windows TRELLIS.2 implementation](https://github.com/IgorAherne/TRELLIS.2-stableprojectorz)
- [TEXGen inference requirements](https://github.com/CVMI-Lab/TEXGen)
- [Pixal3D official implementation](https://github.com/TencentARC/Pixal3D)
- [Pixal3D Studio publisher hardware measurements](https://github.com/PrimeEcto/PIXAL-3D)
- [Hunyuan2.1 paint license including output restrictions](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/blob/main/hy3dpaint/LICENSE)
- [Released TripoSG scope](https://github.com/VAST-AI-Research/TripoSG)
- Local `docs/DEER_GAME_ASSET_PIPELINE.md`, saved `bake.json`, raw paint/atlas,
  `EcologyModel.cs`, and the actual bundled URP package were inspected.

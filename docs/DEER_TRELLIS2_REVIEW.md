# Native TRELLIS.2 deer trial — 23 October 2026

Owner requested a free local complete shape/native-material pass. It completed
and opened in a separate Blender A/B viewer. **Owner rejected its appearance.**
Do not promote/rerig/reroll it without a new request. Accepted TripoSG deer,
original rig, authored motions and Eating correction remain preserved. The next
discussion concerns modular anatomy/materials; that alternative is not built.

## Output and actual timings

Same alpha reference hash c7e2e06b97153063579983aab9cb4a8bd6cd83bba2866a4af8bc080a51e0f926,
seed732023, native512 pipeline, twelve steps per sampling stage. Native shape:
440828 vertices/889216 triangles. Simplified GLB:9799 UV-split vertices/**13187
triangles**, one material,2048² base-colour/alpha plus2048² packed roughness/
metallic. No generated normal map claimed. Metallic0; roughness54–255 including
padding. Baking at2K does not prove2K of meaningful fur detail. No photo
projection or independent RealVis views were used in this asset.

Successful run: load5.484s, sample43.977s, decode4.366s, export8.053s,
**total62.766s**. Peak Torch allocated2712.98MiB excludes other native allocations,
driver/apps. This successful run followed failed attempts, with warm filesystem
caches. It excludes installation, downloads, troubleshooting and Blender review;
do not treat it as a cold end-to-end benchmark or visual acceptance.

Ignored evidence: `artifacts/deer-trellis2-20261023`, generation.json,
generation-native-precision.log, weight-provenance.json, native-latents.pt,
native-mesh-materials.pt, native-shape.npz and three CPU preview angles.
GLB hash a9b31dc0859465a86217da87c277262632bf80d994b19595ffeda64dde5eaae0.

## Isolated runtime and troubleshooting

[Windows8GB fork](https://github.com/andrakis/TRELLIS.2-stableprojectorz),
revision1481f4995870460b6442be50b6265307b6d6d07f. Native TRELLIS.2 weights
af44b45f2e35a493886929c6d786e563ec68364d; sparse decoder
25e0d31ffbebe4b5a97464dd851910efc3002d96; public timm DINOv3
30c1109559f65dea34316b0d4842d35c5771fe11. Manifest retains per-file hashes.
No gated-model mirror/account, paid endpoint or shared environment edits.

Working Python3.11 environment: D:/WorldGenAI/trellis2-venv, Torch2.8.0+cu128,
torchvision0.23.0+cu128, triton-windows3.4.0.post21. Its .pth exposes this task's
C:-side small dependencies including transformers4.57.3, timm1.0.22 and
safetensors0.8. ABI-matched cumesh/flex_gemm/o_voxel wheels from pinned fork.
Existing ComfyUI/old deer environments remain unchanged.

Initial C:-side installation failed after overlapping pip resolution/file lock
and low space. Broken C:-side Torch remains unused; D:'s own Torch resolves
first. Mapped safetensors then crashed in torch_cpu; owned-byte deserialization
fixes that load. Loading all flow models exhausted available system commit;
the wrapper now materializes/relinquishes one large flow at a time, including
CPU residency. FP16 conversion produced an empty structure. Successful combined
precision change preserves native BF16 flows with FP32 DINO/structure decoding;
which conversion alone caused the failure was not isolated. Finite conditioning
and predictions pass, structure occupancy6486. Native shape/texture decoders
retain the port's FP16 implementation.

Sparse attention uses a process-local Torch SDPA adapter, not flash-attn binary.
Public timm ViT-L/16 returns normalized pre-final-norm tokens, with publisher's
RoPE rounding. Fork Python exporter extends package search to the native wheel.
MIT Torch UV rasterizer selected; restricted NVIDIA renderer and RMBG not loaded.
Community-port result, not equivalence verification against official Linux.

Automatic approval rejected old-build retention cleanup AND removal of the
failed install's broken Torch folder. Neither deletion happened. D: space was
used instead; C: remains low. Do not claim reclaimed space or evade rejection.

## Review and validation

Desktop **Deer - TRELLIS2 Review.lnk** opens the separate material review blend,
with TRELLIS.2/Previous, Materials/Clay and Side/Front/Back controls. New deer
normalized to1.65m through review transforms only; raw GLB retained. Prior source
review files/apps not overwritten or closed. Inference exited; owner notified
The Forest could reopen. No new rigging, animation, Unity/game integration or
performance test. Not watertight/production retopology/deformation acceptance.

Blender4.5.15 reload checks pass finite geometry/UV, polygon count, both packed
2K maps, connected roughness and actual A/B/view/material operators. Three CPU
angles inspected. GUI launcher verified its own visible window/registration
log. Desktop capture skipped because owner focused another window.

TRELLIS.2/fork MIT notices retained. DINOv3 uses its separate publisher license,
with acknowledgement in viewer; do not call the entire toolchain CC0. Sources:
[official pipeline](https://github.com/microsoft/TRELLIS.2),
[public DINO release](https://huggingface.co/timm/vit_large_patch16_dinov3.lvd1689m).
Models/media/caches/binaries are not Git-backed.

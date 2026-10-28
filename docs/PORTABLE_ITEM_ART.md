# Portable item art sources — 28 October 2026

Geometry is original project code in `LocalPortableMeshes.cs`, with original
project rock/wood geometry for the earlier items. No third-party mesh pack,
external purchase, inference service key or new animal mesh is used.

Two shared raster source atlases were produced with the built-in imagegen tool
using the [imagegen skill](C:/Users/kille/.codex/skills/.system/imagegen/SKILL.md).
They are saved in `unity/WorldGen.Unity/Assets/WorldGen/Resources/`:

- `PortableMaterialAtlasV1.png`: eight equal surface cells: chert, debarked wood,
  bark, rawhide, worked leather, bone, bast fibre and reeds. Prompt: flat diffuse
  photorealistic PBR albedo source, exact 4×2 grid, no objects/labels/borders or
  directional lighting, natural muted material detail. The shader mirrors local
  UV repetition within each cell and insets samples to avoid adjacent-cell bleed.
- `PickupCategoryMasksV1.png`: twelve broad white-on-black mask categories in a
  4×3 grid: rock, branch, cutting flake, spear, bow, cord, hide, carrier, bone,
  reeds, hook/line, feather. Prompt: professional crisp pure-white silhouettes
  on solid black, no shading/texture/text, regular grid with generous margins.
  The importer derives alpha from luminance; black is transparent in the game.

The initial generated transparent icon attempts had noisy alpha and were rejected;
they are not project assets. Selected original outputs remain under
`C:/Users/kille/.codex/generated_images/01a0fa13-856b-7f40-ab01-51bc91fe8f33/`.
Final atlas source names are `exec-b5d54a2d-b06e-4015-80ec-e1d08f42ecad.png`
and `exec-507cef0d-b027-480b-8081-a6171330ea45.png` respectively.

Inventory thumbnails render actual item geometry/materials, one queued item per
frame while the inventory is open. Surface texture reads apply to items only;
the terrain/foliage shaders and populations are unchanged by this art pass.
Actual runtime image review is required before calling the artwork accepted.

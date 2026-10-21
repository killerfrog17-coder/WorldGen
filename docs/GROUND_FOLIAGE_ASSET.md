# Ground foliage atlas V1

Generated21October2026 with the built-in `image_gen` tool, not a CLI/API script.
Transparent background requested and genuine RGBA verified. Source1254x1254,
3,479,327bytes, copied unchanged into the repository. No manual bitmap edits.
SHA256: `01ae07be0de3e9aa28776742e510483731e2be8cce6c6986043d6e10803c0879`.

Asset: `unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundFoliageAtlasV1.png`.
Four quadrants: meadow grass, ribbon grass/sedge, low herbs and fern banks.
Editor/GroundFoliageImporter.cs handles alpha-edge dilation, coverage-preserving
mips, trilinear filtering and normal texture compression; no other textures
are affected. Shader cutoff.35. This is generated production-preview art,
not scanned/identified real botanical species or finished art acceptance.

## Exact generation prompt

Use case: photorealistic-natural. Asset type: production RGBA vegetation alpha-card texture atlas for a real-time 3D open-world game. Generate a square 2048x2048 image with a precise 2 by 2 grid of FOUR equal square cells, no borders, no labels, genuine transparent background. Orthographic front elevation, evenly diffused unlit albedo, sharp detailed botanical silhouettes, no perspective background or ground plane. In EACH cell, foliage grows across the entire bottom edge, densely overlapping and filling about 70 percent of the cell area, with roots at the bottom of that cell and varied upper tips around its upper tenth. Do NOT depict a tiny isolated centered tuft. Top-left: a dense shoulder-wide swath of many fine and medium fresh meadow grass blades, 35-50 long curved and crossing olive/sage green blades. Top-right: a dense swath of broad ribbon grass and slender sedge blades, slightly warmer olive-green, a few subtle grass seed stems. Bottom-left: a bank of many overlapping low herbaceous oval and lobed leaves on thin stems, green woodland ground-cover herbs, no flowers. Bottom-right: a dense bank of overlapping arching fern fronds with individually resolved leaflets, varied direction, filling the lower two thirds and reaching irregularly up. All four cells MUST have separate fully transparent gaps between leaves and a thin transparent upper margin, but dense almost-continuous leafy coverage in their lower third to make full volumetric meadows, not sparse ornament clumps. Keep all foliage of each cell inside its own cell. Natural imperfect shapes, fine leaf veins and fibers, medium-value muted vegetation greens suitable for shader tinting; no baked directional shadows, no ambient backdrop, no black/white border or fringe, no pot, soil, stones, illustration outlines, text or watermarks. Preserve genuine alpha transparency.

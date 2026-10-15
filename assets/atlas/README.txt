WorldGen habitat illustrations — 15 October 2026

Mode: built-in image generation (imagegen skill). These are original AI-generated
natural-history illustrations, not photographs, measured source data, or an exact
reconstruction of any generated location. No named artist or external reference
image was used. Canonical shipped files are biomes/*.jpg (eight assets, ~5 MB).
Original PNG outputs are preserved outside the shipped source assets. JPEG
encoding only changes the storage format; no generated scenery was repainted.

Production prompt set
Common brief: stylized-concept natural-history field-guide landscape painting
for a historical-world atlas; restrained realistic brushwork, natural colors,
wide readable composition with uncrowded foreground, soft atmospheric distance.
No people, animals, buildings, text, logos, frames or watermarks. Not a named
real-world place. Animals are kept out of backgrounds so actual native fauna
records can be shown independently without inventing species presence.

savanna: Tropical seasonal savanna, golden-green grasses, scattered broad-canopied
thorn trees, shallow seasonal stream, gentle distant hills, warm clear daylight;
horizon in the upper third.
rainforest: Lowland tropical rainforest, tall broadleaf canopy, layered lush
understory, buttress roots, small shaded stream and wet stones, humid soft
distance and deep greens.
temperate: Mixed broadleaf woodland in early summer, oaks and beeches with a few
conifers, rich green understory, leaf litter, open grassy clearing, modest stream
in the middle distance, soft daylight.
boreal: Short-summer spruce and pine woodland, moss, lichens, berry shrubs,
glacial rocks, small still pools, distant low wooded hills, cool clear sky;
not perpetual deep snow.
grassland: Temperate grassland and open steppe, green and tawny grasses,
wildflowers, low herbs, shallow seasonal drainage, rounded treeless hills,
large blue sky; no fences, cultivation or roads.
arid: Arid desert and xeric shrubland, pale ochre stony ground, widely spaced
drought-tolerant scrub, dry wash, gravel fans, distant rocky ridges and a small
bank of sand rather than an endless dune sea; muted earth tones and blue sky;
no river, lush oasis or palms.
wetland: Freshwater floodplain and marsh, broad calm river bend and shallow side
channels, reeds, sedges, wet grasses, muddy banks and scattered flood-tolerant
broadleaf trees in the distance; natural water-land gradation, not geometric
ponds; muted blue-brown water.
alpine: Cold tundra and alpine meadow, low mosses, lichens, sedges, tiny flowers
and gray stones, open treeless ground, rounded ridges and remnant snow patches,
cool short summer; not an ice sheet or woodland.

Integration
HabitatPortrait.At selects an illustrative family using the cell's actual biome.
It deliberately omits unsupported tidal/saline/ocean/ice scenes. Actual local
conditions and suitable recorded native taxa are displayed separately. Coarse
habitat does not determine city architecture or the appearance of its people.
scripts/prepare-atlas.ps1 stages these canonical assets into browser and Unity
StreamingAssets folders. Inspection loads them lazily; simulation never reads
them, and they do not affect world or historical hashes.

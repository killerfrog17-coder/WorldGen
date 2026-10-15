# Map presets — 3.32

The dropdown uses this play-first order. Seed and detail survive preset changes; other settings reset. Windows remain editable ranges, resolved through deterministic named PRNG streams. Rolls are linear unless stated otherwise.

| Preset | Main settings/intent |
|---|---|
| Continents plus (default) | 4–10 continents, 10–50 interconnectedness |
| Continents | 2–8 continents, 28–42% land |
| Earthlike | 5–7 continents, 25–35% land, 0.95 continent variety, 50 interconnectedness |
| Pangeae | One continental identity, 30–50% land |
| Earth | Bundled measured relief; simulated climate/ecology |
| Terra | Rough Americas, Africa/Eurasia and Oceania template |
| Archipelago | 350–500 plates, 20–50 continents, 23–35% land |
| Fractal | 8–22 continents, strong warp, 15–40 interconnectedness |
| Two continents | Two equal-target identities, 30–45% land |
| Large continents | 2–3 continents, 30–50% land |
| Small continents | 8–16 continents, 200–320 plates |
| Large Islands | 10–20 equal-target identities, 180–300 plates |
| Shuffle | Wide ranges; mountains/lakes 50–150%; interconnectedness 1–10 linear; land/continent rolls favor low values |
| Inland Sea | Land outside a central 48° spherical sea cap |
| Crater Sea | Land outside a 55° sea cap, with a 9° central island |
| Donut | Spherical annulus 35°–80° from its center, reaching latitude ±80° |
| Aborea | Wet forest-favoring climate; reduced mountains |
| Boreal | Wet climate with −12°C offset |
| Ice age | Continents plus with −7°C offset and strong glaciation |
| Lakes | No ocean; inland moisture allowance, restrained lake retention (20%) |
| Desert world | No ocean or supplemental moisture; hot, dry climate |

Ordinary presets retain plate generation: continent counts describe identities, not guaranteed unbroken landmasses. Equal target areas are approximate because plates are indivisible. Forest/cold presets alter climate inputs, never biome labels; polar ice, mountains and wetlands can remain.

Shape presets guide whole-plate assignment then constrain final land/sea elevation before climate. They bypass interconnectedness search. Shape boundaries take precedence over the land-percentage setting. Editing settings retains the shape until choosing Custom or another map type. Plate provenance and pre-conditioning elevations remain inspectable. Templates are authored geography, not impact simulations or geological reconstructions.

`InteriorRainMm` supplies nominal annual scenario rainfall, modulated seasonally/spatially and added before the precipitation multiplier. This moisture allowance supports oceanless Lakes and forest scenarios without pretending ocean evaporation exists. It is not a globally closed water-cycle model. Ordinary worlds keep it zero.

Saved complete recipes contain resolved settings and versioned `LandShape`, not a dependency on mutable preset names. Algorithm 3.32 retains the 3.31 route when new settings are absent. Old worlds are not regenerated on load.

# Prehistory 2

Historical contract. New simulations now use [Prehistory 3](PREHISTORY_3.md); existing version-2 saves retain the rules below.

New simulations prefer viable savanna patches (capacity at least 200), then grassland, with warm habitat, capacity and seeded ties. Earth's African preference is subordinate to biome suitability. Rerolls retain the habitat preference; manual placement still permits other habitable patches.

The authoritative step is 100 years. The prehistory HUD defaults to one such step, with independently selectable 1/5/10/50-step advances and 1/2/4/10 steps per real second. Playback starts paused, at two steps per second. These are scheduling controls, never inputs to demographic calculations. No later historical phases are implemented yet; their defaults should be assigned at their future phase transitions.

Short open-water crossings no longer require 15,000 elapsed years. Their per-step probability is 0.015 + 0.30 exp(-distanceMetres / 55000); traversal time, viable founding bands, habitat capacity and the existing 140 km route bound still apply. Shelf crossings retain the bounded 600 km / 130 m depth search and glacial window, with 0.22 chance per step. Mesh resolution still limits detection of narrow straits. These are gameplay coefficients, not historical calibration.

Growth, migration fractions and drift variance are scaled to the shorter authoritative step. All decisions remain seeded and ordered. New checkpoints use `prehistory-2`; existing `prehistory-1` saves retain their original 250-year algorithm, crossing rules and exact continuation. The HUD reports the appropriate step size for those legacy saves. No generated terrain or climate changes.

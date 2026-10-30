# Hunting and exertion revision

**October 30 follow-up in progress:** the owner rejected the previous procedural first-person arms after playtesting. Earlier mechanical passes below do not establish acceptable animation quality. A dedicated CC0 first-person rig, camera-authored clips, forearm twist distribution and six derived skin atlases replace that approach. The replacement is still under visual review; the existing shortcut has not been switched to this candidate.

The animal follow-up explicitly reviews all 177 visible model families in `WildlifeNaturalHistory`, with native-taxon overrides for animals sharing a mesh. Profiles distinguish social structure, diel rhythm, hunting style, response to people and sensory group. These are ecological/gameplay priors, not a claim to have reproduced every species' complete ethogram. Habitat/resource population accounting and bounded encounter pacing remain authoritative.

Changes under validation include sight-constrained animal-to-animal encounters, finite prey-location memory, physical hunting strata, defensive warnings, non-circulating herd alarms, actual-daylight activity, independent water needs, terrestrial ectotherm mobility and cheap local disturbance responses for small actors. Packs alone do not make wolves hunt people, and sea lions cannot inherit big-cat behavior from their name. Predatory human encounters depend on the species' response, hunger, individual boldness and perceived opportunity. Exact numeric thresholds are tuning choices.

Additional natural-history references:
- [NPS wolf behavior and human encounters](https://www.nps.gov/dena/learn/nature/upload/Annual-Wolf-Report-2024.pdf).
- [California wildlife agency: solitary mountain lions and ambush predation](https://wildlife.ca.gov/Conservation/Mammals/Mountain-Lion).
- [NPS: mountain-lion encounter context](https://www.nps.gov/gumo/planyourvisit/mountain-lion-safety.htm).
- [NPS: bear safety and different attack contexts](https://www.nps.gov/subjects/bears/safety.htm).
- [Cornell Lab: snowy owls hunt during Arctic daylight](https://www.allaboutbirds.org/guide/Snowy_Owl/).
- [NPS: bison summer rut](https://www.nps.gov/articles/bison-bellows-7-27-16.htm).
- [Queensland government: kangaroo mobs and responses to people](https://www.qld.gov.au/environment/plants-animals/animals/living-with/kangaroos).
- [Macropus rufus species account](https://academic.oup.com/mspecies/article/52/998/143/6046133).
- [Cornell Lab: turkey-vulture olfaction](https://academy.allaboutbirds.org/in-defense-of-turkey-vultures/).

Current limitations include simplified group composition rather than simulated offspring, no complete den/nest or arboreal navigation, no measured water-temperature field for fish, and neutral activity-animation fallbacks where original source clips are unavailable. These limits are separate from the per-species behavior review.

Completed in two checkpoints: animal/hunting first, then first-person arms and inventory/HUD refinements. All player builds use IL2CPP. Original approved animal/human source art and locomotion bundles remain unchanged.

Shared exertion has burst capacity, breathing, accumulated physical fatigue and heat strain. Full effort fades continuously; sustainable travel has different demand. Brief jogging only partly recovers a burst. Species profiles alter sustainable fraction, burst duration, recovery and heat tolerance. Flight/gliding have separate costs. These numerical profiles are gameplay priors, not measured laboratory values for every simulated taxon.

Perception integrates movement, posture, habitat concealment, illumination, field of view, terrain/solid LOS, movement sound and directional scent. It accumulates suspicion and preserves last-known information. Social alarms spread locally, not by granting every herd member perfect vision of the player. Notable active hunt states and effort survive saves/streaming. Nearby detailed decisions are staggered under a shared soft 1 ms decision budget; distant groups retain bounded coarse processing and small insects retain simple wandering.

Additional motion is exported read-only from original rigs into independently versioned small banks. Existing source meshes/textures and locomotion bundles are not re-exported. 87 banks expose original eating, sleep/sitting, alert and attack clips where present, plus fitted native-skeleton deer/wolf rest and kangaroo grazing poses. These supplements do not change the original approved files. Remaining missing species-specific actions use neutral fallbacks; the entire animation set is not complete.

The six-sector wind indication gives a source direction relative to the camera. Wind from behind is at the bottom. Weak airflow fades out; moderate airflow is broad/uncertain rather than a precise compass. Symbols fit between the six thermal arms inside the existing outer arcs. No extra control banner or stealth radar is added.

Research:
- Kangaroo social vigilance: https://pmc.ncbi.nlm.nih.gov/articles/PMC2880096/
- Human approach/flight response: https://onlinelibrary.wiley.com/doi/abs/10.1002/ece3.5818
- Indigenous stalking/wind/rain masking: https://museum.wa.gov.au/research/records-supplements/records/food-resources-aborigines-south-west-western-australia/accessible-version/
- Deer visual ecology: https://www.mdpi.com/2076-2615/14/3/420
- Scent-driven deer vigilance: https://pmc.ncbi.nlm.nih.gov/articles/PMC8528536/
- Herbivore alarm eavesdropping: https://www.sciencedirect.com/science/article/pii/S0003347217304207
- Human wind perception: https://pmc.ncbi.nlm.nih.gov/articles/PMC4521093/ (1.5?3 m/s laboratory airflow; no universal perception threshold)
- Flight energetics: https://pmc.ncbi.nlm.nih.gov/articles/PMC4992708/
- Gliding versus flapping measurements: https://pmc.ncbi.nlm.nih.gov/articles/PMC11574358/
- Repeated-sprint recovery: https://pmc.ncbi.nlm.nih.gov/articles/PMC3524088/

The claim that motionless people are categorically invisible to kangaroos is not supported here. Their response depends on vigilance, context and experience with humans. Real hunting techniques should help without becoming guaranteed success buttons.

Validation so far: Core 23 hunting, 67 modes, 24 metabolism/camp experience, 161 HUD, 103 abundance, 77 item-use and 37 water checks pass. Native hunting check: 22 PASS, EXIT 0, clean log; actual GPU captures of deer eating/rest, wolf rest, kangaroo grazing and wind sectors inspected. Original art hashes exact. The first checkpoint was selected before the follow-up; the current shortcut includes both phases.


## First-person handling and live inventory

A private, runtime-only mesh derivative filters the local MPFB avatar to its skinned arms. The original world avatar remains whole and unchanged. Camera callbacks show these arms only to the local first-person view. Two-bone arm targets and native finger joints follow each held grip; wind-up, thrust, cutting, bow draw/release and gathering drive the targets. Free offhand support improves a long spear's thrust; occupying that hand leaves the spear available with shorter reach, less force and slower recovery. Original locomotion and source FBXs/Blends are unchanged.

Inventory blocks player input while the world clock, animals, projectiles, dropped items, camp fires and survival needs continue. Equipment is dragged onto the portrait; only actually worn equipment supplies named storage entries. Portrait rotation uses dragging. The fixed empty anatomic slots, turn buttons and bottom controls banner are removed.

The stomach reflects felt hunger with a small reserve-stage floor until body reserves are depleted. Felt hydration uses actual fluid deficit, distinguishing parched from critical dehydration. Urgent need pulses the empty interior of the appropriate icon. Active food or water illness gradually greens the stomach; delayed, symptom-free poisoning cannot be diagnosed from the HUD. Existing quick fire cooking remains available.

Release validation: 37 first-person/inventory, 22 hunting and 97 camp checks pass on the final IL2CPP package, all EXIT 0 with clean logs. Actual GPU captures were inspected for grips, thrust, throw wind-up, bow/arrow alignment, gathering, wind sectors, need states, animal poses, fire/cooking and placement. This includes native arm meshes for six avatars, local-camera privacy, full world-avatar preservation, live inventory needs/time/gravity, actual portrait-equipment operation, cooking, occupied-hand crafting, sleep disturbance and thermal rendering. The automated portrait test exercises the drop handler; it does not claim manual IMGUI mouse-drag testing in batch mode.

Focused Core checks: 329 HUD, 75 inventory, 77 item use, 23 hunting, 67 survival modes, 24 metabolism/camp, 103 wildlife abundance and 37 water. Original protected art hashes remain exact. Source and all 87 motion banks are Git-backed. These procedural first-person actions are a first pass for playtesting, not a full authored mocap library. The work does not claim a general frame-rate, terrain-loading or water-seam fix.

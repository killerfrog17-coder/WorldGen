# Hunting and exertion revision

Phase 1 is the animal/hunting checkpoint; ship it before first-person arms and inventory/HUD refinements in phase 2. All player builds use IL2CPP. Original approved animal/human source art and locomotion bundles remain unchanged.

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

Validation so far: Core 23 hunting, 67 modes, 24 metabolism/camp experience, 161 HUD, 103 abundance, 77 item-use and 37 water checks pass. Native hunting check: 22 PASS, EXIT 0, clean log; actual GPU captures of deer eating/rest, wolf rest, kangaroo grazing and wind sectors inspected. Original art hashes exact. Phase 1 shortcut selected; phase 2 arms/inventory/need HUD is in progress.


## First-person handling and live inventory

A private, runtime-only mesh derivative filters the local MPFB avatar to its skinned arms. The original world avatar remains whole and unchanged. Camera callbacks show these arms only to the local first-person view. Two-bone arm targets and native finger joints follow each held grip; wind-up, thrust, cutting, bow draw/release and gathering drive the targets. Free offhand support improves a long spear's thrust; occupying that hand leaves the spear available with shorter reach, less force and slower recovery. Original locomotion and source FBXs/Blends are unchanged.

Inventory blocks player input while the world clock, animals, projectiles, dropped items, camp fires and survival needs continue. Equipment is dragged onto the portrait; only actually worn equipment supplies named storage entries. Portrait rotation uses dragging. The fixed empty anatomic slots, turn buttons and bottom controls banner are removed.

The stomach reflects felt hunger with a small reserve-stage floor until body reserves are depleted. Felt hydration uses actual fluid deficit, distinguishing parched from critical dehydration. Urgent need pulses the empty interior of the appropriate icon. Active food or water illness gradually greens the stomach; delayed, symptom-free poisoning cannot be diagnosed from the HUD. Existing quick fire cooking remains available.

Phase 2 validation in progress: 329 HUD, 75 inventory, 77 item use, 23 hunting, 67 survival modes and 24 metabolism/camp checks pass. Native first-person pass 3 passed all checks including actual arm meshes for six avatars, camera privacy, live inventory needs/time, portrait equipment and GPU HUD states. Grip/action captures inspected; the final nocked-arrow addition still requires its new native pass. These are procedural first-person actions; they are not a full authored mocap library.

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

The earlier animal/hunting and inventory/HUD checkpoints are retained in Git. Their original procedural arm presentation was rejected during owner playtesting. All new player builds use IL2CPP. Original approved animal/human source art and locomotion bundles remain unchanged.

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

Follow-up verification at 10:32 CDT: the v19 IL2CPP candidate passed 77 first-person, 35 hunting, 30 nature, 155 item-use and 97 camp checks with clean runtime logs. The hunting run includes four 24-second trials using actual player movement: ordinary walking with scent blowing away caused flight at about 15 m; a crouching approach with scent blowing away reached about 4 m without flight; the stationary trial caused no flight at 24 m; scent blowing toward the animal caused the crouching trial to trigger flight at about 21 m. These are observations in one procedural site, not guaranteed thresholds. The decision-only forced-sweep benchmark with 62 actors measured median 1.426 ms and 95th percentile 1.658 ms under the soft budget; this is not total frame time.

Subsequent source changes add conservative physical ground movement priors instead of the old universal length multiplier. Previously a wolf's nominal maximum was only 4.5 m/s, a hare's 1.95 m/s, and a sloth's 2.25 m/s. Species-family values correct those contradictions before exertion and environmental limits. The source animation stride references remain separate so increasing travel speed does not silently keep the old slow cadence. Bounded obstacle lookahead includes the next decision interval and braking, and active nearby attacks check contact between decisions. These changes passed 709 focused hunting checks but still require the new candidate's native sprint/contact/visual checks. The bow drawing-hand refinement also awaits that candidate. No new shortcut is selected yet.

Locomotion context: [NPS pronghorn speed and endurance](https://www.nps.gov/articles/000/pronghorn.htm), [International Wolf Center](https://wolf.org/wolf-info/basic-wolf-info/wolf-faqs/) and [NSW National Parks red kangaroo](https://www.nationalparks.nsw.gov.au/plants-and-animals/red-kangaroo). The numerical table is a set of conservative game priors rather than a claim that every family has a measured universal top speed.

## First-person handling and live inventory

The replacement uses a dedicated CC0 first-person mesh and native finger rig from [BigAndCrispy's first-person melee project](https://github.com/BigAndCrispy/Unity-First-Person-Melee), with a locally authored derivative in `assets/first-person`. It has 53 baked handling clips, separate diagonal spear grips, distributed forearm twist weights, supported and single-handed spear poses, tool-specific contacts, loaded bow/sling/atlatl handling, gathering, drinking and crafting poses. Six skin atlases derive from the existing approved body maps; those originals stay untouched. Blender poses are baked to bone animation before import, with no runtime arm or finger IK. The authoring script, native source license, export hashes, grip pose data, FBX and Blender files are tracked.

A private camera-only rig renders the local arms and held item copies. The world avatar remains whole for third person and other viewers. Wind-up precedes actual melee contact, and hand-thrown projectiles stay owned by the hand until the release frame. A loaded projectile leaves from its visible held/nocked position and converges toward the aimed point; the camera-to-hand obstruction check prevents spawning through a nearby wall. Inventory interruption cancels a pending throw without losing its item. These input/contact paths still require the latest package's native regression checks.

Inventory blocks player input while the world clock, animals, projectiles, dropped items, camp fires and survival needs continue. Equipment is dragged onto the portrait; only actually worn equipment supplies named storage entries. Portrait rotation uses dragging. The fixed empty anatomic slots, turn buttons and bottom controls banner are removed.

The stomach reflects felt hunger with a small reserve-stage floor until body reserves are depleted. Felt hydration uses actual fluid deficit, distinguishing parched from critical dehydration. Urgent need pulses the empty interior of the appropriate icon. Active food or water illness gradually greens the stomach; delayed, symptom-free poisoning cannot be diagnosed from the HUD. Existing quick fire cooking remains available.

The original procedural-arm release passed mechanical first-person, hunting and camp checks, but owner playtesting found unacceptable arm deformation. Those historical passes do not establish quality of this replacement. The current follow-up remains under visual and input review; the shortcut still points to the earlier release until a replacement is accepted for testing. No general terrain-loading, water-seam or total-frame-rate improvement is claimed.

Additional review references:

- [NPS: moose warnings and limited defensive pursuit](https://www.nps.gov/wrst/planyourvisit/moose-safety.htm).
- [Michigan DNR: elk defense, cornering and breeding season](https://www.michigan.gov/dnr/managing-resources/wildlife/nuisance-wildlife/elk).
- [Cornell Lab: raven diet and live prey](https://www.allaboutbirds.org/guide/common_raven/lifehistory).
- [Cornell Lab: herring gull foraging and predation](https://www.allaboutbirds.org/guide/Herring_Gull/lifehistory).
- [NPS: porcupine retreat and close defensive posture](https://www.nps.gov/articles/acadia-porcupines.htm).
- [Washington wildlife agency: skunk warning behavior](https://wdfw.wa.gov/species-habitats/living/species-facts/skunks).

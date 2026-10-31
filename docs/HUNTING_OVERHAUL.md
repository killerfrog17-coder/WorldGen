# Hunting and exertion revision

## October 31: effort, concealment and distant wildlife correction

Owner playtesting found unacceptable jump/swimming depletion and immediate flight through tall grass. The new candidate corrects these behaviors; package/native verification is recorded separately when complete.

- The human reserve models usable sustained effort, not phosphocreatine alone. Healthy full-speed running now spends the reserve over minutes. A jump costs two points and recovers in seconds during easy activity. Breathing, cumulative fatigue and heat still prolong recovery after hard work. Old saves migrate depleted short-budget reserves once, preserving breathing/fatigue/heat.
- Swimming previously charged both the shared exertion model and a separate flat per-second drain. Only the shared model now applies, using actual strokes rather than water-current drift. Normal swimming is sustainable; hard swimming, load and heat cost more. Jump, combat and bow holding share the same authoritative spending path.
- Sight samples the resident terrain placement grid used for visible grass, including stature and slope admission, and accumulates obstruction along a bounded eight-sample sight line. Physical concealment is independent of graphics quality. Terrain and solid obstruction checks remain. Moonlight is used rather than assuming 70% visibility in darkness.
- Faint cues converge to limited suspicion instead of becoming certain merely through repetition. Watching, threat confirmation and flight are distinct. Flight depends on distance, cue strength, reaction time and stable individual caution. Close surprise, injury and committed social alarm can still cause prompt escape. Far audible movement does not supply perfect visual knowledge.
- Visibility bands now retain medium/large land animals to 1.1/1.6 km, smaller land animals to 300 m where appropriate, and small/large flying birds to 650 m/1.2 km. Existing short tiny-ground-animal ranges remain; butterflies/fireflies retain 220 m. Weather/terrain visibility still applies. Models preload before entering their draw range, are not held up by unrelated terrain work, and are not immediately evicted while still needed for preloading. Current tracked group positions determine admission, rather than original spawn homes. This does not multiply population density.

Targeted model tests: 37 reported-problem regressions, 709 hunting checks, 67 survival/mode checks, 329 HUD checks, 77 item-use checks and 103 abundance checks passed. A fresh jump recovered to over 99.9% reserve in 0.65 s standing, 0.9 s walking and 3.3 s jogging; fresh continuous full-speed running exhausted the test reserve after 220.9 s. These are controlled game-tuning results, not promises under injury, load or heat. Native input, grass-path and actual GPU distance checks are required before selecting the candidate.

Physiology supports distinguishing short maximal effort from complete whole-body incapacity; it does not prescribe this game's exact reserve values. Animal response research likewise distinguishes alert distance from flight initiation and finds substantial context/individual variation. Dense vegetation can conceal an observer while also changing the response once detected. The tuning does not assert that real animals can never flee at 50 m.

Primary references: [single/repeated short sprint recovery](https://pubmed.ncbi.nlm.nih.gov/9241025/), [maximal-effort muscle recovery](https://pmc.ncbi.nlm.nih.gov/articles/PMC1157744/), [mule-deer vigilance and human activity](https://escholarship.org/uc/item/7761v4bn), [white-tailed deer flight-initiation context](https://pmc.ncbi.nlm.nih.gov/articles/PMC4198184/), [zebra risk perception](https://pmc.ncbi.nlm.nih.gov/articles/PMC12077961/). Species-specific reserve and reaction numbers remain gameplay priors. Bounded probes, staggered decisions and the existing shared soft 1 ms decision budget remain; tiny local creatures do not acquire heavy perception work.

**October 30 replacement available for playtesting:** the owner rejected the previous procedural first-person arms. A dedicated CC0 first-person rig, 53 baked handling clips, distributed forearm twist and six derived skin atlases replace that approach. The existing WorldGen shortcut selects `artifacts/player-hunting-cover`, the IL2CPP replacement built at 17:05:41 UTC from production source `7efe691`. Native checks and sampled visual review establish a tested checkpoint; they do not substitute for the owner's judgment of how the animation feels.

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

The selected replacement passed 402 native checks: 81 first-person, 39 hunting, 30 nature, 155 item-use and 97 camp, with successful exits and clean runtime error checks. A second successive 155-check item-use run also passed; it is recorded separately rather than counted as additional distinct coverage. The hunting run includes four 24-second trials using actual player movement: ordinary walking with scent blowing away caused flight at about 15 m in the final run (about 23 m in another run); crouching with scent blowing away reached about 4 m without flight; the stationary trial caused no flight at 24 m; scent blowing toward the animal caused the crouching trial to trigger flight at about 21 m. These are observations at one procedural site, not guaranteed thresholds. The final decision-only forced-sweep benchmark with 62 actors measured median 1.449 ms and 95th percentile 1.812 ms; the largest observed sweep was 2.204 ms. The 1 ms budget is soft and can be exceeded by an individual decision. This is not total frame time or an uncontended hardware benchmark.

Conservative physical ground movement priors replace the old universal length multiplier. Previously a wolf's nominal maximum was only 4.5 m/s, a hare's 1.95 m/s, and a sloth's 2.25 m/s. Species-family values correct those contradictions before exertion and environmental limits. Source animation stride references remain separate from movement speed; existing placeholder gaits are retained, with bounded cadence adjustment rather than newly authored locomotion. Obstacle lookahead includes the next decision interval and braking, without marching terrain along each longer ray. Active nearby attacks check contact between decisions. Cached grass concealment now applies the existing physical grass slope and water-admission rule, so regional grass cannot supply camouflage on bare cliffs or underwater channel beds. These changes passed 709 focused hunting checks and the selected package's native obstacle, contact, sprint and saved-state checks.

Locomotion context: [NPS pronghorn speed and endurance](https://www.nps.gov/articles/000/pronghorn.htm), [International Wolf Center](https://wolf.org/wolf-info/basic-wolf-info/wolf-faqs/) and [NSW National Parks red kangaroo](https://www.nationalparks.nsw.gov.au/plants-and-animals/red-kangaroo). The numerical table is a set of conservative game priors rather than a claim that every family has a measured universal top speed.

## First-person handling and live inventory

The replacement uses a dedicated CC0 first-person mesh and native finger rig from [BigAndCrispy's first-person melee project](https://github.com/BigAndCrispy/Unity-First-Person-Melee), with a locally authored derivative in `assets/first-person`. It has 53 baked handling clips, separate diagonal spear grips, distributed forearm twist weights, supported and single-handed spear poses, tool-specific contacts, loaded bow/sling/atlatl handling, gathering, drinking and crafting poses. Six skin atlases derive from the existing approved body maps; those originals stay untouched. Blender poses are baked to bone animation before import, with no runtime arm or finger IK. The authoring script, native source license, export hashes, grip pose data, FBX and Blender files are tracked.

A private camera-only rig renders the local arms and held item copies. The world avatar remains whole for third person and other viewers. Wind-up precedes actual melee contact, and hand-thrown projectiles stay owned by the hand until the release frame. A loaded projectile leaves from its visible held/nocked position and converges toward the aimed point; the camera-to-hand obstruction check prevents spawning through a nearby wall. Inventory interruption cancels a pending throw without losing its item. Native checks exercise actual mouse input, contact timing, throw ownership and interruption.

The hand stays visible in the cocked throwing pose, including the narrower 4:3 review. Release clips inherit the preceding wind-up's elbow branch and twist state. An export audit rejects large frame-to-frame jumps; this caught the intermediate elbow-branch switches and half-twist angle wrapping. Those defects were corrected before selection. Held poses also move slightly with the player's gait and settle after stopping; the complete rig and held item move together, without moving the camera. Native input checks cover movement, settling and supporting-hand ownership.

Large ragdolls receive a small coherent loss of lateral balance rather than an isolated root impulse; physics still determines their collapse. Conservative local chain limits and tighter anchor projection reduce compounding folds and stretching. Repeated testing exposed variation that an earlier single successful collapse did not reveal; the subsequent correction passed two successive item-use runs, with deer-collapse 99th-percentile edge extension ratios 2.104 and 2.273 below the unchanged 2.5 regression threshold. Some automatic corpse cameras can enter nearby slopes; the corresponding invalid photo is not treated as visual acceptance. Recorded native corpse geometry was also inspected independently. This remains a prototype ragdoll system, not a guarantee that every terrain contact looks natural.

Inventory blocks player input while the world clock, animals, projectiles, dropped items, camp fires and survival needs continue. Equipment is dragged onto the portrait; only actually worn equipment supplies named storage entries. Portrait rotation uses dragging. The fixed empty anatomic slots, turn buttons and bottom controls banner are removed.

The stomach reflects felt hunger with a small reserve-stage floor until body reserves are depleted. Felt hydration uses actual fluid deficit, distinguishing parched from critical dehydration. Urgent need pulses the empty interior of the appropriate icon. Active food or water illness gradually greens the stomach; delayed, symptom-free poisoning cannot be diagnosed from the HUD. Existing quick fire cooking remains available.

The original procedural-arm release passed mechanical first-person, hunting and camp checks, but owner playtesting found unacceptable arm deformation. Those historical passes do not establish quality of this replacement. The new shortcut is ready for owner playtesting. Original approved meshes, rigs, skins, collision identities, 87 supplemental motion banks and existing audio remain unchanged; the private first-person derivative and its authoring inputs are tracked in Git. No general terrain-loading, water-seam or total-frame-rate improvement is claimed.

Additional review references:

- [NPS: moose warnings and limited defensive pursuit](https://www.nps.gov/wrst/planyourvisit/moose-safety.htm).
- [Michigan DNR: elk defense, cornering and breeding season](https://www.michigan.gov/dnr/managing-resources/wildlife/nuisance-wildlife/elk).
- [Cornell Lab: raven diet and live prey](https://www.allaboutbirds.org/guide/common_raven/lifehistory).
- [Cornell Lab: herring gull foraging and predation](https://www.allaboutbirds.org/guide/Herring_Gull/lifehistory).
- [NPS: porcupine retreat and close defensive posture](https://www.nps.gov/articles/acadia-porcupines.htm).
- [Washington wildlife agency: skunk warning behavior](https://wdfw.wa.gov/species-habitats/living/species-facts/skunks).

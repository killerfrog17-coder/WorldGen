# Placed materials, campfires and sleep

Prototype implementation in progress; the selected player remains the preceding
water release until the combined update is tested and packaged.

- E on grassy ground: gather a physical grass bundle. Tree resource menus also
  supply leaf litter; existing reed gathering remains available.
- Right-click grass, reeds, leaves or hide: spread bedding. Add further layers
  through E on the bedding. Stored material mass controls depth and comfort;
  rain wets it and wind/sun dry it. Hides retain their existing provenance.
- Right-click fine tinder: place a fire lay. Add thin kindling and wood through
  its contextual interaction. A stone ring is not a mandatory ignition gate.
- Shape a spindle/hearthboard set from two suitable wood pieces with a cutting
  edge; shred grass/bark/fibre into tinder; split wood into kindling. These are
  existing contextual, timed crafting actions, not a new recipe browser.
- Lighting with a hand drill is timed work, borrows the actual tool and restores
  prior hands. Dry tinder and thin kindling are required before thicker fuel.
  Fuel burns down to ash/embers; rain can extinguish an exposed fire. The camp
  simulation clock is independent of debug sun-position commands.
- Ash remains visible after fuel is consumed. Live embers glow until they cool;
  standing in the flames burns the player, while nearby radiant warmth is safe.
  The hand-drill item shows a slender spindle with a separate hearthboard.
- Firelight uses four nearest local light sources per camera, shared across the
  world's custom shaders. This is a rendering budget, not a cap on placed fires
  or heat simulation. There are no point-light shadow maps. Daylight exposure
  reduces visible firelight relative to the sun. Crackle is currently a small
  procedural spatial placeholder, not an AI-generated or audition-approved take.
- Right-click an actual branch/pole: plant it. Striking the placed stick with a
  rock changes the stored embedding depth, bounded by soil depth and stick
  length. The visible shaft retains its original dimensions and branch variant.
  Later structural loading/tying of members is outside this update.
- Z opens sleep/rest on bare ground; E on bedding allows sleep there. One, four
  and eight-hour requests accelerate world time 32 times; Space/Escape wakes.
  Exposure, hunger, thirst and fuel continue during sleep. Damage or dangerous
  core temperature interrupts it. There is no offline time progression.

Tiredness is independent of energy. Awake rest can recover energy while sleep
pressure increases. Sleep reduces tiredness according to bed comfort. Severe
tiredness modestly reduces movement and stamina recovery; maximum stamina is
still bounded by energy. Tiredness is visible in inventory and the rest dialog;
the owner's existing survival HUD design is retained.

Camp objects store planet positions, exact embedded depth and original material
identities. Save snapshots deep-copy them and reject ownership duplication
between carried and placed items. Old saves default to an empty camp and zero
tiredness. There are no global building blueprints, NPC camp generation, full
wildfire spread, cooking, water containers or new shelter system in this pass.

Research informing this approximation:

- [Grass bedding at Border Cave, approximately 200,000 years ago](https://digitalcommons.usf.edu/kip_articles/6115/),
  original research record by Wadley and colleagues; grass bedding is appropriate
  well before the later Paleolithic setting.
- [NIOSH: homeostatic sleep pressure](https://www.cdc.gov/niosh/work-hour-training-for-nurses/longhours/mod2/11.html),
  supports separating sleep pressure from immediate physical energy. Numerical
  rates here are game balance, not a clinical model.
- [Washington State Parks: tinder, kindling and fuel](https://parks.wa.gov/about/news-center/field-guide-blog/your-guide-building-safe-campfire),
  supports staged fire building. The hand-drill workflow is a plausible
  reconstruction, not a claim of universal use by all Paleolithic populations.

Initial verification: 31 camp/material/sleep/save checks, 784 portable crafting
checks, 75 inventory, 160 thermal/HUD, 67 mode and 33 foraging checks pass.
Native evidence is under artifacts/camp-water-20261029; earlier failed native
runs are retained and are not pass claims. Final native results will be recorded
there and in the repository handoff before release selection.

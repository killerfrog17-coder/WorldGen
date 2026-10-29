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
  reduces visible firelight relative to the sun. Crackle uses three local Stable
  Audio 3 mono recordings with independent playback offsets. Files are under
  `sounds/campfire`; deleting a disliked take skips it at the next world load.
  Output has automated content diagnostics, not human listening approval.
  Item effects volume controls it. Voices persist through fuel-mesh rebuilds.
- A small bounded smoke emitter follows local wind without light/shadow maps.
  Bedding follows the local ground plane on gentle slopes; its interaction
  bounds include that rotation.
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

Verification: 39 core camp/material/sleep/save checks, 784 earlier portable
crafting checks, 75 inventory, 165 fall/death, 37 drinking/swimming, 160 earlier
thermal/HUD, 67 mode and 33 foraging checks pass. The combined packaged camp pass
has 55 native checks, including real firelight compared with a same-frame
disabled-light control, contact burns, ember expiry, sleep and save serialization.
The combined fixture additionally crafts its tinder, kindling and hand drill
through the actual timed actions while restoring an originally held spear.
It also verifies third-person targeting/hammering and exact reuse of the worker's
fitted river data, plus worn-pack placement and recovery without stale container
ownership. Evidence is under `artifacts/camp-water-20261029`; failed
attempts are retained and are not pass claims. The final IL2CPP run additionally
checks real mono recording loading and continuity, valid bounded smoke curves,
and visible-terrain support for bedding and buried posts. Visuals, interaction
rays and contact burns follow the rendered terrain mesh; analytic height is used
only before a visible mesh is available. This prevents small hovering gaps.

Loose bedding uses material volume, rather than treating a hide as an enormous
grass bundle. Hide improves insulation/comfort without inflating bed thickness.
The ground targeting ray caches short-lived stationary results and grass
gathering follows the actual grass slope admission, including the cliff veto.

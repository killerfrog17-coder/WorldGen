# Placed materials, campfires and sleep

The current camping system combines directly placed fire/bedding with adjustable
structure blueprints. Current shelter review evidence is recorded in
`assets/verification/paleolithic-shelters-20261102.json`; the existing root shortcut
selects the checked managed development player in one of the two fixed build slots.

## Adjustable camp plans

The expanded shared catalog also provides conical tents, ridge/A-frame tents,
oval/long communal huts, open canopies, bone-footed lodges, stone-founded huts,
stone windbreaks, sleeping platforms, raised caches and hide-working frames.
These are construction families: the same dome can use brush, reed thatch,
bark or actual hides. Dimensions, costs, entrance anchors, sleeping capacity
and functions live in `CampBlueprints.Catalogue`, shared with future NPC camp
layout. No NPC camps are spawned by this update.

Hide panels retain each contributed animal's species, coat, seed and identity.
The renderer samples the original donor coat array. A panel completed with two
different hides displays both, rather than assigning a fixed tent texture.
Remaining hide mass stays in the inventory; dismantling returns the installed
pieces. Added covering can mix donors too. Bindings and stitched edges keep
their separate rawhide appearance.

| Added family | Dimensions | Function / material variation |
| --- | --- | --- |
| Conical tent | Diameter 2–6 m | Pole frame, open doorway, crown vent, stone edge anchors; four coverings |
| Ridge tent / A-frame | Width 1.5–5 m, depth 2–6 m | Paired supports, repeated ridge bays, closed rear and front doorway |
| Oval / long hut | Width 2–4 m, depth 2–8 m | Bent-branch roof bays and rounded ends; individual or communal shelter |
| Open canopy | Width/depth 1.5–5 m | Rain/shade cover with largely open sides |
| Bone-supported lodge | Diameter 2–6 m | Actual bone/antler footing material, wood upper frame, four coverings |
| Stone-founded hut | Diameter 2–6 m | Low dry-laid walls, organic upper frame/roof, four coverings |
| Stone windbreak | Length 1–6 m | Four stone courses, lee-side wind protection, no roof or bindings |
| Sleeping platform | Width 1–3 m, depth 1.75–3 m | Branch deck, layered covering, actual sleep/rest and ground insulation |
| Hide-working frame | Width 1–3 m | Braced upright frame holding one actual retrievable hide |
| Raised cache | Width 1–3 m, depth 1–2.5 m | Elevated roofed food/equipment stock; physical, dimension-limited storage |

V opens camp plans. E builds, uses stores/racks or opens the completed structure
details; Shift+E inspects an unfinished plan. Creative mode also provides a
"Complete plan (creative)" button for fast review. Sleeping
platform details offer rest/sleep. The plans list scrolls. Existing fireplaces
and bedding remain direct placement. Existing saved structures keep their
original component keys. A footprint requiring unsupported pole lengths is
rejected rather than offering an impossible-to-finish plan.

This pass covers surface-built camp architecture. Natural caves are terrain,
not a manmade blueprint. Excavated floors/storage pits and snow-volume refuges
require persistent terrain/snow-volume editing; they are not represented by
fake holes, solid-ground cellars, or permanent snow blocks in warm climates.
They remain separate terrain features, rather than duplicates of the roof
families above. There are no milled-plank cabins, masonry vaults, metal
fasteners or farming-era buildings in this catalog.

Research basis: [Ohalo II brush huts](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0262434),
[Kharaneh IV construction research](https://hearstmuseum.berkeley.edu/exhibit/experimental-archaeology-thatched-hut-construction/),
[Étiolles tent layouts](https://archeologie.culture.gouv.fr/etiolles/fr/les-habitations-1),
[Mezhyrich fieldwork](https://vovkcenter.org.ua/en/2018/12/01/mezhyrich-2018-summer-school/),
and [Wadi Hammeh 27 stone-founded buildings](https://www.antiquity.ac.uk/projgall/edwards347).
The adjustable roof shapes and auxiliary racks/caches are gameplay engineering
variants of the material kit. The existing illustrated shelter report supplies
additional site references and examples.

Press **V** to select a plan. B remains the backpack key. Choose vegetation,
hide or bark covering for a shelter/screen, or the material-store category.
Click the first corner/end, look toward the opposite corner/end, then click to
confirm. A hut uses centre and radius instead. R rotates in 15-degree steps;
Shift+R reverses rotation. Right-click backs up one placement step, then cancels.
The translucent preview shows the actual dimensions and current requirements.

| Plan | Adjustable dimensions | Function |
| --- | --- | --- |
| Lean-to | Width 1.5–4 m; depth 1.5–3 m | Directional wind shelter, shade and rain cover |
| Windbreak | Length 1–6 m | Directional shelter on the lee side; adjacent ends can join |
| Drying rack | Length 1–3 m | Two rails accepting suitably sized meat/fish pieces |
| Material store | Width 1–4 m; depth 0.75–3 m | Physical wood, stone or vegetation storage |
| Brush hut | Diameter 2–4 m | Enclosed shelter with an open doorway and low curved roof |

Dimensions snap to quarter-metre increments. Larger plans require more or longer
real pieces, more binding, and more covering. Supports follow a saved terrain
survey; placement rejects water, solid obstructions and excessively uneven
ground. A branch is never stretched into a longer beam. Wider lean-tos introduce
additional support bays.

Aim at an unfinished plan and press **E** to add a suitable held/nearby material
and work on a component. Construction proceeds through supports, frame,
bindings and covering. Labour uses solar time and the item-crafting multiplier.
Movement, E, Escape or death interrupts work without discarding progress;
previous hands are restored. **Shift+E** opens details and disassembly controls.
Creative mode supplies components without material or labour costs.

Wood must have suitable length, thickness and condition. Trimming excessive
length requires a cutting edge and returns an actual offcut. Bindings consume
part of a real cordage/sinew/rawhide item. Finished weapons are not automatically
consumed as construction wood. Completed coverings can receive extra material,
with diminishing protection gains. Dismantling returns installed materials and
stored contents at their physical positions.

Completed structures have solid two-sided collision. The player crouches under
low roofs and cannot stand through them. Shelter protection affects the existing
wind, rain and solar-exposure calculations. Overlapping shelters do not multiply
protection without limit.

E on a completed store/rack opens its contents. Stored items appear in Nearby,
can be used for nearby crafting, and do not count as carried weight. Capacity is
based on the plan dimensions and actual item sizes. Rack drying accumulates with
temperature, wind, humidity and rain; a nearby modest fire assists smoking.
Drying preserves calories and source-animal identity while reducing water/mass.
This does not introduce a general food-spoilage simulation.

Fire and bedding remain direct material placement, rather than blueprints:

- E on grassy ground: gather a physical grass bundle. Tree resource menus also
  supply leaf litter; existing reed gathering remains available.
- Right-click grass, reeds, leaves or hide: spread bedding. Add further layers
  through E on the bedding. Stored material mass controls depth and comfort;
  rain wets it and wind/sun dry it. Hides retain their existing provenance.
- Right-click fine tinder: place a fire lay. Aim at it, scroll through the
  contextual item icons, and press E to add fuel, light it, cook food or retrieve
  food. No fire menu is needed. Raw dry grass can be added directly. A stone
  ring is not a mandatory ignition gate.
- Shape a spindle/hearthboard set from two suitable wood pieces with a cutting
  edge; shred grass/bark/fibre into tinder; split wood into kindling. These are
  existing contextual, timed crafting actions, not a new recipe browser.
- Lighting with a hand drill is timed work, borrows the actual tool and restores
  prior hands. A cold fire lay needs dry tinder and thin kindling before thicker fuel.
  Long branches become short fuel pieces with exactly the same total mass.
  Fuel mass, moisture and wind control burn time and flame strength; a substantial
  load lasts hours of solar time and dwindles into embers. Feeding live coals
  eligible dry tinder or kindling revives flames directly, without a drill or
  a separate ignition action. A healthy coal bed can also catch thin wood.
  Dry grass, leaf litter, reeds, bark strips and raw/prepared fibre are eligible
  alongside prepared tinder, using the same rules as fire-lay placement. Wet fuel
  cannot bypass ignition checks; cold ash is not an ignition source.
  Tending intervals are roughly twice the former duration for fine fuel and
  three times for wood, with longer-lived coals. Rain can extinguish an exposed fire. The camp
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
  Existing saved free-form frames remain supported; the normal construction
  path is now the adjustable plan system.
- Placement shows a translucent object at the aimed ground point. Rock strikes
  use forgiving post bounds and a separate 0.3–0.6 second anti-spam interval,
  independent of the rock's slower combat recovery.
- Z opens sleep/rest on bare ground; E on bedding allows sleep there. Sleep has
  no duration picker, clock or countdown. Pressure, body-clock phase and daylight
  determine waking, including shorter daytime bouts. Nearby moving animals or
  loud sounds can interrupt sleep, as can damage and dangerous core temperature.
  Sleep accelerates time 32 times; Space/Escape wakes voluntarily. Exposure,
  hunger, thirst and fuel continue. There is no offline time progression.
- Meat, fish, roots and mushrooms cook over accumulated fire/ember heat and char
  if left too long. E retrieves the same physical item. Cooking does not remove
  mushroom toxicity. Raw-meat illness remains deferred as requested.

Tiredness is independent of energy. Awake rest can recover energy while sleep
pressure increases. Sleep reduces tiredness according to bed comfort. Severe
tiredness modestly reduces movement and stamina recovery; maximum stamina is
still bounded by energy. Tiredness is visible in inventory and the rest dialog;
the owner's existing survival HUD design is retained.

Camp objects store planet positions, exact embedded depth and original material
identities. Save snapshots deep-copy them and reject ownership duplication
between carried and placed items. Old saves default to an empty camp and zero
tiredness. Partial and completed blueprints, terrain footings, stored materials
and drying progress also round-trip through saves. NPC camp generation, full
wildfire spread and new character construction animations are outside this pass.

One grass bundle gives modest comfort. Further layers improve it with diminishing
returns; a hide improves insulation without becoming a thick mattress.

## Physiology and feedback revision

The stomach shows felt hunger, not remaining life. Food enters a digestive store
and then replenishes body reserves. Physiological days follow solar days regardless
of the 24-day historical year. Activity/cold increase energy use. In the current
focused tests, a healthy, hydrated character lasts about 34 days without food at
rest, or 23 under continuous activity. These are gameplay approximations, not a
promise about human survival. Comfortable unwatered survival is about 3.1 days;
heat, activity and illness can shorten it. Old saves receive normal reserves
rather than retroactive starvation.

The owner's HUD is retained. Accumulated thermal glow rises earlier: half of the
stress range produces 85% of the visual range, without changing death thresholds.
Frost/heat edges, bounded camera shivering, low-health desaturation/blur and pain
flashes supply feedback beyond the HUD. Healthy frames skip the extra screen pass.

Eight body-foley groups have three locally generated variants each: eating,
drinking, stomach, dry throat, yawning, panting, pain and shivering. They use item
effects volume. Files are in `sounds/player-survival`; provenance and automated
content checks are in `assets/player-audio/provenance.json`. No human listening
approval is claimed. These small files are tracked directly in Git.

Research for this revision:

- [Human survival physiology review](https://physoc.onlinelibrary.wiley.com/doi/full/10.1113/EP093194).
- [National Academies: water intake and losses](https://www.nationalacademies.org/read/10925/chapter/6).
- [Yetish et al.: natural sleep in preindustrial societies](https://teams.semel.ucla.edu/sleep-research/publication/natural-sleep-and-its-seasonal-variations-three-pre-industrial-societies).
- [US Forest Products Laboratory: wood energy](https://www.fpl.fs.usda.gov/documnts/fpmu/biomass_energy/basic_wood_energy_information.pdf).
- [Stable Audio 3 prompting guide](https://stability.ai/guides/stable-audio-3-prompt-guide).

## Felt hunger and thirst HUD

The stomach is approximately half-full after one comfortable solar day without
food. Activity and cold can advance that need faster. Ordinary fasting then
levels off above empty while usable body reserves remain; the last sliver goes
during actual reserve depletion and starvation damage. A saved food-need signal
tracks adequate intake, so a few berries do not reset a whole day. Older saves
initialize it from existing hunger and reserve data. These display changes do
not shorten the existing starvation/dehydration survival times.

The droplet reaches half at about one litre of water deficit: roughly ten
comfortable hours with the current baseline losses, sooner when sweating or
exercising. At a full comfortable day without water it still has a discernible
fill. Near-empty is reserved for severe dehydration. Need reminders throb in
short bouts, with quiet gaps that shorten as need increases. The original icon
shapes, nausea tint, temperature display and health/energy/stamina rings remain.

[A water-restriction study](https://www.sciencedirect.com/science/article/pii/S0031938420305254)
reported rising thirst/mouth dryness from about four hours, while
[24-hour deprivation research](https://pubmed.ncbi.nlm.nih.gov/7001928/)
found marked thirst and unpleasant dry-mouth sensations. The half-icon mapping
is gameplay tuning based on water deficit, rather than a universal human clock.

Current focused checks pass: 24 physiology/camp-experience, 39 camp, 67 modes,
161 HUD, 37 water and 33 foraging. The final IL2CPP build passes 97 native checks with a clean log, including actual
E/scroll fire controls, food ownership/cooking, heavy-rock hammer timing, close
noise waking, real audio playback and screen-effect pixel comparisons. Final
captures were inspected, including revised irregular frost. Evidence is under
`artifacts/survival-camp-20261029/native-final`; failed earlier fixtures are not
pass claims. The evidence below is historical.

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
has 57 native checks, including real firelight compared with a same-frame
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
An additional 21-check packaged save-library run exercises actual named saves,
overwrites, F5, background actions and full world re-entry with bedding, a planted
post, a burning fire and tiredness. Capturing into a separate session deep-copies
the camp and its materials, as it already does for inventory and wildlife.

Loose bedding uses material volume, rather than treating a hide as an enormous
grass bundle. Hide improves insulation/comfort without inflating bed thickness.
The ground targeting ray caches short-lived stationary results and grass
gathering follows the actual grass slope admission, including the cliff veto.

## Follow-up QA candidate

The selected release above remains unchanged while a separate candidate is
checked. Follow-up fixes release the owned raw-wood meshes when posts/fuel are
rebuilt or removed, consume placement clicks before weapon input, and interrupt
sleep on damage even after prior healing. A 71-check Mono run passed those
paths, including actual virtual keyboard/mouse input through the player loop.

The subsequent 73-check camp candidate also verifies placing material cannot
pass through intervening solids, then becomes available immediately on removing
the obstruction. A separate 21-check water run verifies shallow-water rest and
flooded bedding are rejected, with drinking/swimming/underwater paths retained.
Those candidate runs passed without runtime errors. At the owner's request,
the existing shortcut initially selected a Mono candidate. The owner rejected
Mono and requires IL2CPP for every player build. The shortcut now selects its
IL2CPP replacement built at 2026-10-29T18:07:39Z, including the general bank
stitching change. It passes native boot/terrain upload and source/asset checks;
the final rendered bank check remains pending. The earlier fully tested IL2CPP
package retains its original gameplay evidence.

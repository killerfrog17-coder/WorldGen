# Portable technology prototype

This is a broad first pass of portable crafting, not a claim that every society
had every technique throughout the Paleolithic. No in-game recipe book, global
recipe browser, or crafting-instruction command is provided. Actions appear on
the relevant item's right-click menu. An unavailable action explains its current
missing requirement. The following table is developer documentation only.

## Using the checkpoint

`/give materials` supplies raw test inputs: fibres, bark sheets, reeds, deer hides,
bones, antler, sinew and feathers. It does not supply tools, completed equipment
or a starter bag. Free hands and worn storage are used first; overflow drops
nearby. `/give stone flake` and `/give small rock` remain useful for testing.
`/give list` lists item names, without recipes. Every new item can also be given
individually, e.g. `/give bone needle`, `/give hide pack` or `/give cordage 5`.
`/give deer hide coat=1` retains the actual deer coat index; other supported fauna
names can supply hide, bone and antler. These commands are explicit test supply,
not a new natural harvesting system or an automatic loadout.

Right click a physical primary item, including one in Nearby. Tools and other
materials can be held, packed in accessible carriers or on the ground in reach.
Work automatically stows unrelated held objects into suitable attachments or
worn carriers, otherwise physically drops them, then restores the prior hands.
Starting a project reserves its
specific materials; all involved icons share progress. Work is timed by the
existing solar-day/crafting multiplier, pauses on movement or interruption, and
can resume. Cancel retains all materials. Completion consumes only the selected
ingredients and transforms the original workpiece identity; tools remain.

## Implemented scope

- Binding: separated fibres, cordage, thread, sinew cord, bark/rawhide strips,
  split reed weaving material.
- Stone work: blades, scrapers, choppers, handaxes, spear points, arrowheads and
  borers, alongside existing flake production.
- Bone/antler: awls, needles, points, barbed points, fishing gorges/hooks and
  retouchers. Tool capabilities enable corresponding crafting actions.
- Wood/assembly: handles, notched shafts, an explicitly unbound spear stage,
  bound stone/bone/barbed spears, hafted knives, axes and adzes.
- Ranged/fishing equipment: arrow shafts, unfletched/fletched arrows,
  bow stave/self bow, spear-thrower/dart, sling, line/hand-line and small net.
- Functional storage: hide pouch, folded bark container, twined basket, satchel,
  carrying frame, hide pack, quiver, fibre/hide belts, needle case and sheath.

The October 28 item-use pass adds melee, blocking, recoverable physical
projectiles, simple hand-line/net fishing and contextual tool work. See
[ITEM_USE_OVERNIGHT.md](ITEM_USE_OVERNIGHT.md) for controls and limits. No traps, fire,
construction, clothing system, butchery or tanning chemistry was added. Working
hide is an abstract scraping/softening sequence, not a claim that untreated hide
becomes durable waterproof leather just by rubbing it. Containers do not yet
carry loose liquids; the bark container is ordinary dry-item storage.

## Carrying and appearance

Back, shoulder and waist are separate attachment slots, each with one carrier.
Their storage grids differ. R searches worn carriers for a suitable free space.
Quivers accept arrows; cases/sheaths restrict contents to suitable small tools.
Belts expose an attachment layout for tools, not general bag storage. Belt tools
appear on the body. Bags can be physically dropped with contents retained and
their contents stay accessible while the bag is nearby. A loaded container cannot
be packed inside another bag; filled tool cases may attach to a belt. Nothing grants invisible body inventory.

Hide work preserves species, coat, appearance seed and source animal ID, plus a
list of material origins for multi-hide products. Actual existing fauna coat
arrays are reused on hide surfaces and icons; numeric torso UV rectangles avoid
eyes, hooves and transparent atlas areas. This samples body fur for a placeholder
pelt, rather than claiming the entire animal skin has been anatomically unwrapped.
Mixed-hide objects retain all origins but currently display the first origin.
Coat assets are shared with the fauna cache, not regenerated or modified. Source
animal meshes, skeletons, textures, animations and bundles are untouched.

Meshes distinguish woven baskets, panelled bags with straps/flaps/stitches,
frames, bone objects, chipped stone, wrapped hafts, bows and fletched shafts.
They remain procedural placeholders. The October 28 pass adds shared material
surfaces, actual-model thumbnails, broad white pickup masks and locally generated
Foley alongside existing stone contacts. No attack, throw or crafting animation
pass has been added. A successful melee hit has damage, blood, sound and animal
reaction; an attack into empty air has no visible swing yet.

## Archaeological grounding and uncertainty

Composite bindings, plant fibre processing and sewing are supported by evidence;
availability varied greatly with period and region. This prototype deliberately
does not impose the future character knowledge or regional technology gates.
Examples informing the chains:

- Hardy et al., direct Middle Paleolithic cordage evidence:
  <https://www.nature.com/articles/s41598-020-61839-w>.
- Gilligan et al., eyed needles and the development of sewing:
  <https://doi.org/10.1126/sciadv.adp2887>.
- Projectile evidence and regional variability:
  <https://doi.org/10.1016/j.quascirev.2020.106295>.
- Paleolithic spear-throwers:
  <https://www.cambridge.org/core/journals/proceedings-of-the-prehistoric-society/article/abs/palaeolithic-spearthrowers/7DCFBC801A31F0CF4423B90F58847B54>.
- Early Australian ground-edge axe evidence (not a universal European technology):
  <https://www.sydney.edu.au/news-opinion/news/2016/05/11/world_s-oldest-axe-fragment-found-in-australia.html>.
- Upper Paleolithic fibre technology and impressions:
  <https://www.journals.uchicago.edu/doi/10.1086/420907>.
- Later surviving basketry illustrates preservation bias, not proof that this
  exact pack design existed in the Paleolithic:
  <https://www.science.org/doi/10.1126/sciadv.adi3055>.

Specific pouch, pack, frame, belt and sheath shapes are plausible functional
reconstructions, not excavated universal designs. Slings and some regional tool
combinations have uncertain direct evidence. Future knowledge/era restrictions
must distinguish plausible reconstructions from secure dated finds.

## Internal crafting tree

The table below is generated from the authoritative core definitions. A slash
means alternative input kinds or tool capabilities, not simultaneous tools.

| Action | Primary | Additional materials | Tool capabilities | Work seconds* |
|---|---|---|---|---|
| Separate and prepare fibres | RawFibre | None | Hands | 120 |
| Scrape inner-bark fibres | BarkStrips | None | Scrape | 180 |
| Twist a cordage coil | PreparedFibre | None | Hands | 360 |
| Twist fine thread | PreparedFibre | None | Hands | 420 |
| Separate and twist sinew | Sinew | None | Hands | 240 |
| Split weaving strips | ReedBundle | None | Cut | 150 |
| Cut bark into tying strips | BarkSheet | None | Cut | 120 |
| Flesh and scrape hide | RawHide | None | Scrape | 600 |
| Work and soften hide | ScrapedHide | None | Hands | 900 |
| Cut rawhide binding strips | ScrapedHide / WorkedHide | None | Cut | 150 |
| Shape a cutting blade | StoneFlake | None | Strike | 180 |
| Shape a scraping edge | StoneFlake | None | Strike | 150 |
| Chip a stone chopper | SmallRock / LargeRock | None | Strike | 360 |
| Shape a bifacial handaxe | SmallRock / LargeRock | None | Strike | 900 |
| Shape a spear point | StoneFlake / StoneBlade | None | Strike | 300 |
| Shape a small arrowhead | StoneFlake | None | Strike | 240 |
| Shape a stone borer | StoneFlake | None | Strike | 180 |
| Carve a bone awl | Bone | None | Cut / Scrape | 420 |
| Carve and pierce a needle | Bone | None | Scrape / Pierce | 720 |
| Carve a bone point | Bone | None | Cut / Scrape | 480 |
| Carve a barbed antler point | Antler | None | Cut / Scrape | 720 |
| Carve a fishing gorge | Bone | None | Cut | 240 |
| Carve and pierce a fishhook | Bone | None | Scrape / Pierce | 600 |
| Shape an antler retoucher | Antler | None | Cut / Scrape | 300 |
| Carve a short tool handle | Branch / TrimmedPole | None | Cut | 240 |
| Cut a spear-shaft seat | TrimmedPole | None | Cut | 180 |
| Seat stone point on shaft | NotchedShaft | 1 x StonePoint | Hands | 90 |
| Bind a stone-tipped spear | LooseStoneSpear | 2 x binding (cord/rawhide/sinew) | Hands | 300 |
| Bind a bone-tipped spear | NotchedShaft | 1 x BonePoint; 2 x binding (cord/rawhide/sinew) | Hands | 300 |
| Bind a barbed spear | NotchedShaft | 1 x BarbedPoint; 2 x binding (cord/rawhide/sinew) | Hands | 300 |
| Haft a stone knife | StoneBlade | 1 x WoodHandle; 1 x binding (cord/rawhide/sinew) | Hands | 360 |
| Grind and haft a stone axe | Handaxe | 1 x WoodHandle; 2 x binding (cord/rawhide/sinew) | Grind | 900 |
| Grind and lash a stone adze | StoneChopper | 1 x WoodHandle; 2 x binding (cord/rawhide/sinew) | Grind | 900 |
| Split and shape an arrow shaft | Branch / TrimmedPole | None | Cut | 360 |
| Bind an arrowhead | ArrowShaft | 1 x Arrowhead; 1 x binding (cord/rawhide/sinew) | Hands | 180 |
| Fletch an arrow | UnfletchedArrow | 1 x Feather; 1 x thread or sinew cord | Hands | 240 |
| Carve a self-bow stave | TrimmedPole | None | Cut | 1200 |
| String a self bow | BowStave | 2 x binding (cord/rawhide/sinew) | Hands | 180 |
| Carve a wooden spear-thrower | TrimmedPole / Branch | None | Cut | 600 |
| Carve an antler spear-thrower | Antler | None | Cut / Scrape | 720 |
| Shape a dart shaft | TrimmedPole | None | Cut | 360 |
| Bind a spear-thrower dart | DartShaft | 1 x StonePoint / BonePoint; 1 x binding (cord/rawhide/sinew) | Hands | 240 |
| Knot a sling with hide pocket | Cordage | 1 x Cordage; 1 x RawhideStrips | Hands | 300 |
| Twist a fishing line | Thread / SinewCord | 1 x thread or sinew cord | Hands | 180 |
| Tie a hook to a hand-line | FishingLine | 1 x Fishhook / FishingGorge | Hands | 120 |
| Knot a small fibre net | Cordage | 3 x Cordage | Hands | 900 |
| Cut and lace a hide pouch | WorkedHide | 1 x binding (cord/rawhide/sinew) | Cut / Pierce | 480 |
| Fold and lace a bark container | BarkSheet | 1 x binding (cord/rawhide/sinew) | Pierce | 420 |
| Twine a carrying basket | WeavingStrips | 2 x WeavingStrips; 1 x binding (cord/rawhide/sinew) | Hands | 1200 |
| Sew a hide satchel | WorkedHide | 1 x Cordage; 1 x thread or sinew cord | Cut / Sew | 900 |
| Lash a wooden carrying frame | TrimmedPole | 1 x TrimmedPole; 3 x binding (cord/rawhide/sinew) | Cut | 600 |
| Fit hides to a carrying frame | CarryFrame | 2 x WorkedHide; 3 x binding (cord/rawhide/sinew) | Pierce | 900 |
| Sew a hide quiver | WorkedHide | 2 x binding (cord/rawhide/sinew); 1 x thread or sinew cord | Sew | 720 |
| Braid a fibre belt with tool loops | Cordage | 1 x Cordage | Hands | 360 |
| Cut and lace a hide belt | WorkedHide | 1 x binding (cord/rawhide/sinew) | Cut / Pierce | 360 |
| Carve and close a bone needle case | Bone | 1 x binding (cord/rawhide/sinew) | Cut / Pierce | 420 |
| Cut and lace a knife sheath | WorkedHide | 1 x binding (cord/rawhide/sinew) | Cut / Pierce | 480 |

*Work seconds are nominal action time, compressed by solar-day length and scaled by
the separate crafting multiplier. These are prototype balance values, not
experimental archaeology measurements.

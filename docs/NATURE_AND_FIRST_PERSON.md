# Gathering, food, corpse loot and first-person item motions

This update uses existing placed regional vegetation, original animal assets and the existing physical inventory. No new vegetation carpet, re-rigging, building or cooking system is introduced.

## Interaction

Aim at a useful plant or a dead animal within normal reach and press E. Plants offer their relevant gathering actions; corpse search lists finite contents. Items enter a worn carrier if space permits, otherwise a free hand. Ordinary inventory right-click offers eating and a small taste for foods. Food portions have real mass, approximate calories and water content. Full complex skinning/butchering remains deferred.

Reed stands supply reed bundles for the existing split/weave recipes. Woody shrubs and suitable herb forms supply raw fibre; bark extraction and digging fibrous roots remain available. Certain rosettes also offer a root, requiring the existing digging stick and timed work. Bush fruit, accessible tree fruit/nuts and mushrooms use their actual placed species identity. Fruit availability varies seasonally, reverses between hemispheres and has a broader tropical window. Existing habitat, colony, moisture, slope and salinity placement checks remain intact. No assumption that every bush bears berries, every tree bears nuts, or every environment can provide every material.

Gathering records are saved per source and product. Accessible fruit/material yields are modest bundles/portions, rather than an entire plant or tree crop. Mushroom picking removes the visible fruiting body; these do not yet regrow. Other products replenish at the next gameplay calendar year. Vegetation does not yet visually change between ripe and depleted states. Annual allocation and season windows are gameplay approximations, not a full phenology model.

Corpse loot is generated once, lazily on first search. Remaining physical portions stay assigned to the saved corpse, contribute no carried mass and do not become loose ground clutter. Hide keeps the actual source model, coat and animal identity. Meat/tissue yield scales with mass; bones, sinew, feathers and appropriate hide/antler differ by animal type. Large carcasses use larger slabs to avoid producing tens of thousands of inventory objects. No automatic meat drops, human harvesting, raw-meat illness or spoilage system.

Nutrition is approximate: berries/fruit roughly 35?75 kcal/100g; mushrooms 22?34; roots 65?110; nuts 480?600; raw lean animal flesh roughly 110?120; fat 850. 2,400 kcal replaces a resting gameplay day's hunger drain; existing activity/exposure increases need. Plant-only survival is not artificially forbidden: safe concentrated seasonal foods can be valuable, but small berry/mushroom portions alone require substantial gathering.

Existing generated bush/tree flesh/seed toxicities are used. Mushroom/root hazards are deterministic derived traits of the generated species. Small tasting consumes actual mass, may cause delayed poisoning, and never labels food safe. Taste and the absence of early symptoms do not certify edibility. Poison exposures and their remaining delay/dose are saved; serious doses can kill through the existing death system. Raw animal flesh deliberately has no poisoning penalty in this pass.

First-person item transforms now provide a short anticipation/contact/recovery for thrusts and swings, charge poses for throws/bow/sling/atlatl, guard poses and repetitive tool/crafting motions. Existing combat calculations, projectiles and third-person skeleton animations remain authoritative. This is rudimentary action feedback, not authored arm/hand animation or a new swept-hit collision system.

## Research grounding

* [NOAA: cattail uses, including mats, cordage and basketry](https://sanctuaries.noaa.gov/mallows-potomac/visit/trail-signs/ahpuwiyazq.html).
* [NPS: cattails occur in muddy wet soil](https://www.nps.gov/places/the-sunken-forest-tour-cattail.htm) and [their edible parts vary with season](https://www.nps.gov/miss/learn/nature/cattails.htm). Generated reed species are not all asserted to be edible cattails.
* [USDA FSIS: no general rule distinguishes edible and poisonous mushrooms](https://ask.fsis.usda.gov/article/Is-mushroom-poisoning-caused-by-molds).
* [Iowa State Extension: mushroom toxins and taste are unrelated](https://web-dfsr.s3-us-gov-west-1.amazonaws.com/Iowa/assets/File/Safe%20Mushroom%20Foraging_ISU_Publication.pdf).
* [Harvard Nutrition Source: mushrooms are low in calories](https://nutritionsource.hsph.harvard.edu/food-features/mushrooms/).
* [Texas Parks & Wildlife: comparative game-meat nutrition](https://tpwd.texas.gov/huntwild/hunt/wildgame-recipes/nutrition.phtml).
* [USDA Forest Service: traditional acorn processing and leaching](https://www.fs.usda.gov/psw/publications/documents/psw_gtr044/psw_gtr044_004.pdf). Unprocessed generated nuts can remain toxic; this pass does not pretend all nuts are safe raw or add an unimplemented leaching action.

These support material sources and broad nutritional/toxicity relationships. Exact invented-species yields, windows, damage and delays are explicit game design approximations.

## Verification

Focused Core checks: 33 food/foraging, 75 physical inventory, 755 portable technology, 77 item-use and 69 named-save/shared-world checks pass. The portable-tree reachability check explicitly treats gathered/looted foods as raw inputs, retaining all earlier craftable-item coverage. The selected release passes 30 native integration checks: actual corpse skin targeting, finite loot, coat provenance, eating, save round-trips, actual placed-plant gathering/depletion, calendar renewal, first-person spear/throw/bow poses, all eight new food thumbnail renders, and command-corpse cleanup. There are no native runtime errors. Food contact sheet and spear/bow renders were inspected. Preservation verifies 578 original asset/DLL hashes, 340 frozen production inputs, 177 packaged collision skins/source IDs and 18 existing audio hashes. This is not an exhaustive ecological field study or an FPS benchmark. Evidence: artifacts/nature-actions-20261028.

Selected package: `artifacts/player-nature-actions-ready/WorldGen.exe`, IL2CPP release, BuiltUtc `2026-10-29T00:07:53.2603700Z`. This artifacts directory is a junction to `E:/WorldGen-builds/player-nature-actions-ready`; preserve the target as well. The existing root shortcut is selected; the previous save-slots release is retained.

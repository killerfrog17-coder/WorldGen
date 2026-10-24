"""Build the full editable fauna asset plan from the owner's existing roster."""
import argparse,csv,json,re
from pathlib import Path

ap=argparse.ArgumentParser();ap.add_argument('--roster',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
a=ap.parse_args();a.output.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader(a.roster.open(encoding='utf-8-sig')))
priority=['Deer','Hare','Rabbit','Butterflies and moths (guild)','Fireflies (derived lineage)','Red fox','Wolf','Wild boar','Mouflon','Wild goat','Bear','Bison','Bees (guild)','Dragonflies (guild)','Native detritivorous flies (guild)','Duck','Pheasant','Small native freshwater fish (guild)','Mouse','Squirrel','Horse','Reindeer','Moose','Wild cattle','Grouse','Quail','Turkey','Junglefowl','Raccoon','Badger','Beaver','Otter','Lynx','Mountain goat','Owl','Raven','Deer']
priority=list(dict.fromkeys(priority));order={n:i for i,n in enumerate(priority)}
audio={'Cicadas (derived lineage)','Crickets (derived lineage)','Katydids (derived lineage)','Native frogs (guild)','Native insectivorous understory birds (guild)'}
simple={'Butterflies and moths (guild)','Fireflies (derived lineage)','Bees (guild)','Dragonflies (guild)','Native detritivorous flies (guild)'}
canids={'Red fox','Wolf','African hunting dog','Spotted hyena'}
cats={'Jaguar','Leopard','Lion','Lynx','Ocelot','Puma','Tiger'}
bovids={'Bison','Buffalo','Gazelle','Ibex','Impala','Mouflon','Musk ox','Oryx','Pronghorn','Wild cattle','Wild goat','Wildebeest','Yak'}
deer={'Deer','Duiker','Moose','Reindeer'}
equids={'Horse','Wild ass','Zebra'}
pigs={'Wild boar','Forest hog','Warthog','Collared peccary'}
wet={'Beaver','Capybara','Hippopotamus','Nutria','Otter','Platypus','Sea otter'}
bears={'Bear','Polar bear','Giant panda'}
special={'Aardvark','Anteater','Armadillo','Echidna','Elephant','Giraffe','Hedgehog','Pangolin','Porcupine','Rhinoceros','Sloth','Tapir'}

# Concrete initial coat/material sets. A coat is a mask/palette configuration,
# not a request for a separate generated model or an independent texture atlas.
coats={
'Deer':'tawny woodland|grey upland|snow-pale winter|dark woodland|dappled spotted|faint striped thicket',
'Hare':'brown agouti summer|grey upland|white winter with dark ear tips|sandy dryland|dark peatland',
'Rabbit':'grey-brown agouti|warm brown|dark brown|pale sandy|winter-pale hypothetical',
'Red fox':'red black-legged white-bellied|silver melanistic|cross-pattern dark shoulders|pale dryland|winter-grey northern analogue',
'Wolf':'grey agouti|warm tawny|black melanistic|white northern|dark brown',
'African hunting dog':'irregular black tan white patches|dark-dominant mottled|pale tan-dominant mottled|white-tail-tip bounded variants',
'Bear':'dark brown|cinnamon|near-black|blond brown-bear analogue|grizzled silver-tipped',
'Polar bear':'ivory clean|cream aged coat|wet-darkened ivory|mud-stained temporary overlay',
'Giant panda':'black-white classic|brown-white rare colour analogue|slight white-region warmth variations',
'Bison':'dark umber|warm brown|winter grizzled|pale-brown rare analogue',
'Buffalo':'dark charcoal|mud-brown|grey mature|warm brown juvenile',
'Wild cattle':'dark brown with dorsal stripe|reddish female analogue|blackish male analogue|pale muzzle-ring variants',
'Yak':'black|dark brown|grizzled grey|pale highland hypothetical',
'Musk ox':'dark brown cream saddle|grizzled winter|warm-brown juvenile',
'Mouflon':'brown white saddle|dark winter saddle|light female brown|pale dryland hypothetical',
'Wild goat':'brown dark dorsal stripe|grey winter|warm sandy|dark neck mature-male analogue',
'Ibex':'grey-brown|sandy summer|dark winter|pale ventral region variants',
'Gazelle':'sand white belly dark flank stripe|pale desert|dark upland|reduced flank-band hypothetical',
'Impala':'rufous tan white belly|dark-backed|sandy pale|black facial-mark variants',
'Oryx':'pale body bold face mask|grey-brown body|dark leg markings|reduced mask hypothetical',
'Pronghorn':'tan white rump and throat|dark male cheek patch|pale dryland|dark shoulder hypothetical',
'Wildebeest':'grey blue-black stripes|brown dark face|dark mane variations|reduced striping hypothetical',
'Moose':'dark chocolate|grey-brown|warm juvenile brown|winter grizzled',
'Reindeer':'brown summer pale neck|grey winter cream neck|dark woodland|pale tundra',
'Duiker':'rufous forest|slate-grey|dark dorsal stripe|faint flank bands hypothetical',
'Horse':'bay|black|chestnut|dun with dorsal stripe|grey|bounded white facial and leg markings',
'Wild ass':'grey dorsal stripe|sandy pale|brown-grey|striped lower-leg analogue',
'Zebra':'broad black-white stripes|narrow stripes|brown-shadow stripes|regional stripe-density variants',
'Wild boar':'dark bristled|brown grizzled|grey winter|striped juvenile',
'Forest hog':'dark charcoal|brown-black|grey muzzle|warm juvenile',
'Warthog':'grey-brown sparse hair|warm dust-brown|dark wet skin|pale mud overlay',
'Collared peccary':'dark salt-pepper pale collar|brown-grey|strong collar|subtle collar',
'Camel':'sandy beige|dark brown|pale cream|winter shaggy brown',
'Guanaco':'rufous tan white belly|grey face variant|dark-backed|pale dryland',
'Jaguar':'gold rosettes with central spots|dark gold|melanistic with ghost rosettes|pale upland hypothetical',
'Leopard':'gold rosettes|pale dryland rosettes|melanistic ghost rosettes|grey cool-climate hypothetical',
'Lion':'tawny|pale sand|dark-backed|maned male dark-brown or pale mane',
'Lynx':'grey spotted|rufous spotted|dense winter pale|near-plain spotted-mask reduction',
'Ocelot':'ochre chain rosettes|grey-brown chain rosettes|pale belly and dark bands|dark wetland hypothetical',
'Puma':'tawny|grey-brown|reddish|spotted juvenile',
'Tiger':'orange black stripes|pale orange|white recessive analogue|regional stripe-width variations',
'Raccoon':'grey black eye mask and tail rings|warm brown|dark melanistic analogue|pale grey',
'Badger':'grey coat bold face stripes|warm brown-grey|dark-backed|faint stripe hypothetical',
'Skunk':'black broad white dorsal stripes|narrow stripes|patch-backed analogue|brown-black',
'Wolverine':'dark brown pale flank bands|warm brown|weak-band variant|winter grizzled',
'Weasel':'brown white belly|dark brown|white winter black tail tip where appropriate|sandy dryland analogue',
'Mongoose':'grey-brown|rufous|banded-back analogue|dark woodland',
'Civet':'grey black spots and bands|brown mottled|dark forest|pale mask variant',
'Meerkat':'sandy dark eye patches and dorsal bands|warm brown|grey dryland|reduced bands hypothetical',
'Spotted-tailed quoll':'brown white spots|dark brown white spots|blackish rare analogue|spot-density variants',
'Beaver':'dark brown|reddish brown|near-black|pale brown',
'Otter':'brown pale throat|dark wet brown|grey throat|pale ventral analogue',
'Sea otter':'dark body pale head|brown head|silver-faced mature|dark juvenile',
'Capybara':'warm brown|grey-brown|dark wet coat|pale sandy',
'Nutria':'brown agouti pale muzzle|dark brown|warm rufous|grey-brown',
'Platypus':'dark chocolate|warm brown|pale underside|wet darkening overlay',
'Hippopotamus':'grey-purple skin|warm grey|dark wet skin|pink thin-skin face zones',
'Elephant':'grey wrinkled skin|brown dust overlay|dark wet skin|pale dry dust overlay',
'Rhinoceros':'grey rough skin|brown dust overlay|dark wet skin|pale mud overlay',
'Giraffe':'large angular reticulation|irregular leaf-like spots|pale gaps broad|dark mature-male patches',
'Tapir':'brown-black|black-white saddle analogue|dark grey|striped spotted juvenile',
'Aardvark':'grey-tan sparse hair|warm sand|dark dusty|pale belly',
'Anteater':'grey-black shoulder sash|brown-grey|bold white-edged sash|weak sash hypothetical',
'Armadillo':'grey-brown armour|warm tan|dark plated|pale dryland',
'Pangolin':'brown scales|grey scales|golden-brown scales|dark woodland',
'Echidna':'dark fur pale-tipped spines|brown short spines|grey long spines|dark spine bases',
'Hedgehog':'brown pale-tipped spines|grey spines|warm face|dark mask',
'Porcupine':'black-white banded quills|brown pale-tipped quills|dark crest|warm-brown body',
'Sloth':'grey-brown coarse fur|warm brown|pale face mask|bounded greenish algae overlay',
'Red kangaroo':'rufous male analogue|blue-grey female analogue|sand pale|dark upland hypothetical',
'Wallaby':'grey-brown|rufous shoulders|dark face stripe|pale belly',
'Wombat':'brown dense fur|grey coarse fur|dark brown|pale dryland',
'Koala':'grey white chest|brown-grey northern analogue|dark ears|silver winter',
'Common brushtail possum':'grey|brown|dark melanistic|rufous analogue',
'Long-nosed bandicoot':'brown-grey pale belly|warm brown|faint dorsal bands hypothetical|sandy',
'Mouse':'brown agouti pale belly|grey|sandy|dark forest',
'Squirrel':'red chestnut|grey white belly|black melanistic|dark red',
'Ground squirrel':'grey speckled|brown striped|sandy|dark-backed',
'Marmot':'grizzled brown|grey|golden brown|dark-faced',
'Bank vole':'rufous back grey flanks|dark brown|pale upland|grey-brown',
'Cotton rat':'coarse brown agouti|grey-brown|dark brown|sandy',
'Grass rat':'brown agouti|grey|dark dorsal stripe analogue|sandy',
'Lemming':'brown mottled|black-yellow patterned analogue|grey-brown|white winter where lineage supports it',
'Jerboa':'sand pale belly|grey-brown|rufous sand|dark tail tuft',
'Pika':'grey-brown|rufous|winter-grey|dark upland',
'Agouti':'chestnut agouti|dark rump pale shoulders|golden-brown|grey-brown',
'Paca':'brown white lateral spots|dark brown spotted|warm rufous spotted|spot-row density variants',
'Shrew':'brown dark back|grey-brown|dark woodland|pale belly',
'Duck':'mottled brown female|green-head grey-body male analogue|dark woodland duck|pale barred plumage',
'Goose':'grey-brown barred|white-dark flight feathers|dark-neck pale cheek analogue|brown juvenile',
'Swan':'white adult|grey juvenile|black adult analogue|dark bill palettes',
'Grouse':'brown mottled female|dark male analogue|white winter ptarmigan analogue|grey-barred',
'Pheasant':'brown mottled female|copper male ring-neck analogue|gold-brown barred hypothetical|dark iridescent male',
'Junglefowl':'red-gold male black tail|brown cryptic female|grey barred analogue|dark woodland',
'Turkey':'bronze dark barred|brown cryptic female|pale feather fringes|grey-brown juvenile',
'Quail':'brown striped|grey chestnut|mottled sandy|dark-throat male analogue',
'Peafowl':'blue-green male|brown female|green analogue|white rare analogue',
'Ostrich':'black-white male|grey-brown female|brown juvenile|pale dryland hypothetical',
'Emu':'shaggy brown|grey-brown|dark neck|striped juvenile',
'Cassowary':'black body blue neck red wattles|neck palette variation|brown juvenile|striped chick',
'Kiwi':'brown shaggy|grey-brown|pale face|dark woodland',
'Raven':'black structural sheen|brownish juvenile|grey-neck relative analogue|pale patch rare analogue',
'Owl':'brown mottled|grey barred|pale sandy|dark woodland',
'Snowy owl':'white barred female|sparse-barred mature male|heavily barred juvenile|cream-tinted',
'Falcon':'slate back barred belly|brown juvenile|pale desert|dark moustache mask',
'Hawk':'brown barred|grey-backed|rufous tail analogue|pale morph',
'Fish eagle':'dark body white head|brown juvenile|white-belly analogue|dark-brown relative analogue',
'Forest eagle':'dark brown|black-white crest analogue|brown barred juvenile|pale underparts',
'Vulture':'brown dark wings|black-white contrast|pale neck ruff|bare head colour variation',
'Secretary bird':'grey black flight feathers|dark crest|warm grey juvenile|red-orange facial skin',
'Crane':'grey white face|white black flight feathers|dark neck patches|red bare crown variation',
'Grey heron':'grey white black crown|warm grey juvenile|darker woodland hypothetical|pale underparts',
'Stork':'white black flight feathers|dark iridescent body|brown juvenile|bare face/bill palette',
'Flamingo':'pale pink|deep salmon|grey juvenile|white-pink with black flight feathers',
'Pelican':'white cream|grey-brown|pink breeding flush|dark flight feathers',
'Kingfisher':'blue orange|green white|brown-white large relative analogue|sex-specific bill-base tint where justified',
'Pigeon':'grey wing bars|woodland green analogue|rufous brown|white collar patch',
'Cockatoo':'white yellow crest|black red tail panel|grey rose breast|brown juvenile',
'Hornbill':'black-white wings|brown-grey body|rufous throat|casque and bare-skin colour variants',
'Hummingbird':'green iridescent male|grey-green female|bronze rufous|blue gorget hypothesis',
'Parrot':'green red patches|blue yellow|red green wings|grey red tail analogue',
'Toucan':'black yellow throat|black white throat|green relative analogue|bill zonal colour variants',
'Turaco':'green purple flight-feather patches|blue-green|grey crested relative analogue|red facial ring',
'Woodpecker':'black-white red crown|brown barred|green-backed|sex-dependent crown patch',
'Albatross':'white dark upper wings|brown juvenile|dark-backed|yellow bill accents',
'Cormorant':'black structural sheen|brown juvenile|white throat patch|breeding bare-skin colour',
'Gull':'white grey black tips|dark-backed|brown mottled juvenile|head hood seasonal variation',
'Tern':'grey white black cap|brown barred juvenile|dark noddy analogue|bill seasonal palette',
'Shorebird':'brown cryptic|grey winter|rufous breeding|black-white contrast',
'Puffin':'black-white orange bill|subdued nonbreeding bill|grey face juvenile|crest or bill accent only with supported anatomy',
'Penguin':'black-white|grey juvenile|yellow head accent|pink bare-foot and face variation',
'Fruit bat':'brown fur pale neck|black membrane|golden mantle|grey face',
'Insectivorous bat':'brown fur|grey fur|dark membrane|pale dryland',
'Freshwater turtle':'olive shell|brown patterned shell|dark shell pale seams|yellow head/neck stripes',
'Tortoise':'tan scute rings|dark shell pale seams|starburst scutes|brown plain shell',
'Sea turtle':'olive shell|brown radiating scutes|dark green|pale ventral shell',
'Crocodile':'olive dorsal scales|grey dark bands|brown swamp|yellow-banded juvenile',
'Monitor lizard':'brown yellow spots|grey bands|dark woodland|pale dryland',
'Iguana':'green|brown-grey|orange breeding male analogue|dark banding',
'Native small lizards (guild)':'brown striped|grey mottled|green woodland|sand speckled',
'Salamanders (guild)':'black yellow spots|brown mottled|red-orange warning analogue|dark plain',
'Boa constrictor':'brown saddles|grey dark saddles|rufous tail|dark swamp hypothetical',
'Python':'brown blotches|olive netlike pattern|dark yellow bands|pale dryland',
'Venomous snake':'brown zigzag|sand blotches|olive bands|black-white warning bands only as explicit hypothetical',
'Dolphin':'grey back pale belly|dark dorsal cape|spotted relative analogue|warm-grey juvenile',
'Orca':'black-white eye and saddle patches|grey saddle variants|muted juvenile whites|bounded patch-shape variants',
'Whale':'slate-grey|dark back white ventral patches|mottled grey|pale scarring overlay',
'Dugong':'warm grey|brown-grey|dark wet skin|pale scar overlay',
'Sea lion':'brown tan|dark mature male|pale juvenile|wet darkening overlay',
'Seal':'grey spotted|brown mottled|dark back pale belly|pale juvenile where species appropriate',
'Walrus':'brown wrinkled skin|grey-brown|pinkish warm skin|dark wet skin',
'Shark':'grey pale belly|brown mottled|dark fin tips|pale reef analogue',
'Small native freshwater fish (guild)':'silver dark back|golden brown|olive barred|red fin accents',
'Freshwater predatory fish (guild)':'olive mottled|silver dark back|dark vertical bars|golden-brown spots',
'Coastal forage fish (guild)':'silver blue back|silver green back|fine dark bars|golden lateral stripe',
'Coastal squid (guild)':'mottled brown|pale cream|dark red chromatophore state|grey speckled',
'Butterflies and moths (guild)':'orange black veins|cream dark margins|blue structural upper wings|brown eyespots|bark-camouflage moth|pale green moth',
'Bees (guild)':'gold black bands|brown solitary bee|dark carpenter-like|grey pale-banded',
'Dragonflies (guild)':'blue black|red brown|green black|bronze brown female analogue',
'Fireflies (derived lineage)':'dark elytra pale thorax|brown elytra red thorax|yellow-green luminous segment|amber luminous segment',
'Native detritivorous flies (guild)':'charcoal grey|bronze metallic|green metallic|brown striped thorax',
'Grasshoppers (guild)':'grass green|dry straw brown|grey mottled|bold warning patches hypothetical',
'Native litter beetles (guild)':'black glossy|brown matte|bronze structural|spotted warning analogue',
'Orb-weaving spiders (guild)':'brown mottled|yellow-black banded|green leaf-camouflage|silver abdomen'
}

sex={
'Deer':'Male antlers versus antlerless female preview50/50; male bounded larger neck/body. Species-specific racks after approval.',
'Reindeer':'Both sexes can carry antlers; sex changes rack size/timing and body proportions. Do not reuse male-only deer toggle.',
'Moose':'Mature male palmated antlers and thicker neck; female antlerless. Calves smaller, no antlers.',
'Duiker':'Small simple horns often both sexes with sex/lineage differences; female absence only where supported.',
'Lion':'Male mane mesh and broader head; female no mane. Preserve shoulder/leg skinning.',
'Mouflon':'Male heavy curled horns and larger neck; female absent/small horns depending lineage.',
'Wild goat':'Both sexes may have horns; male thicker longer horns and possible beard.',
'Ibex':'Male massive swept ridged horns; female shorter slender horns; no antler material.',
'Bison':'Both sexes horns; males larger head/forequarters and beard.',
'Buffalo':'Both sexes horns; mature male boss shape where supported; juveniles shorter.',
'Musk ox':'Both sexes horns; mature male broader horn bases.',
'Wild cattle':'Both sexes horns with sex-dependent thickness; male darker coat only in selected aurochs analogue.',
'Yak':'Both sexes horns; males larger horn span and body.',
'Pronghorn':'Both-sex horn occurrence possible; female smaller and sometimes absent; pronged sheath geometry.',
'Impala':'Male lyre horns, female antlerless/hornless; mature male neck bounded larger.',
'Gazelle':'Both-sex horns varies by lineage; male thicker/longer. No universal50/50 horn switch.',
'Oryx':'Both sexes long horns with bounded thickness/curvature differences.',
'Wildebeest':'Both sexes horns; males heavier horn base and neck.',
'Wild boar':'Male more developed tusks and neck; female smaller tusks. Juvenile longitudinal stripes distinct from sex.',
'Warthog':'Both sexes tusks; male larger facial wart geometry and tusks.',
'Forest hog':'Male larger facial pads and tusks; female bounded smaller.',
'Collared peccary':'Subtle size/canine differences; no oversized boar tusks.',
'Hare':'Sexes mostly similar coat and face; bounded body-size distributions, no mandatory ornamental dimorphism.',
'Rabbit':'Subtle size and dewlap tendency in some females; avoid domestic exaggerated features as default.',
'Red kangaroo':'Rufous males and greyer females are a selected real analogue, with overlap; male larger forequarters.',
'Platypus':'Male hind-leg spur accessory only after model approval; coat not a forced sex colour.',
'Elephant':'Tusks and size vary with selected species/sex; African both-sex tusks versus Asian male-biased occurrence needs separate profiles.',
'Rhinoceros':'Both-sex horn occurrence; mature male thickness differences; one/two-horn anatomy may require separate model family.',
'Giraffe':'Both-sex ossicones; males thicker and barer tips, bounded darker mature coats.',
'Orca':'Male taller dorsal fin and larger body, female shorter curved dorsal; must preserve swimming deformation.',
'Sea lion':'Male larger neck/forequarters and sometimes darker mane-like fur; female more slender.',
'Walrus':'Both sexes tusks; male heavier tusks/neck. No antler-style randomness.',
'Peafowl':'Male long train accessory and bright neck; female shorter brown-green plumage; train has motion implications.',
'Pheasant':'Male bright long-tail form versus cryptic brown female; tail length bounded after approval.',
'Junglefowl':'Male larger comb/wattles/sickle tail and bright saddle feathers; female cryptic and smaller ornaments.',
'Turkey':'Male fan-tail/display/wattle/beard accessories versus subtle female; do not claim display clip exists.',
'Duck':'Species-linked dimorphic male/female coats and seasonal eclipse male; not all duck species dimorphic.',
'Snowy owl':'Mature males often less barred; females and juveniles more barred, overlapping rather than absolute.',
'Hummingbird':'Gorget/iridescence often male-biased and species-dependent; females usually subdued; geometry changes minimal.',
'Woodpecker':'Sex-dependent red crown/cheek patches only for selected species; shared body material masks.',
'Ostrich':'Black-white male, grey-brown female analogue; chick pattern separate.',
'Iguana':'Male larger dewlap/crest and possible breeding colour; female subdued, shared core rig.',
'Orb-weaving spiders (guild)':'Female usually much larger in selected orb-weaver analogues; male slimmer abdomen; broad differences may need second body.',
'Fireflies (derived lineage)':'Sex-linked flash programmes; winged female versus flightless female is a distinct representation, not recolour only.'
}
extras={
'Hare':'Bounded ear length/width, ear-tip dark mask, short tail, separate glossy eyes, lid rims and whiskers after approval.',
'Rabbit':'Bounded shorter ears than hare, compact tail, lid rims, nose patch and whiskers; no hare-like hindquarter stretch.',
'Deer':'Existing four rack shapes, bounded ear/tail keys, lids/lashes/brow hairs and muzzle whiskers retained.',
'Moose':'Palmated rack variants, dewlap/bell and long muzzle; avoid recycling deer fork rack.',
'Reindeer':'Reindeer-specific multi-tine rack variants, broad muzzle/pale neck and hoof width.',
'Lion':'Male mane shell/tufts with separate mask; tail tuft; eyes/lids and whiskers.',
'Lynx':'Ear tufts, cheek ruff and short tail; shared core body limits bobcat versus long-leg lynx differences.',
'Giraffe':'Ossicones, short mane, tail tuft, separate eyes/lids; avoid new neck joints after motion approval.',
'Elephant':'Tusks, ear outlines and trunk-tip differences; African/Asian head/ear shapes may need distinct approved body.',
'Camel':'One-hump and two-hump camels require explicit body/morph validation; never pretend texture creates second hump.',
'Rhinoceros':'One/two-horn accessory sets plus head proportion gate; armoured folded-skin types may require second body.',
'Tapir':'Short proboscis and rounded ears; black-white saddle is mask-only, juvenile stripes separate.',
'Bison':'Shoulder hump/ruff, short horns, beard and tail tuft; American/European silhouette differences bounded.',
'Mouflon':'Curled horn variants, short tail, throat ruff; no dense domestic sheep wool unless separate approved morphology.',
'Wild goat':'Swept horn variants, beard and short tail; keratin horn maps shared.',
'Wild boar':'Tusks, bristle ridge, bounded ears and tail; striped juvenile mask.',
'Warthog':'Facial wart sets, tusks and upright-tail pose; body remains own approved rig.',
'Beaver':'Flat scaled tail, incisors, webbing; independent tail mask/material.',
'Otter':'Whiskers, webbing and tapering tail; no beaver paddle-tail substitution.',
'Platypus':'Bill mask/material, broad tail and webbed feet, male spur.',
'Porcupine':'Low-poly quill clusters with distant alpha simplification; quills deform only near attachment.',
'Pangolin':'Overlapping scale normal/roughness and limited silhouette plates; avoid thousands of separate scales.',
'Hedgehog':'Quill cap clusters and fur face; curled-ball behaviour needs real compatible animation.',
'Peafowl':'Separate male train, eye-spot mask and crest; display animation is a gap until sourced.',
'Hornbill':'Casque and bill-size variants bounded by head rig; bare face masks.',
'Cockatoo':'Crest and cheek patches; movable crest only if rig supports it.',
'Toucan':'Large bill zones, eye skin ring and tail markings; keep bill weight rigid to head.',
'Pelican':'Bill pouch and long bill; no ad-hoc swallowing animation.',
'Secretary bird':'Head quills, bare facial patch and long legs; generic bird pack may be unsuitable for strike gait.',
'Walrus':'Tusks and whisker pad, thick neck folds; no fur coat painting.',
'Sea turtle':'Shell scutes, rigid shell and flippers; no terrestrial turtle walk rebranded as swim.',
'Tortoise':'Rigid shell scutes, scaled neck/legs and claw tips; limb skin must not deform shell.',
'Freshwater turtle':'Rigid shell and webbed feet; swim/walk require appropriate source.',
'Boa constrictor':'Head markings and scale direction; no limbs or eye-brow fur.',
'Python':'Scale direction and head shields, optional heat-pit geometry only at suitable close LOD.',
'Venomous snake':'Species-linked head proportions; rattle only for an explicitly rattlesnake-derived profile.',
'Butterflies and moths (guild)':'Four wing-outline auditions; antenna shapes and tails later; opaque body plus thin two-sided wings, no lights.',
'Fireflies (derived lineage)':'Tiny beetle or distant luminous speck; emissive abdominal segment without real-time point light per insect.',
'Dragonflies (guild)':'Two independent wing pairs, slender abdomen, simple compound eyes; wing-vein texture later.',
'Bees (guild)':'Two wing groups, antennae and six cheap legs; pollen bundles only as optional foraging trait.',
'Native detritivorous flies (guild)':'Two wings, six cheap legs and compound-eye blocks; local flight only, no complex skeletal locomotion required.'
}

def family(row):
    n=row['Fauna entry'];f=row['Body / behavior family']
    if n in simple:return 'microfly'
    if n in canids:return 'canid'
    if n in cats:return 'felid'
    if n in bovids|deer|equids|pigs or n in {'Camel','Guanaco','Tapir','Giraffe'}:return 'ungulate'
    if n in {'Pangolin','Armadillo'}:return 'armoured_mammal'
    if n in {'Echidna','Hedgehog','Porcupine'}:return 'spiny_mammal'
    if n in {'Red kangaroo','Wallaby'}:return 'hopper_marsupial'
    if n in bears:return 'bear'
    if 'bird' in f.lower() or f=='Raptor or aerial scavenger' or n in {'Penguin','Puffin','Cormorant','Albatross','Gull','Tern'}:return 'bird'
    if f=='Primate':return 'primate'
    if f=='Bat':return 'bat'
    if n in {'Boa constrictor','Python','Venomous snake'}:return 'snake'
    if n in {'Freshwater turtle','Tortoise','Sea turtle'}:return 'turtle'
    if n in {'Elephant','Rhinoceros','Hippopotamus'}:return 'thickskin'
    if f=='Marine/coastal large animal' and n!='Polar bear':return 'marine'
    if f=='Fish/cephalopod':return 'fish'
    if n=='Orb-weaving spiders (guild)':return 'spider'
    if f in {'Small reptile or amphibian','Larger reptile'}:return 'reptile'
    if f=='Ground insects':return 'ground_insect'
    return 'mammal'

templates={
'canid':('short directional fur; glossy eye/cornea; dark nasal leather; paw pad/claw keratin; sparse inner-ear skin','dorsal saddle, flanks, belly/chest, muzzle/cheek mask, eyelids, inner/outer ears, lower legs, paws, tail base/tip',5500,'fox'),
'felid':('fine short fur; eye/cornea; nasal leather; paw pads; keratin claws; thin ear skin','dorsal/flank pattern, pale underside, chin, cheek/eye surrounds, inner ears, paw pads, tail rings/tip',5500,'fox'),
'ungulate':('short directional fur; eye/cornea; nasal leather; hoof/horn keratin; ear skin','dorsal/flanks, pale belly, shoulder/throat transition, face mask, lids, inner/outer ears, lower legs, hooves, tail underside/tip',6000,'horse'),
'bear':('medium/coarse fur; eye/cornea; nasal leather; paw pads; claw keratin','back/flanks, shoulder grizzle, neck/chest, muzzle, eyelids, ears, paw pads/claws',6000,'fox; seek dedicated CC0 bear gait'),
'mammal':('short/medium fur; eye/cornea; nose/paw skin; claw keratin; ear skin','back, flanks, belly with broad smooth neck blend, muzzle/nose, lids, inner/outer ears, feet, tail',4000,'fox; choose closer dedicated CC0 source where available'),
'bird':('layered contour-feather swatch; flight-feather swatch; eye/cornea; bill/claw keratin; scaled feet; optional bare facial skin','crown, cheek/eye skin, throat, breast, back, belly, folded wing coverts, primary/secondary feathers, rump/tail, bill and feet',4000,'bird'),
'bat':('short fur; thin wing membrane normal/roughness; eye/cornea; ear/nose skin; claw keratin','furred torso/head, ear interior/exterior, membrane webbing, wing fingers, feet',3500,'bird as limited flap audition; seek dedicated CC0 bat source'),
'primate':('short directional fur; eye/cornea; bare facial/palm/sole skin; nail keratin','crown/cheek fur, bare eye/muzzle skin, chest/belly, hands/soles, tail if present, sex-linked bare skin only where justified',6000,'dedicated primate source required; human pack only a clearly labelled compatibility audition'),
'snake':('dorsal scale swatch; broad belly scutes; eye/cornea; mouth tissue','dorsal pattern, flank transition, ventral scutes, head shields, labial region, eye socket',2500,'snake'),
'turtle':('shell-scute colour/normal; scaled limb/neck skin; eye/cornea; claw/bill keratin','dorsal shell, shell seams, ventral plastron, head/neck, limbs/flippers, eye ring',4000,'dedicated turtle source required'),
'thickskin':('wrinkled skin; eye/cornea; nail/hoof keratin; ivory/horn if present; temporary mud overlay','body skin with smooth neck transition, eye sockets, thin ear skin, muzzle, feet/nails, horns/tusks',6000,'species-specific CC0 donor preferred; horse or fox only labelled gait comparison'),
'marine':('skin or short wet fur as species appropriate; eye/cornea; roughness variation; ivory where present','dark dorsal/pale ventral zones, head patches, fins/flippers, tail, mouth, scars as optional overlays',4500,'fish for compatible tail motion; dedicated mammal/seal source preferred'),
'fish':('small scale or smooth cephalopod skin; eye/cornea; translucent fin/membrane swatch','dorsal, flanks, belly, lateral stripe/bars, gills, fins/tail, eye rim; squid mantle/arms separated',2000,'fish; squid needs dedicated mantle/tentacle source'),
'reptile':('scaled or amphibian smooth skin as appropriate; eye/cornea; claw keratin','dorsal pattern, flanks, belly, head, eye ring, limb digits, tail, any crest/dewlap',3500,'dedicated lizard/crocodile source preferred; avoid upright kaiju gait as acceptance'),
'spider':('fine chitin; eye gloss; sparse hair/card swatch','cephalothorax, abdomen pattern, leg segments, joint membranes, eyes and spinneret region',1500,'spider'),
'ground_insect':('chitin colour/roughness; joint membranes; compound-eye swatch; optional wing membrane','head, thorax, abdomen/elytra, leg segments, antennae, eyes, wing panels',1000,'spider only if six-leg remap sound; dedicated CC0 insect preferred'),
'microfly':('chitin/body swatch; eye gloss; thin wing membrane; butterfly wing-scale swatch or emissive abdomen where appropriate','head/eyes, antennae, thorax/abdomen, legs, separate wing panels, optional luminous abdomen',500,'own simple wing flap/hover only; user-authorized')
}
templates['armoured_mammal']=('keratin scale/plate swatch; sparse belly fur or skin; eye/cornea; nasal leather; claw keratin','body armour plates, belly skin/fur, muzzle, eye sockets, ears, feet/claws, tail plates',4000,'rat as labelled quadruped audition; seek dedicated armoured-mammal source')
templates['spiny_mammal']=('fur swatch; keratin quill/spine swatch; eye/cornea; nasal skin; claw keratin','quill cap/back, furred belly/face, muzzle, eye sockets, ears, feet/claws, tail where present',3500,'rat as labelled short-legged quadruped audition; curled posture remains a gap')
templates['hopper_marsupial']=('short fur swatch; eye/cornea; nasal/paw skin; claw keratin; ear skin','back/flanks, belly/chest, muzzle, eye sockets, ears, forepaws, large hindfeet, muscular tail',5500,'Dedicated CC0 kangaroo/wallaby hop required; do not present fox or rabbit gait as anatomically validated')

data=[]
for row in sorted(rows,key=lambda r:(order.get(r['Fauna entry'],100+int(r['Your priority'] or r['Priority'])),r['Fauna entry'])):
    name=row['Fauna entry'];fam=family(row);materials,zones,budget,motion=templates[fam]
    status='accepted_baseline' if name=='Deer' else 'audio_existing' if name in audio else 'deferred' if (row['Your implementation choice'] or row['First implementation recommendation']).startswith('Skip') else 'queued'
    bank=coats.get(name,'natural brown/grey base|pale habitat analogue|dark habitat analogue|subtle dorsal/ventral contrast')
    if fam=='primate':bank='species-linked grey/brown fur and bare-face masks|dark woodland analogue|pale chest/throat analogue|age-linked facial colour where supported'
    variants=bank.split('|');real_aliases=row['Consolidated source species names'] or name
    sexplan=sex.get(name,'Sexes share the core model and initial coat masks unless a supported lineage requires dimorphism; bounded size distributions only. Plan age/season separately from sex.')
    extra=extras.get(name,'Separate glossy eyes and visible lid/socket transitions after motion approval; bounded ear/tail/bill traits only where family anatomy supports them. Distinct body silhouette requires a second approved model, not forced recolouring.')
    if fam=='bird' and name not in sex:sexplan='Species-dependent sex colours, breeding versus nonbreeding and juvenile plumage are separate profiles; do not force sexual dimorphism on monomorphic species.'
    if name in {'Hare','Rabbit'}:motion='cdmir-rabbit CC0 (dedicated); Gobkit Hare CC0 as optional comparison, after clip inspection';budget=3500
    if name=='Squirrel':motion='CDmir squirrel CC0 Run/Idle/Jump stages; undead-source attack style excluded from primary demo';budget=2500
    if name in {'Mouse','Bank vole','Cotton rat','Grass rat','Lemming','Shrew','Jerboa'}:motion='CDmir rat CC0 Walk/Run/Idle as small-rodent audition; assess tail/stride compatibility';budget=1800
    if name in {'Agouti','Paca','Pika','Ground squirrel','Marmot','Nutria','Beaver'}:motion='CDmir rat or squirrel CC0, selected for ground gait; swimming/digging remain explicit gaps';budget=2500
    if name in {'Crocodile','Monitor lizard','Iguana','Native small lizards (guild)','Salamanders (guild)'}:motion='br-n518 crocodile CC0 Idle/Walk/Attack/Death as crawling-quadruped audition; non-crocodile compatibility remains visual-review pending'
    texcount=len(materials.split('; '))
    hypothetical=('Faint disruptive flank bands in shaded cover; sandy pale camouflage in open arid habitat; darker closed-canopy forms. Hypotheses are optional art candidates, not asserted existing species.' if fam not in {'bird','fish','marine','microfly'} else
                  'Bounded habitat-linked pigment/pattern variation within this anatomy; mark invented patterns explicitly. Structural colours, emission and transparency remain separate material traits.')
    info={'priority_rank':len(data)+1,'animal':name,'slug':re.sub(r'[^a-z0-9]+','-',name.lower()).strip('-'),'catalogue_status':row['Catalogue status'],'family':fam,'representation':row['Your implementation choice'] or row['First implementation recommendation'],
      'asset_status':status,'priority_reason':('Existing accepted baseline' if name=='Deer' else 'Early shared gameplay/visual body-family coverage' if name in order else 'Regional habitat breadth after common body families; preserve original roster priority '+row['Priority']),
      'source_species_aliases':real_aliases,'raw_candidates':0 if status!='queued' else 4,'triangle_budget':budget,'texture_plan':{'reusable_swatch_count':texcount,'materials':materials.split('; '),'coat_configuration_count':len(variants),'coats':variants,'paint_zones':zones.split(', '),
        'channels':'One2K base-colour atlas +1K normal/roughness for medium/large animals;1K/512 for small animals;256-512 for tiny fauna. Shared eye/keratin swatches. No extra per-coat draw calls.',
        'method':'Generate reusable neutral swatches only after raw audition queue; after owner approval, map with anatomical masks and broad smooth blends. Never project one side photograph as complete360-degree texture.',
        'seam_control':'Shared continuous body coordinates, smooth throat/belly transitions, explicit face/ear masks; inspect front/side/rear and neck seam.'},
      'sex_age_season':sexplan,'extras':extra,'hypothetical_variation':hypothetical,'motion_source_plan':motion,
      'required_behaviour_clips':(['hover/flight','perch/rest','wing flap if visible'] if fam=='microfly' else ['idle','locomotion appropriate to anatomy','escape/fast motion','feed or forage','rest','death if gameplay requires']),
      'motion_gaps_policy':'Use only verified CC0 source motion. Missing clips stay explicitly missing; do not rename an unrelated action as feeding/running. No complex invented mammal/bird motion.',
      'model_prompt_constraints':'Neutral symmetric anatomy, visible distinct limbs, straight head/neck, natural eye sockets/lids, no baked accessories, isolated reference, front/side/rear checks. Bird/bat wings need actual separable geometry.',
      'review_plan':'1 candidates;2 bounded colour-only preview until painting approved;3 Side/Face/Orbit plus rear/front body;4 blended showcase/locomotion; visible Show/Hide Skeleton, pause and all available individual clips.',
      'second_pass_status':'Pending targeted species/sex research after all raw auditions; current coat analogues are first-pass plans, hypothetical variants explicitly labelled.',
      'actual_model_status':'Not generated' if status=='queued' else 'Already accepted deer prototype' if status=='accepted_baseline' else 'No visual generation in this batch',
      'original_row':row}
    if status=='audio_existing':
        info['triangle_budget']=0;info['texture_plan'].update(reusable_swatch_count=0,materials=[],coat_configuration_count=0,coats=[],paint_zones=[],channels='None: existing spatial audio representation',method='No visual asset generation in this batch',seam_control='Not applicable')
        info['extras']='None: current implementation is noise-only.';info['sex_age_season']='Calling/activity/acoustic profile variation is already represented separately; no visible sex/age model required.'
        info['motion_source_plan']='Not applicable: spatial audio only';info['required_behaviour_clips']=[];info['hypothetical_variation']='Acoustic/activity hypotheses only, no painted coat or body model.'
    elif status=='deferred':
        info['texture_plan']['method']='Deferred: no textures requested/generated until this representation is explicitly activated.'
        if name!='Mole':
            info['texture_plan'].update(reusable_swatch_count=0,materials=[],coat_configuration_count=0,coats=[],paint_zones=[],channels='Deferred unresolved invertebrate/body families; no mammal fur assumption')
            info['extras']='Define an appropriate actual body/interaction family if activated; no current geometry or accessory generation.'
    data.append(info)
assert len(data)==188 and len({x['animal'] for x in data})==188
(a.output/'asset-plan.json').write_text(json.dumps({'scope':'Full188-entry owner catalogue; raw visual auditions only, no runtime integration or painting','rows':data},indent=2))
columns=['Rank','Animal','Status','Representation','Triangles','Candidates','Swatches','Coats','Paint regions','Sex / age / season','Extras','Motion sources','Research status']
with (a.output/'FAUNA_ASSET_PLAN.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(columns)
    for x in data:w.writerow([x['priority_rank'],x['animal'],x['asset_status'],x['representation'],x['triangle_budget'],x['raw_candidates'],x['texture_plan']['reusable_swatch_count'],'; '.join(x['texture_plan']['coats']),'; '.join(x['texture_plan']['paint_zones']),x['sex_age_season'],x['extras'],x['motion_source_plan'],x['second_pass_status']])
print('Planned',len(data),'entries;',sum(x['asset_status']=='queued' for x in data),'visual queues;',sum(x['asset_status']=='audio_existing' for x in data),'audio entries;',sum(x['asset_status']=='deferred' for x in data),'deferred.')

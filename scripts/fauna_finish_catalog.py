"""Appearance recipes for the remaining owner-selected fauna.

Palettes and anatomical markings are explicit art inputs. Existing sourced
species plans provide coat labels and record silhouette/sex/age limitations.
No palette or hypothetical pattern claims to implement another animal's shape.
"""
import json
from pathlib import Path

REPO=Path(__file__).resolve().parent.parent
# sRGB body | underside/accent | characteristic marking recipe.
SPEC='''
owl|756248|c6bca4|owl
raven|15191d|303239|corvid
african-hunting-dog|7b4c27|d7c9a8|painted-dog
agouti|664021|b79b67|grizzle
bank-vole|71482c|ad9d7b|plain
buffalo|292820|62584a|plain
camel|997b4e|c2ac7e|plain
cassowary|20211d|53617b|cassowary
civet|7b7565|c7bfaa|civet
cockatoo|d4d2bd|d9c759|cockatoo
collared-peccary|49433a|b4aa94|collar
cotton-rat|6c5336|b5aa8b|grizzle
crane|999b91|d5d2bd|crane
duiker|75442a|a5916b|plain
emu|504539|94856c|emu
falcon|6a6355|bcb394|raptor
fish-eagle|594532|d2ccb6|eagle
flamingo|d19c99|ddc4b0|flamingo
forest-eagle|584a36|bcb294|raptor
forest-hog|3f3328|8a7660|plain
fruit-bat|49352a|b49667|bat
gazelle|9c6c37|d4c7a8|gazelle
goose|737269|c5c2ae|goose
grass-rat|6b5739|b5ac8e|plain
grasshoppers-guild|6c7442|b6b37a|insect
grey-heron|8c9492|c5c6b8|heron
ground-squirrel|806749|bfb094|grizzle
guanaco|9c6b42|d8cbb1|plain
hawk|76604a|b9ae91|raptor
hornbill|292a26|c8bda1|hornbill
hummingbird|3e7159|c6c4a8|hummingbird
ibex|756448|b9ab8b|plain
impala|995c2b|d7c6a1|gazelle
insectivorous-bat|524635|9b8c70|bat
jaguar|b28a42|d6c49c|rosettes
jerboa|9b7e50|d9cdb0|plain
kingfisher|367283|c18a4a|kingfisher
kiwi|66533b|8f7c60|grizzle
lemming|76613d|b9ab83|grizzle
leopard|ac8240|d8c7a0|rosettes
lion|a78b55|cdbb91|lion
marmot|756043|b1a185|grizzle
meerkat|938362|c3b699|meerkat
mongoose|78684d|b6a98b|grizzle
musk-ox|41342a|9f8e6b|saddle
native-litter-beetles-guild|35342a|626044|insect
native-small-lizards-guild|6c7251|b2b294|lizard
ocelot|a58d5c|d7cdb1|ocelot
oryx|9b9a87|d4ccb1|oryx
ostrich|34352e|d0cdb9|ostrich
paca|78502f|c5b08a|paca
parrot|4d8644|b8c57a|parrot
peafowl|3b6c7b|847746|peafowl
pelican|c6c6b4|b8b59a|pelican
pigeon|7b8083|b2b2a8|pigeon
pika|7d6b4e|bdb093|plain
pronghorn|a88750|ddd3b8|pronghorn
puma|aa8e62|d3c8ad|plain
red-kangaroo|997050|c7b79a|plain
salamanders-guild|394b3b|9c9f79|salamander
secretary-bird|969b94|c6c7b9|secretary
skunk|181a16|d7d4bd|skunk
snowy-owl|d3d2c1|e0dfd0|snowy-owl
spotted-hyena|8b7952|baa986|hyena
spotted-tailed-quoll|70513a|d3cab4|quoll
stork|cfcec0|d9d6c5|stork
swan|d9d8c9|e2e0d2|swan
tapir|5a4f41|9b8c73|tapir
tiger|b77d33|ddd1b0|tiger
toucan|202724|cbb65b|toucan
turaco|537a45|b0af64|turaco
vulture|514735|a29a83|vulture
wallaby|807255|bfb499|plain
warthog|625443|9c8d73|plain
weasel|795334|d8ccb0|plain
wild-ass|999788|d4cfb6|ass
wildebeest|666150|9f9882|plain
wolverine|493628|a28a5e|wolverine
wombat|796b53|a99b80|plain
woodpecker|343a33|d7d3b8|woodpecker
yak|372b23|8b7761|plain
zebra|c6c3ac|dfdcc6|zebra
aardvark|94816b|baa78d|plain
albatross|d3d1c1|dcd8c5|albatross
anteater|75694f|b5ab90|anteater
armadillo|877e66|b3a991|armour
baboon|78664c|b4a084|primate
boa-constrictor|847350|c1b28b|boa
capuchin|5c4732|c6b896|capuchin
capybara|7d6345|ae9874|grizzle
chimpanzee|312c25|8c7d67|primate
coastal-forage-fish-guild|71847d|c5cebf|fish
coastal-squid-guild|a79684|d5c8b0|squid
common-brushtail-possum|837965|c0b297|plain
cormorant|202c29|4f5650|cormorant
crocodile|5c6650|ada991|crocodile
dolphin|7a8b8d|c3c9bf|dolphin
dugong|8c8b7d|b9b8a6|plain
echidna|64523a|c7b18b|spines
elephant|898579|b0a999|thickskin
freshwater-predatory-fish-guild|6f7650|bfc4a1|pike
freshwater-turtle|66704c|b6ad7a|turtle
giant-panda|d0c8aa|e0d9c4|panda
gibbon|766548|c0b18e|primate
giraffe|bca36b|dfcda3|giraffe
gorilla|33332c|8d8b7f|gorilla
guereza|272b26|d4d3c1|guereza
gull|b9bdb5|dbd9c8|gull
hedgehog|786548|bfae87|spines
hippopotamus|77746a|b39a85|thickskin
howler-monkey|513e2a|9d8361|primate
iguana|678052|b4b794|iguana
koala|8e9185|ceccb7|koala
lemur|898d7c|d0cdb7|lemur
long-nosed-bandicoot|84694a|c3b18d|plain
macaque|89795e|bba78d|primate
monitor-lizard|65674d|b5b28e|monitor
nutria|715033|a99672|grizzle
orangutan|91532a|b38b5a|orangutan
orb-weaving-spiders-guild|796342|b9a073|spider
orca|182b2e|d4d9cc|orca
pangolin|7f7156|b4a58a|armour
penguin|29352f|dedbc9|penguin
platypus|655038|9d8b69|platypus
polar-bear|d1cbb3|dfdac6|plain
porcupine|514632|c7bb95|spines
puffin|272d27|d9d7c2|puffin
python|7a704e|c1b48b|python
rhinoceros|817e70|aaa38d|thickskin
sea-lion|66563d|9e8b68|plain
sea-otter|54402c|b9aa88|otter
sea-turtle|6a7960|c6be91|turtle
seal|858878|b9bca9|seal
shark|7b8b85|d4d9c8|shark
shorebird|8a8067|d4ceb5|shorebird
shrew|76614b|baac8e|plain
sloth|7e8066|bcb69c|sloth
spider-monkey|4b3c2a|9a8462|primate
tamarin|896b43|c5ab7f|tamarin
tern|b4bfba|dcdfd1|tern
tortoise|817b50|b7a571|turtle
venomous-snake|81704b|c6b58a|viper
walrus|89755f|b39d83|walrus
whale|637679|aebbb3|whale
'''

PALETTES={}
for line in SPEC.strip().splitlines():
    slug,body,belly,pattern=line.split('|')
    PALETTES[slug]=(body,belly,pattern)

ROWS={x['slug']:x for x in json.loads((REPO/'docs/fauna/asset-plan.json').read_text())['rows']}
SUPPORTED=set(PALETTES)

def category(slug):
    if slug=='coastal-squid-guild':return 'squid'
    if slug=='salamanders-guild':return 'amphibian'
    if slug=='shark':return 'fish'
    return ROWS[slug]['family']

def rgb(value):return [int(value[i:i+2],16)/255 for i in (0,2,4)]

def profiles(slug):
    base,pale,_=PALETTES[slug];base=rgb(base);pale=rgb(pale)
    labels=ROWS[slug]['texture_plan']['coats']
    # Conservative pigment studies when a named silhouette/age would differ.
    result=[]
    for i,label in enumerate(labels[:6]):
        low=label.lower();b=list(base);p=list(pale)
        if i:
            factor=.82 if any(x in low for x in ('dark','black','sooty','melan')) else 1.15 if any(x in low for x in ('pale','light','winter','white')) else .98
            b=[min(.88,x*factor) for x in b]
            if 'grey' in low or 'gray' in low:
                grey=sum(b)/3;b=[x*.25+grey*.75 for x in b]
            elif 'rufous' in low or 'warm' in low:
                b=[min(.88,b[0]*1.12),b[1]*.97,b[2]*.88]
            else:
                shift=(i%3-1)*.025;b=[min(.9,max(.018,x+shift)) for x in b]
        result.append((label,b,p,False))
    return result or [('Base coat',base,pale,False)]

def surface(slug):
    kind=category(slug)
    if kind=='bird':return 'feather-contour-v1.png'
    if kind=='bat':return 'wing-membrane-bat-v1.png'
    if kind=='snake':return 'scale-snake-dorsal-v1.png'
    if kind=='reptile':return 'scale-reptile-v1.png'
    if kind=='amphibian':return 'skin-amphibian-v1.png'
    if kind=='fish':return 'skin-denticle-v1.png' if slug=='shark' else 'scale-fish-v1.png'
    if kind=='squid':return 'skin-squid-v1.png'
    if kind=='turtle':return 'shell-scute-v1.png'
    if slug=='pangolin':return 'keratin-hard-v1.png'
    if kind in {'ground_insect','spider'}:return 'chitin-fine-v1.png'
    if kind=='marine':return 'fur-dense-fine-v1.png' if slug=='sea-otter' else 'skin-marine-smooth-v1.png'
    if kind in {'thickskin','armoured_mammal'}:return 'skin-thick-wrinkled-v1.png'
    if slug in {'yak','musk-ox','camel','orangutan'}:return 'fur-wool-shag-v1.png'
    if kind=='spiny_mammal':return 'fur-coarse-guard-v1.png'
    return 'fur-dense-fine-v1.png'

def radius(slug):
    if slug in {'owl','snowy-owl'}:return (.050,.048)
    if category(slug)=='bird':return (.023,.021)
    if category(slug) in {'snake','reptile','amphibian','turtle'}:return (.022,.020)
    if category(slug) in {'marine','fish','squid'}:return (.028,.026)
    if category(slug)=='thickskin':return (.025,.020)
    return (.030,.024)

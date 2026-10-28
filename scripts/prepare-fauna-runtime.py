"""Pin the owner's current 177 natives and author bounded prototype spawn profiles."""
import hashlib,json,math,re
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-runtime-20261026';root.mkdir(parents=True,exist_ok=True)
rows=json.loads((repo/'artifacts/fauna-anatomy-revision/review-index.json').read_text())['ready']
inventory={r['slug']:r for r in json.loads((root/'native-inventory.json').read_text())['animals']}
plans={r['slug']:r for r in json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']}
roles={'Idle':'idle','Walk':'walk','Trot':'trot','Run':'run','Flight':'fly','Flap':'fly','Glide':'glide',
       'Swim':'swim','Swim Horizontal':'swim','Swim Vertical':'swim','Aquatic idle':'idle',
       'Flipper stroke transfer':'swim','Glide transfer':'glide','Escape pulse':'run',
       'Slither':'walk','Side winding':'walk'}
lengths='''
deer 1.8;hare .65;rabbit .4;wolf 1.5;red-fox 1.05;wild-boar 1.6;mouflon 1.4;wild-goat 1.25;bear 2.1;bison 3.1;horse 2.8;reindeer 2;moose 2.8;wild-cattle 2.7;raccoon .85;badger .85;beaver 1;otter 1.2;lynx 1.1;mouse .18;squirrel .45
butterflies-and-moths-guild .075;fireflies-derived-lineage .018;bees-guild .026;dragonflies-guild .09;native-detritivorous-flies-guild .014;grasshoppers-guild .05;native-litter-beetles-guild .025;orb-weaving-spiders-guild .07
duck .65;pheasant 1;grouse .5;quail .23;turkey 1.15;junglefowl .65;owl 1.1;raven 1.15;cockatoo .85;crane 2.1;emu 1.9;falcon 1;fish-eagle 2.1;flamingo 1.5;forest-eagle 2;goose 1.4;grey-heron 1.8;hawk 1.25;hornbill 1.3;hummingbird .12;kingfisher .32;kiwi .5;ostrich 2.6;parrot .9;peafowl 1.8;pelican 2.7;pigeon .65;secretary-bird 2;stork 2;turaco .75;toucan .75;vulture 2.4;woodpecker .5;albatross 3;cormorant 1.3;gull 1.2;penguin .9;puffin .55;shorebird .5;tern .75;swan 2.1;snowy-owl 1.5;cassowary 1.8
african-hunting-dog 1.3;agouti .6;bank-vole .15;buffalo 2.8;camel 3;civet 1.05;collared-peccary 1.05;cotton-rat .25;duiker 1;forest-hog 1.6;gazelle 1.15;grass-rat .25;ground-squirrel .28;guanaco 2.1;ibex 1.4;impala 1.5;jaguar 2.15;jerboa .24;lemming .16;leopard 2.1;lion 2.7;marmot .65;meerkat .5;mongoose .65;musk-ox 2.3;ocelot 1.2;oryx 2;paca .8;pika .2;pronghorn 1.5;puma 2;red-kangaroo 2.3;skunk .7;spotted-hyena 1.6;spotted-tailed-quoll .85;tapir 2.1;tiger 2.7;wallaby 1.1;warthog 1.4;weasel .3;wild-ass 2.1;wildebeest 2.3;wolverine 1.1;wombat 1;yak 2.6;zebra 2.5
aardvark 1.7;anteater 2;armadillo .7;baboon .95;boa-constrictor 2.6;capuchin .85;capybara 1.3;chimpanzee 1.2;common-brushtail-possum .85;crocodile 3.3;echidna .5;elephant 5.6;freshwater-turtle .35;giant-panda 1.6;gibbon .9;giraffe 4.8;gorilla 1.75;guereza 1.15;hedgehog .27;hippopotamus 3.5;howler-monkey 1.25;iguana 1.4;koala .75;lemur .8;long-nosed-bandicoot .45;macaque .9;monitor-lizard 1.6;nutria 1;orangutan 1.35;pangolin 1.1;platypus .55;polar-bear 2.5;porcupine .85;python 3.6;rhinoceros 3.8;shrew .1;sloth .75;spider-monkey 1.5;tamarin .55;tortoise .4;venomous-snake 1
dolphin 2.4;dugong 2.8;orca 6.5;sea-lion 2.1;sea-otter 1.2;sea-turtle 1;seal 1.7;shark 3;walrus 3;whale 14;small-native-freshwater-fish-guild .14;freshwater-predatory-fish-guild .65;coastal-forage-fish-guild .22;coastal-squid-guild .45;native-small-lizards-guild .2;salamanders-guild .18;fruit-bat .8;insectivorous-bat .28
'''
sizes={k:float(v) for k,v in re.findall(r'([a-z0-9-]+)\s+([.0-9]+)',lengths)}
aquatic=set('dolphin dugong orca sea-lion sea-otter sea-turtle seal shark walrus whale small-native-freshwater-fish-guild freshwater-predatory-fish-guild coastal-forage-fish-guild coastal-squid-guild'.split())
fresh=set('small-native-freshwater-fish-guild freshwater-predatory-fish-guild'.split())
tiny=set('butterflies-and-moths-guild fireflies-derived-lineage bees-guild dragonflies-guild native-detritivorous-flies-guild'.split())
aliases={'small-native-freshwater-fish-guild':['Native freshwater fish','Small freshwater fish guild'],
         'freshwater-predatory-fish-guild':['Freshwater predatory fish'],'coastal-forage-fish-guild':['Coastal forage fish'],
         'coastal-squid-guild':['Coastal squid'],'native-small-lizards-guild':['Native small lizards'],
         'native-litter-beetles-guild':['Native litter beetles'],'native-detritivorous-flies-guild':['Native detritivorous flies'],
         'orb-weaving-spiders-guild':['Orb-weaving spiders'],'salamanders-guild':['Salamanders'],
         'butterflies-and-moths-guild':['Butterflies and moths'],'bees-guild':['Bees'],'dragonflies-guild':['Dragonflies'],
         'grasshoppers-guild':['Grasshoppers'],'fireflies-derived-lineage':[]}
out=[];export=[]
for i,row in enumerate(rows):
    slug=row['slug'];native=inventory[slug];plan=plans[slug];assert slug in sizes,slug
    names=[row['name'],*aliases.get(slug,[]),*[v.strip() for v in plan.get('source_species_aliases','').split(';') if v.strip()]]
    selected=[]
    for clip in native['actions']:
        if clip['label'] not in roles:continue
        role=roles[clip['label']]
        if role=='glide' and slug in aquatic:role='swim'
        if role=='run' and slug in aquatic:role='swim'
        # Prefer the horizontal or explicit swim over a vertical study take.
        priority={'Swim Horizontal':0,'Swim':1,'Swim Vertical':4,'Aquatic idle':0,'Idle':1}.get(clip['label'],1)
        selected.append({**clip,'role':role,'priority':priority})
    selected.sort(key=lambda c:c['priority'])
    seen=set();selected=[c for c in selected if not(c['role'] in seen or seen.add(c['role']))]
    assert selected and any(c['role']=='idle' for c in selected),slug
    medium='water' if slug in aquatic else 'air' if any(c['role']=='fly' for c in selected) else 'ground'
    length=sizes[slug]
    speed=min(2.5,max(.08,length*.65));fast=min(13,max(.2,length*3))
    if medium=='air':speed=min(13,max(.2,length*5));fast=speed*1.5
    if medium=='water':speed=min(4,max(.12,length*.65));fast=speed*1.6
    # Practical density priors; actual taxon mass/diet and regional productivity
    # refine these at runtime. No world-scale multiplier is applied.
    density=180 if medium=='air' else 120 if medium=='water' else 0
    if slug in tiny:density=18000
    radius=22 if slug in tiny else 170 if medium=='air' else 180 if medium=='water' else 70 if length<.4 else 170 if length<1 else 650
    if slug in {'butterflies-and-moths-guild','fireflies-derived-lineage'}:radius=220
    out.append({'slug':slug,'name':row['name'],'aliases':list(dict.fromkeys(names)),'medium':medium,'lengthM':length,
                'walkMps':round(speed,3),'runMps':round(fast,3),'radiusM':radius,'densityPerKm2':density,
                'derivedFirefly':slug=='fireflies-derived-lineage','freshwater':slug in fresh,'tiny':slug in tiny,
                'nocturnal':slug in {'owl','snowy-owl','insectivorous-bat','fruit-bat','fireflies-derived-lineage'},'bundle':slug+'.fauna'})
    export.append({**row,'source_sha256':native['sha256'],'clips':selected,'textureSize':512 if length<.3 else 1024,
                   'forward':native['entry'].get('review_forward_direction') or [0,1,0],'targetLengthM':length})
resources=repo/'unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json'
resources.write_text(json.dumps({'version':1,'animals':out},indent=2)+'\n')
(root/'export-plan.json').write_text(json.dumps({'version':1,'animals':export},indent=2)+'\n')
print('Pinned',len(export),'natives;',sum(len(r['clips']) for r in export),'movement clips;',len(tiny),'tiny flying guilds.')

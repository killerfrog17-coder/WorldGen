"""Shared source dispatch for the raw fauna audition worker and coverage report."""
QUADRUPED_PRIMATE_TRIALS={'Baboon','Capuchin','Chimpanzee','Gorilla','Guereza','Howler monkey','Lemur','Macaque','Spider monkey','Tamarin'}

def transfer_trial_note(animal):
    if animal in QUADRUPED_PRIMATE_TRIALS:return 'EXPERIMENTAL fox quadrupedal motion transfer, not authored primate motion; climbing, grasping, grooming and primate-specific gait still missing'
    return None

def source_for(entry,repo):
    slug=entry['slug'];fam=entry['family'];animal=entry['animal'];bank=repo/'artifacts/fauna-motion-sources'
    if animal in {'Coastal squid (guild)','Sea turtle'}:return None
    if animal in QUADRUPED_PRIMATE_TRIALS:key='fox' # Explicit quadrupedal transfer trial, not primate-authored motion.
    elif fam=='bat':key='bat' if (bank/'bat'/'source-rig.json').exists() else 'bird'
    elif animal=='Sloth':key='sloth'
    elif animal=='Elephant':key='mammoth'
    elif animal=='Shark':key='fish'
    elif animal in {'Dolphin','Orca','Whale','Dugong'}:key='cetacean'
    elif animal=='Sea otter':key='fox'
    elif slug in ('hare','rabbit'):key='rabbit'
    elif slug=='squirrel':key='squirrel'
    elif animal in {'Mouse','Bank vole','Cotton rat','Grass rat','Lemming','Shrew','Jerboa','Agouti','Paca','Pika','Ground squirrel','Marmot','Nutria','Beaver'}:key='rat'
    else:key='rat' if fam in ('armoured_mammal','spiny_mammal') else 'sabretooth' if fam=='felid' else 'fox' if fam in ('canid','mammal','bear') else 'horse' if fam in ('ungulate','thickskin') else 'bird' if fam=='bat' else 'crocodile' if fam=='reptile' else fam
    p=bank/key
    return p if (p/'source-rig.json').exists() else None

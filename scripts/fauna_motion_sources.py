"""Shared source dispatch for the raw fauna audition worker and coverage report."""
def source_for(entry,repo):
    slug=entry['slug'];fam=entry['family'];animal=entry['animal'];bank=repo/'artifacts/fauna-motion-sources'
    if animal in {'Coastal squid (guild)','Sea turtle','Sloth'}:return None
    if animal=='Shark':key='fish'
    elif animal in {'Dolphin','Orca','Whale','Dugong'}:key='cetacean'
    elif animal=='Sea otter':key='fox'
    elif slug in ('hare','rabbit'):key='rabbit'
    elif slug=='squirrel':key='squirrel'
    elif animal in {'Mouse','Bank vole','Cotton rat','Grass rat','Lemming','Shrew','Jerboa','Agouti','Paca','Pika','Ground squirrel','Marmot','Nutria','Beaver'}:key='rat'
    else:key='rat' if fam in ('armoured_mammal','spiny_mammal') else 'fox' if fam in ('canid','felid','mammal','bear') else 'horse' if fam in ('ungulate','thickskin') else 'bird' if fam=='bat' else 'crocodile' if fam=='reptile' else fam
    p=bank/key
    return p if (p/'source-rig.json').exists() else None

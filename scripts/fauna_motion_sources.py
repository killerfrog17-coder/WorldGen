"""Shared source dispatch for the raw fauna audition worker and coverage report."""
import hashlib,json
from pathlib import Path
QUADRUPED_PRIMATE_TRIALS={'Baboon','Capuchin','Chimpanzee','Gorilla','Guereza','Howler monkey','Lemur','Macaque','Spider monkey','Tamarin'}

def transfer_trial_note(animal):
    if animal in QUADRUPED_PRIMATE_TRIALS:return 'EXPERIMENTAL fox quadrupedal motion transfer, not authored primate motion; climbing, grasping, grooming and primate-specific gait still missing'
    if animal=='Sea lion':return 'EXPERIMENTAL fore-flipper swimming transfer; no authored sea-lion or hauled-out land movement supplied'
    if animal=='Walrus':return 'EXPERIMENTAL hind-flipper swimming transfer; no authored walrus or hauled-out land movement supplied'
    return None

def source_for(entry,repo):
    slug=entry['slug'];fam=entry['family'];animal=entry['animal'];bank=repo/'artifacts/fauna-motion-sources'
    record=repo/'artifacts/fauna-overnight-20261024'/slug/'source-trial-reviewed.json'
    if record.exists():
        decision=json.loads(record.read_text());p=Path(decision['motion_source']).resolve()
        if decision.get('decision')=='usable_for_raw_audition' and p.is_relative_to(bank) and all((p/n).exists() for n in ('source-rig.json','source-motions.npz')):
            if hashlib.sha256((p/'source-rig.json').read_bytes()).hexdigest()==decision['motion_rig_sha256'] and hashlib.sha256((p/'source-motions.npz').read_bytes()).hexdigest()==decision['motion_array_sha256']:return p
    if animal in {'Coastal squid (guild)','Sea turtle'}:return None
    if animal in QUADRUPED_PRIMATE_TRIALS:key='fox' # Explicit quadrupedal transfer trial, not primate-authored motion.
    elif animal=='Sea lion':key='front-pinniped-trial'
    elif animal=='Walrus':key='hind-pinniped-trial'
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

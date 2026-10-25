"""Audit the full fauna inventory against current files, not queue labels.

Read-only for models/reviews. The compact report is ignored evidence, not an
art, gait, contact or collision approval. Missing checks remain explicit.
"""
import collections, datetime, hashlib, json
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
batch = repo/'artifacts/fauna-overnight-20261024'
plan = json.loads((repo/'docs/fauna/asset-plan.json').read_text())['rows']
variation = json.loads((repo/'docs/fauna/SPECIES_VARIATION_PLANS.json').read_text())
choices = json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())
digest_cache = {}

def sha(path):
    path = Path(path)
    if path not in digest_cache:
        digest_cache[path] = hashlib.sha256(path.read_bytes()).hexdigest()
    return digest_cache[path]

def read(path):
    return json.loads(path.read_text()) if path.is_file() else {}

assert len({r['slug'] for r in plan}) == len(plan)
assert len({r['priority_rank'] for r in plan}) == len(plan)
rows = []
for entry in plan:
    slug = entry['slug']; root = batch/slug; problems = []
    appearance = variation['animals'].get(entry['animal'], {})
    for field in ('texture_plan', 'sex_age_season', 'extras', 'hypothetical_variation'):
        if not entry.get(field): problems.append('Missing plan '+field)
    for field in ('real_analogues', 'texture_assets',
                  'sex_age_season', 'extras', 'paint_details', 'hypotheses',
                  'geometry_limits', 'sources'):
        if not appearance.get(field): problems.append('Missing variation '+field)
    if entry['asset_status'] in ('queued', 'accepted_baseline') and not appearance.get('coat_configurations'):
        problems.append('Missing visual coat configurations')
    row = {'slug':slug, 'name':entry['animal'], 'priority':entry['priority_rank'],
           'scope':entry['asset_status'], 'problems':problems}
    if entry['asset_status'] != 'queued':
        if entry['asset_status']=='deferred' and entry['representation']!='Skip for now':
            problems.append('Deferred classification conflicts with roster representation')
        if entry['asset_status']=='audio_existing' and not entry['representation'].startswith('Audio only'):
            problems.append('Audio classification conflicts with roster representation')
        row['raw_audition_requirement'] = 'Existing baseline or explicitly nonvisual/deferred'
        rows.append(row); continue
    meta = read(root/'audition.json'); native = read(root/'native-checks.json')
    blend = root/'Animal - Raw Audition.blend'
    if not blend.is_file(): problems.append('Missing native raw review')
    if len(meta.get('candidates', [])) != 4: problems.append('Raw review is not four candidates')
    if meta.get('painted') is not False: problems.append('Raw appearance not explicitly unpainted')
    row['raw_inputs'] = []
    for number in range(1,5):
        candidate = root/f'candidate-{number}'
        mesh = candidate/'model.glb'; rig = candidate/'generated-rig.npz'
        # Own procedural insects may be built directly into the native review.
        procedural = entry['family'] == 'microfly'
        item = {'candidate':number, 'mesh_exists':mesh.is_file(), 'rig_exists':rig.is_file()}
        if not procedural and not (mesh.is_file() and rig.is_file()):
            problems.append(f'Missing generated mesh/rig input {number}')
        row['raw_inputs'].append(item)
    row['native_evidence_matches'] = bool(blend.is_file() and
        native.get('checked_blend_sha256') == sha(blend) and native.get('controls') and
        len(native.get('candidates', [])) == 4)
    if not row['native_evidence_matches']:
        problems.append('Current raw native check missing exact SHA or four-candidate/control proof')
    if any(not candidate.get('actions') for candidate in meta.get('candidates', [])):
        problems.append('Candidate has no named motion clips')
    source = Path(meta['motion_bank']) if meta.get('motion_bank') else None
    if source:
        source_rig = source/'source-rig.json'; source_motion = source/'source-motions.npz'
        source_meta = read(source_rig)
        row['source_license'] = source_meta.get('license')
        row['motion_bank'] = str(source)
        for path,key in ((source_rig,'motion_rig_sha256'),(source_motion,'motion_array_sha256')):
            if not path.is_file() or meta.get(key) != sha(path):
                problems.append('Motion source missing or changed: '+key)
        if row['source_license'] not in ('CC0-1.0', 'CC0', 'CC0-1.0 verified at source page',
                'Project-authored procedural motion', 'Project-authored procedural motion; no third-party motion'):
            problems.append('Motion license needs explicit provenance review')
    elif entry['family'] != 'microfly':
        problems.append('No motion source bank recorded')
    row['motion_limits'] = meta.get('motion_gaps', [])
    selected = choices.get('animals',{}).get(slug,{})
    row['selected_models'] = selected.get('selected_models', [])
    if selected.get('manual_fix_blend'):
        fixed = repo/selected['manual_fix_blend']; evidence = read(root/'owner-fix-native-checks.json')
        row['selected_correction_matches'] = bool(fixed.is_file() and sha(fixed) ==
            selected.get('manual_fix_blend_sha256') == evidence.get('checked_blend_sha256'))
        if not row['selected_correction_matches']: problems.append('Selected correction SHA mismatch')
    rows.append(row)

reused = variation['shared_texture_source_plan']['accepted_reuse_inventory']
preserved = sha(repo/reused['blend']) == reused['blend_sha256']
report = {'time':datetime.datetime.now().astimezone().isoformat(),
          'scope_counts':dict(collections.Counter(row['scope'] for row in rows)),
          'rows_with_gaps':sum(bool(row['problems']) for row in rows),
          'accepted_deer_preserved':preserved, 'rows':rows,
          'claim_limits':'Checks inventory, plans, exact native/source identities and recorded bounded checks. Does not prove natural gait, skin quality, license of model weights or biological accuracy of every alias.'}
(batch/'full-readiness-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k!='rows'},indent=2))
for row in rows:
    if row['problems']: print(row['slug']+': '+'; '.join(row['problems']))

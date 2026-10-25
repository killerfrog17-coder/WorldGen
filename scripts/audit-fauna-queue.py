"""Audit the full raw asset scope without launching Blender or changing reviews.

This checks retained files/provenance and existing native-check evidence. It is
not a new visual, collision, natural-motion or production-acceptance test.
"""
import argparse
import csv
import datetime
import hashlib
import json
from pathlib import Path


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    repo = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=repo / 'artifacts/fauna-overnight-20261024')
    args = parser.parse_args()
    root = args.root.resolve()
    rows = read(repo / 'docs/fauna/asset-plan.json')['rows']
    details = read(repo / 'docs/fauna/SPECIES_VARIATION_PLANS.json')['animals']
    choices = read(repo / 'docs/fauna/OWNER_SELECTIONS.json')
    errors, pending, verified, sources = [], [], [], {}

    def require(condition, description):
        if not condition:
            raise ValueError(description)

    try:
        require(len(rows) == 188, 'Catalogue scope must remain188')
        statuses = {key: sum(r['asset_status'] == key for r in rows)
                    for key in ('queued', 'accepted_baseline', 'audio_existing', 'deferred')}
        require(statuses == {'queued': 176, 'accepted_baseline': 1, 'audio_existing': 5, 'deferred': 6},
                'Representation scope changed')
        require(len({r['slug'] for r in rows}) == len(rows), 'Duplicate animal slugs')
        for row in rows:
            if row['asset_status'] == 'queued':
                require(row['raw_candidates'] == 4, row['animal'] + ': plan must allocate four raw candidates')
            detail = details[row['animal']]
            for key in ('texture_assets', 'sex_age_season', 'extras',
                        'paint_details', 'hypotheses', 'geometry_limits', 'sources'):
                require(bool(detail[key]), row['animal'] + ': missing appearance planning ' + key)
            visual = row['asset_status'] in ('queued', 'accepted_baseline')
            require(bool(detail['coat_configurations']) == visual,
                    row['animal'] + ': coat allocation disagrees with visual/audio/deferred scope')
            require(row['appearance_variation_plan'] == detail, row['animal'] + ': stale plan mirror')
        with (repo / 'docs/fauna/FAUNA_ASSET_PLAN.csv').open(encoding='utf-8-sig', newline='') as stream:
            table = list(csv.DictReader(stream))
        require([(r['Animal'], int(r['Rank'])) for r in table] ==
                [(r['animal'], r['priority_rank']) for r in rows], 'Editable table roster/order mismatch')
        for cell, row in zip(table, rows):
            require(cell['Status'] == row['asset_status'] and
                    int(cell['Candidates']) == row['raw_candidates'] and
                    int(cell['Triangles']) == row['triangle_budget'] and
                    cell['Coats'] == '; '.join(row['texture_plan']['coats']) and
                    cell['Texture allocation'] == row['appearance_variation_plan']['texture_assets'],
                    row['animal'] + ': editable table allocation/coat mismatch')
        require(set(choices['animals']).issubset({r['slug'] for r in rows if r['asset_status'] == 'queued'}),
                'Owner choice outside the new visual roster')
        baseline = repo / 'artifacts/fauna-assets/deer'
        require(digest(baseline / 'Deer - Prototype.blend') == read(baseline / 'asset.json')['blend_sha256'],
                'Accepted deer changed')
    except (KeyError, OSError, ValueError) as exc:
        errors.append({'scope': 'catalogue/plans/accepted deer', 'error': str(exc)})

    for row in rows:
        if row['asset_status'] != 'queued':
            continue
        folder = root / row['slug']
        if (not all((folder / n).exists() for n in
                    ('audition.json', 'native-checks.json', 'Animal - Raw Audition.blend'))
                or any((folder / n).exists() for n in ('review-rebuild.json', 'source-trial-pending.json'))):
            pending.append(row['slug'])
            continue
        try:
            meta, checks = read(folder / 'audition.json'), read(folder / 'native-checks.json')
            # Two established procedural viewers use shorter display labels.
            # These aliases identify the same guild; no art/file rewrite needed.
            display_aliases = {'dragonflies-guild': 'Dragonflies',
                               'native-detritivorous-flies-guild': 'Detritivorous flies'}
            require(meta['animal'] == row['animal'] or
                    (row['slug'] in display_aliases and meta['animal'] == display_aliases[row['slug']]),
                    'Wrong animal metadata')
            require(meta.get('painted') is False, 'Raw audition has painting flag')
            require(len(meta['candidates']) == len(checks['candidates']) == 4, 'Four candidates required')
            require(checks['controls'] is True, 'Missing native control evidence')
            require({c['number'] for c in meta['candidates']} == {1, 2, 3, 4}, 'Candidate numbering mismatch')
            record = {'slug': row['slug'], 'review_sha256': digest(folder / 'Animal - Raw Audition.blend'),
                      'native_checks_sha256': digest(folder / 'native-checks.json'), 'candidates': []}
            if row['family'] != 'microfly':
                bank = Path(meta['motion_bank']).resolve()
                require(bank.is_relative_to(repo / 'artifacts/fauna-motion-sources'), 'Unexpected motion root')
                rig_hash, array_hash = digest(bank / 'source-rig.json'), digest(bank / 'source-motions.npz')
                require((rig_hash, array_hash) ==
                        (meta['motion_rig_sha256'], meta['motion_array_sha256']), 'Motion source changed')
                source = read(bank / 'source-rig.json')
                license_name = source.get('license', '')
                require('CC0' in license_name or license_name.startswith('Project-authored'),
                        'Motion license is not CC0 or explicitly project-authored')
                require(source.get('donor_mesh_exported') is not True, 'Foreign donor geometry flagged')
                origin = Path(source.get('source', ''))
                if origin.is_absolute() and not origin.exists():
                    chain = read(bank / 'retained-source-provenance.json')
                    require(chain['rig_metadata_sha256'] == rig_hash and
                            chain['motion_array_sha256'] == array_hash and
                            chain['historical_review']['sha256'] == source['source_sha256'],
                            'Historical source chain does not match retained bank')
                    canonical = (repo / chain['retained_canonical_bank']).resolve()
                    require(canonical.is_relative_to(repo) and digest(canonical / 'source-motions.npz') == array_hash,
                            'Canonical retained motion arrays changed')
                    for key in ('original_cc0_motion_asset', 'license_file', 'tool_license'):
                        retained = chain[key]
                        path = (repo / retained['path']).resolve()
                        require(path.is_relative_to(repo) and digest(path) == retained['sha256'],
                                'Retained original source/license does not match provenance')
                sources[str(bank.relative_to(repo))] = {'rig_sha256': rig_hash, 'arrays_sha256': array_hash,
                                                       'license': license_name}
            for candidate in meta['candidates']:
                number = candidate['number']
                native = next(c for c in checks['candidates'] if c['candidate'] == number)
                require(0 < candidate['triangles'] <= row['triangle_budget'], 'Triangle budget exceeded')
                require(candidate['triangles'] == native['triangles'], 'Native triangle evidence mismatch')
                require(native['all_clip_samples_finite'] and native['joint_error_m'] < 1e-5,
                        'Native sampled pose/rest evidence failed')
                require(candidate['actions'] and candidate['demos'], 'No animation clips/demos')
                item = {'number': number, 'triangles': candidate['triangles']}
                if row['family'] != 'microfly':
                    directory = folder / ('candidate-' + str(number))
                    rig = read(directory / 'generated-rig.json')
                    if row['slug']=='boa-constrictor' and rig['method']=='Owner-authorized body centerline rig':
                        require(rig.get('manual_rig') and candidate.get('rig_origin')=='manual body centerline','Undeclared manual snake rig')
                        require(rig['source_mesh_sha256']==digest(directory/'model.glb'),'Manual rig source mesh changed')
                    elif row['slug']=='pronghorn' and number==4 and rig['method']=='Owner-permitted accepted deer model reuse':
                        require(rig['source_rig_sha256']==digest(repo/'artifacts/fauna-assets/deer/generated-rig.npz'),'Deer fallback provenance changed')
                        require(digest(directory/'generated-rig.npz')==rig['source_rig_sha256'],'Deer fallback rig changed')
                    else:require(rig['method'] == 'Local SkinTokens skeleton and skin generation', 'Not fresh local rig inference')
                    require(Path(rig['mesh']).resolve() == (directory / 'model.glb').resolve(), 'Rig input belongs to another mesh')
                    require(Path(candidate['rig_file']).resolve() == (directory / 'generated-rig.npz').resolve(), 'Wrong candidate rig path')
                    require((directory / 'model.glb').read_bytes()[:4] == b'glTF', 'Invalid retained model header')
                    item.update(model_sha256=digest(directory / 'model.glb'),
                                rig_sha256=digest(directory / 'generated-rig.npz'))
                    selected = choices['animals'].get(row['slug'])
                    if selected and number in selected['selected_models']:
                        require(item['model_sha256'] == selected['model_sha256'] and
                                item['rig_sha256'] == selected['rig_sha256'], 'Owner-selected files changed')
                else:
                    require(candidate.get('geometry_method', '').startswith('Own procedural'), 'Unexpected microfauna geometry origin')
                record['candidates'].append(item)
            verified.append(record)
        except (KeyError, OSError, ValueError, StopIteration) as exc:
            errors.append({'scope': row['slug'], 'error': str(exc)})

    result = {'time': datetime.datetime.now().astimezone().isoformat(), 'scope': '188 entries /176 new visual sets /four candidates each',
              'planning_and_artifact_errors': errors, 'pending_raw_sets': pending, 'verified_raw_sets': verified,
              'motion_sources': sources, 'raw_inventory_complete': not errors and not pending and len(verified) == 176,
              'limits': 'Hashes, source declarations and prior bounded native-check evidence only. Not visual acceptance, full-pose collision checking, ecological/motion suitability certification, texture completion or permission to paint.'}
    temporary = root / 'queue-audit.json.tmp'
    temporary.write_text(json.dumps(result, indent=2))
    temporary.replace(root / 'queue-audit.json')
    print(json.dumps({'verified_raw_sets': len(verified), 'pending_raw_sets': len(pending),
                      'errors': errors, 'raw_inventory_complete': result['raw_inventory_complete']}))
    return bool(errors)


if __name__ == '__main__':
    raise SystemExit(main())

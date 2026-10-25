"""Prune an exact owner-rejected archive after its checked raw replacement.

Default is read-only. All archived files must match the pre-reroll manifest;
retained candidate files, references and current review are never deleted.
"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    repo = Path(__file__).resolve().parent.parent
    batch = (repo / 'artifacts/fauna-overnight-20261024').resolve()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--animal-root', required=True, type=Path)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    root = args.animal_root.resolve()
    old = (root / 'previous-owner-rejected').resolve()
    assert root.parent == batch and old.parent == root and old.name == 'previous-owner-rejected'
    assert old.is_dir() and not old.is_symlink()
    decision_path=root/'source-trial-reviewed.json'
    if decision_path.exists():
        decision = json.loads(decision_path.read_text())
        assert decision['decision'] == 'usable_for_raw_audition'
    else:
        # A fresh replacement using an established bank is not a new source
        # qualification. Require a checked current-file proof instead.
        proof=json.loads((root/'replacement-checked.json').read_text())
        assert proof['raw_replacement_checked'] and not proof['art_approved']
        assert proof['review_sha256']==digest(root/'Animal - Raw Audition.blend')
        assert (root/'owner-reroll-request-current.json').exists()
        assert not (root/'source-trial-pending.json').exists()
    checks = json.loads((root / 'native-checks.json').read_text())
    assert len(checks['candidates']) == 4 and checks['controls']
    retained = [root / 'Animal - Raw Audition.blend']
    retained += [root / f'candidate-{i}' / name for i in range(1, 5)
                 for name in ('model.glb', 'generated-rig.npz')]
    retained_hashes = {str(p.relative_to(root)): digest(p) for p in retained}
    manifest = json.loads((old / 'candidate-manifest.json').read_text())
    assert {r['candidate'] for r in manifest} == {1, 2, 3, 4}
    expected, records = set(), []
    for row in manifest:
        candidate = (old / f"candidate-{row['candidate']}").resolve()
        assert candidate.parent == old
        for item in row['files']:
            path = (candidate / item['path']).resolve()
            assert path.is_relative_to(candidate) and path.is_file() and not path.is_symlink()
            assert path.stat().st_size == item['bytes'] and digest(path) == item['sha256']
            expected.add(path)
            records.append(dict(item, path=str(path.relative_to(old))))
    for name in ('candidate-manifest.json', 'reference.png', 'native-checks.json', 'audition.json'):
        path = (old / name).resolve()
        assert path.parent == old and path.is_file() and not path.is_symlink()
        expected.add(path)
        records.append({'path': name, 'bytes': path.stat().st_size, 'sha256': digest(path)})
    assert {p.resolve() for p in old.rglob('*') if p.is_file()} == expected, 'Unmanifested archived files'
    provenance = root / 'previous-rejection-provenance.json'
    assert not provenance.exists(), 'Preserve prior rejection provenance'
    total = sum(item['bytes'] for item in records)
    if args.apply:
        previous = json.loads((old / 'audition.json').read_text())
        provenance.write_text(json.dumps({
            'owner_rejected_previous': True, 'time': datetime.datetime.now().astimezone().isoformat(),
            'replacement_checked_for_raw_review_only': True,
            'old_motion_rig_sha256': previous['motion_rig_sha256'],
            'old_motion_array_sha256': previous['motion_array_sha256'],
            'old_files': records, 'candidate_manifest': manifest,
            'retained_sha256': retained_hashes, 'pruned_bytes': total}, indent=2))
        for path in expected:
            path.unlink()
        for path in sorted((p for p in old.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
            path.rmdir()
        old.rmdir()
        assert all(digest(root / name) == value for name, value in retained_hashes.items())
    print(json.dumps({'animal': root.name, 'applied': args.apply, 'verified_files': len(records),
                      'rejected_archive_mib': round(total / 2**20, 2), 'retained_hashes_unchanged': True}))


if __name__ == '__main__':
    main()

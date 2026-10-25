"""Render missing front/rear bind views of ready, unselected fauna on CPU.

This records image provenance, not an automatic art/deformation verdict. The
raw review files are read only. Resume skips only images from the same blend.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

repo = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root', type=Path, default=repo / 'artifacts/fauna-overnight-20261024')
parser.add_argument('--limit', type=int)
args = parser.parse_args()
root = args.root.resolve()
assert root.is_relative_to(repo / 'artifacts')
blender = repo / 'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
report_path = root / 'alignment-sweep.json'
report = json.loads(report_path.read_text()) if report_path.exists() else {'images': {}, 'failures': []}
report.update(pid=os.getpid(), status='running', art_approved=False,
              limits='CPU bind images only; no natural gait, collision or owner approval implied.')


def save():
    report['updated'] = datetime.datetime.now().astimezone().isoformat()
    temporary = report_path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(report, indent=2) + '\n')
    # Windows readers/indexers can briefly deny FILE_SHARE_DELETE even when
    # these task-owned files are writable. Retry this same atomic replacement;
    # never discard completed image evidence or rerender merely for that race.
    for attempt in range(20):
        try:
            temporary.replace(report_path)
            return
        except PermissionError:
            if attempt == 19:
                raise
            time.sleep(.1)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paused():
    control = root / 'run-control.json'
    return control.exists() and json.loads(control.read_text()).get('mode') == 'pause'


choices = json.loads((repo / 'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals']
plan = json.loads((repo / 'docs/fauna/asset-plan.json').read_text())['rows']
families = {row['slug']: row['family'] for row in plan}
ready = json.loads((root / 'review-index.json').read_text())['ready']
ready = [row for row in ready if row['slug'] not in choices and families[row['slug']] != 'microfly']
if args.limit is not None:
    ready = ready[:args.limit]
save()
try:
    for row in ready:
        folder = root / row['slug']
        blend = folder / 'Animal - Raw Audition.blend'
        blend_hash = digest(blend)
        for number in range(1, 5):
            for view in ('front', 'rear'):
                if paused():
                    report['status'] = 'checkpointed'
                    save()
                    sys.exit(0)
                key = f"{row['slug']}/{number}-Bind-{view}"
                image = folder / 'previews' / f'{number}-Bind-{view}.png'
                prior = report['images'].get(key, {})
                if prior.get('blend_sha256') == blend_hash and image.exists() and prior.get('image_sha256') == digest(image):
                    continue
                report['current'] = key
                save()
                command = [str(blender), '--background', '--threads', '3', '--python-exit-code', '1',
                           '--python', str(repo / 'scripts/render-fauna-diagnostic.py'), '--',
                           '--root', str(folder), '--model', str(number), '--clip', 'Bind', '--view', view]
                with (folder / 'alignment-render.log').open('w') as log:
                    result = subprocess.run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT)
                if result.returncode or not image.exists() or digest(blend) != blend_hash:
                    report['failures'].append({'key': key, 'returncode': result.returncode,
                                               'reason': 'Render failed, missing image or review changed during render'})
                    save()
                    continue
                report['images'][key] = {'blend_sha256': blend_hash, 'image': str(image.relative_to(root)),
                                         'image_sha256': digest(image), 'view': view, 'clip': 'Bind', 'device': 'CPU'}
                save()
        print('ALIGNMENT VIEWS', row['slug'], flush=True)
    report['status'] = 'rendered_snapshot'
except Exception as exc:
    report['status'] = 'failed'
    report['error'] = str(exc)
    raise
finally:
    report['current'] = None
    save()

"""Refresh missing exact-file native evidence with two bounded CPU workers.

Never rebuilds or overwrites a review/model. Uses the full readiness audit's
explicit evidence gap, leaves current GUI and all selected correction files alone.
"""
import concurrent.futures, datetime, json, subprocess, sys
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
batch = repo/'artifacts/fauna-overnight-20261024'
blender = repo/'artifacts/tools/blender-4.5.15/blender-4.5.15-windows-x64/blender.exe'
audit = json.loads((batch/'full-readiness-audit.json').read_text())
slugs = [row['slug'] for row in audit['rows'] if row.get('native_evidence_matches') is False]
results = []
status = batch/'native-evidence-refresh-status.json'

def save():
    status.write_text(json.dumps({'total':len(slugs), 'finished':len(results),
        'results':results, 'updated':datetime.datetime.now().astimezone().isoformat()},indent=2)+'\n')

def check(slug):
    root = batch/slug
    with (root/'native-evidence-refresh.log').open('w') as log:
        process = subprocess.run([str(blender),'--background','--threads','2',
            '--python-exit-code','1','--python',str(repo/'scripts/check-fauna-audition.py'),
            '--','--root',str(root)],cwd=repo,stdout=log,stderr=subprocess.STDOUT)
    return {'slug':slug, 'exit_code':process.returncode}

save()
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    futures = [pool.submit(check,slug) for slug in slugs]
    for future in concurrent.futures.as_completed(futures):
        result = future.result(); results.append(result); save()
        print(f"{len(results)}/{len(slugs)} {result['slug']} exit={result['exit_code']}",flush=True)
if any(result['exit_code'] for result in results):sys.exit(1)
print('Exact-file native evidence refreshed; reviews unchanged.',flush=True)

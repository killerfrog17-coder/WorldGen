"""Preserve flight checks and create the concise final native-player report."""
from pathlib import Path
import csv
import importlib
import json
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools/reporting/python'))
sys.path.insert(0, str(ROOT / 'scripts'))
from PIL import Image

OUT = ROOT / 'docs/reports/fast-flight-20261110'
ART = ROOT / 'artifacts/chunk-loading-20261109'
OUT.mkdir(parents=True, exist_ok=True)
cases = [('Normal forest / smooth', 'flight-forest-normal-v14'),
         ('Boosted canyon / hard', 'flight-canyon-hard-v15'),
         ('Normal forest / hard', 'flight-forest-hard-normal-v15'),
         ('Long flight / smooth', 'flight-forest-endurance-v14')]
results = []
for label, name in cases:
    folder = ART / name
    assert (folder / 'complete.txt').exists(), name
    summary = (folder / 'fast-flight.txt').read_text(encoding='utf-8-sig')
    samples = list(csv.DictReader((folder / 'fast-flight.csv').open(encoding='utf-8-sig')))
    work = (folder / 'flight-work.txt').read_text(encoding='utf-8-sig')
    time = None
    audits = []
    for line in work.splitlines():
        match = re.match(r'([0-9.]+)s:', line)
        if match:
            time = float(match[1])
        match = re.search(r'ground gaps=(\d+)/3072', line)
        if match:
            sample = min(samples, key=lambda r: abs(float(r['seconds']) - time))
            audits.append(dict(seconds=time, empty_pixels=int(match[1]), height_above_ground=float(sample['altitude'])))
    land = [r['empty_pixels'] for r in audits if r['height_above_ground'] <= 550]
    result = dict(name=name, label=label,
                  package=json.loads((folder / 'package.json').read_text(encoding='utf-8-sig')),
                  no_resident_frames=int(re.search(r'frames with no resident ground at player=(\d+)', summary)[1]),
                  handoffs=int(re.search(r'handoffs=(\d+)', summary)[1]),
                  max_land_forward_empty_pixels=max(land, default=None),
                  forward_audits=audits,
                  final_east_m=float(samples[-1]['east']),
                  peak_east_m=max(float(r['east']) for r in samples),
                  summary=summary)
    results.append(result)
    evidence = OUT / 'evidence' / name
    evidence.mkdir(parents=True, exist_ok=True)
    for file in ('fast-flight.txt', 'fast-flight.csv', 'flight-work.txt', 'chunk-loading.txt', 'package.json', 'review-site.json'):
        shutil.copyfile(folder / file, evidence / file)

images = [('forest.jpg', 'flight-forest-normal-v14', 'flight-view-8.png'),
          ('canyon.jpg', 'flight-canyon-hard-v15', 'flight-view-8.png'),
          ('before-chart.jpg', 'flight-forest-endurance-v14', 'flight-view-19.png'),
          ('after-chart.jpg', 'flight-forest-endurance-v14', 'flight-view-23.png')]
for destination, run, file in images:
    Image.open(ART / run / file).convert('RGB').save(OUT / destination, quality=91)

checks = {}
for name, files in [('hard-update-shader-v15', ['coverage-shader.txt', 'fallback-publication.txt', 'hard-chunk-updates.txt']),
                    ('hard-settings-v15', ['complete.txt'])]:
    for file in files:
        content = (ART / name / file).read_text(encoding='utf-8-sig')
        checks[name + '/' + file] = content
        target = OUT / 'evidence' / name / file
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding='utf-8')

table = [[r['label'], str(r['no_resident_frames']), str(r['max_land_forward_empty_pixels']) + '/3072', str(r['handoffs'])] for r in results]
data = dict(title='WorldGen - Fast Flight and Chunk Updates', section='STREAMING REVIEW', date='10 NOVEMBER 2026',
    footer='Actual native captures / empty spectator / shared PC / no FPS-floor claim', pages=[
    dict(title='Fast-flight streaming', subtitle='Terrain continuity, bounded forest coverage, and an optional instant replacement mode.', blocks=[
        dict(heading='What changed', text='Movement retains the last terrain-aware detail plan while its successor is calculated. The old path repeatedly replaced it with a coarse guess. Near flight coverage now uses background tiles with at most 64 m sampling instead of relying solely on enormous eight-segment placeholders. Existing coarse forest stands load ahead of exact tree pages; planting and item/tree identities are unchanged.'),
        dict(text='Ground-query and terrain-anchor preparation move off the main thread where possible. Covered flight no longer repeatedly cancels useful background work. Geographic frames last longer, independently of the small floating render origin. A reversed coarsening fade and a fallback-publication loop were found and fixed.'),
        dict(heading='Actual traversal checks', table=dict(headers=['Run', 'Missing foot frames', 'Worst land-view audit', 'Handoffs'], rows=table, widths=[.35,.20,.29,.16])),
        dict(text='Normal flight: 200 m/s. Boosted canyon: 200 then 600 m/s, with a reversal. Normal smooth and canyon runs last 80 s; normal hard lasts 40 s. The long run lasts 260 s at 600 m/s, travelling about 156 km and crossing a geographic frame boundary. All runs use High terrain, Medium vegetation and a 6 km or 7.2 km view. Normal forest uses Balanced loading (two workers); canyon and the long run use Maximum (four).', size=9),
        dict(heading='Hard chunk updates', text='Settings > Terrain > Hard chunk updates. Enabled: ready replacements snap into view. Disabled: the existing smooth transitions remain. Switching on during a fade immediately clears the old draw. The setting persists without rebuilding terrain or changing loading fog.'),
        dict(heading='What the numbers mean', text='The forward audit checks the bottom third of a ground-only render against repeated empty controls; land samples use a height above ground of at most 550 m. Deep ocean can legitimately have no seabed within the camera range and is excluded from this land column. These are sampled coverage checks, not a proof that every possible view is flawless.', size=9)]),
    dict(title='Moving-world captures', subtitle='Native player screenshots from the routes above; this is a streaming pass, not an art overhaul.', blocks=[
        dict(images=[dict(file='forest.jpg', caption='200 m/s forest flight, Balanced loading, smooth updates. Existing aggregate stands provide coverage while exact trees load.')], max_height=245),
        dict(images=[dict(file='canyon.jpg', caption='Boosted canyon route with Hard chunk updates enabled. Terrain is present ahead instead of the former large flat fallback patches.')], max_height=245),
        dict(text='Screenshots show different positions and environments; they are not a matched before/after pair. Distant canopy and temporary terrain detail remain approximate.', size=9)]),
    dict(title='Boundary check and limits', subtitle='A real geographic handoff, plus the remaining costs and verification scope.', blocks=[
        dict(images=[dict(file='before-chart.jpg', caption='Long flight before the geographic-frame change, about 58 km from the start.'), dict(file='after-chart.jpg', caption='Same continuous route after the frame change, about 70 km from the start.')], max_height=185),
        dict(heading='What passed', text='The 260 s run kept resident ground beneath the player and completed one actual frame handoff. Pure checks cover 104,883 plan/coverage assertions and 187 coordinate/handoff assertions. Native production-shader checks cover complementary fades, reversed coarsening, valid nonfinal fallback publication, and instant replacements. Preference tests verify persistence and restore the original saved keys.'),
        dict(heading='Remaining work', text='One geographic handoff took about 40 ms, including 33 ms projecting local scatter. Spreading that projection over earlier frames remains follow-up work. Temporary terrain, distant canopy and some boundaries still need visual refinement; this update does not finish terrain art or guarantee a 100 FPS floor.'),
        dict(heading='Measurement conditions', text='MapleLegends and YouTube shared the PC. Tests render at a paced 60 FPS and deliberately add extra camera renders, GPU readbacks and screenshots. Their frame timings are not gameplay FPS benchmarks. Chunk-ready timings exclude world-file reading and worker preparation. All scenes omit humans, camps, fauna and LLM workloads.', size=9),
        dict(heading='Reproducible inputs', text='The evidence folder preserves CSV samples, native reports, exact package receipts and source sites. This PDF and its four JPEG inputs live outside disposable artifacts. Earlier failed and visually rejected trials were retained as diagnostics and are not presented as successful results.', size=9)])])
(OUT / 'report.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
receipt = dict(source=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
               results=results, native_checks=checks, pure_checks=dict(streaming_plan=104883, handoff=187),
               limitations=['No qualified FPS claim on the shared PC', 'A measured geographic handoff still took 40 ms', 'Distant terrain and canopy remain approximate'],
               excluded=['v8 uniform-empty-control assumption failed', 'v12/v13 fallback staging loop caught and fixed before delivery', 'early forest captures at dusk rejected'])
(ROOT / 'assets/verification/fast-flight-streaming-20261110.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
importlib.import_module('build-geography-night-report').build(OUT / 'report.json', OUT / 'WorldGen - Fast Flight and Chunk Updates 2026-11-10.pdf')

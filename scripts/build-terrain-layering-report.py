"""Preserve accepted native captures and build the terrain-layering handoff."""
from pathlib import Path
import csv
import importlib
import json
import re
import shutil
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'scripts'))
OUT = ROOT / 'docs/reports/terrain-layering-20261110'
ART = ROOT / 'artifacts/chunk-loading-20261109'
LAYER = ROOT / 'artifacts/terrain-layers-20261110'


def collect(name):
    folder = ART / name
    assert (folder / 'complete.txt').exists(), name
    text = (folder / 'chunk-loading.txt').read_text(encoding='utf-8-sig')
    value = lambda label: (float(m[1]) if (m := re.search(re.escape(label) + r'([0-9.]+)', text)) else None)
    result = dict(name=name, near=value('near seconds: '), radius600=value('radius600 seconds: '),
                  complete=value('complete seconds: '), preparation_ms=value('Preparation ms: '), summary=text)
    target = OUT / 'evidence' / name
    target.mkdir(parents=True, exist_ok=True)
    for file in ('chunk-loading.txt', 'chunk-loading.csv', 'fast-flight.txt', 'fast-flight.csv',
                 'flight-work.txt', 'review-site.json', 'package.json', 'terrain-build-stages.jsonl'):
        if (folder / file).exists():
            shutil.copyfile(folder / file, target / file)
    if (folder / 'fast-flight.txt').exists():
        flight = (folder / 'fast-flight.txt').read_text(encoding='utf-8-sig')
        result['missing_ground_frames'] = int(re.search(r'frames with no resident ground at player=(\d+)', flight)[1])
    return result


def build(final_name, canyon_name=None):
    OUT.mkdir(parents=True, exist_ok=True)
    before, after = collect('layers-baseline-60km'), collect(final_name)
    canyon = collect(canyon_name) if canyon_name else None
    pictures = [('near.jpg', 'chunk-loaded.png'), ('forest.jpg', 'flight-view-0.png'), ('moving.jpg', 'flight-view-3.png')]
    for destination, source in pictures:
        Image.open(ART / final_name / source).convert('RGB').save(OUT / destination, quality=92)
    if canyon:
        Image.open(ART / canyon_name / 'flight-view-3.png').convert('RGB').save(OUT / 'canyon.jpg', quality=92)
    for name in ('before', 'candidate-v4', 'contracts-v2', 'forest-physical-v2', 'canyon-physical-v2',
                 'river-stitch-physical-v2', 'backdrop-physical-v1'):
        source = LAYER / name
        if source.exists():
            for file in source.glob('*.json'):
                target = OUT / 'evidence' / name / file.name
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(file, target)
    number = lambda x: 'Not complete in 180 s' if x is None else f'{x:.2f} s'
    rows = [['Nearby terrain', number(before['near']), number(after['near'])],
            ['600 m terrain + forest', number(before['radius600']), number(after['radius600'])],
            ['Entire requested view', number(before['complete']), number(after['complete'])]]
    phase_names = ['Patch/index setup', 'Height + water queries', 'Surface materials', 'Boundary stitching', 'River-bank refinement', 'Water meshes', 'Cover masks']
    def phases(name):
        values = [json.loads(line)['Stages'] for line in (ART / name / 'terrain-build-stages.jsonl').read_text().splitlines()]
        totals = [sum(row[i] for row in values) for i in range(7)]
        return [100*v/sum(totals) for v in totals]
    phase_forest = phases(final_name)
    phase_canyon = phases(canyon_name) if canyon_name else None
    data = dict(title='WorldGen - Independent Terrain and Forest Detail', section='STREAMING REVIEW', date='10 NOVEMBER 2026',
        footer='Native game captures / Extreme terrain / 60 km view / no FPS-floor claim', pages=[
        dict(title='Independent levels of detail', subtitle='Exact terrain shape, cheaper surface sampling, and regional forest previews.', blocks=[
            dict(heading='What changed', text='Terrain height and water retain their canonical evaluators. Fine meshes sample ecological materials on a separate, chart-aligned 32 m grid; narrow water boundaries keep local queries, and slopes still control exposed rock and vegetation. Finer geometry no longer forces equally dense soil queries.'),
            dict(text='Distant forest pages contain at most 64 regional descriptors with no individual-tree probes. GPU previews reuse existing regional tree textures. Actual tree identities and placement are generated close to the camera. Previews wait for resident terrain and blend into ready exact tree pages.'),
            dict(heading='Matched loading check', table=dict(headers=['Milestone', 'Previous build', 'Candidate'], rows=rows, widths=[.4,.3,.3])),
            dict(text='Same forest site, Extreme terrain, Medium vegetation, Maximum loading effort, 1536 x 864 and a 60 km view. Times start after scene preparation and exclude world-file reading and worker initialization. The PC may also run MapleLegends and YouTube. Full-view readiness differs from the nearby playable footprint.', size=9),
            dict(heading='Where the work went', text='The initial exclusive component profile found height/water sampling dominated ordinary terrain tiles. Native profiling also exposed very costly river-bank refinement on coarse distant tiles. Fine soil queries fell from 4,225 to 25 on the sampled dry forest tile. Distant forest pages formerly used 8,192 to 262,144 tree probes; they now use zero.'),
            dict(text='River refinement now indexes short edge corridors instead of scanning every refined point in a large coarse cell. It prepares its dense river-query index when refinement is actually needed. Unused legacy height triangles are no longer constructed for amplified terrain patches.', size=9)]),
        dict(title='Actual game views', subtitle='Accepted captures from the final candidate; the first two untextured canopy trials were rejected.', blocks=[
            dict(images=[dict(file='near.jpg', caption='Near the ground: existing trees, resources and ground materials. This pass changes sampling and loading, not the tree models.')], max_height=245),
            dict(images=[dict(file='forest.jpg', caption='500 m above the forest, looking across the full 60 km view. Regional textured previews replace distant tree censuses.')], max_height=245),
            dict(text='Previews approximate coverage and tree type; they are not simulated trees. Detailed trees retain their deterministic world addresses. The canonical terrain and water checks exclude intentionally resampled material attributes.', size=9)]),
        dict(title='Movement and verification', subtitle='Coverage checks, physical equivalence and the remaining limitations.', blocks=[
            dict(images=[dict(file='moving.jpg', caption='Forward/reverse forest flight at 200 then 600 m/s. Extra GPU ground audits are diagnostic work, not an FPS benchmark.')]+([dict(file='canyon.jpg', caption='Canyon with instant replacements: thin dark coverage seams remain visible. This is a known rendering issue, not an accepted seam fix.')] if canyon else []), max_height=255),
            dict(heading='Checks', text=f'The final flight recorded {after.get("missing_ground_frames", "unknown")} frames without resident ground beneath the player. Pure checks cover 20,826 geometry/water and canopy contracts, plus 158,742 forest-placement, transport and hierarchy assertions. Packaged-worker comparisons preserve complete terrain geometry, normals, water and indices while excluding the intentionally changed surface attributes. Three broad backdrop packets also match in full.'),
            dict(heading='Limits', text='Loading is improved but not instant. Far canopy still approximates shape and density, with broad crown clusters visible from above. Thin terrain-coverage seams remain in fast canyon flight; disabling coverage masks removes those lines, so they need a separate resident-LOD boundary fix. These checks do not establish a universal 100 FPS floor or eliminate all streaming spikes. Shared PC demand, paced rendering, added camera renders and GPU readbacks make these frame timings unsuitable as gameplay FPS results.'),
            dict(heading='Reproducibility', text='The evidence folder retains package receipts, CSV samples, exact sites, worker comparisons and stage profiles. This PDF, its source manifest and JPEG inputs live outside disposable artifacts. Runtime changes are split into Git commits so individual approaches can be reverted.', size=9)])])
    if canyon:
        data['pages'][2]['blocks'].insert(2, dict(heading='Second environment', text=f'Canyon: 600 m terrain-and-forest readiness {number(canyon["radius600"])}; {canyon.get("missing_ground_frames", "unknown")} flight frames without resident ground beneath the player. Full requested view: {number(canyon["complete"])}.'))
        data['pages'][0]['blocks'] += [dict(heading='Remaining terrain-build cost', table=dict(headers=['Completed mesh stages', 'Forest', 'Canyon'], rows=[[name, f'{phase_forest[i]:.1f}%', f'{phase_canyon[i]:.1f}%'] for i, name in enumerate(phase_names)], widths=[.5,.25,.25])),
            dict(text='Shares of summed worker stage elapsed time, including cold loading and flight. These exclude scatter, rendering and world preparation, and are not percentages of total loading wall time. Height queries also include landform/water overlays.', size=8.5)]
    (OUT / 'report.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
    receipt = dict(before=before, after=after, canyon=canyon, terrain_stage_percent=dict(names=phase_names, forest=phase_forest, canyon=phase_canyon), rejected=['Untextured grid canopy v1', 'Mounded canopy v2'],
                   limitations=['No FPS-floor claim', 'Far canopy approximate', 'Diagnostic render overhead'])
    (ROOT / 'assets/verification/terrain-layering-20261110.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    importlib.import_module('build-geography-night-report').build(OUT / 'report.json', OUT / 'WorldGen - Terrain Layering 2026-11-10.pdf')


if __name__ == '__main__':
    build(sys.argv[1], sys.argv[2] if len(sys.argv)>2 else None)

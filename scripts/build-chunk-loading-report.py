"""Preserve concise, reproducible evidence from empty-spectator loading checks."""
from pathlib import Path
import json
import re
import shutil
import subprocess
import sys
import csv

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools/reporting/python'))
from PIL import Image
sys.path.insert(0, str(ROOT / 'scripts'))
from importlib import import_module

OUT = ROOT / 'docs/reports/chunk-loading-20261109'
OUT.mkdir(parents=True, exist_ok=True)
ART = ROOT / 'artifacts/chunk-loading-20261109'

def read(name):
    folder = ART / name
    assert (folder / 'complete.txt').exists(), name
    text = (folder / 'chunk-loading.txt').read_text(encoding='utf-8-sig')
    def number(label):
        match = re.search(re.escape(label) + r'([0-9.]+)', text)
        return float(match[1]) if match else None
    result = dict(name=name, radius600=number('radius600 seconds: '),
                  complete=number('complete seconds: '), near=number('near seconds: '),
                  preparation_ms=number('Preparation ms: '),
                  package=json.loads((folder / 'package.json').read_text(encoding='utf-8-sig')))
    census = re.search(r'fields (\d+)/(\d+), roots (\d+)', text)
    result['forest_census'] = [int(v) for v in census.groups()]
    result['site'] = json.loads((folder / 'review-site.json').read_text(encoding='utf-8-sig'))['Position']
    with (folder / 'chunk-loading.csv').open(encoding='utf-8-sig', newline='') as stream:
        samples = list(csv.DictReader(stream))
    result['observed_queue_maxima'] = {key:max(int(row[key]) for row in samples) for key in ('terrainJobs','scatterJobs','forestJobs','activeWorkers')}
    if 'legacy' not in name:
        assert max(int(r['terrainJobs'])+int(r['scatterJobs']) for r in samples) <= 4
        assert result['observed_queue_maxima']['forestJobs'] <= 2
        assert result['observed_queue_maxima']['activeWorkers'] <= 2
    for file in ('chunk-loading.txt', 'chunk-loading.csv', 'chunk-travel.txt', 'chunk-travel.csv', 'review-site.json', 'package.json'):
        if (folder / file).exists():
            dest = OUT / 'evidence' / name / file
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(folder / file, dest)
    return result

forest_before, forest_after, plains_before, plains_after = [read(name) for name in
    ('forest-legacy-2', 'forest-final-3', 'plains-legacy-1', 'plains-final-2')]
results = [forest_before, forest_after, plains_before, plains_after]

for name, title, capture in [('forest-final-3', 'forest', 'chunk-loaded.png'), ('plains-final-1', 'plains', 'chunk-travel.png')]:
    Image.open(ART / name / capture).convert('RGB').save(OUT / (title + '.jpg'), quality=88)

def seconds(value):
    return 'Not complete' if value is None else f'{value:.2f} s'

rows = []
for label, before, after in [('Temperate forest', forest_before, forest_after), ('Grassland', plains_before, plains_after)]:
    assert before['site'] == after['site'] and before['forest_census'] == after['forest_census']
    rows.append([label, seconds(before['radius600']), seconds(after['radius600']), seconds(before['complete']), seconds(after['complete'])])

data = dict(title='WorldGen - Chunk Loading', section='LOADING REVIEW', date='9 NOVEMBER 2026',
    footer='Actual empty-spectator checks / shared PC / no NPC or animal workload', pages=[
    dict(title='Chunk loading', subtitle='Fair publication, bounded background work, and a gradual loading horizon.', blocks=[
        dict(heading='What changed', text='Completed terrain payloads release their CPU lanes while awaiting upload, with a bounded queue. Nearby forest pages take priority over newer distant pages. Forest and local vegetation share upload opportunities. Vegetation projection uses more of its existing 1.5 ms slice instead of stopping at a low item count. Coarse background charts wait for the first visible neighbourhood.'),
        dict(heading='Matched loading measurements', table=dict(headers=['Environment', '600 m before', '600 m after', 'Full before', 'Full after'], rows=rows, widths=[.25,.185,.185,.19,.19])),
        dict(text='Both versions use the same generated site, High terrain detail, Balanced loading, Medium vegetation, a 900 m view, and 1536 x 864 rendering. Times begin after world-file reading and worker preparation; they measure chunk loading, not total startup. The baseline selects the previous scheduling path in the same test harness. MapleLegends and YouTube share the PC; small differences are uncertain.'),
        dict(heading='Fog controls', text='Terrain settings now have Loading fog and a transition-speed slider. The boundary eases outward rather than racing across the nearby ground. Turning the setting off eases toward the ordinary view-distance fog; atmospheric weather fog remains. These controls persist without rebuilding chunks. Coordinate reanchoring preserves the visible horizon.'),
        dict(heading='Validation and limits', text='8,679 focused fog checks passed. Actual renderer tests verify empty spectator mode, no human model, communities, camps or fauna, and a loaded 600 m terrain/forest footprint. A 400 m flight checks continued loading. Initial asset/mesh-upload stalls remain; this is not a 100 FPS floor claim. Terrain geometry, models, vegetation density and source-world data are unchanged.')]),
    dict(title='Actual test environments', subtitle='Native player captures; no camp, human, or animal workload.', blocks=[
        dict(images=[dict(file='forest.jpg', caption='Temperate forest: same stand and vegetation settings as its baseline.')], max_height=255),
        dict(images=[dict(file='plains.jpg', caption='Grassland traversal checkpoint; scene content is unchanged by the final empty-page scheduling refinement.')], max_height=255),
        dict(text='The screenshots document the test scenes, not an art update. This isolated harness omits the sky. The plains view follows the 400 m traversal, after the loading fog has cleared. Native CSV samples and package hashes are preserved beside this report so results remain reviewable after temporary artifacts are deleted.', size=9)])])
(OUT / 'report.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
receipt = dict(source=subprocess.check_output(['git','rev-parse','HEAD'], cwd=ROOT, text=True).strip(),
               conditions='Shared PC: MapleLegends / YouTube; empty spectator; no humans/camps/fauna; 60 FPS paced rendering, not an FPS benchmark.',
               fog_checks=8679, results=results,
               excluded=['baseline-1: obsolete detail harness threw before measurement', 'chunk-load-baseline-v1-morning: rejected NPC camp workload', 'settings-final and settings-final-windowed: hidden-window black captures rejected; preference roundtrip passed'],
               limitations=['World read and worker preparation excluded from chunk timings', 'Initial asset/upload stalls remain', 'No absolute FPS-floor claim', 'No terrain geometry/art changes'])
if (ART / 'glacial-metadata-final' / 'complete.txt').exists():
    receipt['long_view_before_metadata'] = read('glacial-long-view-final')
    receipt['long_view_after_metadata'] = read('glacial-metadata-final')
    a, b = receipt['long_view_before_metadata'], receipt['long_view_after_metadata']
    assert a['site'] == b['site'] and a['forest_census'] == b['forest_census']
    data['pages'][0]['blocks'].insert(3, dict(text='Long-view regression: at the same 6 km glacial site, empty forest census pages now share a bounded metadata budget without tree uploads. Full loading: '+seconds(a['complete'])+' before this final refinement, '+seconds(b['complete'])+' after. This is a separate sparse-region comparison.'))
if (ART / 'settings-final-visible' / 'complete.txt').exists():
    receipt['native_settings'] = (ART / 'settings-final-visible' / 'complete.txt').read_text(encoding='utf-8-sig')
    shutil.copyfile(ART / 'settings-final-visible' / 'complete.txt', OUT / 'evidence' / 'settings.txt')
    Image.open(ART / 'settings-final-visible' / 'terrain-settings.png').convert('RGB').save(OUT / 'terrain-settings.jpg', quality=88)
    data['pages'].append(dict(title='Terrain settings', subtitle='Loading fog controls continue farther down this panel.', blocks=[
        dict(images=[dict(file='terrain-settings.jpg',caption='Actual menu capture uses the saved owner settings. Timing tests explicitly use High terrain / Balanced loading.')],max_height=400),
        dict(text='Scroll below the detail-distance controls to find Loading fog and Fog transition. Native checks saved, reloaded and verified both values, checked that saving did not invalidate terrain plans, and restored the original preference keys. The visible player menu was inspected without GUI exceptions. No world, NPC, animal, or LLM host was started for this settings check.')]))
(OUT / 'report.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
(ROOT / 'assets/verification/chunk-loading-20261109.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
builder = import_module('build-geography-night-report')
builder.build(OUT / 'report.json', OUT / 'WorldGen - Chunk Loading 2026-11-09.pdf')

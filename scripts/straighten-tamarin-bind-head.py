"""Scoped manual bind correction for the third, unselected tamarin audition.

Warp the model, rig rest points and sampled skin positions together. This is
an explicit manual neck/head adjustment, not another successful neural reroll.
"""
import datetime, hashlib, json, shutil
from pathlib import Path
import numpy as np
import trimesh

repo = Path(__file__).resolve().parent.parent
root = repo / 'artifacts/fauna-overnight-20261024/tamarin'
candidate = root / 'candidate-3'
archive = root / 'candidate-3-agent-rejected-yaw-2'
assert candidate.resolve().parent == root.resolve() and not archive.exists()
assert 'tamarin' not in json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals']
archive.mkdir()
names = ('model.glb', 'generated-rig.npz', 'generated-rig.json')
before = {}
for name in names:
    path = candidate/name
    before[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    shutil.copy2(path, archive/name)

def warp(points):
    p = np.asarray(points).copy()
    u = np.clip((p[:,1]-1.15)/.50, 0, 1)
    v = np.clip((p[:,2]-.90)/.30, 0, 1)
    amount = (u*u*(3-2*u))*(v*v*(3-2*v))
    theta = np.deg2rad(25)*amount
    x, y = p[:,0].copy(), p[:,1]-1.4
    p[:,0] = np.cos(theta)*x - np.sin(theta)*y
    p[:,1] = 1.4 + np.sin(theta)*x + np.cos(theta)*y
    return p

mesh = trimesh.load(candidate/'model.glb', force='mesh', process=False)
points = mesh.vertices[:,[0,2,1]].copy(); points[:,1] *= -1
rig = dict(np.load(candidate/'generated-rig.npz'))
assert np.max(np.abs(points-rig['source_vertices'])) < 1e-5
points = warp(points)
converted = points[:,[0,2,1]].copy(); converted[:,2] *= -1
mesh.vertices = converted
mesh.export(candidate/'model.glb')
for name in ('joints', 'source_vertices', 'sample_points'):
    rig[name] = warp(rig[name])
np.savez_compressed(candidate/'generated-rig.npz', **rig)
proof = {'time':datetime.datetime.now().astimezone().isoformat(),
         'reason':'Native front inspection rejects residual head yaw after the single-candidate reroll',
         'method':'25 degree local head/neck bind rotation with smooth spatial falloff; mesh, rest joints and skin samples warped together',
         'source_sha256':before, 'manual_geometry_edit':True,
         'owner_selection_pending':True}
(candidate/'manual-head-straightening.json').write_text(json.dumps(proof,indent=2)+'\n')
(archive/'rejection.json').write_text(json.dumps(proof,indent=2)+'\n')
(root/'review-rebuild.json').write_text(json.dumps({'reason':proof['reason'],'manual_head_alignment':True},indent=2))
print('Tamarin3 bind head aligned; native rebuild required')

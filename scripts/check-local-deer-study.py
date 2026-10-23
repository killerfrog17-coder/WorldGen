"""Offline Blender asset checks, not a game/performance/visual acceptance test."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np


def main():
    output = Path(sys.argv[sys.argv.index('--')+1]).resolve()
    obj = bpy.data.objects['Locally generated deer']
    rig = bpy.data.objects['Generated Deer Rig']
    assert len(obj.data.polygons) == 50000
    assert len(rig.data.bones) == 27
    assert not any(pb.constraints for pb in rig.pose.bones)
    assert all(1 <= len(v.groups) <= 4 for v in obj.data.vertices)
    assert all(abs(sum(g.weight for g in v.groups)-1) < .003 for v in obj.data.vertices)
    clips = []
    def points():
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        return np.array([v.co[:] for v in evaluated.data.vertices])
    for name in ('Idle','Walk','Run'):
        action = bpy.data.actions[name]
        rig.animation_data.action = None
        for pb in rig.pose.bones:
            pb.matrix_basis.identity()
        rig.animation_data.action = action
        rig.animation_data.action_slot = action.slots[0]
        start,end = map(int,action.frame_range)
        positions = []
        lows, highs = [], []
        for frame in range(start,end+1):
            bpy.context.scene.frame_set(frame)
            vertices = points()
            assert np.isfinite(vertices).all(), (name,frame)
            assert np.max(np.abs(vertices)) < 3, (name,frame,'exploding skin')
            lows.append(vertices.min(0))
            highs.append(vertices.max(0))
            if frame in (start,end):
                positions.append(vertices)
        delta = float(np.max(np.abs(positions[1]-positions[0])))
        assert delta < .0001, (name,delta,'loop seam')
        clips.append({'clip':name,'frames_including_repeat':end-start+1,
            'loop_max_vertex_delta_m':delta,'bounds_min':np.min(lows,axis=0).tolist(),
            'bounds_max':np.max(highs,axis=0).tolist()})
    output.write_text(json.dumps({'clips':clips,'checks':'finite skin, four weights, normalized weights, closed loops, baked constraints',
        'not_checked':'game integration, locomotion blending, natural gait, ground-contact accuracy or FPS'},indent=2))
    print('Local deer asset checks passed',json.dumps(clips))


if __name__ == '__main__':
    main()

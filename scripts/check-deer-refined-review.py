"""Offline structural checks for the private AI motion study, not art acceptance."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np


def main():
    output=Path(sys.argv[sys.argv.index('--')+1]).resolve()
    obj=bpy.data.objects['Locally generated deer']
    rig=bpy.data.objects['AI Deer Rig']
    assert len(rig.data.bones)==33
    assert len(obj.data.uv_layers)>0
    assert all(1<=len(v.groups)<=4 for v in obj.data.vertices)
    assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in obj.data.vertices)
    assert not any(b.constraints for b in rig.pose.bones)
    rows=[]
    for name in bpy.context.scene['review_clips'].split('|'):
        action=bpy.data.actions[name]
        rig.animation_data.action=action
        rig.animation_data.action_slot=action.slots[0]
        minimum,maximum=1e6,-1e6
        for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
            bpy.context.scene.frame_set(frame)
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            values=np.array([v.co[:] for v in evaluated.data.vertices])
            assert np.isfinite(values).all(),(name,frame)
            assert np.max(np.abs(values))<4,(name,frame,'exploding skin')
            minimum=min(minimum,float(values[:,2].min()))
            maximum=max(maximum,float(values[:,2].min()))
        rows.append({'action':name,'range':list(action.frame_range),
                     'lowest_vertex_range_m':[minimum,maximum]})
    output.write_text(json.dumps({'clips':rows,'checks':'UV present, normalized 1-4 weights, finite bounded skin, no live constraints',
        'not_acceptance':'Natural gait, intersections, exact contact, loops, production topology, redistribution rights or FPS'},indent=2))
    print(json.dumps(rows))


if __name__=='__main__':
    main()

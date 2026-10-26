"""Read the current owner-selected natives for runtime export planning; never save them."""
import argparse, hashlib, json, sys, time
from pathlib import Path
import bpy
from mathutils import Vector

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--limit',type=int,default=0)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
rows=json.loads((repo/'artifacts/fauna-anatomy-revision/review-index.json').read_text())['ready']
out=repo/'artifacts/fauna-runtime-20261026';out.mkdir(parents=True,exist_ok=True)
result=[]
for row in rows[:a.limit or None]:
    path=Path(row['path']);digest=hashlib.sha256(path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(path))
    s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0]
    rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
    s.frame_set(0);bpy.context.view_layer.update()
    meshes=[];materials={}
    for name in e['objects']:
        ob=bpy.data.objects.get(name)
        if not ob or ob.type!='MESH':continue
        p=[ob.matrix_world@Vector(corner) for corner in ob.bound_box]
        meshes.append({'name':name,'vertices':len(ob.data.vertices),'polygons':len(ob.data.polygons),'visible':not ob.hide_render,'matrix':list(map(list,ob.matrix_world)),
                       'bounds':[[min(q[i] for q in p) for i in range(3)],[max(q[i] for q in p) for i in range(3)]],
                       'uvs':[u.name for u in ob.data.uv_layers],'materials':[m.name if m else None for m in ob.data.materials],
                       'armature':any(m.type=='ARMATURE' for m in ob.modifiers),'shape_keys':list(ob.data.shape_keys.key_blocks.keys()) if ob.data.shape_keys else []})
        for m in ob.data.materials:
            if not m or m.name in materials:continue
            nodes=[];links=[]
            if m.node_tree:
                for n in m.node_tree.nodes:
                    nodes.append({'name':n.name,'type':n.type,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None,
                                  'size':list(n.image.size) if n.type=='TEX_IMAGE' and n.image else None,
                                  'uv':n.uv_map if n.type in {'UVMAP','NORMAL_MAP'} else None,
                                  'inputs':{i.name:list(i.default_value) if hasattr(i.default_value,'__len__') else i.default_value for i in n.inputs if hasattr(i,'default_value') and isinstance(i.default_value,(int,float,str,Vector))}})
                links=[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name) for l in m.node_tree.links]
            materials[m.name]={'nodes':nodes,'links':links,'diffuse':list(m.diffuse_color)}
    actions=[]
    for label,name in e['actions'].items():
        action=bpy.data.actions[name]
        actions.append({'label':label,'name':name,'frames':list(action.frame_range)})
    record={'slug':row['slug'],'name':row['name'],'source':str(path.relative_to(repo)),'sha256':digest,'entry':e,
            'fps':s.render.fps/s.render.fps_base,'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'rest':list(map(list,b.matrix_local))} for b in rig.data.bones],
            'rig_matrix':list(map(list,rig.matrix_world)),'meshes':meshes,'materials':materials,'actions':actions}
    result.append(record)
    (out/'native-inventory.json').write_text(json.dumps({'animals':result},indent=2)+'\n')
    assert hashlib.sha256(path.read_bytes()).hexdigest()==digest
    print('INVENTORY',len(result),row['slug'],len(meshes),'meshes',len(rig.data.bones),'bones',','.join(x['label'] for x in actions),flush=True)

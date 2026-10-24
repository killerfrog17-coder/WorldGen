"""One-window comparison of four fresh meshes with independent generated rigs."""
import json,math,random,time
from pathlib import Path
import bpy
from mathutils import Vector,Quaternion

s=bpy.context.scene;models=json.loads(s['deer_four_rigs'])
animal=s.get('fauna_animal','Deer')
finished=bool(models[0].get('same_approved_model'))
warning_path=Path(bpy.data.filepath).parent/'visual-warnings.json'
screening=json.loads(warning_path.read_text()) if warning_path.exists() else {}
state={'model':0,'clip':'Showcase','mode':0,'view':0,'skeleton':False,'coat':'Tawny woodland' if finished else 'Unpainted preview','started':time.monotonic(),'seed':734100,'coat_index':0,'active_rack':-1,'sex':'Female','pending_only':True}
queue_cache=[];queue_items_cache=[]
def queue_entries():
    global queue_cache,queue_items_cache
    p=Path(bpy.data.filepath).parent.parent/'review-index.json'
    if p.exists():
        queue_cache=json.loads(p.read_text()).get('ready',[])
        choices_path=Path(__file__).resolve().parent.parent/'docs/fauna/OWNER_SELECTIONS.json'
        if state['pending_only'] and choices_path.exists():
            choices=json.loads(choices_path.read_text())
            decided=set(choices.get('animals',{}))
            decided.update(slug for slug,entry in choices.get('procedural_revisions',{}).items()
                           if entry.get('proceed_without_re_review'))
            # The owner accepts any bee candidate after the requested simple
            # silhouette revision; no further insect approval round was wanted.
            if choices.get('pending_revisions',{}).get('bees-guild',{}).get('status')=='completed':decided.add('bees-guild')
            queue_cache=[entry for entry in queue_cache if entry['slug'] not in decided]
        queue_items_cache=[(x['slug'],x['name'],'Raw motion audition -4 candidates') for x in queue_cache]
    return queue_cache
def animal_items(self,context):
    queue_entries();return queue_items_cache or [('none','No review queue','')]
if finished:
    for coat in models[0]['approved_art']['coats']:
        material=bpy.data.materials[coat['material']];nodes=material.node_tree.nodes;links=material.node_tree.links
        if 'Species tint' not in nodes:
            bsdf=nodes.get('Principled BSDF');source=bsdf.inputs['Base Color'].links[0].from_socket
            tint=nodes.new('ShaderNodeHueSaturation');tint.name='Species tint'
            links.new(source,tint.inputs['Color']);links.new(tint.outputs['Color'],bsdf.inputs['Base Color'])
def current():return models[state['model']]
def rig():return bpy.data.objects[current()['rig']]
def regions():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':yield window,area,area.spaces.active
def playback():
    for window,area,space in regions():
        with bpy.context.temp_override(window=window,area=area):
            if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
        return
def visibility():
    for index,entry in enumerate(models):
        body=bpy.data.objects[entry['body']];r=bpy.data.objects[entry['rig']]
        for name in entry.get('objects',[entry['body']]):
            obj=bpy.data.objects[name];hidden=index!=state['model']
            if entry.get('approved_art'):
                racks=[x['object'] for x in entry['approved_art']['antlers']]
                if name in racks:hidden=hidden or racks.index(name)!=state['active_rack']
            obj.hide_set(hidden);obj.hide_render=hidden
        r.hide_viewport=False;r.hide_set(index!=state['model'] or not state['skeleton']);r.show_in_front=True
        r.data.display_type='OCTAHEDRAL'
    for window,area,space in regions():
        space.overlay.show_overlays=state['skeleton'];space.overlay.show_bones=True
        space.overlay.show_floor=False;space.overlay.show_axis_x=False;space.overlay.show_axis_y=False
def choose(label,play=True,phase=0):
    r=rig();r.animation_data_clear();state['clip']=label
    for bone in r.pose.bones:bone.matrix_basis.identity()
    if label=='Bind':
        r.data.pose_position='REST';s.frame_start=0;s.frame_end=1;s.frame_set(0)
        if not bpy.app.background:
            for window,area,space in regions():
                with bpy.context.temp_override(window=window,area=area):
                    if window.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
                break
    else:
        r.data.pose_position='POSE';entry=current();name=entry['demos'][label] if label in entry['demos'] else entry['actions'][label]
        action=bpy.data.actions[name];r.animation_data_create().action=action;r.animation_data.action_slot=action.slots[0]
        s.frame_start=int(action.frame_range[0]);s.frame_end=int(action.frame_range[1]);s.frame_set(s.frame_start+round(phase*(s.frame_end-s.frame_start)))
        if play and not bpy.app.background:playback()
    visibility()
def set_view():
    target=rig().matrix_world@rig().pose.bones['Head'].head if state['view']==1 else Vector((0,0,.8))
    direction=Vector((0,1,.05)) if state['view'] in (1,3) else Vector((0,-1,.05)) if state['view']==4 else Vector((1,0,.08))
    for window,area,space in regions():
        view=space.region_3d;view.view_perspective='PERSP';view.view_location=target
        view.view_distance=.9 if state['view']==1 else 3.1;view.view_rotation=direction.to_track_quat('Z','Y')
class D4_OT_model(bpy.types.Operator):
    bl_idname='deer_four.model';bl_label='1 - Next rig / model'
    number:bpy.props.IntProperty(default=-1)
    def execute(self,context):
        phase=(s.frame_current-s.frame_start)/max(1,s.frame_end-s.frame_start)
        playing=context.screen.is_animation_playing if context.screen else False
        state['model']=(state['model']+1)%len(models) if self.number<0 else self.number
        choose(state['clip'],play=playing,phase=phase);set_view();return {'FINISHED'}
class D4_OT_animal(bpy.types.Operator):
    bl_idname='deer_four.animal';bl_label='Change animal'
    animal:bpy.props.EnumProperty(items=animal_items)
    step:bpy.props.IntProperty(default=0)
    def execute(self,context):
        global s,models,animal,finished,screening
        entries=queue_entries()
        if not entries:return {'CANCELLED'}
        if self.step:
            index=next((i for i,x in enumerate(entries) if Path(x['path'])==Path(bpy.data.filepath)),-1 if self.step>0 else 0)
            selected=entries[(index+self.step)%len(entries)]
        else:selected=next((x for x in entries if x['slug']==self.animal),entries[0])
        if not Path(selected['path']).exists():return {'CANCELLED'}
        bpy.ops.wm.open_mainfile(filepath=selected['path'])
        s=bpy.context.scene;models=json.loads(s['deer_four_rigs']);animal=s.get('fauna_animal','Deer');finished=bool(models[0].get('same_approved_model'))
        warning_path=Path(selected['path']).parent/'visual-warnings.json'
        screening=json.loads(warning_path.read_text()) if warning_path.exists() else {}
        state.update(model=0,clip='Showcase',mode=0,view=0,skeleton=False,coat='Unpainted preview',started=time.monotonic(),active_rack=-1)
        choose('Showcase',play=False);set_view()
        if not bpy.app.background:
            if not bpy.app.timers.is_registered(tick):bpy.app.timers.register(tick,first_interval=.1)
            bpy.app.timers.register(lambda:(playback(),None)[1],first_interval=.2)
        return {'FINISHED'}
class D4_OT_filter(bpy.types.Operator):
    bl_idname='deer_four.filter';bl_label='Toggle pending animals filter'
    def execute(self,context):
        state['pending_only']=not state['pending_only'];queue_entries()
        for window,area,space in regions():area.tag_redraw()
        return {'FINISHED'}
class D4_OT_species(bpy.types.Operator):
    bl_idname='deer_four.species';bl_label='2 - Randomize species colour'
    def execute(self,context):
        if finished:
            state['seed']=(1664525*state['seed']+1013904223)&0xffffffff
            coats=models[0]['approved_art']['coats'];state['coat_index']=(state['coat_index']+1+state['seed']%(len(coats)-1))%len(coats)
            profile=coats[state['coat_index']];material=bpy.data.materials[profile['material']]
            tint=material.node_tree.nodes['Species tint'];tint.inputs['Hue'].default_value=.48+((state['seed']>>8)%1000)/25000
            tint.inputs['Saturation'].default_value=.90+((state['seed']>>16)%100)/500;tint.inputs['Value'].default_value=.92+((state['seed']>>24)%100)/625
            ear=max(.72,min(1.08,profile['ear_scale']+(((state['seed']>>12)%100)/100-.5)*.06))
            tail=max(.70,min(1.20,profile['tail_scale']+(((state['seed']>>20)%100)/100-.5)*.12))
            male=bool((state['seed']>>6)&1);state['active_rack']=profile['antler_style'] if male else -1
            state['sex']='Male' if male else 'Female';state['coat']=profile['label']
            for entry in models:
                body=bpy.data.objects[entry['body']];body.data.materials[0]=material
                keys=body.data.shape_keys.key_blocks
                keys['Compact ears'].value=max(0,(1-ear)/.28);keys['Large ears'].value=max(0,(ear-1)/.08)
                keys['Short tail'].value=max(0,(1-tail)/.30);keys['Long tail'].value=max(0,(tail-1)/.20)
            visibility();context.scene['review_phenotype_seed']=str(state['seed']);return {'FINISHED'}
        label,saturation,value=random.choice([('Woodland',1,1),('Pale',.45,1.4),('Dark',.8,.65),('Grey',.15,.95),('Warm',1.2,1.1)])
        for entry in models:
            hsv=bpy.data.materials[entry['material']].node_tree.nodes['Review coat variation']
            hsv.inputs['Saturation'].default_value=saturation;hsv.inputs['Value'].default_value=value
        state['coat']=label;return {'FINISHED'}
class D4_OT_view(bpy.types.Operator):
    bl_idname='deer_four.view';bl_label='3 - Side / Face / Orbit'
    front:bpy.props.BoolProperty(default=False)
    rear:bpy.props.BoolProperty(default=False)
    def execute(self,context):
        state['view']=3 if self.front else 4 if self.rear else (state['view']+1)%3
        state['started']=time.monotonic();set_view();return {'FINISHED'}
class D4_OT_mode(bpy.types.Operator):
    bl_idname='deer_four.mode';bl_label='4 - Showcase / Locomotion'
    def execute(self,context):state['mode']=1-state['mode'];choose('Locomotion' if state['mode'] else 'Showcase');return {'FINISHED'}
class D4_OT_skeleton(bpy.types.Operator):
    bl_idname='deer_four.skeleton';bl_label='Show / Hide Skeleton'
    def execute(self,context):state['skeleton']=not state['skeleton'];visibility();return {'FINISHED'}
class D4_OT_play(bpy.types.Operator):
    bl_idname='deer_four.play';bl_label='Pause / Play'
    def execute(self,context):
        if state['clip']!='Bind':bpy.ops.screen.animation_play()
        return {'FINISHED'}
class D4_OT_clip(bpy.types.Operator):
    bl_idname='deer_four.clip';bl_label='Preview animation'
    clip:bpy.props.StringProperty()
    def execute(self,context):choose(self.clip);return {'FINISHED'}
class D4_OT_free(bpy.types.Operator):
    bl_idname='deer_four.free';bl_label='Stop automatic orbit'
    def invoke(self,context,event):
        state['view']=-1;self.region=context.space_data.region_3d;self.region.lock_rotation=False
        self.previous=(event.mouse_x,event.mouse_y);context.window_manager.modal_handler_add(self);return {'RUNNING_MODAL'}
    def modal(self,context,event):
        if event.type in {'ESC','RIGHTMOUSE'} or (event.type=='MIDDLEMOUSE' and event.value=='RELEASE'):return {'FINISHED'}
        if event.type=='MOUSEMOVE':
            dx=event.mouse_x-self.previous[0];dy=event.mouse_y-self.previous[1];self.previous=(event.mouse_x,event.mouse_y)
            self.region.view_rotation=(Quaternion((0,0,1),-dx*.006)@self.region.view_rotation@Quaternion((1,0,0),dy*.006)).normalized();context.area.tag_redraw()
        return {'RUNNING_MODAL'}
def controls(layout):
    row=layout.row(align=True);row.operator('deer_four.model',text=f"1: {'Rig' if finished else 'Model'} {state['model']+1}/{len(models)}")
    row.operator('deer_four.species',text='2: Species');row.operator('deer_four.view',text='3: View');row.operator('deer_four.mode',text='4: Loop')
    row.operator('deer_four.skeleton',text='Hide Skeleton' if state['skeleton'] else 'Show Skeleton')
    row.operator('deer_four.play',text='Pause / Play')
def header(self,context):controls(self.layout)
class D4_PT_panel(bpy.types.Panel):
    bl_idname='D4_PT_panel';bl_label='Animal raw motion review'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Animal Review'
    def draw(self,context):
        col=self.layout.column();entry=current();col.label(text=animal)
        entries=queue_entries()
        if (Path(bpy.data.filepath).parent.parent/'review-index.json').exists():
            col.operator('deer_four.filter',text='Pending animals only' if state['pending_only'] else 'All ready animals',depress=state['pending_only'])
            col.label(text=str(len(entries))+' animals in this list')
        if entries:
            col.operator_menu_enum('deer_four.animal','animal',text='Choose animal')
            row=col.row(align=True);row.operator('deer_four.animal',text='Previous animal').step=-1;row.operator('deer_four.animal',text='Next animal').step=1
        col.label(text=f"{'RIG' if finished else 'MODEL'} {state['model']+1} / {len(models)} - {entry['joints']} generated joints")
        warnings=[w for w in screening.get('warnings',[]) if w.get('candidate')==state['model']+1]
        if warnings:
            box=col.box();box.alert=True
            ruled_out=state['model']+1 in screening.get('candidate_recommendation_exclusions',[])
            box.label(text='Screened out: visible defects' if ruled_out else 'Known visual issue',icon='ERROR')
            box.label(text='See REVIEW_INDEX.csv for details')
        if finished:col.label(text='Approved finished deer - prototype' if len(models)==1 else 'Same approved finished deer in all four')
        row=col.row(align=True)
        for index in range(len(models)):row.operator('deer_four.model',text=str(index+1),depress=index==state['model']).number=index
        for op in ('deer_four.model','deer_four.species','deer_four.view','deer_four.mode'):col.operator(op)
        row=col.row(align=True);row.operator('deer_four.view',text='Front body').front=True;row.operator('deer_four.view',text='Rear body').rear=True
        col.operator('deer_four.skeleton',text='Hide Skeleton' if state['skeleton'] else 'Show Skeleton');col.operator('deer_four.play')
        col.label(text='Playing: '+state['clip'].replace('_',' '));col.label(text='Coat: '+state['coat']+('' if finished else ' (colour only)'))
        if finished:col.label(text=state['sex']+' - '+('Antlers' if state['active_rack']>=0 else 'No antlers'))
        col.separator();col.label(text='Individual animations')
        preferred=['Idle','Walk','Trot','Run','Eating','Sleep','Rest','Jump','lay_to_idle','Death']
        labels=['Bind',*[n for n in preferred if n in entry['actions']],*[n for n in entry['actions'] if n not in preferred]]
        for label in labels:
            col.operator('deer_four.clip',text='Get Up' if label=='lay_to_idle' else label).clip=label
        col.label(text='Own simple insect rig / wing motion' if entry.get('source_family')=='own simple microfauna' else 'Fresh skeleton + network skin per candidate')
        col.label(text='Raw automatic transfer; no manual repairs')
def tick():
    for window,area,space in regions():
        if state['view']==2:
            angle=(time.monotonic()-state['started'])*.22
            space.region_3d.view_rotation=Vector((math.cos(angle),math.sin(angle),.08)).to_track_quat('Z','Y')
        elif state['view']==1:space.region_3d.view_location=rig().matrix_world@rig().pose.bones['Head'].head
        area.tag_redraw()
    return .05
for cls in (D4_OT_model,D4_OT_animal,D4_OT_filter,D4_OT_species,D4_OT_view,D4_OT_mode,D4_OT_skeleton,D4_OT_play,D4_OT_clip,D4_OT_free,D4_PT_panel):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
config=bpy.context.window_manager.keyconfigs.addon
if config:
    km=config.keymaps.new(name='3D View',space_type='VIEW_3D')
    for key,op in [('ONE','deer_four.model'),('TWO','deer_four.species'),('THREE','deer_four.view'),('FOUR','deer_four.mode'),('MIDDLEMOUSE','deer_four.free')]:km.keymap_items.new(op,key,'PRESS')
choose('Showcase',play=False);set_view()
if not bpy.app.background:
    bpy.ops.deer_four.skeleton();assert not rig().hide_get();bpy.ops.deer_four.skeleton();assert rig().hide_get()
    bpy.app.timers.register(tick,first_interval=.1);bpy.app.timers.register(lambda:(playback(),None)[1],first_interval=1.5)
print('Four generated deer rigs viewer ready.',flush=True)

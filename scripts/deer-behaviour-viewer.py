"""Session-only Blender controller/viewer for the deer prototype.

Native actions remain in the file/export. This wrapper demonstrates the extra
runtime systems: blending, phase/speed, root motion, gaze and terrain foot IK.
It never starts the game or writes user preferences.
"""
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Quaternion,Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Library,F,R,Z,matrix,blend_pose,smooth

_controllers={}
_busy=False
CLIP_NAMES=['Idle','Walk','Trot','Run','Graze','Drink','Alert','Lie Down','Rest','Get Up',
            'Jump','Turn Left','Turn Right','Hit Left','Hit Right','Death']


def height(x,y):
    u=x*F.x+y*F.y
    v=x*R.x+y*R.y
    return .10*u+.045*math.sin(1.15*u)*math.cos(.8*v)


def make_environment(scene):
    if not bpy.data.objects.get('Deer test terrain'):
        vertices=[]
        faces=[]
        count=100
        for y in range(count+1):
            for x in range(count+1):
                xx=(x/count-.5)*50
                yy=(y/count-.5)*50
                vertices.append((xx,yy,height(xx,yy)))
        for y in range(count):
            for x in range(count):
                a=y*(count+1)+x
                faces.append((a,a+1,a+count+2,a+count+1))
        data=bpy.data.meshes.new('Terrain contact grid')
        data.from_pydata(vertices,[],faces)
        data.update()
        terrain=bpy.data.objects.new('Deer test terrain',data)
        scene.collection.objects.link(terrain)
        material=bpy.data.materials.new('Contact test soil grid')
        material.use_nodes=True
        nodes=material.node_tree.nodes
        bsdf=nodes.get('Principled BSDF')
        bsdf.inputs['Roughness'].default_value=.95
        geometry=nodes.new('ShaderNodeNewGeometry')
        checker=nodes.new('ShaderNodeTexChecker')
        checker.inputs['Color1'].default_value=(.16,.12,.07,1)
        checker.inputs['Color2'].default_value=(.22,.17,.10,1)
        checker.inputs['Scale'].default_value=1.5
        material.node_tree.links.new(geometry.outputs['Position'],checker.inputs['Vector'])
        material.node_tree.links.new(checker.outputs['Color'],bsdf.inputs['Base Color'])
        data.materials.append(material)
        terrain.hide_set(True)
        terrain.hide_render=True
    if not bpy.data.objects.get('Drinking water marker'):
        bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.38,depth=.012)
        water=bpy.context.object
        water.name='Drinking water marker'
        material=bpy.data.materials.new('Prototype water')
        material.diffuse_color=(.04,.14,.18,1)
        material.use_nodes=True
        bsdf=material.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value=(.04,.14,.18,1)
        bsdf.inputs['Roughness'].default_value=.2
        water.data.materials.append(material)
        water.hide_set(True)
        water.hide_render=True


class Controller:
    def __init__(self,scene):
        self.scene=scene
        root=Path(scene['deer_library_root'])
        if not (root/'deer-motion-library.json').exists():
            root=Path(bpy.data.filepath).parent
        self.lib=Library(json.loads((root/'deer-motion-library.json').read_text()),
                         np.load(root/'deer-motion-library.npz'))
        self.sk=self.lib.skeleton
        self.rig=bpy.data.objects['AI Deer Rig']
        self.actor=bpy.data.objects['Deer Actor']
        self.rig.animation_data_clear()
        self.rig.data.pose_position='POSE'
        self.bones=[self.rig.pose.bones[n] for n in self.sk.names]
        for b in self.bones:
            b.rotation_mode='QUATERNION'
        self.camera_offset=Vector((2.7,-5,2.1))
        self.light_offsets={o.name:o.location.copy() for o in scene.objects if o.type=='LIGHT'}
        self.terrain_xy=(0,0)
        make_environment(scene)
        self.reset()

    def reset(self):
        self.position=Vector((0,0,0))
        self.heading=0
        self.speed=0
        self.phase=0
        self.time=0
        self.elapsed=0
        self.active='Idle'
        self.transition=1
        self.from_pose=None
        self.base_pose,self.contacts,_,_=self.lib.sample('Idle',0)
        self.previous_contacts=self.contacts.copy()
        self.locks=[None]*4
        self.last_frame=None
        self.previous_yaw=0
        self.previous_shift=Vector()
        self.root_start_heading=0
        self.gaze_yaw=0
        self.gaze_pitch=0
        self.segment=-1
        self.actor.matrix_world=Matrix.Identity(4)

    def request(self,name):
        if name not in self.lib.clips:
            return
        self.from_pose=(self.base_pose[0].copy(),self.base_pose[1].copy())
        self.from_contacts=self.contacts.copy()
        self.transition=0
        self.active=name
        info=self.lib.clips[name]
        self.elapsed=info.get('phase_offset',0) if info['speed']>0 else 0
        self.previous_yaw=0
        self.previous_shift=Vector()
        self.root_start_heading=self.heading
        self.water_site=None

    def routine(self,t):
        clips=self.lib.clips
        plan=[('Idle',4),('Walk',4),('Graze',7),('Idle',2),('Walk',3),('Drink',6),
              ('Alert',2.5),('Run',4),('Trot',3),('Walk',2),('Turn Left',clips['Turn Left']['duration']),
              ('Idle',2),('Lie Down',clips['Lie Down']['duration']),('Rest',5),
              ('Get Up',clips['Get Up']['duration']),('Idle',2),('Walk',2),
              ('Jump',clips['Jump']['duration']),('Walk',2),('Idle',3)]
        duration=sum(d for _,d in plan)
        t=t%duration
        for i,(name,d) in enumerate(plan):
            if t<d:
                return i,name,t
            t-=d
        return 0,'Idle',0

    def locomotion(self,dt):
        v=self.speed
        options=[('Idle',0),('Walk',self.lib.clips['Walk']['speed']),
                 ('Trot',self.lib.clips['Trot']['speed']),('Run',self.lib.clips['Run']['speed'])]
        a,b=options[-2],options[-1]
        for aa,bb in zip(options,options[1:]):
            if v<=bb[1]:
                a,b=aa,bb
                break
        u=max(0,min(1,(v-a[1])/max(.01,b[1]-a[1])))
        stride=sum(self.lib.clips[n]['speed']*self.lib.clips[n]['duration']*w
                   for (n,_),w in ((a,1-u),(b,u)))
        if stride>.01:
            self.phase+=v*dt/stride
        def sample(n):
            info=self.lib.clips[n]
            t=self.time if n=='Idle' else self.phase*info['duration']+info.get('phase_offset',0)
            return self.lib.sample(n,t)
        pa,ca,_,_=sample(a[0])
        pb,cb,_,_=sample(b[0])
        self.active=f'{a[0]} / {b[0]}' if .05<u<.95 else a[0] if u<.5 else b[0]
        return blend_pose(pa,pb,u),ca*(1-u)+cb*u

    def update(self,dt,absolute_time):
        settings=self.scene.deer_preview
        if settings.mode=='NATIVE':
            return
        dt=max(0,min(.25,dt))*settings.rate
        self.time+=dt
        mode=settings.mode
        if mode=='ROUTINE':
            segment,name,local=self.routine(absolute_time*settings.rate)
            if segment!=self.segment:
                if segment==0 and self.segment>0:
                    self.reset()
                self.request(name)
                self.segment=segment
        info=self.lib.clips.get(self.active,self.lib.clips['Idle'])
        target_speed=settings.speed if mode in ('LOCOMOTION','SLOPE') else info['speed']
        if self.active in ('Death','Lie Down','Rest','Get Up','Graze','Drink','Alert','Idle'):
            if mode not in ('LOCOMOTION','SLOPE'):
                target_speed=0
        self.speed+=(target_speed-self.speed)*(1-math.exp(-dt/.28)) if dt>0 else 0
        self.elapsed+=dt
        if mode=='MANUAL' and not info['loop'] and self.elapsed>info['duration']:
            next_clip={'Lie Down':'Rest','Get Up':'Idle','Jump':'Walk','Turn Left':'Idle',
                       'Turn Right':'Idle','Hit Left':'Idle','Hit Right':'Idle'}.get(self.active)
            if next_clip:
                self.request(next_clip)
                info=self.lib.clips[self.active]
        if mode in ('LOCOMOTION','SLOPE'):
            base,contacts=self.locomotion(dt)
            yaw=0
            shift=Vector()
        else:
            base,contacts,yaw,shift=self.lib.sample(self.active,self.elapsed)
            shift=Vector(shift)
        self.heading+=yaw-self.previous_yaw
        self.position+=Quaternion(Z,self.root_start_heading)@(shift-self.previous_shift)
        self.previous_yaw=yaw
        self.previous_shift=shift
        if not self.active.startswith('Turn '):
            steer=settings.steering if mode in ('LOCOMOTION','SLOPE') else (8 if self.speed>.1 else 0)
            self.heading+=math.radians(steer)*dt
        self.position+=Quaternion(Z,self.heading)@F*self.speed*dt
        if self.from_pose is not None and self.transition<1:
            duration=.7 if self.active in ('Graze','Drink') else .38
            self.transition=min(1,self.transition+dt/duration)
            w=smooth(self.transition)
            base=blend_pose(self.from_pose,base,w)
            contacts=self.from_contacts*(1-w)+contacts*w
        self.base_pose=base
        self.contacts=contacts
        g=self.sk.fk(base)
        fn=height if mode=='SLOPE' else (lambda x,y:0)
        self.position.z=fn(self.position.x,self.position.y)
        slope=Quaternion()
        if mode=='SLOPE':
            h=.04
            dx=(height(self.position.x+h,self.position.y)-height(self.position.x-h,self.position.y))/(2*h)
            dy=(height(self.position.x,self.position.y+h)-height(self.position.x,self.position.y-h))/(2*h)
            slope=Z.rotation_difference(Vector((-dx,-dy,1)).normalized())
        actor=matrix(slope@Quaternion(Z,self.heading),self.position)
        self.actor.matrix_world=actor
        active_info=self.lib.clips.get(self.active)
        can_look=active_info is None or active_info['procedural_gaze']
        if can_look and settings.natural_look:
            target_yaw=math.radians(17)*math.sin(self.time*.42)+math.radians(7)*math.sin(self.time*.73+.8)
            target_pitch=math.radians(4)*math.sin(self.time*.35)
            if self.active=='Alert':
                target_yaw=math.radians(30)
        else:
            target_yaw=target_pitch=0
        target_yaw+=math.radians(settings.look_yaw) if can_look else 0
        target_pitch+=math.radians(settings.look_pitch) if can_look else 0
        blend=1-math.exp(-dt/.38) if dt>0 else 0
        self.gaze_yaw+=(target_yaw-self.gaze_yaw)*blend
        self.gaze_pitch+=(target_pitch-self.gaze_pitch)*blend
        self.sk.gaze(g,self.gaze_yaw,self.gaze_pitch,weight=1 if can_look else 0,
                     ears=1 if can_look and settings.natural_look else 0,time=self.time)
        for k,foot in enumerate(self.sk.feet):
            world=actor@self.sk.sole(g,foot)
            if contacts[k]>.55 and self.locks[k] is None:
                self.locks[k]=world.copy()
            if contacts[k]<.2:
                self.locks[k]=None
            if self.locks[k] is not None and (world-self.locks[k]).length>.6:
                self.locks[k]=world.copy()
        self.foot_diagnostics=[]
        if settings.foot_ik:
            self.foot_diagnostics=self.sk.ground_feet(g,contacts,height=fn,actor=actor,
                                                     locks=self.locks if settings.plant_feet else None)
        loc,rot=self.sk.basis(g)
        for i,b in enumerate(self.bones):
            b.location=loc[i]
            b.rotation_quaternion=rot[i]
            b.scale=(1,1,1)
        self.previous_contacts=contacts.copy()
        terrain=bpy.data.objects['Deer test terrain']
        terrain.hide_set(mode!='SLOPE')
        terrain.hide_render=mode!='SLOPE'
        floor=bpy.data.objects.get('Studio floor (not part of animal)')
        if floor:
            floor.hide_set(mode=='SLOPE')
            floor.hide_render=mode=='SLOPE'
        if mode=='SLOPE':
            cx=round(self.position.x/10)*10
            cy=round(self.position.y/10)*10
            if (cx,cy)!=self.terrain_xy:
                self.terrain_xy=(cx,cy)
                terrain.location=(cx,cy,0)
                for v in terrain.data.vertices:
                    v.co.z=height(v.co.x+cx,v.co.y+cy)
                terrain.data.update()
        water=bpy.data.objects['Drinking water marker']
        visible=self.active=='Drink' and self.transition>=1
        water.hide_set(not visible)
        water.hide_render=not visible
        if visible:
            nose=actor@(g[self.sk.index['Head']]@self.sk.nose)
            if self.water_site is None:
                self.water_site=(nose.x,nose.y,fn(nose.x,nose.y)+.014)
            water.location=self.water_site
        if settings.follow:
            target=self.position+Vector((0,0,.75))
            for window in bpy.context.window_manager.windows:
                for area in window.screen.areas:
                    if area.type=='VIEW_3D':
                        area.spaces.active.region_3d.view_location=target
            camera=self.scene.camera
            camera.location=self.position+self.camera_offset
            camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
            for n,offset in self.light_offsets.items():
                if bpy.data.objects.get(n):
                    bpy.data.objects[n].location=self.position+offset


def get_controller(scene):
    key=scene.as_pointer()
    if key not in _controllers:
        _controllers[key]=Controller(scene)
    return _controllers[key]


def frame_update(scene,*args):
    global _busy
    if _busy or not scene.get('deer_behaviour_viewer',False):
        return
    _busy=True
    try:
        c=get_controller(scene)
        frame=scene.frame_current+scene.frame_subframe
        dt=0 if c.last_frame is None else (frame-c.last_frame)/scene.render.fps
        if dt<0 or dt>.5:
            c.reset()
            dt=0
        c.last_frame=frame
        c.update(dt,frame/scene.render.fps)
    finally:
        _busy=False


def mode_changed(self,context):
    if not context.scene.get('deer_behaviour_viewer',False):
        return
    c=get_controller(context.scene)
    c.reset()
    if self.mode=='NATIVE':
        native_clip(context.scene,self.clip)
    else:
        c.rig.animation_data_clear()
        context.scene.frame_start=0
        context.scene.frame_end=24*90-1
        context.scene.frame_set(0)
        c.request(self.clip if self.mode=='MANUAL' else 'Idle')
    frame_update(context.scene)


def native_clip(scene,name):
    c=get_controller(scene)
    action=bpy.data.actions.get(name)
    if not action:
        return
    c.actor.matrix_world=Matrix.Identity(4)
    for name in ('Deer test terrain','Drinking water marker'):
        obj=bpy.data.objects.get(name)
        if obj:
            obj.hide_set(True)
            obj.hide_render=True
    floor=bpy.data.objects.get('Studio floor (not part of animal)')
    if floor:
        floor.hide_set(False)
        floor.hide_render=False
    c.rig.animation_data_create().action=action
    if action.slots:
        c.rig.animation_data.action_slot=action.slots[0]
    scene.frame_start=0
    scene.frame_end=int(action.frame_range[1])
    scene.frame_set(0)


class DEERPREVIEW_Properties(bpy.types.PropertyGroup):
    mode:bpy.props.EnumProperty(name='Preview',items=[
        ('ROUTINE','Daily routine','Feeding, drinking, alert/flee, rest and movement'),
        ('MANUAL','Actions + transitions','Choose a motion below'),
        ('LOCOMOTION','Speed & steering','Continuous gait blending and movement'),
        ('SLOPE','Uneven ground','Terrain alignment and planted feet'),
        ('NATIVE','Native clips','Play the portable baked actions without procedural layers')],
        default='ROUTINE',update=mode_changed)
    clip:bpy.props.StringProperty(default='Idle')
    speed:bpy.props.FloatProperty(name='Move speed (m/s)',default=1.05,min=0,max=10)
    steering:bpy.props.FloatProperty(name='Turn (degrees/s)',default=12,min=-45,max=45)
    rate:bpy.props.FloatProperty(name='Playback rate',default=1,min=.2,max=2)
    natural_look:bpy.props.BoolProperty(name='Natural head / ears',default=True)
    look_yaw:bpy.props.FloatProperty(name='Head right / left',default=0,min=-55,max=55)
    look_pitch:bpy.props.FloatProperty(name='Head up / down',default=0,min=-25,max=25)
    foot_ik:bpy.props.BoolProperty(name='Ground correction',default=True)
    plant_feet:bpy.props.BoolProperty(name='Plant stance feet',default=True)
    follow:bpy.props.BoolProperty(name='Follow animal',default=True)


class DEERPREVIEW_OT_clip(bpy.types.Operator):
    bl_idname='deer_preview.clip'
    bl_label='Preview deer motion'
    clip:bpy.props.StringProperty()
    def execute(self,context):
        scene=context.scene
        p=scene.deer_preview
        p.clip=self.clip
        if p.mode=='NATIVE':
            native_clip(scene,self.clip)
        else:
            if p.mode!='MANUAL':
                p.mode='MANUAL'
            c=get_controller(scene)
            c.request(self.clip)
            scene.frame_end=24*90-1
            frame_update(scene)
        if context.screen and not context.screen.is_animation_playing:
            bpy.ops.screen.animation_play()
        return {'FINISHED'}


class DEERPREVIEW_OT_play(bpy.types.Operator):
    bl_idname='deer_preview.play'
    bl_label='Pause / Play'
    def execute(self,context):
        bpy.ops.screen.animation_play()
        return {'FINISHED'}


class DEERPREVIEW_OT_reset(bpy.types.Operator):
    bl_idname='deer_preview.reset'
    bl_label='Reset preview'
    def execute(self,context):
        get_controller(context.scene).reset()
        context.scene.frame_set(0)
        return {'FINISHED'}


class DEERPREVIEW_OT_lod(bpy.types.Operator):
    bl_idname='deer_preview.lod'
    bl_label='Preview mesh LOD'
    level:bpy.props.IntProperty()
    def execute(self,context):
        for i,n in enumerate(get_controller(context.scene).lib.meta['lods']):
            obj=bpy.data.objects[n]
            obj.hide_set(i!=self.level)
            obj.hide_render=i!=self.level
        context.scene['review_lod']=self.level
        return {'FINISHED'}


class DEERPREVIEW_OT_view(bpy.types.Operator):
    bl_idname='deer_preview.view'
    bl_label='Inspect deer'
    side:bpy.props.StringProperty(default='side')
    def execute(self,context):
        c=get_controller(context.scene)
        q=Quaternion(Z,c.heading)
        axis={'side':R,'front':F,'three-quarter':(R+F*.6).normalized()}[self.side]
        direction=q@axis*5+Z*1.4
        region=context.space_data.region_3d
        region.view_rotation=direction.to_track_quat('Z','Y')
        region.view_distance=4.4
        region.view_location=c.position+Z*.75
        return {'FINISHED'}


class DEERPREVIEW_PT_panel(bpy.types.Panel):
    bl_label='Deer prototype'
    bl_idname='DEERPREVIEW_PT_panel'
    bl_space_type='VIEW_3D'
    bl_region_type='UI'
    bl_category='Deer'
    def draw(self,context):
        layout=self.layout
        p=context.scene.deer_preview
        c=get_controller(context.scene)
        layout.prop(p,'mode',text='')
        layout.label(text='Current: '+c.active)
        row=layout.row(align=True)
        row.operator('deer_preview.play',text='Pause / Play')
        row.operator('deer_preview.reset',text='Reset')
        layout.separator()
        for names in [('Idle','Alert','Graze','Drink'),('Walk','Trot','Run','Jump'),
                      ('Lie Down','Rest','Get Up'),('Turn Left','Turn Right'),
                      ('Hit Left','Hit Right','Death')]:
            row=layout.row(align=True)
            for name in names:
                row.operator('deer_preview.clip',text=name,depress=c.active==name).clip=name
        layout.separator()
        layout.prop(p,'rate')
        if p.mode in ('LOCOMOTION','SLOPE'):
            layout.prop(p,'speed')
            layout.prop(p,'steering')
        layout.prop(p,'natural_look')
        layout.prop(p,'look_yaw')
        layout.prop(p,'look_pitch')
        layout.prop(p,'foot_ik')
        layout.prop(p,'plant_feet')
        layout.prop(p,'follow')
        row=layout.row(align=True)
        for title,side in [('Side','side'),('Front','front'),('3/4','three-quarter')]:
            row.operator('deer_preview.view',text=title).side=side
        row=layout.row(align=True)
        for i,title in enumerate(('14k','6k','2.2k','700')):
            row.operator('deer_preview.lod',text=title,
                depress=context.scene.get('review_lod',0)==i).level=i
        layout.separator()
        layout.label(text='CC0 motion adapted to our AI deer')
        layout.label(text='Native clips = no procedural layers')


def header(self,context):
    if not context.scene.get('deer_behaviour_viewer',False):
        return
    row=self.layout.row(align=True)
    row.label(text='Deer:')
    row.prop(context.scene.deer_preview,'mode',text='')
    row.operator('deer_preview.play',text='Pause / Play')
    row.operator('deer_preview.reset',text='Reset')


classes=(DEERPREVIEW_Properties,DEERPREVIEW_OT_clip,DEERPREVIEW_OT_play,DEERPREVIEW_OT_reset,
         DEERPREVIEW_OT_lod,DEERPREVIEW_OT_view,DEERPREVIEW_PT_panel)
for cls in classes:
    bpy.utils.register_class(cls)
bpy.types.Scene.deer_preview=bpy.props.PointerProperty(type=DEERPREVIEW_Properties)
bpy.types.VIEW3D_HT_header.prepend(header)
bpy.app.handlers.frame_change_pre.append(frame_update)
scene=bpy.context.scene
scene.sync_mode='FRAME_DROP'
c=get_controller(scene)
for i,n in enumerate(c.lib.meta['lods']):
    obj=bpy.data.objects[n]
    obj.hide_set(i!=0)
    obj.hide_render=i!=0
scene['review_lod']=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.show_region_ui=True
            area.spaces.active.overlay.show_overlays=False
            for region in area.regions:
                if region.type=='UI':
                    try:
                        region.active_panel_category='Deer'
                    except (AttributeError,TypeError):
                        pass
frame_update(scene)


def autoplay():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                region=next((r for r in area.regions if r.type=='WINDOW'),None)
                if region:
                    with bpy.context.temp_override(window=window,area=area,region=region):
                        if not window.screen.is_animation_playing:
                            bpy.ops.screen.animation_play()
                    return None
    return 1


if not bpy.app.background:
    bpy.app.timers.register(autoplay,first_interval=2)
print('Deer behaviour viewer ready.',flush=True)

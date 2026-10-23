// Mesh2Motion's actual skeleton, skinning and authored clips, via afk-rig.
// Anatomical markers guide joint placement only. No old skin/motion transfer.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';

const root=path.resolve(process.argv[2]??'artifacts/deer-mesh2motion-20261023');
const tool=path.resolve(process.argv[3]??'artifacts/tools/mesh2motion-cli/node_modules/afk-rig');
const {RigSession}=await import(pathToFileURL(path.join(tool,'dist/index.js')).href);
const data=JSON.parse(fs.readFileSync(path.join(root,'input-landmarks.json'),'utf8'));
const marker=data.joints;
const head=n=>marker[n].head;
const tail=n=>marker[n].tail;
const lerp=(a,b,t)=>a.map((x,i)=>x+(b[i]-x)*t);
const midpoint=(a,b)=>lerp(a,b,.5);
const session=await RigSession.start({model:path.join(root,'Deer-Unrigged-Forward.glb'),
  skeleton:'horse',project:path.join(root,'Deer-Fitted.afk-rig.json')});
const write=(name,value)=>fs.writeFileSync(path.join(root,name),JSON.stringify(value,null,2));
write('autofit-status.json',session.status());
const p={root:[head('Pelvis')[0],0,head('Pelvis')[2]],hips:head('Pelvis'),
  spine_1:head('Spine1'),spine_2:head('Spine2'),spine_3:head('Chest'),
  spine_4:head('Neck1'),spine_5:head('Neck2'),spine_6:head('Neck3'),head:head('Head')};
const rib=midpoint(head('Spine2'),head('Chest'));
p.ribcage_middle=[rib[0],rib[1]-.12,rib[2]];
p.ribcage_leaf=[rib[0],rib[1]-.27,rib[2]];
const vertices=[];
for (const primitive of session.model.primitives) {
  const values=primitive.geometry.attributes.position.array;
  for(let i=0;i<values.length;i+=3)vertices.push(Array.from(values.slice(i,i+3)));
}
const mean=points=>points.reduce((s,v)=>s.map((x,i)=>x+v[i]/points.length),[0,0,0]);
for (const side of ['l','r']) {
  // Earlier inferred suffixes are not reliable anatomical left/right. Select
  // by actual geometry: glTF character-left is +X in Mesh2Motion's convention.
  const select=stem=>['L','R'].sort((a,b)=>head(stem+'_'+b)[0]-head(stem+'_'+a)[0])[side==='l'?0:1];
  const f=select('UpperFore'),b=select('Thigh'),e=select('Ear');
  p['front_scapula_'+side]=head('Shoulder_'+f);
  p['front_humerus_'+side]=head('UpperFore_'+f);
  p['front_leg_upper_'+side]=head('Elbow_'+f);
  p['front_leg_lower_'+side]=head('Wrist_'+f);
  p['front_leg_ankle_'+side]=head('ForeHoof_'+f);
  p['front_leg_foot_'+side]=lerp(head('ForeHoof_'+f),tail('ForeHoof_'+f),.65);
  p['front_leg_leaf_'+side]=tail('ForeHoof_'+f);
  p['back_leg_pelvis_'+side]=head('Thigh_'+b);
  p['back_leg_upper_'+side]=lerp(head('Thigh_'+b),head('Knee_'+b),.40);
  p['back_leg_lower_'+side]=head('Knee_'+b);
  p['back_leg_ankle_'+side]=head('Hock_'+b);
  p['back_leg_foot_'+side]=head('HindHoof_'+b);
  p['back_leg_toe_'+side]=lerp(head('HindHoof_'+b),tail('HindHoof_'+b),.65);
  p['back_leg_leaf_'+side]=tail('HindHoof_'+b);
  p['ear_1_'+side]=head('Ear_'+e);
  const cloud=vertices.filter(v=>v[1]>1.50&&(side==='l'?v[0]>p.head[0]:v[0]<p.head[0]));
  const top=Math.max(...cloud.map(v=>v[1]));
  const earTip=mean(cloud.filter(v=>v[1]>top-.045));
  p['ear_2_'+side]=midpoint(head('Ear_'+e),earTip);
  p['ear_leaf_'+side]=earTip;
}
// The previous inferred muzzle markers lie above the actual nose. Fit the
// Mesh2Motion muzzle to the visible geometry, not those rejected markers.
const maxZ=Math.max(...vertices.map(v=>v[2]));
const nose=mean(vertices.filter(v=>v[2]>maxZ-.055&&v[1]>1.05&&v[1]<1.4));
p.nose=[nose[0],nose[1]+.015,nose[2]-.055];
p.head_leaf=nose;
p.mouth=[nose[0],nose[1]-.035,nose[2]-.09];
p.mouth_tip=[nose[0],nose[1]-.04,nose[2]-.015];
p.ribcage_front=[head('Neck1')[0],head('Neck1')[1]-.12,head('Neck1')[2]-.08];
p.ribcage_front_leaf=[head('Neck1')[0],head('Neck1')[1]-.18,head('Neck1')[2]-.02];
const start=head('Tail1'),tip=tail('Tail2');
for(let i=1;i<=4;i++)p['tail_'+i]=lerp(start,tip,(i-1)/4);
p.tail_leaf=tip;
// Assert the real template names rather than silently dropping mismatches.
const known=new Set(session.joint_names());
const missing=[...known].filter(n=>!p[n]);
const unknown=Object.keys(p).filter(n=>!known.has(n));
if(missing.length||unknown.length)throw new Error(JSON.stringify({missing,unknown}));
session.set_joints(p);
write('fitted-status.json',session.status());write('fitted-inspection.json',session.inspect());
const skeleton=await session.render({views:['front','left'],size:1000,xray:true});
fs.writeFileSync(path.join(root,'fitted-skeleton.png'),skeleton.png);
write('fitted-skeleton-pixels.json',{panels:skeleton.panels,joint_pixels:skeleton.joint_pixels});
const exports=[];
for(const rest of ['template','limbs','aligned']) {
  const start=performance.now();
  console.log('Binding/exporting native clips, rest mode:',rest);
  const result=await session.export({output:path.join(root,`Deer-Mesh2Motion-${rest}.glb`),
    animations:'all',rest_rotations:rest,ground:false});
  exports.push({rest,seconds:(performance.now()-start)/1000,...result});
  write('exports.json',exports);
}
// The automatic distance solver lets the short tail and hip sockets capture
// broad areas of rump. Correct only that leakage, retaining its rig and motion.
const binding=session.bind({},'limbs');
const names=binding.bone_names;
const smooth=(a,b,x)=>{const t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t);};
const distance=(v,a,b)=>{
  const d=b.map((x,i)=>x-a[i]),r=v.map((x,i)=>x-a[i]);
  const t=Math.max(0,Math.min(1,d.reduce((s,x,i)=>s+x*r[i],0)/d.reduce((s,x)=>s+x*x,0)));
  return Math.hypot(...v.map((x,i)=>x-a[i]-d[i]*t));
};
const bodyNames=['hips','spine_1','spine_2','spine_3','spine_4'];
const torso=bodyNames.map((n,i)=>({index:names.indexOf(n),a:p[n],b:p[bodyNames[Math.min(i+1,bodyNames.length-1)]]}));
let changed=0,maxRemoved=0;
for(let pi=0;pi<session.model.primitives.length;pi++) {
  const primitive=session.model.primitives[pi],v=primitive.geometry.attributes.position.array;
  const {indices,weights}=binding.weights[pi];
  const changedMask=new Uint8Array(v.length/3);
  for(let i=0;i<v.length/3;i++) {
    const pos=Array.from(v.slice(i*3,i*3+3));
    const values=new Map();let removed=0;
    for(let k=0;k<4;k++) {
      const index=indices[i*4+k],name=names[index],w=weights[i*4+k];let mask=1;
      if(name.startsWith('tail_'))mask=1-smooth(.025,.070,distance(pos,start,tip));
      if(name.startsWith('back_leg_pelvis_')||name.startsWith('back_leg_upper_'))
        mask*=1-smooth(.84,1.04,pos[1]);
      values.set(index,(values.get(index)??0)+w*mask);removed+=w*(1-mask);
    }
    if(removed<1e-6)continue;
    changed++;changedMask[i]=1;maxRemoved=Math.max(maxRemoved,removed);
    const nearby=torso.map(b=>({...b,w:1/Math.max(.025,distance(pos,b.a,b.b))**4}))
      .sort((a,b)=>b.w-a.w).slice(0,3);
    const total=nearby.reduce((s,b)=>s+b.w,0);
    for(const b of nearby)values.set(b.index,(values.get(b.index)??0)+removed*b.w/total);
    pack(i,values);
  }
  // Blend corrections along actual surface edges, including welded UV seams;
  // never blur weights between spatially close but disconnected legs.
  const keys=new Map(),groups=[];
  for(let i=0;i<v.length/3;i++) {
    const key=Array.from(v.slice(i*3,i*3+3)).map(x=>Math.round(x*1e5)).join(',');
    if(!keys.has(key)){keys.set(key,groups.length);groups.push([]);}
    groups[keys.get(key)].push(i);
  }
  const vertexGroup=new Int32Array(v.length/3);
  groups.forEach((g,i)=>g.forEach(v=>vertexGroup[v]=i));
  const neighbors=groups.map(()=>new Set()),tri=primitive.geometry.index.array;
  for(let t=0;t<tri.length;t+=3)for(let k=0;k<3;k++) {
    const a=vertexGroup[tri[t+k]],b=vertexGroup[tri[t+(k+1)%3]];
    neighbors[a].add(b);neighbors[b].add(a);
  }
  for(let pass=0;pass<3;pass++) {
    const previous=groups.map(group=>{
      const values=new Map();
      for(const i of group)for(let k=0;k<4;k++)values.set(indices[i*4+k],
        (values.get(indices[i*4+k])??0)+weights[i*4+k]/group.length);
      return values;
    });
    groups.forEach((group,g)=>{
      if(!group.some(i=>changedMask[i])&&!Array.from(neighbors[g]).some(n=>groups[n].some(i=>changedMask[i])))return;
      const values=new Map(Array.from(previous[g],([b,w])=>[b,w*.65]));
      for(const n of neighbors[g])for(const [b,w] of previous[n])
        values.set(b,(values.get(b)??0)+w*.35/neighbors[g].size);
      for(const i of group)pack(i,values);
    });
  }
  function pack(i,values) {
    const top=Array.from(values).filter(([b,w])=>w>1e-8).sort((a,b)=>b[1]-a[1]).slice(0,4);
    const sum=top.reduce((s,b)=>s+b[1],0);
    for(let k=0;k<4;k++){indices[i*4+k]=top[k]?.[0]??0;weights[i*4+k]=(top[k]?.[1]??0)/sum;}
  }
}
// Process-local adapter only: the installed package and native clips stay exact.
session.bind=()=>binding;
const cleaned=await session.export({output:path.join(root,'Deer-Mesh2Motion-fitted.glb'),
  animations:'all',rest_rotations:'limbs',ground:false});
exports.push({rest:'fitted',...cleaned});write('exports.json',exports);
write('weight-cleanup.json',{changed_vertices:changed,max_removed_foreign_weight:maxRemoved,
  method:'Soft anatomical tail/upper-haunch masks; fill from torso; three surface-edge passes',
  previous_skin_used:false,animation_curves_modified:false});
const packageInfo=JSON.parse(fs.readFileSync(path.join(tool,'package.json'),'utf8'));
write('pipeline.json',{tool:'Mesh2Motion engine via afk-rig',version:packageInfo.version,
  source:data.source,source_sha256:data.source_sha256,input_sha256:data.glb_sha256,
  old_skin_used:false,old_motion_used:false,mesh_generated:false,texture_generated:false,
  gpu_inference:false,paid_service:false,owner_game_closed:false,
  raw_clips:'All bundled authored horse clips, no additional procedural animation',exports});
console.log('Native Mesh2Motion trial exported.');

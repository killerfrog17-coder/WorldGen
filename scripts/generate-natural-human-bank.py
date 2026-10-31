"""Bounded overnight auditions: relaxed real actions, raw outputs kept separate.

Two workers may partition the queue. No model downloads or account required.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import time

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-natural-20261031'
CASES=[
 ('idle',120,'A person stands comfortably upright, feet hip width apart, with relaxed arms beside the body. Quiet breathing and very small natural weight shifts.'),
 ('tool_ready',120,'A person stands comfortably upright holding a small knife loosely beside their right hip, elbow slightly bent. Calm ordinary posture, steady legs.'),
 ('tool_walk',150,'A person walks casually forward carrying a small tool low beside their right hip. The right elbow stays softly bent with a small restrained arm swing.'),
 ('tool_stab',90,'An upright person makes one short quick right handed knife thrust at chest height, then retracts their hand. Feet stay planted, knees remain almost straight. Small controlled torso rotation.'),
 ('spear_thrust_one',90,'An upright person makes one controlled straight forward thrust with a spear in their right hand at chest height, then retracts. Feet remain planted and knees stay softly bent at the same height.'),
 ('spear_thrust_two',90,'An upright person makes one compact forward spear thrust with both hands at chest height, extends the elbows, then retracts. Feet stay planted, pelvis stays level, a small forward weight shift.'),
 ('bow_shoot',180,'A standing archer turns slightly side on, smoothly raises a bow with the left arm, draws the string to the right cheek, holds still, releases, and calmly lowers the bow. Upright spine, steady pelvis, planted feet.'),
 ('bow_ready',120,'A person stands upright holding an undrawn bow low beside their left thigh with the left arm relaxed and slightly bent. The right hand rests loosely near their waist. Calm still posture.'),
 ('spear_ready_one',120,'A person stands upright casually carrying a long spear in their right hand just outside the right hip, with a relaxed bent elbow. Feet hip width apart, calm steady posture.'),
 ('spear_ready_two',120,'An upright person carries a spear comfortably across the front of their lower ribs in both hands, elbows softly bent down. The spear is clear of the belly. Relaxed steady legs and small natural breathing.'),
 ('tool_cut',90,'A standing person makes one short controlled diagonal right handed knife cut across the space in front of their chest, then returns to a relaxed ready position. Upright pelvis, feet planted, restrained shoulder rotation.'),
 ('tool_blunt',90,'A standing person makes one compact right handed hammer strike forward and downward from shoulder height, then recovers. The knees stay softly bent at a constant height, feet remain planted.'),
 ('tool_chop',120,'A standing person makes one deliberate right handed hatchet chop downward into wood at waist height. A moderate shoulder swing and slight forward lean, planted feet, no deep knee bend, then a calm recovery.'),
 ('tool_guard',120,'A person stands steadily with feet hip width apart, holding a short club diagonally across their chest in their right hand. Elbows softly bent, a compact protective stance with quiet breathing.'),
 ('punch',90,'A standing person makes one short straight right handed punch at face height and retracts smoothly to chest level. The feet stay planted, small shoulder and torso rotation, pelvis remains level.'),
 ('fists_ready',120,'A person stands upright with a relaxed practical guard, elbows close to the ribs and loosely closed hands at upper chest height. Feet planted with gentle quiet breathing.'),
 ('fists_guard',120,'A person stands upright and steadily protects their face with both forearms, elbows close to the ribs, without bouncing. Feet hip width apart and knees only slightly bent.'),
 ('swim_carry',180,'A person swims slowly with a horizontal body, using the left arm and alternating leg kicks. The right arm stays extended forward at the water surface carrying a long pole. Smooth continuous calm swimming.'),
 ('swim_carry_two',180,'A person floats horizontally and swims slowly with alternating leg kicks, keeping both arms extended forward holding a floating object. Calm smooth kicking, steady upper body, face just above the water.'),
 ('swim',180,'A person swims forward with smooth alternating front crawl arm strokes and gentle flutter kicks, body horizontal in the water. Natural continuous movement at a moderate pace.'),
 ('throw',150,'A person calmly draws a stone back beside the right ear and throws it forward overarm, follows through and returns upright. A small weight shift between planted feet, no deep squat or jump.'),
 ('spear_throw',180,'An upright person holds a javelin beside the right ear, draws the right elbow back, makes a controlled forward overarm javelin throw and follows through. One small step forward, a steady pelvis and no deep squat.'),
 ('sling_ready',120,'A person stands upright calmly holding a sling in the right hand low beside the right thigh, with the elbow relaxed. Feet planted and quiet breathing.'),
 ('sling_whirl',180,'An upright person smoothly rotates a sling in a compact vertical circle beside the right shoulder. The feet stay planted and the pelvis remains level, with restrained shoulder motion.'),
 ('sling_release',120,'A standing person releases a sling throw with one controlled forward right arm swing, follows through across the body and returns upright. Small torso rotation, feet planted, no deep knee bend.'),
 ('gather',150,'A person calmly bends at hips and knees, reaches down to pick a small object from the ground with the right hand, then stands upright smoothly. One ordinary unhurried pickup.'),
 ('drink',150,'An upright person brings both cupped hands from waist height to their mouth, takes a small drink, and lowers the hands. Steady feet and a small head inclination, no crouching.'),
 ('eat',150,'An upright person brings a small bite of food from chest height to their mouth with the right hand, eats it calmly, and lowers the hand. The pelvis stays level and the feet remain planted.'),
 ('work_knapping',180,'A seated-height hand task performed standing: a person holds a stone near the waist in the left palm and gives it small precise taps with a hammerstone in the right hand. Hands stay below the chest, torso upright, steady legs.'),
 ('work_cut',180,'An upright person holds a small piece of wood at waist height in the left hand and makes short careful whittling strokes away from the body with a knife in the right hand. Quiet planted feet, small precise hand movements.'),
 ('work_twist',180,'A standing person calmly twists and rolls plant fibres between both hands at waist height. Small precise repeated finger and wrist movements, elbows close to the sides, quiet steady legs.'),
 ('work_dig',180,'A person uses a short digging stick with both hands to loosen soil in front of the feet. Modest forward bend at the hips, a short downward push and lever motion, calm steady rhythm and no deep squat.'),
 ('carry_large',150,'An upright person carries a large heavy rock close in front of their abdomen with both hands under it, elbows tucked near the sides. Slight backward counterbalance, quiet steady feet and very small breathing motion.'),
]

def main():
 p=argparse.ArgumentParser();p.add_argument('--worker-index',type=int,default=0);p.add_argument('--worker-count',type=int,default=1);p.add_argument('--attempts',type=int,default=2);p.add_argument('--case');p.add_argument('--run-one',action='store_true');p.add_argument('--seed',type=int);p.add_argument('--prompt');p.add_argument('--frames',type=int);p.add_argument('--steps',type=int,default=50);p.add_argument('--encode-only',action='store_true');a=p.parse_args()
 spec=importlib.util.spec_from_file_location('natural_generate',REPO/'scripts/run-kimodo-demo-generation.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 m.ROOT=ROOT;m.CASES=[{'id':n,'label':n,'frames':f,'prompt':s,'seed':3100+i*10} for i,(n,f,s) in enumerate(CASES)]
 if not (ROOT/'model-provenance.json').exists():(ROOT/'model-provenance.json').write_bytes((REPO/'artifacts/kimodo-first-person-demo/model-provenance.json').read_bytes())
 (ROOT/'queue.json').write_text(json.dumps(m.CASES,indent=2))
 if a.run_one:
  sys.argv=[sys.argv[0],'--case',a.case,'--seed',str(a.seed),'--steps',str(a.steps)]
  if a.prompt:sys.argv+=['--prompt',a.prompt]
  if a.frames:sys.argv+=['--frames',str(a.frames)]
  if a.encode_only:sys.argv+=['--encode-only']
  m.main();return
 jobs=[]
 for i,c in enumerate(m.CASES):
  if a.case and c['id']!=a.case:continue
  for attempt in range(a.attempts):jobs.append((i,c,attempt))
 assigned=[j for k,j in enumerate(jobs) if k%a.worker_count==a.worker_index]
 for number,(i,c,attempt) in enumerate(assigned):
  command=[sys.executable,str(Path(__file__)), '--run-one','--case',c['id'],'--seed',str(c['seed']+attempt),'--steps',str(a.steps)]
  started=time.time();subprocess.run(command,cwd=REPO,check=True)
  (ROOT/f'worker-{a.worker_index}.json').write_text(json.dumps({'completed':number+1,'assigned':len(assigned),'case':c['id'],'seed':c['seed']+attempt,'last_seconds':time.time()-started,'running':number+1<len(assigned)},indent=2))
  print('NATURAL QUEUE',a.worker_index,number+1,'/',len(assigned),c['id'],c['seed']+attempt,flush=True)
 print('NATURAL WORKER FINISHED',a.worker_index,flush=True)

if __name__=='__main__':main()

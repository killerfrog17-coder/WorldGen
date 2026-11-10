"""Correlate optional native stop/resume callbacks with NPC benchmark slow frames.
GC cycle duration is not pause duration. Require completed native evidence and
use each window's measured Mono/native clock bridge, with uncertainty reported.
"""
import argparse, csv, json
from pathlib import Path

def analyze(folder):
    if not (folder / 'checks.txt').exists():
        raise ValueError('Only analyze a completed, successful native run')
    with (folder / 'native-gc-events.csv').open(encoding='utf-8-sig', newline='') as f:
        events=sorted(csv.DictReader(f), key=lambda e:int(e['native_qpc']))
    pending={};pauses=[];unmatched=0
    for event in events:
        kind=int(event['event']);thread=int(event['thread']);qpc=int(event['native_qpc'])
        if kind==6:
            if thread in pending:unmatched+=1
            pending[thread]={6:qpc}
        elif kind in (7,8) and thread in pending:pending[thread][kind]=qpc
        elif kind==9:
            pause=pending.pop(thread,None)
            if pause is None or 7 not in pause or 8 not in pause:unmatched+=1;continue
            pause[9]=qpc
            if not pause[6]<=pause[7]<=pause[8]<=pause[9]:raise ValueError('Non-monotonic stop/resume sequence')
            pauses.append(pause)
    unmatched+=len(pending)
    results=[]
    for row in json.loads((folder/'performance.json').read_text(encoding='utf-8-sig')):
        hz=row['ClockFrequency'];managed_hz=row['ManagedFrequency']
        if hz<=0 or managed_hz<=0:raise ValueError('No actual clock bridge')
        midpoint=(row['ClockBefore']+row['ClockAfter'])/2
        def native(qpc):return row['ClockNative']+(qpc-midpoint)*hz/managed_hz
        peaks=[]
        for frame in row['SlowFrames']:
            end=native(frame['ManagedFrameEnd']);start=end-frame['FrameMs']*hz/1000
            overlap=sum(max(0,min(end,p[9])-max(start,p[6])) for p in pauses)*1000/hz
            peaks.append(dict(frame_ms=frame['FrameMs'],npc_ms=frame['NpcMs'],stop_resume_overlap_ms=overlap))
        results.append(dict(mode=row['Mode'],clock_bridge_uncertainty_ms=(row['ClockAfter']-row['ClockBefore'])*500/managed_hz,
            mean_frame_ms=row['MeanFrameMs'],p99_frame_ms=row['P99FrameMs'],worst_frames=peaks,
            allocation_counter_available=row.get('AllocationCounterAvailable',False)))
    return dict(scope='Native Mono pre-stop to post-restart intervals overlapping recorded slow frames; overlap is evidence, not proof of sole causation.',events=len(events),complete_stop_resume_intervals=len(pauses),unmatched_intervals=unmatched,windows=results)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('folder',type=Path);parser.add_argument('--output',type=Path);args=parser.parse_args()
    result=analyze(args.folder);text=json.dumps(result,indent=2)+'\n'
    if args.output:args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(text,encoding='utf-8')
    else:print(text)

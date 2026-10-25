"""Owner-requested read-only native progress window on the rightmost display.

Counts require hash-matching checks and a recorded visual inspection. Estimates
are rough workload ranges, not promises or a fabricated countdown.
"""
import ctypes, datetime, hashlib, json, math, tkinter as tk
import ctypes.wintypes
from pathlib import Path
from tkinter import ttk

repo=Path(__file__).resolve().parent.parent
batch=repo/'artifacts/fauna-overnight-20261024'; manifest=batch/'owner-review-progress.json'
def read(path):
    try: return json.loads(path.read_text())
    except (OSError,ValueError): return {}
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
cache={}
def cached_sha(path):
    if not path.is_file():return None
    key=(str(path),path.stat().st_mtime_ns,path.stat().st_size)
    if key not in cache:cache[key]=sha(path)
    return cache[key]
def readiness(task,started):
    folder=batch/task['slug']
    if task['kind']=='manual':
        out=folder/'Animal - Owner Corrected.blend'; evidence=read(folder/'owner-fix.json')
        check=read(folder/'owner-fix-native-checks.json'); fraction=.55 if out.exists() else 0
        if out.exists() and check.get('checked_blend_sha256')==cached_sha(out):
            fraction=.8
            if evidence.get('visual_checks_completed') and not evidence.get('native_checks_pending',True):return 1,'Ready'
        return fraction,'Checking repair' if fraction else 'Repair queued'
    proof=read(folder/'owner-redo-readiness.json'); out=folder/'Animal - Raw Audition.blend'
    if proof.get('visual_inspected') and proof.get('checked_blend_sha256')==cached_sha(out) and proof.get('request_time','')>=started:return 1,'Ready for selection'
    request=read(folder/'owner-reroll-request-current.json'); valid=request.get('time','')>=started
    if not valid:return 0,'New reference / regeneration queued'
    count=sum((folder/f'candidate-{i}'/'model.glb').exists() for i in range(1,5))
    rigs=sum((folder/f'candidate-{i}'/'generated-rig.npz').exists() for i in range(1,5))
    fraction=.10+.35*count/4+.25*rigs/4
    check=read(folder/'native-checks.json')
    if check.get('checked_blend_sha256')==cached_sha(out) and out.exists():fraction=.85
    return fraction, f'{count}/4 models, {rigs}/4 rigs' if fraction<.85 else 'Visual checks'

window=tk.Tk(); window.title('Animal batch progress'); window.configure(bg='#19232d')
window.attributes('-topmost',True)
rectangles=[]
try:
    callback=ctypes.WINFUNCTYPE(ctypes.c_int,ctypes.c_void_p,ctypes.c_void_p,ctypes.POINTER(ctypes.wintypes.RECT),ctypes.c_void_p)
    @callback
    def monitor(hdc,unused,rect,data):
        r=rect.contents;rectangles.append((r.left,r.top,r.right,r.bottom));return 1
    ctypes.windll.user32.EnumDisplayMonitors(None,None,monitor,0)
except (AttributeError,TypeError):pass
if rectangles:
    left,top,right,bottom=max(rectangles,key=lambda r:r[0]);window.geometry(f'570x360+{max(left,right-590)}+{max(top,bottom-430)}')
else:window.geometry('570x360')
style=ttk.Style();style.theme_use('clam');style.configure('Fauna.Horizontal.TProgressbar',background='#62c79e',troughcolor='#344250',borderwidth=0)
tk.Label(window,text='Animal reviews · preparing the next batch',font=('Segoe UI',14,'bold'),fg='white',bg='#19232d',anchor='w').pack(fill='x',padx=18,pady=(15,8))
summary=tk.StringVar();tk.Label(window,textvariable=summary,font=('Segoe UI',11),fg='#e1edf5',bg='#19232d',anchor='w').pack(fill='x',padx=18)
bar=ttk.Progressbar(window,maximum=100,style='Fauna.Horizontal.TProgressbar');bar.pack(fill='x',padx=18,pady=10)
eta=tk.StringVar();tk.Label(window,textvariable=eta,font=('Segoe UI',11,'bold'),fg='#a8e8d0',bg='#19232d',anchor='w').pack(fill='x',padx=18)
current=tk.StringVar();tk.Label(window,textvariable=current,font=('Segoe UI',10),fg='#d3dce6',bg='#19232d',wraplength=530,anchor='w',justify='left').pack(fill='x',padx=18,pady=8)
listing=tk.Listbox(window,height=6,font=('Segoe UI',10),bg='#24333f',fg='#e1edf5',borderwidth=0,highlightthickness=0);listing.pack(fill='both',expand=True,padx=18,pady=(0,10))
tk.Label(window,text='You can close this window; work continues. Blender opens when the batch is ready.',font=('Segoe UI',8),fg='#a7b7c7',bg='#19232d').pack(pady=(0,10))
def tick():
    data=read(manifest);tasks=data.get('tasks',[]);started=data.get('started','');results=[readiness(t,started) for t in tasks]
    done=sum(f==1 for f,_ in results);fraction=sum(f for f,_ in results)/max(1,len(tasks));bar['value']=fraction*100
    summary.set(f'{done} / {len(tasks)} tasks ready · {round(fraction*100)}% through the planned stages')
    seconds=sum(t.get('estimate_seconds',240)*(1-f) for t,(f,_) in zip(tasks,results))*.65
    if done==len(tasks) and tasks:eta.set('Batch ready — opening the Blender comparison')
    else:eta.set(f'Rough remaining: {max(1,math.ceil(seconds*.7/60))}–{max(2,math.ceil(seconds*1.35/60))} min · retries may extend this')
    queue=read(batch/'queue-state.json');gpu=queue.get('current');phase=queue.get('phase')
    current.set(data.get('current','Working')+(f'\nGeneration: {gpu} · {phase}' if gpu else ''))
    selection=listing.curselection();listing.delete(0,'end')
    for task,(f,status) in zip(tasks,results):listing.insert('end',('✓ ' if f==1 else '· ')+task['name']+' — '+status)
    if selection:listing.see(selection[0])
    (batch/'owner-review-progress-live.json').write_text(json.dumps({'ready':done,'total':len(tasks),'fraction':fraction,'estimated_remaining_seconds':seconds,'updated':datetime.datetime.now().astimezone().isoformat()},indent=2))
    window.after(3000,tick)
tick();window.mainloop()

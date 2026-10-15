// Presentation only: dock dragging and audio-device/browser policy. No simulation.
let callback,observer,resizeObserver,audio,disposed=false,musicTimer,requesting=false,retryMusic=false;
let desiredEra=1,currentTrack=null,musicVolume=.6,gain=0,musicRevision=0,fade=null;
function applyVolume(){if(audio)audio.volume=musicVolume*gain;}
function fadeTo(target,seconds){
 if(fade){cancelAnimationFrame(fade.frame);fade.resolve(false);fade=null;}
 return new Promise(resolve=>{const from=gain,start=performance.now();const job={resolve,frame:0};fade=job;
  const tick=now=>{const t=Math.min(1,(now-start)/(seconds*1000));gain=from+(target-from)*t;applyVolume();if(t>=1){fade=null;resolve(true);}else job.frame=requestAnimationFrame(tick);};job.frame=requestAnimationFrame(tick);});
}
export async function musicContext(era,volume){
 const next=Math.max(0,Math.min(1,volume));if(next!==musicVolume){musicVolume=next;try{localStorage.setItem('worldgen-music-volume',String(next));}catch{}applyVolume();}
 if(era===desiredEra)return;desiredEra=era;const revision=++musicRevision;clearTimeout(musicTimer);
 if(currentTrack&&era>=currentTrack.first&&era<=currentTrack.last){if(audio&&!audio.paused)void fadeTo(1,.8);return;}
 if(audio&&!audio.paused)await fadeTo(0,1.1);
 if(revision!==musicRevision||disposed)return;
 audio?.pause();currentTrack=null;gain=0;applyVolume();if(desiredEra>0)void requestMusic();
}
const heights=new Map();
function clampHeight(value){return Math.max(100,Math.min(window.innerHeight-100,value));}
function layout(){
 for(const panel of document.querySelectorAll('[data-dock]')){
  const key=panel.dataset.dock;
  if(heights.has(key))panel.style.height=`${clampHeight(heights.get(key))}px`;
  resizeObserver?.observe(panel);
  const list=panel.querySelector('.state-list-scroll'),columns=panel.querySelector('.state-columns');
  if(list&&columns){const entries=[...columns.querySelectorAll('.society-entry')],rows=Math.max(1,Math.floor(list.clientHeight/21));
   if(columns.style.getPropertyValue('--state-rows')!==String(rows))columns.style.setProperty('--state-rows',String(rows));
   entries.forEach((entry,i)=>{const row=String(Math.floor(i/(rows*2))*rows+i%rows+1),column=String(Math.floor(i/rows)%2+1);
    if(entry.style.gridRow!==row)entry.style.gridRow=row;if(entry.style.gridColumn!==column)entry.style.gridColumn=column;});}
 }
}
let drag=null;
function pointerDown(event){
 const handle=event.target.closest('.panel-resizer');if(!handle||event.button!==0)return;
 const panel=handle.closest('[data-dock]');drag={panel,y:event.clientY,height:panel.getBoundingClientRect().height};
 handle.setPointerCapture(event.pointerId);event.preventDefault();
}
function pointerMove(event){if(!drag)return;const height=clampHeight(drag.height+drag.y-event.clientY);heights.set(drag.panel.dataset.dock,height);drag.panel.style.height=`${height}px`;layout();event.preventDefault();}
function pointerUp(){drag=null;}
function keyDown(event){
 const handle=event.target.closest?.('.panel-resizer');if(!handle||!['ArrowUp','ArrowDown'].includes(event.key))return;
 const panel=handle.closest('[data-dock]');heights.set(panel.dataset.dock,clampHeight(panel.getBoundingClientRect().height+(event.key==='ArrowUp'?20:-20)));layout();event.preventDefault();
}
async function requestMusic(){if(disposed||desiredEra===0||audio&&!audio.paused)return;if(requesting){retryMusic=true;return;}requesting=true;try{await callback.invokeMethodAsync('NextMusic');}finally{requesting=false;if(retryMusic){retryMusic=false;if(!disposed&&desiredEra>0)setTimeout(requestMusic,0);}}}
function gesture(){if(audio?.paused&&!disposed&&desiredEra>0)void requestMusic();}
export function init(dotnet){
 disposed=false;callback=dotnet;desiredEra=1;currentTrack=null;gain=0;
 try{const saved=localStorage.getItem('worldgen-music-volume');musicVolume=saved===null ? .6 : Math.max(0,Math.min(1,Number(saved)));if(!Number.isFinite(musicVolume))musicVolume=.6;}catch{musicVolume=.6;}
 audio=new Audio();applyVolume();audio.addEventListener('ended',requestMusic);audio.addEventListener('error',()=>scheduleMusic());
 observer=new MutationObserver(layout);observer.observe(document.querySelector('.game-shell'),{childList:true,subtree:true});
 resizeObserver=new ResizeObserver(layout);layout();
 document.addEventListener('pointerdown',pointerDown);document.addEventListener('pointermove',pointerMove);document.addEventListener('pointerup',pointerUp);document.addEventListener('pointercancel',pointerUp);document.addEventListener('keydown',keyDown);
 document.addEventListener('pointerdown',gesture);document.addEventListener('keydown',gesture);window.addEventListener('resize',layout);scheduleMusic();return musicVolume;
}
export async function catalog(){try{const response=await fetch('music/catalog.json',{cache:'no-store'});return response.ok?await response.json():[];}catch{return [];}}
export async function playTrack(filename,first,last,expectedEra){
 const revision=musicRevision;if(desiredEra===0||expectedEra!==desiredEra||desiredEra<first||desiredEra>last)return 'stale';
 audio.src=`music/${encodeURIComponent(filename)}`;
 try{gain=0;applyVolume();await audio.play();if(revision!==musicRevision){audio.pause();return 'stale';}currentTrack={first,last};void fadeTo(1,.8);return 'playing';}catch(error){return error.name==='NotAllowedError'?'blocked':'failed';}
}
export function scheduleMusic(){clearTimeout(musicTimer);if(!disposed&&desiredEra>0)musicTimer=setTimeout(requestMusic,10000);}
export function musicStatus(){return {era:desiredEra,playing:!!audio&&!audio.paused,source:audio?.src??'',volume:audio?.volume??0,gain,musicVolume,first:currentTrack?.first??0,last:currentTrack?.last??0};}
export function dispose(){disposed=true;musicRevision++;if(fade){cancelAnimationFrame(fade.frame);fade.resolve(false);fade=null;}clearTimeout(musicTimer);audio?.pause();if(audio){audio.removeAttribute('src');audio.load();}observer?.disconnect();resizeObserver?.disconnect();
 document.removeEventListener('pointerdown',pointerDown);document.removeEventListener('pointermove',pointerMove);document.removeEventListener('pointerup',pointerUp);document.removeEventListener('pointercancel',pointerUp);document.removeEventListener('keydown',keyDown);
 document.removeEventListener('pointerdown',gesture);document.removeEventListener('keydown',gesture);window.removeEventListener('resize',layout);callback=null;drag=null;}

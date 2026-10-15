// Presentation only: dock dragging and audio-device/browser policy. No simulation.
let callback,observer,resizeObserver,audio,disposed=false,musicTimer,requesting=false;
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
async function requestMusic(){if(disposed||requesting)return;requesting=true;try{await callback.invokeMethodAsync('NextMusic');}finally{requesting=false;}}
function gesture(){if(audio?.paused&&!disposed)requestMusic();}
export function init(dotnet){
 disposed=false;callback=dotnet;audio=new Audio();audio.volume=.6;audio.addEventListener('ended',requestMusic);audio.addEventListener('error',()=>scheduleMusic());
 observer=new MutationObserver(layout);observer.observe(document.querySelector('.game-shell'),{childList:true,subtree:true});
 resizeObserver=new ResizeObserver(layout);layout();
 document.addEventListener('pointerdown',pointerDown);document.addEventListener('pointermove',pointerMove);document.addEventListener('pointerup',pointerUp);document.addEventListener('pointercancel',pointerUp);document.addEventListener('keydown',keyDown);
 document.addEventListener('pointerdown',gesture);document.addEventListener('keydown',gesture);window.addEventListener('resize',layout);scheduleMusic();
}
export async function catalog(){try{const response=await fetch('music/catalog.json',{cache:'no-store'});return response.ok?await response.json():[];}catch{return [];}}
export async function playTrack(filename){
 audio.src=`music/${encodeURIComponent(filename)}`;
 try{await audio.play();return 'playing';}catch(error){return error.name==='NotAllowedError'?'blocked':'failed';}
}
export function scheduleMusic(){clearTimeout(musicTimer);if(!disposed)musicTimer=setTimeout(requestMusic,10000);}
export function dispose(){disposed=true;clearTimeout(musicTimer);audio?.pause();if(audio){audio.removeAttribute('src');audio.load();}observer?.disconnect();resizeObserver?.disconnect();
 document.removeEventListener('pointerdown',pointerDown);document.removeEventListener('pointermove',pointerMove);document.removeEventListener('pointerup',pointerUp);document.removeEventListener('pointercancel',pointerUp);document.removeEventListener('keydown',keyDown);
 document.removeEventListener('pointerdown',gesture);document.removeEventListener('keydown',gesture);window.removeEventListener('resize',layout);callback=null;drag=null;}

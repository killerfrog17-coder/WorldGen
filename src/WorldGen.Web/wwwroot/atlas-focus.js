let reader,previousFocus;
const selector='button:not(:disabled),input:not(:disabled),select:not(:disabled),[tabindex="0"]';
function visibleControls(){return [...reader.querySelectorAll(selector)].filter(el=>el.getClientRects().length);}
function trap(event){
 if(event.key!=='Tab'||!reader?.isConnected)return;
 const controls=visibleControls();if(!controls.length)return;
 const first=controls[0],last=controls[controls.length-1],active=document.activeElement;
 if(event.shiftKey&&(active===first||!reader.contains(active))){event.preventDefault();last.focus();}
 else if(!event.shiftKey&&(active===last||!reader.contains(active))){event.preventDefault();first.focus();}
}
export function activate(element){
 deactivate();reader=element;previousFocus=document.activeElement;
 document.addEventListener('keydown',trap,true);
 visibleControls().find(el=>el.textContent==='Close')?.focus();
}
export function deactivate(){
 document.removeEventListener('keydown',trap,true);
 if(previousFocus?.isConnected)previousFocus.focus();reader=previousFocus=null;
}

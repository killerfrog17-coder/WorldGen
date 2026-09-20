let rollbackWorker,rollbackJson="",generationView=false;
let callback,worker,readyWorker,generation=0,canvas,gl,program,frame=0,lastJson='';
let yaw=.6,pitch=-.18,zoom=3.15,dragging=false,moved=false,lastX=0,lastY=0;
let viewExpected=1,viewReceived=0,revision=0,currentOptions={},pendingOptions={};
let map=false,mapZoom=1,panX=0,panY=0,centerLongitude=0,chunks=[],pending=[],incoming=[],viewDone=false,viewTicket=0,viewResolve,viewReject;
export function snapshot(){return lastJson;}
export function viewInfo(){return {map,vertices:chunks.reduce((n,c)=>n+c.vertices,0),triangles:chunks.reduce((n,c)=>n+c.count/3,0),chunks:chunks.length,centerLongitude,loading:!!viewResolve,revision,options:currentOptions};}
function mapScale(){return Math.max(1.05,2.1/(canvas.clientWidth/canvas.clientHeight))*mapZoom;}
export function longitude(value){centerLongitude=value;}
export function fullscreen(){if(document.fullscreenElement)return document.exitFullscreen();return document.querySelector('.observatory').requestFullscreen();}
export function init(dotnet){
 callback=dotnet;canvas=document.getElementById('globe');gl=canvas.getContext('webgl',{antialias:true,alpha:true,preserveDrawingBuffer:true});
 if(!gl||!gl.getExtension('OES_element_index_uint'))throw new Error('WebGL with 32-bit indices is required.');
 const vertex=`attribute vec3 position;attribute vec3 color;uniform mat3 rotation;uniform float aspect,camera,mapScale,mapOffset;uniform bool isMap;uniform vec2 pan;varying vec3 vColor,vNormal;varying float mapX;void main(){vColor=color;mapX=position.x+mapOffset;if(isMap){vNormal=vec3(0.,0.,1.);gl_Position=vec4((mapX-pan.x)/(mapScale*aspect),(position.y-pan.y)/mapScale,-.1+position.z,1.);}else{vec3 p=rotation*position;vNormal=rotation*normalize(position);float z=p.z-camera;gl_Position=vec4(p.x*2.4/aspect,p.y*2.4,-1.0001*z-.01,-z);}}`;
 const fragment=`precision mediump float;varying vec3 vColor,vNormal;varying float mapX;uniform bool isMap,unlit,linePass;void main(){if(isMap&&abs(mapX)>2.00001)discard;float light=(unlit||linePass)?1.:.55+.45*max(0.,dot(normalize(vNormal),normalize(vec3(-.5,.7,1.))));gl_FragColor=vec4(vColor*light,1.);}`;
 function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}
 program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));
 canvas.onpointerdown=e=>{dragging=true;moved=false;lastX=e.clientX;lastY=e.clientY;canvas.setPointerCapture(e.pointerId);};
 canvas.onpointermove=e=>{if(!dragging)return;const dx=e.clientX-lastX,dy=e.clientY-lastY;if(Math.abs(dx)+Math.abs(dy)>2)moved=true;if(map){const scale=mapScale()*2/canvas.clientHeight;panX=Math.max(-2,Math.min(2,panX-dx*scale));panY=Math.max(-1,Math.min(1,panY+dy*scale));}else{yaw+=dx*.006;pitch=Math.max(-1.553,Math.min(1.553,pitch+dy*.006));}lastX=e.clientX;lastY=e.clientY;};
 canvas.onpointerup=e=>{dragging=false;if(!moved)pick(e);};canvas.onpointercancel=()=>dragging=false;
 canvas.onwheel=e=>{e.preventDefault();const factor=Math.exp(e.deltaY*(e.deltaMode===1?.03:.0022));if(map)mapZoom=Math.max(.015,Math.min(2,mapZoom*factor));else zoom=1.025+Math.max(.035,Math.min(5,(zoom-1.025)*factor));};
 canvas.onkeydown=e=>{if(e.key.startsWith('Arrow'))e.preventDefault();if(map){if(e.key==='ArrowLeft')panX-=.1;if(e.key==='ArrowRight')panX+=.1;if(e.key==='ArrowUp')panY+=.1;if(e.key==='ArrowDown')panY-=.1;}else{if(e.key==='ArrowLeft')yaw-=.1;if(e.key==='ArrowRight')yaw+=.1;if(e.key==='ArrowUp')pitch-=.1;if(e.key==='ArrowDown')pitch+=.1;pitch=Math.max(-1.553,Math.min(1.553,pitch));}};
 const controls=document.querySelector('.controls'),handle=document.createElement('div');handle.className='panel-resize';handle.setAttribute('role','separator');handle.setAttribute('aria-label','Resize settings panel');controls.append(handle);
 handle.onpointerdown=e=>{handle.setPointerCapture(e.pointerId);e.preventDefault();};
 handle.onpointermove=e=>{if(handle.hasPointerCapture(e.pointerId)){const bounds=document.querySelector('.workspace').getBoundingClientRect();document.documentElement.style.setProperty('--panel-width',Math.max(240,Math.min(innerWidth-360,e.clientX-bounds.left))+'px');}};
 render();
}
function rotation(){const c=Math.cos(yaw),s=Math.sin(yaw),a=Math.cos(pitch),b=Math.sin(pitch);return new Float32Array([c,b*s,-a*s,0,a,b,s,-b*c,a*c]);}
function free(list){for(const c of list)for(const b of c.buffers)gl.deleteBuffer(b);}
function upload(bytes){
 const header=new Int32Array(bytes.buffer,bytes.byteOffset,5);let offset=bytes.byteOffset+20;
 function take(Type,count){const a=new Type(bytes.buffer,offset,count);offset+=count*4;return a;}
 const d={Positions:take(Float32Array,header[0]),Colors:take(Float32Array,header[0]),Triangles:take(Uint32Array,header[1]),Lines:take(Float32Array,header[2]),LineColors:take(Float32Array,header[2]),IsMap:!!header[3],Unlit:!!header[4]},buffers=[];
 function buffer(array,index=false){const b=gl.createBuffer(),target=index?gl.ELEMENT_ARRAY_BUFFER:gl.ARRAY_BUFFER;gl.bindBuffer(target,b);gl.bufferData(target,array,gl.STATIC_DRAW);buffers.push(b);return b;}
 return {p:buffer(d.Positions),c:buffer(d.Colors),i:buffer(d.Triangles,true),l:buffer(d.Lines),lc:buffer(d.LineColors),buffers,count:d.Triangles.length,lineCount:d.Lines.length/3,vertices:d.Positions.length/3,map:d.IsMap,unlit:d.Unlit};
}
function render(){
 frame=requestAnimationFrame(render);
 // A single bounded upload per animation frame leaves event handling available.
 if(incoming.length){pending.push(upload(incoming.shift()));viewReceived++;if(viewReceived%4===0)callback.invokeMethodAsync("Progress","Preparing view",80+Math.floor(19*viewReceived/viewExpected));}
 if(viewDone&&!incoming.length&&viewResolve){free(chunks);chunks=pending;currentOptions=pendingOptions;revision++;if(generationView){rollbackWorker?.terminate();rollbackWorker=null;rollbackJson="";generationView=false;}pending=[];if(chunks.length){if(map!==chunks[0].map){mapZoom=1;panX=panY=0;}map=chunks[0].map;}const resolve=viewResolve;viewResolve=viewReject=null;viewDone=false;resolve();}
 const scale=Math.min(devicePixelRatio,2),width=Math.round(canvas.clientWidth*scale),height=Math.round(canvas.clientHeight*scale);if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
 gl.viewport(0,0,width,height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);if(!chunks.length)return;
 gl.enable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.useProgram(program);
 const loc=name=>gl.getUniformLocation(program,name);
 gl.uniformMatrix3fv(loc('rotation'),false,rotation());gl.uniform1f(loc('aspect'),width/height);gl.uniform1f(loc('camera'),zoom);gl.uniform1i(loc('isMap'),map?1:0);gl.uniform1f(loc('mapScale'),mapScale());gl.uniform2f(loc('pan'),panX,panY);
 const p=gl.getAttribLocation(program,'position'),c=gl.getAttribLocation(program,'color');
 function attributes(position,color){gl.bindBuffer(gl.ARRAY_BUFFER,position);gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,color);gl.enableVertexAttribArray(c);gl.vertexAttribPointer(c,3,gl.FLOAT,false,0,0);}
 for(const piece of chunks)for(let copy=map?-1:0;copy<=(map?1:0);copy++){
  gl.uniform1f(loc('mapOffset'),-centerLongitude/90+copy*4);gl.uniform1i(loc('unlit'),piece.unlit?1:0);gl.uniform1i(loc('linePass'),0);
  attributes(piece.p,piece.c);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,piece.i);gl.drawElements(gl.TRIANGLES,piece.count,gl.UNSIGNED_INT,0);
  if(piece.lineCount){gl.uniform1i(loc('linePass'),1);attributes(piece.l,piece.lc);gl.drawArrays(gl.LINES,0,piece.lineCount);}
 }
}
function pick(e){
 if(!readyWorker||viewResolve)return;const box=canvas.getBoundingClientRect(),mx=(e.clientX-box.left)/box.width*2-1,my=1-(e.clientY-box.top)/box.height*2;
 let x,y,z;
 if(map){const px=mx*mapScale()*box.width/box.height+panX,py=my*mapScale()+panY;if(Math.abs(px)>2||Math.abs(py)>1)return;const lon=px*Math.PI/2+centerLongitude*Math.PI/180,lat=py*Math.PI/2;x=Math.cos(lat)*Math.cos(lon);y=Math.cos(lat)*Math.sin(lon);z=Math.sin(lat);}
 else{
  let dx=mx*(box.width/box.height)/2.4,dy=my/2.4,dz=-1,len=Math.hypot(dx,dy,dz);dx/=len;dy/=len;dz/=len;
  const b=zoom*dz,disc=b*b-zoom*zoom+1;if(disc<0)return;const t=-b-Math.sqrt(disc),px=dx*t,py=dy*t,pz=zoom+dz*t,r=rotation();
  x=r[0]*px+r[1]*py+r[2]*pz;z=r[3]*px+r[4]*py+r[5]*pz;y=-(r[6]*px+r[7]*py+r[8]*pz);
 }
 readyWorker.postMessage({type:'point',x,y,z});
}
export function pickCell(cell){readyWorker?.postMessage({type:'pick',cell});}
export function view(options){
 cancelView();if(!readyWorker)return Promise.reject(new Error('Generate a world first.'));
 viewReceived=0;const settings=JSON.parse(options),count=JSON.parse(lastJson).CellCount;pendingOptions=settings;viewExpected=Math.ceil((count*3-6)/8192)+(settings.GridSpacing?1:0);
 const ticket=++viewTicket;return new Promise((resolve,reject)=>{viewResolve=resolve;viewReject=reject;readyWorker.postMessage({type:'view',options,ticket});});
}
function cancelView(){viewTicket++;readyWorker?.postMessage({type:'cancel-view'});incoming=[];free(pending);pending=[];viewDone=false;const resolve=viewResolve;viewResolve=viewReject=null;resolve?.();}
function receive(source,d){
 if(d.type==='view-piece'&&d.ticket===viewTicket)incoming.push(d.bytes);
 else if(d.type==='view-complete'&&d.ticket===viewTicket)viewDone=true;
 else if(d.type==='inspection')callback.invokeMethodAsync('Inspection',d.text);
 else if(d.type==='export'){if(d.kind==='heightmap')downloadBytes('worldgen.heightmap.pgm',d.data);else download(d.kind==='csv'?'worldgen.cells.csv':'worldgen.result.json',d.data,d.kind==='csv'?'text/csv':'application/json');}
 else if(d.type==='error'){const reject=viewReject;reject?.(new Error(d.message));cancelView();callback.invokeMethodAsync('Failed',d.message);}
}
export function generate(recipe){
 cancel();const ticket=generation,created=new Worker(new URL('./worker/main.js',import.meta.url),{type:'module'});worker=created;
 created.onerror=e=>callback.invokeMethodAsync('Failed',e.message||'Worker failed.');
 created.onmessage=async e=>{
  const d=e.data;if(d.type==='ready'){if(ticket===generation)created.postMessage({recipe});}
  else if(d.type==='progress'){if(ticket===generation)callback.invokeMethodAsync('Progress',d.stage,Math.floor(d.percent*.8));}
  else if(d.type==='complete'&&ticket===generation){rollbackWorker=readyWorker;rollbackJson=lastJson;generationView=true;readyWorker=created;worker=null;lastJson=d.json;await callback.invokeMethodAsync('Complete',d.json);}
  else receive(created,d);
 };
}
export function cancel(){
 generation++;worker?.terminate();worker=null;cancelView();
 if(generationView){readyWorker?.terminate();readyWorker=rollbackWorker;lastJson=rollbackJson;rollbackWorker=null;rollbackJson="";generationView=false;callback?.invokeMethodAsync('Restore',lastJson);}
 if(currentOptions.Projection!==undefined)callback?.invokeMethodAsync('RestoreView',JSON.stringify(currentOptions));
}
export function exportWorld(kind){readyWorker?.postMessage({type:'export',kind});}
export function download(name,text,type){save(name,new Blob([text],{type}));}
export function downloadBytes(name,base64){save(name,new Blob([Uint8Array.from(atob(base64),c=>c.charCodeAt(0))],{type:'image/x-portable-graymap'}));}
function save(name,blob){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export function dispose(){cancel();readyWorker?.terminate();cancelAnimationFrame(frame);free(chunks);}

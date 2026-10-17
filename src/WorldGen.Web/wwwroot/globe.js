import * as worldLibrary from './world-library.js';
let libraryExport=null,libraryLoadTicket=0,bundledFetch=null;
let rollbackWorker,rollbackJson="",generationView=false,generationViewImported=false;
let previewWorker,previewTicket=0,isGenerating=false,backupChunks=null,backupOptions=null,backupMap=false,previewLoading=false,queuedPreview=null;
let displayWorker,pendingWorker,paletteRequest; let callback,worker,readyWorker,generation=0,canvas,gl,program,frame=0,lastJson='';
let peopleRequest;let peopleTicket=0;let satelliteOpacity=1,overlayOpacity=.6,humanLayersEnabled=false;
let societyMarks=[],societyBorders=[],marksCanvas,settlementMarkers=1,filteredSocietyMarks=[];
function filterCityMarkers(){filteredSocietyMarks=societyMarks.filter(m=>!m.City||settlementMarkers===2||settlementMarkers===1&&m.Capital||settlementMarkers===3&&m.Urban||settlementMarkers===4&&m.Large);}
export function cityMarkerMode(mode){settlementMarkers=mode;filterCityMarkers();}
export function humanLayers(enabled,base,overlay){humanLayersEnabled=enabled;satelliteOpacity=base;overlayOpacity=overlay;}
export function humanOpacity(value){satelliteOpacity=Math.max(0,Math.min(1,value));}
export function focusHuman(lon,lat){if(map)centerLongitude=lon;else{yaw=-Math.PI/2-lon*Math.PI/180;pitch=lat*Math.PI/180;}}
export function focusAtlas(lon,lat){focusHuman(lon,lat);if(map){const limit=Math.max(0,1-mapScale());panY=Math.max(-limit,Math.min(limit,lat/90));}}
export function people(command,value=0){if(!readyWorker)return Promise.reject(new Error('Generate a world first.'));return new Promise((resolve,reject)=>{if(peopleRequest){reject(new Error('A simulation update is already running.'));return;}const requestId=++peopleTicket;peopleRequest={resolve,reject,source:readyWorker,requestId};readyWorker.postMessage({type:'people',command,value,requestId});});}
let yaw=.6,pitch=-.18,zoom=3.15,dragging=false,moved=false,lastX=0,lastY=0;
let viewExpected=1,viewReceived=0,revision=0,currentOptions={},pendingOptions={};
let map=false,mapZoom=1,panX=0,panY=0,centerLongitude=0,chunks=[],pending=[],incoming=[],viewDone=false,viewTicket=0,viewResolve,viewReject;
export function snapshot(){return lastJson;}
export function viewInfo(){return {map,mapZoom,zoom,cityMarkerMode:settlementMarkers,visibleCityMarkers:filteredSocietyMarks.filter(m=>m.City).length,vertices:chunks.reduce((n,c)=>n+c.vertices,0),triangles:chunks.reduce((n,c)=>n+c.count/3,0),chunks:chunks.length,centerLongitude,loading:!!viewResolve,revision,options:currentOptions};}
function mapScale(){return Math.max(1,2/(canvas.clientWidth/canvas.clientHeight))*Math.min(1,mapZoom);}
export function longitude(value){centerLongitude=value;}
export function fullscreen(){if(document.fullscreenElement)return document.exitFullscreen();return document.querySelector('.observatory').requestFullscreen();}
export function exitApp(){window.close();if(!window.closed)callback.invokeMethodAsync('Failed','Your browser does not allow this tab to close itself. You can close the tab now.');}
export function init(dotnet){
 document.addEventListener('keydown',e=>{if(e.key==='Escape'){e.preventDefault();callback?.invokeMethodAsync('PauseSimulation');}else if(e.ctrlKey||e.altKey||e.metaKey||e.target.isContentEditable||['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;else if(['KeyR','KeyC'].includes(e.code)){if(!e.repeat){e.preventDefault();callback?.invokeMethodAsync('RenderKey',e.code==='KeyR'?'r':'c');}}else if(/^Digit[1-7]$/.test(e.code)){e.preventDefault();callback?.invokeMethodAsync('MapKey',Number(e.code.slice(-1)),e.shiftKey);}else if(['+','=','-','_'].includes(e.key)){e.preventDefault();callback?.invokeMethodAsync('OpacityKey',e.key==='+'||e.key==='='?1:-1,e.shiftKey);}else if([' ', ',', '.', '<', '>'].includes(e.key)){e.preventDefault();callback?.invokeMethodAsync('SimulationKey',e.key===','?'<':e.key==='.'?'>':e.key);}});
 callback=dotnet;canvas=document.getElementById('globe');gl=canvas.getContext('webgl',{antialias:true,alpha:true,preserveDrawingBuffer:true});
 if(!gl||!gl.getExtension('OES_element_index_uint'))throw new Error('WebGL with 32-bit indices is required.');
 const derivatives=!!gl.getExtension('OES_standard_derivatives');
 const vertex=`attribute vec3 position;attribute vec3 color;attribute vec2 cellUV;attribute vec2 blendIds;attribute float naturalHeight;attribute vec4 stroke;varying vec4 vStroke;varying highp vec2 vUV;varying vec4 vHuman;varying vec3 civicTint;uniform sampler2D palette,humanMask;uniform bool natural,blendCells;uniform float paletteHeight;uniform mat3 rotation;uniform float aspect,camera,mapScale,mapOffset;uniform bool isMap;uniform vec2 pan;varying vec3 vColor,vNormal,vSphere;varying float mapX,vHeight;vec2 blendUV(float id){return vec2((mod(id,2048.)+.5)/2048.,(floor(id/2048.)+.5)/paletteHeight);}void main(){vStroke=stroke;vUV=cellUV;float code=texture2D(humanMask,cellUV).a;vHuman=vec4(code<.3?1.:0.,(code>.5||(code>.1&&code<.3))?1.:0.,(code>.95||(code>.2&&code<.3))?1.:0.,code>.88?.24:code>.8?.46:code>.73?.70:.78);civicTint=(code>.9&&code<.97||code>.18&&code<.245)?texture2D(humanMask,cellUV).rgb:vHuman.www;vColor=natural?texture2D(palette,cellUV).rgb:color;if(blendCells)vColor=(vColor+texture2D(palette,blendUV(blendIds.x)).rgb+texture2D(palette,blendUV(blendIds.y)).rgb)/3.;vHeight=naturalHeight;mapX=position.x+mapOffset;if(isMap){float lon=position.x*1.57079632679,lat=position.y*1.57079632679;vSphere=vec3(cos(lat)*cos(lon),sin(lat),-cos(lat)*sin(lon));vNormal=vec3(0.,0.,1.);gl_Position=vec4((mapX-pan.x)/(mapScale*aspect),(position.y-pan.y)/mapScale,-.1+position.z,1.);}else{vSphere=normalize(position);vec3 p=rotation*position;vNormal=rotation*normalize(position);float z=p.z-camera;gl_Position=vec4(p.x*2.4/aspect,p.y*2.4,-1.0001*z-.01,-z);}}`;
 const fragment=(derivatives?'#extension GL_OES_standard_derivatives : enable\n':'')+`precision highp float;varying highp vec2 vUV;varying vec4 vHuman;varying vec3 civicTint;uniform sampler2D palette,humanMask;uniform bool usePalette,natural,smallFeatures,simulation,riverStroke;varying vec4 vStroke;uniform float riverPixelM;uniform float satelliteOpacity,overlayOpacity;varying vec3 vColor,vNormal,vSphere;varying float mapX,vHeight;uniform bool isMap,unlit,linePass;
 float hash3(vec3 p){return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453);}
 float noise3(vec3 p){vec3 b=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(mix(hash3(b),hash3(b+vec3(1,0,0)),f.x),mix(hash3(b+vec3(0,1,0)),hash3(b+vec3(1,1,0)),f.x),f.y),mix(mix(hash3(b+vec3(0,0,1)),hash3(b+vec3(1,0,1)),f.x),mix(hash3(b+vec3(0,1,1)),hash3(b+vec3(1,1,1)),f.x),f.y),f.z);}
 void main(){if(isMap&&abs(mapX)>2.00001)discard;
 if(riverStroke){float pixelM=${derivatives?'max(1.,max(length(dFdx(normalize(vSphere))),length(dFdy(normalize(vSphere))))*6371000.)':'riverPixelM'};float width=max(0.,vStroke.z),order=max(1.,vStroke.w),envelope=25000.+8.*width,limit=1500.*pow(3.,min(11.,order-1.));float visibility=1.-smoothstep(limit*.7,limit*1.3,pixelM);float halfWidth=min(envelope*.95,max(width*.75,pixelM*(.5+.025*log2(1.+width))));float distanceM=length(vec2(vStroke.x,max(0.,vStroke.y)))*envelope;float aa=${derivatives?'max(1.,fwidth(distanceM))':'max(1.,pixelM)'};float alpha=(1.-smoothstep(halfWidth-aa*.6,halfWidth+aa*.6,distanceM))*visibility;if(alpha<.003)discard;vec3 riverColor=mix(vec3(.075,.31,.48),vec3(.035,.25,.43),clamp(log2(1.+width)/12.,0.,1.));gl_FragColor=vec4(riverColor,alpha);return;}
 float light=(unlit||linePass)?1.:.55+.45*max(0.,dot(normalize(vNormal),normalize(vec3(-.5,.7,1.))));vec3 c=usePalette&&!natural?texture2D(palette,vUV).rgb:vColor;
 if(smallFeatures){${derivatives?'if(abs(vHeight)/max(1e-9,length(fwidth(normalize(vSphere))))<.65)discard;':''} }if(natural){vec3 p=normalize(vSphere);float fine=${derivatives?'1.-smoothstep(.0002,.0015,length(fwidth(p)))':'0.'};c*=1.+.045*(noise3(p*45.)-.5)+.08*(noise3(p*600.)-.5)*fine;vec3 ocean=mix(vec3(.025,.08,.16),vec3(.12,.48,.56),clamp((vHeight+6500.)/6500.,0.,1.));float aa=${derivatives?'max(1.,fwidth(vHeight))':'1.'};c=mix(ocean,c,smoothstep(-aa,aa,vHeight));}if(simulation&&!linePass){float occupancy=smoothstep(.35,.65,vHuman.y);bool water=(natural||smallFeatures)?vHeight<0.:vHuman.x>.5;vec3 plain=water?vec3(.55,.78,.9):vec3(1.);c=mix(plain,c*light,satelliteOpacity);if(!water&&occupancy>0.)c=mix(c,vHuman.z>.5?mix(civicTint,vec3(1.),.3):civicTint,overlayOpacity*occupancy);gl_FragColor=vec4(c,1.);}else gl_FragColor=vec4(c*light,1.);}`;
 function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}
 program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));
 canvas.onpointerdown=e=>{dragging=true;moved=false;lastX=e.clientX;lastY=e.clientY;canvas.setPointerCapture(e.pointerId);};
 canvas.onpointermove=e=>{if(!dragging)return;const dx=e.clientX-lastX,dy=e.clientY-lastY;if(Math.abs(dx)+Math.abs(dy)>2)moved=true;if(map){const scale=mapScale()*2/canvas.clientHeight;centerLongitude=((centerLongitude-dx*scale*90+180)%360+360)%360-180;panY=Math.max(-1,Math.min(1,panY+dy*scale));}else{yaw+=dx*.006;pitch=Math.max(-1.553,Math.min(1.553,pitch+dy*.006));}lastX=e.clientX;lastY=e.clientY;};
 canvas.onpointerup=e=>{dragging=false;if(!moved)pick(e);};canvas.onpointercancel=()=>dragging=false;
 canvas.onwheel=e=>{e.preventDefault();const factor=Math.exp(e.deltaY*(e.deltaMode===1?.03:.0022));if(map)mapZoom=Math.max(.015,Math.min(1,mapZoom*factor));else zoom=1.025+Math.max(.035,Math.min(5,(zoom-1.025)*factor));};
 canvas.onkeydown=e=>{if(e.key.startsWith('Arrow'))e.preventDefault();if(map){if(e.key==='ArrowLeft')panX-=.1;if(e.key==='ArrowRight')panX+=.1;if(e.key==='ArrowUp')panY+=.1;if(e.key==='ArrowDown')panY-=.1;}else{if(e.key==='ArrowLeft')yaw-=.1;if(e.key==='ArrowRight')yaw+=.1;if(e.key==='ArrowUp')pitch-=.1;if(e.key==='ArrowDown')pitch+=.1;pitch=Math.max(-1.553,Math.min(1.553,pitch));}};
 const controls=document.querySelector('.controls'),handle=document.createElement('div');handle.className='panel-resize';handle.setAttribute('role','separator');handle.setAttribute('aria-label','Resize settings panel');controls.append(handle);
 handle.onpointerdown=e=>{handle.setPointerCapture(e.pointerId);e.preventDefault();};
 handle.onpointermove=e=>{if(handle.hasPointerCapture(e.pointerId)){const bounds=document.querySelector('.workspace').getBoundingClientRect();document.documentElement.style.setProperty('--panel-width',Math.max(240,Math.min(innerWidth-360,e.clientX-bounds.left))+'px');}};
 render();
}
function rotation(){const c=Math.cos(yaw),s=Math.sin(yaw),a=Math.cos(pitch),b=Math.sin(pitch);return new Float32Array([c,b*s,-a*s,0,a,b,s,-b*c,a*c]);}
function free(list){for(const c of list){if(c.texture)gl.deleteTexture(c.texture);for(const b of c.buffers||[])gl.deleteBuffer(b);}}
function upload(bytes){
 const marker=new Int32Array(bytes.buffer,bytes.byteOffset,3);
 if(marker[0]===-1){const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.NEAREST);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,2048,marker[2],0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array(bytes.buffer,bytes.byteOffset+12));return {texture,layer:marker[1],height:marker[2],vertices:0,count:0};}
 const header=new Int32Array(bytes.buffer,bytes.byteOffset,10);let offset=bytes.byteOffset+40;
 function take(Type,count){const a=new Type(bytes.buffer,offset,count);offset+=count*4;return a;}
 const d={Positions:take(Float32Array,header[0]),Colors:take(Float32Array,header[0]),Triangles:take(Uint32Array,header[1]),Lines:take(Float32Array,header[2]),LineColors:take(Float32Array,header[2]),IsMap:!!header[3],Unlit:!!header[4]},buffers=[];
 const ids=take(Int32Array,header[6]),uv=new Float32Array(ids.length*2),height=Math.ceil(JSON.parse(lastJson).CellCount/2048);for(let i=0;i<ids.length;i++){uv[i*2]=(ids[i]%2048+.5)/2048;uv[i*2+1]=(Math.floor(ids[i]/2048)+.5)/height;}
 const naturalHeights=take(Float32Array,header[7]),blendIds=Float32Array.from(take(Int32Array,header[8])),stroke=take(Float32Array,header[9]);
 function buffer(array,index=false){const b=gl.createBuffer(),target=index?gl.ELEMENT_ARRAY_BUFFER:gl.ARRAY_BUFFER;gl.bindBuffer(target,b);gl.bufferData(target,array,gl.STATIC_DRAW);buffers.push(b);return b;}
 return {kind:header[5],strokeBuffer:stroke.length?buffer(stroke):null,blendBuffer:blendIds.length?buffer(blendIds):null,heightBuffer:naturalHeights.length?buffer(naturalHeights):null,uv:buffer(uv),p:buffer(d.Positions),c:buffer(d.Colors),i:buffer(d.Triangles,true),l:buffer(d.Lines),lc:buffer(d.LineColors),buffers,count:d.Triangles.length,lineCount:d.Lines.length/3,vertices:d.Positions.length/3,map:d.IsMap,unlit:d.Unlit};
}
function render(){
 drawSocietyMarks();
 frame=requestAnimationFrame(render);
 // A single bounded upload per animation frame leaves event handling available.
 if(incoming.length){pending.push(upload(incoming.shift()));viewReceived++;if(viewReceived%4===0)callback.invokeMethodAsync("Progress","Preparing view",Math.min(99,80+Math.floor(19*viewReceived/viewExpected)));}
 if(viewDone&&!incoming.length&&viewResolve){if(chunks!==backupChunks)free(chunks);chunks=pending;displayWorker=pendingWorker;currentOptions=pendingOptions;revision++;if(generationView&&!isGenerating){rollbackWorker?.terminate();rollbackWorker=null;rollbackJson="";generationView=false;generationViewImported=false;if(backupChunks)free(backupChunks);backupChunks=null;}pending=[];if(chunks.length){const nextMap=pendingOptions.Projection===1;if(map!==nextMap){mapZoom=1;panX=panY=0;}map=nextMap;}const resolve=viewResolve;viewResolve=viewReject=null;viewDone=false;resolve();}
 const scale=Math.min(devicePixelRatio,2),width=Math.round(canvas.clientWidth*scale),height=Math.round(canvas.clientHeight*scale);if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
 gl.depthMask(true);gl.disable(gl.BLEND);gl.viewport(0,0,width,height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);if(!chunks.length)return;
 gl.enable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.useProgram(program);
 const loc=name=>gl.getUniformLocation(program,name);
 gl.uniform1f(loc('satelliteOpacity'),satelliteOpacity);gl.uniform1f(loc('overlayOpacity'),overlayOpacity);
 const humanMask=chunks.find(c=>c.texture&&c.layer===100);if(humanMask){gl.activeTexture(gl.TEXTURE1);gl.bindTexture(gl.TEXTURE_2D,humanMask.texture);gl.uniform1i(loc('humanMask'),1);}gl.activeTexture(gl.TEXTURE0);
 if(map){const limit=Math.max(0,1-mapScale());panY=Math.max(-limit,Math.min(limit,panY));}
 gl.uniformMatrix3fv(loc('rotation'),false,rotation());gl.uniform1f(loc('aspect'),width/height);gl.uniform1f(loc('camera'),zoom);gl.uniform1i(loc('isMap'),map?1:0);gl.uniform1f(loc('mapScale'),mapScale());gl.uniform2f(loc('pan'),panX,panY);
 const p=gl.getAttribLocation(program,'position'),c=gl.getAttribLocation(program,'color'),uv=gl.getAttribLocation(program,'cellUV');
 const smooth=currentOptions.Natural!==false && [0,1].includes(currentOptions.Layer) && chunks.some(c=>c.texture&&c.layer===1000+currentOptions.Layer);
 const crispHistory=smooth&&humanLayersEnabled;
 const blendAttribute=gl.getAttribLocation(program,'blendIds');
 const h=gl.getAttribLocation(program,'naturalHeight'),strokeAttribute=gl.getAttribLocation(program,'stroke');
 gl.uniform1f(loc('riverPixelM'),map?6371000*3.14159265*mapScale()/height:6371000*Math.max(.035,zoom-1)/height);
 const palette=chunks.find(c=>c.texture&&c.layer===(smooth?1000:0)+currentOptions.Layer);if(palette){gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,palette.texture);gl.uniform1i(loc('palette'),0);gl.uniform1f(loc('paletteHeight'),palette.height);}
 function attributes(position,color){gl.bindBuffer(gl.ARRAY_BUFFER,position);gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,color);gl.enableVertexAttribArray(c);gl.vertexAttribPointer(c,3,gl.FLOAT,false,0,0);}
 for(const piece of chunks.filter(p=>!p.texture&&(p.kind===0&&(!smooth||crispHistory)||p.kind===5&&smooth&&!crispHistory||p.kind===6&&smooth||p.kind===1&&currentOptions.ShowRivers||p.kind===2&&currentOptions.Wireframe||p.kind===3&&currentOptions.Layer===4||p.kind===4&&currentOptions.Layer===38||p.kind===10+currentOptions.GridSpacing)))for(let copy=map?-1:0;copy<=(map?1:0);copy++){
  const isRiver=piece.kind===1;gl.uniform1i(loc('riverStroke'),isRiver?1:0);if(isRiver){gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);gl.depthMask(false);}else{gl.disable(gl.BLEND);gl.depthMask(true);}
  if(piece.strokeBuffer){gl.bindBuffer(gl.ARRAY_BUFFER,piece.strokeBuffer);gl.enableVertexAttribArray(strokeAttribute);gl.vertexAttribPointer(strokeAttribute,4,gl.FLOAT,false,0,0);}else{gl.disableVertexAttribArray(strokeAttribute);gl.vertexAttrib4f(strokeAttribute,0,0,0,0);}
  gl.uniform1i(loc('simulation'),humanLayersEnabled&&!!humanMask&&[0,5,6].includes(piece.kind)?1:0);
  const blend=crispHistory&&piece.kind===0;const isNatural=piece.kind===5||blend;
  gl.uniform1i(loc('blendCells'),blend?1:0);
  if(piece.blendBuffer){gl.bindBuffer(gl.ARRAY_BUFFER,piece.blendBuffer);gl.enableVertexAttribArray(blendAttribute);gl.vertexAttribPointer(blendAttribute,2,gl.FLOAT,false,0,0);}else{gl.disableVertexAttribArray(blendAttribute);gl.vertexAttrib2f(blendAttribute,0,0);}
  gl.uniform1i(loc('natural'),isNatural?1:0);gl.uniform1i(loc('smallFeatures'),piece.kind===6?1:0);
  if(piece.heightBuffer){gl.bindBuffer(gl.ARRAY_BUFFER,piece.heightBuffer);gl.enableVertexAttribArray(h);gl.vertexAttribPointer(h,1,gl.FLOAT,false,0,0);}else{gl.disableVertexAttribArray(h);gl.vertexAttrib1f(h,0);}
  const usePalette=(piece.kind===0||isNatural)&&currentOptions.Layer!==3&&!!palette;gl.uniform1i(loc('usePalette'),usePalette?1:0);if(usePalette||humanLayersEnabled&&[0,5,6].includes(piece.kind)){gl.bindBuffer(gl.ARRAY_BUFFER,piece.uv);gl.enableVertexAttribArray(uv);gl.vertexAttribPointer(uv,2,gl.FLOAT,false,0,0);}else{gl.disableVertexAttribArray(uv);gl.vertexAttrib2f(uv,0,0);}
  gl.uniform1f(loc('mapOffset'),-centerLongitude/90+copy*4);gl.uniform1i(loc('unlit'),map||(piece.kind!==0&&piece.kind!==5)||currentOptions.Layer===2||currentOptions.Layer===3||currentOptions.Layer>=38?1:0);gl.uniform1i(loc('linePass'),0);
  attributes(piece.p,piece.c);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,piece.i);gl.drawElements(gl.TRIANGLES,piece.count,gl.UNSIGNED_INT,0);
  if(piece.lineCount){gl.uniform1i(loc('linePass'),1);attributes(piece.l,piece.lc);gl.drawArrays(gl.LINES,0,piece.lineCount);}
 }
}
function drawSocietyMarks(){
 if(!canvas)return;if(!marksCanvas){marksCanvas=document.createElement('canvas');Object.assign(marksCanvas.style,{position:'absolute',inset:'0',width:'100%',height:'100%',pointerEvents:'none'});canvas.parentElement.append(marksCanvas);}
 const w=canvas.clientWidth,h=canvas.clientHeight;if(marksCanvas.width!==w||marksCanvas.height!==h){marksCanvas.width=w;marksCanvas.height=h;}const ctx=marksCanvas.getContext('2d');ctx.clearRect(0,0,w,h);if(!humanLayersEnabled||overlayOpacity<=0)return;
 ctx.globalAlpha=overlayOpacity;drawStateBorders(ctx,w,h);ctx.font='12px Georgia';ctx.textAlign='center';ctx.fillStyle='#161c1f';ctx.strokeStyle='#ffffffa0';ctx.lineWidth=2;
 const r=rotation(),labels=[];for(const mark of filteredSocietyMarks){if(mark.City){if(settlementMarkers===0||settlementMarkers===1&&!mark.Capital||settlementMarkers===3&&!mark.Urban||settlementMarkers===4&&!mark.Large)continue;if(map?mapZoom>=(mark.Capital?.9:mark.Urban?.5:.18):zoom>=(mark.Capital?4:mark.Urban?2.5:1.7))continue;}let x,y;
 if(map){let px=(Math.atan2(mark.Y,mark.X)*2/Math.PI-centerLongitude/90+2)%4;if(px<0)px+=4;px-=2;x=((px-panX)/(mapScale()*w/h)+1)*w/2;y=(1-(Math.asin(mark.Z)*2/Math.PI-panY)/mapScale())*h/2;}
 else{const a=mark.X,b=mark.Z,c=-mark.Y,px=r[0]*a+r[3]*b+r[6]*c,py=r[1]*a+r[4]*b+r[7]*c,pz=r[2]*a+r[5]*b+r[8]*c;if(pz*zoom<=1)continue;x=(px*2.4/(w/h)/(zoom-pz)+1)*w/2;y=(1-py*2.4/(zoom-pz))*h/2;}
 if(x<0||x>w||y<0||y>h)continue;if(mark.City){const radius=mark.Capital?4:mark.Urban?3:2;ctx.beginPath();ctx.arc(x,y,radius,0,2*Math.PI);ctx.fillStyle=mark.Capital?'#ffda64':'#f2f2e8';ctx.strokeStyle='#172b35';ctx.lineWidth=1;ctx.fill();ctx.stroke();ctx.fillStyle='#161c1f';ctx.strokeStyle='#ffffffa0';ctx.lineWidth=2;y+=radius+10;if(map?mapZoom>(mark.Capital?.32:.16):zoom>(mark.Capital?1.9:1.55))continue;}else y-=12;
 const half=ctx.measureText(mark.Name).width/2+5;if(labels.some(l=>Math.abs(l.y-y)<17&&Math.abs(l.x-x)<l.half+half))continue;labels.push({x,y,half});ctx.strokeText(mark.Name,x,y);ctx.fillText(mark.Name,x,y);
 }
}
function drawStateBorders(ctx,w,h){
 const r=rotation();ctx.strokeStyle='#171b1f';ctx.lineWidth=.8;ctx.beginPath();
 function point(at){const a=societyBorders[at],b=societyBorders[at+1],c=societyBorders[at+2];
  if(map){let px=(Math.atan2(b,a)*2/Math.PI-centerLongitude/90+2)%4;if(px<0)px+=4;return {x:((px-2-panX)/(mapScale()*w/h)+1)*w/2,y:(1-(Math.asin(Math.max(-1,Math.min(1,c)))*2/Math.PI-panY)/mapScale())*h/2,pole:Math.abs(c)>.99999};}
  const px=r[0]*a+r[3]*c-r[6]*b,py=r[1]*a+r[4]*c-r[7]*b,pz=r[2]*a+r[5]*c-r[8]*b;if(pz*zoom<=1)return null;return{x:(px*2.4/(w/h)/(zoom-pz)+1)*w/2,y:(1-py*2.4/(zoom-pz))*h/2};
 }
 for(let i=0;i<societyBorders.length;i+=6){const a=point(i),b=point(i+3);if(!a||!b)continue;
  if(map){const span=2*h/mapScale();if(a.pole)a.x=b.x;if(b.pole)b.x=a.x;if(b.x-a.x>span/2)b.x-=span;if(b.x-a.x< -span/2)b.x+=span;for(const copy of [-1,0,1]){ctx.moveTo(a.x+copy*span,a.y);ctx.lineTo(b.x+copy*span,b.y);}}
  else{ctx.moveTo(a.x,a.y);ctx.lineTo(b.x,b.y);}
 }ctx.stroke();
}
function pick(e){
 if(!readyWorker||displayWorker!==readyWorker)return;const box=canvas.getBoundingClientRect(),mx=(e.clientX-box.left)/box.width*2-1,my=1-(e.clientY-box.top)/box.height*2;
 let x,y,z;
 if(map){const px=mx*mapScale()*box.width/box.height+panX,py=my*mapScale()+panY;if(Math.abs(px)>2||Math.abs(py)>1)return;const lon=px*Math.PI/2+centerLongitude*Math.PI/180,lat=py*Math.PI/2;x=Math.cos(lat)*Math.cos(lon);y=Math.cos(lat)*Math.sin(lon);z=Math.sin(lat);}
 else{
  let dx=mx*(box.width/box.height)/2.4,dy=my/2.4,dz=-1,len=Math.hypot(dx,dy,dz);dx/=len;dy/=len;dz/=len;
  const b=zoom*dz,disc=b*b-zoom*zoom+1;if(disc<0)return;const t=-b-Math.sqrt(disc),px=dx*t,py=dy*t,pz=zoom+dz*t,r=rotation();
  x=r[0]*px+r[1]*py+r[2]*pz;z=r[3]*px+r[4]*py+r[5]*pz;y=-(r[6]*px+r[7]*py+r[8]*pz);
 }
 readyWorker.postMessage({type:'point',x,y,z,people:humanLayersEnabled});
}
export function pickCell(cell){readyWorker?.postMessage({type:'pick',cell,people:humanLayersEnabled});}
export function view(options,replaceWorld=false){
 if(paletteRequest){paletteRequest.resolve();paletteRequest=null;}
 const cached=JSON.parse(options);
 if(!replaceWorld && chunks.length && cached.Projection===currentOptions.Projection){
  displayWorker?.postMessage({type:'select-layer',layer:cached.Layer});
  if(chunks.some(c=>c.texture&&c.layer===cached.Layer)){currentOptions=cached;if(viewResolve)pendingOptions={...pendingOptions,Layer:cached.Layer,GridSpacing:cached.GridSpacing,ShowRivers:cached.ShowRivers,Wireframe:cached.Wireframe};revision++;return Promise.resolve();}
  return new Promise((resolve,reject)=>{paletteRequest={options:cached,resolve,reject,source:readyWorker};readyWorker.postMessage({type:'palette',layer:cached.Layer});});
 }
 cancelView();if(!readyWorker)return Promise.reject(new Error('Generate a world first.'));
 viewReceived=0;const settings=JSON.parse(options),summary=JSON.parse(lastJson),count=summary.CellCount,riverCount=summary.Metrics?.find(m=>m.Name==='hydrology_rivers')?.Value||0;pendingOptions=settings;pendingWorker=readyWorker;viewExpected=4*Math.ceil((count*3-6)/8192)+Math.ceil(riverCount/1024)+4+6;
 const ticket=++viewTicket;return new Promise((resolve,reject)=>{viewResolve=resolve;viewReject=reject;readyWorker.postMessage({type:'cached-view',options,ticket});});
}
function cancelView(){if(paletteRequest){paletteRequest.resolve();paletteRequest=null;}viewTicket++;readyWorker?.postMessage({type:'cancel-view'});incoming=[];free(pending);pending=[];viewDone=false;const resolve=viewResolve;viewResolve=viewReject=null;resolve?.();}
function receive(source,d){
 if(d.type==='lore'){if(peopleRequest?.source===source&&peopleRequest.requestId===d.requestId){peopleRequest.resolve(d.text);peopleRequest=null;}return;}
 if(d.type==='people'){if(source!==readyWorker)return;const summary=JSON.parse(d.json);if(!summary.ClockOnly){societyMarks=summary.Marks||[];filterCityMarkers();societyBorders=summary.Borders||[];const old=chunks.filter(c=>c.texture&&c.layer===100);free(old);chunks=chunks.filter(c=>!old.includes(c));if(d.bytes.length)chunks.push(upload(d.bytes));revision++;}callback.invokeMethodAsync('HumanUpdate',d.json);if(peopleRequest?.source===source&&peopleRequest.requestId===d.requestId){peopleRequest.resolve(d.json);peopleRequest=null;}return;}
 if(d.type==='palette'){if(paletteRequest?.source===source&&d.layer===paletteRequest.options.Layer){const request=paletteRequest;paletteRequest=null;if(d.bytes.length){chunks.push(upload(d.bytes));currentOptions=request.options;revision++;request.resolve();}else request.reject(new Error('Layer is not available yet.'));}return;}
 if(d.type==='view-piece'&&d.ticket===viewTicket)incoming.push(d.bytes);
 else if(d.type==='view-complete'&&d.ticket===viewTicket)viewDone=true;
 else if(d.type==='inspection')callback.invokeMethodAsync('Inspection',d.text);
 else if(d.type==='export'&&d.requestId==='library'){if(libraryExport?.source===source){const request=libraryExport;libraryExport=null;request.resolve(d.data);}}
 else if(d.type==='export'){if(d.kind==='heightmap')downloadBytes('worldgen.heightmap.pgm',d.data,'image/x-portable-graymap');else if(d.kind==='world')downloadBytes('worldgen.world',d.data,'application/octet-stream');else download(d.kind==='csv'?'worldgen.cells.csv':'worldgen.result.json',d.data,d.kind==='csv'?'text/csv':'application/json');}
 else if(d.type==='error'){if(peopleRequest?.source===source){peopleRequest.reject(new Error(d.message));peopleRequest=null;}if(libraryExport?.source===source){libraryExport.reject(new Error(d.message));libraryExport=null;}const reject=viewReject;reject?.(new Error(d.message));cancelView();callback.invokeMethodAsync('Failed',d.message);}
}
export function generate(recipe){
 cancel();const ticket=generation,created=new Worker(new URL('./worker/main.js',import.meta.url),{type:'module'});worker=created;
 isGenerating=true;rollbackWorker=readyWorker;rollbackJson=lastJson;generationView=true;backupChunks=chunks;backupOptions=currentOptions;backupMap=map;
 created.onerror=e=>callback.invokeMethodAsync('Failed',e.message||'Worker failed.');
 created.onmessage=async e=>{
  const d=e.data;if(d.type==='ready'){if(ticket===generation)created.postMessage({recipe});}
  else if(d.type==='progress'){if(ticket===generation)callback.invokeMethodAsync('Progress',d.stage,Math.floor(d.percent*.8));}
  else if(d.type==='preview'&&ticket===generation){showPreview(d.json,ticket);}
  else if(d.type==='complete'&&ticket===generation){isGenerating=false;queuedPreview=null;previewLoading=false;previewTicket++;previewWorker?.terminate();previewWorker=null;generationViewImported=false;readyWorker=created;worker=null;lastJson=d.json;await callback.invokeMethodAsync('Complete',d.json,false);}
  else receive(created,d);
};
}
function showPreview(json,generationTicket){
 if(previewLoading){queuedPreview=json;return;}previewLoading=true;
 const ticket=++previewTicket;previewWorker?.terminate();cancelView();
 const created=new Worker(new URL('./worker/main.js',import.meta.url),{type:'module'});previewWorker=created;
 created.onmessage=async e=>{if(ticket!==previewTicket||generationTicket!==generation)return;const d=e.data;
  if(d.type==='ready')created.postMessage({type:'load-preview',json});
  else if(d.type==='preview-ready'){readyWorker=created;lastJson=d.json;await callback.invokeMethodAsync('Preview',d.json);previewLoading=false;if(queuedPreview&&isGenerating){const next=queuedPreview;queuedPreview=null;showPreview(next,generationTicket);}}
  else receive(created,d);
 };
}
export function cancel(){
 if(peopleRequest){peopleRequest.reject(new Error('Simulation request cancelled.'));peopleRequest=null;}
 isGenerating=false;queuedPreview=null;previewLoading=false;previewTicket++;previewWorker?.terminate();previewWorker=null;
 libraryLoadTicket++;bundledFetch?.abort();bundledFetch=null;
 if(libraryExport){libraryExport.reject(new Error('World save cancelled.'));libraryExport=null;}
 generation++;worker?.terminate();worker=null;cancelView();
 if(generationView){const imported=generationViewImported;readyWorker?.terminate();readyWorker=rollbackWorker;lastJson=rollbackJson;rollbackWorker=null;rollbackJson="";generationView=false;generationViewImported=false;callback?.invokeMethodAsync('Restore',lastJson,imported);}
 if(backupChunks){if(chunks!==backupChunks)free(chunks);chunks=backupChunks;backupChunks=null;currentOptions=backupOptions;map=backupMap;}
 if(currentOptions.Projection!==undefined)callback?.invokeMethodAsync('RestoreView',JSON.stringify(currentOptions));
}
export function exportWorld(kind){readyWorker?.postMessage({type:'export',kind});}
export function listWorlds(){return worldLibrary.list();}
export async function saveLibraryWorld(name,groupId=null){
 if(isGenerating||generationView)throw new Error('Finish creating the world before saving.');
 if(!readyWorker||!lastJson)throw new Error('Generate or open a world first.');
 if(libraryExport)throw new Error('A world save is already in progress.');
 if(!name?.trim()||name.trim().length>80)throw new Error('Enter a world name of 1–80 characters.');
 const summary=JSON.parse(lastJson);
 const data=await new Promise((resolve,reject)=>{libraryExport={resolve,reject,source:readyWorker};readyWorker.postMessage({type:'export',kind:'world',requestId:'library'});});
 return worldLibrary.write(name,data,summary,groupId);
}
export async function deleteLibraryWorld(id,group){if(!confirm(group?'Are you sure? Delete this world and all its saves?':'Are you sure? Delete this save?'))return false;await worldLibrary.remove(id,group);return true;}
export async function openLibraryWorld(id){cancel();const ticket=libraryLoadTicket,data=await worldLibrary.read(id);if(ticket===libraryLoadTicket)loadWorldFile(data);}
export async function openBundledEarth(){
 cancel();const ticket=libraryLoadTicket;bundledFetch=new AbortController();
 const response=await fetch('./world/Earth.world',{signal:bundledFetch.signal});
 if(!response.ok)throw new Error('Bundled Earth is unavailable. Rebuild or reinstall WorldGen to restore the world library.');
 const bytes=new Uint8Array(await response.arrayBuffer());let binary='';
 for(let offset=0;offset<bytes.length;offset+=32768)binary+=String.fromCharCode(...bytes.subarray(offset,offset+32768));
 if(ticket===libraryLoadTicket){bundledFetch=null;loadWorldFile(btoa(binary));}
}
function loadSnapshot(message){
 cancel();const ticket=generation,created=new Worker(new URL('./worker/main.js',import.meta.url),{type:'module'});worker=created;
 created.onerror=e=>callback.invokeMethodAsync('Failed',e.message||'Worker failed.');
 created.onmessage=async e=>{
  const d=e.data;if(d.type==='ready'){if(ticket===generation)created.postMessage(message);}
  else if(d.type==='progress'&&ticket===generation){await callback.invokeMethodAsync('Progress',d.stage,Math.min(79,Math.floor(d.percent*.8)));}
  else if(d.type==='complete'&&ticket===generation){rollbackWorker=readyWorker;rollbackJson=lastJson;generationView=true;generationViewImported=true;readyWorker=created;worker=null;lastJson=d.json;await callback.invokeMethodAsync('Complete',d.json,true);}
  else receive(created,d);
 };
}
export function loadWorld(json){loadSnapshot({type:'load-world',json});}
export function loadWorldFile(data){loadSnapshot({type:'load-world-file',data});}
export function download(name,text,type){save(name,new Blob([text],{type}));}
export function downloadChronicle(text){download('world-chronicle.txt',text,'text/plain;charset=utf-8');}
export function downloadBytes(name,base64,type='application/octet-stream'){save(name,new Blob([Uint8Array.from(atob(base64),c=>c.charCodeAt(0))],{type}));}
function save(name,blob){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export function dispose(){cancel();readyWorker?.terminate();cancelAnimationFrame(frame);free(chunks);}

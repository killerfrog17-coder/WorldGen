let callback, worker, generation=0, canvas, gl, program, positions=[], indices=[], colors=[], lines=[];
let yaw=.6, pitch=-.18, zoom=3.15, dragging=false, moved=false, lastX=0,lastY=0, frame=0, lastJson='';
let pBuffer,cBuffer,iBuffer,lBuffer,nBuffer;
export function snapshot(){ return lastJson; }
export function init(dotnet){
    callback=dotnet; canvas=document.getElementById('globe'); gl=canvas.getContext('webgl',{antialias:true,alpha:true,preserveDrawingBuffer:true});
    if(!gl)throw new Error('WebGL is unavailable in this browser. Enable hardware acceleration or use another browser.');
    if(!gl.getExtension('OES_element_index_uint'))throw new Error('32-bit WebGL indices unavailable.');
    const vertex=`attribute vec3 position; attribute vec3 color; attribute vec3 normal; uniform mat3 rotation; uniform float aspect; uniform float camera; varying vec3 vColor; varying vec3 vNormal; void main(){vec3 p=rotation*position;vNormal=rotation*normal;vColor=color;float z=p.z-camera;gl_Position=vec4(p.x*2.4/aspect,p.y*2.4,-1.002*z-.2002,-z);}`;
    const fragment=`precision mediump float;varying vec3 vColor;varying vec3 vNormal;uniform bool linePass;void main(){if(linePass){gl_FragColor=vec4(.28,.72,.86,1.);return;}vec3 n=normalize(vNormal);float light=.50+.50*max(0.,dot(n,normalize(vec3(-.5,.7,1.))));float rim=pow(1.-max(n.z,0.),3.);gl_FragColor=vec4(vColor*light+vec3(.07,.17,.21)*rim,1.);}`;
    function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(s));return s;}
    program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,vertex));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,fragment));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));
    pBuffer=gl.createBuffer();cBuffer=gl.createBuffer();iBuffer=gl.createBuffer();lBuffer=gl.createBuffer();nBuffer=gl.createBuffer();
    canvas.onpointerdown=e=>{dragging=true;moved=false;lastX=e.clientX;lastY=e.clientY;canvas.setPointerCapture(e.pointerId);};
    canvas.onpointermove=e=>{if(!dragging)return;const dx=e.clientX-lastX,dy=e.clientY-lastY;if(Math.abs(dx)+Math.abs(dy)>2)moved=true;yaw+=dx*.006;pitch=Math.max(-1.5,Math.min(1.5,pitch+dy*.006));lastX=e.clientX;lastY=e.clientY;};
    canvas.onpointerup=e=>{dragging=false;if(!moved)pick(e);};canvas.onpointercancel=()=>dragging=false;
    canvas.onwheel=e=>{e.preventDefault();zoom=Math.max(1.65,Math.min(5,zoom+e.deltaY*.0015));};
    canvas.onkeydown=e=>{if(e.key==='ArrowLeft')yaw-=.1;if(e.key==='ArrowRight')yaw+=.1;if(e.key==='ArrowUp')pitch-=.1;if(e.key==='ArrowDown')pitch+=.1;};
    render();
}
function rotation(){const c=Math.cos(yaw),s=Math.sin(yaw),a=Math.cos(pitch),b=Math.sin(pitch);return new Float32Array([c,b*s,-a*s,0,a,b,s,-b*c,a*c]);}
function render(){
    frame=requestAnimationFrame(render);const scale=Math.min(devicePixelRatio,2),width=Math.round(canvas.clientWidth*scale),height=Math.round(canvas.clientHeight*scale);if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
    gl.viewport(0,0,width,height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);if(!indices.length)return;
    gl.enable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.useProgram(program);gl.uniformMatrix3fv(gl.getUniformLocation(program,'rotation'),false,rotation());gl.uniform1f(gl.getUniformLocation(program,'aspect'),width/height);gl.uniform1f(gl.getUniformLocation(program,'camera'),zoom);gl.uniform1i(gl.getUniformLocation(program,'linePass'),0);
    const p=gl.getAttribLocation(program,'position'),c=gl.getAttribLocation(program,'color'),n=gl.getAttribLocation(program,'normal');gl.bindBuffer(gl.ARRAY_BUFFER,nBuffer);gl.enableVertexAttribArray(n);gl.vertexAttribPointer(n,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,pBuffer);gl.enableVertexAttribArray(p);gl.vertexAttribPointer(p,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,cBuffer);gl.enableVertexAttribArray(c);gl.vertexAttribPointer(c,3,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,iBuffer);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_INT,0);
    if(lines.length){gl.uniform1i(gl.getUniformLocation(program,'linePass'),1);gl.bindBuffer(gl.ARRAY_BUFFER,lBuffer);gl.vertexAttribPointer(p,3,gl.FLOAT,false,0,0);gl.disableVertexAttribArray(c);gl.disableVertexAttribArray(n);gl.vertexAttrib3f(n,0,0,1);gl.vertexAttrib3f(c,.2,.7,.85);gl.drawArrays(gl.LINES,0,lines.length/3);}
}
function pick(e){
    if(!positions.length)return;const box=canvas.getBoundingClientRect(),mx=(e.clientX-box.left)/box.width*2-1,my=1-(e.clientY-box.top)/box.height*2;const r=rotation();let best=.025,id=-1;
    for(let i=0;i<positions.length;i+=3){let x=positions[i],y=positions[i+1],z=positions[i+2];const rx=r[0]*x+r[3]*y+r[6]*z,ry=r[1]*x+r[4]*y+r[7]*z,rz=r[2]*x+r[5]*y+r[8]*z;if(rz<0)continue;const px=rx*2.4/(box.width/box.height)/(zoom-rz),py=ry*2.4/(zoom-rz),d=(px-mx)**2+(py-my)**2;if(d<best){best=d;id=i/3;}}
    if(id>=0)callback.invokeMethodAsync('Pick',id);
}
export function draw(json){const data=JSON.parse(json);positions=new Float32Array(data.Positions);colors=new Float32Array(data.Colors);indices=new Uint32Array(data.Triangles);lines=new Float32Array(data.Lines);const normals=new Float32Array(positions.length);for(let t=0;t<indices.length;t+=3){const a=indices[t]*3,b=indices[t+1]*3,c=indices[t+2]*3;const ux=positions[b]-positions[a],uy=positions[b+1]-positions[a+1],uz=positions[b+2]-positions[a+2],vx=positions[c]-positions[a],vy=positions[c+1]-positions[a+1],vz=positions[c+2]-positions[a+2];let nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;const sign=nx*positions[a]+ny*positions[a+1]+nz*positions[a+2]<0?-1:1;for(const i of[a,b,c]){normals[i]+=nx*sign;normals[i+1]+=ny*sign;normals[i+2]+=nz*sign;}}for(let i=0;i<normals.length;i+=3){const length=Math.hypot(normals[i],normals[i+1],normals[i+2]);normals[i]/=length;normals[i+1]/=length;normals[i+2]/=length;}for(const [buffer,array,target]of[[pBuffer,positions,gl.ARRAY_BUFFER],[nBuffer,normals,gl.ARRAY_BUFFER],[cBuffer,colors,gl.ARRAY_BUFFER],[iBuffer,indices,gl.ELEMENT_ARRAY_BUFFER],[lBuffer,lines,gl.ARRAY_BUFFER]]){gl.bindBuffer(target,buffer);gl.bufferData(target,array,gl.STATIC_DRAW);}}
export function generate(recipe){
    cancel();const ticket=generation;worker=new Worker(new URL('./worker/main.js',import.meta.url),{type:'module'});
    worker.onerror=e=>{if(ticket===generation)callback.invokeMethodAsync('Failed',e.message||'Worker failed to start. Build worker assets with scripts/build.ps1.');};
    worker.onmessage=async e=>{if(ticket!==generation)return;const d=e.data;if(d.type==='ready')worker.postMessage({recipe});else if(d.type==='progress')callback.invokeMethodAsync('Progress',d.stage,d.percent);else if(d.type==='error')callback.invokeMethodAsync('Failed',d.message);else if(d.type==='complete'){lastJson=d.json;await callback.invokeMethodAsync('Complete',d.json);}};
}
export function cancel(){generation++;worker?.terminate();worker=null;}
export function download(name,text,type){const blob=new Blob([text],{type});save(name,blob);}
export function downloadBytes(name,base64){const bytes=Uint8Array.from(atob(base64),c=>c.charCodeAt(0));save(name,new Blob([bytes],{type:'image/x-portable-graymap'}));}
function save(name,blob){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export function dispose(){cancel();cancelAnimationFrame(frame);}

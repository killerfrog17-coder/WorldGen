// Read-only renderer for the overnight review's diagnostic SVG exports.
// Images and sheets stay beside the external review.json, outside source control.
// Usage: node scripts/history-overnight-images.mjs [--timeline|--states-only|--years=Y1,Y2] [--modes=States,Territories] [--set=NAME --bounds=W,S,E,N] [--no-cities] FOLDER...
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {gunzipSync} from 'node:zlib';
const timeline=process.argv.includes('--timeline');
const requestedStatesOnly=process.argv.includes('--states-only');
const noCities=process.argv.includes('--no-cities');
const boundsArg=process.argv.find(arg=>arg.startsWith('--bounds='));
const bounds=boundsArg?boundsArg.slice(9).split(',').map(Number):null;
if(bounds&&(bounds.length!==4||bounds.some(value=>!Number.isFinite(value))||bounds[0]>=bounds[2]||bounds[1]>=bounds[3]||bounds[0]<-180||bounds[2]>180||bounds[1]<-90||bounds[3]>90))throw Error('Bounds must be west,south,east,north in degrees.');
if(bounds&&!process.argv.some(arg=>arg.startsWith('--set=')))throw Error('A bounded crop requires a distinct --set name.');
const yearsArg=process.argv.find(arg=>arg.startsWith('--years='));
const focusYears=yearsArg?yearsArg.slice(8).split(',').map(Number):[];
const setArg=process.argv.find(arg=>arg.startsWith('--set='));
const setName=setArg?setArg.slice(6):'';
const modesArg=process.argv.find(arg=>arg.startsWith('--modes='));
const requestedModes=modesArg?modesArg.slice(8).split(','):[];
if(requestedModes.some(mode=>!['States','Territories','Technology','Cultures','Religions'].includes(mode)))throw Error('Unknown diagnostic overlay mode.');
if(setName&&!/^[a-z0-9-]+$/.test(setName))throw Error('Set name must contain lowercase letters, digits or hyphens.');
if(focusYears.some(year=>!Number.isInteger(year)||year<0))throw Error('Focused years must be nonnegative integers.');
const sequence=timeline||focusYears.length>0;
const folders=process.argv.slice(2).filter(arg=>!['--timeline','--states-only','--no-cities'].includes(arg)&&!arg.startsWith('--years=')&&!arg.startsWith('--set=')&&!arg.startsWith('--modes=')&&!arg.startsWith('--bounds=')).map(folder=>path.resolve(folder));
const browser=await chromium.launch({headless:true,channel:'msedge'});
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
try{
 for(const folder of folders){
  const reportFile=await fs.access(path.join(folder,'review.json')).then(()=> 'review.json',()=> 'episode.json');
  const report=JSON.parse(await fs.readFile(path.join(folder,reportFile),'utf8'));
  const status=await fs.readFile(path.join(folder,'review-status.json'),'utf8').then(JSON.parse,error=>{if(error.code==='ENOENT')return null;throw error;});
  const isEpisode=reportFile==='episode.json',statesOnly=requestedStatesOnly||isEpisode;
  const modes=requestedModes.length?requestedModes:sequence||statesOnly?['States']:['States','Technology','Cultures','Religions'];
  const presentation=(isEpisode?'opaque overlay 1, neutral background 0':'climate background 1, overlay opacity .58')+(noCities?'; settlement marker overlay hidden for border review':'')+(bounds?`; crop W/S/E/N ${bounds.join('/')}`:'');
  const eraNames={4:'Bronze Age',5:'Iron Age',6:'Medieval',7:'Early Modern',8:'Industrial',9:'Modern',10:'Postmodern'};
  const rows=isEpisode?report.Rows.map(row=>({...row,EraName:eraNames[row.Era]??`Era ${row.Era}`,FirstState:-1,States:row.Courts.length,ActiveTowns:null,OldestStateAge:Math.max(0,...row.Courts.map(court=>row.Year-court.FoundedYear)),SponsoredPorts:null})):report.Rows;
  let selection=[];
  for(const row of rows){
   const label=row.FirstState>=0&&row.Year>=row.FirstState&&row.Era<4?'Bronze Age / first states':row.EraName;
   if(!selection.some(value=>value.label===label))selection.push({row,label});
  }
  const last=rows.at(-1);
  if(!selection.some(value=>value.row.Year===last.Year))selection.push({row:last,label:`${last.EraName} / latest mapped`});
  if(sequence)selection=rows.filter(row=>!focusYears.length||focusYears.includes(row.Year)).map(row=>({row,label:row.FirstState>=0&&row.Year>=row.FirstState&&row.Era<4?'Bronze Age / first states':row.EraName}));
  if(focusYears.some(year=>!selection.some(value=>value.row.Year===year)))throw Error('A focused year is absent from review.json.');
  const out=path.join(folder,(focusYears.length?'focused':timeline?'timeline':'screenshots')+(setName?`-${setName}`:'')+(noCities?'-no-cities':''));await fs.mkdir(out,{recursive:true});
  const previous=await fs.readFile(path.join(out,'screenshots.json'),'utf8').then(JSON.parse,error=>{if(error.code==='ENOENT')return null;throw error;});
  const cropCacheMatches=!bounds||JSON.stringify(previous?.bounds)===JSON.stringify(bounds);
  const rendered=[];
  for(const {row,label} of selection){
   for(const mode of modes){
    const source=path.join(folder,`year-${row.Year}`,`${mode}-${row.Year}.svg.gz`);
    const file=`${mode.toLowerCase()}-${row.Year}.png`;
    const sourceStat=await fs.stat(source).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
    let cached=null;
    for(const candidate of [...new Set([path.join(out,file),...(noCities||bounds?[]:['screenshots','focused','focused-late','reference-comparison'].map(dir=>path.join(folder,dir,file)))])]){
     const stat=await fs.stat(candidate).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
     if(stat&&cropCacheMatches&&(!sourceStat||stat.mtimeMs>=sourceStat.mtimeMs)){cached=candidate;break;}
    }
    if(cached){
     if(cached!==path.join(out,file))await fs.copyFile(cached,path.join(out,file));
     rendered.push({year:row.Year,label,mode,file,population:row.Population,states:row.States,towns:row.ActiveTowns,oldestStateAge:row.OldestStateAge,ports:row.SponsoredPorts});
     continue;
    }
    // The bounded overnight harness intentionally exports only States at
    // intermediate/final checkpoints. Other modes remain era-milestone views.
    if(!sourceStat&&mode!=='States'&&row.Year===last.Year)continue;
    if(!sourceStat)throw Error(`Missing SVG and cached PNG: ${source}. Re-export this checkpoint from its frozen saved world.`);
    const svg=gunzipSync(await fs.readFile(source)).toString('utf8');
    const page=await browser.newPage({viewport:{width:1800,height:960}});
    await page.setContent('<style>body{margin:0}svg{display:block}</style>'+svg,{timeout:120000,waitUntil:'load'});
    if(noCities)await page.addStyleTag({content:'circle{display:none}'});
    // HistoryAudit's equirectangular diagnostic export uses x=900+5*lon,
    // y=510-5*lat. Reframe existing paths only; source SVG stays untouched.
    if(bounds)await page.evaluate(([west,south,east,north])=>{
     const svg=document.querySelector('svg');
     svg.setAttribute('viewBox',`${900+5*west} ${510-5*north} ${5*(east-west)} ${5*(north-south)}`);
     svg.setAttribute('width','1800');svg.setAttribute('height','960');
    },bounds);
    await page.evaluate(()=>{
     const occupied=[];
     for(const label of document.querySelectorAll('text[text-anchor="middle"]')){
      const r=label.getBBox(),box={x:r.x-3,y:r.y-2,width:r.width+6,height:r.height+4};
      if(occupied.some(p=>box.x<p.x+p.width&&box.x+box.width>p.x&&box.y<p.y+p.height&&box.y+box.height>p.y))label.style.display='none';else occupied.push(box);
     }
    });
    await page.screenshot({path:path.join(out,file)});await page.close();
    rendered.push({year:row.Year,label,mode,file,population:row.Population,states:row.States,towns:row.ActiveTowns,oldestStateAge:row.OldestStateAge,ports:row.SponsoredPorts});
   }
  }
  for(const mode of modes){
   const modeFrames=rendered.filter(value=>value.mode===mode);
   for(let start=0;start<modeFrames.length;start+=sequence?6:modeFrames.length){
   const cards=modeFrames.slice(start,start+(sequence?6:modeFrames.length)).map(value=>`<article><h2>${escape(value.label)} · elapsed ${value.year}</h2><p>${value.states} states · ${value.towns===null?'town census not exported':`${value.towns} active towns`} · ${(value.population/1e6).toFixed(1)}M people · oldest state ${value.oldestStateAge}y${value.ports===null?'':` · ${value.ports} sponsored ports`}</p><a href="${value.file}"><img src="${value.file}"></a></article>`).join('');
   const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#10202b;color:#fff;font:16px sans-serif}h1{font-size:22px;margin:8px}.warning{background:#6b210a;color:#fff1b5;padding:12px;font-size:26px;font-weight:bold}main{display:grid;grid-template-columns:repeat(2,1800px);gap:8px}h2{font-size:22px;margin:6px}p{margin:6px}img{display:block;width:1800px}</style><h1>${escape(path.basename(folder))}: ${mode}. Frozen diagnostic export; ${presentation}. First-state rows classified Bronze minimum.</h1>${status?.Warning?`<div class="warning">${escape(status.Warning)}</div>`:''}<main>${cards}</main>`;
   const file=path.join(out,`${mode.toLowerCase()}-${sequence?String(start).padStart(3,'0'):'overview'}.html`);await fs.writeFile(file,html);
   const page=await browser.newPage({viewport:{width:3608,height:1200}});await page.goto(pathToFileURL(file).href,{waitUntil:'load'});await page.screenshot({path:file.replace('.html','.png'),fullPage:true});await page.close();
   }
  }
  await fs.writeFile(path.join(out,'screenshots.json'),JSON.stringify({source:path.join(folder,reportFile),settlementMarkers:!noCities,bounds,selection:rendered},null,2));
  console.log(`${path.basename(folder)}: ${rendered.length} images, ${sequence?'six-frame chronological sheets':statesOnly?'States overview':'4 mode sheets'}`);
 }
}finally{await browser.close();}

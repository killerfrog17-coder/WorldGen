// Read-only renderer for the overnight review's diagnostic SVG exports.
// Images and sheets stay beside the external review.json, outside source control.
// Usage: node scripts/history-overnight-images.mjs [--timeline|--states-only|--years=Y1,Y2] [--set=NAME] FOLDER...
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {gunzipSync} from 'node:zlib';
const timeline=process.argv.includes('--timeline');
const statesOnly=process.argv.includes('--states-only');
const yearsArg=process.argv.find(arg=>arg.startsWith('--years='));
const focusYears=yearsArg?yearsArg.slice(8).split(',').map(Number):[];
const setArg=process.argv.find(arg=>arg.startsWith('--set='));
const setName=setArg?setArg.slice(6):'';
if(setName&&!/^[a-z0-9-]+$/.test(setName))throw Error('Set name must contain lowercase letters, digits or hyphens.');
if(focusYears.some(year=>!Number.isInteger(year)||year<0))throw Error('Focused years must be nonnegative integers.');
const sequence=timeline||focusYears.length>0;
const folders=process.argv.slice(2).filter(arg=>!['--timeline','--states-only'].includes(arg)&&!arg.startsWith('--years=')&&!arg.startsWith('--set=')).map(folder=>path.resolve(folder));
const browser=await chromium.launch({headless:true,channel:'msedge'});
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
try{
 for(const folder of folders){
  const report=JSON.parse(await fs.readFile(path.join(folder,'review.json'),'utf8'));
  const rows=report.Rows;
  let selection=[];
  for(const row of rows){
   const label=row.FirstState>=0&&row.Year>=row.FirstState&&row.Era<4?'Bronze Age / first states':row.EraName;
   if(!selection.some(value=>value.label===label))selection.push({row,label});
  }
  const last=rows.at(-1);
  if(!selection.some(value=>value.row.Year===last.Year))selection.push({row:last,label:`${last.EraName} / final`});
  if(sequence)selection=rows.filter(row=>!focusYears.length||focusYears.includes(row.Year)).map(row=>({row,label:row.FirstState>=0&&row.Year>=row.FirstState&&row.Era<4?'Bronze Age / first states':row.EraName}));
  if(focusYears.some(year=>!selection.some(value=>value.row.Year===year)))throw Error('A focused year is absent from review.json.');
  const out=path.join(folder,(focusYears.length?'focused':timeline?'timeline':'screenshots')+(setName?`-${setName}`:''));await fs.mkdir(out,{recursive:true});
  const rendered=[];
  for(const {row,label} of selection){
   for(const mode of sequence||statesOnly?['States']:['States','Technology','Cultures','Religions']){
    const source=path.join(folder,`year-${row.Year}`,`${mode}-${row.Year}.svg.gz`);
    if(!await fs.access(source).then(()=>true,()=>false))continue;
    const file=`${mode.toLowerCase()}-${row.Year}.png`;
    const cached=path.join(folder,'screenshots',file);
    if(sequence&&await fs.access(cached).then(()=>true,()=>false)){
     await fs.copyFile(cached,path.join(out,file));
     rendered.push({year:row.Year,label,mode,file,population:row.Population,states:row.States,towns:row.ActiveTowns,oldestStateAge:row.OldestStateAge,ports:row.SponsoredPorts});
     continue;
    }
    const svg=gunzipSync(await fs.readFile(source)).toString('utf8');
    const page=await browser.newPage({viewport:{width:1800,height:960}});
    await page.setContent('<style>body{margin:0}svg{display:block}</style>'+svg,{timeout:120000,waitUntil:'load'});
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
  for(const mode of sequence||statesOnly?['States']:['States','Technology','Cultures','Religions']){
   const modeFrames=rendered.filter(value=>value.mode===mode);
   for(let start=0;start<modeFrames.length;start+=sequence?6:modeFrames.length){
   const cards=modeFrames.slice(start,start+(sequence?6:modeFrames.length)).map(value=>`<article><h2>${escape(value.label)} · elapsed ${value.year}</h2><p>${value.states} states · ${value.towns} active towns · ${(value.population/1e6).toFixed(1)}M people · oldest state ${value.oldestStateAge}y · ${value.ports} sponsored ports</p><a href="${value.file}"><img src="${value.file}"></a></article>`).join('');
   const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#10202b;color:#fff;font:16px sans-serif}h1{font-size:22px;margin:8px}main{display:grid;grid-template-columns:repeat(2,1800px);gap:8px}h2{font-size:22px;margin:6px}p{margin:6px}img{display:block;width:1800px}</style><h1>${escape(path.basename(folder))}: ${mode}. Frozen diagnostic export; climate background, overlay opacity .58. First-state rows classified Bronze minimum.</h1><main>${cards}</main>`;
   const file=path.join(out,`${mode.toLowerCase()}-${sequence?String(start).padStart(3,'0'):'overview'}.html`);await fs.writeFile(file,html);
   const page=await browser.newPage({viewport:{width:3608,height:1200}});await page.goto(pathToFileURL(file).href,{waitUntil:'load'});await page.screenshot({path:file.replace('.html','.png'),fullPage:true});await page.close();
   }
  }
  await fs.writeFile(path.join(out,'screenshots.json'),JSON.stringify({source:path.join(folder,'review.json'),selection:rendered},null,2));
  console.log(`${path.basename(folder)}: ${rendered.length} images, ${sequence?'six-frame chronological sheets':statesOnly?'States overview':'4 mode sheets'}`);
 }
}finally{await browser.close();}

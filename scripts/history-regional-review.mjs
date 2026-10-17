// Render a geographic window directly from diagnostic SVG geometry, not an
// AI-redrawn map or a resized bitmap. Outputs remain in the external artifact.
// Usage: node scripts/history-regional-review.mjs --bounds=W,S,E,N --years=Y1,Y2 --set=NAME FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
import {gunzipSync} from 'node:zlib';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const args=process.argv.slice(2);
const bounds=args.find(x=>x.startsWith('--bounds='))?.slice(9).split(',').map(Number);
const years=args.find(x=>x.startsWith('--years='))?.slice(8).split(',').map(Number);
const name=args.find(x=>x.startsWith('--set='))?.slice(6)??'regional';
if(!bounds||bounds.length!==4||bounds.some(x=>!Number.isFinite(x))||bounds[0]<-180||bounds[2]>180||bounds[1]<-90||bounds[3]>90||bounds[0]>=bounds[2]||bounds[1]>=bounds[3])throw Error('Use ordered W,S,E,N bounds without dateline wrapping.');
if(!years||years.length<1||years.length>12||years.some(x=>!Number.isInteger(x)||x<0)||new Set(years).size!==years.length)throw Error('Choose1-12 distinct checkpoint years.');
if(!/^[a-z0-9-]+$/.test(name))throw Error('Invalid output set name.');
const input=args.find(x=>!x.startsWith('--'));if(!input)throw Error('Artifact folder required.');
const folder=path.resolve(input),episode=await fs.access(path.join(folder,'review.json')).then(()=>false,()=>true);
const source=path.join(folder,episode?'episode.json':'review.json');
const report=JSON.parse(await fs.readFile(source,'utf8'));
const rows=years.map(year=>{const row=report.Rows.find(r=>r.Year===year);if(!row)throw Error(`Missing checkpoint${year}`);return row;});
const out=path.join(folder,`regional-${name}`);await fs.mkdir(out,{recursive:true});
const [west,south,east,north]=bounds,viewBox=[900+west*5,510-north*5,(east-west)*5,(north-south)*5];
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const browser=await chromium.launch({headless:true,channel:'msedge'}),images=[];
try{
 for(const row of rows){
  const file=path.join(folder,`year-${row.Year}`,`States-${row.Year}.svg.gz`);
  const svg=gunzipSync(await fs.readFile(file)).toString('utf8');
  const page=await browser.newPage({viewport:{width:1200,height:1000}});
  await page.setContent('<style>body{margin:0;background:#10202b}svg{display:block}</style>'+svg,{timeout:120000});
  await page.evaluate(({box})=>{const svg=document.querySelector('svg');svg.setAttribute('viewBox',box.join(' '));svg.setAttribute('width','1200');svg.setAttribute('height','1000');},{box:viewBox});
  const image=`states-${row.Year}.png`;await page.screenshot({path:path.join(out,image)});await page.close();
  images.push({row,image,states:row.States??row.Courts.length,towns:row.ActiveTowns??row.Towns?.length??null});
 }
 for(let first=0;first<images.length;first+=6){
  const cards=images.slice(first,first+6).map(({row,image,states,towns})=>`<article><h2>Year${row.Year} · ${states} active courts · ${towns??'unexported'} active towns globally</h2><p>${escape(row.Courts?row.Courts.map(c=>`${c.Id}:${c.Name}`).join(' | '):row.EraName)}</p><img src="${image}"></article>`).join('');
  const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#10202b;color:white;font:18px Arial}h1{font-size:23px;margin:8px}header p{margin:8px}main{display:grid;grid-template-columns:1200px 1200px;gap:8px}article{width:1200px}h2{font-size:23px;margin:8px}article p{margin:8px;min-height:25px}img{display:block;width:1200px;height:1000px}</style><header><h1>${escape(path.basename(folder))}: actual States geometry, ${west}..${east}°E / ${south}..${north}°N</h1><p>Direct SVG viewBox window; no recoloring, border edits or marker normalization. Fill ≠ one court; outlines may also show pre-state cultures. White markers require≥800 residents. Counts above are global.</p></header><main>${cards}</main>`;
  const stem=`states-${String(first).padStart(3,'0')}`,file=path.join(out,stem+'.html');await fs.writeFile(file,html);
  const page=await browser.newPage({viewport:{width:2408,height:1200}});await page.goto(pathToFileURL(file).href);await page.locator('img').evaluateAll(images=>Promise.all(images.map(i=>i.decode())));await page.screenshot({path:path.join(out,stem+'.png'),fullPage:true});await page.close();
 }
 await fs.writeFile(path.join(out,'index.json'),JSON.stringify({source,bounds,viewBox,selection:images.map(({row,image,states,towns})=>({year:row.Year,image,states,towns,hash:row.Hash??null}))},null,2));
 console.log(`${path.basename(folder)}: ${images.length} direct regional SVG rasterizations`);
}finally{await browser.close();}

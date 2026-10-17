// Compose existing native Auto captures; never invent or rerender their maps.
// Usage: node scripts/history-native-review.mjs [--years=Y1,Y2] [--set=NAME] NATIVE_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const yearsArg=process.argv.find(x=>x.startsWith('--years='));
const years=yearsArg?yearsArg.slice(8).split(',').map(Number):[];
if(years.some(x=>!Number.isInteger(x)||x<0))throw Error('Invalid observation year.');
const set=process.argv.find(x=>x.startsWith('--set='))?.slice(6)??'overview';
if(!/^[a-z0-9-]+$/.test(set))throw Error('Invalid sheet name.');
const folder=path.resolve(process.argv.slice(2).find(x=>!x.startsWith('--'))??'');
const raw=await fs.readFile(path.join(folder,'observations.jsonl'),'utf8');
const lines=raw.trimEnd().split(/\r?\n/),rows=[];
for(let i=0;i<lines.length;i++){
  try{rows.push(JSON.parse(lines[i]));}
  catch(error){if(i!==lines.length-1||raw.endsWith('\n'))throw error;console.warn('Skipped incomplete final live observation.');}
}
if(!rows.length)throw Error('No complete native observations.');
let selection=years.length?rows.filter(r=>years.includes(r.Year)):rows.filter((r,i)=>!rows.slice(0,i).some(other=>other.Era===r.Era));
if(!years.length)for(const row of rows.slice(-3))if(!selection.includes(row))selection.push(row);
if(years.some(year=>!selection.some(r=>r.Year===year)))throw Error('Requested native observation absent.');
const out=path.join(folder,`native-sheets-${set}`);await fs.mkdir(out,{recursive:true});
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const index=[];
try{
 for(let start=0;start<selection.length;start+=6){
  const group=selection.slice(start,start+6),cards=[];
  for(const row of group){
   if(path.basename(row.Image)!==row.Image)throw Error('Unexpected native image path.');
   const source=path.join(folder,`${row.Image}-map.png`);await fs.access(source);
   cards.push(`<article><h2>Native Auto year ${row.Year} · observer era ${row.Era}</h2><p>${row.States} active courts · ${row.Towns} active towns · ${(row.Population/1e6).toFixed(2)}M people · wall ${row.WallSeconds.toFixed(1)}s</p><img src="${pathToFileURL(source).href}"><small>Checkpoint ${escape(row.Hash)}</small></article>`);
  }
  const name=`states-${String(start).padStart(3,'0')}`;
  const html=`<!doctype html><meta charset="utf-8"><style>*{box-sizing:border-box}body{margin:0;background:#101f29;color:#eff6ff;font:17px Arial}header{padding:10px}header p{margin:5px 0}main{display:grid;grid-template-columns:900px 900px}article{width:900px;padding:4px}h2{font-size:19px;margin:5px 0}p{margin:4px 0;font-size:15px}img{display:block;width:892px;height:557.5px}small{font-size:10px}</style><header><b>${escape(path.basename(folder))}: existing native camera captures</b><p>Camera-only map, opaque States over neutral background. Native HUD capture is separate; icons/labels may be IMGUI-only.</p><p>Tint ≠ one state. Outlines may include pre-state cultures. Counts come from the same observation JSONL, not image interpretation.</p></header><main>${cards.join('')}</main>`;
  const file=path.join(out,`${name}.html`);await fs.writeFile(file,html);
  const page=await browser.newPage({viewport:{width:1800,height:1000}});await page.goto(pathToFileURL(file).href);await page.locator('img').evaluateAll(images=>Promise.all(images.map(i=>i.decode())));
  await page.screenshot({path:path.join(out,`${name}.png`),fullPage:true});await page.close();index.push({sheet:`${name}.png`,rows:group});
 }
 await fs.writeFile(path.join(out,'observations-index.json'),JSON.stringify({source:path.join(folder,'observations.jsonl'),lastObservedYear:rows.at(-1).Year,sheets:index},null,2));
 console.log(`${path.basename(folder)}: ${selection.length} existing native maps, ${index.length} sheets`);
}finally{await browser.close();}

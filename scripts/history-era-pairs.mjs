// Pure presentation: pair actual cached PNGs by observed era, not calendar year.
// Usage: node scripts/history-era-pairs.mjs CONTROL_FOLDER TRIAL_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const folders=process.argv.slice(2).map(value=>path.resolve(value));
if(folders.length!==2)throw Error('Control and trial review folders are required.');
const reports=await Promise.all(folders.map(async folder=>JSON.parse(await fs.readFile(path.join(folder,'review.json'),'utf8'))));
const eras=[['neolithic',3],['bronze',4],['iron',5],['medieval',6],['early-modern',7],['industrial',8],['modern',9],['postmodern',10],['latest-mapped',null]];
const escape=value=>String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const out=path.join(folders[1],'era-comparison');await fs.mkdir(out,{recursive:true});
const entries=[];
try{
for(const [name,era] of eras){
  const panels=[];
  for(let index=0;index<2;index++){
    const folder=folders[index],report=reports[index];
    const row=era===null?report.Rows.at(-1):report.Rows.find(row=>(row.FirstState>=0&&row.Year>=row.FirstState&&row.Era<4?4:row.Era)===era);
    if(!row){panels.push({folder,row:null,image:null});continue;}
    const filename=`states-${row.Year}.png`;
    let image=null;
    for(const subfolder of ['screenshots','reference-comparison','focused-early-v21']){
      const candidate=path.join(folder,subfolder,filename);
      if(await fs.access(candidate).then(()=>true,()=>false)){image=candidate;break;}
    }
    if(!image&&era===null){panels.push({folder,row:null,image:null,reason:`Latest mapped checkpoint ${row.Year} has not been rasterized yet; the active replay may still be evolving.`});continue;}
    if(!image)throw Error(`Render actual States checkpoint ${row.Year} in ${folder} first.`);
    panels.push({folder,row,image});
  }
  const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#10202b;color:white;font:24px sans-serif}h1{font-size:30px;margin:12px}p{margin:10px}main{display:grid;grid-template-columns:1800px 1800px;gap:8px}h2{font-size:27px;margin:10px}img{display:block;width:1800px}.missing{height:960px;background:#283640;padding:24px}</style><h1>Observed era comparison: ${escape(name)}</h1><p>Matched terrain/fresh starts; era-relative, not equal-year or identical-history targets. Climate tint and ruling-culture fill do not uniquely identify countries. Missing era images are not invented.</p><main>${panels.map(panel=>`<article><h2>${escape(path.basename(panel.folder))}</h2>${panel.row?`<p>${escape(panel.row.EraName)} / ${panel.row.Year}; ${panel.row.FirstState<0?'before first state':`${panel.row.Year-panel.row.FirstState}y after first state`}; ${panel.row.ActiveTowns} towns / ${panel.row.States} courts / ${(panel.row.Population/1e6).toFixed(1)}M population</p><img src="${pathToFileURL(panel.image).href}">`:`<div class="missing">${escape(panel.reason??'No saved checkpoint observed in this era. This does not imply the capability package was skipped.')}</div>`}</article>`).join('')}</main>`;
  const file=path.join(out,`${name}.html`);await fs.writeFile(file,html);
  const page=await browser.newPage({viewport:{width:3608,height:1200}});await page.goto(pathToFileURL(file).href,{waitUntil:'load'});
  await page.screenshot({path:file.replace('.html','.png'),fullPage:true});await page.close();
  entries.push({era:name,panels,comparison:file.replace('.html','.png')});
}
await fs.writeFile(path.join(out,'comparisons.json'),JSON.stringify({control:folders[0],trial:folders[1],entries},null,2));
console.log(`${path.basename(folders[1])}: ${entries.length} actual era-pair panels, explicit absent-checkpoint notices`);
}finally{await browser.close();}

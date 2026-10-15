// Render the actual saved audit maps at comparable historical milestones.
// These are diagnostics, not additional simulation or geography generation.
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright';
const folder=process.argv[2];
if(!folder)throw Error('Usage: node scripts/history-era-images.mjs <audit-folder>');
const rows=(await fs.readFile(path.join(folder,'eras.csv'),'utf8')).trim().split(/\r?\n/).slice(1).map(line=>{const at=line.lastIndexOf(',');return [line.slice(0,at),Number(line.slice(at+1))];});
const eras=new Map(rows);
const files=(await fs.readdir(folder)).filter(f=>/^States-\d+\.svg$/.test(f)).map(f=>({file:f,year:Number(f.match(/\d+/)[0])})).sort((a,b)=>a.year-b.year);
const stages=['Cultivation','First state','Iron working','Audit end'];
const escape=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
let cards='';
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
for(const stage of stages){
 const year=eras.get(stage),item=year>=0?files.find(f=>f.year>=year):null;
 if(!item){cards+=`<section><h2>${escape(stage)} — not reached</h2><p>No qualifying milestone before the audit safety limit.</p></section>`;continue;}
 // Rasterize one diagnostic at a time. Four embedded maximum-detail SVGs
 // multiply DOM/geometry memory and can kill an otherwise valid audit review.
 const snapshot=await browser.newPage({viewport:{width:1800,height:960}});
 let raster;
 try{await snapshot.goto(pathToFileURL(path.resolve(folder,item.file)).href,{timeout:120000});raster=await snapshot.screenshot();}
 finally{await snapshot.close();}
 const ceiling=eras.get('Medieval capstones in one culture');
 const label=stage==='Audit end'?(ceiling>=0?`Post-ceiling review · ${year-ceiling} years after medieval capstones`:`Safety-limit review · year ${year.toLocaleString('en-US')} · medieval ceiling not reached`):`${stage} · first year ${year.toLocaleString('en-US')}`;
 cards+=`<section><h2>${escape(label)}</h2><img src="data:image/png;base64,${raster.toString('base64')}"/></section>`;
}
 const page=await browser.newPage({viewport:{width:1800,height:1050},deviceScaleFactor:1});
 await page.setContent(`<html><head><style>body{margin:0;background:#091922;color:#e8ece8;font:16px system-ui}main{display:grid;grid-template-columns:1fr 1fr;gap:8px;padding:8px}section{background:#10283a;height:508px}h2{font-size:17px;font-weight:500;padding:9px 12px;margin:0}img{width:100%;height:auto;display:block}p{padding:20px}</style></head><body><main>${cards}</main></body></html>`);
 await page.evaluate(()=>Promise.all([...document.images].map(i=>i.decode())));
 await page.screenshot({path:path.join(folder,'era-storyboard.png')});
 console.log('Saved '+path.join(folder,'era-storyboard.png'));
}finally{await browser.close();}

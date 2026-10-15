import { chromium } from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { gunzipSync } from 'node:zlib';

const folder=path.resolve(process.argv[2]??'');
const report=JSON.parse(await fs.readFile(path.join(folder,'era-review.json'),'utf8'));
if(!report.Verified)throw Error('Only a hash-verified era replay may be rendered.');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 for(const era of report.Eras)for(const mode of ['States','Technology']) {
  const svg=path.join(folder,`${mode}-${era.Year}.svg.gz`);
  if(!await fs.access(svg).then(()=>true,()=>false))continue;
  const page=await browser.newPage({viewport:{width:1800,height:960}});
  await page.setContent(gunzipSync(await fs.readFile(svg)).toString('utf8'),{timeout:120000,waitUntil:'load'});
  // Match the interactive viewers' collision rejection. The diagnostic SVG
  // retains every candidate label; a world-scale screenshot should not stack
  // eighty names on top of one another and misrepresent the actual interface.
  await page.evaluate(()=>{
   const occupied=[];
   for(const label of document.querySelectorAll('text[text-anchor="middle"]')){
    const r=label.getBBox(),box={x:r.x-3,y:r.y-2,width:r.width+6,height:r.height+4};
    if(occupied.some(p=>box.x<p.x+p.width&&box.x+box.width>p.x&&box.y<p.y+p.height&&box.y+box.height>p.y))label.style.display='none';
    else occupied.push(box);
   }
  });
  await page.screenshot({path:path.join(folder,`${mode.toLowerCase()}-${era.Year}.png`)});
  await page.close();
  process.stdout.write(`${era.Name}: ${mode} ${era.Year}\n`);
 }
 const cards=report.Eras.map(era=>`<article><h2>${era.Name}</h2><p>Elapsed ${era.Year.toLocaleString('en-US')} years · ${era.States} states · ${(era.Population/1e6).toFixed(1)}M people</p><img src="${pathToFileURL(path.join(folder,`states-${era.Year}.png`)).href}"></article>`).join('');
 const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#101b24;color:#e0e8ed;font:16px sans-serif}main{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;padding:12px}article{background:#20313d}h2{font-size:19px;margin:8px 12px 0}p{font-size:13px;margin:5px 12px 0;color:#b9c8d3}img{width:100%;display:block}</style><main>${cards}</main>`;
 const overview=path.join(folder,'era-overview.html');await fs.writeFile(overview,html);
 const page=await browser.newPage({viewport:{width:1800,height:1200}});
 await page.goto(pathToFileURL(overview).href,{waitUntil:'load'});await page.screenshot({path:path.join(folder,'era-overview.png'),fullPage:true});await page.close();
} finally {await browser.close();}

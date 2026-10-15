// Research-only contact sheets of locally acquired video frames. Never staged
// as game assets or source-controlled; attribution remains visible in the sheet.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const folder=path.resolve(process.argv[2]);
const frames=(await fs.readdir(folder)).filter(name=>/^frame-\d+\.jpg$/.test(name)).sort();
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 for(let start=0;start<frames.length;start+=12){
  const cards=frames.slice(start,start+12).map((name,index)=>`<article><p>${Math.floor((start+index)*15/60)}:${String((start+index)*15%60).padStart(2,'0')}</p><img src="${pathToFileURL(path.join(folder,name))}"></article>`).join('');
  const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#111b24;color:#fff;font:14px sans-serif}h1{font-size:18px}main{display:grid;grid-template-columns:repeat(3,1fr);gap:6px}p{margin:3px}img{width:100%;display:block}</style><h1>Ollie Bye — The History of the World: Every Year. Research reference; 15-second samples.</h1><main>${cards}</main>`;
  const file=path.join(folder,`sheet-${String(start).padStart(3,'0')}.html`);await fs.writeFile(file,html);
  const page=await browser.newPage({viewport:{width:1800,height:1600}});await page.goto(pathToFileURL(file).href);await page.screenshot({path:file.replace('.html','.png'),fullPage:true});await page.close();
 }
}finally{await browser.close();}

// Rasterize immutable offline political-identity SVGs; no simulation calculations.
// Usage: node scripts/history-political-identity-images.mjs POLITICAL_IDENTITY_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';
const folder=path.resolve(process.argv[2]??'');
if(!process.argv[2])throw Error('Political identity folder required.');
const evidence=JSON.parse(await fs.readFile(path.join(folder,'identities.json'),'utf8'));
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
  for(const name of ['controllers','sovereigns']){
    const page=await browser.newPage({viewport:{width:1800,height:1120}});
    await page.setContent('<style>body{margin:0}svg{display:block}</style>'+await fs.readFile(path.join(folder,name+'.svg'),'utf8'),{waitUntil:'load',timeout:120000});
    await page.screenshot({path:path.join(folder,name+'.png')});await page.close();
  }
  console.log(`Rendered actual saved political identities at ${evidence.Year}: ${folder}`);
}finally{await browser.close();}

import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { gunzipSync } from 'node:zlib';
const folder=process.argv[2],year=process.argv[3];
if(!folder||!year)throw Error('Usage: node scripts/history-snapshot-images.mjs <snapshot-folder> <year>');
const compact=process.argv.includes('--compact'),root=path.resolve('artifacts')+path.sep;
if(compact&&(!path.resolve(folder).startsWith(root)||!/^\d+$/.test(year)))throw Error('Compaction is restricted to numbered generated snapshots inside artifacts.');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1800,height:960}});
 for(const mode of ['States','Religions','Technology',...(compact?['Cultures']:[])]) {
  const plain=path.resolve(folder,`${mode}-${year}.svg`),packed=plain+'.gz';
  if(await fs.access(plain).then(()=>true,()=>false))await page.goto(pathToFileURL(plain).href,{timeout:120000});
  else if(await fs.access(packed).then(()=>true,()=>false))await page.setContent(gunzipSync(await fs.readFile(packed)).toString('utf8'),{timeout:120000,waitUntil:'load'});
  else continue;
  const dated=path.join(folder,`${mode.toLowerCase()}-${year}.png`);
  await page.screenshot({path:dated});
  await fs.copyFile(dated,path.join(folder,`${mode.toLowerCase()}.png`));
  // Only these exact generated exports are removed, after a successful PNG.
  // The saved world and diagnostic tables remain available for reconstruction.
  if(compact){await fs.unlink(plain).catch(e=>{if(e.code!=='ENOENT')throw e;});await fs.unlink(packed).catch(e=>{if(e.code!=='ENOENT')throw e;});}
 }
} finally {await browser.close();}

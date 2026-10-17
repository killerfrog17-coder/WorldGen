// Diagnostic geometry only; no simulation code. External artifacts stay external.
// Usage: node scripts/history-religion-images.mjs EVIDENCE_FOLDER...
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 for(const folder of process.argv.slice(2)) {
  const page=await browser.newPage({viewport:{width:1800,height:1120}});
  const modes=['public','household'];
  if(await fs.stat(path.resolve(folder,'associations.svg')).then(()=>true,()=>false)) modes.push('associations');
  for(const mode of modes) {
   const source=path.resolve(folder,`${mode}.svg`);
   await page.goto(pathToFileURL(source).href,{waitUntil:'load',timeout:120000});
   await page.screenshot({path:path.resolve(folder,`${mode}.png`)});
  }
  await page.close();
  const sheet=await browser.newPage({viewport:{width:1800,height:1120*modes.length}});
  const images=await Promise.all(modes.map(async mode=>(await fs.readFile(path.resolve(folder,`${mode}.png`))).toString('base64')));
  await sheet.setContent(`<style>body{margin:0}img{display:block;width:1800px}</style>${images.map(img=>`<img src="data:image/png;base64,${img}">`).join('')}`);
  await sheet.screenshot({path:path.resolve(folder,'comparison.png'),fullPage:true});
  await sheet.close();
  console.log(`Rendered recorded ${modes.join('/')} evidence: ${folder}`);
 }
} finally {await browser.close();}

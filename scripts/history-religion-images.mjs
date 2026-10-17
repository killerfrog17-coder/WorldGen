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
  for(const mode of ['public','household']) {
   const source=path.resolve(folder,`${mode}.svg`);
   await page.goto(pathToFileURL(source).href,{waitUntil:'load',timeout:120000});
   await page.screenshot({path:path.resolve(folder,`${mode}.png`)});
  }
  await page.close();
  const sheet=await browser.newPage({viewport:{width:1800,height:2240}});
  const publicImage=(await fs.readFile(path.resolve(folder,'public.png'))).toString('base64');
  const householdImage=(await fs.readFile(path.resolve(folder,'household.png'))).toString('base64');
  await sheet.setContent(`<style>body{margin:0}img{display:block;width:1800px}</style><img src="data:image/png;base64,${publicImage}"><img src="data:image/png;base64,${householdImage}">`);
  await sheet.screenshot({path:path.resolve(folder,'comparison.png'),fullPage:true});
  await sheet.close();
  console.log(`Rendered recorded public/household evidence: ${folder}`);
 }
} finally {await browser.close();}

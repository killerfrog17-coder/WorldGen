import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
const folder=process.argv[2],year=process.argv[3];
if(!folder||!year)throw Error('Usage: node scripts/history-snapshot-images.mjs <snapshot-folder> <year>');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1800,height:960}});
 for(const mode of ['States','Religions','Technology']) {
  await page.goto(pathToFileURL(path.resolve(folder,`${mode}-${year}.svg`)).href,{timeout:120000});
  const dated=path.join(folder,`${mode.toLowerCase()}-${year}.png`);
  await page.screenshot({path:dated});
  await fs.copyFile(dated,path.join(folder,`${mode.toLowerCase()}.png`));
 }
} finally {await browser.close();}

import {chromium} from 'playwright';
import path from 'node:path';
import fs from 'node:fs/promises';

const folders=process.argv.slice(2);
if(!folders.length)throw Error('Usage: node scripts/fauna-snapshot-images.mjs <fauna-review-folder> [...]');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage({viewport:{width:1600,height:850}});
 for(const folder of folders)for(const layer of ['Climate','Ecoregions','Biomes','penguin-web','elephant-web','forest-eagle-web']){
  const source=path.resolve(folder,layer+'.svg');if(!await fs.access(source).then(()=>true,()=>false))continue;
  await page.setContent("<style>html,body{margin:0;background:#10202a}svg{display:block}</style>"+await fs.readFile(source,'utf8'),{timeout:120000});
  await page.locator('svg').screenshot({path:path.join(folder,layer+'.png'),timeout:120000});
  console.log('Rendered',folder,layer);
 }
}finally{await browser.close();}

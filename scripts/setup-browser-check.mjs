import { chromium } from 'playwright';
import fs from 'node:fs/promises';
await fs.mkdir('artifacts/setup-review',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});const page=await browser.newPage({viewport:{width:1440,height:1000}});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
const heartbeat=setInterval(async()=>{try{console.log(await page.locator('.progress-card,.error').allTextContents());if(errors.length)console.log(errors);await page.screenshot({path:'artifacts/setup-review/current.png'});}catch{}},20000);
try{
 await page.goto('http://127.0.0.1:5094');await page.getByRole('button',{name:'Set Up World',exact:true}).waitFor();await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.screenshot({path:'artifacts/setup-review/menu.png'});
 if(process.argv.includes('--load')){
 const savedData=await fs.readFile('artifacts/setup-review/native-earth/snapshot.world');await page.evaluate(data=>window.__wg.loadWorldFile(data),savedData.toString('base64'));await page.waitForFunction(()=>!!window.__wg.snapshot(),{},{timeout:300000});
 }
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 if(!process.argv.includes('--load')){await page.getByLabel('Map type',{exact:true}).selectOption('Earth');await page.getByLabel('Map detail',{exact:true}).fill('5000');await page.getByLabel('Seed',{exact:true}).click();}
 else await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start world')?.disabled,{},{timeout:180000});
 await page.screenshot({path:'artifacts/setup-review/setup.png'});
 if(!process.argv.includes('--load')){
 await page.getByRole('button',{name:'Generate world',exact:true}).click();
 await page.getByRole('button',{name:'Plates',exact:true}).waitFor();
 await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Plates')?.disabled,{},{timeout:240000});
 await page.waitForFunction(()=>window.__wg.viewInfo().vertices>0,{},{timeout:180000});await page.screenshot({path:'artifacts/setup-review/plates-preview.png'});console.log('PASS plate preview rendered');
 await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start world')?.disabled,{},{timeout:300000});
 console.log('PASS generation complete');
 }
 const summary=JSON.parse(await page.evaluate(()=>window.__wg.snapshot()));const expected=JSON.parse(await fs.readFile('artifacts/setup-review/native-earth/hashes.json','utf8'));
 if(JSON.stringify(summary.Stages.map(s=>s.Hash))!==JSON.stringify(expected.map(s=>s.Hash)))throw Error('Native/browser stage hashes differ');console.log('PASS native/browser all-stage hashes');
 await page.getByRole('button',{name:'Climate',exact:true}).click();
 await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return !v.loading&&v.options.Layer===2;},{},{timeout:120000});
 await page.getByRole('button',{name:'Toggle globe or map'}).click();await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return !v.loading&&v.map;},{},{timeout:120000});
 await page.screenshot({path:'artifacts/setup-review/earth-climate.png'});
 const canvas=page.locator('#globe');const rect=await canvas.boundingBox();await page.mouse.move(rect.x+rect.width*.5,rect.y+rect.height*.5);await page.mouse.down();await page.mouse.move(rect.x+rect.width*.75,rect.y+rect.height*.5,{steps:8});await page.mouse.up();
 const info=await page.evaluate(()=>window.__wg.viewInfo());if(Math.abs(info.centerLongitude)<1)throw Error('Map did not wrap');
 await page.getByRole('button',{name:'Ecoregions',exact:true}).click();await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return !v.loading&&v.options.Layer===38;},{},{timeout:120000});
 const native=JSON.parse(await fs.readFile('artifacts/setup-review/native-earth/world.json','utf8'));const landCell=native.Ecology.Region.findIndex(id=>id>=0);
 await page.evaluate(cell=>window.__wg.pickCell(cell),landCell);await page.getByRole('button',{name:'Details',exact:true}).click();await page.getByRole('dialog',{name:'Ecoregion details'}).waitFor();await page.screenshot({path:'artifacts/setup-review/ecoregion-details.png'});await page.getByRole('button',{name:'Close',exact:true}).click();
 await page.getByRole('button',{name:'Start world',exact:true}).click();await page.screenshot({path:'artifacts/setup-review/viewer.png'});
 await page.getByRole('button',{name:'Save world',exact:true}).click();await page.getByText('Saved a complete world copy in this browser.').waitFor({timeout:120000});
 await page.getByRole('button',{name:'Main menu',exact:true}).last().click();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.getByRole('button',{name:'Save world',exact:true}).waitFor();
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS menu/setup/Earth/preview/viewer/save/load/wrapped map');
}finally{clearInterval(heartbeat);await browser.close();}

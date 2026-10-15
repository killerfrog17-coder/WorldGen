import { chromium } from 'playwright';
import fs from 'node:fs/promises';
await fs.mkdir('artifacts/history-1/browser',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
page.on('console',m=>{if(m.type()==='error')console.log('CONSOLE '+m.text());});page.on('requestfailed',r=>console.log('REQUEST FAILED '+r.url()));
const heartbeat=setInterval(async()=>{try{console.log('BROWSER '+(await page.locator('.progress-card,.error,.sim-bar').allTextContents()).join(' | '));await page.screenshot({path:'artifacts/history-1/browser/current.png'});}catch{}},20000);heartbeat.unref();
try{
 await page.goto('http://127.0.0.1:5094');await page.getByRole('button',{name:'Set Up World',exact:true}).click();console.log('Setup ready');
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});console.log('Module ready');
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile('artifacts/history-1/Earth-preview.world')).toString('base64'));
 console.log('Snapshot dispatched');await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start world')?.disabled,{},{timeout:240000});
 console.log('Starting loaded world');await page.getByRole('button',{name:'Start world',exact:true}).click();await page.getByRole('button',{name:'Step',exact:true}).waitFor();
 await page.waitForTimeout(1000);
 await page.locator('.society-dock button').nth(2).click();await page.getByRole('button',{name:'Map settings',exact:true}).click();
 await page.getByLabel('Map opacity',{exact:true}).fill('0.15');await page.getByLabel('Population opacity',{exact:true}).fill('1');
 await page.getByRole('button',{name:'Switch to map',exact:true}).click();await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:90000});
 await page.locator('.society-dock button').nth(0).click();await page.locator('.society-dock button').nth(1).click();
 for(const mode of ['Civilizations','States','Cultures','Religions','Technology']){
  await page.getByLabel('Historical overlay').selectOption(mode);await page.waitForTimeout(1000);
  const summary=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
  if(!summary.Legend.length)throw Error('Empty '+mode+' legend');
  await page.locator('.society-entry').first().click();await page.waitForTimeout(500);
  await page.getByRole('button',{name:'Selection',exact:true}).click();
  const selected=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
  if(!selected.Inspection.includes('LOCAL CHRONICLE')||!(await page.locator('.selection-summary').innerText()).length)throw Error('Missing underlying linked lore or narrative selection '+mode);
  if(mode==='Religions'&&selected.Subject?.Name!==await page.locator('.society-name').first().innerText())throw Error('Religious legend selected a converted holy site rather than current followers');
  await page.screenshot({path:'artifacts/history-1/browser/'+mode.toLowerCase()+'.png'});
  await page.getByRole('button',{name:'Map settings',exact:true}).click();
 }
 const [download]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:'Export campaign chronicle',exact:true}).click()]);
 await download.saveAs('artifacts/history-1/browser/exported-chronicle.txt');
 if(!(await fs.readFile('artifacts/history-1/browser/exported-chronicle.txt','utf8')).includes('Complete event archive'))throw Error('Incomplete lore export');
 await page.getByLabel('Years per simulation step').fill('37');await page.getByRole('button',{name:'Set',exact:true}).click();await page.waitForTimeout(500);await page.getByRole('button',{name:'Step',exact:true}).click();
 await page.waitForFunction(()=>document.querySelector('.sim-bar').innerText.includes('100,037'),{},{timeout:90000});
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS: saved history, five overlays, matching legends, locality inspection, campaign export, irregular manual step.');
}catch(e){console.log(await page.locator('body').innerText());await page.screenshot({path:'artifacts/history-1/browser/failure.png'});throw e;}finally{await browser.close();}

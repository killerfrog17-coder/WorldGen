import {chromium} from 'playwright';
import fs from 'node:fs';
import assert from 'node:assert/strict';
const url=process.env.WORLDGEN_URL||'http://127.0.0.1:5093';
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const page=await browser.newPage({viewport:{width:1600,height:1100}});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));
page.on('console',m=>{if(m.text().startsWith('ECOLOGY CHECK'))console.log(m.text());});
const folder='artifacts/ecology-review';fs.mkdirSync(folder,{recursive:true});
const workerOnly=process.argv.includes('--worker-only');
const ready=async()=>{await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready'||document.querySelector('[role=alert]'),{},{timeout:180000});if(await page.locator('[role=alert]').count())throw Error(await page.locator('[role=alert]').innerText());};
try{
 if(!workerOnly){
 await page.goto(url);await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByText('Advanced import / export',{exact:true}).click();
 for(const name of ['earth','one50','six30']){
  await page.locator('input[type=file]').nth(1).setInputFiles(`${folder}/${name}.world`);await ready();
  await page.getByLabel('View',{exact:true}).selectOption('Map');await ready();
  await page.getByLabel('Diagnostic layer',{exact:true}).selectOption('Ecoregions');await ready();
  await page.locator('#globe').screenshot({path:`${folder}/${name}-ecoregions.png`});
  const summary=await page.evaluate(async()=>JSON.parse((await import('/globe.js')).snapshot()));
  assert.deepEqual(summary.Stages,JSON.parse(fs.readFileSync(`${folder}/${name}.hashes.json`,'utf8')));
  console.log('PASS native snapshot and ecology rendering',name);
 }
 }else await page.goto(url+'/worker/main.js');
 // Generate one current recipe in the actual .NET browser worker and compare
 // every stage to native; no stale golden rewriting and no JS ecology model.
 const testName=workerOnly?'crosshost':'earth';
 const recipe=fs.readFileSync(`${folder}/${testName}.recipe.json`,'utf8');
 const expected=JSON.parse(fs.readFileSync(`${folder}/${testName}.hashes.json`,'utf8'));
 const result=await page.evaluate(recipe=>new Promise((resolve,reject)=>{
  const worker=new Worker('/worker/main.js',{type:'module'});let stages;const timer=setTimeout(()=>{worker.terminate();reject(Error('Ecology worker timeout'));},240000);
  worker.onerror=e=>{clearTimeout(timer);worker.terminate();reject(Error(e.message));};
  worker.onmessage=e=>{const d=e.data;if(d.type==='progress')console.log('ECOLOGY CHECK '+d.stage+' '+d.percent+'%');if(d.type==='ready')worker.postMessage({recipe});if(d.type==='error'){clearTimeout(timer);worker.terminate();reject(Error(d.message));}if(d.type==='complete'){stages=JSON.parse(d.json).Stages;worker.postMessage({type:'point',x:.9254,y:.3368,z:.17365});}if(d.type==='inspection'){clearTimeout(timer);worker.terminate();resolve({stages,text:d.text});}};
 }),recipe);
 assert.deepEqual(result.stages,expected);assert.ok(result.text.includes('FOOD WEB'));assert.ok(result.text.includes('Aridity P/PET'));
 fs.writeFileSync(`${folder}/earth-browser-inspection.txt`,result.text);
 assert.deepEqual(errors,[]);console.log('PASS all native/browser stage hashes and ecology inspector');
}finally{await browser.close();}

import { chromium } from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
import path from 'node:path';
import { createHash } from 'node:crypto';

const source=process.argv[2],output=process.argv[3]||'artifacts/history32/living-browser';
if(!source)throw Error('Usage: node scripts/living-world-browser-check.mjs <saved-world> [output]');
await fs.mkdir(output,{recursive:true});
let stage='launch',heartbeatBusy=false;
async function log(event,data={}){const row={time:new Date().toISOString(),stage,event,...data};console.log(JSON.stringify(row));await fs.appendFile(`${output}/browser-events.jsonl`,JSON.stringify(row)+'\n');}
const debugPort=Number(process.env.WORLDGEN_CDP_PORT||0);
const browser=await chromium.launch({headless:true,channel:'msedge',args:debugPort?[`--remote-debugging-port=${debugPort}`]:[]});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.setDefaultTimeout(120000);page.on('pageerror',e=>{errors.push(String(e));log('pageerror',{error:String(e)}).catch(()=>{});});
page.on('console',message=>{if(['error','warning'].includes(message.type()))log('console-'+message.type(),{text:message.text()}).catch(()=>{});});
page.on('console',message=>{if(message.text().startsWith('__WORLDGEN_PROBE__'))log('worker-trace',{text:message.text().slice(18)}).catch(()=>{});});
page.on('requestfailed',request=>log('requestfailed',{url:request.url(),error:request.failure()?.errorText}).catch(()=>{}));
async function heartbeatReport(){
 if(heartbeatBusy)return;heartbeatBusy=true;
 try{
  const dom=await Promise.race([page.evaluate(()=>({url:location.href,ready:document.readyState,title:document.title,text:document.body?.innerText.slice(-4500),start:document.querySelector('button.start')?{disabled:document.querySelector('button.start').disabled,text:document.querySelector('button.start').textContent}:null,canvas:document.querySelector('canvas')?{width:document.querySelector('canvas').width,height:document.querySelector('canvas').height}:null})),new Promise((_,reject)=>setTimeout(()=>reject(Error('DOM heartbeat exceeded5s')),5000))]);
  await log('heartbeat',{url:dom.url,ready:dom.ready,start:dom.start,canvas:dom.canvas,tail:dom.text?.slice(-500)});await fs.writeFile(`${output}/latest-dom.json`,JSON.stringify({stage,dom,errors},null,2));
 }catch(error){await log('heartbeat-unavailable',{error:String(error)});}finally{heartbeatBusy=false;}
}
const heartbeat=setInterval(()=>heartbeatReport().catch(console.error),15000);heartbeat.unref();
async function read(command){
 const deadline=Date.now()+180000;
 for(;;){try{return await page.evaluate(command=>window.__wg.people(command),command);}catch(e){if(!String(e).includes('A simulation update is already running')||Date.now()>deadline)throw e;await page.waitForTimeout(100);}}
}
async function section(name){
 for(const chapter of await page.locator('.atlas-chapters button').allTextContents()){
  const button=page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name,exact:true});
  if(await button.count()){await button.click();return;}
  await page.getByRole('navigation',{name:'Article chapters'}).getByRole('button',{name:chapter,exact:true}).click();
 }
 await page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name,exact:true}).click();
}
async function screenshot(name){await page.screenshot({path:`${output}/${name}.png`,timeout:15000});await log('screenshot',{name});}
try{
 await log('start',{source,debugPort});stage='navigation';
 if(process.env.WORLDGEN_WORKER_ROOT){
  const root=path.resolve(process.env.WORLDGEN_WORKER_ROOT);
  await page.route('**/worker/_framework/**',async route=>{
   const relative=decodeURIComponent(new URL(route.request().url()).pathname.split('/worker/')[1]),file=path.resolve(root,relative);
   if(!file.startsWith(root+path.sep))throw Error('Worker asset escaped configured root.');
   const extension=path.extname(file),contentType=extension==='.wasm'?'application/wasm':extension==='.js'?'text/javascript':extension==='.json'?'application/json':'application/octet-stream';
   await route.fulfill({path:file,contentType});
  });
  await log('worker-assets-override',{root});
 }
 await page.addInitScript(()=>{
  const NativeWorker=window.Worker;
  window.Worker=class extends NativeWorker{
   constructor(...args){super(...args);this.addEventListener('message',e=>{const d=e.data;if(['ready','progress','complete','view-complete','error'].includes(d?.type))console.log('__WORLDGEN_PROBE__'+JSON.stringify({direction:'receive',at:performance.now(),type:d.type,stage:d.stage,percent:d.percent,error:d.message}));});}
   postMessage(message,...options){if(['load-world-file','people','view','cached-view'].includes(message?.type))console.log('__WORLDGEN_PROBE__'+JSON.stringify({direction:'send',at:performance.now(),type:message.type,command:message.command,length:message.data?.length}));return super.postMessage(message,...options);}
  };
 });
 await page.route('**/music/catalog.json',r=>r.fulfill({contentType:'application/json',body:'[]'}));
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5080');
 await screenshot('startup');stage='setup';
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await screenshot('setup');stage='import';await log('import-begin',{bytes:(await fs.stat(source)).size});
 await page.evaluate(async bytes=>{window.__wg=await import('/globe.js');await window.__wg.loadWorldFile(bytes);},(await fs.readFile(source)).toString('base64'));
 stage='wait-world-ready';await log('import-dispatched');await heartbeatReport();
 await page.waitForFunction(()=>document.querySelector('button.start')&&!document.querySelector('button.start').disabled,{},{timeout:600000});
 await fs.writeFile(`${output}/world-summary.json`,await page.evaluate(()=>window.__wg.snapshot()));
 stage='start-simulation';await screenshot('world-ready');
 await page.getByRole('button',{name:'Start world',exact:true}).click();await page.locator('.sim-bar').waitFor();
 stage='read-before';
 const before=await read('lore');
 await page.waitForFunction(()=>!window.__wg.viewInfo().loading&&!document.querySelector('.progress-card'),{},{timeout:120000});
 await log('simulation-view',{view:await page.evaluate(()=>window.__wg.viewInfo())});
 stage='atlas';
 await screenshot('simulation');await page.getByRole('button',{name:'World',exact:true}).click();
 await page.getByRole('dialog',{name:'World atlas'}).waitFor();await screenshot('world-overview');
 await page.locator('.atlas-directory').getByRole('button',{name:'States',exact:true}).click();
 await page.locator('.atlas-directory-list button').first().click();
 await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='state');
 for(const [name,file] of [['Political interests','political-interests'],['Public priorities','public-priorities'],['Local resource potential','resources'],['Climate and livelihoods','climate']]){
  await section(name);await screenshot(file);
  const text=await page.locator('.atlas-reading').innerText();assert.ok(text.includes(name));
  if(name==='Political interests')assert.equal(await page.locator('.atlas-reading progress').count(),5);
  if(name==='Public priorities')assert.equal(await page.locator('.atlas-reading progress').count(),4);
  if(name==='Local resource potential')assert.equal(await page.locator('.atlas-reading progress').count(),13);
 }
 await page.locator('.atlas-directory').getByRole('button',{name:'Peoples',exact:true}).click();await page.locator('.atlas-directory-list button').first().click();
 await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='people');
 await section('People and appearance');assert.equal(await page.locator('.appearance-swatch').count(),3);await screenshot('appearance');
 if(process.env.WORLDGEN_PORT_TOWN){
  stage='harbor-reader';const townId=Number(process.env.WORLDGEN_PORT_TOWN);
  const port=JSON.parse(await page.evaluate(id=>window.__wg.people('atlas:city',id),townId));
  await page.locator('.atlas-directory').getByRole('button',{name:'Cities',exact:true}).click();
  await page.getByRole('textbox',{name:'Search atlas names'}).fill(port.Title);
  await page.locator(`.atlas-directory-list button[data-kind="city"][data-id="${townId}"]`).click();
  await page.waitForFunction(id=>document.querySelector('.atlas-article')?.dataset.id===String(id),townId);
  await section('Harbor and supply');await screenshot('harbor-supply');
  const supply=await page.locator('.atlas-reading').innerText();
  assert.ok(supply.includes('not a new farming hinterland')&&supply.includes('Total cargo received')&&supply.includes('Freight paid'));
  assert.ok(await page.locator('.atlas-reading .atlas-link-row button').count());
  await section('Settlement history and hinterland');assert.ok((await page.locator('.atlas-reading').innerText()).includes('Reserved harbor footprint'));await screenshot('harbor-footprint');
  await section('Provisioning and resilience');const food=await page.locator('.atlas-reading').innerText();
  assert.ok(food.includes('Latest recorded food imports')&&!food.includes('Lifetime food imports'));await screenshot('harbor-food-flows');
  if(process.env.WORLDGEN_CURRENT_PORT_LABELS)assert.ok(food.includes('Latest outgoing transport losses')&&food.includes('incoming route losses'));
  await page.getByRole('textbox',{name:'Search atlas names'}).fill('');
 }
 await page.locator('.atlas-directory').getByRole('button',{name:'Organizations',exact:true}).click();
 const count=await page.locator('.atlas-directory-list button').count();
 if(count){
  await page.locator('.atlas-directory-list button').first().click();await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='organization');await screenshot('organization');
  await section('Member governments');assert.ok(await page.locator('.atlas-reading .atlas-link-row button').count());await screenshot('organization-members');
  await section('Common finances');assert.ok((await page.locator('.atlas-reading').innerText()).includes('Unspent reserve'));await screenshot('organization-finances');
  if(await page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name:'Operating budget',exact:true}).count()){await section('Operating budget');await screenshot('organization-budget');}
  else await log('legacy-organization-budget-absent');
 }else console.log('No active diplomatic organization in this control; directory empty rather than invented.');
 await page.setViewportSize({width:1024,height:768});await screenshot('compact-reader');
 const bounds=await page.locator('.atlas-reader').boundingBox();assert.ok(bounds.x>=0&&bounds.y>=0&&bounds.x+bounds.width<=1025&&bounds.y+bounds.height<=769);
 await page.keyboard.press('Escape');await page.getByRole('dialog',{name:'World atlas'}).waitFor({state:'hidden'});
 const after=await read('lore');assert.equal(after,before);assert.deepEqual(errors,[]);
 await log('read-only-lore',{characters:after.length,sha256:createHash('sha256').update(after).digest('hex')});
 console.log('PASS actual WASM/Blazor resource, climate, political, appearance and organization pages, compact viewport, read-only lore.');
}catch(e){await log('failure',{error:String(e),stack:e.stack});await screenshot('failure').catch(error=>log('failure-screenshot-unavailable',{error:String(error)}));console.log((await page.locator('body').innerText()).slice(-5000));throw e;}
finally{clearInterval(heartbeat);await browser.close();}

import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);if(!source||!output)throw Error('Usage: atlas-overhaul-browser-check.mjs <world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1500,height:1000}}),errors=[];page.setDefaultTimeout(60000);page.on('pageerror',e=>errors.push(String(e)));
async function request(command,value=0){for(let i=0;i<120;i++){try{return JSON.parse(await page.evaluate(([c,v])=>window.__wg.people(c,v),[command,value]));}catch(e){if(!String(e).includes('already running'))throw e;await page.waitForTimeout(100);}}throw Error('Worker remained busy');}
const categories=[['state','States'],['province','Provinces'],['city','Cities'],['people','Peoples'],['ethnicity','Ethnicities'],['language','Languages'],['religion','Religions'],['religious-body','Ritual bodies'],['technology','Technology'],['ruler','Rulers'],['notable','Notable people'],['organization','Organizations']];
const report=[];
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5092');await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>[...document.querySelectorAll('button')].some(b=>b.textContent==='Start world'&&!b.disabled),{},{timeout:600000});console.log('Imported world; checking rendered Atlas.');
 await page.getByRole('button',{name:'Start world',exact:true}).click();await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});
 const before=await request('status');await page.keyboard.press('w');const atlas=page.getByRole('dialog',{name:'World atlas'});await atlas.waitFor();
 await page.screenshot({path:output+'/world.png'});
 for(const [kind,name] of categories){
  console.log('Reviewing '+name);
  await atlas.locator('.atlas-directory').getByRole('button',{name,exact:true}).click();await atlas.getByRole('heading',{name,exact:true}).waitFor();
  const headers=await atlas.locator('.atlas-record-table thead button').allTextContents();assert.ok(headers.length>=4,name+' substantive columns');
  const dir=await request('atlas-directory:'+kind);
  const numeric=dir.flatMap(r=>r.Measures||[]).find(m=>m.Numeric&&headers.some(x=>x.trim().startsWith(m.Key)));
  if(numeric){await atlas.locator(`button[data-sort="${numeric.Key}"]`).click();await page.waitForTimeout(80);
   const column=headers.findIndex(x=>x.trim().startsWith(numeric.Key));const cells=await atlas.locator(`.atlas-record-table tbody td:nth-child(${column+1})`).allTextContents();
   const numbers=cells.map(v=>Number(v.replace(/,/g,'').replace(/%$/,''))).filter(Number.isFinite);if(!/Founded|Recorded|Accession/.test(numeric.Key))assert.ok(numbers.every((n,i)=>i===0||numbers[i-1]>=n),name+' descending numeric sort');}
  if(!dir.length){assert.match(await atlas.locator('.atlas-record-table').innerText(),/No matching records/);report.push({kind,records:0});continue;}
  const visible=atlas.locator(`.atlas-record-table button[data-kind="${kind}"]`).first();const id=Number(await visible.getAttribute('data-id'));const entry=dir.find(row=>row.Id===id);assert.ok(entry,'Visible row belongs to the actual directory');await visible.click();await atlas.locator(`.atlas-article[data-kind="${kind}"][data-id="${entry.Id}"]`).waitFor();
  const exhibits=await atlas.locator('[data-exhibit]').count();await page.screenshot({path:output+'/'+kind+'.png'});
  if(kind==='state'&&process.argv.includes('--domestic')){
   const card=atlas.locator('[data-exhibit="Domestic power groups"]');await card.waitFor();assert.match(await card.textContent(),/governing/);
   await card.locator('.exhibit-title').click();await atlas.getByRole('heading',{name:'Domestic power groups',exact:true}).waitFor();
   assert.match(await atlas.textContent(),/organization/);await page.screenshot({path:output+'/domestic-power.png'});
   await atlas.getByRole('button',{name:'Power & relations',exact:true}).click();assert.match(await atlas.textContent(),/Institutional bargaining/);
   await page.screenshot({path:output+'/domestic-bargaining.png'});await atlas.getByRole('button',{name:'Overview',exact:true}).click();
  }
  if(kind==='ethnicity'&&await atlas.locator('.atlas-portraits').count()){
   assert.equal(await atlas.locator('.atlas-portraits g').count(),6);await atlas.locator('[data-exhibit="People and appearance"] .exhibit-title').click();
   await atlas.locator('.appearance-parameters progress').first().waitFor();assert.equal(await atlas.locator('.appearance-parameters progress').count(),8);await page.screenshot({path:output+'/ethnicity-appearance.png'});
  }
  if(kind==='language'&&await atlas.locator('.lexicon').count()){assert.ok(await atlas.locator('.lexicon article').count()>=6);}
  if(kind==='technology'&&await atlas.locator('.exhibit-tech').count()){assert.ok(await atlas.locator('.exhibit-tech progress').count()>=3);}
  if(await atlas.locator('.network-node').count()){
   const target=await atlas.locator('.network-node').first().textContent();await atlas.locator('.network-node').first().click();await page.waitForTimeout(100);assert.ok(await atlas.locator('h1').count(),target+' graph navigates');await atlas.getByRole('button',{name:'Back',exact:true}).click();
  }
  report.push({kind,records:dir.length,exhibits});
 }
 await page.keyboard.press('Escape');await atlas.waitFor({state:'detached'});assert.equal((await request('status')).Seconds,before.Seconds);assert.deepEqual(errors,[]);
 await fs.writeFile(output+'/result.json',JSON.stringify({report,errors,passed:true},null,2));console.log('PASS tab-specific dashboards, numeric table sorting, graph navigation, appearance/phonetic/technology visual data and paused time.');
}catch(e){await page.screenshot({path:output+'/failure.png'});console.log((await page.locator('body').innerText()).slice(-1800));throw e;}finally{await browser.close();}

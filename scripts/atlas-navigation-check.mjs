import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/atlas-navigation-check.mjs <world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1400,height:900}}),errors=[];
page.setDefaultTimeout(60000);page.on('pageerror',e=>errors.push(String(e)));
async function status(){for(let i=0;i<120;i++){try{return JSON.parse(await page.evaluate(()=>window.__wg.people('status')));}catch(e){if(!String(e).includes('already running'))throw e;await page.waitForTimeout(100);}}throw Error('Worker remained busy');}
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5092');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>[...document.querySelectorAll('button')].some(b=>b.textContent==='Start world'&&!b.disabled),{},{timeout:240000});
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});
 const before=await status();
 assert.equal(await page.locator('.sim-navigation button').count(),1);
 assert.equal(await page.locator('.sim-navigation button').innerText(),'World [W]');
 const states=JSON.parse(await page.evaluate(()=>window.__wg.people('atlas-directory:state')));
 let selected;
 if(states.length){
  selected=states[0];await page.evaluate(cell=>window.__wg.pickCell(cell),selected.Cell);
  await page.waitForFunction(id=>document.querySelector('.society-dock')?.textContent.includes(id),selected.Name,{timeout:60000});
 }
 await page.keyboard.press('w');const atlas=page.getByRole('dialog',{name:'World atlas'});
 await atlas.waitFor();
 if(selected){await atlas.locator(`.atlas-article[data-kind="state"][data-id="${selected.Id}"]`).waitFor();}
 await page.keyboard.press('w');await atlas.locator('.atlas-article[data-kind="world"]').waitFor();
 for(const name of ['States','Provinces','Cities','Peoples','Ethnicities','Languages','Religions','Ritual bodies','Technology','Rulers','Notable people','Organizations']){
  await atlas.locator('.atlas-directory').getByRole('button',{name,exact:true}).click();
  await atlas.getByRole('heading',{name,exact:true}).waitFor();
  assert.equal(await atlas.locator('.atlas-record-table').count(),1,name+' table');
 }
 await atlas.locator('.atlas-directory').getByRole('button',{name:'Cities',exact:true}).click();
 await atlas.getByRole('textbox',{name:'Search atlas names'}).fill('no-such-atlas-record-989898');
 await atlas.locator('.atlas-record-table').getByText('No matching records.',{exact:true}).waitFor();
 await page.screenshot({path:output+'/empty-cities.png'});
 await atlas.getByRole('textbox',{name:'Search atlas names'}).press('w');
 assert.equal(await atlas.getByRole('heading',{name:'Cities',exact:true}).count(),1,'W typing must stay in search');
 await page.keyboard.press('Escape');await atlas.waitFor({state:'detached'});
 assert.equal(await page.getByText('Settings · paused',{exact:true}).count(),0,'Esc closes atlas without pausing menu');
 await page.keyboard.press('w');await atlas.waitFor();
 await atlas.locator('.atlas-directory').getByRole('button',{name:'Map settings',exact:true}).click();
 await atlas.getByRole('heading',{name:'Map settings',exact:true}).waitFor();
 await atlas.getByRole('combobox',{name:'Historical overlay',exact:true}).selectOption('Cultures');
 await page.waitForFunction(()=>document.querySelector('.society-dock button')?.textContent.includes('Cultures'));
 await atlas.getByRole('combobox',{name:'Base map',exact:true}).selectOption('Terrain');
 await page.screenshot({path:output+'/map-settings.png'});
 await page.keyboard.press('w');await atlas.locator('.atlas-article[data-kind="world"]').waitFor();
 await page.screenshot({path:output+'/world-overview.png'});
 await page.keyboard.press('Escape');await atlas.waitFor({state:'detached'});
 assert.equal((await status()).Seconds,before.Seconds);assert.deepEqual(errors,[]);
 await fs.writeFile(output+'/result.json',JSON.stringify({selected,years:before.Years,errors,passed:true},null,2));
 console.log('PASS unified World button; W selected/world; every category table; empty Cities; typing and Esc; map settings; unchanged time.');
}catch(e){await page.screenshot({path:output+'/failure.png'});console.log((await page.locator('body').innerText()).slice(-1800));throw e;}
finally{await browser.close();}

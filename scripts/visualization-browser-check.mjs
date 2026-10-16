import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const output=process.env.VISUAL_CHECK_OUTPUT||'artifacts/visualization/final-browser';await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:process.env.VISUAL_DEBUG_PORT?[`--remote-debugging-port=${process.env.VISUAL_DEBUG_PORT}`]:[]}),page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.setDefaultTimeout(600000);page.on('pageerror',e=>errors.push(String(e)));
const beat=setInterval(()=>console.log('Waiting for visualization check…'),30000);beat.unref();
async function status(){const deadline=Date.now()+120000;for(;;){try{return await page.evaluate(async()=>JSON.parse(await window.__wg.people('status')));}catch(e){if(!String(e).includes('A simulation update is already running')||Date.now()>deadline)throw e;await page.waitForTimeout(50);}}}
async function chapter(name){await page.getByRole('navigation',{name:'Article chapters'}).getByRole('button',{name,exact:true}).click();}
async function section(name){if(!await page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name,exact:true}).count()){for(const c of await page.locator('.atlas-chapters button').allTextContents()){await chapter(c);if(await page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name,exact:true}).count())break;}}await page.getByRole('navigation',{name:'Article sections'}).getByRole('button',{name,exact:true}).click();}
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5094');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();await page.evaluate(async bytes=>{window.__wg=await import('/globe.js');await window.__wg.loadWorldFile(bytes);},(await fs.readFile(process.argv[2]||'artifacts/history29/upgraded-earth20/final.world')).toString('base64'));
 await page.waitForFunction(()=>document.querySelector('button.start')&&!document.querySelector('button.start').disabled,{},{timeout:600000});
 await page.getByRole('button',{name:'Start world',exact:true}).click();await page.locator('.society-dock').waitFor();
 const before=await status();console.log('Loaded history',before.Years,before.Population);
 for(let i=0;i<3;i++)await page.locator('.society-dock>button').nth(i).click();
 await page.getByRole('button',{name:'Map settings',exact:true}).click();await page.getByLabel('Map opacity',{exact:true}).fill('.15');
 await page.getByRole('button',{name:'Switch to map',exact:true}).click();await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading);
 for(const [number,mode,kind] of [[1,'States','state'],[3,'Cultures','people'],[4,'Religions','religion'],[5,'Technology','technology'],[6,'Territories','state'],[7,'Provinces','province']]){
  await page.locator('#globe').focus();await page.keyboard.press(String(number));await page.waitForFunction(mode=>document.querySelector('[aria-label="Historical overlay"]').value===mode,mode);
  const modeState=await status();assert.equal(modeState.Overlay,mode);await page.waitForFunction(name=>document.querySelector('.society-name')?.textContent===name,modeState.Legend[0].Name);
  await page.locator('.society-entry').first().click();await page.getByRole('button',{name:'Selection',exact:true}).click();
  await page.waitForFunction(mode=>document.querySelector('.selection-card .card-subtitle')?.textContent?.length>0,mode);
  const s=await status();assert.equal(s.Subject.Kind,kind,`${mode} subject`);await page.waitForFunction(title=>document.querySelector('.selection-card h3')?.textContent===title,s.Card.Title);
  if(mode==='Technology')assert.ok((await page.locator('.card-subtitle').innerText()).includes('technology'));
  await page.screenshot({path:`${output}/${mode.toLowerCase()}.png`});
  if(mode==='Technology'){
   await page.getByRole('button',{name:'Atlas',exact:true}).click();await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='technology');
   await page.screenshot({path:`${output}/technology-overview.png`});await section('Knowledge and deployment');assert.ok(await page.locator('.technical-card').count()>10);await page.screenshot({path:`${output}/technology-desk.png`});await page.getByRole('button',{name:'Close',exact:true}).click();
  }
  await page.getByRole('button',{name:'Map settings',exact:true}).click();
 }
 await page.locator('#globe').focus();await page.keyboard.press('1');await page.locator('.society-entry').first().click();await page.getByRole('button',{name:'Atlas',exact:true}).click();await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='state');
 assert.ok(await page.locator('.atlas-fact-tiles>div').count()>0);assert.ok(await page.locator('.atlas-chapters button').count()<=5);await page.screenshot({path:`${output}/state-atlas.png`});await section('Diplomatic neighborhood');await page.screenshot({path:`${output}/relationships.png`});await page.getByRole('button',{name:'Close',exact:true}).click();
 await page.getByRole('button',{name:'World',exact:true}).click();await page.locator('.atlas-article[data-kind="world"]').waitFor();await page.screenshot({path:`${output}/world-overview.png`});await section('Recorded history');await page.getByRole('button',{name:'Older',exact:true}).click();assert.ok((await page.locator('.atlas-record-count').innerText()).includes('Records 41'));await page.getByRole('button',{name:'Close',exact:true}).click();
 await page.getByRole('button',{name:'Map settings',exact:true}).click();await page.getByLabel('Map opacity',{exact:true}).fill('.5');await page.getByLabel('Population opacity',{exact:true}).fill('1');await page.locator('#globe').focus();
 for(const [key,label,value] of [['-','Population opacity',.75],['=','Population opacity',1],['Shift+-','Map opacity',.25],['Shift+=','Map opacity',.5]]){await page.keyboard.press(key);await page.waitForFunction(({label,value})=>Math.abs(Number(document.querySelector(`[aria-label="${label}"]`).value)-value)<.001,{label,value});}
 await page.keyboard.press('Shift+5');await page.waitForFunction(()=>document.querySelector('[aria-label="Base map"]').value==='Ecoregions');
 await page.getByLabel('Population opacity',{exact:true}).fill('0');await page.evaluate(async cell=>window.__wg.pickCell(cell),before.Legend[0].Cell);await page.getByRole('button',{name:'Selection',exact:true}).click();await page.locator('.selection-card figure img').waitFor();await page.waitForFunction(()=>document.querySelector('.selection-card img').naturalWidth>0);await page.screenshot({path:`${output}/ecoregion-card.png`});
 const after=await status();assert.equal(after.Seconds,before.Seconds);assert.equal(after.Population,before.Population);assert.deepEqual(errors,[]);
 await page.setViewportSize({width:1024,height:720});await page.screenshot({path:`${output}/small-window.png`});console.log('PASS: contextual selections, charts, atlas chapters, archive paging, map shortcuts and 25% layer opacity; no simulation change.');
}catch(e){await page.screenshot({path:`${output}/failure.png`});console.log((await page.locator('body').innerText()).slice(-5000));if(!process.env.VISUAL_KEEP_BROWSER)throw e;console.error(e);}finally{clearInterval(beat);if(process.env.VISUAL_KEEP_BROWSER){console.log('Viewer retained for read-only inspection on local debug port '+process.env.VISUAL_DEBUG_PORT);await new Promise(()=>{});}else await browser.close();}

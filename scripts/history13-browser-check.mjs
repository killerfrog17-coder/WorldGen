// End-to-end adapter/UI evidence. No historical calculations live here.
import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/history13-browser-check.mjs <world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.setDefaultTimeout(120000);page.on('pageerror',e=>errors.push(String(e)));
let stage='opening';const heartbeat=setInterval(()=>console.log('History13 browser: '+stage),20000);heartbeat.unref();
const status=async()=>JSON.parse(await page.evaluate(async()=>{for(let attempt=0;attempt<300;attempt++){try{return await window.__wg.people('status');}catch(e){if(!String(e).includes('already running'))throw e;await new Promise(resolve=>setTimeout(resolve,100));}}throw Error('Simulation worker did not become idle');}));
const ready=async()=>page.waitForFunction(()=>!document.querySelector('.progress-card')&&[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:240000});
async function section(name){const button=page.getByRole('button',{name,exact:true});await button.click();await button.and(page.locator('[aria-current="page"]')).waitFor();await page.waitForFunction(name=>document.querySelector('.atlas-reading h2')?.textContent===name,name);await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));const tabs=await page.locator('.atlas-contents button').evaluateAll(buttons=>buttons.map(b=>({name:b.textContent,selected:b.classList.contains('selected'),current:b.getAttribute('aria-current'),background:getComputedStyle(b).backgroundColor})));await fs.writeFile(output+'/tabs-'+name.replaceAll(' ','-')+'.json',JSON.stringify(tabs,null,2));const active=tabs.filter(b=>b.selected||b.current==='page');if(active.length!==1||!active[0].selected||active[0].current!=='page'||active[0].name!==name)throw Error('Visible section highlight disagrees with article: '+JSON.stringify(active));}
try{
 await page.goto('http://127.0.0.1:5094');
 await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Modern history UI check',bytes,{CellCount:20000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();
 stage='loading shared .NET snapshot';await page.getByRole('button',{name:'Load last save',exact:true}).click();await ready();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 const before=await status();if(before.TechnologyCeiling<0)throw Error('Current history has no ceiling');
 stage='checking folded selection';
 const canvas=page.locator('#globe'),box=await canvas.boundingBox();await canvas.click({position:{x:box.width*.55,y:box.height*.45}});
 await page.waitForTimeout(500);if(await page.locator('.society-panel.selection').count())throw Error('A map click unfolded the selection panel');
 await page.evaluate(cell=>window.__wg.pickCell(cell),before.Cultures[0].OriginCell);
 await page.waitForTimeout(500);if(await page.locator('.society-panel.selection').count())throw Error('A known cell selection unfolded its panel');
 await page.screenshot({path:output+'/folded-map.png'});
 stage='reading actual named crops';await page.getByRole('button',{name:'World',exact:true}).click();
 await page.locator('.atlas-directory').getByRole('button',{name:'Cities',exact:true}).click();await page.locator('.atlas-directory-list button').first().click();
 await section('Crops and cultivated food');
 const crops=await page.locator('.atlas-reading').innerText();if(!crops.includes('Last survey')||!crops.includes('modeled mix'))throw Error('Missing actual local crop survey');
 await page.locator('.atlas-contents').evaluate(async element=>{await Promise.all(element.getAnimations({subtree:true}).map(animation=>animation.finished.catch(()=>{})));});await page.screenshot({path:output+'/city-crops.png'});
 stage='reading civic memory';let civicMemory=false;
 const cityRows=page.locator('.atlas-directory-list button'),cityCount=Math.min(20,await cityRows.count());
 for(let index=0;index<cityCount;index++){
  const id=await cityRows.nth(index).getAttribute('data-id');await cityRows.nth(index).click();await page.waitForFunction(id=>document.querySelector('.atlas-article')?.dataset.id===id&&!document.querySelector('.atlas-loading'),id);const memory=page.getByRole('button',{name:'Civic rivalries and memory',exact:true});
  if(await memory.count()){await section('Civic rivalries and memory');const text=await page.locator('.atlas-reading').innerText();if(!text.includes('actual nearby city conflicts')||!text.includes('first recorded'))throw Error('Missing dated civic memory narrative');await page.locator('.atlas-contents').evaluate(async element=>{await Promise.all(element.getAnimations({subtree:true}).map(animation=>animation.finished.catch(()=>{})));});await page.screenshot({path:output+'/city-rivalries.png'});civicMemory=true;break;}
 }
 if(!civicMemory)throw Error('No civic memory article among the first twenty current cities in this integration fixture');
 await page.locator('.atlas-directory').getByRole('button',{name:'Peoples',exact:true}).click();await page.locator('.atlas-directory-list button').first().click();
 await section('Selected food plants');if(!(await page.locator('.atlas-reading').innerText()).includes('maturity'))throw Error('Missing cultivar traits');
 await page.locator('.atlas-contents').evaluate(async element=>{await Promise.all(element.getAnimations({subtree:true}).map(animation=>animation.finished.catch(()=>{})));});await page.screenshot({path:output+'/crop-lineages.png'});await page.getByRole('button',{name:'Close',exact:true}).click();
 const afterReading=await status();if(before.Seconds!==afterReading.Seconds||before.Population!==afterReading.Population)throw Error('Reading advanced history');
 stage='technology ceiling and habitat details';await page.getByRole('button',{name:'View',exact:true}).click();
 await page.locator('.society-panel.selection').getByLabel('Technology limit').selectOption('0');
 await page.waitForFunction(()=>{const control=document.querySelector('.society-panel.selection select[aria-label="Technology limit"]');return control?.value==='0'&&!control.disabled;});
 if((await status()).TechnologyCeiling!==0)throw Error('Ceiling control did not reach Core');
 await page.getByLabel('Base map',{exact:true}).selectOption('Ecoregions');await ready();
 await page.getByLabel('Population opacity',{exact:true}).evaluate(e=>{e.value='0';e.dispatchEvent(new Event('input',{bubbles:true}));});
 await page.locator('.society-panel.selection header').getByRole('button',{name:'Selection',exact:true}).click();
 await page.evaluate(cell=>window.__wg.pickCell(cell),before.Cultures[0].OriginCell);
 await page.waitForFunction(()=>document.querySelector('.selection-summary')?.textContent.includes('growing season'));
 const habitat=await page.locator('.selection-summary').innerText();for(const text of ['soil','taxa','feeding links'])if(!habitat.includes(text))throw Error('Missing narrative habitat detail '+text);
 await page.getByRole('button',{name:'Species and food web',exact:true}).click();await page.locator('.ecology-dialog svg').waitFor();
 await page.screenshot({path:output+'/ecoregion-food-web.png'});await page.locator('.ecology-dialog').getByRole('button',{name:'Close',exact:true}).click();
 stage='saving ceiling and reloading';await page.evaluate(()=>window.__wg.saveLibraryWorld('Ceiling roundtrip check'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();
 await page.locator('.world-row').filter({hasText:'Ceiling roundtrip check'}).getByRole('button',{name:'Load last save',exact:true}).click();await ready();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});const restored=await status();
 if(restored.TechnologyCeiling!==0||restored.Seconds!==before.Seconds||restored.Population!==before.Population)throw Error('Saved ceiling or historical state did not roundtrip');
 if(errors.length)throw Error(errors.join('\n'));
 await fs.writeFile(output+'/checks.json',JSON.stringify({selectionStayedFolded:true,readOnlyAtlas:true,localCropSurvey:true,cultivarTraits:true,civicMemory,ecoregionFoodWeb:true,savedCeiling:restored.TechnologyCeiling,seconds:restored.Seconds},null,2));
 console.log('PASS: folded map selection, city crop surveys, cultivar traits, ecological narrative/web, and saved technology ceiling.');
}catch(e){await page.screenshot({path:output+'/failure.png'});throw e;}
finally{clearInterval(heartbeat);await browser.close();}

// UI/adapter check only: all simulation and articles come from shared .NET.
import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2],output=process.argv[3];
if(!source||!output)throw Error('Usage: node scripts/history11-browser-check.mjs <world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.setDefaultTimeout(120000);
page.on('pageerror',e=>errors.push(String(e)));
async function capture(file){
 const tabs=await page.locator('.atlas-contents button').evaluateAll(buttons=>buttons.filter(b=>b.classList.contains('selected')||b.getAttribute('aria-current')==='page').map(b=>({name:b.textContent,selected:b.classList.contains('selected'),current:b.getAttribute('aria-current')})));
 if(tabs.length&&(tabs.length!==1||!tabs[0].selected||tabs[0].current!=='page'))throw Error('Article selection and accessibility state disagree: '+JSON.stringify(tabs));
 await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
 await page.locator('.atlas-contents').evaluate(async element=>{await Promise.all(element.getAnimations({subtree:true}).map(animation=>animation.finished.catch(()=>{})));});
 await page.screenshot({path:output+'/'+file});
}
let stage='starting';const heartbeat=setInterval(()=>console.log('History11 browser reader check: '+stage),20000);heartbeat.unref();
try{
 stage='opening app';
 await page.goto('http://127.0.0.1:5094');
 stage='importing snapshot';
 await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Historical viewer check',bytes,{CellCount:20000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 stage='loading library world';await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 stage='waiting for shared .NET viewer';
 await page.waitForFunction(()=>!document.querySelector('.progress-card')&&[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:240000});
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 stage='reading immutable status';
 const before=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 stage='opening atlas';await page.getByRole('button',{name:'World',exact:true}).click();await page.waitForSelector('.atlas-reader');
 await page.locator('.atlas-directory').getByRole('button',{name:'Cities',exact:true}).click();
 await page.locator('.atlas-directory-list button').first().click();
 await page.getByRole('button',{name:'Population and movement',exact:true}).click();
 await page.getByRole('button',{name:'Population and movement',exact:true}).and(page.locator('[aria-current="page"]')).waitFor();
 await page.waitForFunction(()=>document.querySelector('.atlas-reading h2')?.textContent==='Population and movement');
 const movement=await page.locator('.atlas-reading').innerText();
 for(const text of ['Natural balance','Arrivals from nearby countryside','Lifetime rural arrivals'])if(!movement.includes(text))throw Error('Missing recorded city flow '+text);
 await capture('city-population.png');
 await page.locator('.atlas-directory').getByRole('button',{name:'States',exact:true}).click();
 await page.locator('.atlas-directory-list button').first().click();
 await page.getByRole('button',{name:'Administration and expansion',exact:true}).click();
 await page.getByRole('button',{name:'Administration and expansion',exact:true}).and(page.locator('[aria-current="page"]')).waitFor();
 await page.waitForFunction(()=>document.querySelector('.atlas-reading h2')?.textContent==='Administration and expansion');
 const administration=await page.locator('.atlas-reading').innerText();
 if(!administration.includes('Supported urban receipts')||!administration.includes('Urban residents outside supported reach'))throw Error('Missing actual administrative accounts');
 await capture('state-administration.png');
 stage='reading institutional continuity';
 await page.getByRole('button',{name:'Leadership and continuity',exact:true}).click();
 await page.getByRole('button',{name:'Leadership and continuity',exact:true}).and(page.locator('[aria-current="page"]')).waitFor();
 await page.waitForFunction(()=>document.querySelector('.atlas-reading h2')?.textContent==='Leadership and continuity');
 const continuity=await page.locator('.atlas-reading').innerText();
 for(const text of ['Recorded officeholders','Mean completed tenure','Recorded disputed successions'])if(!continuity.includes(text))throw Error('Missing recorded institutional history '+text);
 await capture('state-continuity.png');
 const campaigns=page.getByRole('button',{name:'Campaigns',exact:true});if(await campaigns.count()){await campaigns.click();await campaigns.and(page.locator('[aria-current="page"]')).waitFor();await page.waitForFunction(()=>document.querySelector('.atlas-reading h2')?.textContent==='Campaigns');await capture('state-campaigns.png');}
 await page.getByRole('button',{name:'Close',exact:true}).click();
 const after=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 if(after.Seconds!==before.Seconds||after.Population!==before.Population)throw Error('Reading changed historical state');
 await page.screenshot({path:output+'/map-hud.png'});
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: readable population flows, supported administration, leadership continuity, campaigns, and immutable viewer state.');
}catch(e){await page.screenshot({path:output+'/failure.png'});throw e;}
finally{clearInterval(heartbeat);await browser.close();}

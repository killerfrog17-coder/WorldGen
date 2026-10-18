import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/notables-browser-check.mjs <review.world> <output>');
const {Focus:focus,Family:family}=JSON.parse(await fs.readFile(path.join(path.dirname(source),'result.json'),'utf8'));
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
async function status(){for(let n=0;n<120;n++){try{return JSON.parse(await page.evaluate(()=>window.__wg.people('status')));}catch(e){if(!String(e).includes('already running'))throw e;await page.waitForTimeout(250);}}throw Error('Worker remained busy');}
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5092');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>document.querySelector('.error')?.textContent||[...document.querySelectorAll('button')].some(b=>b.textContent==='Start world'&&!b.disabled),{},{timeout:240000});
 assert.equal((await page.locator('.error').allTextContents()).length,0);
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});const before=await status();
 await page.getByRole('button',{name:'Atlas',exact:true}).click();const atlas=page.getByRole('dialog',{name:'World atlas'});
 await atlas.getByRole('button',{name:'Notable people',exact:true}).click();
 await atlas.getByRole('checkbox',{name:'Include historical records'}).check();
 async function open(person){await atlas.getByRole('textbox',{name:'Search atlas names'}).fill(person.Name);await atlas.locator(`.atlas-directory-list button[data-kind="notable"][data-id="${person.Id}"]`).click();await atlas.locator(`.atlas-article[data-kind="notable"][data-id="${person.Id}"]`).waitFor();}
 await open(focus);await atlas.getByRole('button',{name:'History',exact:true}).click();await atlas.getByRole('button',{name:'Recorded achievements',exact:true}).click();
 const deeds=await atlas.locator('.atlas-reading').innerText();assert.match(deeds,/recorded achievements/i);await page.screenshot({path:output+'/achievements.png'});
 await atlas.getByRole('button',{name:'People & place',exact:true}).click();await atlas.getByRole('button',{name:'Abilities and interests',exact:true}).click();await page.screenshot({path:output+'/abilities.png'});
 if(family){await open(family);await atlas.getByRole('button',{name:'People & place',exact:true}).click();await atlas.getByRole('button',{name:'Family and household',exact:true}).click();await page.screenshot({path:output+'/household.png'});}
 assert.equal((await status()).Seconds,before.Seconds);assert.equal(errors.length,0,errors.join('\n'));
 await fs.writeFile(output+'/result.json',JSON.stringify({focus,family,deeds,errors},null,2));console.log('PASS actual browser notable-person directory, biographies/deeds, ability chart, household relationships and paused time.');
}catch(e){console.log((await page.locator('body').innerText()).slice(-2500));await page.screenshot({path:output+'/failure.png'});throw e;}
finally{await browser.close();}

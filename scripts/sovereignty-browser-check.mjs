import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/sovereignty-browser-check.mjs <review.world> <output>');
const {Focus:focus}=JSON.parse(await fs.readFile(path.join(path.dirname(source),'result.json'),'utf8'));
assert.ok(focus,'The actual replay must contain a participating living town.');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
async function status(){for(let n=0;n<120;n++){try{return JSON.parse(await page.evaluate(()=>window.__wg.people('status')));}catch(error){if(!String(error).includes('already running'))throw error;await page.waitForTimeout(250);}}throw Error('Worker remained busy');}
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5092');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>document.querySelector('.error')?.textContent||[...document.querySelectorAll('button')].some(b=>b.textContent==='Start world'&&!b.disabled),{},{timeout:240000});
 assert.equal((await page.locator('.error').allTextContents()).length,0);
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});
 const before=await status();
 await page.getByRole('button',{name:'World [W]',exact:true}).click();
 const atlas=page.getByRole('dialog',{name:'World atlas'});
 await atlas.getByRole('button',{name:'Cities',exact:true}).click();
 await atlas.getByRole('textbox',{name:'Search atlas names'}).fill(focus.Name);
 await atlas.locator(`.atlas-directory-list button[data-kind="city"][data-id="${focus.City}"]`).click();
 await atlas.locator(`.atlas-article[data-kind="city"][data-id="${focus.City}"]`).waitFor();
 await atlas.getByRole('button',{name:'Power & relations',exact:true}).click();
 await atlas.getByRole('button',{name:'Allegiance and sovereignty',exact:true}).click();
 const reading=await atlas.locator('.atlas-reading').innerText();
 assert.match(reading,/Last governing transfer|Latest local dispute/);
 await page.screenshot({path:output+'/sovereignty.png'});
 await atlas.getByRole('button',{name:'Close',exact:true}).click();
 await page.evaluate(()=>document.activeElement?.blur());
 await page.keyboard.press('1');await page.waitForTimeout(500);
 const after=await status();assert.equal(after.Overlay,'States');assert.equal(after.Seconds,before.Seconds);
 assert.equal(errors.length,0,errors.join('\n'));
 await page.screenshot({path:output+'/states.png'});
 await fs.writeFile(output+'/result.json',JSON.stringify({focus,seconds:after.Seconds,reading,errors},null,2));
 console.log('PASS actual browser local-sovereignty article, real dated transfer/dispute, States hotkey, paused time and no JS errors.');
}catch(error){console.log((await page.locator('body').innerText()).slice(-3000));await page.screenshot({path:output+'/failure.png'});throw error;}
finally{await browser.close();}

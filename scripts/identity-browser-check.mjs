import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/identity-browser-check.mjs <new-rules.world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
async function status(){for(let n=0;n<120;n++){try{return JSON.parse(await page.evaluate(()=>window.__wg.people('status')));}catch{await page.waitForTimeout(250);}}throw Error('Worker remained busy');}
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5092');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>document.querySelector('.error')?.textContent||[...document.querySelectorAll('button')].some(b=>b.textContent==='Start world'&&!b.disabled),{},{timeout:240000});
 const loadError=await page.locator('.error').allTextContents();if(loadError.length)throw Error(loadError.join('\n'));
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});
 const initial=await status();
 await page.getByRole('button',{name:'Atlas',exact:true}).click();
 const atlas=page.getByRole('dialog',{name:'World atlas'});
 await atlas.getByRole('button',{name:'Ethnicities',exact:true}).click();
 await atlas.locator('.atlas-directory-list button[data-kind="ethnicity"]').first().click();
 await atlas.locator('.atlas-article[data-kind="ethnicity"]').waitFor();
 await atlas.getByRole('button',{name:'People & place',exact:true}).click();
 await atlas.getByRole('button',{name:'Inherited traditions',exact:true}).click();
 await atlas.locator('.atlas-reading').getByText('A compiled social-heritage index, not DNA or a measure of ethnic purity.',{exact:true}).waitFor();
 await page.screenshot({path:output+'/heritage.png'});
 await atlas.getByRole('button',{name:'Cities',exact:true}).click();
 await atlas.locator('.atlas-directory-list button[data-kind="city"]').first().click();
 await atlas.locator('.atlas-article[data-kind="city"]').waitFor();
 await atlas.getByRole('button',{name:'History',exact:true}).click();
 await atlas.getByRole('button',{name:'Local historical memory',exact:true}).click();
 await atlas.locator('.atlas-reading').getByText('Established local rights',{exact:true}).waitFor();
 await page.screenshot({path:output+'/local-memory.png'});
 await atlas.getByRole('button',{name:'Close',exact:true}).click();
 await page.locator('canvas').first().click({position:{x:800,y:450}});
 await page.keyboard.press('9');await page.waitForTimeout(700);
 const current=await status();assert.equal(current.Overlay,'Ethnicities');assert.ok(current.Legend.length>0);
 const chosen=JSON.parse(await page.evaluate(cell=>window.__wg.people('select',cell),current.Legend[0].Cell));
 assert.equal(chosen.Subject.Kind,'ethnicity');assert.equal(chosen.Subject.Id,current.Legend[0].ColorId);
 assert.equal(chosen.Seconds,initial.Seconds);assert.equal(errors.length,0,errors.join('\n'));
 await page.screenshot({path:output+'/identity-map.png'});
 await fs.writeFile(output+'/result.json',JSON.stringify({peoples:current.Legend.length,subject:chosen.Subject,errors},null,2));
 console.log('PASS browser ethnic-identity atlas, actual town memory, hotkey9, exact ethnic selection, paused time and no JS errors.');
}catch(error){console.log((await page.locator('body').innerText()).slice(-3000));await page.screenshot({path:output+'/failure.png'});throw error;}
finally{await browser.close();}

import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/speech-browser-check.mjs <new-rules.world> <output>');
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
 await page.getByRole('button',{name:'World [W]',exact:true}).click();
 const atlas=page.getByRole('dialog',{name:'World atlas'});
 await atlas.getByRole('button',{name:'Languages',exact:true}).click();
 await atlas.locator('.atlas-directory-list button[data-kind="language"]').first().click();
 await atlas.locator('.atlas-article[data-kind="language"]').waitFor();
 await atlas.getByRole('button',{name:'People & place',exact:true}).click();
 await atlas.getByRole('button',{name:'Sounds and reusable roots',exact:true}).click();
 await atlas.locator('.atlas-reading').getByText('Name structure',{exact:true}).waitFor();
 await page.screenshot({path:output+'/sounds.png'});
 await atlas.getByRole('button',{name:'Spoken languages here',exact:true}).click();
 await atlas.locator('.atlas-reading').getByText('Main home language',{exact:true}).waitFor();
 await page.screenshot({path:output+'/community.png'});
 await atlas.getByRole('button',{name:'Close',exact:true}).click();
 await page.locator('canvas').first().click({position:{x:800,y:450}});
 await page.keyboard.press('8');await page.waitForTimeout(700);
 const state=await status();assert.equal(state.Overlay,'Languages');assert.ok(state.Legend.length>0);
 const chosen=JSON.parse(await page.evaluate(cell=>window.__wg.people('select',cell),state.Legend[0].Cell));
 assert.equal(chosen.Subject.Kind,'language');assert.equal(chosen.Subject.Id,state.Legend[0].ColorId);
 assert.equal(chosen.Seconds,initial.Seconds);assert.equal(errors.length,0,errors.join('\n'));
 await page.screenshot({path:output+'/languages-map.png'});
 await fs.writeFile(output+'/result.json',JSON.stringify({languages:state.Legend.length,subject:chosen.Subject,errors},null,2));
 console.log('PASS browser language atlas, local domains, hotkey8, exact language selection, paused time and no JS errors.');
}catch(error){console.log((await page.locator('body').innerText()).slice(-3000));await page.screenshot({path:output+'/failure.png'});throw error;}
finally{await browser.close();}

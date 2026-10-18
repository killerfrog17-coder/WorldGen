import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2]??'artifacts/history-5/tests/Earth.world';
const output=process.env.LOCAL_HISTORY_BROWSER_OUTPUT??'artifacts/history-10/browser';
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
const heartbeat=setInterval(()=>console.log('Local historical model browser check running'),20000);heartbeat.unref();
try{
 await page.goto('http://127.0.0.1:5094');
 await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Historical model upgrade check',bytes,{CellCount:5000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.waitForFunction(()=>!document.querySelector('.progress-card')&&[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:240000});
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 const before=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 if(!before.CanUpgradeHistory)throw Error('Legacy rules need an explicit upgrade choice');
 await page.locator('#globe').focus();await page.keyboard.press('Escape');
 await page.getByRole('button',{name:'Update simulation rules',exact:true}).click();
 await page.waitForFunction(()=>![...document.querySelectorAll('button')].some(b=>b.textContent==='Update simulation rules'),{},{timeout:120000});
 const upgraded=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 if(upgraded.CanUpgradeHistory||upgraded.Seconds!==before.Seconds||upgraded.EpochYear!==before.EpochYear)throw Error('Upgrade changed historical dates or failed to change rules');
 await page.screenshot({path:output+'/upgraded-pause-menu.png'});
 await page.getByRole('button',{name:'Resume',exact:true}).click();
 await page.getByRole('button',{name:'World [W]',exact:true}).click();
 await page.waitForSelector('.atlas-reader',{timeout:120000});
 await page.screenshot({path:output+'/atlas.png'});
 await page.getByRole('button',{name:'Close',exact:true}).click();
 const saved=await page.evaluate(async()=>{const id=await window.__wg.saveLibraryWorld('Upgraded historical save');return (await import('/world-library.js')).read(id);});
 await fs.writeFile(output+'/upgraded.world',Buffer.from(saved,'base64'));
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: explicit historical upgrade, unchanged time/epoch, atlas, and binary save.');
}catch(e){await page.screenshot({path:output+'/failure.png'});throw e;}
finally{clearInterval(heartbeat);await browser.close();}

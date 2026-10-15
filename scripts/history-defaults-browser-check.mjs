import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2]??'src/WorldGen.Web/wwwroot/world/Earth.world';
const output=process.env.HISTORY_CHECK_OUTPUT??'artifacts/history-7/defaults-browser';
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));
try {
 await page.goto('http://127.0.0.1:5094');
 await page.evaluate(async bytes=>{const lib=await import('/world-library.js');await lib.write('Defaults check',bytes,{CellCount:20000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.waitForFunction(()=>[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:240000});
 await page.getByRole('button',{name:'View',exact:true}).click();
 if(await page.getByLabel('Base map',{exact:true}).inputValue()!=='Climate'||await page.getByLabel('Historical overlay').inputValue()!=='States')throw Error('Wrong default layers');
 if(Number(await page.getByLabel('Map opacity',{exact:true}).inputValue())!==0||Number(await page.getByLabel('Population opacity',{exact:true}).inputValue())!==1)throw Error('Wrong default opacity');
 await page.evaluate(()=>document.activeElement?.blur());await page.keyboard.press('=');await page.waitForTimeout(100);
 if(Number(await page.getByLabel('Map opacity',{exact:true}).inputValue())!==.25)throw Error('Base keyboard increment is not 25%');
 await page.keyboard.press('Shift+-');await page.waitForTimeout(100);
 if(Number(await page.getByLabel('Population opacity',{exact:true}).inputValue())!==.75)throw Error('Overlay keyboard increment is not 25%');
 await page.getByLabel('Map opacity',{exact:true}).fill('0');await page.getByLabel('Population opacity',{exact:true}).fill('1');
 const scheduled=JSON.parse(await page.evaluate(async()=>{const map=await import('/globe.js');return await map.people('calendar');}));
 await page.getByRole('button',{name:'Step',exact:true}).click();
 await page.waitForFunction(()=>!document.querySelector('.sim-bar strong')?.textContent.startsWith('Year 0')&&[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:90000});
 const summary=JSON.parse(await page.evaluate(async()=>{const map=await import('/globe.js');return await map.people('status');}));
 if(!summary.Started||scheduled.NextEventSeconds<=scheduled.Seconds||summary.Seconds!==scheduled.NextEventSeconds||summary.Seconds%31536000===0)throw Error('A geography-only save must begin at the actual seeded, sub-year next event, not a hard-coded year window');
 await page.screenshot({path:output+'/defaults-and-first-step.png'});
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: Climate/States, white/opaque defaults, 25% opacity keys, geography-only save starts at its next scheduled event.');
}catch(e){await page.screenshot({path:output+'/failure.png'});throw e;}
finally{await browser.close();}

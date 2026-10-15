import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2]??'D:/WorldGen-calendar-20261013/history-tests/Earth.world';
const output=process.env.CALENDAR_CHECK_OUTPUT??'artifacts/calendar/browser';await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));const heartbeat=setInterval(()=>console.log('Calendar browser check running'),20000);heartbeat.unref();
try{
 await page.goto('http://127.0.0.1:5094');
 await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Calendar test',bytes,{CellCount:5000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.waitForFunction(()=>!document.querySelector('.progress-card')&&!document.querySelector('.sim-bar strong')?.textContent.startsWith('Year 0')&&[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:240000});
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 const before=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 async function rate(value,unit){await page.getByLabel('Simulation time rate').fill(String(value));await page.getByLabel('Simulation rate unit').selectOption(String(unit));await page.getByRole('button',{name:'Set',exact:true}).click();await page.waitForTimeout(150);}
 await rate(1,0);const moved=JSON.parse(await page.evaluate(()=>window.__wg.people('time',3661)));
 if(!moved.ClockOnly||moved.Seconds!==before.Seconds+3661||moved.Years!==before.Years)throw Error('Clock-only seconds rebuilt domain or lost precision');
 if(!(await page.locator('.sim-bar strong').innerText()).includes('Jan 1 · 01:01'))throw Error('Real-time display should show minutes without seconds');
 await page.screenshot({path:output+'/real-time.png'});
 await rate(.5,3);if(!(await page.locator('.sim-bar strong').innerText()).endsWith('01h'))throw Error('Half-day rate should include hours, no minutes');
 await rate(14,3);if(!(await page.locator('.sim-bar strong').innerText()).includes('Jan 1')||(await page.locator('.sim-bar strong').innerText()).includes('01h'))throw Error('Two-week rate should display days');
 await rate(6,5);if(!(await page.locator('.sim-bar strong').innerText()).endsWith('Jan'))throw Error('Six-month rate should display months only');
 await rate(1000,6);if((await page.locator('.sim-bar strong').innerText()).includes('Jan'))throw Error('Millennium/second rate should display year only');
 await page.screenshot({path:output+'/fast-time.png'});
 await rate(1,0);await page.locator('#globe').focus();await page.keyboard.press(' ');await page.waitForTimeout(2400);await page.keyboard.press(' ');await page.waitForFunction(()=>[...document.querySelectorAll('.sim-bar button')].some(b=>b.textContent==='Step'&&!b.disabled),{},{timeout:90000});
 const live=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));if(live.Seconds<=moved.Seconds||live.Seconds>moved.Seconds+5)throw Error('Real-time playback is not bounded by elapsed time');
 const paused=live.Seconds;await page.waitForTimeout(1200);const still=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));if(still.Seconds!==paused)throw Error('Paused calendar keeps advancing');
 await page.getByRole('button',{name:'Step',exact:true}).click();await page.waitForFunction(year=>document.querySelector('.sim-bar strong')?.textContent.includes((year+1).toLocaleString('en-US')),before.Years-(before.EpochYear>=0?before.EpochYear:0),{timeout:90000});
 const next=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));if(next.Years!==before.Years+1||next.Seconds%31536000!==0)throw Error('Step must stop at the next scheduled event');
 const id=await page.evaluate(()=>window.__wg.saveLibraryWorld('Calendar roundtrip'));
 const packed=await page.evaluate(async id=>(await import('/world-library.js')).read(id),id);await fs.writeFile(output+'/continued.world',Buffer.from(packed,'base64'));
 await page.screenshot({path:output+'/next-event.png'});if(errors.length)throw Error(errors.join('\n'));console.log('PASS calendar: rate-dependent dates, exact seconds, clock-only rendering, real-time playback, pause, next event and save.');
}catch(e){await page.screenshot({path:output+'/failure.png'});throw e;}finally{clearInterval(heartbeat);await browser.close();}

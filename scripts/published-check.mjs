import {chromium} from 'playwright';
import fs from 'node:fs';
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
try{
await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5081');
await page.getByRole('button',{name:'Cancel',exact:true}).click();
await page.getByLabel('Preset',{exact:true}).selectOption('Preview');
await page.getByRole('button',{name:'Generate world',exact:false}).click();
await page.waitForFunction(()=>Number(document.querySelector('.progress-card strong')?.textContent.replace('%',''))>=10,{},{timeout:600000});
await page.getByLabel('Seed',{exact:true}).fill('73422');await page.getByLabel('Seed',{exact:true}).blur();
await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready'||document.querySelector('[role=alert]'),{},{timeout:600000});
if(await page.locator('[role=alert]').count())throw new Error(await page.locator('[role=alert]').textContent());
const w=JSON.parse(await page.evaluate(async()=> (await import('/globe.js')).snapshot()));
const expected=JSON.parse(fs.readFileSync('tests/golden/v3/ordinary.hashes.json','utf8'));
if(w.Stages.some((s,i)=>s.Hash!==expected[i].Hash))throw new Error('Published build hash mismatch');
if(!await page.getByText('Settings changed. Regenerate to apply.').count())throw new Error('Lost mid-generation edits');
await page.screenshot({path:'artifacts/browser/published.png',fullPage:true});
if(errors.length)throw new Error(errors.join('\n'));console.log('PASS published static app, all-stage CLI parity, retained mid-generation edits, no browser errors');
}catch(e){console.error(e);process.exitCode=1;}finally{await browser.close();}

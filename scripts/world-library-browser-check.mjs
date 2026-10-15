import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});page.setDefaultTimeout(120000);
process.on('SIGINT',async()=>{await browser.close();process.exit(130);});
const errors=[];page.on('pageerror',error=>{errors.push(String(error));console.log('PAGE ERROR',String(error));});page.on('console',message=>{if(message.type()==='error')console.log('CONSOLE ERROR',message.text());});
const progress=setInterval(async()=>{try{console.log('STATUS',await page.locator('.top-status').innerText({timeout:2000}),await page.locator('.progress-card').allTextContents(),await page.locator('[role=alert]').allTextContents());}catch{}},20000);
const ready=async()=>{await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready'||document.querySelector('[role=alert]'),{},{timeout:600000});if(await page.locator('[role=alert]').count())throw new Error(await page.locator('[role=alert]').innerText());};
const snapshot=()=>page.evaluate(async()=>JSON.parse((await import('/globe.js')).snapshot()));
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5082');
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByRole('button',{name:'Open Earth',exact:true}).click();await ready();
 const earth=await snapshot();assert.equal(earth.Recipe.TerrainSource,'Earth');assert.ok(earth.CellCount>=5000);
 console.log('PASS bundled Earth full snapshot opens through UI:',earth.CellCount,'cells');
 await page.getByLabel('World name',{exact:true}).fill('Library roundtrip Earth');
 await page.getByRole('button',{name:'Save a new copy',exact:true}).click();
 await page.getByText('Saved a complete world copy in this browser.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Open Library roundtrip Earth',exact:true}).waitFor();
 await page.reload();await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByRole('button',{name:'Open Library roundtrip Earth',exact:true}).click();await ready();
 const restored=await snapshot();assert.deepEqual(restored.Stages,earth.Stages);assert.deepEqual(restored.Recipe,earth.Recipe);
 console.log('PASS named save survives reload and restores every stage hash and recipe');
 await page.getByLabel('View',{exact:true}).selectOption('Map');await ready();
 await page.getByRole('button',{name:'Climate',exact:true}).click();await ready();
 fs.mkdirSync('artifacts/world-library/browser',{recursive:true});await page.screenshot({path:'artifacts/world-library/browser/earth-library.png',fullPage:true});
 assert.deepEqual(errors,[]);console.log('PASS restored Earth climate map renders without browser errors');
}finally{clearInterval(progress);await browser.close();}

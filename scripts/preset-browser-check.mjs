import { chromium } from 'playwright';
import fs from 'node:fs/promises';
await fs.mkdir('artifacts/presets-332',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto('http://127.0.0.1:5094');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 const select=page.getByLabel('Map type',{exact:true});
 const names=await select.locator('option').allTextContents();
 if(names.length!==21||names[0]!=='Continents'||!names.includes('Terra'))throw Error('Preset catalog missing/out of order');
 for(const name of names)await select.selectOption(name);
 await select.selectOption('Archipelago');
 if(await page.getByLabel('Plates min',{exact:true}).inputValue()!=='350'||await page.getByLabel('Continents min',{exact:true}).inputValue()!=='20')throw Error('Preset minima not displayed correctly');
 await page.screenshot({path:'artifacts/presets-332/setup-archipelago.png'});
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS all 20 preset selections plus Custom; no page errors');
} finally {await browser.close();}

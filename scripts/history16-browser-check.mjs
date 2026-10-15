// Read/render verification only; all historical rules stay in shared .NET.
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/history16-browser-check.mjs <world> <output>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.setDefaultTimeout(240000);page.on('pageerror',error=>errors.push(String(error)));
let stage='opening';const heartbeat=setInterval(()=>console.log('History16 UI: '+stage),20000);heartbeat.unref();
try{
 await page.goto(process.env.WORLDGEN_URL??'http://127.0.0.1:5096');
 await page.evaluate(async data=>{const library=await import('/world-library.js');await library.write('History16 UI check',data,{CellCount:20000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();
 stage='loading actual saved history';await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.locator('.sim-bar').waitFor();await page.waitForFunction(()=>!document.querySelector('.progress-card'),{},{timeout:240000});
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 const status=()=>page.evaluate(async()=>{for(let tries=0;tries<100;tries++){try{return JSON.parse(await window.__wg.people('status'));}catch(error){if(!String(error).includes('already running'))throw error;await new Promise(resolve=>setTimeout(resolve,100));}}throw Error('Worker stayed busy');});
 const before=await status();
 stage='political labels and map';await page.getByRole('button',{name:'View',exact:true}).click();
 await page.getByRole('button',{name:'Switch to map',exact:true}).click();
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading);
 const expected=before.Legend.map(entry=>entry.Name.replace(/ State$/,''));
 await page.getByLabel('Historical overlay').selectOption('Civilizations');
 const civilizations=await status();
 if(expected.length&&!expected.every(name=>civilizations.Marks.some(mark=>!mark.City&&mark.Name===name)))throw Error('Civilization colors are incorrectly labeling broader cultures instead of their individual states');
 await page.locator('.society-dock button').nth(2).click();
 await page.waitForFunction(()=>!document.querySelector('.society-panel.selection'));
 await page.waitForTimeout(500);await page.screenshot({path:output+'/earth-civilizations.png'});
 await page.getByRole('button',{name:'View',exact:true}).click();await page.getByLabel('Historical overlay').selectOption('States');
 const after=await status();if(after.Seconds!==before.Seconds||after.Population!==before.Population)throw Error('Inspection changed history');
 await page.locator('.society-dock button').nth(2).click();await page.waitForTimeout(300);await page.screenshot({path:output+'/earth-states.png'});
 if(errors.length)throw Error(errors.join('\n'));
 await fs.writeFile(output+'/checks.json',JSON.stringify({years:after.Years,population:after.Population,stateLabels:expected,errors},null,2));
 console.log('PASS: state labels in civilization mode, folded controls, unchanged history and real browser screenshots');
}finally{clearInterval(heartbeat);await browser.close();}

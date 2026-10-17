import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const [source,output]=process.argv.slice(2);
if(!source||!output)throw Error('Usage: node scripts/map-marks-browser-check.mjs <saved.world> <output-folder>');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')console.log(m.text());});
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5091');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 console.log('Browser renderer initialized; loading saved world.');
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 await page.waitForFunction(()=>{const button=[...document.querySelectorAll('button')].find(b=>b.textContent==='Start world');return button&&!button.disabled;},{},{timeout:180000});
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 console.log('Saved world loaded; checking view and hotkeys.');
 await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:180000});
 let before=await page.evaluate(()=>window.__wg.viewInfo());assert.equal(before.cityMarkerMode,1);
 const status=JSON.parse(await page.evaluate(()=>window.__wg.people('status')));
 const capital=status.Marks.find(m=>m.City&&m.Capital);assert.ok(capital,'Loaded world needs a living capital.');
 await page.locator('canvas').first().hover();await page.mouse.wheel(0,-900);await page.waitForTimeout(350);
 await page.evaluate(p=>window.__wg.focusAtlas(Math.atan2(p.Y,p.X)*180/Math.PI,Math.asin(p.Z)*180/Math.PI),capital);
 await page.waitForTimeout(200);
 if(!before.options.ShowRivers)await page.keyboard.press('r');
 await page.waitForFunction(()=>window.__wg.viewInfo().options.ShowRivers&&!window.__wg.viewInfo().loading);
 await page.screenshot({path:output+'/capitals-rivers.png'});
 const steps=[2,3,4,0,1],counts={1:before.visibleCityMarkers};
 for(const expected of steps){await page.keyboard.press('c');await page.waitForFunction(m=>window.__wg.viewInfo().cityMarkerMode===m,expected);const info=await page.evaluate(()=>window.__wg.viewInfo());assert.equal(info.loading,false);counts[expected]=info.visibleCityMarkers;if([2,4].includes(expected))await page.screenshot({path:output+'/cities-mode-'+expected+'.png'});}
 assert.equal(counts[0],0);assert.ok(counts[2]>=counts[3]&&counts[3]>=counts[4]);
 await page.keyboard.press('r');await page.waitForFunction(()=>!window.__wg.viewInfo().options.ShowRivers&&!window.__wg.viewInfo().loading);
 await page.screenshot({path:output+'/rivers-off.png'});
 assert.equal(errors.length,0,errors.join('\n'));await fs.writeFile(output+'/result.json',JSON.stringify({counts,views:await page.evaluate(()=>window.__wg.viewInfo()),errors},null,2));
 console.log('PASS browser: compiled stroke shader/new geometry protocol; R cached toggle; C five-mode cycle',counts);
}catch(error){await page.screenshot({path:output+'/failure.png'});throw error;}
finally{await browser.close();}

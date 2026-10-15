import { chromium } from 'playwright';
import fs from 'node:fs';
fs.mkdirSync('artifacts/browser',{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});
process.on('SIGINT',async()=>{await browser.close();process.exit(130);});
page.setDefaultTimeout(120000);
const progressTimer=setInterval(async()=>{try{console.log('PROGRESS',await page.locator('.progress-card').innerText({timeout:1000}));}catch{}},30000);
const errors=[];page.on('pageerror',e=>{errors.push(String(e));console.log('PAGE ERROR',String(e));});page.on('console',m=>{if(m.type()==='error')console.log('CONSOLE',m.text());});
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5080');
 await page.getByText('Advanced import / export',{exact:true}).click();
 // Use the shared Preview preset for the interactive browser suite. Full-detail
 // rendering is exercised separately in the native Unity player.
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByLabel('Preset',{exact:true}).selectOption('Preview');
 await page.getByRole('button',{name:'Generate world',exact:false}).click();
 console.log('Generating shared Preview recipe (20,000 terrain cells).');
 await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready'||document.querySelector('[role=alert]'),{},{timeout:600000});
 console.log('STATUS',await page.locator('.top-status').textContent());
 if(await page.locator('[role=alert]').count())throw new Error(await page.locator('[role=alert]').textContent());
 await page.screenshot({path:'artifacts/browser/desktop.png',fullPage:true});
 const first=JSON.parse(await page.evaluate(async()=> (await import('/globe.js')).snapshot()));
 fs.writeFileSync('artifacts/browser/ordinary.hashes.json',JSON.stringify(first.Stages));
 console.log('HASH',first.Stages.at(-1).Hash);
 const expected=JSON.parse(fs.readFileSync('tests/golden/v3/ordinary.hashes.json','utf8'));
 if(first.Stages.some((s,i)=>s.Hash!==expected[i].Hash))throw new Error('Browser/CLI stage hash mismatch');
 if(!process.argv.includes('--interaction-only')){
 await page.getByRole('button',{name:'Regenerate / reapply'}).click();
 await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready',{},{timeout:600000});
 const repeat=JSON.parse(await page.evaluate(async()=> (await import('/globe.js')).snapshot()));
 if(JSON.stringify(first.Stages)!==JSON.stringify(repeat.Stages))throw new Error('Browser repeat mismatch');
 console.log('PASS browser regeneration hashes');
 const save=page.waitForEvent('download');await page.getByRole('button',{name:'Export recipe',exact:true}).click();const recipeDownload=await save;await recipeDownload.saveAs('artifacts/browser/saved.recipe.json');
 if(JSON.stringify(JSON.parse(fs.readFileSync('artifacts/browser/saved.recipe.json','utf8')))!==JSON.stringify(first.Recipe))throw new Error('Saved recipe mismatch');
 console.log('PASS saved recipe');
 await page.locator('.file-button').filter({hasText:'Import recipe'}).locator('input').setInputFiles('artifacts/browser/saved.recipe.json');
 await page.getByRole('button',{name:'Regenerate / reapply'}).click();await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready',{},{timeout:600000});
 const loaded=JSON.parse(await page.evaluate(async()=> (await import('/globe.js')).snapshot()));if(loaded.Stages.some((s,i)=>s.Hash!==first.Stages[i].Hash))throw new Error('Loaded recipe reconstruction mismatch');
 console.log('PASS loaded-recipe reconstruction');
 const exportWait=page.waitForEvent('download');await page.getByRole('button',{name:'Export result'}).click();const exported=await exportWait;await exported.saveAs('artifacts/browser/exported.result.json');
 if(JSON.parse(fs.readFileSync('artifacts/browser/exported.result.json','utf8')).Stages.at(-1).Hash!==first.Stages.at(-1).Hash)throw new Error('Result export mismatch');
 const heightWait=page.waitForEvent('download');await page.getByRole('button',{name:'Export heightmap',exact:true}).click();await(await heightWait).saveAs('artifacts/browser/heightmap.pgm');if(fs.readFileSync('artifacts/browser/heightmap.pgm').subarray(0,2).toString()!=='P5')throw new Error('Invalid heightmap');
 console.log('PASS result and heightmap exports');
 }
 await page.evaluate(async()=>{globalThis.worldgenTest=await import('/globe.js');});
 const info=()=>page.evaluate(()=>globalThis.worldgenTest.viewInfo());
 async function action(change,expected={}){
   const before=(await info()).revision;await change();
   await page.waitForFunction(args=>{const v=globalThis.worldgenTest.viewInfo();return v.revision>args.before&&!v.loading&&document.querySelector('.top-status')?.textContent==='World ready'&&(args.expected.Layer===undefined||v.options.Layer===args.expected.Layer)&&(args.expected.Map===undefined||v.map===args.expected.Map);},{before,expected},{timeout:120000});
   const committed=await info();console.log('VIEW',JSON.stringify({revision:committed.revision,map:committed.map,layer:committed.options.Layer,loading:committed.loading,expected}));
   if(await page.locator('[role=alert]').count())throw new Error(await page.locator('[role=alert]').textContent());
 }
 for(const entry of await page.getByLabel('Diagnostic layer').locator('option').evaluateAll(options=>options.map(o=>({value:o.value,id:Number(o.dataset.layer)}))))await action(()=>page.getByLabel('Diagnostic layer').selectOption(entry.value),{Layer:entry.id});
 for(const projection of ['Map','Globe']){
   console.log('Checking',projection,'views');
   await action(()=>page.getByLabel('View',{exact:true}).selectOption(projection),{Map:projection==='Map'});
   if((await info()).map!==(projection==='Map'))throw new Error('Projection did not update');
   for(const layer of ['Terrain','Satellite','Climate','Heightmap']){
     await action(()=>page.locator('.map-tabs').getByRole('button',{name:layer,exact:true}).click(),{Layer:['Terrain','Satellite','Climate','Heightmap'].indexOf(layer)});
     await page.screenshot({path:'artifacts/browser/'+projection.toLowerCase()+'-'+layer.toLowerCase()+'.png',fullPage:true});
   }
 }
 await action(()=>page.getByLabel('View',{exact:true}).selectOption('Map'),{Map:true});
 const beforeLongitude=(await info()).revision;
 await page.getByLabel('Center longitude',{exact:true}).fill('179');
 await page.waitForFunction(()=>globalThis.worldgenTest.viewInfo().centerLongitude===179);
 if((await info()).revision!==beforeLongitude)throw new Error('Longitude rebuilt the mesh');
 await action(()=>page.getByLabel('View',{exact:true}).selectOption('Globe'),{Map:false});
 await action(()=>page.getByLabel('Wireframe').check());await action(()=>page.getByLabel('Show plates').check());await action(()=>page.getByLabel('Grid spacing').selectOption('15'));
 const overlays=await info();if(!overlays.options.Wireframe||!overlays.options.ShowPlates||overlays.options.GridSpacing!==15)throw new Error('Overlay options mismatch');
 await action(()=>page.getByLabel('Diagnostic layer').selectOption('Terrain'),{Layer:0});
 const size=await page.locator('#globe').boundingBox();await page.locator('#globe').click({position:{x:size.width/2,y:size.height/2}});
 await page.waitForFunction(()=>document.querySelector('.inspection pre')?.textContent.startsWith('Cell '),{},{timeout:120000});
 console.log('INSPECTION',await page.locator('.inspection pre').textContent());
 await page.getByLabel('Preset',{exact:true}).selectOption('500 plates');await page.getByLabel('Detail',{exact:true}).fill('20000');await page.getByLabel('Detail',{exact:true}).press('Tab');await page.getByRole('button',{name:'Regenerate / reapply'}).click();
 await page.waitForFunction(()=>Number(document.querySelector('.progress-card strong')?.textContent.replace('%',''))>=10,{},{timeout:600000});
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready');
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:'artifacts/browser/mobile.png',fullPage:true});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);if(overflow)throw new Error('Mobile horizontal overflow');
 if(errors.length)throw new Error(errors.join('\n'));
 console.log('PASS browser/CLI hashes, all views, longitude, wireframe, plates, grid, picking, mid-generation cancellation and responsive layout'+(process.argv.includes('--interaction-only')?'':'; repeat, recipe save/load/reconstruction, result and heightmap exports'));
}catch(e){await page.screenshot({path:'artifacts/browser/failure.png',fullPage:true});console.error(e);process.exitCode=1;}finally{clearInterval(progressTimer);await browser.close();}

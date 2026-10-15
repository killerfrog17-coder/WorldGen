import { chromium } from 'playwright';
import fs from 'node:fs';
const output=process.env.WORLDGEN_CHECK_OUTPUT||'artifacts/hydrology/browser'; fs.mkdirSync(output,{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1100}});
page.setDefaultTimeout(120000); const errors=[]; page.on('pageerror',e=>errors.push(String(e)));
const step=message=>console.log(new Date().toISOString(),message);
const ready=async()=>{await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent==='World ready'||document.querySelector('[role=alert]'),{},{timeout:600000});if(await page.locator('[role=alert]').count())throw new Error(await page.locator('[role=alert]').textContent());};
const snapshot=async()=>JSON.parse(await page.evaluate(async()=>(await import('/globe.js')).snapshot()));
const fractionalState=async label=>{
 const download=page.waitForEvent('download');
 await page.evaluate(async()=>(await import('/globe.js')).exportWorld('json'));
 const file=output+'/'+label+'.json'; await(await download).saveAs(file);
 const world=JSON.parse(fs.readFileSync(file,'utf8')),h=world.Hydrology,n=world.Surface.Cells.length;
 for(const name of ['NorthernSpring','NorthernSummer','NorthernAutumn','NorthernWinter']){
  const season=world.Climate[name];
  if(season?.TemperatureTenthsC?.length!==n||season?.PrecipitationMm?.length!==n)throw new Error('Missing four-season climate: '+name);
 }
 if(h.LakeCoverageFraction?.length!==n||h.LakeWaterVolumeM3?.length!==n)throw new Error('Fractional lake arrays missing from saved world');
 if(h.LakeCoverageFraction.some(f=>!Number.isFinite(f)||f<0||f>1)||h.LakeWaterVolumeM3.some(v=>!Number.isFinite(v)||v<0))throw new Error('Invalid fractional lake data');
 const hidden=h.LakeCoverageFraction.findIndex(f=>f>0&&f<.9);
 if(hidden<0)throw new Error('Earth fixture has no retained sub-cell lakes');
 for(const body of h.Waters.filter(w=>w.Kind!==0&&w.Kind!=='Ocean')){
  if(!Number.isFinite(body.SurfaceElevationM))throw new Error('Precise lake water level missing');
  const area=body.Cells.reduce((a,i)=>a+world.Surface.Cells[i].AreaM2*h.LakeCoverageFraction[i],0);
  if(Math.abs(area-body.AreaM2)>1)throw new Error('Stored lake area is not fractional-cell weighted');
 }
 await page.evaluate(async cell=>(await import('/globe.js')).pickCell(cell),hidden);
 await page.getByText(/Estimated lake coverage:/).waitFor();
 if(!(await page.getByText(/Estimated lake coverage:/).textContent()).includes('sub-cell lake (not painted)'))throw new Error('Hidden lake inspection unavailable');
 return JSON.stringify({climate:world.Climate,coverage:h.LakeCoverageFraction,volume:h.LakeWaterVolumeM3,waters:h.Waters,rivers:h.Rivers});
};
const reusedWorld=process.env.WORLDGEN_WORLD;
if(reusedWorld){
 try{
  step('opening WorldGen for direct compact-world load');
  await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5081');
  await page.getByText('Advanced import / export',{exact:true}).click();
  await page.getByRole('button',{name:'Cancel',exact:true}).click();
  await page.locator('.file-button').filter({hasText:'Import world'}).locator('input').setInputFiles(reusedWorld); await ready();
  step('compact Earth load and initial view complete');
  const earth=await snapshot(),expected=JSON.parse(fs.readFileSync('tests/golden/v3/earth.hashes.json','utf8'));
  if(earth.Recipe.TerrainSource!=='Earth'||earth.Stages.length!==expected.length||earth.Stages.some((s,i)=>s.Hash!==expected[i].Hash))throw new Error('Loaded Earth snapshot mismatch');
  await page.getByLabel('Rivers',{exact:true}).check(); await ready(); await page.getByLabel('View',{exact:true}).selectOption('Map'); await ready();
  await page.screenshot({path:output+'/earth-rivers.png',fullPage:true}); step('river map screenshot complete');
  const recipeDownload=page.waitForEvent('download');await page.getByRole('button',{name:'Export recipe',exact:true}).click();await(await recipeDownload).saveAs(output+'/restored.recipe.json');
  if(JSON.parse(fs.readFileSync(output+'/restored.recipe.json','utf8')).TerrainSource!=='Earth')throw new Error('Loaded world settings not restored');
  if(errors.length)throw new Error(errors.join('\n'));
  console.log('PASS compact Earth load, river map, restored hash and settings');
 }finally{await browser.close();}
 process.exit(0);
}
try {
 step('opening WorldGen');
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5081');
 await page.getByText('Advanced import / export',{exact:true}).click();
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await page.getByLabel('Preset',{exact:true}).selectOption('Earth');
 // Preview-size Earth for cross-host exactness; visual checks use larger meshes separately.
 await page.getByLabel('Detail',{exact:true}).fill('20000');
 await page.getByLabel('Detail',{exact:true}).dispatchEvent('change');
 step('generating 20k Earth world');
 await page.getByRole('button',{name:/Generate world|Regenerate \/ reapply/}).click(); await ready();
 step('Earth generation and initial view complete');
 let earth=await snapshot(); if(earth.Recipe.TerrainSource!=='Earth')throw new Error('Earth source lost');
 const expected=JSON.parse(fs.readFileSync('tests/golden/v3/earth.hashes.json','utf8'));
 if(earth.Stages.length!==expected.length||earth.Stages.some((s,i)=>s.Hash!==expected[i].Hash))throw new Error('Earth WASM/native mismatch');
 const fractions=await fractionalState('earth-before-load');
 await page.getByLabel('Rivers',{exact:true}).check(); await ready();
 await page.getByLabel('View',{exact:true}).selectOption('Map'); await ready();
 step('river map view complete; capturing screenshot');
 await page.screenshot({path:output+'/earth-rivers.png',fullPage:true});
 const download=page.waitForEvent('download'); await page.getByRole('button',{name:'Export world backup',exact:true}).click(); await(await download).saveAs(output+'/earth.world');
 step('compact world download complete');
 const packed=fs.readFileSync(output+'/earth.world');
 if(packed.length<32||packed.subarray(0,8).toString()!=='WGWRLD01')throw new Error('Invalid compact world snapshot');
 await page.getByLabel('Preset',{exact:true}).selectOption('Preview');
 await page.getByLabel('Detail',{exact:true}).fill('5000');
 await page.getByLabel('Detail',{exact:true}).dispatchEvent('change');
 step('generating replacement 5k Preview world');
 await page.locator('details').filter({has:page.getByLabel('Lake amount slider',{exact:true})}).evaluate(el=>el.open=true);
 const lakeSlider=page.getByLabel('Lake amount slider',{exact:true});
 if(await lakeSlider.getAttribute('min')!=='0'||await lakeSlider.getAttribute('max')!=='1000')throw new Error('Lake slider limits');
 await lakeSlider.fill('0');await lakeSlider.dispatchEvent('input');
 await page.getByRole('button',{name:/Generate world|Regenerate \/ reapply/}).click(); await ready();
 const dry=await snapshot(); if(dry.Recipe.LakeAmountPercent!==0)throw new Error('Lake slider did not update authoritative recipe');
 const dryDownload=page.waitForEvent('download');await page.evaluate(async()=>(await import('/globe.js')).exportWorld('json'));
 await(await dryDownload).saveAs(output+'/no-lakes.json');
 const dryWorld=JSON.parse(fs.readFileSync(output+'/no-lakes.json','utf8'));
 if(dryWorld.Hydrology.Waters.some(w=>w.Kind!==0&&w.Kind!=='Ocean')||dryWorld.Hydrology.LakeCoverageFraction.some(f=>f!==0))throw new Error('0% lakes retained inland water');
 step('replacement world complete; uploading compact Earth world');
 await page.locator('.file-button').filter({hasText:'Import world'}).locator('input').setInputFiles(output+'/earth.world');
 await page.waitForFunction(()=>document.querySelector('.top-status')?.textContent!=='World ready'); await ready();
 step('compact Earth load and view complete');
 const restored=await snapshot(); if(restored.Stages.at(-1).Hash!==earth.Stages.at(-1).Hash||restored.Recipe.TerrainSource!=='Earth')throw new Error('World snapshot not restored');
 if(await fractionalState('earth-after-load')!==fractions)throw new Error('Fractional lakes or river metadata changed across compact save/load');
 const recipeDownload=page.waitForEvent('download');await page.getByRole('button',{name:'Export recipe',exact:true}).click();await(await recipeDownload).saveAs(output+'/restored.recipe.json');
 if(JSON.parse(fs.readFileSync(output+'/restored.recipe.json','utf8')).TerrainSource!=='Earth')throw new Error('Loaded world settings not restored');
 if(errors.length)throw new Error(errors.join('\n'));
 console.log('PASS Earth all-stage parity, river toggle, fractional lake inspection/data, full world save/load, restored settings');
}finally{await browser.close();}

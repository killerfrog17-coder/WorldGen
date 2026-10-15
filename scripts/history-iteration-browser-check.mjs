import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2]??'artifacts/history-3/tests/Earth-preview.world';
const output=process.env.HISTORY_CHECK_OUTPUT??'artifacts/history-3/browser';
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
async function readPeople(command){
 const deadline=Date.now()+120000;
 for(;;){try{return await page.evaluate(command=>window.__wg.people(command),command);}catch(error){
  if(!String(error).includes('A simulation update is already running')||Date.now()>deadline)throw error;
  await page.waitForTimeout(100);
 }}
}
const timings=[],started=Date.now();
await page.addInitScript(()=>{const Base=window.Worker;window.Worker=class extends Base{constructor(...args){super(...args);this.addEventListener('message',event=>{const d=event.data;if(['ready','progress','complete','view-complete'].includes(d?.type))console.info('HISTORY_TIMING '+JSON.stringify({type:d.type,stage:d.stage,percent:d.percent}));});}};});
page.on('pageerror',e=>errors.push(String(e)));
page.on('console',m=>{if(m.text().startsWith('HISTORY_TIMING ')){const entry={seconds:(Date.now()-started)/1000,...JSON.parse(m.text().slice(15))};timings.push(entry);console.log(JSON.stringify(entry));}});
page.on('console',m=>{if(m.type()==='error')console.log('CONSOLE '+m.text());});page.on('requestfailed',r=>console.log('REQUEST FAILED '+r.url()));
const heartbeat=setInterval(async()=>{try{console.log('BROWSER '+(await page.locator('.progress-card,.error,.sim-bar').allTextContents()).join(' | '));await page.screenshot({path:output+'/current.png'});}catch{}},20000);heartbeat.unref();
try{
 await page.goto('http://127.0.0.1:5094');
 if(process.argv.includes('--library')){
  await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Historical workflow check',bytes,{CellCount:5000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
  await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 }else{
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();console.log('Setup ready');
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});console.log('Module ready');
 await page.evaluate(bytes=>window.__wg.loadWorldFile(bytes),(await fs.readFile(source)).toString('base64'));
 console.log('Snapshot dispatched');await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start world')?.disabled,{},{timeout:240000});
 console.log('Starting loaded world');await page.getByRole('button',{name:'Start world',exact:true}).click();await page.getByRole('button',{name:'Step',exact:true}).waitFor();
 }
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.waitForFunction(()=>{const bar=document.querySelector('.sim-bar');return bar&&!bar.innerText.startsWith('Year 0')&&[...bar.querySelectorAll('button')].some(b=>b.textContent==='Step'&&!b.disabled);},{},{timeout:240000});
 await page.waitForTimeout(1000);
 await page.locator('.society-dock button').nth(2).click();await page.getByRole('button',{name:'Map settings',exact:true}).click();
 await page.getByLabel('Map opacity',{exact:true}).fill('0.15');await page.getByLabel('Population opacity',{exact:true}).fill('1');
 await page.getByRole('button',{name:'Switch to map',exact:true}).click();await page.waitForFunction(()=>window.__wg.viewInfo().map&&!window.__wg.viewInfo().loading,{},{timeout:90000});
 await page.locator('.society-dock button').nth(0).click();await page.locator('.society-dock button').nth(1).click();
 for(const mode of ['Civilizations','States','Cultures','Religions','Technology']){
  await page.getByLabel('Historical overlay').selectOption(mode);await page.waitForTimeout(1000);
  const summary=JSON.parse(await readPeople('status'));
  if(!summary.Legend.length)throw Error('Empty '+mode+' legend');
  if(summary.Events.some(e=>/takes office|opens a trade agreement|do not renew their defensive pact/.test(e.Text)))throw Error('Routine diplomatic record leaked into headline feed');
  if(mode==='States'){
   for(const [sort,field] of [['Population','Population'],['Land area','AreaKm2']]){
    await page.getByLabel('Sort societies').selectOption(sort);await page.waitForTimeout(150);
    const first=[...summary.Legend].sort((a,b)=>b[field]-a[field])[0];
    const expected=first[field].toLocaleString('en-US')+(sort==='Land area'?' km²':'');
    if(await page.locator('.society-value').first().innerText()!==expected)throw Error('Missing or incorrect numeric '+sort+' value');
    await page.screenshot({path:output+'/'+sort.toLowerCase().replace(' ','-')+'-ranking.png'});
   }
   await page.getByLabel('Sort societies').selectOption('Default');
   if(await page.locator('.society-value').count())throw Error('Abstract dominance score exposed');
  }
  await page.locator('.society-entry').first().click();await page.waitForTimeout(500);
  await page.getByRole('button',{name:'Selection',exact:true}).click();
  const selected=JSON.parse(await readPeople('status'));
  if(!selected.Inspection.includes('LOCAL CHRONICLE')||!(await page.locator('.selection-summary').innerText()).length)throw Error('Missing underlying linked lore or narrative selection '+mode);
  if(mode==='Religions'&&selected.Subject?.Name!==await page.locator('.society-name').first().innerText())throw Error('Religious legend selected a converted holy site rather than current followers');
  await page.screenshot({path:output+'/'+mode.toLowerCase()+'.png'});
  await page.getByRole('button',{name:'Map settings',exact:true}).click();
 }
 const [download]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:'Export campaign chronicle',exact:true}).click()]);
 await download.saveAs(output+'/exported-chronicle.txt');
 const lore=await fs.readFile(output+'/exported-chronicle.txt','utf8');
 for(const heading of ['Complete event archive','Settlement gazetteer','Religious traditions','Notable and current leaders','Military campaigns'])if(!lore.includes(heading))throw Error('Incomplete lore export: '+heading);
 const beforeStep=JSON.parse(await readPeople('status'));
 console.log('Before step '+JSON.stringify(beforeStep).slice(0,300));
 await page.getByLabel('Simulation time rate').fill('37');await page.getByLabel('Simulation rate unit').selectOption('6');await page.getByRole('button',{name:'Set',exact:true}).click();await page.waitForTimeout(500);await page.evaluate(async()=>{let s=JSON.parse(await window.__wg.people('status'));const target=s.Seconds+37*31536000;while(s.Seconds<target)s=JSON.parse(await window.__wg.people('timeBatch',target-s.Seconds));});
 const displayedYear=beforeStep.Years+37-(beforeStep.EpochYear>=0?beforeStep.EpochYear:0);
 await page.waitForFunction(year=>document.querySelector('.sim-bar').innerText.includes(year.toLocaleString('en-US')),displayedYear,{timeout:90000});
 await page.getByRole('button',{name:'Manual',exact:true}).click();await page.getByRole('button',{name:'Auto',exact:true}).waitFor();
 const settings=await page.getByRole('button',{name:'Map settings',exact:true});if(!await page.getByLabel('Map opacity',{exact:true}).isVisible())await settings.click();
 await page.getByLabel('Map opacity',{exact:true}).fill('0.5');await page.getByLabel('Population opacity',{exact:true}).fill('1');
 await page.locator('#globe').focus();
 for(const [key,label,expected] of [['-','Map opacity',.25],['=','Map opacity',.5],['Shift+-','Population opacity',.75],['Shift+=','Population opacity',1]]) {
  await page.keyboard.press(key);await page.waitForTimeout(100);
  if(Math.abs(Number(await page.getByLabel(label,{exact:true}).inputValue())-expected)>.001)throw Error('25% shortcut '+key);
 }
 await page.keyboard.press('Shift+=');await page.waitForTimeout(100);if(await page.getByLabel('Population opacity',{exact:true}).inputValue()!=='1')throw Error('Opacity upper clamp');
 await page.keyboard.press('-');await page.keyboard.press('-');await page.keyboard.press('-');await page.waitForTimeout(150);
 if(await page.getByLabel('Map opacity',{exact:true}).inputValue()!=='0')throw Error('Opacity lower clamp');
 await page.screenshot({path:output+'/opacity-controls.png'});
 if(process.argv.includes('--roundtrip-save')){
  const saved=await page.evaluate(()=>window.__wg.saveLibraryWorld('Browser continuation audit'));
  const packed=await page.evaluate(async id=>(await import('/world-library.js')).read(id),saved);
  await fs.writeFile(output+'/continued.world',Buffer.from(packed,'base64'));
  console.log('Saved browser-written checkpoint for independent .NET validation.');
 }
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS: saved history, five overlays, matching legends, locality inspection, campaign export, irregular manual step.');
}catch(e){console.log(await page.locator('body').innerText());await page.screenshot({path:output+'/failure.png'});throw e;}finally{clearInterval(heartbeat);await fs.writeFile(output+'/timings.json',JSON.stringify(timings,null,2));await browser.close();}

import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const source=process.argv[2]??'D:/WorldGen-validation-20261008/accepted-ui/final.world';
const output=process.env.ATLAS_CHECK_OUTPUT??'artifacts/atlas-browser';
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const context=await browser.newContext({viewport:{width:1600,height:1000},permissions:['clipboard-read','clipboard-write']});
const page=await context.newPage(),errors=[];
let illustratedProfiles=0;
async function checkHabitat(){
 const card=page.getByRole('region',{name:'Illustrated habitat profile'});
 if(!await card.count())return;
 await card.locator('img').scrollIntoViewIfNeeded();
 await page.waitForFunction(()=>[...document.querySelectorAll('.habitat-portrait img')].every(img=>img.complete&&img.naturalWidth>0),{},{timeout:30000});
 if(!(await card.innerText()).includes('Illustrative habitat'))throw Error('Missing illustration disclaimer');
 if(await card.locator('.habitat-conditions>div').count()!==3)throw Error('Missing actual local condition tiles');
 illustratedProfiles++;
 await page.screenshot({path:output+'/illustrated-habitat-'+illustratedProfiles+'.png'});
}
async function enabled(locator){await locator.waitFor();const deadline=Date.now()+30000;while(!await locator.isEnabled()){if(Date.now()>deadline)throw Error('UI control remained disabled');await page.waitForTimeout(30);}}
async function contains(locator,text){const deadline=Date.now()+30000;while(!(await locator.innerText()).includes(text)){if(Date.now()>deadline)throw Error('Expected UI text: '+text);await page.waitForTimeout(30);}}
// Exercise the UI-to-clipboard payload without replacing the user's OS clipboard.
await page.addInitScript(()=>Object.defineProperty(navigator,'clipboard',{value:{writeText:async text=>{window.__atlasCopied=text;},readText:async()=>window.__atlasCopied??''}}));
page.on('pageerror',e=>errors.push(String(e)));
const heartbeat=setInterval(async()=>{try{console.log('Atlas browser check: '+(await page.locator('.progress-card,.error,.sim-bar').allTextContents()).join(' | '));}catch{}},20000);heartbeat.unref();
try{
 await page.goto('http://127.0.0.1:5094');
 await page.evaluate(async bytes=>{const library=await import('/world-library.js');await library.write('Atlas UI audit',bytes,{CellCount:5000,Recipe:{AlgorithmVersion:'worldgen-3.32.0'}});},(await fs.readFile(source)).toString('base64'));
 await page.reload();await page.getByRole('button',{name:'Load World',exact:true}).click();await page.getByRole('button',{name:'Load last save',exact:true}).click();
 await page.waitForFunction(()=>{const step=[...document.querySelectorAll('button')].find(b=>b.textContent==='Step');const bar=document.querySelector('.sim-bar');return step&&!step.disabled&&!document.querySelector('.progress-card')&&bar&&!bar.innerText.includes('Year 0');},{},{timeout:300000});
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 const before=await page.evaluate(()=>window.__wg.people('lore'));
 await page.getByRole('button',{name:'World',exact:true}).click();await page.getByRole('dialog',{name:'World atlas'}).waitFor();
 await page.keyboard.press('Tab');if(!await page.locator('.atlas-reader').evaluate(el=>el.contains(document.activeElement)))throw Error('Keyboard focus escaped reader');
 await page.keyboard.press('Shift+Tab');if(!await page.locator('.atlas-reader').evaluate(el=>el.contains(document.activeElement)))throw Error('Reverse keyboard focus escaped reader');
 await page.screenshot({path:output+'/world-overview.png'});
 await page.getByRole('button',{name:'Political centers',exact:true}).click();
 if(!(await page.locator('.atlas-reading progress').count()))throw Error('Missing population comparison chart');
 await page.screenshot({path:output+'/population-comparison.png'});
 await page.locator('.atlas-directory-list button').first().click();
 if(process.argv.includes('--technical')){
  await page.getByRole('button',{name:'Research and practical technology',exact:true}).click();
  await page.locator('.technical-card').first().waitFor();
  if(await page.locator('.technical-card').count()<10)throw Error('Missing leveled technical practices');
  if(!(await page.locator('.technical-card').first().innerText()).includes('Practical deployment'))throw Error('Knowledge and deployment are not independently visible');
  await page.screenshot({path:output+'/technical-research.png'});
  await page.getByRole('button',{name:'Empire and constituent governments',exact:true}).click();
  if(!(await page.locator('.atlas-reading').innerText()).includes('Territories'))throw Error('Missing imperial/local-government explanation');
  await page.screenshot({path:output+'/imperial-governments.png'});
  await page.locator('.atlas-directory').getByRole('button',{name:'Provinces',exact:true}).click();await page.locator('.atlas-directory-list button').first().click();
  await page.waitForFunction(()=>document.querySelector('.atlas-article')?.dataset.kind==='province');
  if(!(await page.locator('.atlas-lead').innerText()).includes('administrative'))throw Error('Missing actual provincial narrative');
  await page.screenshot({path:output+'/province-profile.png'});
  await page.locator('.atlas-directory').getByRole('button',{name:'States',exact:true}).click();await page.locator('.atlas-directory-list button').first().click();
 }
 await page.getByRole('button',{name:'Diplomatic neighborhood',exact:true}).waitFor();await page.screenshot({path:output+'/state-profile.png'});
 await page.getByRole('button',{name:"Capital's setting",exact:true}).click();await page.screenshot({path:output+'/capital-setting.png'});
 await page.getByRole('button',{name:'Diplomatic neighborhood',exact:true}).click();await page.screenshot({path:output+'/diplomatic-neighborhood.png'});
 const relational=await page.locator('.atlas-reading .atlas-link-row button').count();
 if(relational){await page.locator('.atlas-reading .atlas-link-row button').first().click();await page.getByRole('button',{name:'Back',exact:true}).click();}
 for(const [category,section,file] of [['Cities','Provisioning and resilience','city'],['Peoples','Practices and knowledge','people'],['Religions','Beliefs and observance','religion'],['Rulers','Leadership profile','ruler']]){
  await page.locator('.atlas-directory').getByRole('button',{name:category,exact:true}).click();await enabled(page.locator('.atlas-directory-list button').first());await page.locator('.atlas-directory-list button').first().click();
  await page.getByRole('button',{name:section,exact:true}).waitFor();await page.screenshot({path:output+'/'+file+'-profile.png'});
  await checkHabitat();
  await page.getByRole('button',{name:section,exact:true}).click();await page.screenshot({path:output+'/'+file+'-detail.png'});
 }
 await page.getByLabel('Include historical records').check();
 await page.getByRole('button',{name:'Next directory page',exact:true}).click();await page.screenshot({path:output+'/historical-directory.png'});
 await page.getByRole('button',{name:'Previous directory page',exact:true}).click();
 await page.locator('.atlas-directory').getByRole('button',{name:'World overview',exact:true}).click();
 await contains(page.locator('.atlas-article h1'),'World at a glance');await enabled(page.getByRole('button',{name:'Recorded history',exact:true}));
 await page.getByRole('button',{name:'Recorded history',exact:true}).click();await enabled(page.getByRole('button',{name:'Older',exact:true}));
 const firstRecord=await page.locator('.atlas-timeline>div').first().innerText();await page.getByRole('button',{name:'Older',exact:true}).click();await contains(page.locator('.atlas-record-count'),'Records 41');
 if(firstRecord===await page.locator('.atlas-timeline>div').first().innerText())throw Error('History page did not advance');
 await page.getByLabel('Include routine records').check();await contains(page.locator('.atlas-record-count'),'Records 1');
 await page.screenshot({path:output+'/linked-history.png'});
 await page.getByRole('button',{name:'Copy article',exact:true}).click();await contains(page.locator('.atlas-copy-notice'),'Article copied');
 const copied=await page.evaluate(()=>navigator.clipboard.readText());if(!copied.includes('World at a glance')||!copied.includes('Recorded history'))throw Error('Copied article lacks its title or history');
 await page.locator('.atlas-directory').getByRole('button',{name:'Cities',exact:true}).click();await enabled(page.locator('.atlas-directory-list button').first());await page.locator('.atlas-directory-list button').first().click();
 await enabled(page.getByRole('button',{name:'Show on map',exact:true}));
 await page.getByLabel('Search atlas names').fill('no-such-record-name');if(await page.locator('.atlas-directory-list button').count())throw Error('Atlas search did not filter names');
 await page.getByLabel('Search atlas names').fill('');
 await page.getByRole('button',{name:'Show on map',exact:true}).click();await page.getByRole('button',{name:'Selection',exact:true}).waitFor();
 await page.screenshot({path:output+'/selection-summary.png'});
 if(!await page.locator('.selection-summary').innerText())throw Error('No narrative selection summary');
 await page.getByRole('button',{name:'Atlas',exact:true}).click();await page.getByRole('dialog',{name:'World atlas'}).waitFor();await page.keyboard.press('Escape');
 await page.getByRole('dialog',{name:'World atlas'}).waitFor({state:'hidden',timeout:10000});
 if(await page.getByRole('heading',{name:'PAUSED',exact:true}).count())throw Error('Closing reader unexpectedly opened pause menu');
 const after=await page.evaluate(()=>window.__wg.people('lore'));if(before!==after)throw Error('Reading profiles changed simulated history');
 if(!illustratedProfiles)throw Error('No illustrative habitat was exercised by the profile checks');
 await page.getByRole('button',{name:'World',exact:true}).click();await page.setViewportSize({width:1024,height:768});await page.waitForTimeout(300);
 await page.screenshot({path:output+'/compact-reader.png'});
 const bounds=await page.locator('.atlas-reader').boundingBox();if(bounds.x<0||bounds.y<52||bounds.x+bounds.width>1025||bounds.y+bounds.height>733)throw Error('Reader escapes usable viewport');
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: five linked profile types, geography, comparisons, diplomacy, search, back, history and directory paging, copy, focus, map selection, Escape, compact viewport, unchanged campaign lore.');
}catch(e){await page.screenshot({path:output+'/failure.png'});console.log((await page.locator('body').innerText()).slice(-6000));throw e;}
finally{clearInterval(heartbeat);await browser.close();}

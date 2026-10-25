import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';
const base=process.argv[2]??'http://127.0.0.1:8766';
const data=await(await fetch(base+'/eyes/index.json')).json();
assert.equal(data.count,171);assert.equal(new Set(data.animals.map(x=>x.slug)).size,171);
let checked=0;
for(const animal of data.animals)for(const view of ['front','right','left'])for(const stage of ['after','before']){
 const bytes=await fs.readFile(path.resolve('artifacts/fauna-phone-review/dist/eyes',animal.images[view][stage]));
 assert.equal(bytes.toString('ascii',0,4),'RIFF');assert.equal(bytes.toString('ascii',8,12),'WEBP');checked++;
}
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try{
 const page=await browser.newPage({viewport:{width:390,height:844},isMobile:true,hasTouch:true});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/eyes.html');await page.waitForSelector('#animal option',{state:'attached'});assert.equal(await page.locator('#animal option').count(),171);
 for(const slug of ['rabbit','red-fox','raccoon','owl','jaguar','pronghorn','penguin','giant-panda','capuchin','whale']){
  await page.selectOption('#animal',slug);
  for(const view of ['front','right','left']){
   await page.click(`[data-view="${view}"]`);
   for(const stage of ['before','after']){
    await page.click(`[data-stage="${stage}"]`);
    await page.waitForFunction(()=>{const im=document.querySelector('#photo');return im.complete&&im.naturalWidth===512});
    assert.ok((await page.locator('#photo').getAttribute('src')).includes(`${slug}-${view}-${stage}.webp`));
   }
  }
 }
 await page.fill('#search','panda');assert.equal(await page.locator('#animal option').count(),1);assert.equal(await page.locator('#animal').inputValue(),'giant-panda');
 await page.fill('#search','');await page.selectOption('#animal','hare');await page.click('#previous');assert.equal(await page.locator('#animal').inputValue(),'whale');await page.click('#next');assert.equal(await page.locator('#animal').inputValue(),'hare');
 assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));assert.deepEqual(errors,[]);
 await page.screenshot({path:'artifacts/fauna-phone-review/checks/eyes-mobile.png',fullPage:true});
 await page.setViewportSize({width:1280,height:900});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 console.log(JSON.stringify({animals:171,images:checked,mobile_controls:'pass',image_switching:'pass',filter_and_wrap:'pass',errors}));
}finally{await browser.close()}

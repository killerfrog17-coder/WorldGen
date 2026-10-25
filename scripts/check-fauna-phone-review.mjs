import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';

const url=process.argv[2]??'http://127.0.0.1:8766';
const output=path.resolve('artifacts/fauna-phone-review/checks');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const page=await browser.newPage({viewport:{width:390,height:844},isMobile:true,hasTouch:true});
const errors=[];page.on('pageerror',error=>errors.push(String(error)));
try{
  await page.goto(url);await page.locator('footer').scrollIntoViewIfNeeded();
  await page.waitForFunction(()=>[...document.images].every(image=>image.complete&&image.naturalWidth>0));
  assert.equal(await page.locator('figure').count(),6);
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.evaluate(()=>scrollTo(0,0));
  const clips=[];
  for(const clip of ['walk','run','eating']){
    await page.locator(`[data-clip="${clip}"]`).click();
    await page.locator('video').scrollIntoViewIfNeeded();
    await page.waitForFunction(()=>{const v=document.querySelector('video');return v.readyState>=2&&v.videoWidth>0});
    const metadata=await page.locator('video').evaluate(video=>({width:video.videoWidth,height:video.videoHeight,duration:video.duration}));
    assert.equal(metadata.width,640);assert.equal(metadata.height,480);assert(metadata.duration>=2.9);
    await page.waitForFunction(()=>document.querySelector('video').currentTime>.3);
    await page.locator('video').evaluate(video=>video.pause());
    clips.push({clip,...metadata,playback_advances:true});
  }
  await page.locator('[data-speed="0.25"]').click();
  assert.equal(await page.locator('video').evaluate(video=>video.playbackRate),.25);
  await page.locator('[data-clip="walk"]').click();
  await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
  assert.equal(await page.locator('video').evaluate(video=>video.playbackRate),.25);
  await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(output,'mobile-review.png'),fullPage:true});
  assert.deepEqual(errors,[]);
  const result={url,viewport:'390x844',stills:6,clips,quarter_speed:true,no_horizontal_overflow:true,page_errors:errors};
  await fs.writeFile(path.join(output,'browser-check.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify(result));
}finally{await browser.close()}

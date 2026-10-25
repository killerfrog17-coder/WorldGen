import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {chromium} from 'playwright';
const base=process.argv[2]??'http://127.0.0.1:8766';
const output=path.resolve('artifacts/fauna-phone-review/checks');
const manifest=await (await fetch(base+'/overview/index.json')).json();
assert.equal(manifest.count,177);
for(const group of Object.values(manifest.modes)){
  assert.equal(group.pages.length,8);
  assert.equal(group.pages.flatMap(p=>p.names).length,177);
  let next=1;for(const page of group.pages){assert.equal(page.first,next);assert.equal(page.last-page.first+1,page.names.length);next=page.last+1}assert.equal(next,178);
}
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const page=await browser.newPage({viewport:{width:390,height:844},isMobile:true,hasTouch:true});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));
try{
  await page.goto(base+'/overview.html');
  await page.waitForSelector('#pages figure');
  for(const mode of ['side','front','alternate']){
    await page.locator(`[data-mode="${mode}"]`).click();
    await page.locator('img').evaluateAll(images=>images.forEach(image=>image.loading='eager'));
    await page.waitForFunction(()=>[...document.images].every(image=>image.complete&&image.naturalWidth>0));
    assert.equal(await page.locator('#pages figure').count(),8);
    assert.equal(await page.locator('#pages h2').last().textContent(),'Sheet 8 · 169–177');
    assert((await page.locator('#master-link').getAttribute('href')).endsWith(`all-${mode}.jpg`));
    assert.equal(await page.locator(`[data-mode="${mode}"]`).getAttribute('aria-pressed'),'true');
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  }
  await page.locator('[data-mode="side"]').click();await page.evaluate(()=>scrollTo(0,0));
  await page.screenshot({path:path.join(output,'overview-mobile.png')});
  assert.deepEqual(errors,[]);
  const result={animals:177,modes:3,sheets:27,phone_viewport:'390x844',loaded_all_modes:true,coverage_contiguous:true,no_overflow:true,page_errors:errors};
  await fs.writeFile(path.join(output,'overview-browser-check.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify(result));
}finally{await browser.close()}

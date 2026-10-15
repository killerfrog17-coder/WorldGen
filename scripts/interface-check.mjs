import {chromium} from 'playwright';
import fs from 'node:fs';
import assert from 'node:assert/strict';
const output=process.env.INTERFACE_CHECK_OUTPUT||'artifacts/interface-check';fs.mkdirSync(output,{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
page.on('pageerror',error=>errors.push(String(error)));page.setDefaultTimeout(180000);
try{
 await page.goto(process.env.WORLDGEN_URL||'http://127.0.0.1:5080');
 console.log('Opened interface');
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 const fixture=process.argv[2]||'src/WorldGen.Web/wwwroot/world/Earth.world';
 await page.evaluate(async bytes=>{const m=await import('/globe.js');await m.loadWorldFile(bytes);},fs.readFileSync(fixture).toString('base64'));
 console.log('Loading checkpoint',fixture);
 await page.getByRole('button',{name:'Start world',exact:true}).waitFor();
 await page.waitForFunction(()=>!document.querySelector('button.start')?.disabled);
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 console.log('Started viewer');
 await page.locator('.society-dock').waitFor();
 for(let i=0;i<3;i++)await page.locator('.society-dock>button').nth(i).click();
 await page.locator('.world-era').waitFor();
 const era=await page.locator('.world-era').textContent();assert.ok(era.length>0);
 let rects=await page.locator('[data-dock]').evaluateAll(p=>p.map(e=>{const r=e.getBoundingClientRect();return {key:e.dataset.dock,x:r.x,right:r.right,y:r.y,height:r.height,width:r.width};}));
 assert.ok(Math.abs(rects[0].right-rects[1].x)<1,'States panel meets chronicle without a gap');
 assert.ok(Math.abs(rects[1].right-rects[2].x)<1,'Chronicle meets selection panel');
 const initialHeights=rects.map(r=>r.height);
 for(const key of ['peoples','chronicle','selection']){
  const handle=page.locator(`[data-dock="${key}"] .panel-resizer`),box=await handle.boundingBox();
  await page.mouse.move(box.x+box.width/2,box.y+4);await page.mouse.down();await page.mouse.move(box.x+box.width/2,box.y-130,{steps:10});await page.mouse.up();
 }
 rects=await page.locator('[data-dock]').evaluateAll(p=>p.map(e=>({key:e.dataset.dock,height:e.getBoundingClientRect().height})));
 rects.forEach((r,i)=>assert.ok(r.height>initialHeights[i]+100,`${r.key} expands by dragging`));
 const list=await page.locator('.state-columns').evaluate(e=>({rows:getComputedStyle(e).gridTemplateRows.split(' ').length,children:[...e.children].map(c=>{const r=c.getBoundingClientRect();return {x:r.x,y:r.y};})}));
 if(list.children.length>1)assert.ok(new Set(list.children.map(c=>Math.round(c.x))).size<=2,'States use no more than two columns');
 const capacity=await page.locator('.state-list-scroll').evaluate(e=>Math.max(1,Math.floor(e.clientHeight/21)));
 if(list.children.length>capacity)assert.ok(list.children[capacity].x>list.children[0].x&&Math.abs(list.children[capacity].y-list.children[0].y)<1,'First column fills the visible height before wrapping to the right');
 await page.screenshot({path:`${output}/expanded.png`});
 await page.locator('.society-dock>button').first().click();await page.locator('.society-dock>button').first().click();
 assert.ok((await page.locator('[data-dock="peoples"]').boundingBox()).height>initialHeights[0]+100,'Fold/unfold preserves chosen height');
 await page.setViewportSize({width:1024,height:720});await page.waitForTimeout(200);
 const small=await page.locator('[data-dock="peoples"]').boundingBox();assert.ok(small.y>=52,'Resized panels remain below top bar');
 await page.screenshot({path:`${output}/small-window.png`});
 assert.deepEqual(errors,[]);console.log(JSON.stringify({passed:true,era,initialHeights,resized:rects,states:list.children.length,errors},null,2));
}catch(error){await page.screenshot({path:`${output}/failure.png`});console.log((await page.locator('body').innerText()).slice(-5000));throw error;}finally{await browser.close();}

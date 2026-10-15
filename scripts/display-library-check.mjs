import {chromium} from 'playwright';
import fs from 'node:fs/promises';
await fs.mkdir('artifacts/display-review',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1500,height:1000}});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
try{
 await page.goto('http://127.0.0.1:5094');
 await page.getByRole('button',{name:'Set Up World',exact:true}).waitFor();
 await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 await page.screenshot({path:'artifacts/display-review/setup.png'});
 const data=await fs.readFile('artifacts/setup-review/native-earth/snapshot.world');
 await page.evaluate(data=>window.__wg.loadWorldFile(data),data.toString('base64'));
 await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return v.vertices>0&&!v.loading;},null,{timeout:300000});
 console.log('PASS native Earth snapshot uploaded');
 const timings=await page.evaluate(async()=>{
  const results=[];const before=window.__wg.viewInfo();
  for(const layer of [4,0,1,2,3,38,0]){const t=performance.now();await window.__wg.view(JSON.stringify({...before.options,Layer:layer}));const now=window.__wg.viewInfo();if(now.loading||now.vertices!==before.vertices)throw Error('Cached layer remeshed');results.push({layer,ms:performance.now()-t});}
  for(const grid of [0,5,10,15,30]){const t=performance.now();await window.__wg.view(JSON.stringify({...before.options,GridSpacing:grid,ShowRivers:true,Wireframe:true}));results.push({grid,ms:performance.now()-t});}
  return results;
 });
 console.log('Cached switches',timings);
 if(timings.some(t=>t.ms>100))throw Error('Cached display switch exceeded 100 ms');
 await page.getByRole('button',{name:'Climate',exact:true}).click();
 await page.screenshot({path:'artifacts/display-review/climate-globe.png'});
 await page.getByRole('button',{name:'Toggle globe or map'}).click();
 await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return v.map&&!v.loading;},null,{timeout:180000});
 await page.screenshot({path:'artifacts/display-review/climate-map.png'});
 await page.getByRole('button',{name:'Start world',exact:true}).click();
 await page.getByLabel('World name').fill('Library check');
 await page.getByRole('button',{name:'Save world',exact:true}).click();
 await page.waitForFunction(async()=>{const m=await import('/world-library.js');return (await m.list()).length===1;},null,{timeout:120000});
 await page.getByRole('button',{name:'Save world',exact:true}).waitFor({state:'visible'});
 await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save world').disabled);
 await page.getByRole('button',{name:'Save world',exact:true}).click();
 await page.waitForFunction(async()=>{const m=await import('/world-library.js');return (await m.list()).length===2;},null,{timeout:120000});
 await page.getByRole('button',{name:'Main menu',exact:true}).last().click();
 await page.getByRole('button',{name:'Load World',exact:true}).click();
 await page.getByRole('button',{name:'+',exact:true}).click();
 if(await page.locator('.save-row').count()!==2)throw Error('Saves not grouped');
 await page.screenshot({path:'artifacts/display-review/worlds.png'});
 page.once('dialog',d=>d.dismiss());await page.locator('.save-row').first().getByRole('button',{name:'Delete',exact:true}).click();
 if(await page.locator('.save-row').count()!==2)throw Error('Cancelled deletion changed saves');
 page.once('dialog',d=>d.accept());await page.locator('.save-row').first().getByRole('button',{name:'Delete',exact:true}).click();
 await page.waitForFunction(()=>!document.querySelector('.save-row'));
 if(await page.getByRole('button',{name:'+',exact:true}).count())throw Error('Single save still has expand');
 page.once('dialog',d=>d.accept());await page.locator('.world-row').getByRole('button',{name:'Delete',exact:true}).click();
 await page.getByText('No saved worlds yet.').waitFor();
 if(errors.length)throw Error(errors.join('\n'));
 await fs.writeFile('artifacts/display-review/timings.json',JSON.stringify(timings,null,2));
 console.log('PASS cached layers/overlays, projection, grouped snapshots, confirmed individual/world deletion');
}finally{await browser.close();}

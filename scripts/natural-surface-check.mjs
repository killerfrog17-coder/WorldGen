import {chromium} from 'playwright';
import fs from 'node:fs/promises';
const root='artifacts/natural-331',data=await fs.readFile(root+'/native/world.json','utf8'),world=JSON.parse(data);
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
page.on('pageerror',e=>{errors.push(String(e));console.error(e);});page.on('console',m=>{if(m.type()==='error'){errors.push(m.text());console.error(m.text());}});
try{
 await page.goto('http://127.0.0.1:5094');await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 console.log('Setup ready');await page.evaluate(async()=>{window.__wg=await import('/globe.js');});
 await page.evaluate(data=>window.__wg.loadWorld(data),data);
 console.log('Snapshot requested');
 await page.waitForFunction(()=>{const v=window.__wg.viewInfo();return v.vertices>0&&!v.loading;},null,{timeout:300000});
 await page.evaluate(async()=>{const o=window.__wg.viewInfo().options;await window.__wg.view(JSON.stringify({...o,Projection:1,Layer:0,GridSpacing:0,ShowRivers:false,Natural:true}));});
 await page.waitForTimeout(500);await page.locator('canvas').screenshot({path:root+'/browser-smooth.png'});
 const timings=await page.evaluate(async()=>{const before=window.__wg.viewInfo(),results=[];for(const [Layer,Natural] of [[0,false],[0,true],[1,true],[2,true],[4,true],[0,true]]){const t=performance.now();await window.__wg.view(JSON.stringify({...before.options,Layer,Natural}));const now=window.__wg.viewInfo();if(now.loading||now.vertices!==before.vertices)throw Error('Cached switch rebuilt geometry');results.push(performance.now()-t);}return results;});
 console.log('Instant smooth/categorical/toggle checks (ms):',timings);
 const group=world.Subcells.Cells.filter(g=>g.IslandCount>=3&&Math.abs(world.Surface.Cells[g.Cell].Position.Z)<.65).sort((a,b)=>b.LargestIslandAreaM2-a.LargestIslandAreaM2)[0];
 const island=world.Subcells.Islands.slice(group.FirstIsland,group.FirstIsland+group.IslandCount).sort((a,b)=>b.AreaM2-a.AreaM2)[0];
 const lon=Math.atan2(island.Center.Y,island.Center.X)*180/Math.PI,lat=Math.asin(island.Center.Z)*180/Math.PI;
 await page.evaluate(lon=>window.__wg.longitude(lon),lon);
 const box=await page.locator('canvas').boundingBox(),scale=Math.max(1.05,2.1/(box.width/box.height));
 await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();await page.mouse.move(box.x+box.width/2,box.y+box.height/2+lat/90*box.height/(2*scale),{steps:8});await page.mouse.up();
 await page.mouse.wheel(0,-1550);await page.waitForTimeout(800);
 await page.locator('canvas').screenshot({path:root+'/browser-islands-close.png'});
 console.log('Close-up island group',JSON.stringify(group));
 if(errors.length)throw Error(errors.join('\n'));
 if(process.argv.includes('--visual-only')){console.log('PASS natural surface screenshots and cached display switches');await browser.close();process.exit(0);}
 const expected=JSON.parse(await fs.readFile(root+'/native/hashes.json','utf8'));
 const stages=await page.evaluate(recipe=>new Promise((resolve,reject)=>{const w=new Worker('/worker/main.js',{type:'module'});const timeout=setTimeout(()=>{w.terminate();reject(Error('Worker timeout'));},600000);w.onerror=e=>reject(Error(e.message));w.onmessage=e=>{if(e.data.type==='progress')console.log(e.data.stage,e.data.percent);if(e.data.type==='ready')w.postMessage({recipe});if(e.data.type==='error'){clearTimeout(timeout);w.terminate();reject(Error(e.data.message));}if(e.data.type==='complete'){clearTimeout(timeout);w.terminate();resolve(JSON.parse(e.data.json).Stages);}};}),JSON.stringify(world.Recipe));
 if(stages.length!==expected.length||stages.some((s,i)=>s.Hash!==expected[i].Hash||s.Name!==expected[i].Name))throw Error('New algorithm native/WASM stage mismatch');
 console.log('PASS new island metadata cross-host hashes and natural surface rendering');
}catch(error){console.error(await page.locator('body').innerText());await page.screenshot({path:root+'/browser-failure.png'});throw error;}finally{await browser.close();}

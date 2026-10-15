import {chromium} from 'playwright';
import fs from 'node:fs/promises';
await fs.mkdir('artifacts/controls-329',{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage({viewport:{width:1600,height:1100}});
const errors=[];page.on('pageerror',e=>errors.push(String(e)));
try{
 await page.goto('http://127.0.0.1:5094');await page.getByRole('button',{name:'Set Up World',exact:true}).click();
 const plates=page.locator('.optional-range').filter({has:page.getByLabel('Plates',{exact:true})});
 if(await plates.locator('input[type=range]').count()!==1)throw Error('Expected fixed slider');
 await plates.getByRole('checkbox',{name:'Range'}).check();
 const range=page.locator('.optional-range').filter({has:page.getByLabel('Plates min',{exact:true})});
 if(await range.locator('input[type=range]').count()!==2)throw Error('Expected separate min/max sliders');
 await page.getByLabel('Plates min',{exact:true}).fill('20');await page.getByLabel('Plates max',{exact:true}).fill('180');await page.getByLabel('Seed',{exact:true}).click();
 await page.getByRole('button',{name:'Plates roll options'}).click();await page.getByLabel('Plates roll distribution').selectOption('High');
 await page.getByLabel('Map detail',{exact:true}).fill('123456');await page.getByLabel('Seed',{exact:true}).click();
 if(await page.getByLabel('Map detail',{exact:true}).inputValue()!=='123000')throw Error('Integer precision not applied');
 await page.screenshot({path:'artifacts/controls-329/ranges.png'});
 if(process.argv.includes('--ui-only')){console.log('PASS fixed/optional slider UI and precision');await browser.close();process.exit(0);}
 await page.evaluate(async()=>window.__wg=await import('/globe.js'));
 const file=await fs.readFile('artifacts/setup-review/native-earth/snapshot.world');
 await page.evaluate(data=>window.__wg.loadWorldFile(data),file.toString('base64'));
 await page.waitForFunction(()=>window.__wg.viewInfo().vertices>0&&!window.__wg.viewInfo().loading,null,{timeout:300000});
 await page.getByRole('button',{name:'Ecoregions',exact:true}).click();
 const native=JSON.parse(await fs.readFile('artifacts/setup-review/native-earth/world.json','utf8'));
 const region=native.Ecology.Regions.reduce((a,b)=>a.FoodWeb.length>b.FoodWeb.length?a:b);
 const cell=native.Ecology.Region.findIndex(id=>id===region.Id);
 await page.evaluate(cell=>window.__wg.pickCell(cell),cell);
 await page.getByRole('button',{name:'Details',exact:true}).click();
 await page.locator('.food-web').waitFor();
 if(await page.locator('.food-link').count()<3)throw Error('Missing real food links');
 await page.screenshot({path:'artifacts/controls-329/food-web.png'});
 await page.getByRole('button',{name:'Close',exact:true}).click();
 // Prepare the next stage while still using the already uploaded palette.
 const concurrent=await page.evaluate(async()=>{
  const before=window.__wg.viewInfo();const building=window.__wg.view(JSON.stringify(before.options),true);
  const start=performance.now();await window.__wg.view(JSON.stringify({...before.options,Layer:0}));
  const visible=window.__wg.viewInfo();const elapsed=performance.now()-start;await building;
  return {elapsed,layer:visible.options.Layer,wasLoading:visible.loading,after:window.__wg.viewInfo().options.Layer};
 });
 if(concurrent.elapsed>100||concurrent.layer!==0||!concurrent.wasLoading||concurrent.after!==0)throw Error('Completed layer unavailable during loading: '+JSON.stringify(concurrent));
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS optional ranges, endpoint controls, roll choice, precision, food web, old snapshot, concurrent layer switch',concurrent);
}finally{await browser.close();}

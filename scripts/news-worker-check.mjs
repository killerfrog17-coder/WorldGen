import {chromium} from 'playwright';
import fs from 'node:fs/promises';
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage();await page.goto((process.env.WORLDGEN_URL||'http://127.0.0.1:5094')+'/worker/main.js');
 const result=await page.evaluate(data=>new Promise((resolve,reject)=>{
  const worker=new Worker('/worker/main.js',{type:'module'});let before;
  const timer=setTimeout(()=>{worker.terminate();reject(Error('News worker timed out'));},180000);
  worker.onerror=e=>{clearTimeout(timer);worker.terminate();reject(Error(e.message));};
  worker.onmessage=({data:d})=>{
   if(d.type==='error'){clearTimeout(timer);worker.terminate();reject(Error(d.message));}
   if(d.type==='ready')worker.postMessage({type:'load-world-file',data});
   if(d.type==='complete')worker.postMessage({type:'people',command:'status',requestId:1});
   if(d.type==='people'&&d.requestId===1){before=JSON.parse(d.json);worker.postMessage({type:'people',command:'news:0:-1:0',value:-1,requestId:2});}
   if(d.requestId===2){if(d.type!=='lore'||typeof JSON.parse(d.text).Label!=='string'){clearTimeout(timer);worker.terminate();reject(Error('News detail was mistaken for simulation state'));return;}worker.postMessage({type:'people',command:'status',requestId:3});}
   if(d.type==='people'&&d.requestId===3){const after=JSON.parse(d.json);clearTimeout(timer);worker.terminate();if(before.Seconds!==after.Seconds||before.Population!==after.Population)reject(Error('Reading news changed history'));else resolve({passed:true,year:after.Years,population:after.Population});}
  };
 }), (await fs.readFile(process.argv[2]||'src/WorldGen.Web/wwwroot/world/Earth.world')).toString('base64'));
 console.log('PASS isolated news/read-only worker routing',result);
}finally{await browser.close();}

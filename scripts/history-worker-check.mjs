// Cross-host continuation evidence. Calls the actual shared .NET WASM worker;
// no simulation or authoritative arithmetic is implemented in JavaScript.
import {chromium} from 'playwright';
import fs from 'node:fs/promises';
const [source,output,yearText='3',mode='years']=process.argv.slice(2),years=Number(yearText);
if(!source||!output||!Number.isInteger(years)||years<1||years>100)throw Error('Usage: node scripts/history-worker-check.mjs <world> <output> [years 1..100]');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage();
let stage='opening worker';const heartbeat=setInterval(()=>console.log('History worker: '+stage),20000);heartbeat.unref();
try{
 await page.goto(process.env.WORLDGEN_URL??'http://127.0.0.1:5094');
 await page.exposeFunction('historyWorkerStage',value=>{stage=value;});
 const result=await page.evaluate(async ({data,years,mode})=>{
  const worker=new Worker('/worker/main.js',{type:'module'}),phases=[],began=performance.now();
  let pending;const ready=new Promise((resolve,reject)=>{pending={type:'ready',resolve,reject};});
  worker.onerror=e=>pending?.reject(Error(e.message));
  worker.onmessage=e=>{const d=e.data;if(d.type==='progress'){phases.push({stage:d.stage,milliseconds:performance.now()-began});window.historyWorkerStage(d.stage);}if(d.type==='error')pending?.reject(Error(d.message));else if(d.type===pending?.type){const p=pending;pending=null;p.resolve(d);}};
  const request=(message,type)=>new Promise((resolve,reject)=>{pending={type,resolve,reject};worker.postMessage(message);});
  const timer=setTimeout(()=>{pending?.reject(Error('Worker continuation exceeded twenty minutes'));worker.terminate();},1200000);
  try{
   await ready;phases.push({stage:'Runtime ready',milliseconds:performance.now()-began});await window.historyWorkerStage('loading shared snapshot');await request({type:'load-world-file',data},'complete');phases.push({stage:'Snapshot loaded',milliseconds:performance.now()-began});
   await window.historyWorkerStage('continuing shared history');
   const before=JSON.parse((await request({type:'people',command:'status'},'people')).json);
   let after;
   if(mode==='playback'){
    let position=before.Seconds,target=position+years*31536000,requests=0;
    while(position<target){if(++requests>200)throw Error('Playback batches made insufficient progress');const value=target-position;const update=JSON.parse((await request({type:'people',command:'timeBatch',value},'people')).json);if(update.Seconds<=position||update.Seconds>position+value)throw Error('Playback batch crossed its actual request boundary');position=update.Seconds;}
    after=JSON.parse((await request({type:'people',command:'status'},'people')).json);
   }else after=JSON.parse((await request({type:'people',command:'years',value:years},'people')).json);
   phases.push({stage:'History continued',milliseconds:performance.now()-began});
   await window.historyWorkerStage('serializing worker checkpoint');const saved=await request({type:'export',kind:'world'},'export');phases.push({stage:'Checkpoint serialized',milliseconds:performance.now()-began});
   if(after.Years!==before.Years+years||after.Seconds-before.Seconds!==years*31536000)throw Error('Worker lost exact requested calendar interval');
   return {data:saved.data,phases,before:{years:before.Years,seconds:before.Seconds,population:before.Population},after:{years:after.Years,seconds:after.Seconds,population:after.Population}};
  }finally{clearTimeout(timer);worker.terminate();}
 },{data:(await fs.readFile(source)).toString('base64'),years,mode});
 stage='writing shared worker checkpoint';await fs.writeFile(output+'/continued.world',Buffer.from(result.data,'base64'));delete result.data;
 await fs.writeFile(output+'/continuation.json',JSON.stringify(result,null,2));console.log('PASS: actual WASM continuation and binary save over '+years+' years; compare its checkpoint hash with the native/.NET continuation.');
}finally{clearInterval(heartbeat);await browser.close();}

import {chromium} from 'playwright';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';

// Actual WASM deserialization/hash validation and shared atlas projection.
// Input is produced by --language-world, never a second language generator.
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage();await page.goto((process.env.WORLDGEN_URL||'http://127.0.0.1:5096')+'/worker/main.js');
 const result=await page.evaluate(data=>new Promise((resolve,reject)=>{
  const worker=new Worker('/worker/main.js',{type:'module'});let before,article;
  const timer=setTimeout(()=>finish(Error('Language worker check timed out')),600000);
  function finish(error,value){clearTimeout(timer);worker.terminate();error?reject(error):resolve(value);}
  function ask(command,requestId,value=0){worker.postMessage({type:'people',command,requestId,value});}
  worker.onerror=e=>finish(Error(e.message));
  worker.onmessage=({data:d})=>{try{
   if(d.type==='error')throw Error(d.message);
   if(d.type==='ready')worker.postMessage({type:'load-world-file',data});
   if(d.type==='complete')ask('status',1);
   if(d.type==='people'&&d.requestId===1){before=JSON.parse(d.json);ask('calendar',2);}
   if(d.requestId===2){const calendar=JSON.parse(d.text);if(calendar.RulesVersion!=='history-30')throw Error('Expected history30 fixture');ask('atlas:people',3);}
   if(d.requestId===3){article=JSON.parse(d.text);const language=article.Sections.find(s=>s.Title==='Language and names');if(!language||language.Timeline.length!==6||!language.Facts.some(f=>f.Label==='Sounds'&&f.Value.length>0))throw Error('Missing language/root/phonetic article');ask('status',4);}
   if(d.type==='people'&&d.requestId===4){const after=JSON.parse(d.json);if(after.Seconds!==before.Seconds||after.Population!==before.Population)throw Error('Language inspection changed simulation');finish(null,{passed:true,title:article.Title,roots:article.Sections.find(s=>s.Title==='Language and names').Timeline});}
  }catch(error){finish(error);}};
 }), (await fs.readFile(process.argv[2]||'artifacts/languages/history30-review.world')).toString('base64'));
 assert.equal(result.passed,true);console.log('PASS WASM language save/load and read-only culture atlas',JSON.stringify(result));
}finally{await browser.close();}

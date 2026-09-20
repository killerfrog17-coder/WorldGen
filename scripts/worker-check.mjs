import {chromium} from 'playwright';
import fs from 'node:fs';
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
process.on('SIGINT',async()=>{await browser.close();process.exit(130);});
const queue=fs.readdirSync('tests/golden/v3').filter(f=>f.endsWith('.recipe.json'));
const timings={};
if(process.argv.includes('--resume'))for(let i=queue.length-1;i>=0;i--){
 const stem=queue[i].replace('.recipe.json','.hashes.json'),path='artifacts/browser-worker/'+stem;
 if(fs.existsSync(path)){
  const actual=JSON.parse(fs.readFileSync(path,'utf8')),expected=JSON.parse(fs.readFileSync('tests/golden/v3/'+stem,'utf8'));
  if(actual.length===expected.length&&actual.every((s,k)=>s.Hash===expected[k].Hash)){console.log('RESUME verified',queue[i]);queue.splice(i,1);}
 }
}
async function check(){
 const page=await browser.newPage();
 await page.goto((process.env.WORLDGEN_URL||'http://127.0.0.1:5080')+'/worker/main.js');
 while(queue.length){
  const file=queue.shift(),recipe=fs.readFileSync('tests/golden/v3/'+file,'utf8'),start=Date.now();
  console.log('START',file);
  const stages=await page.evaluate(recipe=>new Promise((resolve,reject)=>{
   const worker=new Worker('/worker/main.js',{type:'module'});
   const timeout=setTimeout(()=>{worker.terminate();reject(new Error('Worker timed out'));},600000);
   worker.onerror=e=>{clearTimeout(timeout);worker.terminate();reject(new Error(e.message));};
   worker.onmessage=e=>{if(e.data.type==='ready')worker.postMessage({recipe});if(e.data.type==='error'){clearTimeout(timeout);worker.terminate();reject(new Error(e.data.message));}if(e.data.type==='complete'){clearTimeout(timeout);worker.terminate();resolve(JSON.parse(e.data.json).Stages);}};
  }),recipe);
  const expected=JSON.parse(fs.readFileSync('tests/golden/v3/'+file.replace('.recipe.json','.hashes.json'),'utf8'));
  if(stages.length!==expected.length||stages.some((s,i)=>s.Name!==expected[i].Name||s.Hash!==expected[i].Hash))throw new Error('Cross-host mismatch '+file+'\n'+JSON.stringify(stages));
  fs.mkdirSync('artifacts/browser-worker',{recursive:true});fs.writeFileSync('artifacts/browser-worker/'+file.replace('.recipe.json','.hashes.json'),JSON.stringify(stages));
  timings[file]=Date.now()-start;fs.writeFileSync('artifacts/browser-worker/v3-timings.json',JSON.stringify(timings,null,2));console.log('PASS',file,stages.at(-1).Hash,timings[file]+'ms');
 }
 await page.close();
}
try{await Promise.all([check(),check()]);fs.writeFileSync('artifacts/browser-worker/v3-timings.json',JSON.stringify(timings,null,2));}
catch(e){console.error(e);process.exitCode=1;}finally{await browser.close();}

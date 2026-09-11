import {chromium} from 'playwright';
import fs from 'node:fs';
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const page=await browser.newPage();page.on('console',m=>console.log(m.text()));
try{
await page.goto((process.env.WORLDGEN_URL||'http://127.0.0.1:5080')+'/worker/main.js');
for(const file of fs.readdirSync('tests/golden').filter(f=>f.endsWith('.recipe.json'))){
const recipe=fs.readFileSync('tests/golden/'+file,'utf8');
const start=Date.now();
const stages=await page.evaluate(recipe=>new Promise((resolve,reject)=>{
const worker=new Worker('/worker/main.js',{type:'module'});const timeout=setTimeout(()=>{worker.terminate();reject(new Error('Worker timed out'));},600000);
worker.onerror=e=>{clearTimeout(timeout);worker.terminate();reject(new Error(e.message));};
worker.onmessage=e=>{console.log(e.data.type,e.data.stage||'');if(e.data.type==='ready')worker.postMessage({recipe});if(e.data.type==='error'){clearTimeout(timeout);worker.terminate();reject(new Error(e.data.message));}if(e.data.type==='complete'){clearTimeout(timeout);worker.terminate();resolve(JSON.parse(e.data.json).Stages);}};
}),recipe);
const expected=JSON.parse(fs.readFileSync('tests/golden/'+file.replace('.recipe.json','.hashes.json'),'utf8'));
if(stages.some((s,i)=>s.Hash!==expected[i].Hash))throw new Error('Cross-host mismatch '+file+'\n'+JSON.stringify(stages));
fs.mkdirSync('artifacts/browser-worker',{recursive:true});fs.writeFileSync('artifacts/browser-worker/'+file.replace('.recipe.json','.hashes.json'),JSON.stringify(stages));console.log('PASS',file,stages.at(-1).Hash,Date.now()-start+'ms');
}
}catch(e){console.error(e);process.exitCode=1;}finally{await browser.close();}

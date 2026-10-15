// Storage-only browser regression. Actual world serialization remains covered by .NET tests.
import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const moduleText=await readFile(new URL('../src/WorldGen.Web/wwwroot/world-library.js',import.meta.url));
const server=createServer((req,res)=>{res.setHeader('Content-Type',req.url==='/world-library.js'?'text/javascript':'text/html');res.end(req.url==='/world-library.js'?moduleText:'<!doctype html><title>World library test</title>');});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try{
 const page=await browser.newPage();await page.goto(`http://127.0.0.1:${server.address().port}`);
 const written=await page.evaluate(async()=>{
  const library=await import('/world-library.js');
  if((await library.list()).length!==0)throw new Error('Library should start empty.');
  const bytes=Uint8Array.from({length:100000},(_,i)=>i%256);let binary='';for(const byte of bytes)binary+=String.fromCharCode(byte);
  const data=btoa(binary),summary={CellCount:5000,Recipe:{AlgorithmVersion:'test'}};
  const first=await library.write('Earth test',data,summary),second=await library.write('Earth test',data,summary);
  if(first===second)throw new Error('Saving another copy overwrote a world.');
  let invalidRejected=false;try{await library.write(' ',data,summary);}catch{invalidRejected=true;}
  return {first,second,data,invalidRejected};
 });
 assert.equal(written.invalidRejected,true);
 await page.reload();
 const loaded=await page.evaluate(async({first,second})=>{
  const library=await import('/world-library.js');let missingRejected=false;try{await library.read('missing');}catch{missingRejected=true;}
  return {list:await library.list(),first:await library.read(first),second:await library.read(second),missingRejected};
 },written);
 assert.equal(loaded.list.length,2);assert.equal(loaded.first,written.data);assert.equal(loaded.second,written.data);assert.equal(loaded.missingRejected,true);
 assert.ok(loaded.list.every(row=>row.name==='Earth test'&&row.cells===5000&&row.algorithm==='test'&&!('data' in row)));
 console.log('World library: save, duplicate-name copies, reload, exact binary recovery, metadata, invalid names and missing-save errors passed.');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}

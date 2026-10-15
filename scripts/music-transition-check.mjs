import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const browser=await chromium.launch({channel:'msedge',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
const wav=Buffer.alloc(44+8000*2*12);wav.write('RIFF');wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(1,22);wav.writeUInt32LE(8000,24);wav.writeUInt32LE(16000,28);wav.writeUInt16LE(2,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(wav.length-44,40);
try{
 const origin=process.env.WORLDGEN_URL||'http://127.0.0.1:5094';
 await page.route('**/audio-test',r=>r.fulfill({contentType:'text/html',body:'<div class="game-shell"></div>'}));
 await page.route('**/music/*.wav',r=>r.fulfill({contentType:'audio/wav',body:wav}));
 await page.goto(origin+'/audio-test');
 await page.evaluate(async()=>{
  window.music=await import('/game-interface.js');window.requests=0;
  music.init({invokeMethodAsync:async()=>{requests++;const era=music.musicStatus().era;if(era>0){const first=era===1?1:era<5?2:5,last=era===1?1:era<5?4:10;await music.playTrack(`test-${first} ${first}-${last}.wav`,first,last,era);}}});
  await music.musicContext(2,.6);
 });
 await page.waitForFunction(()=>music.musicStatus().playing&&music.musicStatus().gain>.95);
 const original=await page.evaluate(()=>music.musicStatus());
 await page.evaluate(()=>music.musicContext(3,.6));await page.waitForTimeout(100);
 assert.equal((await page.evaluate(()=>music.musicStatus())).source,original.source,'Compatible ages keep the same song');
 await page.evaluate(()=>{void music.musicContext(5,.6);});await page.waitForTimeout(300);
 const fading=await page.evaluate(()=>music.musicStatus());assert.ok(fading.volume>0&&fading.volume<original.volume,'Incompatible song fades, not an abrupt cut');
 await page.waitForFunction(()=>music.musicStatus().first===5&&music.musicStatus().gain>.95);
 await page.evaluate(()=>music.musicContext(0,.6));assert.equal((await page.evaluate(()=>music.musicStatus())).playing,false,'Silent setup/start context stops playback after fading');
 const quietRequests=await page.evaluate(()=>requests);await page.waitForTimeout(400);assert.equal(await page.evaluate(()=>requests),quietRequests,'Silent context does not request new music');
 await page.evaluate(()=>music.musicContext(6,.25));await page.waitForFunction(()=>music.musicStatus().playing&&music.musicStatus().gain>.95);
 assert.equal(await page.evaluate(()=>localStorage.getItem('worldgen-music-volume')),'0.25','Volume persists');
 assert.ok((await page.evaluate(()=>music.musicStatus())).volume<=.25,'Volume scales the fade envelope');
 await page.evaluate(()=>music.dispose());assert.deepEqual(errors,[]);
 const menu=await browser.newPage({viewport:{width:1280,height:800}});menu.on('pageerror',e=>errors.push(String(e)));
 await menu.route('**/music/catalog.json',r=>r.fulfill({contentType:'application/json',body:'[]'}));
 await menu.goto(origin);await menu.getByRole('button',{name:'Options',exact:true}).click();await menu.getByLabel('Music volume').fill('0.33');
 await menu.waitForFunction(()=>localStorage.getItem('worldgen-music-volume')==='0.33');
 await menu.reload();await menu.getByRole('button',{name:'Options',exact:true}).click();await menu.waitForFunction(()=>document.querySelector('[aria-label="Music volume"]')?.value==='0.33');
 if(process.env.MUSIC_CHECK_OUTPUT){await fs.mkdir(process.env.MUSIC_CHECK_OUTPUT,{recursive:true});await menu.screenshot({path:process.env.MUSIC_CHECK_OUTPUT+'/audio-options.png'});}
 assert.deepEqual(errors,[]);console.log('PASS: compatible continuation, timed fades, silent start, resumed music, main-menu volume and persisted preferences');
}finally{await browser.close();}

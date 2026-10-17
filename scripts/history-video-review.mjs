// Research-only documentary frames; never simulation input or game assets.
// Usage: node scripts/history-video-review.mjs FRAME_FOLDER [SAMPLE_SECONDS]
// Optional frame-ocr.json comes from history-video-ocr.ps1. Optional
// frame-date-corrections.json maps filenames to verified signed years or null.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const folder=path.resolve(process.argv[2]);
const interval=Number(process.argv[3]??2);
if(!Number.isFinite(interval)||interval<=0)throw new Error('Sample interval must be positive.');
const names=(await fs.readdir(folder)).filter(name=>/^frame-\d+\.jpg$/.test(name)).sort((a,b)=>Number(a.match(/\d+/)[0])-Number(b.match(/\d+/)[0]));
const readOptional=async name=>{try{return JSON.parse(await fs.readFile(path.join(folder,name),'utf8'));}catch(error){if(error.code==='ENOENT')return null;throw error;}};
const ocr=new Map((await readOptional('frame-ocr.json')??[]).map(row=>[row.File,row]));
const corrections=await readOptional('frame-date-corrections.json')??{};
const eras=[
 {id:'prehistory',label:'Prehistory / before state origins',from:-Infinity,to:-3301},
 {id:'bronze',label:'State origins / Bronze',from:-3300,to:-1201},
 {id:'iron-classical',label:'Iron / Classical',from:-1200,to:499},
 {id:'medieval',label:'Medieval',from:500,to:1499},
 {id:'early-modern',label:'Early Modern',from:1500,to:1759},
 {id:'industrial',label:'Industrial',from:1760,to:1899},
 {id:'modern',label:'Modern',from:1900,to:1969},
 {id:'postmodern',label:'Postmodern',from:1970,to:Infinity}
];
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const timestamp=seconds=>`${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`;
const date=year=>year===null?'Date unclassified':year<0?`${-year} BCE`:`${year} CE`;
const frames=names.map(name=>{
 const seconds=Number(name.match(/\d+/)[0])*interval;
 const raw=ocr.get(name)?.DateText??'';
 const match=raw.trim().match(/^(\d[\d, ]*)\s*(BCE|BC|CE|AD)?$/i);
 let year=match?Number(match[1].replaceAll(/[ ,]/g,''))*(/^BC/i.test(match[2]??'')?-1:1):null;
 if(year===0)year=null;
 const corrected=Object.hasOwn(corrections,name);
 if(corrected)year=corrections[name];
 const era=year===null?null:eras.find(era=>year>=era.from&&year<=era.to);
 return {file:name,seconds,timestamp:timestamp(seconds),year,date:date(year),era:era?.id??'unclassified',dateSource:corrected?'manually verified':match?'OCR provisional':'unclassified',ocr:raw};
});
const manifest={source:'https://www.youtube.com/watch?v=-6Wu0Q7x5D0',title:'Ollie Bye — The History of the World: Every Year',intervalSeconds:interval,note:'Research only. Fuzzy, regional era boundaries are broad review bins, not universal transitions. Grey/blank land is not evidence of absent people or culture. Video color categories include language/civilizational groupings and cannot measure religion. OCR dates require inspection.',eraBoundaries:eras.map(era=>({...era,from:Number.isFinite(era.from)?era.from:null,to:Number.isFinite(era.to)?era.to:null})),frames};
await fs.writeFile(path.join(folder,'frame-index.json'),JSON.stringify(manifest,null,2));
const csv=['file,seconds,timestamp,year,date,era,date_source,ocr',...frames.map(frame=>[frame.file,frame.seconds,frame.timestamp,frame.year??'',frame.date,frame.era,frame.dateSource,frame.ocr].map(value=>`"${String(value).replaceAll('"','""')}"`).join(','))].join('\r\n');
await fs.writeFile(path.join(folder,'frame-index.csv'),csv);
const browser=await chromium.launch({headless:true,channel:'msedge'});
const sheets=[];
try{
 // Preserve readable date-panel crops for manual OCR auditing of every sample.
 for(let start=0;start<frames.length;start+=96){
  const cards=frames.slice(start,start+96).map(frame=>`<article><p>${frame.file} · ${frame.timestamp} · OCR ${escape(frame.ocr)}</p><div class="crop"><img src="${pathToFileURL(path.join(folder,frame.file))}"></div></article>`).join('');
  const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#13212c;color:white;font:12px sans-serif}main{display:grid;grid-template-columns:repeat(8,300px)}p{height:16px;margin:3px}.crop{position:relative;width:300px;height:80px;overflow:hidden}.crop img{position:absolute;left:0;top:-525px;width:1920px;max-width:none}</style><main>${cards}</main>`;
  const basename=`date-audit-${String(start).padStart(3,'0')}`;
  const file=path.join(folder,`${basename}.html`);await fs.writeFile(file,html);
  const page=await browser.newPage({viewport:{width:2400,height:1300}});await page.goto(pathToFileURL(file).href);await page.screenshot({path:path.join(folder,`${basename}.png`),fullPage:true});await page.close();
 }
 for(const era of [...eras,{id:'unclassified',label:'Title / end cards / unresolved dates'}]){
  const group=frames.filter(frame=>frame.era===era.id);
  for(let start=0;start<group.length;start+=12){
   const sample=group.slice(start,start+12);
   const cards=sample.map(frame=>`<article><p>${frame.timestamp} — ${escape(frame.date)} · ${frame.file}</p><a href="${pathToFileURL(path.join(folder,frame.file))}"><img src="${pathToFileURL(path.join(folder,frame.file))}"></a></article>`).join('');
   const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#111b24;color:#fff;font:14px sans-serif}h1{font-size:18px;margin:10px}small{display:block;margin:10px}main{display:grid;grid-template-columns:repeat(3,1fr);gap:6px}p{margin:3px}img{width:100%;display:block}</style><h1>Ollie Bye — The History of the World: Every Year · ${escape(era.label)} · ${interval}-second samples</h1><small>Research reference; documentary classifications and incomplete historical coverage are not simulation ground truth. Dates: OCR provisional unless manually verified.</small><main>${cards}</main>`;
   const basename=`sheet-${era.id}-${String(start).padStart(3,'0')}`;
   const file=path.join(folder,`${basename}.html`);await fs.writeFile(file,html);
   const page=await browser.newPage({viewport:{width:2400,height:1900}});await page.goto(pathToFileURL(file).href);await page.screenshot({path:path.join(folder,`${basename}.png`),fullPage:true});await page.close();
   sheets.push({era:era.id,file:`${basename}.png`,html:`${basename}.html`,frames:sample.map(frame=>frame.file)});
  }
 }
 await fs.writeFile(path.join(folder,'sheet-index.json'),JSON.stringify({sheets},null,2));
 const links=sheets.map(sheet=>`<li><a href="${sheet.html}">${escape(sheet.file)}</a> (${sheet.frames.length} frames)</li>`).join('');
 await fs.writeFile(path.join(folder,'index.html'),`<!doctype html><meta charset="utf-8"><title>Ollie Bye reference review</title><h1>Two-second reference review</h1><p>${escape(manifest.note)}</p><p>${frames.length} frames. <a href="frame-index.csv">Timestamp / date / era index</a>. <a href="frame-index.json">Full metadata</a>.</p><ul>${links}</ul>`);
 console.log(JSON.stringify({frames:frames.length,sheets:sheets.length,unclassified:frames.filter(frame=>frame.year===null).map(frame=>({file:frame.file,ocr:frame.ocr})),eras:eras.map(era=>({era:era.id,count:frames.filter(frame=>frame.era===era.id).length}))},null,2));
}finally{await browser.close();}

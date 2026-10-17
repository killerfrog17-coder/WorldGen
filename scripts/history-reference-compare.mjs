// Pair actual documentary frames with saved simulation era checkpoints.
// Usage: node scripts/history-reference-compare.mjs REFERENCE_FOLDER REVIEW_FOLDER...
// Output remains beside the external review.json. This does not calibrate rules.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {gunzipSync} from 'node:zlib';
import {chromium} from 'playwright';

const [referenceInput, ...reviewInputs] = process.argv.slice(2);
if (!referenceInput || !reviewInputs.length) throw Error('Reference and review folders are required.');
const referenceFolder = path.resolve(referenceInput);
const reference = JSON.parse(await fs.readFile(path.join(referenceFolder, 'frame-index.json'), 'utf8'));
const comparisons = [
  {name:'bronze', label:'State origins / Bronze', era:4, reference:'frame-0068.jpg'},
  {name:'iron', label:'Iron / Classical', era:5, reference:'frame-0193.jpg'},
  {name:'medieval', label:'Medieval', era:6, reference:'frame-0422.jpg'},
  {name:'early-modern', label:'Early Modern', era:7, reference:'frame-0500.jpg'},
  {name:'industrial', label:'Industrial', era:8, reference:'frame-0514.jpg'},
  {name:'modern', label:'Modern', era:9, reference:'frame-0535.jpg'},
  {name:'postmodern', label:'Postmodern onset', era:10, reference:'frame-0546.jpg'},
  {name:'final', label:'Late review endpoint', final:true, reference:'frame-0554.jpg'}
];
const escape = value => String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const browser = await chromium.launch({headless:true,channel:'msedge'});
try {
  for (const input of reviewInputs) {
    const folder = path.resolve(input);
    const review = JSON.parse(await fs.readFile(path.join(folder,'review.json'),'utf8'));
    const out = path.join(folder,'reference-comparison');
    await fs.mkdir(out,{recursive:true});
    const entries = [];
    for (const comparison of comparisons) {
      const row = comparison.final ? review.Rows.at(-1) : review.Rows.find(row =>
        comparison.era === 4 ? row.FirstState >= 0 && row.Year >= row.FirstState : row.Era === comparison.era);
      if (!row) continue;
      const frame = reference.frames.find(frame => frame.file === comparison.reference);
      if (!frame || frame.year == null) throw Error('Missing dated reference frame.');
      const local = `states-${row.Year}.png`;
      const candidates = ['screenshots','focused','focused-late'].map(dir => path.join(folder,dir,local));
      let image;
      for (const candidate of candidates) if (await fs.access(candidate).then(()=>true,()=>false)) { image=candidate; break; }
      if (!image) {
        image = path.join(out,local);
        const svg = gunzipSync(await fs.readFile(path.join(folder,`year-${row.Year}`,`States-${row.Year}.svg.gz`))).toString('utf8');
        const page = await browser.newPage({viewport:{width:1800,height:960}});
        await page.setContent('<style>body{margin:0}svg{display:block}</style>'+svg,{timeout:120000,waitUntil:'load'});
        await page.screenshot({path:image}); await page.close();
      }
      const refImage = path.join(referenceFolder,frame.file);
      const html = `<!doctype html><meta charset="utf-8"><style>body{margin:0;color:#fff;background:#10202b;font:22px sans-serif}h1{font-size:30px;margin:12px}p{margin:10px}main{display:grid;grid-template-columns:1800px 1800px;gap:8px}h2{font-size:25px;margin:10px}img{display:block;width:1800px}</style><h1>${escape(path.basename(folder))}: ${comparison.label}</h1><p>Structural comparison only. Different dates, categories and population visibility. Video blank land does not mean no people; video colors do not identify religion. No exact-country target.</p><main><article><h2>Ollie Bye: ${frame.date} · video ${frame.timestamp}</h2><p>Actual source frame, ${reference.intervalSeconds}-second extraction. Broad era bin; not a universal boundary.</p><img src="${pathToFileURL(refImage).href}"></article><article><h2>Simulation: elapsed ${row.Year} · ${row.Year-row.FirstState}y after first state</h2><p>${row.States} states · ${row.ActiveTowns} active towns · ${(row.Population/1e6).toFixed(1)}M people. Tint = ruling culture; outlines also include pre-state cultures.</p><img src="${pathToFileURL(image).href}"></article></main>`;
      const htmlPath = path.join(out,`${comparison.name}.html`);
      await fs.writeFile(htmlPath,html);
      const page = await browser.newPage({viewport:{width:3608,height:1200}});
      await page.goto(pathToFileURL(htmlPath).href,{waitUntil:'load'});
      const pngPath = htmlPath.replace('.html','.png');
      await page.screenshot({path:pngPath,fullPage:true}); await page.close();
      entries.push({era:comparison.label,reference:{...frame,image:refImage},simulation:{year:row.Year,era:row.EraName,firstState:row.FirstState,image},comparison:pngPath});
    }
    await fs.writeFile(path.join(out,'comparisons.json'),JSON.stringify({source:reference.source,entries},null,2));
    console.log(`${path.basename(folder)}: ${entries.length} actual reference/simulation pairs`);
  }
} finally { await browser.close(); }

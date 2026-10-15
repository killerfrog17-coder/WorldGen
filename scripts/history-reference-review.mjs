// Independent research visualization, not simulation input or a training target.
// Cliopatria/Seshat (CC BY 4.0): historical territorial reconstructions have
// uneven coverage, nested dependencies and uncertain borders.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const input=path.resolve(process.argv[2]),folder=path.resolve(process.argv[3]);
await fs.mkdir(folder,{recursive:true});
const {features}=JSON.parse(await fs.readFile(input,'utf8'));
const years=[-3000,-1500,-500,100,800,1500,1800,1950,2024];
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
function geometry(g){
 if(!g)return '';
 const polygons=g.type==='Polygon'?[g.coordinates]:g.type==='MultiPolygon'?g.coordinates:[];
 return polygons.map(poly=>{
  let origin=null;
  const rings=poly.map(ring=>{
   let last=origin;
   return ring.map(([lon,lat],index)=>{
    if(last!==null){while(lon-last>180)lon-=360;while(lon-last< -180)lon+=360;}
    last=lon;if(origin===null)origin=lon;
    return `${index?'L':'M'}${((lon+180)*5).toFixed(2)},${((90-lat)*5).toFixed(2)}`;
   }).join('')+'Z';
  }).join('');
  return [-1800,0,1800].map(offset=>`<path transform="translate(${offset},0)" d="${rings}"/>`).join('');
 }).join('');
}
function color(name){let hash=2166136261;for(const c of name)hash=Math.imul(hash^c.charCodeAt(0),16777619);return `hsl(${(hash>>>0)%360} 48% 63%)`;}
const base=features.filter(f=>f.properties.FromYear<=2024&&f.properties.ToYear>=2024).map(f=>geometry(f.geometry)).join('');
const report=[];
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const year of years){
  const rows=features.filter(f=>f.properties.FromYear<=year&&f.properties.ToYear>=year&&f.properties.Type==='POLITY').sort((a,b)=>b.properties.Area-a.properties.Area||a.properties.Name.localeCompare(b.properties.Name,'en'));
  const body=rows.map(f=>`<g fill="${color(f.properties.Name)}"><title>${escape(f.properties.Name)}</title>${geometry(f.geometry)}</g>`).join('');
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="940" viewBox="0 0 1800 940"><defs><clipPath id="map"><rect width="1800" height="900"/></clipPath></defs><rect width="1800" height="940" fill="#8eb5d7"/><g clip-path="url(#map)"><g fill="#eee">${base}</g><g stroke="#354451" stroke-width=".5" fill-rule="evenodd">${body}</g></g><rect y="900" width="1800" height="40" fill="#182833"/><text x="15" y="925" fill="white" font-family="sans-serif" font-size="16">${year<0?-year+' BCE':year+' CE'} — Cliopatria / Seshat, CC BY 4.0. Reconstructed territorial claims; incomplete and overlapping coverage.</text></svg>`;
  await fs.writeFile(path.join(folder,`reference-${year}.svg`),svg);
  const page=await browser.newPage({viewport:{width:1800,height:940}});await page.setContent(svg);await page.screenshot({path:path.join(folder,`reference-${year}.png`)});await page.close();
  const independent=rows.filter(f=>!f.properties.MemberOf);
  report.push({Year:year,Records:rows.length,TopLevelRecords:independent.length,Largest:independent.slice(0,10).map(f=>({Name:f.properties.Name,AreaKm2:f.properties.Area}))});
 }
 const cards=years.map(year=>`<article><h2>${year<0?-year+' BCE':year+' CE'}</h2><img src="${pathToFileURL(path.join(folder,`reference-${year}.png`))}"></article>`).join('');
 const html=`<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#101b24;color:white;font:16px sans-serif}main{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;padding:12px}article{background:#20313d}h2{margin:6px;font-size:18px}img{display:block;width:100%}</style><main>${cards}</main>`;
 const file=path.join(folder,'reference-overview.html');await fs.writeFile(file,html);const page=await browser.newPage({viewport:{width:1800,height:1120}});await page.goto(pathToFileURL(file).href);await page.screenshot({path:path.join(folder,'reference-overview.png'),fullPage:true});await page.close();
 await fs.writeFile(path.join(folder,'reference-statistics.json'),JSON.stringify({Source:'https://github.com/Seshat-Global-History-Databank/cliopatria',License:'CC BY 4.0',Note:'Research only. Top-level status and temporal sampling are imperfect; these are not calibration targets.',Snapshots:report},null,2));
}finally{await browser.close();}

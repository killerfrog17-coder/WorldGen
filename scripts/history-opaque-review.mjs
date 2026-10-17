// Render offline opaque state/faith diagnostics and summarize exported identities.
// Usage: node scripts/history-opaque-review.mjs OPAQUE_FOLDER...
import fs from 'node:fs/promises';
import path from 'node:path';
import {gunzipSync} from 'node:zlib';
import {chromium} from 'playwright';
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const input of process.argv.slice(2)){
  const folder=path.resolve(input);
  for(const file of (await fs.readdir(folder)).filter(file=>/^(States|Religions)-\d+\.svg\.gz$/.test(file))){
   const page=await browser.newPage({viewport:{width:1800,height:960}});
   await page.setContent('<style>body{margin:0}svg{display:block}</style>'+gunzipSync(await fs.readFile(path.join(folder,file))).toString('utf8'),{timeout:120000,waitUntil:'load'});
   await page.screenshot({path:path.join(folder,file.replace('.svg.gz','.png'))});await page.close();
  }
  const data=JSON.parse(await fs.readFile(path.join(folder,'religions.json'),'utf8'));
  const identities=new Map(data.Religions.map(religion=>[religion.Id,religion]));
  const totalPopulation=data.Towns.reduce((sum,town)=>sum+town.Population,0);
  const rank=selector=>{
   const groups=new Map();
   for(const town of data.Towns){const id=selector(town);if(id===undefined||id===null||id<0)continue;const item=groups.get(id)??{id,towns:0,population:0};item.towns++;item.population+=town.Population;groups.set(id,item);}
   return [...groups.values()].sort((a,b)=>b.population-a.population||a.id-b.id).map(item=>({...item,name:identities.get(item.id)?.Name??'Unknown',shareOfObservedTownPopulation:totalPopulation?item.population/totalPopulation:0}));
  };
  const publics=rank(town=>town.Public),households=rank(town=>town.Community?.Household);
  const summary={year:data.Year,recordedReligions:data.Religions.length,observedTowns:data.Towns.length,observedTownPopulation:totalPopulation,publicIdentities:publics.length,householdIdentities:households.length,publics,households,householdDiffersFromPublic:data.Towns.filter(town=>town.Community?.Household>=0&&town.Community.Household!==town.Public).length,notes:'Observations are active towns, not a census of all people or cell area. Public identity and household affiliation are distinct. Colors do not identify theology; generated names are fictional.'};
  await fs.writeFile(path.join(folder,'religion-summary.json'),JSON.stringify(summary,null,2));
  console.log(JSON.stringify({folder,year:summary.year,recordedReligions:summary.recordedReligions,towns:summary.observedTowns,publics:summary.publicIdentities,households:summary.householdIdentities,topPublic:publics[0],differentHouseholds:summary.householdDiffersFromPublic}));
 }
}finally{await browser.close();}

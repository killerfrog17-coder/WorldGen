// Offline retained-parent lineages only. No world load, belief census or model calculation.
// Usage: node scripts/history-religion-families.mjs RELIGION_EVIDENCE_JSON
import fs from 'node:fs/promises';
import path from 'node:path';

const input=process.argv[2];
if(!input)throw Error('Religion evidence.json is required.');
const source=path.resolve(input),data=JSON.parse(await fs.readFile(source,'utf8'));
const faiths=new Map(data.Faiths.map(f=>[f.Id,f]));
const resolved=new Map();
function rootOf(id){
  if(resolved.has(id))return resolved.get(id);
  const visited=[],seen=new Set();let current=id;
  for(let steps=0;steps<=faiths.size;steps++){
    if(seen.has(current))throw Error(`Retained ParentId cycle at religion ${current}.`);
    seen.add(current);visited.push(current);
    const faith=faiths.get(current);
    if(!faith)throw Error(`Missing retained parent religion ${current}.`);
    if(faith.ParentId<0){for(const item of visited)resolved.set(item,current);return current;}
    current=faith.ParentId;
  }
  throw Error('Religion ancestry exceeded bounded actor count.');
}
for(const id of faiths.keys())rootOf(id);
function depthOf(id){let depth=0;for(let steps=0;steps<faiths.size;steps++){const f=faiths.get(id);if(f.ParentId<0)return depth;depth++;id=f.ParentId;}throw Error('Unbounded retained lineage.');}
const families=new Map();
for(const faith of faiths.values()){
  const root=rootOf(faith.Id);
  if(!families.has(root))families.set(root,{root,faiths:[],towns:[]});
  families.get(root).faiths.push(faith);
}
for(const town of data.Towns)if(town.Public>=0){
  const family=families.get(rootOf(town.Public));
  family.towns.push(town);
}
const totalTowns=data.Towns.length,totalUrban=data.Towns.reduce((sum,t)=>sum+t.Population,0);
const rows=[...families.values()].map(family=>{
  const root=faiths.get(family.root),towns=family.towns;
  const urban=towns.reduce((sum,t)=>sum+t.Population,0);
  const depths=towns.map(t=>depthOf(t.Public)).sort((a,b)=>a-b);
  const counts=key=>Object.fromEntries([...new Set(towns.map(t=>faiths.get(t.Public)[key]))].sort().map(value=>[value,towns.filter(t=>faiths.get(t.Public)[key]===value).length]));
  return {Root:root.Id,Name:root.Name,RootReach:root.Reach,RootVocation:root.Vocation,
    RootFoundedYear:root.FoundedYear,RootAge:data.Years-root.FoundedYear,RetainedDescendants:family.faiths.length,
    PublicDescendants:family.faiths.filter(f=>f.PublicTowns>0).length,
    PublicTownLineageDepth:{Maximum:depths.at(-1)??0,Median:depths.length?(depths.length%2?depths[depths.length>>1]:(depths[depths.length/2-1]+depths[depths.length/2])/2):0},
    PublicTowns:towns.length,ShareOfAllActiveTowns:totalTowns?towns.length/totalTowns:0,
    UrbanResidentsUnderPublicPredominance:urban,ShareOfActiveTownResidents:totalUrban?urban/totalUrban:0,
    TownCultures:new Set(towns.map(t=>t.CultureId)).size,
    TownCourts:new Set(towns.filter(t=>t.Owner>=0).map(t=>t.Owner)).size,
    CurrentReachTownCounts:counts('Reach'),CurrentVocationTownCounts:counts('Vocation'),
    DescendantIds:family.faiths.map(f=>f.Id).sort((a,b)=>a-b),
    LargestCurrentTraditions:family.faiths.filter(f=>f.PublicTowns>0).sort((a,b)=>b.PublicTowns-a.PublicTowns||a.Id-b.Id).slice(0,8).map(f=>({Id:f.Id,Name:f.Name,ParentId:f.ParentId,Reach:f.Reach,Vocation:f.Vocation,PublicTowns:f.PublicTowns}))};
}).sort((a,b)=>b.PublicTowns-a.PublicTowns||a.Root-b.Root);
const output=path.join(path.dirname(source),'religion-family-review.json');
await fs.writeFile(output,JSON.stringify({Source:source,Hash:data.Hash,Year:data.Years,ActiveTowns:totalTowns,ActiveTownResidents:totalUrban,
  Roots:rows.length,PublicRoots:rows.filter(row=>row.PublicTowns>0).length,
  LargestIndividualTraditions:data.Faiths.filter(f=>f.PublicTowns>0).slice().sort((a,b)=>b.PublicTowns-a.PublicTowns).slice(0,8).map(f=>({Id:f.Id,Name:f.Name,Reach:f.Reach,PublicTowns:f.PublicTowns,ShareOfAllActiveTowns:totalTowns?f.PublicTowns/totalTowns:0})),
  Families:rows,Notes:[
    'Bounded offline walk follows only the saved single ParentId, not inferred similarity, palette color, political identity or a recurring runtime census.',
    'Parent lineage is not necessarily a coherent present-day institution or mutually recognized religious family. A synthesis may retain only one parent; unrecorded secondary ancestry cannot be reconstructed.',
    'A deeply prehistoric common ancestor may link traditions that now differ strongly; retained ancestry is not proof of a living doctrinal identity or an organized world religion.',
    'Town counts and urban residents under public predominance are not believer counts or shares of total world population. Rural adherents and mixed local belief are not enumerated.',
    'Current descendant Reach/Vocation counts preserve internal differences rather than inheriting the root category automatically.'
  ]},null,2));
console.log(JSON.stringify({output,year:data.Years,publicRoots:rows.filter(r=>r.PublicTowns>0).length,top:rows.slice(0,5).map(r=>({root:r.Root,name:r.Name,towns:r.PublicTowns,share:r.ShareOfAllActiveTowns,urbanShare:r.ShareOfActiveTownResidents}))}));

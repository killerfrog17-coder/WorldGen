// Offline interpretation of the explicit continental diagnostic, never physics.
// Usage: node scripts/history-continental-audit-summary.mjs AUDIT_JSON
import fs from 'node:fs/promises';
import path from 'node:path';

const filename=process.argv[2];
if(!filename)throw Error('Pass an external continental-diffusion.json.');
const a=JSON.parse(await fs.readFile(filename,'utf8'));
const regions=new Map(a.Regions.map(r=>[r.Id,r]));
const cultures=new Map(a.Cultures.map(c=>[c.Id,c]));
const source=a.FirstCourt.Region,sourceLand=a.FirstCourt.Landmass;
const land=a.Edges.filter(e=>e.SameLandmass&&regions.get(e.From).Landmass===sourceLand&&regions.get(e.To).Landmass===sourceLand);
const outgoing=new Map();
for(const e of land){if(!outgoing.has(e.From))outgoing.set(e.From,[]);outgoing.get(e.From).push(e);}
function reach(allowed){
  const seen=new Set([source]),queue=[source];
  for(let i=0;i<queue.length;i++)for(const e of outgoing.get(queue[i])??[])if(allowed(e)&&!seen.has(e.To)){seen.add(e.To);queue.push(e.To);}
  return seen;
}
const current=reach(e=>e.Inhabited&&e.Donor>=0&&e.BoundaryAllowed);
const physical=reach(e=>e.FixedDonorBoundaryAllowed);
if(current.size!==a.CurrentDonorReach||physical.size!==a.PhysicalReach)throw Error('Reach verification disagrees with published audit.');
function region(r){
  const c=cultures.get(r.Culture),teacher=cultures.get(r.Teacher);
  return {...r,CultureName:c?.Name??null,KnowledgeEra:c?.KnowledgeEra??null,CropPractice:c?.Crops.Practice??null,
    TeacherName:teacher?.Name??null,TeacherKnowledgeEra:teacher?.KnowledgeEra??null};
}
function edge(e){
  return {...e,FromLatitude:regions.get(e.From).Latitude,FromLongitude:regions.get(e.From).Longitude,
    ToLatitude:regions.get(e.To).Latitude,ToLongitude:regions.get(e.To).Longitude,
    EffectiveGateKm:e.BoundaryTransmission>0?(e.BoundaryKm??e.BestCellKm)/e.BoundaryTransmission:null,
    GateLimitKm:e.LandTransport==null?null:600*e.LandTransport,
    AnnualTransitIfNoHardGate:e.AnnualTransit,
    MeanWaitIfNoHardGate:e.AnnualTransit>0?1/e.AnnualTransit:null};
}
const cut=land.filter(e=>current.has(e.From)&&!current.has(e.To));
const cutCounts={edges:cut.length,noDonor:cut.filter(e=>e.Donor<0).length,
  noInhabitedPair:cut.filter(e=>!e.Inhabited).length,routeGateRejected:cut.filter(e=>!e.BoundaryAllowed).length};
const selectedTowns=a.Towns.map(t=>({Id:t.Id,Name:t.Name,Latitude:t.Latitude,Longitude:t.Longitude,Culture:t.CultureId,
  CultureName:cultures.get(t.CultureId)?.Name,KnowledgeEra:cultures.get(t.CultureId)?.KnowledgeEra,
  CropPractice:cultures.get(t.CultureId)?.Crops.Practice,Owner:t.PolityId,Population:t.Population,Age:t.Age,
  Rural:t.Rural,RuralCapacity:t.RuralCapacity,FoodCapacity:t.FoodCapacity,FoodReserve:t.FoodReserve,
  Farming:t.Farming,Institution:t.Institution,Supported:t.Supported,DeclineYears:t.DeclineYears,
  Proto:t.Proto,CivicPreparationYears:t.CivicPreparationYears,CivicExampleExposure:t.CivicExampleExposure,
  FounderAvailability:t.FounderAvailability,LocalTownRate:t.LocalTownRate,
  MeanYearsBetweenOpportunities:t.LocalTownRate>0?1/t.LocalTownRate:null,
  Nearest:t.Nearest,Candidates:t.Candidates.map(({Habitat,...c})=>c)}));
const summary={Year:a.Year,CheckpointHash:a.CheckpointHash,CoreSha256:a.CoreSha256,FirstCourt:a.FirstCourt,
  RawReach:a.RawReach,PhysicalReach:a.PhysicalReach,CurrentDonorReach:a.CurrentDonorReach,
  CurrentComponent:[...current].sort((x,y)=>x-y).map(id=>region(regions.get(id))),CurrentCutCounts:cutCounts,
  CurrentCut:cut.map(edge),PhysicalComponent:[...physical].sort((x,y)=>x-y).map(id=>region(regions.get(id))),
  Targets:a.Paths.map(p=>({Name:p.Name,Region:region(regions.get(p.Region)),RawConnected:p.RawRoute.Reachable,
    FirstCulturePhysicalConnected:p.FirstCulturePhysicalRoute.Reachable,CurrentDonorConnected:p.CurrentDonorRoute.Reachable,
    RawAnchorDistance:p.RawRoute.Cost,RawPath:p.RawRoute.Edges.map(edge)})),Towns:selectedTowns,
  Caveats:[...a.Notes,'Cut rejection counts overlap. Present snapshot reach does not prove the same gate was closed at every historical year.',
    'Raw anchor paths prove regional graph connectivity, not a paid traversable journey. No location is a required homeland.',
    'Current proposal adaptation/funding tests are one year, not a full history of failed proposals.']};
const output=path.join(path.dirname(filename),'continental-summary.json');
await fs.writeFile(output,JSON.stringify(summary,null,2));
console.log(JSON.stringify({output,year:a.Year,reach:[a.RawReach,a.PhysicalReach,a.CurrentDonorReach],cut:cutCounts,
  targets:summary.Targets.map(t=>({name:t.Name,region:t.Region.Id,residents:t.Region.ContactResidents,
    culture:t.Region.CultureName,teacher:t.Region.TeacherName,exposure:t.Region.Exposure,civic:t.Region.CivicExposure,
    connected:t.CurrentDonorConnected}))},null,2));

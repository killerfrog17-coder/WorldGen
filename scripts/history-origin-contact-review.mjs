// Read-only join of archived origin/ancestry evidence with actual saved town
// opportunities. No world load, replay, authoritative calculation or RNG.
// Usage: node scripts/history-origin-contact-review.mjs REVIEW_FOLDER ORIGIN_AUDIT
import fs from 'node:fs/promises';
import path from 'node:path';
const [folderInput,auditInput]=process.argv.slice(2);
if(!folderInput||!auditInput)throw Error('Review folder and saved origin audit are required.');
const folder=path.resolve(folderInput),auditPath=path.resolve(auditInput);
const [review,audit,chronology]=await Promise.all(['review.json',auditPath,'political-chronology.json'].map(async file=>JSON.parse(await fs.readFile(path.isAbsolute(file)?file:path.join(folder,file),'utf8'))));
if(!Array.isArray(audit.Origins)||!Array.isArray(review.Rows))throw Error('Unexpected diagnostic schema.');
const firstState=review.Rows.find(row=>row.FirstState>=0)?.FirstState;
if(!Number.isInteger(firstState))throw Error('No first-state observation.');
const cache=new Map();
async function opportunities(year){
  if(!cache.has(year))cache.set(year,JSON.parse(await fs.readFile(path.join(folder,`year-${year}`,'opportunities.json'),'utf8')));
  return cache.get(year);
}
const origins=[];
for(const origin of audit.Origins){
  const candidateRows=review.Rows.filter(row=>row.Year<origin.Year&&row.Year>=origin.FounderTown.FoundedYear);
  const selected=[...new Set([...candidateRows.slice(-2).map(row=>row.Year),candidateRows[0]?.Year].filter(Number.isInteger))].sort((a,b)=>a-b);
  const observations=[];
  for(const year of selected){
    const snapshot=await opportunities(year),town=snapshot.Towns.find(town=>town.Id===origin.SettlementId);
    observations.push(town?{year,population:town.Population,farming:town.Farming,fiscal:town.Fiscal,institution:town.Institution,
      supported:town.Supported,proto:town.Proto,protoSince:town.ProtoSince,preparation:town.Preparation,
      townCivicTeacher:town.CivicTeacherPolityId,townCivicExposure:town.CivicExampleExposure,
      regionalCivicTeacher:town.CivicTeacher,regionalCivicExposure:town.CivicExposure,reserveYears:town.ReserveYears}:
      {year,notInOpportunityExport:true});
  }
  const protoEvents=chronology.Events.filter(event=>event.SettlementId===origin.SettlementId&&event.Kind==='proto-urban-center'&&event.Year<=origin.Year);
  const proto=protoEvents.at(-1),currentCulture=origin.Ancestry[0];
  const earlierCropIntroductions=[];
  currentCulture.SourceCulture.forEach((donor,group)=>{
    const adopted=currentCulture.AdoptedYear[group];
    if(donor>=0&&adopted>0&&adopted<=origin.Year)earlierCropIntroductions.push({group,donor,adopted});
  });
  origins.push({year:origin.Year,afterFirstState:origin.Year-firstState,culture:origin.CultureId,town:origin.FounderTown,
    foundingPlace:origin.FoundingPlace,protoYear:proto?.Year??null,yearsProtoToCourt:proto?origin.Year-proto.Year:null,
    observations,earlierCropIntroductions,earlierAncestralInstitutionEvents:origin.EarlierInstitutionEvents});
}
const output=path.join(folder,'origin-contact-review.json');
await fs.writeFile(output,JSON.stringify({sourceAudit:auditPath,sourceHash:audit.Hash,firstState,origins,notes:[
  'Independent founding is a local political branch, not proof of pristine farming, no foreign knowledge or no previous exchange.',
  'Actual pre-founding town and regional civic fields are measured at each listed checkpoint, not interpolated between checkpoints.',
  'A town absent from opportunities.json may be smaller than its800-resident export threshold; this does not prove nonexistence.',
  'Crop SourceCulture and AdoptedYear retain introduction provenance. A prior introduced crop proves preceding agrarian borrowing/inheritance, not civic instruction or an exact migration route.',
  'Parent-chain absence of earlier courts and zero civic exposure at sampled times constrain classification but do not prove an exhaustive negative contact history.',
  'No whole-world inhabitants, modern homelands, exact real-Earth state identities or required European colonial priority are inferred.'
]},null,2));
console.log(`${path.basename(folder)}: ${origins.length} origin/contact joins; metadata only, source ${audit.Hash}`);

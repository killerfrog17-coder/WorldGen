// Summarize an offline state audit. No world load or model calculation.
// Usage: node scripts/history-state-continuity-review.mjs STATE_AUDIT REVIEW_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
const [auditInput,folderInput]=process.argv.slice(2);
if(!auditInput||!folderInput)throw Error('State audit and review folder required.');
const folder=path.resolve(folderInput),audit=JSON.parse(await fs.readFile(path.resolve(auditInput),'utf8'));
const review=JSON.parse(await fs.readFile(path.join(folder,'review.json'),'utf8'));
const states=new Map(audit.States.map(state=>[state.Id,state]));
function distribution(ages){
  ages.sort((a,b)=>a-b);const n=ages.length;
  return {count:n,median:n?(n%2?ages[n>>1]:(ages[n/2-1]+ages[n/2])/2):0,max:n?ages.at(-1):0,
    thresholds:[500,1000,2000,4000].map(years=>({overYears:years,count:ages.filter(age=>age>years).length,share:n?ages.filter(age=>age>years).length/n:0}))};
}
const snapshots=[];
for(const row of review.Rows.filter(row=>row.FirstState>=0)){
  const towns=JSON.parse(await fs.readFile(path.join(folder,`year-${row.Year}`,'towns.json'),'utf8'));
  const ids=[...new Set(towns.filter(town=>town.PolityId>=0).map(town=>town.PolityId))];
  const priorEndedRawOwners=ids.filter(id=>states.get(id).EndedYear>=0&&states.get(id).EndedYear<row.Year);
  const boundary=audit.States.filter(state=>state.FoundedYear===row.Year||state.EndedYear===row.Year);
  const recordedAlive=audit.States.filter(state=>state.FoundedYear<=row.Year&&(state.EndedYear<0||state.EndedYear>row.Year));
  snapshots.push({year:row.Year,era:row.EraName,reportedStates:row.States,
    recordedIntervals:distribution(recordedAlive.map(state=>row.Year-state.FoundedYear)),
    boundaryStates:boundary.map(state=>({id:state.Id,foundation:state.FoundedYear,end:state.EndedYear})),
    snapshotRawOwnerCount:ids.length,priorEndedRawOwners,
    snapshotRawOwnerAges:distribution(ids.map(id=>row.Year-states.get(id).FoundedYear)),
    confidence:priorEndedRawOwners.length||ids.length!==row.States?'Raw-owner group is not a complete resolved living-state census.':
      'Raw-owner count matches recorded living-state count, with no owner known ended before this year; same-year alias/order uncertainty remains explicit.'});
}
const focus=states.get(audit.Focus.Id),events=audit.Focus.Events;
const sovereigns=audit.CurrentSovereignGroups??null;
const controlledPopulation=sovereigns?.reduce((total,group)=>total+group.ExclusiveCourtPopulations,0)??0;
const controlledArea=sovereigns?.reduce((total,group)=>total+group.ExclusiveCourtAreaKm2,0)??0;
const controlledTownResidents=sovereigns?.reduce((total,group)=>total+group.TownResidents,0)??0;
const consolidation=sovereigns?{
  currentCourts:audit.FinalDistribution.LivingStates,actualSovereigns:sovereigns.length,
  oneTownSovereigns:sovereigns.filter(group=>group.TownCount===1).length,
  controlledPopulation,controlledAreaKm2:controlledArea,controlledTownResidents,
  recognizedTitleAreaKm2:audit.States.filter(state=>state.EndedYear<0).reduce((sum,state)=>sum+(state.RecognizedAreaKm2??0),0),
  topByPopulation:[...sovereigns].sort((a,b)=>b.ExclusiveCourtPopulations-a.ExclusiveCourtPopulations).slice(0,8).map(group=>({...group,
    shareOfControlledPopulation:controlledPopulation?group.ExclusiveCourtPopulations/controlledPopulation:0,
    shareOfControlledTownResidents:controlledTownResidents?group.TownResidents/controlledTownResidents:0})),
  topByArea:[...sovereigns].sort((a,b)=>b.ExclusiveCourtAreaKm2-a.ExclusiveCourtAreaKm2).slice(0,8).map(group=>({...group,
    shareOfControlledArea:controlledArea?group.ExclusiveCourtAreaKm2/controlledArea:0})),
  notes:['Current sovereign IDs resolve actual bounded imperial patrons, not ruling-culture palette colors.',
    'Denominators are summed exclusive owned-court caches, not all world people or land. Recognized title area is separate from actual owned area.',
    'These are final saved accounts only; they do not reconstruct earlier allegiance, borders or size. No required country count or automatic lifetime is imposed.']
}:null;
const capitalSamples=events.filter(event=>event.PolityId===focus.Id&&['secession','succession','election','regime-change','realm-succession-crisis'].includes(event.Kind));
const capitalChanges=[];
for(const event of capitalSamples)if(capitalChanges.at(-1)?.place.Cell!==event.Place.Cell)capitalChanges.push({year:event.Year,kind:event.Kind,place:event.Place});
const output=path.join(folder,'state-continuity-review.json');
await fs.writeFile(output,JSON.stringify({source:path.resolve(auditInput),sourceHash:audit.Hash,finalYear:audit.Year,
  finalDistribution:distribution(audit.States.filter(state=>state.EndedYear<0).map(state=>audit.Year-state.FoundedYear)),finalSovereignty:consolidation,snapshots,
  focus:{state:focus,eventsByKind:events.reduce((all,event)=>(all[event.Kind]=(all[event.Kind]??0)+1,all),{}),
    capitalChanges,minimumCapitalMoves:Math.max(0,capitalChanges.length-1),
    recordedCaptureWins:audit.Focus.Captures.filter(event=>event.FocusWon).length,
    recordedCaptureLosses:audit.Focus.Captures.filter(event=>event.FocusLost).length,
    loggedBattleWins:events.filter(event=>event.Kind==='field-battle'&&event.PolityId===focus.Id).length,
    loggedBattleLosses:events.filter(event=>event.Kind==='field-battle'&&event.OtherPolityId===focus.Id).length,
    military:audit.Focus.MilitaryGovernment},notes:[
      'Final distribution uses actual final EndedYear<0, not an approximate reconstructed era interval.',
      'Year-only archive intervals can disagree with intra-year saved rows. Boundary records and actual snapshot raw-owner counts are included; do not silently substitute either for an exact resolved historical census.',
      'Capital-change count is a lower bound from actual capital-bearing events, not an invented complete relocation log.',
      'Dead-state aggregate population/area/military caches do not describe live final holdings. Declared wars alone do not establish credible threats.',
      'No automatic expiry, source edit, rule change, replay or new saved-world load.'
    ]},null,2));
console.log(`${path.basename(folder)}: final ${audit.Year}; ${snapshots.length} snapshot joins; focus${focus.Id} ended${focus.EndedYear}, >=${capitalChanges.length-1} observed capital moves`);

// Compact evidence from the frozen sparse relay chronology; no world load.
import fs from 'node:fs/promises';
import path from 'node:path';
import {gzipSync} from 'node:zlib';
const folder=path.resolve(process.argv[2]??'');
const lines=(await fs.readFile(path.join(folder,'timeline.jsonl'),'utf8')).trim().split(/\r?\n/);
const rows=[];
for(let i=0;i<lines.length;i++){
 try{rows.push(JSON.parse(lines[i]));}catch(error){if(i!==lines.length-1)throw error;}
}
if(!rows.length)throw Error('No completed timeline rows.');
const summary=await fs.readFile(path.join(folder,'summary.json'),'utf8').then(JSON.parse,error=>{if(error.code==='ENOENT')return null;throw error;});
const selectedYears=new Set([171000,172600,173100,175100,178000,178800,178900,179000,179800]);
const selected=rows.filter(row=>selectedYears.has(row.Year));
const fields=selected.map(row=>({year:row.Year,regions:row.AdditionalEvidence.Regions.map(region=>({...region,
 neighbors:region.Neighbors.map(n=>({route:n.Route,source:n.SourceRegion,sourceResidents:n.SourceContactResidents,sourceRaw:n.SourceRaw,
 sourceLiveTowns:n.SourceRegionLiveTownCount,sourceLocalCulture:n.SourceLocalCulture,retainedTeacher:n.SourceRetainedTeacher,
 chosenDonor:n.ChosenRegionalDonor,sourceLocalKnowledge:n.SourceLocalKnowledgeCount,retainedKnowledge:n.RetainedTeacherKnowledgeCount,
 receipt:n.SourceReceipt,exposure:n.SourceTeacherExposure,from:n.Departure,to:n.Arrival,sea:n.Sea,
 physicalGate:n.ActualPhysicalGateAllowed,transmission:n.EnvironmentalTransmission,effectiveKnowledgePackage:n.ActualFilteredKnowledgePackage,
 chosenOffers:n.ChosenDonorOffers,retainedOffers:n.RetainedTeacherOffers})),Neighbors:undefined
 }))}));
const sources=[14,65,51,269,517,569,617].map(id=>({id,observations:selected.flatMap(row=>{
 const c=row.AdditionalEvidence.Cultures.find(c=>c.Culture===id);if(!c)return [];
 return [{year:row.Year,name:c.Name,foundation:c.FoundedYear,Farming:c.Farming,Towns:c.TownCount,origin:c.Origin,
 groups:c.OwnGroups,actualPeers:c.ActualReceivedPeers}];
})})).filter(source=>source.observations.length);
const candidates=[];
for(const row of rows)for(const region of row.AdditionalEvidence.Regions)for(const n of region.Neighbors)
 for(const original of n.RetainedTeacherOffers??[]){
  const chosen=n.ChosenDonorOffers.find(group=>group.Group===original.Group);
  if(n.ActualPhysicalGateAllowed&&original.SenderAdoptedYear>0&&original.ProfileFitNowAtTarget>.12&&(!chosen||chosen.SenderAdoptedYear<=0||chosen.ProfileFitNowAtTarget<=.12))
   candidates.push({year:row.Year,region:region.Region,route:n.Route,sourceRegion:n.SourceRegion,chosen:n.ChosenRegionalDonor,
    retained:n.SourceRetainedTeacher,group:original.Group,chosenFit:chosen?.ProfileFitNowAtTarget??0,retainedFit:original.ProfileFitNowAtTarget,
    sourceLastGenericReceipt:n.SourceReceipt,retainedAdoptedYear:original.SenderAdoptedYear,
    warning:'Potential teacher-choice representation gap only. Generic regional receipt does not prove a specific held/forwarded seed genotype.'});
 }
const output={completed:summary!==null,firstYear:rows[0].Year,lastYear:rows.at(-1).Year,rows:rows.length,
 allProbeHashesPreserved:rows.every(row=>row.ProbeHashPreserved),exactFinalMatch:summary?.ExactFinalMatch??null,summary,
 fields,sources,potentialTeacherChoiceCases:candidates,notes:[
  'Same original1000yr prestate/100yr poststate publications, frozen Q17. No physics, upgrades or replay saves.',
  'Current genotype at an old receipt is not reconstructed past genotype; all provenance/date restrictions remain explicit.',
  'A teacher with farming knowledge can have only a locally viable potato and no viable northern crop. Check actual group adoption/fits, not Farming score alone.',
  'Sparse contact lessons, crop introduction, trial seed persistence, local crop adaptation, settlement and state origins are distinct mechanisms.',
  'Potential teacher-choice cases are hypotheses, not proof of a paid seed packet that was never represented.'
 ]};
await fs.writeFile(path.join(folder,'relay-review.json'),JSON.stringify(output,null,2));
if(summary&&process.argv.includes('--gzip-source')){
 const compressed=gzipSync(await fs.readFile(path.join(folder,'timeline.jsonl')),{level:6});
 await fs.writeFile(path.join(folder,'timeline.jsonl.gz'),compressed);
 console.log(`Recoverable compressed source ${(compressed.length/1024/1024).toFixed(2)}MiB; original retained.`);
}
console.log(`Relay ${rows.length} rows through${rows.at(-1).Year}; complete=${summary!==null}; potential teacher-choice cases=${candidates.length}`);

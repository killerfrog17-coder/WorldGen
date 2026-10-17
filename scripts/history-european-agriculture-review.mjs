// Summarize completed/partial offline chronology; never loads a saved world.
// Usage: node scripts/history-european-agriculture-review.mjs TIMELINE_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
const folder=path.resolve(process.argv[2]??'');
const rows=(await fs.readFile(path.join(folder,'timeline.jsonl'),'utf8')).trim().split(/\r?\n/).filter(Boolean).map(JSON.parse);
if(!rows.length)throw Error('No timeline observations yet.');
const summary=await fs.readFile(path.join(folder,'summary.json'),'utf8').then(JSON.parse,error=>{if(error.code==='ENOENT')return null;throw error;});
const ids=[...new Set(rows.flatMap(row=>row.Cultures.map(culture=>culture.Culture)))].sort((a,b)=>a-b);
const cultures=ids.map(id=>{
  const seen=rows.flatMap(row=>row.Cultures.filter(culture=>culture.Culture===id).map(culture=>({year:row.Year,culture})));
  const first=seen[0],last=seen.at(-1);
  const groups=Array.from({length:6},(_,group)=>{
    const observations=seen.map(({year,culture})=>({year,value:culture.Groups.find(value=>value.Group===group)}));
    const dated=observations.find(row=>row.value.BestDatedCandidateEvenBelowFitGate>=0);
    const fit=observations.find(row=>row.value.CurrentAdmissibleDonor>=0);
    const practice=observations.find(row=>row.value.Practice>0),adopted=observations.find(row=>row.value.AdoptedYear>0);
    const transitions=[];
    for(const row of observations){
      const g=row.value,key=JSON.stringify([g.Block,g.CurrentAdmissibleDonor,g.AdoptedYear>0]);
      if(transitions.at(-1)?.key!==key)transitions.push({key,year:row.year,block:g.Block,donor:g.CurrentAdmissibleDonor,
        receipt:g.DonorReceiptYear,fit:g.Fit,bestDatedFit:g.BestDatedFitEvenBelowGate,practice:g.Practice,adoptedYear:g.AdoptedYear,
        phenotypeLatitude:g.Selected?.DaylengthLatitudeTenths,selectedYears:g.Selected?.SelectedYears});
    }
    return {group,firstSampledDatedCandidate:dated?{year:dated.year,donor:dated.value.BestDatedCandidateEvenBelowFitGate,receipt:dated.value.BestDatedReceipt,fit:dated.value.BestDatedFitEvenBelowGate}:null,
      firstSampledAdmissibleDonor:fit?{year:fit.year,donor:fit.value.CurrentAdmissibleDonor,receipt:fit.value.DonorReceiptYear,fit:fit.value.Fit,practice:fit.value.Practice}:null,
      firstSampledPractice:practice?.year??null,actualAdoptedYear:adopted?.value.AdoptedYear??null,
      final:last.culture.Groups.find(value=>value.Group===group),transitions:transitions.map(({key,...rest})=>rest)};
  });
  return {id,name:last.culture.Name,foundation:last.culture.FoundedYear,firstObserved:first.year,lastObserved:last.year,observations:seen.length,
    place:last.culture.EconomicPlace,finalFarming:last.culture.Farming,groups};
});
await fs.writeFile(path.join(folder,'review.json'),JSON.stringify({completed:summary!==null,firstYear:rows[0].Year,lastYear:rows.at(-1).Year,
  allProbeHashesPreserved:rows.every(row=>row.ProbeHashPreserved),exactFinalMatch:summary?.ExactFinalMatch??null,summary,cultures,
  fields:rows.map(row=>({year:row.Year,fields:row.Fields})),notes:[
    'First sampled eligibility is not an invented exact first delivery date. Contacts/fits can change between100-year observations; actual stored receipt/adoption dates are reported separately.',
    'Culture lifetime is not the residence age of all people. Fixed accepted geographic fields also track predecessor/anonymous regional populations.',
    'No inference of slow adoption from present-day rates alone: preceding no-donor, no-native and maturity blocks are visible in transitions.',
    'Best dated candidate selects positive fit only: no candidate can mean a dated adopted profile has exactly zero fit, not necessarily that every peer lacks adopted crops.',
    'Current donor calculations on already-adopted groups are diagnostic alternatives only; the actual introduction branch is skipped and inherited crops need not wait500 years.',
    'Exact final equivalence is claimed only when the completed frozen replay summary explicitly matches the original saved endpoint hash.'
  ]},null,2));
console.log(`${path.basename(folder)}: ${rows.length} observations through${rows.at(-1).Year}; ${cultures.length} current/predecessor cultures; complete=${summary!==null}`);

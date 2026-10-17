import fs from 'node:fs';

// Offline arithmetic over an existing audit export, never a world/replay hook.
const [input,output]=process.argv.slice(2);
if(!input||!output)throw new Error('Usage: node scripts/history-conquest-burden-review.mjs INPUT.json OUTPUT.json');
if(fs.existsSync(output))throw new Error('Do not overwrite audit evidence.');
const d=JSON.parse(fs.readFileSync(input,'utf8')),states=new Map(d.States.map(s=>[s.Id,s])),focus=states.get(d.Focus.Id);
const captures=d.Focus.Captures.filter(c=>c.FocusWon).sort((a,b)=>a.Year-b.Year||a.Id-b.Id),start=d.Year-100;
function advance(initial,from,events,to,cost,rate){let debt=initial,last=from;for(const e of events){debt=Math.max(0,debt-rate*(e.Year-last));debt=Math.min(1000,debt+cost);last=e.Year;}return Math.max(0,debt-rate*(to-last));}
const older=captures.filter(c=>c.Year<start),recent=captures.filter(c=>c.Year>=start);
// Two units/year is the MAXIMUM existing recovery; this does not assume that
// writing, funding, or calm actually existed at the earlier capture dates.
const bestRecoveryBefore=advance(0,focus.FoundedYear,older,start,90,2);
const live=d.States.filter(s=>s.EndedYear<0),total=live.reduce((n,s)=>n+s.Population,0);
const result={Source:input,Year:d.Year,Hash:d.Hash,Focus:focus.Id,HistoricalCapacityAssumption:'None: nominal fastest legal old-rule recovery2/year, no crisis/deficit pauses; omit rural/patronage debt. This is an optimistic lower-bound arithmetic model, NOT reconstructed historical budgets or technology.',
  SavedDebt:focus.CurrentCycle.ConquestDebt,SavedLastConquestYear:focus.CurrentCycle.LastConquestYear,
  TownCapturesAll:captures.length,Window:{StartBeforeFirstSameYearCapture:start,End:d.Year,TownCaptures:recent.length},
  OptimisticBeforeWindow:bestRecoveryBefore,OptimisticAfterActualTownEvents:advance(bestRecoveryBefore,start,recent,d.Year,90,2),OptimisticAfterNoTownEvents:advance(bestRecoveryBefore,start,[],d.Year,90,2),
  Sensitivity:[0,10,30,90].flatMap(cost=>[2,5,10].map(rate=>({AssumedTownCost:cost,AssumedAnnualRecovery:rate,Before:bestRecoveryBefore,After:advance(bestRecoveryBefore,start,recent,d.Year,cost,rate)}))),
  RecentCaptures:recent.map(c=>{const loser=states.get(c.OtherPolityId),prior=captures.filter(p=>p.SettlementId===c.SettlementId&&p.Year<c.Year).at(-1);return{Year:c.Year,Town:c.SettlementId,Loser:c.OtherPolityId,LoserCulture:loser?.CultureId,FocusCulture:focus.CultureId,LoserDirectParent:loser?.ParentId,LoserAgeAtCapture:loser?c.Year-loser.FoundedYear:null,PreviousRecordedFocusCapture:prior?.Year??null,RepeatRecordedCapture:!!prior};}),
  DebtDistribution:[500,600,700,800,900,950].map(threshold=>{let rows=live.filter(s=>s.CurrentCycle.ConquestDebt>=threshold);return{Threshold:threshold,Count:rows.length,OwnedPopulationShare:rows.reduce((n,s)=>n+s.Population,0)/total};}),
  Notes:['No source save/model mutation, world load, upgrade, RNG, or simulation is performed.','Sensitivity rates/costs are explicit hypothetical constants, not past measured capacity.','The old debt counter is reviewed on actor dates; continuous arithmetic can differ by review-phase rounding. Missing rural/imposed-client additions or actual recovery pauses can only increase this optimistic old-rule burden.','ParentId and shared culture are descriptive, not proof of current political legitimacy or an automatic reincorporation exemption.']};
fs.writeFileSync(output,JSON.stringify(result,null,2));
console.log(JSON.stringify({Before:result.OptimisticBeforeWindow,After:result.OptimisticAfterActualTownEvents,WithoutCaptures:result.OptimisticAfterNoTownEvents,SavedDebt:result.SavedDebt,Captures:recent.length,Repeated:result.RecentCaptures.filter(c=>c.RepeatRecordedCapture).length}));

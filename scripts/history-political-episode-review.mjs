// Offline summary of the enhanced actual-session episode; never loads a world.
// Usage: node scripts/history-political-episode-review.mjs EPISODE_FOLDER
import fs from 'node:fs/promises';
import path from 'node:path';
const folder=path.resolve(process.argv[2]??'');
if(!process.argv[2])throw Error('Episode folder required.');
const data=JSON.parse(await fs.readFile(path.join(folder,'episode.json'),'utf8'));
const rows=data.Rows,first=rows[0],last=rows.at(-1);
const baselineCampaigns=new Map(first.Campaigns.map(c=>[c.Id,c]));
const campaigns=new Map(),events=new Map(),leaders=new Map(),appearances=new Map();
for(const row of rows){
  for(const c of row.Campaigns)campaigns.set(c.Id,c);
  for(const e of row.Events)if(e.Year>=first.Year&&e.Year<=last.Year)events.set(e.Id,e);
  for(const l of row.Leaders??[])leaders.set(l.Id,l);
  for(const court of row.Courts){const list=appearances.get(court.Id)??[];list.push({year:row.Year,...court});appearances.set(court.Id,list);}
}
const campaignRows=[...campaigns.values()].map(c=>{
  const baseline=baselineCampaigns.get(c.Id),existing=!!baseline&&c.StartedYear<first.Year;
  const increment=key=>Math.max(0,(c[key]??0)-(existing?(baseline[key]??0):0));
  const prior=new Set(existing?(baseline.CapturedSettlements??[]):[]);
  return {...c,PresentAtFirstFrame:!!baseline,
    WindowCasualtiesA:increment('CasualtiesA'),WindowCasualtiesB:increment('CasualtiesB'),
    WindowVictoryCounterA:increment('VictoriesA'),WindowVictoryCounterB:increment('VictoriesB'),
    WindowConquestCounter:increment('Conquests'),
    NewlyRecordedCapturedSettlementIds:[...new Set(c.CapturedSettlements??[])].filter(id=>!prior.has(id))};
}).filter(c=>c.StartedYear<=last.Year&&(c.EndedYear<0||c.EndedYear>=first.Year));
const startCourts=new Map(first.Courts.map(c=>[c.Id,c])),endCourts=new Map(last.Courts.map(c=>[c.Id,c]));
const courtChanges=[...appearances].map(([id,list])=>{
  const start=startCourts.get(id),end=endCourts.get(id),earliest=list[0],latest=list.at(-1);
  return {id,name:latest.Name,foundation:latest.FoundedYear,firstObserved:earliest.year,lastObserved:latest.year,
    livingAtFirst:!!start,livingAtLast:!!end,
    firstCities:start?.Cities??0,lastCities:end?.Cities??0,cityDelta:(end?.Cities??0)-(start?.Cities??0),
    firstControlledCells:start?.ActualControlledFineCells??0,lastControlledCells:end?.ActualControlledFineCells??0,
    cellDelta:(end?.ActualControlledFineCells??0)-(start?.ActualControlledFineCells??0),
    observedRegimeCounterIncrease:Math.max(0,latest.RegimeChanges-earliest.RegimeChanges),
    observedSeparationCounterIncrease:Math.max(0,latest.RegionalSeparations-earliest.RegionalSeparations),
    capitalSamples:list.filter((value,index)=>index===0||value.CapitalId!==list[index-1].CapitalId).map(value=>({year:value.year,id:value.CapitalId,place:value.Capital}))};
});
const eventRows=[...events.values()].sort((a,b)=>a.Year-b.Year||a.Id-b.Id);
const total=key=>campaignRows.reduce((sum,c)=>sum+c[key],0);
const output={source:path.join(folder,'episode.json'),coreSha256:data.CoreSha256,realmRules:data.RealmRules,
  start:first.Year,end:last.Year,frames:rows.length,interval:data.Interval,
  foundations:data.FoundationObservations,
  first:{courts:first.Courts.length,sovereigns:first.Sovereigns,population:first.Population},
  last:{courts:last.Courts.length,sovereigns:last.Sovereigns,population:last.Population},
  controllerTransitions:rows.slice(1).map(row=>({year:row.Year,changedFineCells:row.ChangedControllerFineCells,pairs:row.ControllerTransitions})),
  totalObservedControllerTransitions:rows.slice(1).reduce((sum,row)=>sum+row.ChangedControllerFineCells,0),
  eventCounts:Object.fromEntries([...new Set(eventRows.map(e=>e.Kind))].sort().map(kind=>[kind,eventRows.filter(e=>e.Kind===kind).length])),
  actualConquestEvents:eventRows.filter(e=>e.Kind==='conquest'),
  regimeSuccessionEvents:eventRows.filter(e=>['regime-change','succession','election','realm-succession-crisis'].includes(e.Kind)),
  leadershipRecordsStartedWithinWindow:[...leaders.values()].filter(l=>l.StartedYear>=first.Year&&l.StartedYear<=last.Year),
  campaigns:{intersecting:campaignRows.length,startedWithinWindow:campaignRows.filter(c=>c.StartedYear>=first.Year).length,
    windowCasualtiesA:total('WindowCasualtiesA'),windowCasualtiesB:total('WindowCasualtiesB'),
    windowCampaignVictoryCounter:total('WindowVictoryCounterA')+total('WindowVictoryCounterB'),
    windowConquestCounter:total('WindowConquestCounter'),newCapturedSettlementCampaignPairs:campaignRows.reduce((sum,c)=>sum+c.NewlyRecordedCapturedSettlementIds.length,0),records:campaignRows},
  endedOrAbsentAtLast:courtChanges.filter(c=>c.livingAtFirst&&!c.livingAtLast),
  strongestRises:[...courtChanges].sort((a,b)=>b.cityDelta-a.cityDelta||b.cellDelta-a.cellDelta).slice(0,12),
  strongestDeclines:[...courtChanges].sort((a,b)=>a.cityDelta-b.cityDelta||a.cellDelta-b.cellDelta).slice(0,12),
  notes:['Actual campaign counters are not raw war declarations. Victory counters are recorded military outcomes, not an inferred complete field-battle census.',
    'Counter differences subtract the first-frame baseline for campaigns already in progress. Campaigns completed between snapshots remain included. Captured-town pairs deduplicate within each campaign, not across different wars.',
    'Observed controller transitions can revisit the same cell across frames, so their sum is neither unique land area nor net territorial growth. Not every ownership change is automatically a conquest.',
    'A court absent at the last frame is no longer living in that snapshot, but its exact end date is only bracketed unless an archive event gives it. Sovereign allegiance and live court identity are separate.',
    'This is numerical episode evidence; visual rise/fall acceptance additionally requires inspecting the actual exported images.']};
await fs.writeFile(path.join(folder,'episode-review.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({folder,start:output.start,end:output.end,frames:output.frames,eventCounts:output.eventCounts,campaigns:output.campaigns.intersecting,victoryCounter:output.campaigns.windowCampaignVictoryCounter,newCapturedPairs:output.campaigns.newCapturedSettlementCampaignPairs,controllerTransitions:output.totalObservedControllerTransitions}));

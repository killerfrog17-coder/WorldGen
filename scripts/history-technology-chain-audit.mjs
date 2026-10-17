// Offline saved-JSON inspection only. Never advances or mutates a world.
// Usage: node scripts/history-technology-chain-audit.mjs NEW_REPORT.json FOLDER...
import fs from 'node:fs';
import path from 'node:path';
const [destination,...folders]=process.argv.slice(2);
if(!destination||!folders.length||fs.existsSync(destination))throw Error('Provide a new report file and saved replay folders.');
const names=['Storage','Cultivation','Pottery','Sailing','Irrigation','Copper','Writing','Bronze','Roads','Iron','Coinage','Mills','Steel','Navigation','Scholasticism','Printing','Gunpowder','OceanNavigation','ScientificMethod','CropRotation'];
// Exact selected requirements transcribed from HistoryTechnicalModel.cs for
// explanatory labels only, never a replacement research/deployment mechanism.
const required={12:[[9,3],[11,2]],14:[[6,3],[11,2],[10,2]],15:[[6,4],[11,3],[14,3]],16:[[9,4],[14,3]],17:[[13,4],[3,4],[0,3]],18:[[14,4],[15,2]],19:[[1,4],[11,2]]};
const selected=[6,9,10,11,12,13,14,15,16,17,18,19];
function packageYear(p,keys){const dates=keys.map(k=>p.InstalledYear[k]-1);return dates.every(y=>y>=0)?Math.max(...dates):-1;}
function earlyYear(p){const side=[16,17,19].map(k=>p.InstalledYear[k]-1).filter(y=>y>=0).sort((a,b)=>a-b);const core=packageYear(p,[15,18]);return core>=0&&side.length>=2?Math.max(core,side[1]):-1;}
function row(c){
 const p=c.Practice;
 return {Id:c.Id,Name:c.Name,Era:c.Era,Institution:c.Institution,Surplus:c.Surplus,SupportedCities:c.SupportedCities,SupportedUrban:c.SupportedUrban,LastYear:p.LastYear,ResearchBudget:p.LastResearchBudget,ResearchSpent:p.LastResearchSpent,LearningBudget:p.LastLearningBudget,LearningSpent:p.LastLearningSpent,MedievalInstalledYear:packageYear(p,[12,11,14]),EarlyModernInstalledYear:earlyYear(p),Practices:selected.map(k=>({
  Id:k,Name:names[k],Level:p.Levels[k],LevelYear:p.LevelYear[k],InstalledYear:p.InstalledYear[k]-1,Deployment:p.Deployment[k],Allocation:p.Allocation[k],Progress:p.Progress[k],Learning:p.Learning[k],Offer:p.Offers[k],OfferYear:p.OfferYear[k],Source:p.Sources[k],
  MissingKnowledgePrerequisites:(required[k]??[]).filter(([id,level])=>p.Levels[id]<level).map(([id,level])=>({Name:names[id],Required:level,Actual:p.Levels[id]}))
 }))};
}
const reports=[];
for(const folder of folders){
 const review=JSON.parse(fs.readFileSync(path.join(folder,'review.json')));
 const dirs=fs.readdirSync(folder).filter(n=>/^year-\d+$/.test(n)).sort((a,b)=>Number(a.slice(5))-Number(b.slice(5)));
 const snapshots=[];
 for(const dir of dirs){
  const file=path.join(folder,dir,'opportunities.json');if(!fs.existsSync(file))continue;
  const saved=JSON.parse(fs.readFileSync(file));const cultures=(saved.Technical??[]).filter(c=>c.Practice);
  const counts={};for(const c of cultures)counts[c.Era]=(counts[c.Era]??0)+1;
  // Preserve complete exported technical rows, not just a selected winner:
  // competing lineages and changing supported actor sets remain inspectable.
  snapshots.push({Year:saved.Year,ExportedSupportedCultures:cultures.length,CurrentEraCounts:counts,Cultures:cultures.map(row)});
 }
 const events=review.Rows.map(r=>({Year:r.Year,Era:r.EraName,FirstState:r.FirstState,ActiveTowns:r.ActiveTowns,Population:r.Population}));
 const observed={};for(const s of snapshots)for(const c of s.Cultures)for(const [kind,y] of [['Medieval',c.MedievalInstalledYear],['EarlyModern',c.EarlyModernInstalledYear]])if(y>=0&&(!observed[kind]||y<observed[kind].Year))observed[kind]={Year:y,Culture:c.Id,Name:c.Name,ObservedAt:s.Year};
 const latest=snapshots.at(-1);const firstState=events.find(e=>e.FirstState>=0)?.FirstState;
 const skillDates=[];
 for(const [k,levels] of [[6,[3,4]],[9,[3,4]],[12,[1]],[14,[1,2,3,4]],[15,[1,2]],[16,[1]],[18,[1]]])for(const level of levels){
  let earliest;for(const snap of snapshots)for(const c of snap.Cultures){const p=c.Practices.find(p=>p.Id===k);if(p.Level!==level)continue;if(!earliest||p.LevelYear<earliest.LevelYear)earliest={Name:names[k],Level:level,LevelYear:p.LevelYear,Culture:c.Id,CultureName:c.Name,ObservedAt:snap.Year,InstalledYear:p.InstalledYear,Offer:p.Offer,OfferYear:p.OfferYear,Source:p.Source,Allocation:p.Allocation,Progress:p.Progress};}if(earliest)skillDates.push(earliest);
 }
 console.log(path.basename(folder),JSON.stringify({LatestYear:latest?.Year,FirstState:firstState,Observed:observed,Eras:latest?.CurrentEraCounts,FirstObservedExactSkillDates:skillDates}));
 reports.push({Folder:path.resolve(folder),GlobalSnapshotRows:events,ObservedInstalledPackages:observed,Snapshots:snapshots});
}
fs.writeFileSync(destination,JSON.stringify({Notes:'Only already written opportunities.json and review.json were inspected; no world was loaded or advanced. Technical rows include cultures with currently supported cities at export time, so this is not a complete historical-culture census. LevelYear is the last level-acquisition date, not all earlier level dates. OfferYear records delivered technical offers; a remembered old offer is not proof of a currently usable route. Allocation is last-review independent research weight in basis points, not extra budget; learning has a separate conserved budget. No demand/war/political/market samples are reconstructed from missing data. Progress excludes already consumed level costs.',Reports:reports},null,2));

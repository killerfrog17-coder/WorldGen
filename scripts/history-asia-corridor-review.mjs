// Read-only summary of the explicit frozen-world corridor evidence.
// Usage: node scripts/history-asia-corridor-review.mjs AUDIT_JSON
import fs from 'node:fs/promises';
import path from 'node:path';
const source=path.resolve(process.argv[2]??'');
if(!process.argv[2])throw Error('Asia corridor audit JSON is required.');
const audit=JSON.parse(await fs.readFile(source,'utf8'));
const best=(context,field)=>Math.max(0,...context.Groups.map(group=>group[field]??0));
const fields=audit.Fields.map(field=>{
  const contexts=field.Contexts.map(context=>({
    kind:context.Kind,route:context.Route,from:context.From,to:context.To,
    donor:context.Donor,donorName:context.DonorName,donorFarming:context.DonorFarming,
    departure:context.Departure,arrival:context.Arrival,
    physicalGate:context.BoundaryAllowed,transmission:context.Transmission,
    sourceResidents:context.SourceResidents,recipientResidents:context.RecipientResidents,
    lastReceipt:context.LastKnowledgeReceipt,lastDirect:context.LastDirectReceipt,
    holderTown:context.HolderTown,holderLive:context.HolderLive,holderSupported:context.HolderSupported,
    currentTravelKm:context.CurrentTravelKm,packageYield:context.CarriedPackageYield,
    bestGlobalArrival:best(context,'GlobalArrivalFit'),bestHeldArrival:best(context,'LocalArrivalFit'),
    bestGlobalPractice:best(context,'GlobalPracticeFit'),bestHeldPractice:best(context,'LocalPracticeFit'),
    crops:context.Groups.filter(group=>group.GlobalProfile||group.LocalHeldProfile).map(group=>({
      group:group.Group,globalCrop:group.GlobalProfile?.CropId??-1,localCrop:group.LocalHeldProfile?.CropId??-1,
      donorAdopted:group.DonorAdoptedYear,acquired:group.LotAcquiredYear,lastPracticed:group.LotLastPracticeYear,
      globalArrival:group.GlobalArrivalFit,localArrival:group.LocalArrivalFit,
      practicePlace:group.RecipientPracticePlace,globalPractice:group.GlobalPracticeFit,localPractice:group.LocalPracticeFit,
      actualDonor:group.ActualDevelopmentDonor,actualChosenFit:group.ActualChosenFit
    }))
  }));
  const routes=contexts.filter(context=>context.route>=0);
  return {name:field.Name,target:field.TargetRegion,neighbors:field.CurrentAdjacentRegions,
    currentAdjacentContexts:routes.length,currentGatePassing:routes.filter(context=>context.physicalGate).length,
    currentGatePassingFarmingContexts:routes.filter(context=>context.physicalGate&&context.donorFarming>=180).length,
    currentGatePassingGlobalArrivalAbove12:routes.filter(context=>context.physicalGate&&context.donorFarming>=180&&context.bestGlobalArrival>.12).length,
    currentGatePassingHeldArrivalAbove12:routes.filter(context=>context.physicalGate&&context.donorFarming>=180&&context.holderLive&&context.bestHeldArrival>.12).length,
    contexts};
});
const output=source.replace(/\.json$/i,'-review.json');
if(output===source)throw Error('Input must have a .json extension.');
await fs.writeFile(output,JSON.stringify({source,year:audit.Year,hash:audit.Hash,fields,notes:[
  'This is current final-state availability, never a reconstruction of preindustrial routes, seed genotypes or recipient populations.',
  'An actual recent knowledge receipt is not proof of specific crop shipment. Acquired/adoption/last-practice dates remain visible.',
  'Physical gate plus viable crop does not establish real funded migration or founding: source households, stocks, costs, destination fields and spacing remain separate.',
  'Nearest-town contexts are explicitly not certified missions. No Core rules, worlds or source audit evidence are edited.'
]},null,2));
for(const field of fields)console.log(JSON.stringify({name:field.name,culture:field.target.Culture,farming:field.target.Farming,
  raw:field.target.RawPopulation,contact:field.target.ContactResidents,teacher:field.target.Teacher,exposure:field.target.Exposure,
  adjacent:field.currentAdjacentContexts,gate:field.currentGatePassing,farmGate:field.currentGatePassingFarmingContexts,
  globalFit:field.currentGatePassingGlobalArrivalAbove12,localFit:field.currentGatePassingHeldArrivalAbove12}));

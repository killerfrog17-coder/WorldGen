// Read-only behavioral probe of an owner-supplied reference checkout.
// The reference source is evaluated in memory and is never copied into WorldGen.
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import {performance} from 'node:perf_hooks';
import {createHash} from 'node:crypto';
import Delaunator from 'delaunator';

const root=path.resolve(process.argv[2]||'');
if(!process.argv[2]||!fs.existsSync(path.join(root,'js/planet-worker.js')))throw new Error('Pass the local reference directory containing js/planet-worker.js.');
const output=path.resolve('artifacts/orogen-reference');fs.mkdirSync(output,{recursive:true});
const manifest=Object.fromEntries(fs.readdirSync(path.join(root,'js')).filter(f=>f.endsWith('.js')).sort().map(f=>[f,createHash('sha256').update(fs.readFileSync(path.join(root,'js',f))).digest('hex')]));
fs.writeFileSync(path.join(output,'source-hashes.json'),JSON.stringify(manifest,null,2));
let result;
const endpoint={postMessage(message){if(message.type==='progress')console.log(message.pct,message.label);if(message.type==='done')result=message;if(message.type==='error')throw new Error(message.message+'\n'+message.stack);}};
const context=vm.createContext({self:endpoint,console,performance,setTimeout,clearTimeout});
const library=new vm.SyntheticModule(['default'],function(){this.setExport('default',Delaunator);},{context});
const modules=new Map();
function read(file){
    file=path.resolve(file);if(!file.startsWith(root+path.sep))throw new Error('Import outside the reference checkout: '+file);
    if(!modules.has(file))modules.set(file,new vm.SourceTextModule(fs.readFileSync(file,'utf8'),{context,identifier:file}));
    return modules.get(file);
}
const main=read(path.join(root,'js/planet-worker.js'));
await main.link((specifier,parent)=>specifier.startsWith('https://cdn.jsdelivr.net/npm/delaunator@5.0.1/')?library:read(path.resolve(path.dirname(parent.identifier),specifier)));
await main.evaluate();
const detail=Number(process.argv[3]||20000);if(!Number.isInteger(detail)||detail<5000||detail>2560000)throw new Error('Invalid reference detail');
const parameters={cmd:'generate',N:detail,P:80,jitter:.75,nMag:.4,numContinents:4,continentSizeVariety:.35,landCoverage:.3,terrainWarp:.75,smoothing:.1,glacialErosion:.5,hydraulicErosion:.5,thermalErosion:.1,ridgeSharpening:.5,temperatureOffset:0,precipitationOffset:0,seed:73421,skipClimate:false};
endpoint.onmessage({data:parameters});
if(!result)throw new Error('Reference did not finish.');
const summary={parameters,cells:result.numRegions,landFraction:Array.from(result.r_elevation).filter(v=>v>=0).length/result.numRegions,rawElevationMin:result.r_elevation.reduce((a,b)=>Math.min(a,b),Infinity),rawElevationMax:result.r_elevation.reduce((a,b)=>Math.max(a,b),-Infinity),climateClasses:new Set(result.debugLayers.koppen).size,metrics:result.terrainMetrics};
fs.writeFileSync(path.join(output,'summary-'+detail+'.json'),JSON.stringify(summary,null,2));
console.log('REFERENCE',JSON.stringify({cells:summary.cells,landFraction:summary.landFraction,climateClasses:summary.climateClasses}));

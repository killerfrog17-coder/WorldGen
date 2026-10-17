// Read-only approximate geographic bins for saved Earth-town observations.
// Not a whole-population census, state-area measurement or homeland prior.
// Usage: node scripts/history-earth-town-geography.mjs EXTERNAL_REVIEW_FOLDER...
import fs from 'node:fs/promises';
import path from 'node:path';

function csvRecords(text) {
  const rows=[]; let row=[], field='', quoted=false;
  for(let i=0;i<text.length;i++) {
    const c=text[i];
    if(c==='"') { if(quoted&&text[i+1]==='"'){field+='"';i++;}else quoted=!quoted; }
    else if(!quoted&&(c===','||c==='\n')) {row.push(field.replace(/\r$/,''));field='';if(c==='\n'){rows.push(row);row=[];}}
    else field+=c;
  }
  if(quoted)throw Error('Unclosed CSV quote.');
  if(field||row.length){row.push(field.replace(/\r$/,''));rows.push(row);}
  const headings=rows.shift()??[];
  return rows.filter(row=>row.some(Boolean)).map(row=>Object.fromEntries(headings.map((name,i)=>[name,row[i]??''])));
}
function region(lat,lon) {
  if(lat < -55)return 'South55S';
  if(lon >= -170 && lon <= -30)return 'Americas';
  if(lon >= -20 && lon <= 52 && lat < 37)return 'Africa';
  if(lon >= -15 && lon < 45 && lat >= 37)return 'Europe';
  if(lon >= 25 && lon < 90 && lat >= 10)return 'WestCentralSouthAsia';
  if(lon >= 110 && lon <= 155 && lat < -10)return 'Australia';
  if(lon >= 90 && lon <= 150 && lat >= -15 && lat <= 55)return 'EastSoutheastAsia';
  return 'Other';
}
if(process.argv.includes('--self-test')) {
  const rows=csvRecords('name,value\r\n"a,b",2\r\n"c""d",3\r\n');
  if(rows.length!==2||rows[0].name!=='a,b'||rows[1].name!=='c"d')throw Error('CSV test failed.');
  if(region(-66,100)!=='South55S'||region(45,10)!=='Europe'||region(-30,130)!=='Australia')throw Error('Region test failed.');
  console.log('CSV and rough-region checks passed.');
} else for(const input of process.argv.slice(2).filter(arg=>arg!=='--include-prestate')) {
  const folder=path.resolve(input);
  const review=JSON.parse(await fs.readFile(path.join(folder,'review.json'),'utf8'));
  if(review.Recipe.TerrainSource!=='Earth')throw Error('Earth-only bins must not be applied to generated geography.');
  const observations=[];
  const includePrestate=process.argv.includes('--include-prestate');
  for(const row of review.Rows.filter(row=>includePrestate||row.FirstState>=0)) {
    const towns=JSON.parse(await fs.readFile(path.join(folder,`year-${row.Year}`,'towns.json'),'utf8'));
    const townIds=new Map(towns.map(town=>[town.Id,town]));
    const sites=csvRecords(await fs.readFile(path.join(folder,`year-${row.Year}`,`sites-${row.Year}.csv`),'utf8'))
      .filter(site=>site.kind==='town'&&townIds.has(Number(site.id)));
    if(sites.length!==towns.length)throw Error(`Incomplete coordinate join at ${row.Year}.`);
    const groups=new Map();
    for(const site of sites) {
      const lat=Number(site.latitude),lon=Number(site.longitude),town=townIds.get(Number(site.id));
      if(!Number.isFinite(lat)||!Number.isFinite(lon))throw Error('Invalid coordinates.');
      const name=region(lat,lon),group=groups.get(name)??{region:name,towns:0,townResidents:0,politicallyAffiliated:0,visibleTownMarkers:0};
      group.towns++;group.townResidents+=town.Population;group.politicallyAffiliated+=Number(town.PolityId>=0);group.visibleTownMarkers+=Number(town.Population>=800);groups.set(name,group);
    }
    observations.push({year:row.Year,era:row.EraName,firstState:row.FirstState,worldPopulation:row.Population,states:row.States,activeTowns:towns.length,groups:[...groups.values()].sort((a,b)=>a.region.localeCompare(b.region))});
  }
  const result={source:path.join(folder,'review.json'),notes:[
    'These are active-town residents and political affiliation, not all people, believers, sovereign territory or regional carrying capacity.',
    'Coordinate bins are deliberately rough and ordered; source CSV coordinates are rounded. They must not be applied to generated-world geography.',
    'Bins: South55S lat<-55; Americas -170..-30E; Africa -20..52E lat<37; Europe -15..<45E lat>=37; WestCentralSouthAsia25..<90E lat>=10 after preceding bins; Australia110..155E lat<-10; EastSoutheastAsia90..150E lat-15..55; remainderOther.',
    'An alternative first-origin location is not itself an error. Compare opportunity, regional networks and physical diffusion, not exact historical countries.'
  ],observations};
  await fs.writeFile(path.join(folder,includePrestate?'earth-town-geography-all.json':'earth-town-geography.json'),JSON.stringify(result,null,2));
  console.log(`${path.basename(folder)}: ${observations.length} Earth town-coordinate checkpoints`);
}

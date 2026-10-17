// Read-only review of active settlement snapshots, not a simulation census.
// Usage: node scripts/history-town-continuity.mjs EXTERNAL_REVIEW_FOLDER...
// Writes only town-continuity.json beside each external review.json.
import fs from 'node:fs/promises';
import path from 'node:path';

for (const input of process.argv.slice(2)) {
  const folder = path.resolve(input);
  const report = JSON.parse(await fs.readFile(path.join(folder, 'review.json'), 'utf8'));
  const snapshots = [];
  for (const row of report.Rows.filter(row => row.FirstState >= 0)) {
    const towns = JSON.parse(await fs.readFile(path.join(folder, `year-${row.Year}`, 'towns.json'), 'utf8'));
    if (!Array.isArray(towns) || towns.some(town => !Number.isInteger(town.Id) || town.Population <= 0)) {
      throw Error(`Unexpected active town data in ${folder}, year ${row.Year}`);
    }
    if (new Set(towns.map(town => town.Id)).size !== towns.length) throw Error('Duplicate town IDs.');
    snapshots.push({row, towns});
  }
  const transitions = snapshots.slice(1).map((current, index) => {
    const previous = snapshots[index];
    const currentIds = new Set(current.towns.map(town => town.Id));
    const previousIds = new Set(previous.towns.map(town => town.Id));
    const currentCells = new Set(current.towns.map(town => town.Cell));
    const disappeared = previous.towns.filter(town => !currentIds.has(town.Id));
    const previousPopulation = previous.towns.reduce((sum, town) => sum + town.Population, 0);
    const disappearedPopulation = disappeared.reduce((sum, town) => sum + town.Population, 0);
    return {
      from: previous.row.Year, to: current.row.Year, years: current.row.Year - previous.row.Year,
      fromEra: previous.row.EraName, toEra: current.row.EraName,
      before: previous.towns.length, after: current.towns.length,
      survivingIds: previous.towns.length - disappeared.length,
      disappearedIds: disappeared.map(town => town.Id).sort((a, b) => a - b),
      appearedIds: current.towns.filter(town => !previousIds.has(town.Id)).map(town => town.Id).sort((a, b) => a - b),
      disappearedPreviousPopulation: disappearedPopulation,
      shareOfPreviousTownPopulation: previousPopulation ? disappearedPopulation / previousPopulation : 0,
      disappearedCellsOccupiedAgain: disappeared.filter(town => currentCells.has(town.Cell)).length
    };
  });
  const first = snapshots[0];
  const firstStateTownCohort = (first?.towns ?? []).filter(town => town.PolityId >= 0).map(town => ({
    id: town.Id, name: town.Name, firstObserved: first.row.Year, firstObservedPolity: town.PolityId,
    observedYears: snapshots.filter(snapshot => snapshot.towns.some(value => value.Id === town.Id)).map(snapshot => snapshot.row.Year)
  }));
  const southernReach = snapshots.map(snapshot => ({
    year: snapshot.row.Year, era: snapshot.row.EraName,
    townsSouthOf55S: snapshot.towns.filter(town => town.Latitude < -55).map(town => ({
      id: town.Id, name: town.Name, latitude: town.Latitude, population: town.Population,
      age: town.Age, polity: town.PolityId, climate: town.Climate
    }))
  }));
  const result = {
    source: path.join(folder, 'review.json'), firstStateTownCohort, transitions, southernReach,
    notes: [
      'Only saved active-town observations are compared; no event cause or whole-world population census is inferred.',
      'A missing ID means absent from the later active-town export, not necessarily wartime destruction.',
      'A new ID in the same cell is reported as re-occupation, not proven continuous settlement.',
      'First-state cohort lists all politically affiliated towns at the first saved state checkpoint, not identified capitals.',
      'Snapshot intervals differ; compare per-interval survival only with their elapsed years visible.',
      'Latitude below -55 is a geographic diagnostic, not automatically Antarctic land on generated worlds.'
    ]
  };
  await fs.writeFile(path.join(folder, 'town-continuity.json'), JSON.stringify(result, null, 2));
  console.log(`${path.basename(folder)}: ${snapshots.length} snapshots, ${transitions.length} transitions, ${firstStateTownCohort.length} first-state cohort towns`);
}

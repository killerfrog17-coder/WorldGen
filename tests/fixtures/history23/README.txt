History-23 review inputs, deliberately retained source fixtures.

These are complete recipes, including the accepted trained climate weights.
They were exported from the actual review worlds only after independently
regenerating them and matching every named generation-stage hash. They are not
new golden expectations and do not replace the older v3 golden corpus.
Snapshots, maps and binaries remain ignored/rebuildable artifacts.

Example reconstruction (PowerShell, from repository root):

dotnet run --project src/WorldGen.Cli -c Release -- --recipe tests/fixtures/history23/continents.recipe.json --save-world artifacts/review-input.world --summary
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --living-review artifacts/review-input.world artifacts/review-history 200000 --fresh --save --after-first-state 20000
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --era-review artifacts/review-history/final.world artifacts/review-eras
node scripts/history-era-review-images.mjs artifacts/review-eras

Use the corresponding earth/archipelago recipe for the other two controls.
Run with the historical source checkpoint under review: later algorithm changes
must not silently regenerate expectations. See docs/HISTORY23_REVIEW.md for
reviewed endpoints and limitations, and docs/VALIDATION.md for native evidence.

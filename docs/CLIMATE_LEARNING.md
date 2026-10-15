# Bounded Earth climate calibration

## Approved default update

The owner has now approved aggressive run `wild-20261002-174123-921`, selected iteration277, for all new recipes/presets. The ten learned values are embedded as version3.26 defaults, with no additional training. Earlier statements below that defaults remain untrained describe the original experimental workflow. Future training still never promotes a result automatically. Existing saved worlds remain unchanged. To update one, choose **Use current trained climate defaults**, then **Regenerate/Reapply**. **Reset climate to untrained baseline** still selects3.24 explicitly.

## Aggressive experiment (3.26)

Use the separate root **Learn Climate - Aggressive.lnk** for the owner-authorized wide search. Start with300 proposals (roughly20–30minutes, machine/load dependent; live estimates supersede this). Existing conservative runs and defaults are untouched. Aggressive checkpoints can be resumed only in aggressive mode.

Most moisture process multipliers now span0.001–100; thermal bias spans−20 to+20C, seasonal amplitude0.01–5, continental cooling0.001–10. Marine moderation stays within0.001–1.428571 because its leading0.7 coefficient must remain a valid mixing fraction. Positive finite values and the existing numerical moisture residual guard remain mandatory; this is broad exploration, not permission for invalid arithmetic.

Proposals alternate small/medium/large multiplicative steps, multi-parameter mutations, and periodic log-uniform global proposals. There is no displacement penalty, separate temperature/rain regression veto, or plateau early stopping. Acceptance and selection both minimize the mean training+validation monthly-value loss; **validation is therefore fitted data in this mode**, not independent validation. Test regions remain unused until final reporting, and a failed test does not discard the selected experiment. Classification thresholds and simulation equations are unchanged; better monthly scores do not guarantee visibly better climate classes. No training was run by the agent.

Explicit profiles use algorithm3.26;3.25 profiles keep their original bounds and behavior. Reset still returns to ordinary3.24. This section overrides the conservative-mode safeguards below only when `--aggressive` / `-Aggressive` is selected. The command is `powershell -NoExit -ExecutionPolicy Bypass -File scripts/learn-climate.ps1 -Aggressive`.

Double-click **Learn Climate.lnk** in the repository root. Enter a proposal budget (start with **100**). You can start fresh or resume a listed run; the number entered on resume means additional proposals. Ctrl+C preserves the last completed checkpoint. The console stays open on completion or failure.

No training has been run by the implementation agent. Observation preparation, synthetic tests and ordinary application builds are separate from fitting. The installed observation cache is ready to use.

## What it learns

This is derivative-free, regularized parameter learning: deterministic bounded coordinate/evolutionary proposals, accepted against observed monthly temperature and precipitation. It is not a neural climate replacement. Ten global parameters adjust thermal bias, land seasonality, continental cooling, maritime moderation, ocean evaporation, storm exchange, frontal rainout, orographic rainout, convection and humidity-limited land evaporation. Their bounds are defined in `ClimateCalibration.cs`. No trained geographic masks, regional coefficients, climate-class thresholds or target color proportions are permitted.

The ordinary defaults remain **worldgen-3.24.0**. Explicit calibrated recipes use **worldgen-3.25.0** and carry their full coefficient vector. Even a neutral experimental vector gets its own version/hash. Null weights preserve the previous recipe hash, numerical climate output and old snapshot compatibility. Generation still calls `WorldPipeline.Generate` for every candidate, including terrain, all twelve months and hydrology. No reduced alternate climate implementation or stale terrain cache is used. Global temperature/precipitation controls remain separate and can be changed after fitting; they are held at zero during fitting.

## Reference data

[WorldClim 2.1](https://worldclim.org/data/worldclim21.html) supplies 1970–2000 monthly mean temperatures (degrees Celsius), precipitation totals (mm/month), and reference elevation. Attribution: Fick, S.E. and Hijmans, R.J. (2017), *WorldClim 2: new 1km spatial resolution climate surfaces for global land areas*, doi:10.1002/joc.5086. These are interpolated observation-based climatologies, not perfect ground truth, especially over poorly sampled and mountainous regions.

The preparation script downloads the 10-arcminute rasters from the official UC Davis server; no data are uploaded. It validates raster orientation, units/ranges and ZIP integrity, and records source URLs and SHA-256 checksums in `source.json`. Downloads and derived observations stay under ignored `artifacts/climate-learning/data`, not in distributable game assets. Review the data provider's terms before redistributing its files.

Temperature, precipitation and elevation are area-weighted into 2-degree blocks using a common valid-land mask. Blocks with less than 70% valid land are excluded. Model samples are aggregated over the same blocks on a fixed 20,000-cell Earth grid. Blocks with model/reference mean height differences above 600m are excluded, rather than teaching climate coefficients to compensate for different relief. The preparation currently yields 3,313 blocks before model-height/mask matching. Model months March–February are explicitly matched to observation months January–December.

## Overfitting defenses and limits

- Only ten bounded global process coefficients can change. Advection, circulation, physical distances, ocean-only external moisture and rainout remain mandatory. Minimum process multipliers are positive; land recycling is still precipitation- and humidity-limited. Synthetic dry-world tests cover the minimum and maximum profiles.
- Geographic 20-degree tiles are deterministically split approximately 60% training, 20% validation and 20% test. A 2-degree excluded rim separates tiles. All months at a location stay together. This is not a random split of neighboring cells.
- Each large 30-by-60-degree geographic region has equal influence, with actual cell-area weighting within it. Both temperature MAE and log(1+rainfall) MAE matter, preventing high-rainfall regions alone from dominating. Raw rain MAE is also reported. Classification never enters the loss.
- Objective: `0.5 * temperatureMAE / 5 + 0.5 * rainLogMAE / 0.7`. A `0.10 * mean(normalized weight displacement squared)` penalty discourages drifting to extreme coefficients. These scales are design choices, not measured uncertainty estimates.
- Training acceptance uses training loss plus regularization; either temperature or precipitation regressing more than roughly 3% from baseline fails its component guard. Validation independently selects among accepted checkpoints using the same regularization and component guards. Numerical moisture residuals must stay within `max(100ppm, 1.2*baseline + 50ppm)`.
- Stop after the requested budget or 60 proposals without a selected validation improvement. Validation never changes proposal direction or training acceptance, but repeated checkpoint selection can still overfit validation.
- Held-out test results are computed only at the end and do not rank checkpoints. The report flags failure to improve or a material component regression. It does **not** auto-install weights even when the test improves. Repeatedly inspecting the same test set weakens its independence.

These safeguards reduce overfitting; **no Earth-only fit can guarantee transfer to arbitrary generated planets**. The model may have structural errors that weights cannot repair. Bounds are deliberately not loosened when the optimizer stalls. Spatial averages also cannot resolve local rain shadows or small islands. Generated-world review is still required before promoting any candidate to the game default.

## Results, inspection and rollback

Each run has a new directory under `artifacts/climate-learning/run-*` containing:

- `checkpoint.json`: current search position, best validation profile, baseline metrics, completed count and input hash; saved atomically each completed proposal.
- `iterations.csv`: training/validation metrics, parameter penalty, acceptance/selection and water-budget status.
- `report.json`: final training, validation and held-out metrics; bounds and selected values; explicit pass/fail caveat.
- `selected.recipe.json`, `selected.world`, `baseline.world`: full reproducible inputs and snapshots. The worlds are 20k calibration previews; increase Detail and regenerate for a higher-detail view, which may differ with resolved relief.
- copied observation CSV and source manifest for reproducibility. Resume rejects a different observation checksum or algorithm contract.

At completion, two **new** named copies appear in the root `world` library: `Earth baseline ...` and `Earth learned ...` (or `Earth experiment CHECK ...` if the held-out comparison fails). Open the updated app, **Your worlds → Refresh**, then open either copy. Existing Earth and all previous saves are left unchanged. An already running old executable must be restarted using the updated root WorldGen shortcut to read experimental profiles.

To reject the fit, simply open the baseline or your original Earth. To retain a world's recipe but remove calibration, use **Reset climate to untrained baseline**, then Regenerate/Reapply. To try learned weights on generated terrain, open its learned Earth copy, then **New generated world** and adjust normal generation settings. Choosing a preset resets calibration. Browser users can import the generated `.world` file; weights persist in its recipe there too. Nothing reads a mutable global 'active weights' file.

The old standalone executable remains at `artifacts/player-3-24/WorldGen.exe` for complete application rollback. No existing saves or source baseline constants are overwritten by learning.

## Budgets and commands

Start with **100 proposals**; use **25** for a quick smoke run, and only extend toward **200–300** if validation is still improving. More iterations cannot repair a missing physical mechanism. Preliminary planning estimates on this PC: 25 ≈ 3–6 minutes, 100 ≈ 10–20 minutes, 300 ≈ 30–60 minutes, excluding first-use downloads and allowing for CPU contention. These are estimates based on earlier generation timings, not a measured training benchmark. The first actual baseline solve prints a machine-specific estimate; subsequent progress updates refine it. Early stopping can shorten the run.

```powershell
# Interactive launcher
powershell -NoExit -ExecutionPolicy Bypass -File scripts/learn-climate.ps1
# Non-interactive budget (new run)
powershell -ExecutionPolicy Bypass -File scripts/learn-climate.ps1 -Iterations 100
# Prepare observations only; never fits
powershell -ExecutionPolicy Bypass -File scripts/learn-climate.ps1 -PrepareOnly
# Contract, synthetic climate and scoring tests; never fits
dotnet run --project src/WorldGen.ClimateTraining -c Release -- --self-test --check-data artifacts/climate-learning/data/observations.csv
```

The launcher uses .NET 10 and an isolated Python environment under artifacts for pinned NumPy/rasterio preparation. After preparation the trainer itself is C# and offline. Failed downloads/builds abort before fitting, with an error left visible. Runs are sequential to avoid contention and preserve simple ordering. Runtime estimates do not affect candidate choice or the reproducible optimization trajectory.

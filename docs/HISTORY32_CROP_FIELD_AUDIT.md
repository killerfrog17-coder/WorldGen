# French crop-field factors: exact saved-world audit

17 October2026. Read-only diagnosis, not a new agricultural rule. The three
sites below come from G Earth year176100, hash
`eacf2838970bf41cf3ff49bb2d7edb1ec7911222c0df74a9e59d5f0afb1af4d6`.
The output `crop-field-g-france-v1.json` is under
`D:/WorldGenBuildArtifacts/overnight-20261017/` and records the exact Core
SHA256, thirteen passing causal/integrity checks and unchanged source hash.

Reproduce with `--crop-field-audit WORLD NEW_JSON`. The diagnostic examines
three known cells and the actual carried wheat profile; it neither scans
historical terrain memberships nor creates farms, people, food or seed.

## First separate the two French cells

| Saved field | Arrival176034 | Departure175424 | Iberian capital163381 |
| --- | ---: | ---: | ---: |
| Location |49.461N1.811E |49.049N2.039E |39.340N3.929W |
| Biome |BorealForest |Peatland |TemperateForest |
| Groundwater depth, cm |414 |69 |1157 |
| Surface flood fraction, permille |5 |39 |3 |
| Flood days/year |117 |106 |117 |
| Fertility, permille |650 |450 |650 |
| Ideal-retuned wheat landscape |.090489 |.149371 |.175912 |

The actual.09 target is not the shallow-groundwater peatland. The latter's
ideal-retuned wheat already passes the separate.12 normalized frontier gate.
Those values are sensitivity substitutions, not actual bred crops or a
historical replay. The target still has no recorded native cereal-group
opportunity; hypothetical rye cannot be silently supplied there.

## Literal factor decomposition

At arrival176034 the seasonal temperatures are8.5,15.7,11.0 and2.7C. The
current cereal growing-window heuristic selects a two-quarter mean13.3C.
The actual wheat has105-day maturity and a10C minimum warmth trait.

| Active component | Value |
| --- | ---: |
| Crop maturation factor |1 |
| Cultivar warmth factor |.7875 |
| Generic cereal-group suitability |.231155 |
| Abiotic potential |.422193 |
| Group suitability / abiotic potential |.547510 |
| Transfer physical threshold |1 |
| Transfer forest-clearing factor |.85 |
| Field-area heat factor |.441667 |
| Field-area slope factor |.946111 |
| Field-area fertility factor |.928571 |
| Field-area forest-clearing factor |.70 |
| Growing-season length factor |.91 |
| Final normalized field quality |.24758825 |
| Environmental latitude factor, current seed |.279202 |
| Ideal-retuned wheat fit |.365483 |

The ideal-retuned result is.365483 times.24758825, quantized to.090489.
Current carried seed instead gives fit.102325 and landscape.025334.

An important negative finding: the field-area heat factor is largely cancelled
by the same heat factor inside the abiotic denominator when the physical
threshold is saturated at1, as it is here. Counting field heat, generic group
temperature and cultivar warmth as three independent complete multipliers
would overstate thermal suppression. There are still two distinct remaining
restrictions: the generic cereal-group niche and actual cultivar physiology.
The generic group includes a fixed warmth/seasonal-harvest response that does
not itself adapt when the cultivar's minimum warmth changes.

Counterfactual controls, with actual thermal/water/soil constraints retained:

- Removing only forest-cover clearing factors raises ideal-retuned wheat
  landscape to.151777. This is not permission to assume cleared fields in an
  empty forest; it identifies where actual land preparation could matter.
- Raising fertility alone from650 to700 raises it only to.097423, still below
  .12. The target is not a profoundly infertile soil in these saved inputs.
- Changing groundwater depth to2000cm and flood extent/duration to zero,
  while retaining actual water supply, leaves active fit and field area
  exactly unchanged. Changing only peat/organic labels also has no effect.

## What the current model does and does not model

`HistoricalCrops.Suitability`, `HistoricalCropCatalog.Fit/TransferFactor` and
`HistoricalLandscapeEffects.CropAreaYield` do not read groundwater depth or
flood duration. There is no peatland-specific crop-yield penalty to remove.
Organic soil enters continuous fertility, while flood fraction can improve
the river-access/alluvial production mix. Ecology classifies organic soil
with water table shallower than80cm as peatland, and maps that biome to
`Open|Wetland`, not forest. Its crop clearing factor is consequently1, not.7.

The two forest factors currently remain tied to immutable habitat, including
already cultivated towns. They do not represent a saved local cleared-field
history. A causal next investigation should separate native ecosystem niche
from cultivated physiology and distinguish potential land conversion from
actual converted plots. Labor, households, provisions and costs must be paid
before any destination-field benefit; world ecology and accepted terrain
should remain unchanged. No blanket forest, soil or thermal waiver follows
from this diagnostic.

## Agronomic interpretation, not a flood-fraction waiver

[FAO AquaCrop v4 chapter3](https://www.fao.org/fileadmin/user_upload/faowater/docs/AquaCropV40Chapter3.pdf),
sections3.7.6 and3.10.4, models crop-specific root-zone aeration and the time
spent above an anaerobiosis threshold. A rising groundwater table can restrict
active rooting depth; physiological damage is not determined solely by the
surface area under floodwater. Thus69cm groundwater may matter biologically
even with only3.9% surface flooding. Its absence from these crop functions is
a model limitation, not evidence that real peatland is automatically arable.

[FAO56 chapter8](https://www.fao.org/4/X0490E/x0490e0e.htm) treats root-zone
water balance and drainage explicitly and distinguishes water supply from
plant-available storage. Neither source supports applying the full106 flood
days to every hectare or simply declaring the remaining96.1% perfectly
drained. A future bounded waterlogging model needs crop-root sensitivity,
depth/duration, local plot choice and funded drainage, not a biome-only ban.

## Revision22 actual-field and cold controls

This later calendar correction is separate from the preceding G-field substrate
diagnosis; it does not retroactively change any old hash or waive peat, forest,
water, growing-day or soil constraints. `HistoryCropMaturityWorldTests` queries
the saved U21 Earth year162200 checkpoint, historical hash
`02961901cacc6c95c771c1b966572e6d502e8309f758f432b733368542882fef`.
Fifteen assertions pass against coherent dormant22 query code:160 actually
saved profiles at their recorded practice fields and the French field exercise
220 changed non-orchard queries, while70 orchard queries remain exactly21=22.
One actual saved wheat profile (culture1, practice field159475, maturity98 days)
queried at French cell176034 changes its window13.3→15.3C and transfer fit
0.172540→0.280658. This is a crop-fit query, not a funded settlement or harvest.

Same-engine21→22→21 cached/literal results remain exact. Wild opportunities,
native masks and generic area yield are unchanged. Every habitat field except
the intentionally selected crop-window warmth remains identical, including
annual cold, growing days, moisture, soil, flooding and forest. Orchard
reproductive/cold behavior is literal; annual-crop heat reproduction still uses
the existing function with its selected window temperature. Actual unwatered
hyper-arid cell123401 remains zero-fit; frozen/insufficient-season cell0 remains
zero-fit. These are not claims that the river-seasonal French field is unwatered.
A controlled25-degree seed photoperiod mismatch at that actual field retains
its penalty. Original history and all saved crop traits remain untouched.

The literal U21 revision21 plus12-year control is exactly
`a21438f9f37f94867196c747c20e7dfd4b17b1d506bf9c186e98ac433d4ef855`
in both `candidate-v21-freeze` and dormant22 source, with external files
`crop22-{current,frozen21}-u21-earth12.hash`.

After activation, seven further assertions use coherent
`candidate-w22-freeze` DLLs, not a manually forced future rule. Public explicit
upgrade conserves all inhabitants, town food, treasuries and crop traits; initial
full-world save/load preserves its hash. Actual22 twelve-year continuation is
exactly five years, an intermediate full-world save/load, then seven years:
`995ac7cba642358ca1d6ed4798f46c22826ba2a07756a165b756c7ae0258c8c9`.
Fresh `Prepare`/`Start` also selects22. External driver artifacts are
`crop22-world-driver-bin` (dormant query proof) and
`crop22-cold-world-driver-bin` (genuine activated cold proof), under the
overnight evidence root. Full22 geographic outcomes remain a separate review.

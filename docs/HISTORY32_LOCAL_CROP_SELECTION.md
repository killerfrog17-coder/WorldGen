# Local crop selection: diagnosis and revision10

17 October 2026 source experiment. Generation, accepted habitats and terrain
remain unchanged. This is not a declaration that western diffusion is solved.

## Saved-world diagnosis, including negative findings

The read-only `--agronomy-lineage-audit WORLD FRONTIER_JSON NEW_JSON` compares
ten actual G Earth sites and seven causal/integrity checks. Its exact source is
`candidate-g-earth200/final.world`, year176100, realm7, hash
`eacf2838970bf41cf3ff49bb2d7edb1ec7911222c0df74a9e59d5f0afb1af4d6`.
External reports `agronomy-lineage-g-earth-final-v3.json` and
`frontier-g-earth-final.json` are under
`D:/WorldGenBuildArtifacts/overnight-20261017/`.

The critic's European case is route12479, departure175424→arrival176034,
49.46145N1.81116E. The source is culture51 Lifufi and state3408 Fuffiwusi,
with1,840,807 remote residents. A court's culture origin is not its capital:
Lifufi's origin/practice field is25.216N115.903E, while the actual capital is
cell163381 at39.3397N3.9293W. Border contact is physically permitted.

| Actual or diagnostic seed scenario | France target yield | Departing French border yield | Occupied Iberian capital yield |
| --- | ---: | ---: | ---: |
| Current inherited crop package | .025334 | .043102 | .101492 |
| Current wheat, hypothetically retuned daylength only | .090489 | .149371 | .175912 |
| Current wheat, selection index20000 only | .043110 | .072450 | .127783 |
| Model's best local unselected grain representative | .075816 | .128083 | .158071 |
| Locally recorded native grain opportunity? | No | No | Yes |

These substitutions do not simulate breeding or grant plants to history.
They retain the actual climate, water, soil, forest, cold, salinity and
reproductive constraints. A crop-group native opportunity is not a botanical
inventory proving a particular wild species. The model's local rye is therefore
an unavailable counterfactual at the French target, but an admissible group
opportunity at the occupied Iberian capital. The real .12 landscape-yield gate
still rejects the French target even after idealized latitude retuning and
extended selection. The occupied source, however, has a genuine local-adaptation
limitation capable of crossing that gate.

The target wheat's carried fit is .102325: catalog transfer .366490 multiplied
by environmental latitude factor .279202. Its inherited trait is31.6 degrees,
selected index4000, maturity105 days and minimum warmth10C. Its native mask is
zero for all six crop groups. Nine saved recent cereal donor receipts precede
the recipients' current year and postdate donor adoption; none offers a fit
above.12 at this target. Four current donor-anchor routes remain physically
permitted. Those current anchors are not the archived historical endpoints.

The initial donor-first explanation was **falsified**: for unadopted groups,
`DevelopmentCropDonor` already starts with minimum fit .12 and returns-1 when
no suitable, recently delivered donor exists. It then permits the native path
if that native group is known. The problematic branch is an **already adopted**
group: selection maintenance followed by `continue` never compares another
same-group variety. This revision does not fabricate a native replacement.

India/West Asia are not a uniform failed-seed region. At22.545N72.951E a real
frontier has crop fit .530765 but landscape yield .006229, aridity88/1000 and
soil fertility150/1000. At14.425N79.400E, current yield .232870 already passes;
native rice/tuber opportunities exist and the local yam representative raises
yield only modestly to .251089. Southern zero-fit samples have relief2302m
and3873m. The occupied West Asian site26.276N42.002E already has yield .586863.
None justifies removing drought, forest, relief, soil or photoperiod penalties.

## Active-path defect

The aggregate calendar dispatches `AdvanceAggregateCulture`, whose adopted-crop
branch caps `SelectedYears` at4000 and never moves daylength latitude or water
demand. The older dense `AdvanceHistoricalCultures` path calls
`SelectLocalCrops`, which instead has a20000 ceiling, occupied local practice,
bounded maturity/water/warmth selection and gradual latitude movement. Calling
that dense routine directly is unsafe: it scans settlements and assumes the
retired dense culture-cell arrays.

The G wheat's current-state effective-cultivation function returns41 years for
an illustrative100-year interval, while the active selected-index delta is
zero at its cap. This is a deterministic mechanism probe, **not** a replay or
an estimate of historical cultivation during the next century. All six Lifufi
seed records have index4000 despite additional recorded local husbandry.

## Revision10 contract

The new path is enabled only by realm revision10, fresh or explicit upgrade.
Revision9 and earlier retain the literal preceding aggregate branch.
Upgrade itself changes no crop profile or past adoption date.

One existing culture event addresses its six adopted groups. An actual occupied
recorded practice field is retained; if unusable, only that event's existing
sampled town can replace it. Valid own-culture rural residents or a living
own-culture town are required. No culture/state/region membership census, new
queue, global donor broadcast or per-terrain-cell cultivar store is added.
Local-context husbandry is reduced when the actual practice site changes.

Positive existing cultivation weights effective selection by fit and farming
effort. Fit below.08, no cultivation, no occupied site or no elapsed time yields
no adaptation. The index may continue to20000, with finite physiological
limits: maturity at most30% shorter, water demand at most15% lower and minimum
warmth at most4C lower than its crop definition. Seed origin and immediate
donor remain stored. Daylength optimum moves toward that actual field at
.02 tenths of a degree per effective selection year. Named fractional draws
avoid permanent zero-rounding in short five-year aggregate events, without
inventing a minimum gain. A mature index20000 seed can still retune locally.

The shared physiological helper preserves the dense legacy arithmetic exactly.
New aggregate selection additionally preserves stronger inherited traits:
old aggregate index4000 wheat already has105-day maturity/minimum10C/yield1450,
whereas blindly applying the older dense curve at4000 would regress it to
112 days/minimum11.3C/yield975. Fresh revision10 crops follow the existing
bounded dense curve; old selected seed lots are not retroactively unbred.
Recorded aggregate yield1450 is possible because aggregate validation returns
before the dense validator's1300 trait ceiling. This change does not weaken
that validator or reinterpret old records.

There remains one seed profile/practice field per culture. A Chinese practice
field cannot provide an independent breeding history for every Iberian estate;
locally founded descendant communities have their own copied profile and field.
Independent colonial seed niches and within-group crop replacement would need
separate saved scope and evidence. This repair does not promise that a vacant
French frontier becomes inhabited, or lower its actual provisioning gate.

## Historical and agronomic constraints

[Turner et al., Science2005](https://pubmed.ncbi.nlm.nih.gov/16284181/)
identified barley photoperiod variation linked to different sowing practices;
reduced responsiveness benefits spring-sown varieties. Selection changes
reproductive timing, not simply an always-better universal crop level.
[Hung et al., PNAS2012](https://repository.cshl.edu/26967/1/Ware%20PNAS%202012.pdf)
found genetic variation enabling maize's spread into longer-day environments.
This supports gradual inherited local adaptation, not instantaneous northward
transport or abolition of photoperiod costs.

[FAO wheat agronomy](https://www.fao.org/4/i2800e/i2800e.pdf) distinguishes
growth thresholds, thermal development, cultivar-specific vernalization and
photoperiod, with spring/winter life cycles differing substantially. The
[FAO crop-period guidance](https://www.fao.org/4/S2022E/s2022e07.htm) likewise
warns against uniform crop durations. The simulation's fixed two-season
warmth window and normalized .12 livelihood gate remain coarse game priors,
not calibrated agronomic limits. Their independent assessment should retain
water, soil, slope and seasonal maturation, not simply choose the hottest
quarter everywhere or declare all temperate cells viable.

## Verification

The isolated `agronomy-selection-final-bin` build passes nine pure assertions,
including 216 literal legacy arithmetic comparisons, inherited-trait
preservation, finite physiological bounds, post-cap retuning and fractional-rate
and overshoot controls. The actual CP and G Earth fixtures each pass thirteen
assertions, including zero-fit/zero-elapsed negative controls. Both include real occupied
descendant fields, inactive and empty-field negatives, retained crop lineage
and material accounts, full save/load, actual twelve-year cold partition
equivalence, and source immutability.

The controlled selection probes hold the actual field and demographic context
fixed for fifty twenty-year practice steps. They are not thousand-year full
simulation replays. CP culture10/group4 at field157352 advances selection
4000 to4682 and daylength latitude306 to315 toward its actual target351. G
Earth culture15/group0 at field156198 advances4000 to4557 and316 to328 toward
342. Full historical map acceptance remains separate.

For exact old-rule control, frozen `candidate-i9-tests4-bin` and current source
both advance the same explicit realm9 CP medieval snapshot twelve years to:

`18954b5c851adf378ee9f0d4e9a703d5d346ebb84fff84e333a2f1373aa6d079`.

The source hash is
`51f7ed592e5796330e0b7355893b870d7d5f3d858aaa615349af0682fad1f827`.
The external evidence files are `local-crop-selection-old9-cp-medieval.world`,
`local-crop-selection-frozen-old9-cp12.hash` and
`local-crop-selection-current-old9-cp12.hash` under
`D:/WorldGenBuildArtifacts/overnight-20261017/`. No golden expectations were
regenerated. Revision10 does not silently alter revision9 saves.

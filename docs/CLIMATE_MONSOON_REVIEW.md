# Read-only monsoon diagnosis, 16 October 2026

This is a diagnosed limitation of the saved worldgen-3.32 climate, not a climate
algorithm change or a historical-simulation calibration. The reviewed Earth
terrain/ecology is the exact history32 B2 replay input: seed73421, 200k terrain
cells, default learned climate weights, no temperature or precipitation offset.
History replay does not recalculate these abiotic fields.

## Measured saved inputs

The offline `--late-regional-audit <world> <folder>` reads a validated world,
queries a detached historical branch, and verifies that the input state remains
unchanged. Its geographic boxes and point names are diagnostics only; none are
referenced by generation or historical events. Rural values are existing map
downscaling estimates, not additional authoritative population accounts.

Nearest terrain-cell values, with rainfall summed over the four quarters:

| Place | Cell, height | Saved climate | Annual rain | Summer wind east/north | Summer humidity |
| --- | --- | --- | ---: | --- | ---: |
| Delhi |148053,216m|BWh|102mm|−.214/−.417|6permille|
| Kolkata |138547,7m|BWh|159mm|+.148/−.342|39permille|
| Mumbai |132727,5m|BWh|367mm|+.222/−.388|128permille|
| Chennai |123022,20m|Am|1424mm|−.398/−.260|241permille|
| North China plain |156376,89m|Cfa|971mm|−.825/+.692|203permille|

Winds above use the saved model units, not measured station m/s. Positive north
means transport northward. Delhi's seasonal rainfall is5/3/36/58mm; Kolkata's
is13/17/51/78mm. Their modeled summer low pressure is visible (−376/−367Pa)
but accompanies southward, exceptionally dry near-surface flow.

For a direct primary sanity comparison, IMD's1981–2010 station tables give
[New Delhi Safdarjung762.3mm/year](https://city.imd.gov.in/citywx/extreme/SEP/safdarjung2.htm)
and [Kolkata Alipore1813.3mm/year](https://city.imd.gov.in/citywx/extreme/JUN/kolkata2.htm).
These are observational targets, not precise requirements for each nearby coarse
terrain cell. Nonetheless, seven- to eleven-fold rainfall deficits cannot be
explained by the ecological label or historical rules.

Within the diagnostic India box8–33N/68–92E,1067of1640land cells are Desert and
240are Ice; only126are Savanna and100are tropical forests. This box also
includes Himalayan territory, so its aggregate fraction is not an estimate of
India's national biome coverage. The named lowland points distinguish the real
problem from that boundary effect. The habitat proxy actually softens Mumbai's
BWh to Savanna; Delhi and Kolkata are already arid in raw precipitation and
Koppen classification. Do not compensate by granting those deserts extra crops,
people or states in the historical simulator.

## Source-level cause

`Climate.Season` calculates a continental seasonal thermal pressure term, but
the local `pressure` array only contributes to displayed pressure diagnostics.
It does not directly force low-level pressure-gradient inflow.
`AtmosphericCirculation.BasinWind` does affect transport, but converts its
land/ocean thermal contrast into a geostrophic streamfunction. Its exact dual
edge fluxes telescope to zero divergence around a cell. That is valuable for
nondivergent basin gyres, but cannot supply the missing ageostrophic convergence
into a heated continental low. The zonal Hadley/ITCZ and subsidence patterns
migrate only±.14radians (about8degrees), without the larger land-specific
poleward extension of the convergence belt.

This is a causal model limitation, not a proof that this one mechanism accounts
for every rainfall error. The relevant physical contrast is described by the
[Met Office monsoon overview](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/wind/monsoons),
its [South Asia seasonal-forecast review](https://www.metoffice.gov.uk/binaries/content/assets/metofficegovuk/pdf/business/international/scipsa_review_seasonal_forecasting_south_asia_final.pdf),
and [NOAA's explanation of frictional surface wind](https://www.noaa.gov/jetstream/synoptic/origin-of-wind).

The default learned `FrontalRainout` multiplier is79.873156, alongside
`StormExchange`3.990525 and `OceanEvaporation`.700332. Such a large sink deserves
joint review before another training run. Its storm-belt kernel is weak over
India in summer, so it is not by itself an adequate explanation of this case.
Re-optimizing these weights alone could mask the missing physical response.

## Bounded experiment proposal, not implemented

Keep the existing basin streamfunction and test a separate near-surface
down-thermal-pressure velocity. A simple Rayleigh-drag closure is
`u_ageostrophic = -r*grad(p_thermal)/(rho*(r*r+f*f))`, where the gradient is inPa/m,
`rho` is air density inkg/m3, `f=2*Omega*sin(latitude)` in1/s and `r` is a drag
rate in1/s. An inverse1–3day drag timescale is an experiment window, not a
validated coefficient. The near-equatorial limit requires explicit numerical
regularization and a global speed bound. Do not double-add the geostrophic
rotation or force the existing full zonal Hadley pressure field a second time.

Compute the gradient on the existing physically smoothed thermal field. Put the
new component in the transported wind, not in the exact nondivergent basin
component. `AtmosphericMoisture` already uses paired conservative face fluxes,
finite ocean evaporation, condensation and column convergence; it can therefore
receive actual low-level convergence without an invented precipitation source.

Required controls before any adoption: mirrored generic NH/SH seasonal coast
reversals; local/global moisture balance and cancellation; oceanless zero-source
worlds unless explicit scenario moisture is supplied; dry landlocked/Pangea
interiors; independent Sahara/Australia/Congo/Eurasia checks, not just India;
and coarse/fine reconstruction/hash consistency. No geographical whitelist or
Koppen-class override is appropriate.

Output-changing climate work needs an explicit generation version/profile,
complete-recipe hash participation, persistent old3.32 behavior and reviewed
reconstruction tests. It is separate from history32. Existing saved climate,
hydrology, ecology and world integrity must not be silently rewritten.

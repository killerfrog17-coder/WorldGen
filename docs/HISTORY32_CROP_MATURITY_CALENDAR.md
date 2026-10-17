# Planted-crop maturation windows: candidate22

This is a history rule, not a climate/heightmap change. Existing21 and earlier
saves retain their literal calendar unless explicitly upgraded. No native crop
mask, transport route, adoption date, field area or treasury is fabricated.

The previous planted-crop fit always selected a six-month thermal average,
even for an actual98-day wheat cultivar or a114-day potato. Candidate22
instead samples at most twelve cyclic planting windows of the recorded
cultivar's maturation length. Each accepted seasonal temperature represents
three equal months; the last month is fractionally weighted with integer
arithmetic. These are coarse seasonal approximations, not invented measured
monthly weather or daily extremes. Year wrap and hemisphere rotation work.

Orchard group4 keeps the preceding calendar: flowering/chilling and perennial
survival are not simply annual field maturation. Existing reproductive rules,
whole-year cold, frost-free days, photoperiod, water, soil, salinity, slope,
forest preparation and actual local seed requirements remain. Reproductive
heat stress intentionally sees the newly selected growing-window temperature.
Wild/native availability and existing field-area arithmetic are unchanged.

The physical-habitat cache is still bounded. Its key includes the exact
duration and uses a64-bit cell/group/duration encoding; the fit cache also
distinguishes21 versus22 rules. No ordinary actor scans a world or region.

## Evidence

-408 pure assertions pass, including cyclic/hemisphere invariance, fractional
  one-day changes, aseasonal invariance and finite observed-temperature bounds.
-Five recorded-field checks pass. The dated Q17 potato profile at accepted
  French cell176034 moves from13.3C six-month warmth to14.7C over114 days:
  fit.121870 to.165617. This is a dated-profile counterfactual, not a newly
  observed21 delivery.
-Fifteen actual-world query checks pass across160 saved profiles, including
  same-engine cached/literal21/22 alternation and70 unchanged orchard queries.
  Actual98-day wheat at that French field moves.172540 to.280658. No query
  changes original crops, people, stocks or the source hash. Actual unwatered,
  frozen, insufficient-season and photoperiod negatives remain.
-Frozen21 and guarded22 source both advance the actual stopped U21 Earth
  snapshot12 years to
  `a21438f9f37f94867196c747c20e7dfd4b17b1d506bf9c186e98ac433d4ef855`.
-Genuine activated22 explicit-upgrade, full initial and intermediate world
  reload, and12 versus cold5+7 pass seven checks, ending at
  `995ac7cba642358ca1d6ed4798f46c22826ba2a07756a165b756c7ae0258c8c9`.

Full Earth/Continents Plus geographical and era review is a separate gate.
A physiologically better local fit does not prove a repaired Eurasian history.

## Research boundary

[FAO's crop-water manual](https://www.fao.org/4/S2022E/s2022e07.htm)
distinguishes crop type and growth-stage durations rather than a universal
six-month crop. Its illustrative durations do not calibrate this simulation.
[FAO's date-palm climatic requirements](https://www.fao.org/4/y4360e/y4360e08.htm)
distinguish flowering, fruit maturation and survival, supporting the deliberate
orchard exclusion. The simulation still lacks measured monthly crop-water
balances, planting calendars and explicit vernalization.

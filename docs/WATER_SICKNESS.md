# Natural-source water and illness

Implemented 30 October 2026. Replaces the generic river/lake per-sip infection
tuning described in the original water-update notes. This changes survival
rules, not water geometry, shaders, swimming or salinity.

## Source conditions

Water contamination is a hidden, deterministic spatial field keyed by world
seed, hydrological reach/water body, planetary position and solar day. Smooth
300 m patches combine persistent source differences with occasional smooth
one-to-three-day pulses. Many patches have zero or negligible exposure; a small
minority are chronically problematic. Reloading, moving the floating terrain
origin and repeatedly pressing E do not choose new source conditions.

Background wildlife exposure uses habitat productivity/wetland context and at
most five dominant upstream cells. Saved groups actually drinking near the site
can raise local pressure. This is an exposure prior: it does not mean that every
animal is infected or that animal presence automatically contaminates water.
Analytical previous-day/week rainfall supplies a runoff proxy; no annual weather
replay or per-frame source simulation. Up to 128 environments are cached per
explorer, refreshed by solar/weather hour. Terrain rendering only queries
salinity/turbidity, never this illness model.

Sediment, clarity, turbulence and river-versus-lake labels do not themselves
create infection. Flow is not treated as disinfection. Groundwater protection
and actual human waste have separate model inputs, but are **not currently
connected**: no protected-spring or sanitation system exists, and ordinary
streams/campfires are not falsely given those properties. Catchment context is
a bounded proxy, not explicit pathogen transport through the entire watershed.

## Exposure and symptoms

Contamination is an abstract infection hazard per litre. A saved susceptibility
threshold accumulates exposure over a 24-solar-hour window. Splitting the same
volume into additional sips does not reroll the threshold or response. An
unestablished exposure clears after that window. Disease state and the random
sequence survive player capture and serialization; additional sips never reset
incubation or extend the same generic illness.

The prototype deliberately chooses 55% unnoticed courses, 30% mild, 12% moderate
and 3% severe **among established infections**, not among drinks. These are
playability choices, not measured Paleolithic epidemiology. Incubation is
12–48 solar hours. Unnoticed courses clear without tint, extra fluid loss or
energy penalties. Symptomatic courses last 1–3 solar days, rising over three
hours and easing over the final six. Older saved illnesses remain readable.

Symptoms cause severity-dependent fluid/energy loss and a modest reduction in
available energy. Mild cases are small setbacks; severe dehydration can kill
through the existing physiology. Water illness itself never directly subtracts
health. Green stomach tint tracks current nausea, including the onset/recovery
envelope; it cannot detect contaminated water or incubation. Food-poisoning tint
still contributes independently.

All physiology follows playable solar days, independent of the 24-day seasonal
year and historical calendar compression. There is no new safety meter,
diagnosis label, water-treatment crafting or universal dirty-water immunity.
Long parasite diseases remain outside this generic acute-illness prototype.

## Evidence and calibration

- [Australian Drinking Water Guidelines: contamination of source waters](https://guidelines.nhmrc.gov.au/australian-drinking-water-guidelines/part-2/chapter-5/5.4-enteric-pathogens/5.4.2-contamination-of-source-waters-with-enteric-pathogens):
  actual faecal contamination pathways, catchment differences and temporal/runoff variability.
- [CDC: norovirus](https://www.cdc.gov/norovirus/about/index.html): an example of
  delayed acute gastrointestinal disease and dehydration; not a claim all
  waterborne infections have this course.
- [CDC: Giardia](https://www.cdc.gov/giardia/about/): illustrates why a short
  generic stomach upset should not be presented as every possible parasite.

Core checks exercise deterministic source differences, temporal/spatial
continuity, exposure volume, save/reload, legacy saves, unnoticed/mild/severe
courses, recovery and differing day lengths. A reproducible calibration sample
of 500 varied low-pressure sites, 2 L/day for 30 days, produced 28 infected
characters and 11 with any symptoms. This is a model check, **not real-world
safety advice or an empirical infection-rate estimate**.

Native candidate verification and selected-package evidence are recorded
separately in `assets/water-sickness-release-verification.json` once available.

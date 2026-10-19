# Random-access weather preview

`ProceduralWeather.Version = weather-preview-3` is a derived, local query
provider. It does not change the generation recipe, accepted climate, terrain,
hydrology, historical rules or saved historical records. The Unity explorer
uses it; the strategic map's climate remains the existing long-term climate.

## Current weather and retrospective water

The primitive provider takes the world seed, unit spherical position and four
seasonal climate inputs. `WorldWeather` places these on a fixed one-degree
spherical lattice. Its lazy climate profiles use compact C2 weights over a
bounded local spatial-index query, rather than replacing a nearest-cell bag
as the observer crosses a border. Local samples interpolate four fixed nodes;
the same weights apply to instantaneous rates, interval water and retained
water estimates. Longitude wraps; polar nodes are shared. Local elevation changes
temperature/phase, not the location or total water of the passing storm. No
full weather atlas is generated or updated; a bounded50000-entry climate cache
does not tick. There is no territorial census or recurring world-cell loop.

Each 32-day root has a climate-conditioned precipitation budget. A spatially
coherent, deterministic eight-level binary cascade conserves this budget while
partitioning it into three-hour packets. Rain and snow-water channels are
conserved separately, with shared splits so a phase change cannot move a storm.
A compact cubic B-spline turns those packets into
continuous rates. `At(date)` and `Between(start,end)` use exactly the same
packets; analytical kernel integrals and complete-subtree prefix sums make
24-hour/week/month totals additive without stepping through those hours.
Monthly/yearly budgets fluctuate around the seasonal climate; individual
calendar years are not constrained to exactly equal the climate normal.

This is an independently implemented empirical generator, not a fluid solver
or observational weather forecast. Conservative rainfall cascades are a
documented disaggregation technique; see [Müller-Thomy et al., 2018](https://hess.copernicus.org/articles/22/5259/2018/).
This implementation uses spherical noise to correlate neighboring locations;
it does not reproduce that paper's station calibration/resampling algorithm.

Dates can be queried in any visitation order, including ancient history.
Individual retrospective windows are capped at 366 days. Work is bounded by
the fixed tree depth and at most thirteen root blocks, not by the world's
age or cell count. Longer climatological studies should explicitly aggregate
bounded windows outside the frame loop.

`SurfaceAt` supplies **estimates**, not visible ground changes: retained snow
water equivalent, recent retained liquid, soil wetness and runoff potential.
Five age bands and a fixed number of thermal samples reuse the same incoming
water. Retention/melt are approximations, with a one-year memory; this is not
an exact snowpack model, glacier solver, river hydrograph or catchment flood
depth. No snow cover, puddles, standing water or terrain changes are drawn.
Rain/snow partition is cloud-budget based with a near-freezing shower
temperature correction; detailed wet-bulb, freezing-rain and vertical sounding
physics are not implemented. Hurricanes/tornado tracks remain future work.

## Clouds and local effects

An explorer-only URP RenderGraph feature ray-marches two curved, physical-height
cloud layers against the existing scene depth. Low/middle cloud bodies have
bounded density/self-shadow samples; an independent higher, elongated thin
layer approximates cirrus. Cover controls range from clear to overcast. The
camera can travel below, inside or above the layers, without attaching a flat
cloud ceiling to its head. Cloud heights are above local planetary sea level,
not an invariant distance above the player. The [WMO cloud definitions](https://cloudatlas.wmo.int/en/clouds-definitions.html)
inform these broad visual families; this is not an implementation of every
cloud genus or mesoscale weather process.

Cloud quality: Off, Low (quarter resolution, 48 samples, width cap 512), Medium
(third resolution, 64 samples, cap 768), High (half resolution, 96 samples,
cap 1024). A single small tileable value/fBm-and-Worley 3D texture is built once. No per-frame cloud
texture generation/readback occurs. Bulk cloud displacement has a directly
integrable flow independent of local visitor climate, preventing ancient-date
texture jumps when crossing a climate interpolation boundary. Local surface
wind additionally reflects the mean stored seasonal circulation.

The renderer samples weather **at each ray-marched cloud position**, not one
cover/height for the viewer's entire sky. A cancellable worker fills a window
of the global lattice; ordinary0.1-scale midlatitude windows contain roughly
2000–3000 nodes, with wider longitude coverage near the poles. Each node supplies
cover, cirrus, condensed-water rate, storm strength and physical layer heights.
Local rain and visible clouds share the global interpolation. Rain smoothly
enforces dense cover; desert cloud opportunities are moisture-limited.
Precipitation fields have regional coherence instead of high-frequency speckling.
Cloud darkness increases with deck coverage, water and storm depth, in both
the sky and the optional map overlay; this is not calibrated radiometry.

The cloud-shape follow-up removes the uniform dense-cover slab. Rounded Worley
billows shape cloud bodies, with noisy curved undersides and variable tops.
Rain-bearing nimbus is an interior density floor in that same volume, driven
by the same wet column as precipitation, rather than a separate storm mesh.
Ray steps concentrate near the observer so long sightlines do not skip the
overhead layer. Local precipitation also requires cloud cover and stops above
the nominal cloud top. These are broad cumulus/nimbus/overcast forms, not a
complete cloud taxonomy or simulated drop trajectories.

The textured-lighting pass separates broad connected cloud masses, nested
billows and fine3D erosion. Erosion is applied after the height envelope: it
roughens the actual volume boundary rather than only varying transparency.
Surface slopes come from three local forward density differences. Low permits
two lighting evaluations per view ray; Medium/High permit four. Each uses
one/two coarse sunward density probes with the current local weather, without
another geographic weather lookup or a long shadow march. Lighting is reused
through short ray sections. Analytic transmission/powder/multiple-scattering
terms are visual approximations, not a full radiative-transfer solver.

The existing32-cubed noise texture and48/64/96 view-ray budgets are unchanged.
Fine erosion uses distance mip selection; upsampling respects the actual
quality's resolution instead of blurring High as though it were Low. No cloud
fluid simulation, extra streaming volume, CPU cloud mesh or per-frame noise
generation is added. General real-time volumetric-cloud background is available
in [Guerrilla's Nubis presentations](https://www.guerrilla-games.com/read/nubis-realtime-volumetric-cloudscapes-in-a-nutshell);
this small independent implementation is not a port of that system.

The field refreshes after five weather minutes or a window move, at most once
per1.5real seconds after an upload. Two buffers crossfade for1.25seconds; overlapping windows refer
to identical global nodes. Tangent-chart reanchoring preserves the field and
refreshes geometry immediately, avoiding a one-frame blank sky. The local visual
forecast follows the displayed field's time during a background refresh;
historical-water queries remain random-access at their requested date. This
bounded visual lag is not a stateful physical weather simulation.

The feature reads the actual forward opaque depth, not just the SSAO
depth-normal prepass (some existing terrain/scatter shaders lack that prepass).
Both shipped pipelines use single-sample depth. A future MSAA pipeline change
must resolve that depth before sampling, rather than binding an MSAA surface
as a normal 2D depth texture. Explicit sun visibility tests also use opaque
depth. Small depth-aware compositing exclusions protect near foreground;
reduced-resolution silhouettes can still have a small soft fringe.

Rain and snow are bounded camera-local particle volumes with wind velocities,
no particle collision jobs or globally spawned emitters. Precipitation stops
above its cloud layer. An18m-wide near-observer box concentrates the capped
particle budget; visibility uses a sublinear rate mapping so light showers are
not just a few isolated drops. This is visual sampling, not a changed water budget.
Rain supports up to6000 Low/12000 High particles, scaling from drizzle to
downpours rather than exhausting its budget in moderate rain. A continuous
recorded SA3 rainfall bed scales with rainfall, with independent volume.
One recorded wind voice changes gain and quiet intervals with wind strength.
All existing non-music effects now use eight curated offline AI recordings;
runtime PCM synthesis and synthetic fallback are removed. No foliage, animal,
surf or other environment-specific ambience is added. See
[recorded weather effects](WEATHER_AUDIO.md) for production/playback contracts.
Thunderstorm flashes have
dated deterministic opportunities and a real-time flash cadence limiter.
These are visual/audio effects, not authoritative history events.

Thunder now uses two screened offline SA3 mono recordings and four
pooled spatial voices. Arrival delay is the visual bolt proxy's distance
divided by an approximate343m/s, with distance rolloff, progressively reduced
high frequencies and restrained pitch variation. Nearby arrivals can overlap;
the pool remains bounded. Pausing stops its audio clock/voices, and teleporting
clears old queued sounds. Rain uses crossfading banks; wind never layers gusts.
Dry conditions stop rain and cancel thunder. These are
AI-generated effects, not verified field recordings or a full acoustic
propagation/terrain-occlusion model. No footsteps or animal calls are added.
See [sound-effects direction](EXPLORATION_MUSIC.md#sound-effects-direction-reviewed19-october2026)
for the offline asset-generation and future event/material pipeline.

Cloud formation permits extended overcast intervals without active
rain, conditioned on monthly precipitation and the stored moisture proxy.
Its synoptic coverage uses a broad planetary field (frequency5 rather than24),
so systems span regional distances instead of aliasing into world-map speckles.
The shader forms a closed deck as cover approaches full. Preview3 intentionally
changes derived weather through spatial interpolation, regional precipitation
scales and shared phase cascades; it does not promise preview1/2 weather
continuation. Accepted worldgen/climate/terrain and historical rules/saves are unchanged.

## Fog and precipitation visibility

Morning radiation fog uses local solar hour, recent two-day water, seasonal
moisture, light surface flow and cloud-limited nocturnal cooling. Lower sheltered
terrain is favored; fog fades after morning rather than being a permanent biome
tint. This is an empirical proxy, not a dew-point/valley-flow solver. See
[NWS radiation fog](https://www.weather.gov/safety/fog-radiation).

Scene-depth-limited shallow height fog attenuates distant objects while allowing
the viewer to climb above it. Rain/snow curtains integrate six distant field
samples below their cloud columns, plus a full-resolution near contribution.
Visibility decreases with precipitation and fog density. The deliberately
stronger game presentation gives4mm/h rain about850m nominal visibility,
25mm/h storms about180m,50mm/h downpours about100m and4mm/h snow-water blizzards
about285m; the fog test mode is around220m.
These are game coefficients, not an observationally fitted visibility formula.
[WMO precipitation hazards](https://community.wmo.int/site/knowledge-hub/programmes-and-initiatives/aviation/aviation-hazards-precipitation)
informs the heavy-precipitation/low-cloud/visibility relationship. A settings
slider independently reduces or disables weather fog and precipitation visibility.

## Testing commands and optional map clouds

In exploration chat use `/weather clear`, `cloudy`, `overcast`, `rain`, `storm`,
`snow`, `blizzard`, `downpour` or `fog`. `/weather get` reports the mode; `/weather auto` restores
the natural provider. `/weather set rain` is also accepted. Help is alphabetical
and Tab completion includes these modes. Overrides are explicitly session-only
visual tests, shared by the sky, particles and cloud map; they never add water
to the recorded past, modify the climate or get saved as a world rule. Forced
rain/snow clouds remain above local ground even on high mountains.

The **M map** has an optional **Clouds checkbox**, off by default, independent
of its selected base layer. Lighter clouds are relatively thin/dry; darker clouds
indicate thicker, wetter decks. It shows meteorological coverage, not exact ray-marched
billows. A cancellable128×64 worker texture samples the same Core weather provider
and date. It refreshes on pan/zoom, a command change or a new weather hour, at
most once every3real seconds for time changes. The base map is not recolored or
rebuilt when toggling clouds. Nothing is queried while the map/checkbox is closed;
there is no recurring scan of the200k-cell world or full-resolution weather atlas.

Video settings expose cloud/particle quality, sun glare, sun rays, transient
eye adaptation, lightning strength (zero disables flashes), wind and thunder
volume. Options persist per machine, outside saved world recipes. Cloud and
particle quality are independent; disabling clouds does not change rainfall.
The astronomical sky no longer masks disks/stars at a flat horizon. Actual
curved terrain occludes them. Live lighting accounts for the rendered horizon
dip; visual disk exaggeration still does not change physical moonlight.

## Validation

See the current weather entry in `VALIDATION.md` for final build measurements
and native capture results. Core tests cover rate/integral agreement, interval
additivity, old/negative dates, cold/warm phases, spatial continuity, wind
displacement and visitation-independent surface estimates. Native tests use
explicit **renderer fixtures** for clear/cumulus/cirrus/overcast/rain/snow and
lightning, then restore the natural provider. Fixtures are not evidence of
frequency calibration. Low-end hardware and scientific climate/weather
calibration must not be inferred from available-host results.

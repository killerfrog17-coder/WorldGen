# Random-access weather preview

`ProceduralWeather.Version = weather-preview-2` is a derived, local query
provider. It does not change the generation recipe, accepted climate, terrain,
hydrology, historical rules or saved historical records. The Unity explorer
uses it; the strategic map's climate remains the existing long-term climate.

## Current weather and retrospective water

The provider takes the world seed, unit spherical position and four seasonal
climate inputs. The explorer reuses the terrain locator and samples only the
nearest fine cell and its immediate neighbors, with a local lapse correction.
There is no global weather array, territorial census or per-cell weather tick.

Each 32-day root has a climate-conditioned precipitation budget. A spatially
coherent, deterministic eight-level binary cascade conserves this budget while
partitioning it into three-hour packets. Rain and snow-water channels are
conserved separately. A compact cubic B-spline turns those packets into
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

Cloud quality: Off, Low (quarter resolution, 24 samples, width cap 512), Medium
(third resolution, 40 samples, cap 768), High (half resolution, 64 samples,
cap 1024). A single small tileable value/fBm-and-Worley 3D texture is built once. No per-frame cloud
texture generation/readback occurs. Bulk cloud displacement has a directly
integrable flow independent of local visitor climate, preventing ancient-date
texture jumps when crossing a climate interpolation boundary. Local surface
wind additionally reflects the mean stored seasonal circulation.

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
Delayed thunder uses a small generated clip. Wind uses continuous stereo
band-limited turbulence and resonant noise, with smooth4–13second gusts and
10–35second quiet gaps in calm conditions. Strong gales shorten the gaps and
introduce a sustained low bed/howl. No eight-second recording, loop fade seam,
animal call or downloaded sound is used. The PCM callback owns its filters and
PRNG and makes no Unity API calls or per-buffer allocations; main-thread controls
are volatile floats. See [Unity PCM streaming](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audioclip/pcmreadercallback).
Thunderstorm flashes have
dated deterministic opportunities and a real-time flash cadence limiter.
These are visual/audio effects, not authoritative history events.

Cloud formation in preview2 permits extended overcast intervals without active
rain, conditioned on monthly precipitation and the stored moisture proxy.
Its synoptic coverage uses a broad planetary field (frequency5 rather than24),
so systems span regional distances instead of aliasing into world-map speckles.
The shader forms a closed deck as cover approaches full, rather than leaving
procedural holes at100% cover. Preview1's entire precipitation cascade, water
integrals, temperature and retention remain unchanged. This is a derived-weather
revision, not a change to worldgen/climate or historical rules.

## Testing commands and optional map clouds

In exploration chat use `/weather clear`, `cloudy`, `overcast`, `rain`, `storm`,
`snow` or `blizzard`. `/weather get` reports the mode; `/weather auto` restores
the natural provider. `/weather set rain` is also accepted. Help is alphabetical
and Tab completion includes these modes. Overrides are explicitly session-only
visual tests, shared by the sky, particles and cloud map; they never add water
to the recorded past, modify the climate or get saved as a world rule. Forced
rain/snow clouds remain above local ground even on high mountains.

The **M map** has an optional **Clouds checkbox**, off by default, independent
of its selected base layer. White represents cloud cover, with a blue tint for
precipitation. It shows meteorological coverage, not exact individual ray-marched
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

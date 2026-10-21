# Celestial atmosphere — 19 October 2026

## Configuration and use

New game → **Sky & atmosphere** configures zero to four moons and a seeded
stellar environment. Arrangements include Earth-like, moonless, rust rock,
large white/red twins, ice, and small captured bodies. Each body has a name,
surface, radius, orbital distance, density, reflectivity, inclination, node,
epoch and RGB reflectance. Derived orbital/phase periods are displayed, not
independently editable timers. Stellar choices are solar neighborhood, dense
galactic field and star cluster. Aerosol haze is adjustable in setup and paused
World settings. Moon/star definitions are fixed after history starts.

Settings are saved in optional `PlaySettings.Sky` version1, separate from the
generation recipe. Fresh games default to one Earth-size/distance moon. Older
play sidecars missing Sky remain moonless; existing historical saves without
`HistoryState.Sky` retain their preceding literal rules and canonical bytes.
Start a new game to configure its sky and give future cultures its calendars.
The explicit Core `ConfigureCelestialHistory` attachment API is tested, but
there is no automatic historical retrofit or player-facing bulk upgrade here.

### Visual exaggeration and display settings

21October2026: new `PlaySettings`/new-game defaults are3× for BOTH sun and moon.
This only changes apparent disk size, not illumination/orbits. Explicit values
in existing saves are preserved; optional-field deserialization and no-sidecar
legacy fallback retain their preceding1× defaults. Runtime without a session
uses the new3× visual fallback. Existing worlds can edit either value in paused
World settings → Sky display; no automatic save rewrite is performed.

Sky & atmosphere → **Sky display** has separate **Moon visual exaggeration**
and **Sun visual exaggeration** multipliers.1× preserves physical apparent size;
5× draws a disk five times as wide without changing emitted/reflected light,
ambient illumination, stellar washout, orbital periods, phases, or cultural
calendars. Use these controls instead of changing physical radius/distance just
to make the sky easier to read on screen. They remain editable after history
starts (Esc → World settings), with no reload. They are optional play-session
fields outside the authoritative sky context; preceding saves default to1×.
Controls accept0.01–20×, with an extreme-disk angular-radius safety cap of60°
for moons/10° for the sun. These are rendering choices, not new celestial bodies.

Both main-menu Settings and Esc → Game settings expose fullscreen, VSync and
maximum FPS. VSync follows display refresh and overrides the independent desktop
frame-rate cap; disable it to use that cap.0 selects Unlimited. FPS edits apply
on Apply/Enter, not every typed digit. VSync/FPS preferences persist per machine
through Unity PlayerPrefs, not per world. Tests change process-local settings
without overwriting the owner's preferences.

F3 remains unboxed/nonblocking. Alongside local/reference solar time, it reports
the northern season and percentage between named solstices/equinoxes, total
celestial night illuminance, and each moon's phase/illuminated fraction/horizon
status. For example: “Northern spring · 47.1% from vernal equinox to summer
solstice.” This diagnostic does not require real-world month names. The existing
date command grammar remains a fixed365-day convenience calendar.

`/time set sunrise` and `sunset` now solve the solar horizon for the player's
latitude and current seasonal date; polar dates without that event report it
instead of inventing06:00/18:00. Other local/reference time behavior is unchanged.

## Geometry, light and rendering

Each moon follows an inclined circular prograde orbit in the ecliptic frame.
Period is `2π sqrt(a³ / G(Mplanet + Mmoon))`; mass follows radius and density.
The planet is Earth-mass/radius, year365days, axial tilt23.44°. Saved calendar
periods use deterministic scalar square root and explicit nanoday quantization.
The Earth-like
two-body circular approximation gives approximately27.28days sidereal and
29.49days mean synodic, not an exact measured lunar ephemeris. Apparent diameter
and parallax use observer-to-body distance. Phase lighting is the actual dot
product of surface normal and moon-to-sun direction, not a phase texture timer.
The mean synodic estimate is shown in setup; actual festival phase dates solve
ecliptic elongation with a bounded32-step search.

Disk illumination and irradiance account for radius, distance, reflectivity,
Lambert phase, color and horizon. Red reflectance has lower photopic luminance
than white at equal size/distance. The strongest visible source supplies the
existing directional light; up to four remaining sources add cheap diffuse
light. Moonlight tints ambient air/terrain and suppresses stars. Artist-tuned
exposure maps estimated illuminance to useful renderer brightness: this is not
a calibrated photometric camera. Stored night-lux values are approximate.

Procedural craters/maria, rust rock/polar caps, ice fractures and irregular dark
surfaces use no downloaded game textures. Sun and far-to-near opaque moon disks
occlude background stars and each other. No N-body dynamics, eccentricity,
precession, eclipse shadow/illumination, tidal simulation or tidal climate
feedback is claimed. Validation rejects planet/Roche intersections and radially
overlapping orbits, but does not prove long-term multi-moon dynamical stability.

Stellar environments generate their own reproducible directions and density,
not one shared static sky photograph. Dense fields have a seeded dusty galactic
band/bulge; clusters have a smooth exponential angular concentration, not a
hard-edged cap. Authored stellar-background illuminances are aesthetic presets,
not predictions of a particular Galactic location. Actual Galactic-center dust
can obscure stars; proximity alone does not guarantee a brilliant optical sky.
The4096×2048 star atlas is generated only on scene/configuration changes.

Land, underwater terrain, water and scatter share analytic aerial perspective:
Beer–Lambert RGB Rayleigh extinction and aerosol extinction integrated through
8km/1.2km exponential density profiles along a straight endpoint-height path.
It obscures long sea-level views more than high-altitude ones and blends the
outermost LOD boundary. This is constant-cost shading, not volumetric ray
marching or a full spherical multiple-scattering solver. Clouds/weather remain
unimplemented in this pass.

## Saved cultural astronomy

Fresh Unity histories attach the configured sky before Start. An independent
optional `HistoricalSkyContext.Version=1` stores the celestial definition and
aligned append-only faith/culture conventions. Named authoritative RNG streams
choose practical solar/lunar/lunisolar reckoning, a possible sacred moon,
specific moon symbolism, star symbolism and a principal local/solar/lunar
observance. Moonless worlds cannot acquire lunar calendars or sacred moons.
Practical lunar timekeeping does not imply worship, and some faiths have no
astronomical symbolism. Lunar calendar months use the configured body's actual
mean phase cycle. Agricultural founders may use lunisolar correction; otherwise
lunar founders use uncorrected lunar reckoning. These are founding/traditional
conventions, not a complete calendar-reform or literacy model.

Descendant cultures and religious branches inherit their parent's conventions
rather than randomly resetting astronomy. Atlas people and religion articles
show these conventions and calculate the next astronomical observance on demand.
Leap correction is an average whole-month insertion interval, not a simulated
legislated calendar. No festival attendance/economic bonus, daily event queue,
new technology, free resources, population change, terrain change, recurring
territorial census or dynamic weather is introduced. Playable date commands
remain visual/calendar offsets and never rewrite historical records.

## References and verification

Primary references: [NASA Moon facts](https://science.nasa.gov/moon/facts/),
[NASA lunar phases](https://science.nasa.gov/moon/moon-phases/),
[NASA Kepler overview](https://science.nasa.gov/solar-system/orbits-and-keplers-laws/),
[NOAA scattering overview](https://gml.noaa.gov/grad/uv.html),
[ESO Galactic-center field and dust](https://www.hq.eso.org/public/images/eso0949f/).
These support physical principles, not scientific acceptance of authored presets
or a universal cultural-astronomy claim. No reference code or art was copied.

Run `--atmosphere-only` and `--atmosphere-world-only` through the Core test
executable for orbital/lighting/haze, actual creation hooks, Atlas, cold saves
and continuation checks. `--worldgen-atmosphere-check --worldgen-output <path>`
on the Unity player creates hidden native camera captures and a result report.
`--atmosphere-native <celestial-traditions.json>` compares the emitted native
calendar/convention profiles to CLR-created profiles with exact canonical hashes.
It never takes input/cursor focus. Camera captures do not prove IMGUI layout.
See VALIDATION.md for the actual accepted build/evidence and remaining limits.

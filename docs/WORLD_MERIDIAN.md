# Official map and longitude-aware solar time — 19 October 2026

The subsequent `CELESTIAL_ATMOSPHERE.md` extension adds configurable lunar
orbits/calendars and seeded stellar environments while retaining this saved
prime and local/reference clock. The limits below concern this earlier meridian
checkpoint and the still-fixed planetary orbit, not the new lunar controls.

A completed world gets one default chart seam; its antipode is the official
prime meridian and the default map center. Panning the map never changes this
reference. Internal mesh coordinates, measured Earth relief, climate, history,
generation recipes and integrity hashes are not rotated or rewritten.

`WorldMeridian` rasterizes conservative equal-area land-cell footprints on a
720-column ×180-row chart. It first minimizes the meridional length intersecting
non-ocean cells (including inland waters). A land-free seam therefore always
beats one crossing a continent. Ties maximize latitude-weighted ocean clearance
using circular distance transforms; unavoidable polar land does not dominate
that tie-break. All-ocean/all-land worlds have a deterministic zero prime.
This is a half-degree/one-degree cell-footprint approximation, not a geometric
guarantee about every subcell islet or exact coastline. It runs once for each
immutable completed world, off-thread during Unity generation/load, with a weak
cache; no per-frame geography scan is added.

The selected raw longitude is saved in optional `PlaySession` fields
`MeridianVersion=1` and `PrimeMeridianLongitude`. New generation gets a fresh
choice. Existing sidecars without the fields remain readable and adopt the
convention once; a saved explorer preserves its preceding local clock during
that adoption. A saved version1 prime is not recomputed during play or load.

`SolarHour` now means mean solar time at that reference. Local solar time is
reference time plus official longitude/15, wrapped to24hours. East is later;
the opposite longitude differs by12hours. Movement/teleportation changes the
local clock, never the shared world phase. The sky and contextual music use the
same local solar time and the same seasonal calendar. F3 reports official
longitude, local solar time and reference time. The simulation map, local map's
Whole world view, browser default view and longitude graticules use the official
center/reference; explicit focus/pan remains available.

`/time` reads local time; `/time set ...` and `/time add ...` adjust the one shared
clock to satisfy that command at the player's current location. Other locations
then see the corresponding time difference. Dates remain the shared playable
seasonal calendar, not independently legislated local calendars/time zones.
The existing23.44° axial tilt and simple seasonal declination are retained.
Clock zones, daylight saving, equation of time, adjustable orbital parameters
and alternate calendars are not implemented. No historical events are replayed
or granted by these clock commands. See `SKY_CHAT.md` and `VALIDATION.md`.

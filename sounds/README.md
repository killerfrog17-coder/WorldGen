# Sound effects

`weather-isolated/*.wav` contains seven local SA3 Medium rain/thunder effects.
Wind playback and its active recording have been removed at the owner's request.
`weather-isolated/screened.tsv` binds runtime admission to their SHA256 hashes.
The rejected original `weather` pack is no longer used. Generating a candidate
does not approve it: see `scripts/generate-weather-sounds.py`,
`scripts/audit-weather-audio.py` and `scripts/publish-weather-sounds.py`.
Release provenance is in ignored `artifacts/weather-isolated/release.json`.

Only wind, rainfall and thunder are intended; no contextual ambience is added.
Diagnostic screening is not human listening certification. Generated media
is not in Git. Missing files stay silent, with a log warning; there is no
procedural synthesis fallback. See `docs/WEATHER_AUDIO.md`.

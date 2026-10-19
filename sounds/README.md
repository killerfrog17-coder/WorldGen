# Sound effects

`weather-isolated/*.wav` contains eight curated local SA3 effects: the exact
owner-approved Small SFX wind gust and seven Medium rain/thunder recordings.
`weather-isolated/screened.tsv` binds runtime admission to their SHA256 hashes.
The rejected original `weather` pack is no longer used. Generating a candidate
does not approve it: see `scripts/generate-weather-sounds.py`,
`scripts/audit-weather-audio.py` and `scripts/publish-weather-sounds.py`.
Release provenance is in ignored `artifacts/weather-isolated/release.json`.

Only wind, rainfall and thunder are intended; no contextual ambience is added.
The owner auditioned the wind; diagnostic screening of other sources is not
human listening certification. Generated media
is not in Git. Missing files stay silent, with a log warning; there is no
procedural synthesis fallback. See `docs/WEATHER_AUDIO.md`.

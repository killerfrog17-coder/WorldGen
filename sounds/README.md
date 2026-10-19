# Sound effects

`weather/*.wav` contains locally generated SA3 Medium effects. Run
`scripts/generate-weather-sounds.py` against the existing CUDA ComfyUI server
to create the library. Source briefs are in `scripts/weather-sounds.csv`;
model/prompt/seed/master/hash records are in ignored `artifacts/weather-sounds`.

These are independent wind, rainfall and thunder recordings, without animal
calls, vegetation, footsteps or location-dependent ambience. Generated media
is not in Git. Missing files stay silent, with a log warning; there is no
procedural synthesis fallback. See `docs/WEATHER_AUDIO.md`.

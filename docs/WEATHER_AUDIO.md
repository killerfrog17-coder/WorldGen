# Recorded weather effects

The19October2026 isolation revision supersedes the rejected17-recording pack.
The owner heard leaves/birds and wet-weather sounds in wind, plus excessive
layering. Offline AudioSet diagnostics corroborated rain/thunder/wildlife in
several old recordings. Prompts and valid PCM did not establish clean content.
Music, weather generation, water budgets, terrain and history are unchanged.
Footsteps, foliage, animals, surf and other contextual ambience remain deferred.

## Curated sources

The active `sounds/weather-isolated` library contains eight distinct recordings:
one wind gust, two light-rain, two moderate-rain, one downpour and two thunder.
The exact wind preview was heard and approved by the owner ("Yes, suitable").
It is SA3 Small SFX, seed533197, prompt `TrackType: SFX. Wind blowing.` and hash
`e1eea27b3784c806f347f56e037b1c5e4a93e5487139c350ae73850ddd2e881f`.
The other seven selected recordings use SA3 Medium. Failed candidates are not
included just to supply variety. Small SFX was selected for this specific gust,
not claimed to be the stronger general music model.

Both models run offline on the existing CUDA ComfyUI installation. Weights stay
on D:. Generation uses8steps/CFG1, FP16 diffusion, FP32 tiled decoding and short
positive source descriptions. At CFG1, putting forbidden sources in a negative
conditioning input is not an effective exclusion guarantee; listing them in
the positive prompt can itself contaminate a scene. Wind is attenuated toward
-32dBFS and never boosted; rain/thunder retain conservative export gain and a
0.70 peak ceiling. Boundary fades do not synthesize a source. WAVs are44100Hz
PCM16; wind/rain stereo, thunder mono. No gameplay model inference occurs.

`scripts/generate-weather-sounds.py` generates candidates, not approved assets.
It supports explicit catalogs, checkpoint, seed base and sampler; its resumable
results record actual prompts/seeds/master paths/hashes. The short wind trial
catalog is `scripts/weather-wind-trials.csv`. `scripts/publish-weather-sounds.py`
curates the selected records and moves owned rejects into ignored review
artifacts, recoverably. Release provenance is in
`artifacts/weather-isolated/release.json`; raw masters remain outside the repo.

`scripts/audit-weather-audio.py` uses overlapping five-second AudioSet windows
to flag animals, foliage, speech/music, machinery, unrelated water and baked-in
rain/thunder. It writes hash-bound `screened.tsv`; runtime admits only files
matching that list. This catches obvious contaminants, not every audible
artifact. Only the wind has explicit owner listening approval. The classifier
is an offline QA tool, not a listening certificate or runtime dependency.

```powershell
E:\AI\ComfyUI\.venv\Scripts\python.exe scripts/audit-weather-audio.py sounds/weather-isolated --output artifacts/weather-sound-review/final-audit.json --screen
E:\AI\StableAudio3\optimized\tflite\.venv\Scripts\python.exe scripts/check-weather-sounds.py
```

Source scripts, selected metadata and the hash admission manifest are backed
up by Git (`scripts/weather-selected.csv` records the exact chosen prompts,
models, seeds and hashes). Floating-point GPU regeneration is not guaranteed
to reproduce an identical media hash; a regenerated file needs review again.
WAVs, raw masters, players, model weights and diagnostic artifacts
are not. A fresh checkout needs the matching recordings; absent/changed files
stay silent with a warning rather than synthesize replacement sounds.

## Playback

One nonlooping wind voice plays the auditioned gust. Strength controls gain
and quiet intervals (roughly20-45seconds in light wind,0.8-3seconds at the
strong end), not overlapping recordings or pitch manipulation. Strong/gale
banks deliberately alias the same clip. There is no continuous white-noise bed
or second wind layer. Existing wind/rain volume controls still apply.

Rain banks alone use two bounded voices with three-second equal-power joins.
Rain requires actual liquid precipitation>=0.1mm/h, cloud cover>=0.85 and an
observer below the cloud top. Dry weather, fog, snow and blizzards immediately
stop rain voices rather than leave a fading wet bed. Thunder additionally
requires active storm potential>0.2; leaving those conditions cancels pending
arrivals and stops old thunder. Four pooled spatial voices retain physical
distance/343m/s delay, attenuation/filtering and nonrepeating variant selection.
Teleport resets sounds; pause freezes playback and arrival clocks.

Loading is asynchronous, fixed-size and independent of world-cell counts.
`--worldgen-sounds-root` selects a folder containing `weather-isolated`.
F3 reports the active wind recording, rain voices and active/queued thunder.
No ordinary text HUD is added. There are no runtime synthesizers or PCM callbacks.

## Verification limits

Media checks validate provenance, screen hashes, finite unclipped PCM, layout,
duration, energy and boundaries. Native checks exercise actual decoding,
playback, pause/resume, transitions, delayed thunder and negative dry-weather
tests. Automated native checks are muted and do not establish subjective
timbre or inaudible joins. See VALIDATION.md for the completed test record.

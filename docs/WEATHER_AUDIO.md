# Recorded weather effects

The19October2026 audio revision replaces all existing non-music sound sources:
wind, rain and thunder. It does not add footsteps, animals, vegetation, surf,
settlement noise or other environment-dependent ambience. Snow itself remains
silent; blizzards use wind. Music is unchanged.

## Offline production

`scripts/weather-sounds.csv` contains17 isolated-source briefs: three gentle
wind recordings, two strong-wind, two gale, two light-rain, two moderate-rain,
two downpour and four thunder variants. Wind prompts explicitly exclude rain,
thunder, sand/debris, foliage, animals, voices, waves and structures. Rain is
generic open-air rain without roofs, gutters, leaf impacts or identifiable
ground materials; thunder is isolated without a baked rain/wind background.
Prompt exclusions guide the model, not an acoustic guarantee. These clips
still need listening/owner feedback for timbre and unwanted audible artifacts.

`generate-weather-sounds.py` uses the existing local SA3 Medium CUDA ComfyUI
installation, not a new model, API or CPU inference fallback. The graph uses
FP16 diffusion, FP32 SAME-L decoding,8steps/CFG1. Decoder tiles64/overlap16 avoid
the severe observed memory pressure with larger tiles while the game is open.
Effects are generated at32seconds, thunder14seconds. Export removes thunder
leading silence, downmixes thunder to mono, applies boundary fades and
conservative RMS/peak gain, and writes44100Hz PCM16 WAV. It adds no synthetic
source sound. Ambient variants are stereo; thunder is mono for spatial playback.

Run with the existing Python environment:

```powershell
E:\AI\StableAudio3\optimized\tflite\.venv\Scripts\python.exe -u scripts/generate-weather-sounds.py
E:\AI\StableAudio3\optimized\tflite\.venv\Scripts\python.exe scripts/check-weather-sounds.py
```

Source briefs/exporter are Git-backed. Generated `sounds/weather/*.wav`, raw
masters under the external ComfyUI output directory, and ignored
`artifacts/weather-sounds` job catalog/results/verification records are not.
The catalog records the model/checkpoint/encoder, full prompts, seeds, CUDA
device, steps/precision and source/license references; each result retains its
raw master path, actual seed, prompt job ID and exported file/PCM hashes.
The installed model weights stay on D:. No model or user recording is deleted.

The producer is resumable, skips verified completed files, rejects changed or
unmanaged destinations and uses a single-job lock. A timeout deliberately
does not submit duplicate GPU work. `PAUSE` in the evidence folder stops
between recordings. See status.json for completion rather than plan count.

## Runtime

`LocalTerrainWeatherAudio.cs` asynchronously decodes the small WAV library
once when entering exploration. It searches the checkout's `sounds` folder,
or `sounds` beside a standalone player. `--worldgen-sounds-root` explicitly
selects a folder containing `weather`. A missing/broken clip is skipped with
a log warning; there is no procedural sound fallback or gameplay inference.

Six intensity banks use two bounded nonlooping voices apiece. Successive clips
have3second equal-power overlaps and avoid immediate identical variants.
Wind/rain bank weights change continuously with actual wind/gust speed and
rainfall; volume controls remain independent. The gentle-wind recordings have
their own irregular swells/quiet gaps. No cell biome chooses vegetation or
wildlife noises. Rain fades out above its cloud column and during dry weather.
Teleporting stops old ambient voices and queued thunder; pause preserves bed
playheads and freezes sound-arrival clocks. These are playback controls and
filters, not waveform synthesis.

Four pooled thunder voices retain bolt-proxy distance/343m/s arrival delay,
distance rolloff and high-frequency loss. Variant selection avoids consecutive
identical thunder even for repeated strike seeds. The former WindSynth,
RainSynth, runtime generated clips and audio PCM callbacks are removed.
Work remains fixed-size and independent of world-cell counts. No generation,
weather-water budget, historical rule, save format or terrain changes occur.

## Review limits

The offline check verifies provenance, distinct finite nonclipping44100Hz
recordings, mono/stereo layout, duration/energy and silent file boundaries.
Native checks exercise actual WAV decoding, PCM access, recorded source
playback, bank overlap, pause/resume and delayed overlapping thunder. Muted
automated audio checks do not establish subjective sound quality, perfect
loop perception or the absence of unwanted source sounds. Consult the latest
entry in VALIDATION.md for completed build/test evidence.

# Exploration music — 19 October 2026

21October2026 menu/audio follow-up: Master and Music controls appear in both
Overview (Music directly under Master) and Audio. Persistent independent Mute
checkboxes preserve each slider value, using separate master/music mute flags
rather than overwriting volume with zero. Music mute does not mute effects;
master mute applies to the listener. Muting is not track cancellation.

Exploration `Paused` and the simulation pause menu now call `AudioSource.Pause`
on the soundtrack and return before ordinary `isPlaying=false` track-completion
handling. Resume calls `UnPause`, retaining clip position/gain; planned quiet
time between tracks is held too. A clip finishing async decoding while the
menu is open starts at zero gain and is immediately held, not audibly played
behind settings. Ordinary map browsing is not newly treated as a pause menu;
front-end menu music still plays. Collection changes retain existing fades.
No soundtrack files/selection rules, weather/effect volumes or world simulation
are changed. Compile/package only, no agent listening/runtime testing.

This is the music-only first atmosphere pass. It adds no footsteps, animal
calls, ambience emitters, weather, clouds, cities or combat audio. Historical
generation/simulation rules and accepted terrain remain unchanged.

## Using it

Gameplay reads `music/exploration` beneath the existing external music folder.
The menu and simulator continue reading the original top-level era soundtrack;
the two collections never fill each other's missing tracks. The music volume
slider applies to both. An empty exploration folder stays silent and retries,
so newly generated/copied songs become available without restarting the game.
OGG, WAV and MP3 are supported. Only one selected clip is streamed/decoded at a
time; discarded clips are released. No audio is imported into Unity assets.

Filename example:

`Silver Between Branches [biome=TemperateForest+TemperateRainforest;time=night].ogg`

Fields separated by semicolons are AND requirements; values joined by `+` are
alternatives. Field order/case do not matter. `any` or `*` removes a restriction;
an untagged file is universal. Unknown keys, unknown biomes/time values, repeated
keys and malformed brackets fail closed. They never silently become universal.
Biome names are the current Core enum names, not climate codes or speculative
future landforms. There are no regional assignments or per-world soundtrack banks.

Time values are `day`, `night`, `dawn`/`sunrise`, `morning`, `evening` and
`dusk`/`sunset`. The existing saved solar-day clock and visual seasonal phase
provide solar altitude at the player latitude. Below−6° is night, −6° to+6° is
dawn/dusk, above+6° is ordinary daylight. Twilight overlaps day when the sun is
above the horizon and night when below it. Daylight before10:00 is also morning;
from16:00 it is also evening. Polar darkness therefore does not incorrectly pick
noon/day songs just because the clock says12:00. The clock is the current basic
local solar-time model, not a new astronomical/weather system.

Requirements are checked when choosing and before starting a decoded clip.
Ordinary biome/time changes do not interrupt the playing piece. A context change
between gameplay and menu/simulation fades the old collection out before the
other starts. Selection avoids the last three exploration tracks, relaxing only
when the eligible pool is too small. Specific biome/time pieces have greater
selection weight than general travel music. Between naturally completed pieces,
10–35 real seconds of silence leave room for future world ambience. Playback,
fade durations and gaps use real time, never simulation time warp. F3 includes
the current track name; ordinary play has no audio HUD.

## Local production

`scripts/exploration-music.csv` contains30 authored briefs, covering forest,
boreal forest, grassland/savanna, desert, tropical forest, alpine/tundra/ice,
wetlands, Mediterranean woodland and water biomes, plus general travel pieces.
All current biome/day-period combinations have at least two eligible catalog
tracks when twilight flags are included. Target duration128–146seconds, with
publication accepting90–180seconds after silence trimming.

Prompts follow [Stability's guide](https://stability.ai/guides/stable-audio-3-prompt-guide):
musical style, instrumentation, mood, arrangement and tempo. These are original
acoustic/chamber exploration briefs, not requested artist/song imitations.
They request instrumental music without baked environmental effects.

The existing ComfyUI server performs SA3 Medium inference on CUDA, using FP16
diffusion, FP32 tiled SAME-L decoding,8steps and CFG1. No CPU inference fallback
or paid API is used. The batch client uses the installed Python that provides
numpy/soundfile/filelock; its location under a tflite environment does **not**
mean this batch uses LiteRT/CPU inference. It talks only to the CUDA server.

Example resume command on the current host:

```powershell
E:\AI\StableAudio3\optimized\tflite\.venv\Scripts\python.exe -u scripts/generate-exploration-music.py
```

Defaults: the existing server at127.0.0.1:8188, masters under
`E:\AI\ComfyUI\output\worldgen-exploration`, OGGs under `music/exploration`
(the music folder is currently a junction to `E:\WorldGenMusic`), and job
catalog/status/results under ignored `artifacts/exploration-music`.
The existing model weights remain on D:. No new model download is needed.

The batch verifies stereo44100Hz, finite samples, sufficient energy,90–180s
duration, internal silence, conservative RMS/peak matching, nonclipping encoded
readback and nonduplicate waveforms before atomic publication. Seeds, prompts,
master paths and hashes are recorded. Output is deliberately quieter than the
older action-oriented era soundtrack (target−23dBFS RMS, capped at−2dBFS peak),
not certified LUFS mastering. These checks do **not** certify musical quality,
the absence of every vocal-like artifact or a coherent artistic collection.
Those need listening/owner feedback. Source prompts/scripts are Git-backed;
recordings, model weights, masters and generated evidence are not.

Completed managed tracks are skipped on resume; changed/unmanaged files are not
overwritten. Creating `artifacts/exploration-music/PAUSE` stops between tracks.
There is one job lock; timed-out running jobs are not automatically resubmitted.
See current status.json rather than inferring completion from a started batch.

## Sound-effects direction (reviewed19 October2026)

Footsteps remain deferred until the soil/surface work. The weather portion
now uses local SA3 Medium recordings: see [recorded weather effects](WEATHER_AUDIO.md)
for the seven rain/thunder effects (wind removed). The broader SFX/footstep/animal
workflow below remains a direction, not completed features or a new installation.

The existing CUDA SA3 Medium setup can generate sound effects; Stability's
[prompt guide](https://kb.stability.ai/knowledge-base/stable-audio-3-prompt-guide)
documents `TrackType: SFX` and source/action/recording descriptions. It is the
lowest-friction first candidate on this host, not a demonstrated universal
SFX-quality winner. Its [Community license](https://stability.ai/license)
has a commercial annual-revenue threshold; review applicable terms before
production distribution or expanded commercial use.

[MOSS-SoundEffect v2](https://huggingface.co/OpenMOSS-Team/MOSS-SoundEffect-v2.0)
is a dedicated environmental/creature/human-action candidate, with48kHz audio,
durations up to30seconds and a declared Apache2.0 model license. It has not
been installed, auditioned or benchmarked on this host's8GB RTX4060. Compare
a small matched prompt/seed set before choosing it over SA3. Do not infer
hardware fit or sound quality from parameter counts alone. Other candidates
need license screening: the published [TangoFlux checkpoints](https://huggingface.co/declare-lab/TangoFlux),
[AudioX](https://huggingface.co/HKUSTAudio/AudioX) and [Woosh weights](https://github.com/SonyResearch/Woosh)
state non-commercial restrictions, unlike their sometimes permissive code.

Generate and curate assets offline; do not put model inference in the game.
Reuse the GPU setup, but use a separate SFX catalog/exporter: this music
batch's90-180second duration and quiet-music mastering gates do not fit one-shots.
Keep masters/models outside the repository. Save source prompts, model revision,
seeds, source/license notes and export metadata, with media excluded from Git.
Audition for artifacts, unwanted music/animals, recognizable source identity,
loop joins and mono compatibility, in addition to signal/peak/silence checks.
Generated animal sounds are not evidence of biological accuracy; use verified
references or appropriately licensed recordings for recognizable real calls.

Runtime direction: semantic sound events resolve small variant banks, without
immediate repetition. Point effects are dry mono spatial emitters; ambient
beds are layered stereo with crossfades. Foot contacts use actual surface,
moisture, footwear/gait and contact timing after soil work. Animal calls come
from actual present animals and behavior, not biome-level random recordings.
Distance/obstruction/interior context controls attenuation/filtering/reverb.
Use pooled nearby voices and separate music/ambience/effects/voice buses;
neither every world cell nor every remote entity needs an active audio source.

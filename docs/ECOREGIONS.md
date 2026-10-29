# Ecoregions and the Earth starter pack (3.28)

## Wildlife audio overhaul ? 28 October 2026

Release: **1,088 admitted takes in 42 banks** (1,295,005,888 bytes), from 648
previous plus 636 newly generated candidates; 196 were excluded by screening.
The accepted cricket/cicada/katydid/frog/understory banks have 93/30/46/63/96
files. Full counts are in the task media proof. The selected IL2CPP build is
`player-wildlife-audio-ready`, BuiltUtc `2026-10-29T03:50:20.2026354Z`.
42 native audio-flow checks and 57 Core catalogue/activity checks passed; all
1,088 active WAV hashes/formats verified. Existing protected assets and source
freeze verified. Generator/screen workers stopped; user game left running.


The previously empty playback bank is now populated with local Stable Audio 3
Small-SFX recordings. The original audition batches are retained; no candidate
folder or owner-deleted audition is restored. The active, shared development
library is `sounds/fauna/ai/<animal>/`. Its media directory is a junction to
`E:/WorldGen-audio/wildlife-overhaul-20261028/active`, keeping bulk WAVs off C:.
All local development players find this shared library through the existing
sound-root resolver. These external recordings are not embedded in the player
executable. A standalone export must include the matching `sounds` directory.

Delete any active WAV you dislike. The next automatic catalogue scan (about ten
seconds) omits it and stops a cached copy if needed. Missing takes do not crash
or silence the remaining bank. Publishing/resuming never recreates an indexed
deleted file. Original auditions and stereo masters remain available separately.
There is **no sound-review/file-management UI**, per the owner's correction;
Audio settings retain ordinary ambient and animal volume/toggle controls only.

Playback now has fifteen habitat groups: crickets, cicadas, katydids, native
frogs, understory birds, cockatoos, hornbills, hummingbirds, parrots, pigeons,
ravens, toucans, turacos, woodpeckers and woodland owls. Native bird/frog taxa must
actually occur in the source ecoregion. Tree callers use the same planting
predicate as visible trees. Exposed/open-habitat callers are not forced to perch
in nonexistent trees. Terrestrial salinity, ice and underwater exclusions remain.
Fireflies and butterflies have no invented vocalisation.

Callers are spatial mono sources, not a whole-forest recording following the
camera. Their world addresses survive nearby chunk/camera changes. Regional
families retain compatible forms; broad insect/frog/understory guilds can have
three coexisting acoustic forms, with each individual retaining its assigned
form and alternating takes without an immediate repeat. These are acoustic
representations of existing lineages/guilds, not newly simulated species or a
new population census. Small stable individual pitch differences, natural phrase
gaps and overlapping insect joins add variation. Current finite clips cannot
reproduce every generated acoustic/genetic trait exactly.

There are 24 ambient caller slots and 16 pooled modeled-animal voices. These are
audio mixing budgets, not animal population caps. Recordings decode on demand;
a 96 MiB idle-cache budget evicts unpinned clips (currently playing/scheduled
clips stay pinned). Hash verification occurs off the main thread. No model
inference, classifier or Python dependency runs inside the game.

Calling responds to season/hemisphere, local solar time, temperature, humidity,
rain and wind. Birds emphasize dawn/dusk; woodland owls call at night. Nearby
walking can briefly hush callers; small birds can alarm before going quiet when
an appropriate bank is available. Modeled birds suppress a nearby duplicate
invisible caller of the same family. Animal voices follow the actual rendered
animal: sparse contact calls, movement/danger alarms, injury cries, defensive
threats, seasonal male cervid rut calls, occasional pack howls and close wing
buzz for appropriate flying insects/hummingbirds. Corpses do not resume random
calling. Calls reuse existing behavior/health state; no new animal navigation,
mouth animation or complete species-specific communication model is claimed.
Several modeled relatives use broad compatible voice families as placeholders.

### Prompting and biological basis

Reviewed the [official SA3 prompt guide](https://stability.ai/guides/stable-audio-3-prompt-guide)
and [upstream guide](https://github.com/Stability-AI/stable-audio-3/blob/main/docs/guides/prompting.md).
Prompts specify a single caller, the requested action and close dry recording
character; they avoid scene-wide forest/night/wetland descriptions. `TrackType:
SFX` and realistic short durations help steer toward isolated effects. CFG1 is
not a reliable negative-prompt exclusion tool. Prompts do not guarantee clean
content: unwanted speech/music/weather/other animals are screened afterward.

Natural-history priors draw on [Cornell bird communication](https://www.birds.cornell.edu/k12/bird-communication/),
[NOAA cricket temperature response](https://www.noaa.gov/education/explainers/can-crickets-tell-temperature-answer-is-in-their-chirp),
[NPS nocturnal insects](https://www.nps.gov/subjects/sound/upload/Dark-Side-of-Shenandoah_Gross_updated-508.pdf),
[Indiana DNR cicadas](https://www.in.gov/dnr/entomology/nursery-growers-and-dealers/periodical-cicada/)
and [Minnesota DNR katydids](https://www.dnr.state.mn.us/insects/katydids.html).
These support broad timing/call-purpose distinctions, not the exact numerical
activity windows, gain, range, cooldowns or thresholds, which are game priors.
No third-party wildlife recordings were downloaded/copied into this bank.

### Asset preparation and evidence

`scripts/audit-wildlife-audio.py` uses the existing local AudioSet classifier to
flag obvious contamination. It is a diagnostic screen, not human listening
approval, a biological-identity guarantee or proof of seamless transitions.
Source prompts, seeds, models, processing and delivered hashes are tracked in
`sounds/fauna/origins.json`; `library.tsv` admits exact files. New discrete calls
have empty lead/tails trimmed with short endpoint fades; source performances and
masters remain intact. Derived clips inherit the screen of their exact parent
hash. The user can reject takes directly regardless of classifier outcome.
Model/encoder terms remain the Stability AI Community License and Gemma terms;
outputs are not described as CC0. No paid service/account was used.

Source generation, screening, raw/processed indices and package/test proof are
under `artifacts/wildlife-audio-20261028`. Scripts generate, screen, prepare and
publish deterministically indexed candidates; numerical checks do not imply the
agent listened to the recordings. No changes to rain, wind, thunder, music,
fauna models/skins/animations, terrain geometry or construction are intended.


## Remaining audio-first fauna candidates - 22 October 2026

Owner asks whether more noise-only planned implementations remain and requests
all of them added to the ongoing AUDIO GENERATION workflow. The editable desktop
fauna CSV lists14 audio-first rows: the five already covered, plus nine woodland/
canopy bird families. Those birds are noise-only FIRST, not permanently invisible:
their planned eventual lightweight/interactive/huntable individuals stay intact.
The planning CSV and saved fauna taxonomy are not rewritten by this asset pass.
No speculative extra crickets/frogs/owls/mammals are counted as new ecology.

Original batch completed288 jobs with zero generator failures in1568.59s
(26min9s); this supersedes the in-progress launch count below. Completion means
valid generated/prepared PCM, NOT owner acceptance of every sound or isolation.
Original review folder is retained; owner-deleted auditions are never restored.

New `additional` collection in scripts/generate-fauna-batch.py:

| Candidate group | Count | Intended later use |
| --- | ---: | --- |
| Cockatoo | 32 | Sparse perched contact/squawk calls |
| Hornbill | 32 | Hollow low/honking calls in supported habitat |
| Hummingbird calls | 32 | Close sparse chirps/contact notes, not a distant chorus |
| Parrot | 48 | Varied non-speech contact/squawk forms |
| Pigeon | 32 | Low cooing phrases |
| Raven | 48 | Croaks/rattles/knocking vocal forms, not fabricated human speech |
| Toucan | 32 | Croaking/yelping/rattling contact forms |
| Turaco | 32 | Resonant hollow/rough call phrases |
| Woodpecker calls | 32 | Short contact/rattle calls |
| Woodpecker bill impacts | 24 | Separate actual wood-tapping bursts, not musical percussion |
| Hummingbird wings | 16 | Separate brief nearby wing hum/flutter, not continuous ambience |

Total360 x20s new candidates, interleaved by group/form/take, same isolated-source
Small-SFX workflow and deletion-safe mono audition index. About605MiB additional
mono delivery candidates, with stereo copies/raw masters extra. The previous
measured batch suggests roughly33min, allowing30-45min or longer under GPU load.
Time/disk are extrapolations, not a finished new-batch benchmark. All648 total
candidates are review material, not an accepted shipped bank or live animal count.
The current in-game bank remains empty pending selection/cache/placement work.

New folder: artifacts/fauna-ai-review-additional-20261022. Started04:31:19UTC
23October, first four groups prepared at publication of this note. A tool-managed
terminal session runs the worker after the compound detached-launch command was
rejected; no permissions or shared-server configuration changed. Existing
ComfyUI/RTX4060/models reused, queue initially idle. No code/DSP/field-recorded
alternatives generated and no agent listening/game launches/profiling.

Script now supports --collection original (unchanged default catalogue) and
--collection additional. Processing uses suitable per-group low-cut frequencies
to preserve low coos, croaks, wood resonance and wing hum rather than filtering
every bird like a high whistle. If C: delivery volume drops below3GiB, it creates
the owned jobs/PAUSE marker after publishing the current clip, without any shared
server interrupt or file cleanup. Resume an IDLE additional producer only with:

```powershell
python scripts/generate-fauna-batch.py --collection additional --seed-base 21103017 --batch-root artifacts/fauna-ai-review-additional-20261022
```

Biological sound references, used for broad source/action distinctions, NOT
downloaded/copied recordings or guarantees of synthetic species accuracy:
[Cornell raven calls](https://www.allaboutbirds.org/guide/Common_Raven/sounds),
[Cornell woodpecker drumming](https://www.allaboutbirds.org/news/how-to-recognize-woodpeckers-by-their-drumming-sounds/),
[NPS woodpecker calls and trunk drumming](https://home.nps.gov/articles/pileated-woodpecker.htm),
[Mass Audubon hummingbird wing sound](https://www.massaudubon.org/nature-wildlife/birds/ruby-throated-hummingbirds),
[San Diego Zoo toucan vocal/bill sounds](https://animals.sandiegozoo.org/animals/toucan)
and [turaco account](https://blogs.sandiegozoo.org/story-hub/zoonooz/a-world-of-birds).
Other call shapes are bounded authored acoustic priors for existing broad bird
families, not scientific reproductions of every member. Source-isolated prompts
still need listening rejection, especially for mimicry, unwanted background
callers, electronic wing hum or accidental musical/drum interpretations.

## Large AI audition batch started - 22 October 2026

Owner approved the288-candidate batch after the size discussion. New reusable
`scripts/generate-fauna-batch.py` builds an immutable, interleaved catalogue:
48 cricket,32 cicada,48 katydid,64 frog,96 bird, all20s. Six/four/six/eight/eight
acoustic forms each have8 candidate takes (12 for birds), allowing roughly half
to be rejected toward the proposed144-file accepted bank. Forms are prompt
families, NOT claimed real species, guaranteed stable AI voice identities or new
saved world taxa. Audition for compatible takes before binding to regional flora/
fauna. No automatic listening approval or selection based on merely valid PCM.

Current batch: `artifacts/fauna-ai-review-20261022`, started03:51:27UTC23October
(22October local). Five initial clips completed at publication of this note;
this is an IN-PROGRESS checkpoint, not a claim288 jobs succeeded. The detached
hidden Python worker uses existing local ComfyUI + RTX4060/Small-SFX, one
inference at a time; GPU use may compete with the game. It does not start/kill
the shared service or install/download new models. Output layout:

```text
artifacts/fauna-ai-review-20261022/
  audition/<group>/*.wav      Mono44.1k PCM16 listening candidates; delete rejects here
  candidates-stereo/*.wav     Preserved stereo candidates, NOT runtime media
  audition-index.json         Form/take, prompt/seed/model/master, exact hashes, processing
  catalog.csv                All288 immutable source-isolated prompts
  batch-status.json          Prepared/present counts and overall state
  jobs/status.json           Actual inference progress/errors
  generator.log              Captured inference job events
  README.txt                 Listening and pause/resume instructions
```

Completed clips get mono downmix, group rumble removal, conservative common
level, peak bound and endpoint fades, matching the initial owner-liked AI bank's
preparation. Stereo copies remain available; mono is spatial source format, not
a reduction in sample rate. Atomic WAV/index publication makes completed clips
available during inference, not only after the entire batch. Once an audition
is indexed, resuming never recreates it if the owner deletes it. Changed existing
unindexed files are not overwritten. No hot reload or in-game bank expansion is
implied. The active `sounds/fauna` manifest STAYS EMPTY; no unreviewed batch
audio is installed into the game and no executable/shortcut change is required.

Graceful pause: create an empty `jobs/PAUSE` file; the current clip finishes.
After any failure, inspect status/current-job.json and the shared Comfy queue
before resuming, to avoid duplicating still-running inference. Same-root resume:

```powershell
python scripts/generate-fauna-batch.py --batch-root artifacts/fauna-ai-review-20261022
```

Both wrapper and generator have file locks. Resume is for an idle/stopped
producer, not starting a second worker while this one runs. No whole-server
interrupt, automatic owner-file deletion or model unloading. Python helper
compiles; actual initial five offline generations/preparations succeeded. No
agent listening, gameplay launches, test suites or profiling. Model licences,
source-purity limitations and storage estimates from the sections below apply.

## AI selected; comparison media withdrawn - 22 October 2026

Owner prefers AI and requests the new audio folder cleared before replacements.
All45 managed WAVs in sounds/fauna and45 corresponding packaged copies were
hash-checked against the manifest and sent to the Windows Recycle Bin. Both
playback manifests are now header-only. Credits remain for the historical
withdrawn comparison. Rain, thunder, music, owner processes/settings/saves and
older rollback build folders are untouched. Raw generation/download/provenance
artifacts outside these playback folders remain. Existing decoded sounds can
continue in an already-running game; next launch has NO wildlife audio until
new approved media is published. No build is required for external media removal.
The spatial-audio section below describes implemented playback and the previous
library, NOT a current45-clip populated bank. No replacements generated this turn.

### Bank size recommendation (design estimate, not an audibility guarantee)

| Group | Accepted takes | Suggested structure |
| --- | ---: | --- |
| Cricket | 24 | 6 stable call forms, 4 takes each |
| Cicada | 16 | 4 stable buzz forms, 4 takes each |
| Katydid | 24 | 6 stable rasp forms, 4 takes each |
| Native frog | 32 | 8 stable call forms, 4 takes each |
| Understory bird | 48 | 8 stable voices, 6 phrases/takes each |

This is144 accepted clips; generate roughly twice as many candidates so owner
rejections are normal, not a replenishment chore. The previous15 x20s SA3 batch
finished in118.59s (~7.9s/clip). Linear extrapolation for288 candidates is about
38min; allow35-50min or longer with competing GPU load. This is NOT a new timing
benchmark or promise. At the current mono44.1k PCM16 delivery format,20s is
~1.68MiB: ~242MiB for144 accepted takes/~484MiB for288 candidates; temporary
stereo masters/evidence/copies are additional. Accepted uncompressed float PCM
would be~484MiB if all loaded, so deploy a bounded lazy working bank, not an
ever-growing resident library. Shorter frog/bird phrase takes are another future
option; existing generation helper currently enforces an18s non-transient minimum.

Do not equate these sample lengths with time-until-repetition-is-noticed. A
recognizable chirp/phrase or unwanted background can expose any bank immediately;
call spacing, source positions and stable species identity matter. Future library
selection should bind a regional species to a call-form, and vary compatible
takes within it, rather than let one caller change into unrelated voices at each
join. Larger per-family banks, call-form metadata and delete-friendly publishing
are planned, NOT implemented by this cleanup. Current loader caps45 admitted
files and current one-clip helper accepts variants1-3; extend those before
publishing this proposed large bank. Manual deletions skip files on next load,
but hot reload/file-watching is not present.

### Source-isolated SA3 prompting

Reviewed the [official SFX guide](https://stability.ai/guides/stable-audio-3-prompt-guide)
and [upstream prompting guide](https://github.com/Stability-AI/stable-audio-3/blob/main/docs/guides/prompting.md).
They recommend specifying source, action/duration and recording character;
`TrackType: SFX` is useful, and duration should fit the described event. Small-SFX
is the upstream model for SFX rather than music. Application to OUR sources:
use one caller, a stable repeated call, close dry direct recording and silence
between phrases. Avoid whole-scene cues (forest-at-night ambience, wetlands,
summer soundscape) that invite additional layers. Avoid enumerating unwanted
animals in the positive prompt; the current CFG1 workflow is not a reliable
negative-prompt exclusion mechanism. Those isolation choices are engineering
inferences, NOT a guide guarantee or proven optimal prompt. Listen/reject any
cross-species calls, atmospheric beds, handling noise or changing voice identity.

Example, with the helper's automatic SFX prefix shown:

> TrackType: SFX. A single cricket repeating a clear four-chirp phrase with brief
> quiet gaps. Close-miked isolated insect, dry direct sound, silent background,
> consistent pitch and microphone position.

`scripts/fauna-ai-prompts.csv` now holds15 revised UNRENDERED starter briefs.
Use a fresh output/evidence directory pair with the generic generator and pass
both to the publisher (`--ai-root`, `--ai-evidence`). Do not reuse the old
immutable `artifacts/fauna-ai-candidates`/`fauna-ai-jobs` pair with changed briefs.
The single-prompt helper already creates a new run folder. No new candidates,
source separation, AI classifier or audio listening were performed this turn.

## Spatial fauna audio - 22 October 2026

The three derived calling-insect parents now have noise-only playback. Native
frogs and native insectivorous understory birds use the EXISTING `Anura habitat
guild` and `Understory bird guild` taxa, requiring `Biogeography.Present` in the
source cell. They are audio-only for this pass, not permanently denied physical
animals later. Firefly remains silent metadata. No new saved taxa, food-web
links, populations, AI, physical animals, generation rules or save schema.
The earlier profiles-only section below is historical where it says no playback.

`FaunaAmbience` derives native-guild calling profiles and habitat/activity gains.
Insects retain their existing province-founder/regional profiles. Species pitch
is stable and modestly varied; a finite library does not realize every stored
pulse/harmonic/temperature-response trait. Native calling forms are regional
presentation derivatives, not new taxonomic or evolutionary claims. Annual
seasons use hemisphere-relative warm peaks; humid warm regions use thermal/
weather opportunities. Solar-hour, thermal and rain/wind gates soften activity;
birds favour dawn/dusk and frogs favour moist conditions. Frog refuges use river,
groundwater/flood and damp organic-forest PROXIES, not actual breeding-water or
dynamic soil-wetness simulation. No arbitrary sounds on bare ocean/ice ground.

Calling sites use a seeded planet-fixed jittered cube grid, NOT player-centred
random spawning or render chunk/LOD seeds. Crickets are near the ground,
katydids in low vegetation, frogs at wet ground/refuges. Cicadas and birds snap
to an actual tree from the existing canonical planting predicate/cache. They
remain ecological trees when graphics toggles hide them. This is not a perched
animal or a guarantee a calling insect is on its specific biological host.

One background habitat job at a time owns a separate terrain patch; moving
24 m refreshes a 175 m neighbourhood, reusing successful AND rejected sites and
evicting beyond 230 m. No complete per-frame world census, terrain queries,
audio raycasts, model inference or per-animal physics. The 12-site voice pool
retains callers across render-tile/chart changes; sources reproject with the
floating origin. Activity refreshes at 2 Hz; gain/filter envelopes run per frame.
Cheap resident terrain lower bounds muffle distant callers behind hills, not
full material/foliage acoustic propagation. No camera-frustum culling for audio.
DSP-clock scheduling overlaps two mono 3D voices for continuous insects with
equal-power joins, stable starting offsets and varied clips. Frogs/birds use
call-phrase clips with gaps. Settings pause world callers; explicit preview can
still play. No exact future multiplayer/audio-phase synchronization is claimed:
locations and profiles are seeded, scheduling currently follows local DSP time.

### Three comparable sound libraries

Audio settings has a wildlife master toggle/volume and per-group enable/source
selection plus Preview (cycles variants), Stop preview, and loading status.
Preview bypasses habitat/time, NOT master/wildlife volume. Missing or changed
hash-bound media is skipped, never silently substituted by another method.

There are **45 mono PCM16 44.1 kHz WAVs**, three per group per method:

| Group | Local AI | Independent DSP | Free field recording |
| --- | --- | --- | --- |
| Cricket | 3 SA3 Small-SFX candidates | 3 tonal chirp/trill trains | 3 excerpts from TheWoodlandNomad's Cricket Chirp, CC0 |
| Cicada | 3 SA3 Small-SFX candidates | 3 modulated filtered-noise buzzes | 3 excerpts from macdaddyno1's Japanese Cicada, CC0 |
| Katydid | 3 SA3 Small-SFX candidates | 3 band-limited rasp trains | 3 excerpts from timb12957's SC_Katydid.wav, CC BY 4.0 |
| Native frog | 3 SA3 Small-SFX candidates | 3 FM/harmonic croak phrases | 3 excerpts from SpliceSound's Green frog, croaking.wav, CC0 |
| Understory bird | 3 SA3 Small-SFX candidates | 3 FM whistle phrases | 3 excerpts from organicmanpl's Single bird, CC0 |

Field variants are three excerpts of ONE source per group, not three independent
recordings. All methods use group-specific rumble removal, mono downmix,
resampling, bounded common level/peak and short endpoint fades. These operations
do not prove isolation, convincing anatomy, absence of unwanted AI sounds,
natural transitions or a fictional regional species match. No clips have been
auditioned by the agent; owner listening is required. Default source is AI,
individually replaceable with DSP/field recordings in settings.

Media totals 50.11 MiB on disk; all comparison banks decode to about 100.22 MiB
PCM before engine overhead (not a measured native memory profile). Hash screening
runs off-thread and Unity loads files asynchronously once per exploration. No
per-frame disk reads. F3 full mode shows cached voice/clip/job status. This first
pass deliberately keeps all banks resident for instant comparison; lazy bank
eviction is a future option. CPU/FPS cost is unmeasured, not guaranteed free.

### Reproduction, replacements and credits

The existing `E:/AI/ComfyUI` service and installed SA3 Small-SFX/encoder weights
were reused on the RTX4060; nothing was installed or charged. This does NOT move
the game/builds onto E:. Local offline AI is not a dependency at game runtime.
SA3 is free under its qualifying Community terms, not unconditionally licensed
for every commercial business; check commercial registration/revenue conditions
and encoder Gemma terms before distribution. No terms were accepted or account
registration performed on the owner's behalf. See the [model card](https://huggingface.co/stabilityai/stable-audio-3-small-sfx),
[upstream implementation](https://github.com/Stability-AI/stable-audio-3),
[licence](https://stability.ai/license) and
[commercial-use guidance](https://kb.stability.ai/knowledge-base/stable-audio-commerical-and-usage-licensing).

With that existing local ComfyUI service running, generate a candidate:

```powershell
python scripts/render-fauna-ai.py --family cricket --variant 1 --prompt "One cricket giving soft clear rhythmic chirps with natural pauses."
```

Add `--publish` to replace that managed variant and update its SHA256 admission
manifest (no automatic listening/acceptance). Relaunch to load a published clip.
Each run gets a new evidence folder; the helper does not delete old candidates,
kill/start servers, or overwrite unknown/externally modified media. `--seed` and
`--checkpoint` are available; Small-SFX is the default, not a claim to be the
best model for music. Song generation remains the existing separate workflow.
The initial 15 briefs are `scripts/fauna-ai-prompts.csv`; batch reproduction:

```powershell
python scripts/generate-weather-sounds.py --catalog scripts/fauna-ai-prompts.csv --output artifacts/fauna-ai-candidates --evidence artifacts/fauna-ai-jobs --checkpoint stable_audio_3_small_sfx.safetensors --seed-base 731923 --limit 15
python scripts/prepare-fauna-audio.py
```

`prepare-fauna-audio.py` independently synthesizes DSP, processes admitted AI
candidates, verifies source-page licences and downloads only public HQ previews
(original Freesound downloads require login; none bypassed). `--replace` is
required for already managed clips that would change. One-clip replacement uses
`--only-ai` and does not re-download/regenerate the other banks. Exact prompts,
seeds, models, source URL/hash and processing levels are in ignored
`artifacts/fauna-ai-jobs/results.json` and `artifacts/fauna-audio/release.json`.
Runtime `sounds/fauna/library.tsv` and `ATTRIBUTION.txt` ARE source controlled;
WAVs, downloaded masters, models and evidence are NOT backed up in Git. The
packaged player receives a complete `sounds/fauna` copy plus credits; the repo
shortcut uses the repo sound root for later replacements.

Recording primary sources and licences:
[cricket](https://freesound.org/people/TheWoodlandNomad/sounds/363111/),
[cicada](https://freesound.org/people/macdaddyno1/sounds/321591/),
[katydid](https://freesound.org/people/timb12957/sounds/158782/),
[frog](https://freesound.org/people/SpliceSound/sounds/188195/),
[bird](https://freesound.org/people/organicmanpl/sounds/518749/).
Katydid attribution/title/source/licence/modifications ship in ATTRIBUTION.txt;
retain this when redistributing. [CC BY 4.0 terms](https://creativecommons.org/licenses/by/4.0/).

Playback architecture references: [Unity spatial blend](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource-spatialBlend.html),
[DSP scheduling](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayScheduled.html),
[scheduled ends](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.SetScheduledEndTime.html),
and [Wwise randomized sound containers](https://www.audiokinetic.com/en/public-library/2024.1.4_8780/?id=creating_random_container&source=Help).
These inform source variation/overlap, not a new middleware dependency or an
assertion that a particular commercial game's sound system was reproduced.

## Derived small-fauna profiles - 22 October 2026

`AmbientFauna` adds exactly four broad parents: Cricket, Cicada, Katydid and
Firefly. There is no authored Earth species roster for them. One candidate per
parent is derived per region; world-seeded province founders dominate the traits,
and a separate regional stream adds small bounded divergence. Names, stable
version/seed/province/region identities, parent links and habitat/activity traits
are inspectable in F3+E under **Derived small fauna**. This is static procedural
diversification, not simulated genetics, ancestry walks or population evolution.

Crickets and cicadas are explicitly **AudioOnly**, as requested; katydids use
the same representation. Their stored song traits cover carrier frequency,
harmonic/noise mix, relative loudness, pulse/phrase timing, envelopes and modest
within-species differences. Calling tempo temperature sensitivity is distinct
from pitch sensitivity; cicada tempo dependence is deliberately much smaller
than orthopteran tempo dependence, with thermal activity gates separate. A
species has a stable inherited song, not a random pitch every time it calls.
Fireflies are **LightweightVisual** candidates with no acoustic traits: flash
bouts/colour/height and activity metadata only, default nonsynchronous. No
rendered insects, point lights, AudioSources, generated waveforms or playback,
per-frame jobs, individual AI, emergence or reproductive simulation are added.

Activity includes a solar-hour window, thermal envelope, local-warm-season
offset/display-window duration and rain/wind suppression parameters. Warm
humid-climate candidates use a warm/moist opportunity cue rather than an imposed
northern calendar. These are future-scheduler inputs, NOT a running seasonal
population model. Diet/life-stage descriptions distinguish cicada root/plant
xylem feeding and firefly larval versus adult niches without inventing new
saved food-web links or pretending the existing coarse food flags model sap.

The constant-time HabitatSuitability query uses saved habitat/thermal/aridity/
salinity and coarse canopy support. It is explicitly abiotic **potential**, not
occurrence probability, abundance, verified host trees, local wetness, native
dispersal or complete regional coverage. The wiki shows current-cell potential
separately from the regional representative reference. Unknown reference habitat
is null rather than claimed dry. Future audio/visual placement must validate
actual local cover/trees/moisture and apply a coherent locality/dispersal policy;
catalogue membership alone must never produce calling insects everywhere.

Like derived flora, profiles are lazy per-world/per-region data, held through
a weak world context and a concurrent region cache. No terrain scans, lineage
graph walks or global animal census. Named founder/branch/name streams and an
explicit `ambient-fauna-1` version isolate this work. No saved Taxon/SpeciesBits/
region food-web/recipe/schema/biome/historical state changes; old worlds can
inspect candidates without regeneration or a silent recorded ecology upgrade.
The existing native3 count remains 203 recorded entries (184 animal entries,
including 20 animal guilds); these four are separately labeled derived profiles.

Research: [Singing Insects of North America](https://orthsoc.org/sina/howtouse.htm)
uses song as well as ecology/season to distinguish cricket/katydid species;
[NOAA](https://www.noaa.gov/education/explainers/can-crickets-tell-temperature-answer-is-in-their-chirp)
describes species-dependent cricket chirp-rate/temperature relationships;
[UConn's cicada species reference](https://cicadas.uconn.edu/species/) documents
distinct mating songs; [UGA Extension](https://fieldreport.caes.uga.edu/publications/C1213/georgia-fireflies/)
describes species-specific flash signals and temperature-dependent display.
The numerical ranges here are independently authored gameplay priors, not fitted
measurements, imported species records or copied sound recordings. No fixed
13/17-year brood schedule or universal synchronous flashing is assumed.

Owner compile/package-only restriction applies. No tests, player/world launches,
screenshots, sound audition or profiling; numerical/biological calibration and
runtime wiki review remain unverified. This is a profile foundation only.

## Broad animal lineages — native content 3, 16 October 2026

Explicit `earth-native-3` recipes select 203 entries, including 164 named animal lineages. New recipes now default to native3 following focused generation, persistence and independent visual review; existing saves are never repopulated. Explicit `earth-native-2` still constructs its original 96 entries; missing/null still constructs the original 50. Terrain, climate, hydrology, biome classification and regional boundaries are unchanged. Content 3 adds actual coastal-access flags on non-ocean coastal cells and a wetland habitat flag on saline lakes, downstream of the accepted abiotic fields.

Animals are conceptual lineages rather than a list of predetermined geographic subspecies. There is one Elephant, Hare, Wolf, Deer, Bear, Mouse and similar broad archetype, with retained ancestral native sources where appropriate. For example Elephant can have African and Asian ancestral source realms on measured Earth, but there are no separate African/Asian elephant gameplay entries. Procedural worlds choose their own realm homelands. Lion remains a geographically constrained lineage, not a universally climate-filled predator. These broad lineages are a gameplay abstraction: they intentionally combine some actual Earth species/genera and do not assert that their generated forms match modern taxonomic species.

The expanded fauna includes savanna browsers/grazers, apes and canopy monkeys, desert specialists, small burrowing mammals, bats, marsupials/monotremes, reptiles, woodland/open-country/aquatic birds, penguins, gulls and other seabirds, seals, and complementary soil/insect/aquatic prey pools. Examples include giraffes, rhinoceroses, hippos, camels, wild cattle, yaks, gorillas, orangutans, sloths, aardvarks, pangolins, crocodiles, tortoises, kingfishers, cranes, hummingbirds and kiwis. Whales, dolphins, orcas, sharks, dugongs and sea otters have explicit **adjacent-sea feeding/visitor** records, never fictional terrestrial breeding populations. It is broad coverage, **not literally every major Earth animal**: a full pelagic ecosystem, life stages and detailed fish/invertebrate diversity are still absent.

Resident constraints now include canopy structure, explicit coastal or freshwater dependence and (for saline guilds) minimum salinity, in addition to the existing temperature, aridity, relief, strata and resource constraints. Temperate open-hunting hawks do not occupy hot closed-canopy rainforest; a different forest-eagle lineage can. Hippos require a freshwater refuge even if surrounding forage is suitable. Coastal colonies/feeding access use the adjacent sea, with a local phytoplankton → marine invertebrate → forage-fish support network. Penguins cannot be placed in an inland cold cell, and marine forage fish do not appear in inland freshwater merely to support them. Saline algae and brine shrimp support saline-wetland feeding opportunities. These are terrestrial ecoregions with adjacent-water feeding pools, not a claimed open-ocean range/population simulation.

`Taxon.Lineage` stores complete authored source/niche data. Regionally realized `Variant.Ecotype` records lineage identity, an actually occupied sample cell, habitat form, mass/body ratio, coat pattern, ear/appendage variation and display-trait variation (for example tusks or antlers), alongside existing insulation, eligible seasonal whitening, lightness and behavioral priors. Named streams, province, habitat and local thermal regime make related forms coherent and queries repeatable; no query traverses ancestry or region memberships. A coastal animal's form is not sampled from an unoccupied inland regional representative. Thermal body-size effects are deliberately modest and lineage-dependent, not a universal latitude law. Ear differences are enabled for specific eligible lineages; seasonal white coats require an eligible lineage and a cold seasonal environment. These remain static procedural ecotypes, not an implemented dynamic genetic/speciation model.

Food links retain actual cell co-occurrence, plausible strata, size and dietary compatibility and the least basal-rooted closure, with no three-tier limit. Predator abundance is not invented. `AnimalEcotypes.Domesticability(taxon, optionalVariant)` is a constant-time comparative opportunity query using diet, captive breeding, maturity, docility, panic and social tolerance. Elephant taming is not treated as easy captive-breeding domestication; horse/zebra behavior differences remain consequential. Existing historical husbandry already consumes the local variant's behavior rather than its predator count.

Only generation/explicit validation/rendering enumerate terrain. There is no new recurring fauna event, cell census or historical population calculation. Multiple native sources use ordered bounded graph searches during generation; the full pack, realized origins, presence bits, regional ecotypes and food webs are saved. Nullable lineage/ecotype extensions are omitted from old canonical hashes. Old worlds are not silently repopulated when loaded or when their historical rules are upgraded. No golden expectations are regenerated.

Independent references: the [Australian Antarctic Program food web](https://www.antarctica.gov.au/about-antarctica/animals/food-web/) and [penguin accounts](https://www.antarctica.gov.au/about-antarctica/animals/penguins/) motivate adjacent-marine basal support and coastal breeding access; [NPS eagle habitat/diet observations](https://www.nps.gov/articles/bald-eagle.htm) motivate separating fish-hunting and forest/open-country raptors. [Gohli and Voje's comparative mammal study](https://pmc.ncbi.nlm.nih.gov/articles/PMC5069937/) cautions against treating Bergmann/Allen tendencies as universal across lineages. Authored thresholds, sources, trait values and phenotype coefficients are not imported from those studies and are not a scientific calibration.

Review evidence: the focused ecology suite passed 794 assertions, including new/legacy pack content, resource/stratum/size contrasts, co-occurrence, basal support, native-source suitability, actual occupied ecotype sampling, complete binary/JSON persistence and all-stage reconstruction. Direct profile-2/profile-3 comparison retained identical elevations, climate, hydrology, biomes, provinces and region memberships. Fresh 20,000-cell Earth, Archipelago and Boreal controls generated in roughly 5.6–6 seconds on this host; timing is not a universal performance promise. The cold Boreal control had penguin/gull coastal habitat but no elephants/lions. Shared climate/biome/ecoregion and food-web screenshots were viewed by the implementer and an independent critic. Remaining findings: measured-Earth Amazon/Congo forest extents and Eurasian dryness inherit existing climate errors; full dense food webs need better interactive filtering/edge routing. Do not compensate for those climate errors with hard-coded animal geography or remove valid ecological links merely to simplify the diagram. Review artifacts remain ignored, not source data or golden expectations.

An additional independent 200,000-cell Earth/Continents Plus audit found that ground occupancy for nesting or hauling out had incorrectly also granted ground feeding to marine feeders. In the still-unreleased native3 content this is corrected: penguins, puffins, albatrosses, cormorants, seals, sea lions, walruses and the represented marine-grazing turtle forage in water, while gulls and shore-ground waders retain ground access. The existing water-stratum algae/seagrass pool supports turtle grazing; terrestrial herbs and mangrove leaves do not. This changes native3 biogeography output intentionally, not terrain, climate, hydrology or regional boundaries. Native1/native2 catalogs and saved assemblages remain unchanged. The [Australian Antarctic Program penguin account](https://www.antarctica.gov.au/about-antarctica/animals/penguins/), [NOAA bearded-seal account](https://www.fisheries.noaa.gov/species/bearded-seal) and [NOAA green-turtle account](https://www.fisheries.noaa.gov/species/green-turtle) support distinguishing feeding habitat from time spent on land. The generic turtle remains a limited marine-grazer archetype, not a complete representation of every sea-turtle diet or life stage.

The same audit found that an inherited Beaver `Forest | Wetland` envelope allowed dry forest residence without water. Native3 now requires the existing freshwater/refuge condition, supported by the [ADW beaver habitat account](https://animaldiversity.org/accounts/Castor_canadensis/). A generic forest biome is insufficient; an actual riparian/flooded freshwater site can qualify. No new arbitrary latitude or tropical temperature ban is added. Existing native1/native2 catalogs and saved ecology are unchanged.

Native3 also stores a small optional predation-trait record distinguishing dangerous adult prey, snake body form and specialized snake-handling technique. Merely eating occasional vertebrates no longer makes a capuchin behave like an adult apex predator in the risk gate; bears and other explicitly dangerous adults retain the conservative restriction. A secretarybird's snake handling permits a compatible snake food link, without bypassing body-size, stratum or actual co-occurrence checks. It does **not** mean venom immunity, guaranteed kills or diet proportions. [Observed harpy-eagle prey](https://pmc.ncbi.nlm.nih.gov/articles/PMC7456529/) include capuchins, while [UCT observations](https://journals.uct.ac.za/index.php/BO/article/view/251) and [SANBI's account](https://www.sanbi.org/animal-of-the-week/secretarybird/) support the secretarybird/snake distinction and explicitly caution against claiming venom immunity. Null traits preserve the old risk rule and canonical bytes, including pre-extension native3 saves. New traits serialize with the ecology; they are not hidden name-based exceptions inside the feeding algorithm.

### Regional source breadth review

The native3 source selector gives large terrestrial ground lineages (at least 10 kg, excluding flying, coastal, aquatic and freshwater-dependent forms) a soft preference for a broader ancestral resident-habitat patch. The former nearest-cell rule could choose one isolated open marsh stamp in otherwise closed forest, despite suitable wider landscapes nearby. Existing realm/landmass eligibility, Earth-source provenance bounds, temperature/food constraints and final dispersal remain authoritative; no additional origin or global predator fill is granted. Plants, crop progenitors, small animals and the excluded categories retain their source selection.

The existing origin scan retains at most eight candidates from fixed distance shells around the same ancestral anchor. Each receives at most 64 resident-suitable-cell probes. Measured connected area below an authored 30,000 km² regional breadth target adds at most 1,000 km-equivalent provenance cost; an unexhausted frontier at the work cap is unknown and gets no missing-area penalty. This is an approximate, conservative source preference—not species-specific minimum viable population/area, prey biomass, a guaranteed range, or a claim of perfect detail invariance. A small isolated lineage remains possible. Resident-only connectivity can miss an allowed short unsuitable transit: in one Earth test a tiger source moved while its resulting range stayed exactly unchanged. The bounded search does not alter the actual `Reach` rules.

The independent offline 10,000/30,000 km² counterfactual was retained in ignored review artifacts. For the 200,000-cell Earth seed 73421 example, the old lion origin was 139 km from its anchor with 2,668 km² of resident habitat; an alternative 314 km away had 8,586 km², while one 807 km away had at least 30,769 km² and an existing physical route to broader suitable country. The selected 30,000 km² target is a provisional regional breadth prior, not an empirical lion calibration.

All 60 eligible lineages were compared before/after on Earth seeds 73421/731 and Continents Plus seeds 261015/731, each at 20,000 and 200,000 cells. Across these eight controls, 1–19 source/range changes occurred per world, with no new lineage absence or appearance and no excluded-category origin changes. Accepted elevations, climate, hydrology, habitat fields and regional memberships remained identical; only biogeography and the final combined stage hashes changed. One pronghorn range shrank from 764 to 762 cells; other changed ranges were unchanged or larger. Restricted forms were not forced broad: one procedural lion lineage still occupied a single cell, while a second procedural seed retained its already broad distribution. Fresh generation took roughly 5–14 seconds in these controls. Actual occupied-site maps were reviewed, not inferred from catalog size. Current reconstructed Earth climate errors still affect potential ranges; these are not calibrated modern range maps.

The final expanded focused suite passed 857 ecology assertions (104 pure lineage/source assertions), including deterministic fresh/reapply/serialized-recipe/cached reconstruction, persistence/deep copying, exact legacy/null-trait canonical hashes, source exclusions, physical-area probes, bounded/censored search, cancellation, freshwater refuge, and actual food-web negatives/positives. Review JSONs, before/after maps and generated worlds remain ignored artifacts rather than source or golden fixtures. The eight controlled source-preference comparisons above preceded the final separate Beaver freshwater and predation-trait corrections; their original reports/maps are preserved alongside compact final-content reports with exact stage hashes.

## Previous content extension — 14 October 2026

New recipes select `earth-native-2`: 96 entries, including additional forest mammals, reptiles, birds and wild husbandry candidates, serialized behavior/utility traits and regional forms. Missing/null profile preserves the original 50-entry pack below. Existing saved ecology is never silently replaced: regenerate to acquire the new native assemblages. Updating old history rules alone does not invent missing fauna.

The expanded pack still uses native homelands and bounded dispersal, not global climate-only placement. Temperate envelopes and added regional forest communities address implausible tropical assemblages. Procedural homeland selection discourages stranding a whole continental fauna on a tiny first-species islet. Food links remain diet/stratum/size/co-occurrence constrained and basal-rooted, with no fixed trophic depth. The diagram lays out actual dependency components, including omnivory/cycles; it does not estimate population or biomass.

Regional forms retain taxon identity with bounded size, insulation, lightness, eligible seasonal whitening and modest behavioral differences. Historical husbandry uses the local behavior priors. These are provisional morphotypes, not speciation or measured range maps. The native horse entry is not a claim that domestic horses descend from Przewalski's horse. The pack remains representative and lacks many actual crop/livestock progenitors. ALGORITHMS.md records current contracts, history integration, old-hash behavior and research context.

The owner authorizes static ecological synthesis: abiotic habitat fields, potential-natural biomes, regional identities, native assemblages and inspectable food webs. Modern native species, no human introductions or land-use destruction; on procedural planets homelands are generated rather than copied from Earth. This is not population dynamics, evolution, civilization or an exhaustive Earth biodiversity inventory.

## What to use

Select **Ecoregions** for regional colors/borders, **Biomes** for vegetation structure, or the new Floodplains, Soils, Water Table, Permafrost, Aridity, Salinity and Fire Potential diagnostic layers. Click a cell and expand/scroll the inspector for regional species and resource-to-consumer links. An asterisk identifies taxa actually present in the selected cell, not just elsewhere in its region. Older saved worlds remain unchanged; regenerate and save a new copy to acquire ecology.

## Layer 1: habitat conditions

All fields are downstream of existing climate and hydrology. No terrain, coastline, precipitation, temperature, runoff, water storage or receiver is rewritten.

| Stored quantity | Meaning / limitation |
| --- | --- |
| PET, seasonal water deficit, P/PET | Oudin-style radiation/temperature demand estimated at four quarterly means; P/PET capped at 20 for storage. Not actual evapotranspiration or added moisture. |
| Flood fraction, days/year, pulses/year, regime | Discharge/width, local relief, lake margins and coastal elevation estimate inundation potential; river and tidal regimes are separate. These are priors, not fitted flood return periods. Fractional floodplain information remains below display thresholds. |
| Water table, soil class/fertility | Recharge, local drainage relief and wetness proxies; not a groundwater head solution, lithological map, measured soil profile or agricultural suitability guarantee. |
| Permafrost likelihood | Mean-temperature climatological proxy; not ground thermal physics or an active-layer depth. |
| Water-body and cell salinity | Dissolved weathering-load proxy, storage, throughflow and synthetic accumulation history; ocean 35 ppt, closed waters can be hypersaline. Not measured Caspian/Dead Sea chemistry or an ion/sediment budget. |
| Fire potential | Fuel productivity combined with seasonal moisture deficit; no actual fires or fitted return intervals. Supports open/wooded habitat distinctions. |
| Potential productivity | A precipitation/temperature/growing-season proxy in gC/m2/year. Not net resource supply allocated among populations. |

Tidal classification is a low, flat, directly coastal subgrid prior. 730 potential pulses/year represents a generic semidiurnal coast, not a global tidal model; duration is the number of days with recurring tidal influence, not 365 continuously submerged days. River pulse count is currently a one-season prior. Quarterly averages cannot establish real annual flood probabilities. Do not consume these fields as calibrated civil-engineering hazards.

## Layer 2: regionalization

Biome classification uses thermal limits, evaporative demand, seasonality, flooding, salinity, soil wetness and fire suitability—not just the Köppen code. The accepted climate classifier remains unchanged. Seasonal floodplains, freshwater, saline lakes, tidal marshes and mangroves have distinct habitat categories. Whole-cell freshwater classification retains the existing 90% lake coverage rule.

Area-weighted, named-seed province sources grow over non-ocean adjacency with distance, relief and aridity resistance. Every disconnected land component has a source. Each province/biome combination becomes an ecoregion with stable ID, area, cell membership, representative cell, neighbors and patch count. Multiple same-biome patches may share a region; oceans cannot join them. There is no one-region-per-climate rule, no plate-polygon mask and no copied observed Earth ecoregion boundaries. Small islands and small habitat patches are retained, so region count varies with resolution. Boundaries are a procedural first approximation, not an inferred reconstruction of Earth's actual named regions.

## Layer 3: native biogeography and trophic structure

`EarthLifePack.Create()` supplies **50 entries**, a representative mix of real plant/animal species, an explicit wolf subspecies and labeled functional guilds. The latter represent unresolved local diversity; they are not single cosmopolitan species. In particular basal grasses, invertebrates and aquatic guilds avoid claiming complete plant/insect/fish inventories. Detailed marine ecology is not included.

Traits are explicit, serialized `Taxon` records, making future packs possible without changing the food-web/dispersal interfaces:

| Trait | Use |
| --- | --- |
| Scientific identity, rank, parent species | Separate species, subspecies and unresolved guilds. Arctic wolf is `Canis lupus arctos`, with parent `Canis lupus`; parent/subspecies are not counted twice per cell. |
| Habitat flags, coldest/warmest-quarter limits, aridity, elevation, flooding and salinity tolerances | Resident habitat suitability; tolerances are procedural ecological priors, not measured physiological extrema. |
| Passage cold/heat limits, habitat-gap distance, ocean-crossing distance, spread budget, historical-interchange eligibility | More permissive passage envelope and finite accessibility; no unlimited climate-only global flood fill. Cold tolerance alone does not assert that every modern species used ancient bridges. |
| Origin group and native Earth anchor | Earth provenance; procedural worlds place separated group homelands and keep group origins on their homeland landmass. Species may be absent when no suitable homeland exists. |
| Body mass, min/max routine prey mass, diet, foraging/occupied strata | Gates trophic links. Pack hunting is represented by expanded routine prey envelopes, not pack demographics. |
| Required resource | Specialized diets, currently koala dependence on the represented eucalyptus. |

Earth anchors constrain origins, not climate, habitat or final ranges. They are approximate provenance points, not range polygons. Current and historical accessibility are separate graph passes: eligible taxa can use a historical scenario exposing bathymetry down to -120 m and cooling route temperatures by 5 C. Holarctic taxa and lion are eligible in this starter pack; the lion's cold limit still blocks Bering-like routes. Named plants and other regional taxa are not automatically granted historic interchange merely for tolerating cold. Coldest-quarter upper bounds exclude generic gray wolves/foxes from hot year-round rainforest and keep cold-forest specialists out of tropical lowlands. These are authored pack priors, not reconstructed dispersal events. The historical scenario is **not** a reconstructed ice-age climate/ice-sheet/sea-level chronology. A deep ocean cannot become a land bridge. Coarse grids can miss narrow real straits.

Each pass stores a best resistance route per cell, with per-route unsuitable-gap and sea-run limits. This is a conservative bounded routing heuristic, not a complete multiobjective reachability solver: a longer valid alternative can be missed. The historical pass does not model ecological succession or extinction following climate change.

Presence is retained as a compact per-cell bit matrix plus area-weighted regional summaries. Food links require actual cell co-occurrence, diet compatibility, physical access to the prey's stratum, prey-size compatibility and specialist resource compatibility. Examples exercised by tests: wolf-to-hawk and wolf-to-fly are excluded; hawk-to-mouse, web-spider-to-fly and lion-to-zebra are allowed. Healthy adult elephant is outside the routine lion prey envelope; calf/egg/larval stages are not yet represented. All consumers must be connected to a local basal producer/detrital resource; unsupported predator cycles cannot support themselves.

An additional adult-predator risk gate limits predatory prey to one quarter of the consumer's own mass. This prevents the wolf's pack-sized ungulate envelope from making adult lions routine prey; it is a conservative authored defense proxy, not a combat simulation.

The displayed web describes **plausible feeding opportunities**, not observed diet percentages, population carrying capacities, abundance, competitive exclusion, migration calendars or dynamical stability. Several predators sharing one prey species does not prove enough prey biomass exists. Those need a subsequent resource/energy-budget and population model; the current implementation deliberately does not fabricate abundance numbers.

## Determinism and persistence

`WorldPipeline` alone runs habitats → ecoregions → biogeography after hydrology. New recipes are 3.28; 3.27/3.26/3.25/3.24 remain supported. An optional `World.Ecology` field is omitted from canonical hashing when null, preserving historical snapshot hashes. The 3.27 mountain field remains hashed in both 3.27 and 3.28. Numeric fields are quantized, graph costs/ties ordered, and random samples use named project PRNG streams. No external life-pack file, observed range dataset, worker timing or renderer changes authoritative state.

The Earth starter pack version is persisted with its complete trait records. Changes to default traits or ecological algorithms require versioning. New packs are a future recipe/content-version extension, not a renderer-only replacement. Tests cover fresh/reapply/recipe/cached reconstruction, binary/JSON persistence, native/browser parity, resident suitability, trophic co-occurrence, and bridge barriers. No old golden expectations are automatically regenerated.

## Ground foundation,19 October2026

`GroundEnvironment.Version = ground-environment-1` is a derived Core service,
not a replacement WorldPipeline or a silent change to accepted ecology hashes.
An ecoregion remains a geographic biological assemblage, not a soil label. The
reviewed [ecoregions map](https://ecoregions.appspot.com/) illustrates why one
climate/biome cannot specify all regional ground and vegetation traits.

The contracts separate five things:

1. Bedrock family: granite, basalt, sandstone, limestone, mudstone, metamorphic.
2. Parent deposit: residual, alluvium, glacial sediment, windblown sand or loess.
3. Soil: sand/silt/clay fine-earth fractions, coarse fragments, rootable depth(m),
   organic fraction, inferred pH, accessible fertility, drainage, root-zone
   available water(mm), iron tint, salinity and weathering. These are explicit
   authored proxies, not formal soil orders or a calibrated pedological survey.
4. Exposed local mixture: soil, sand, gravel, clay, rock, organic ground, salt and
   existing permanent ice. Smooth supporting-cell interpolation plus absolute
   physical-metre noise makes patches without tile/cell reseeding. Slope exposes
   parent rock; coastal sediment admits both sandy and gravel shores.
5. Future surface cover: living grass, litter, roads, accumulated snow, transient
   wetness and puddles must modify these substrates rather than overwrite them.

Low-relief high ground and exposed mountain faces remain separate landforms.
This revision supplies a loose-sand mask but does NOT generate dune relief or
mesa geometry, move water, cut new channels or alter accepted heights. Earth
uses measured relief but SYNTHETIC geology; a later measured geology adapter can
replace parent-material inference without changing the surface/crop/audio APIs.

Fresh/explicitly upgraded optional historical realm38 uses this same soil for
actual cultivar establishment/yield/food fits: depth, stones, nutrient supply,
acidity, available water and drainage have distinct effects. Wetland grain and
deeper-rooted orchard groups differ. Existing climate, seed, labour and physical
contact constraints remain; this grants no crops, people, water or towns.
Realm37 and earlier continuations remain literal. Cache keys distinguish these
rules, including the cultivar/calendar caches. No daily all-cell soil tick or
recurring empire/ecoregion census is added. Point and actor queries are bounded;
the local terrain's existing one-time per-cell preparation stores ground profiles.

Long-term order: replace provisional geology where justified; improve sediment
source/transport and persistent soil horizons; then richer ground-cover/plant
traits, succession and disturbance; then sparse land-use depletion/amendment and
random-access weather moisture/snow modifiers. Preserve intrinsic ground apart
from these time-dependent overlays. Detailed regional vegetation and ecoregion
taxonomy are intentionally NOT claimed by this ground-only pass.

Research basis, independently implemented:

- [NRCS soil formation](https://www.nrcs.usda.gov/resources/education-and-teaching-materials/soil-facts):
  parent material, organisms, climate, relief and time, including transported material.
- [USGS desert features](https://pubs.usgs.gov/gip/deserts/features/): most desert
  surfaces are not dune sand; rock, pavement, alluvium and playas matter.
- [USGS beaches](https://www.usgs.gov/publications/beaches): sediment sources and
  coastal processes produce sand/gravel mixtures rather than universally sandy shores.
- [NRCS available water capacity](https://www.nrcs.usda.gov/sites/default/files/2022-10/nrcs142p2_051590.pdf):
  texture, coarse fragments, organic matter and salt affect plant-available water;
  more clay does not monotonically mean more available water.
- [NRCS pH](https://www.nrcs.usda.gov/state-offices/illinois/soil-tech-note-24a-ph),
  [Oxisols](https://www.nrcs.usda.gov/conservation-basics/soil/oxisols) and
  [Aridisols](https://www.nrcs.usda.gov/conservation-basics/soil/aridisols)
  inform weathering/leaching and dryland salt priors. Coefficients remain ours,
  not downloaded survey observations or proof of scientific calibration.

## Earlier ecological references

Concepts were independently implemented; no reference-project source or external species table was copied. Numerical trait thresholds and proxy coefficients are initial authored priors, **not** an imported/calibrated trait database.

- [Ecoregions 2017 map](https://ecoregions.appspot.com/) and [Olson et al., terrestrial ecoregion definition](https://digitalcommons.usf.edu/kip_articles/4884/): geographic assemblages, not merely climate labels. The map's downloadable CC-BY dataset is not bundled here.
- [EltonTraits data paper](https://esajournals.onlinelibrary.wiley.com/doi/10.1890/13-1917.1): body size, diet and foraging-stratum trait framework; its dataset is not imported.
- [FAO dryland definition](https://www.fao.org/dryland-forestry/background/en) and [INRAE Oudin PET model](https://webgr.inrae.fr/eng/tools/hydrological-models/evapotranspiration-model): evaporative demand versus precipitation; quarter-mean application is our approximation.
- [US Fish and Wildlife wetland classification](https://www.fws.gov/policy-library/660fw2): distinguish water regimes and chemistry.
- [USGS closed-lake salinity](https://www.usgs.gov/publications/salinity-and-hydrology-closed-lakes): salts, throughflow and concentration concepts, not justification for our synthetic accumulation times.
- University of Michigan Animal Diversity Web: [lion](https://animaldiversity.org/accounts/Panthera_leo/) and [gray wolf](https://animaldiversity.org/accounts/Canis_lupus/) habitat/diet context. Older taxonomy in these accounts is not treated as a complete current taxonomic authority.

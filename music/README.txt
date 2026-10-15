WORLDGEN MUSIC

Drop MP3, OGG or WAV files directly into this folder. No rebuild is needed
for the desktop player; the folder is rescanned between songs (or every
10 seconds while silent). Nothing plays when no eligible tracks exist.

Filename format: songname x-y.ext
Examples: First Light 1-2.ogg, Bronze Roads 4-5.mp3, All Ages 1-10.wav
The inclusive range must use these numbers:

1  Planet creation (also main menu and world setup)
2  Prehistory
3  Neolithic
4  Bronze Age
5  Iron Age
6  Medieval
7  Early Modern
8  Industrial
9  Modern
10 Postmodern

During simulation the era is the highest established capability package
reached within one society, not a claim that everyone shares its technology.
A compatible playing track continues across an era change. An incompatible
track fades out over about 1.1 seconds; the replacement fades in over 0.8.
Entering a world fades menu/setup music and waits for Play before resuming.
Music volume is available in main-menu Options and Esc settings and is
remembered locally; it does not change the world or simulation.
The previous three tracks are excluded, relaxing the oldest exclusions
only when there are too few eligible songs. Selection is random and does
not use or alter simulation randomness. Broken audio files are skipped.

The browser build uses a staged copy of this folder and a generated track
catalog: run scripts/build.ps1 after adding browser music. Browsers may
require a click or keypress before sound can play.

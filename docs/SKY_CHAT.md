# Playable sky, calendar commands and local chat — 19 October 2026

## Controls

In gameplay, Enter opens an empty chat input; `/` opens it with `/` prefilled.
Enter submits. Escape closes chat first, without opening the pause menu. While
chat is open, movement, mouse-look, jump/fly and map/debug hotkeys are blocked;
the world clock continues. Up/Down recall input history, Tab completes command
names (or lists multiple matches), Ctrl+A/C/V select/copy/paste. Home/End and
Left/Right/Backspace/Delete edit input. Scroll reviews recent messages.

The unframed, monospaced bottom input and translucent message rows are inspired
by Minecraft's chat layout, without using Minecraft font/art assets. Closed
messages fade after10seconds; retained local history is capped at100 entries.
Input is capped at256characters and rendered as plain text, not rich-text markup.
Ordinary messages are local `<You>` echoes: no networking, NPC dialogue or chat
agent is implemented. Normal play remains HUD-free once messages fade.

## Commands

`/help` lists the three implemented command families alphabetically:

| Command | Action |
| --- | --- |
| `/date` or `/date get` | Read the playable calendar |
| `/date set 0042-06-21` | Change the playable date/season |
| `/date add 30` | Add30 calendar days; negative values work |
| `/help` | Alphabetical command help |
| `/time` or `/time get` | Read local solar time |
| `/time set noon` | Presets: day07:00, night19:00, noon12:00, midnight00:00, sunrise06:00, sunset18:00 |
| `/time set 21:30` | Set a precise24-hour time |
| `/time add 2.5` | Add solar hours; negative values wrap safely |

The playable calendar uses the simulator's existing365-day, non-leap year,
ordinary month lengths and a1-based year. Invalid commands return a red message
without mutating the clock. Date edits preserve solar time; time edits preserve
the historical event queue and pending credit. These are testing/presentation
commands, **not** a way to replay, rewind or skip civilizations' historical events.

`PlaySession.CalendarOffsetSeconds` is an optional saved offset from committed
history plus pending gameplay seconds. Old gameplay sidecars omit it and retain
zero offset. This does not change the binary world format, world/history hashes
or existing events. The same adjusted seasonal phase feeds sky and music.

## Sky

A shared engine-independent `PlayableSky` samples one sun's local east/up/north
direction from latitude, saved solar time and seasonal phase. Lighting and music
use this same solar altitude. Day length and calendar compression remain the
existing independently configurable game settings. The model is still a basic
local solar-time model: longitude-specific time zones, orbital ephemerides and
custom calendars are not implemented.

Unity uses a camera-specific URP skybox, graded zenith/horizon colors, directional
warm twilight haze, an antialiased approximately0.53° solar disk, a fixed seeded
5,800-star atlas, a restrained stellar background band and slow subtle twinkle.
The celestial frame rotates with time and latitude; stars fade through twilight
and are suppressed below the horizon. The scene's direct light, ambient tint,
fog horizon and ocean brightness follow daylight. No moon is drawn or used as a
light source; moonless nights retain a small gameplay visibility floor.
Existing scene directional lights are suspended during exploration and restored
on disposal, preventing URP from substituting a daytime light when the sun sets.

This is an economical analytic visual atmosphere, not spectral atmospheric
scattering or a weather simulation. It has no clouds, precipitation, aurora,
weather audio, footprints, dynamic ecology or other new atmosphere features.
Sky-body state is separate from atmospheric rendering; the disk renderer accepts
a bounded body array but currently receives exactly one sun. Future multiple
suns, moons, phases, eclipses and orbital providers require their own explicit
implementation; none is claimed by this interface alone.

The atlas is generated once per exploration scene with a named cosmetic PRNG;
there is no per-frame star generation, sky-world census, generation route or
historical RNG consumption. The sky material/atlas and chat input subscriptions
are released on scene disposal. The skybox is camera-specific, not installed
globally over the simulation map.

Local music file URLs now percent-encode the complete basename, including `+`
in multi-biome requirements. This fixes a Windows Unity file-loader404 without
renaming the owner's tracks or changing their eligibility.

Implementation references: [Unity skybox setup](https://docs.unity.com/en-us/engine/6000.5/manual/creating-environments/sky/skyboxes-using),
[Input System keyboard text events](https://docs.unity.cn/Packages/com.unity.inputsystem%401.3/api/UnityEngine.InputSystem.Keyboard.html).
No reference shader or game assets were copied. Validation evidence is in
VALIDATION.md; native sky images are retained only in ignored artifacts.

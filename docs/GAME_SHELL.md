# Game-first shell and save envelope — 19 October 2026

The main menu now leads to New game, Load game, Settings and Exit. New game
uses distinct World generation, Time & calendar, Scale & density and Sapients
tabs. The default World generation tab shows map type, random seed and cell count
(200,000); detailed controls remain closed under Advanced generation. A new game
rolls a fresh cryptographic seed. This is input selection, not a new authoritative
PRNG or generation path.
Generate uses the existing WorldPipeline. Start game first opens the simulation;
Enter world opens the playable scene. Escape there offers resume, simulation,
save, game settings, world settings and main menu. Returning to the simulation
leaves playback paused, never restoring an old fast-forward rate automatically.

## Saved game rules

`PlaySession` / `play-session-1` is separate from Recipe and World. Each library
save atomically publishes `play-session.json` alongside its existing world,
recipe, metadata and heightmap files. It stores settings, solar time, fractional
unpublished historical seconds, exact geographic player location, orientation,
walk/fly mode and height above the ground. Existing snapshots without the sidecar
retain scale 1, not the new-game scale 0.1. The binary world format, generation
version and historical rules are unchanged. Non-Unity clients have not acquired
this new-game UI; callers must pass a PlaySession to preserve its envelope when
creating an additional playable save.

| Setting | New-game default | Meaning |
| --- | --- | --- |
| Day length | 60 real minutes | One visual solar cycle |
| Calendar per day | 1 day | Historical time per solar cycle |
| Horizontal scale | 0.1 | Physical radius/distances, not elevations |
| Scattered density | 1 | Future untracked structures per playable area |

Day/Week/Month presets change calendar compression independently of daylight.
Month is exactly one twelfth of the simulator's year, so twelve one-hour days
span a year. Scattered density is **reserved**: no villages or structures are
implemented here. Its eventual count factor is scale squared times density;
recorded cities, historical populations and settlements must not be dropped.

During play, actual event-calendar work runs through the existing background
SimulationSession, with bounded publication slices. Fractional time survives
saves. Escape, the map and save dialog stop accumulating new time; work already
in flight may finish. Saved snapshots wait for publication to finish. No second
history algorithm, recurring territorial census or wall-clock-based historical
decision was introduced. Solar angle/lighting follows the visual day and seasonal
phase; this is not dynamic climate, weather, seasonal foliage or snow simulation.

## Controls and rendering

WASD/mouse, Space jump, Shift run and F walk/fly are retained. Normal play has no
text HUD. F3 toggles plain debug text without releasing mouse-look or stopping
movement. M independently opens the map, releasing the cursor: wheel zooms,
dragging pans/wraps and clicking teleports. Satellite is its default, with the
shared abiotic layers in a dropdown. Colors cache by layer; viewport pixels
rebuild asynchronously while the old image remains usable. View distance and
flight-speed controls live in Escape → Game settings.

Scale applies to tangent frames, LOD tiles, ground travel, scatter addresses and
physical horizon bounds; vertical metres and prop dimensions stay unscaled.
Large distant source queries subdivide into bounded patches. Applying a new
scale reloads the additive scene after the previous scene finishes unloading.
Local-terrain-preview3 retains physical local-noise/ridge wavelengths on scaled
worlds instead of shrinking them into steep needles. Macro height interpolation
and vertical amplitudes remain unchanged; scale1 retains the preceding sampler's
exact heights and default scatter placement. This is a derived local-version
change, not a worldgen/history change.

## Review limitations

The visible first walkthrough exposed extreme needle-like slopes at a polar
Earth spawn under scale0.1. The revised sampler separates local physical noise
wavelengths from macro compression. The focused mountain transect preserves its
base heights exactly and reduces residual gradient RMS to0.099 of the earlier
compressed pattern. This is not a general claim of local terrain realism; macro
slopes still become steeper with horizontal compression, and representative
landforms need further terrain-specific review.
Underwater walking still follows the seabed; swimming, prop collision, resolved
local hydrology, structures and NPCs remain deferred. Art remains placeholders.

See VALIDATION.md for actual native/managed evidence and outstanding checks.

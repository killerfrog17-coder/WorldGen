# Named saves and quicksaves

Manual Save game always offers a name. Within a playthrough, keeping that name
(case-insensitive) overwrites the corresponding manual slot. Changing it makes a
separate slot. Cancelling the name dialog keeps the old default name. Manual
saves from other playthroughs with the same display name remain separate.

F5 updates one quicksave per playthrough. It stores the active manual-slot ID
and name without overwriting that manual save or changing its default name.
F5 also works in world simulation; no autosave or F9 quickload was introduced.
Loading a quicksave through the existing library restores its manual association.
An early F5 before any manual save creates a playthrough and quicksave normally.

## Storage and compatibility

New slots reuse an immutable, validated planet in `world/.bases/<world-hash>.world`.
Each slot has a GUID directory and atomic `metadata.json` revision pointer. Its
`revisions/<revision-id>/` contains a small `snapshot.world` reference, compressed
binary `history.bin` if history exists, and `play-session.bin`. Current and
immediately previous revisions are retained; later overwrites prune older owned
revisions. Failed/incomplete staging directories are hidden from the browser.
The old published revision is selected until the new files are complete.

The planet is validated/written once, using fast compression (ordinary compression
fallback if needed to respect the existing world-file bound). Later saves avoid
serializing, compressing, revalidating and exporting a heightmap for that unchanged
planet. Dynamic history is validated using a cached geography context and written
per save; this is NOT a per-history-record incremental/delta archive. Historical
compression retains the ordinary level to support dense archives that would exceed
existing bounds with fast compression. Gameplay state uses fast compression and
binary primitive arrays, including lossless blood masks.

The obsolete 64KB gameplay JSON limit is removed. New binary state supports up to
256MB encoded; decoded bounds are 1GiB for gameplay state and 4GB for history.
State/schema validation remains. Old standalone world snapshots and old gameplay
JSON continue to load, including valid blood/inventory sessions above 64KB. Existing
saves are not bulk migrated or deleted. New-format loads are supported by the game,
CLI and terrain worker. Standalone WorldFile exports retain their existing format.
Back up the whole world library, including `.bases`, when moving new saves: an
individual slot now depends on its shared planet. Shared bases are retained when
slots go to the recoverable trash; no base garbage collector was added.

## Background play and crafting UI

Losing window focus does not suspend survival, crafting, fauna/NPC updates,
projectiles, loose-item physics or tool work. Background player movement has zero
input with gravity/fall damage continuing. Cursor and new-action input remain
focus-gated. Explicit pause/menu/map/inventory rules are retained. The player was
already configured to run in background; the additional per-system suspension
checks were the problem. The native checks exercise continuing ticks in the
background-capable batch player; a literal OS Alt-Tab owner trial remains useful.

Running handcraft progress appears at bottom centre while inventory is closed,
showing action, percent, remaining seconds and a progress bar. It hides when the
inventory's existing progress display is visible or work pauses. Disabled crafting
actions remain grey; their missing requirements appear only on hover, including
disabled-button hover. Visual placement/hover appearance remains owner-review
pending; visibility/progress logic was exercised natively.

## Evidence

The 69 focused storage checks include the original 43 world-library checks,
legacy full-world/large-session compatibility, player/animal blood and inventory,
planet reuse, exact history roundtrips, named replacement/new slots, linked
quicksaves, bounded revision retention and cancelled/invalid-write preservation.
The standalone old `--world-library-only` harness is blocked before it reaches
library tests by an unrelated pre-existing SkyChat help-alphabetical assertion;
the new focused entry invokes the unchanged library tests directly.

Packaged IL2CPP native19 checks pass without runtime errors: actual Save game
calls, same-name overwrite, renamed slot, F5 keyboard events, player/animal blood
cold reads, base timestamp unchanged, shared history validation, background craft/
survival/gravity, outside-inventory progress visibility and a real library quicksave
load back into gameplay including terrain-worker source loading.

Core 5,000-cell fixture: first save ~723ms, repeat ~14ms in the initial 69-check run.
Packaged bundled-world fixture: first save 22.2566s, repeat 0.2879s. These are specific
test timings, not guarantees for every world/history or the owner's 200,000-cell
save. First save still writes the planet. Cold loading still reads/validates it.
No full139 combat/ragdoll or13 shoreline rerun was required for this change.

Evidence: `artifacts/save-slots-20261028` and native
`artifacts/overnight-items-20261028/save-slots`. Original fauna/human assets,
animations, accepted textures, simulation rules and terrain data are preserved.

Background save completion also checks world/play-session identity before updating the current slot association, so leaving for another playthrough cannot rebind it. Native19 includes an actual asynchronous context-switch regression. Metadata is flushed before the atomic swap; revision cleanup only removes known payload filenames and rejects linked revision directories.

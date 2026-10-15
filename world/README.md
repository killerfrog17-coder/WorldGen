# World library

The desktop app lists saves here automatically when run from this project. Each save has its own identifier-named folder containing `metadata.json`, `snapshot.world`, `recipe.json`, and `heightmap.pgm`. Names are labels, never filesystem paths. Saving a new copy does not overwrite an older world.

`snapshot.world` retains exact mesh elevations, all calculated climate/hydrology fields, settings, and integrity hashes. The PGM is a portable preview/export (linear -5000 to6000 metres), not the authoritative terrain or a replacement for the snapshot. Future calculated fields can be retained in the snapshot without changing the library layout.

Earth is installed from the bundled full snapshot. Generated saves and staging directories are intentionally excluded from source control. A failed save may leave a recoverable `.pending-*` folder, which is not shown in the library. Copy the entire library when backing up. Old algorithm versions are not silently regenerated; unsupported snapshots show an error.

Standalone copies outside this project use `world` beside the executable, falling back to the application's writable data directory if necessary. The app shows the location and provides an Open Folder button. Browser saves use browser-local storage instead because websites cannot silently write to desktop folders; import/export remains available for backups.

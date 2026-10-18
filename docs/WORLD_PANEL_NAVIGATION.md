# Unified World panel - 18 October 2026

This is presentation-only. Historical and generation versions, saved histories,
and simulation outcomes are unchanged; old saves need no rules upgrade.

The simulation top bar has one **World [W]** entry rather than separate World,
Atlas and View buttons. Map settings are inside this panel. Existing contextual
selection cards remain in the bottom dock.

- W while closed opens the selected subject for the visible historical overlay,
  or the selected base-map information when that layer is the focus. Without a
  selected location it opens the world overview.
- W while open returns to the world overview. It is not a close command.
- Esc closes the panel, including from a focused search field, without also
  opening the pause menu. Typing W in a search field does not navigate.
- Each category opens its own titled Name/Details table. An empty category stays
  on its own page with an empty-state row, never the previous article.
- Search, historical-record filtering and bounded directory paging apply to the
  table as well as the sidebar. Rows open biographies/articles; Back returns to
  the prior page. Map settings retain the existing layer/opacity/grid controls.

`AtlasNavigation.Categories` supplies the shared category names. Directory/view
documents resolve before HistoryAtlas's no-history fallback, so the fresh-world
case needs neither fabricated cities nor an alternative simulation route.

Verification: the historical presentation suite passes852 assertions, including
all12 empty category pages and base-layer information. The browser and native
navigation checks are `scripts/atlas-navigation-check.mjs` and the native
`--worldgen-atlas-check <world>` diagnostic. Artifacts belong outside source
control under `D:/WorldGenBuildArtifacts/world-panel-review`.

Actual browser checks pass on the populated Earth continuation and fresh bundled
Earth (zero cultures/cities/states), including each category's real buttons,
selected-state W focus, W back to overview, typing/search, Esc, layer controls and
unchanged time. Empty-city and map-settings screenshots were opened and reviewed.
The final native IL2CPP check passes selected-subject/overview keyboard dispatch,
all category pages, an empty city table, settings, Esc with focused search and
unchanged full historical hash. The Release/staging build is clean; CLI repeat,
reapply, serialized-recipe and cached-result reconstruction hashes match.

Selected player: `artifacts/player-history-32-world-panel/WorldGen.exe`, IL2CPP,
built2026-10-18T19:05:25Z. The preceding notables35 player is retained as rollback.
No new historical realism or late-world performance acceptance is claimed.

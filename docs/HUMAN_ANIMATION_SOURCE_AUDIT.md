# Free human animation inventory

Current running revision (27 October): the selected runtime bank has one jog
(CMU 16_35) and a separate fast sprint (CMU 143_01), replacing the previous
run-style audition. KayKit runs and Quaternius jog/sprint remain rejected.
CMU has free-for-all-uses permission, **not CC0**; attribution ships in
`StreamingAssets/HUMAN-SOURCES.txt`. Inventory counts below describe the earlier
libraries, not the seven selected runtime movement roles. See
`HUMAN_PLAYER_PROTOTYPE.md` for URLs and reproduction.

Owner requires zero spending. Audited the actual public free downloads on
26 October 2026, without changing the Blender appearance review, any character,
or the game. No animation has been retargeted or appearance-approved here.

| Download | Named entries excluding T-poses | Ordinary human entries | Changing ordinary human clips |
| --- | ---: | ---: | ---: |
| Quaternius UAL1 Standard | 42 | 42 | 39 |
| Quaternius UAL2 Standard | 42 | 42 | 42 |
| KayKit Free 1.1, Medium rig | 131 | 117 | 115 |
| Total core inventory | 215 | 201 | 196 |

The five ordinary static poses are three pistol aiming poses in UAL1 and two
death end poses in KayKit. KayKit's 14-entry Special set consists of skeleton
motions/poses plus an experimental transform and is excluded from ordinary
people. Large rig has another 28 non-T-pose entries, mostly alternate takes of
existing capabilities; those are not added to the core total. FBX/glTF formats,
root-motion exports and repeated T-poses are not additional movements. Walks,
attacks and similar actions overlap between providers; 196 is a count of source
motion entries, not unique gameplay abilities or finished game-ready clips.

Quaternius source skeletons have finger joints (the reported 40 finger-related
names include end/leaf joints). KayKit source rigs have 23 joints and no finger
joints. KayKit hand poses must therefore be supplied separately for detailed
weapon/tool grips. Neither library is a dedicated first-person hand system.

## Coverage for this game

- Ground movement: walk/jog/sprint, crouch, sneak, crawl, jumps, dodges/roll,
  backward walking and some strafe runs. Full eight-direction coverage,
  consistent start/stop/turn sets and every armed locomotion variation are absent
  from these free inventories.
- Combat: punches, kick, multiple one/two-handed attacks, blocking and shields,
  bow draw/aim/release, generic throws, hit reactions and deaths. No explicit
  spear/polearm collection, wrestling/grappling, dismemberment or animal-specific
  hunting interactions. Firearm/spell clips are available but not a priority.
- Resource work: tree chopping, digging, pickaxing, hammering, sawing, generic
  work, planting, watering and harvesting. KayKit supplies seven named fishing
  clips. These are usable candidates, not confirmed exact prop contacts.
- NPC life/rest: idle, talking gestures, waving/cheering, chair/floor sit down,
  idle and get up, lie down/rest/get up, pickup/use item, generic Consume and
  chest-open. Detailed cooking, butchering/skinning, sewing, pottery, wound
  treatment, distinct meal/drink sequences, dialogue faces/lip sync and paired
  social interactions are not supplied as complete systems.
- Water/climbing: Quaternius has forward/idle swim and a one-metre climb-up.
  No full dive/tread/underwater/entry-exit set, ladder or free rock-climbing set.
- Travel: carrying walk, holding poses and a driving loop exist. No complete
  horse riding/mounting, rowing, cart operation or load-dependent carrying set.
- First person: adapt selected actions or obtain/author appropriate arm clips
  and grips later. These source files alone do not supply the first-person
  presentation, gameplay timing, collision or procedural placement systems.

Use Quaternius first for common movement; audition KayKit tool/interaction gaps.
Apply identical source clips to MPFB and UMA for a fair comparison. Keep the
fitted native character rigs/weights; animation mapping is not another neural
auto-rigging job. Real visual motion/contact quality remains untested.

## Provenance and reproduction

Actual downloaded free archives are CC0 according to their bundled licences and
publisher pages. No paid Source/Pro archive was requested.

- <https://quaternius.itch.io/universal-animation-library>
  public upload 17958403, Standard ZIP 15,904,933 bytes.
- <https://quaternius.itch.io/universal-animation-library-2>
  public upload 17958478, Standard ZIP 18,735,003 bytes.
- <https://kaylousberg.itch.io/kaykit-character-animations>
  public upload 15799903, Free 1.1 ZIP 14,858,957 bytes.

Ignored `artifacts/human-animation-sources/*-provenance.json` records archive
SHA256, original source URL, exact file list and zero cost. `inventory.json` /
`inventory.tsv` list every clip and exact GLB SHA. Reproduce with:

```powershell
& E:/AI/WorldGen3D/rig-venv/Scripts/python.exe scripts/audit-human-animation-sources.py
```

The script reads GLB animation arrays and checks finite data/changing tracks.
It does not render or claim natural gait. Temporary anonymous download URLs and
web responses should not enter source control; use the public publisher links.

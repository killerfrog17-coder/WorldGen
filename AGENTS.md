# WorldGen working instructions

These are the current repository instructions. Historical checkpoints are reference material in docs/history/agent-handoffs-20261111.txt and Git, not active instructions. Read history only when the current task needs it. Replace the current checkpoint rather than prepending logs.

## Delivery and scope

- Implement the requested player-visible outcome with the simplest adequate change. Reuse existing systems before adding abstractions, frameworks, caches, compatibility layers or diagnostics. Additional machinery needs a concrete current requirement or measured problem.
- Keep global history regional/aggregate; generate local bands, people and camps deterministically on demand. Do not turn broad history into per-tribe or per-person global simulation.
- Old-save/world/schema compatibility has zero priority unless explicitly requested. Current-version save/load must work.
- Define a small observable acceptance target before substantial work. Deliver a usable slice early. Extra available time is for authorized priorities, not invented edge cases or polishing unrelated systems.
- Answer questions directly. A status question is not permission to expand scope. If the owner asks to pause or finish the current feature, do not start another one.
- A requested playable update includes updating the normal root shortcut to the tested build and verifying its target, unless the owner explicitly asks for a candidate-only handoff or a current concrete blocker prevents selection. Historical protection notes must not indefinitely prevent delivery.

## Parallel work

- OWNER 11 Nov: use parallel subagents whenever independent work can shorten delivery. This supersedes all historical no-agents restrictions.
- Delegate bounded research, separate file ownership, implementation or focused review with a clear expected result. Keep the coordinator working on the critical path. Avoid duplicate audits, nested fan-out and agents for trivial tasks.
- One coordinator owns Unity imports/builds, build-slot selection, shared generated assets and performance measurements. Do not let agents overwrite each other's files or run competing GPU/build workloads. Freeze relevant runtime/Core inputs during a build.

## Proportional verification

- Choose checks for the changed behavior and actual risks. Reuse existing focused checks; add a regression test when it captures a meaningful bug or contract. Do not test trivial reversible edits, duplicate implementation details or create testing infrastructure without a demonstrated need.
- Inspect a small representative visual/gameplay case early, before exporting every variant or running a large sweep. Passing numerical checks is not proof of acceptable art or gameplay.
- After related changes, run affected integration checks once at the reviewable checkpoint. Broaden or repeat passing checks only after relevant changes, a failure, an invalid measurement or a concrete unresolved risk. No unrelated whole-game suite after every patch.
- For performance, first verify fixture camera/count/readiness/focus/settings. Compare representative travel/turning frame times, not just stationary averages. Do not measure FPS while building, running heavy CLI checks or competing with owner gaming; label shared-workload measurements honestly.
- Stop validation when the requested behavior and relevant checks pass and requested delivery artifacts are ready. Fix real failures; do not waive assertions, hide failures or weaken behavior to manufacture a pass. Test counts are not a quality claim.
- Use one concise screenshot PDF at final handoff for substantive visual/gameplay updates, unless the owner requests otherwise. Open it in a new Chrome tab on the leftmost monitor. Do not make a report for routine fixes or workflow-only changes.

## Files, assets and builds

- Canonical paths: assets/project-layout.json. Editable art/export inputs: assets-source/. Manifests/contracts: assets/. Runtime Unity: unity/WorldGen.Unity/Assets/. External runtime audio: sounds/ and music/. Installed tools: tools/. Finished reports: docs/reports/.
- artifacts/ is disposable scratch: logs, captures, previews, temporary tests and reproducible derived outputs only. Never put unique required assets, tools, contracts, presets or personal saves there. Required inputs must not depend on scratch junctions.
- Exactly two reusable player locations: builds/slot-a and builds/slot-b. No per-feature full player folders, daily executable archives or isolated player copies. Git plus immutable free GitHub Release asset packs provide recovery.
- Use scripts/unity-check.ps1 -BuildPlayer for the inactive slot; it runs the layout preflight. Never overwrite a selected or running slot. Ordinary iteration uses fast compilation and Mono NON-DEVELOPMENT players, without watermark. IL2CPP is only for a major playable release explicitly requested by the owner. Do not package after each tiny edit.
- Preserve Unity .meta GUIDs and approved source rigs/art/motions unless the current task authorizes changing them. Restore only known build-generated Unity autos, not owner changes. Commit source/necessary assets regularly; update asset locks when required inputs change. Do not repeatedly hash or download the entire asset inventory for unrelated code/docs edits.
- Do not close owner applications, alter AI preferences, remove installed model weights or personal worlds. No paid services or signups without authorization. Latest visible gameplay reviews and reports use the LEFTMOST monitor; Unity/compiler windows hidden or rightmost. Use scenic movement for visual reviews; rapid whips only for explicit stress tests.
- Preserve unrelated owner work, currently: deleted WorldGen_OneShot_Prompt.md; modified docs/reports/ai-settings-20261108/WorldGen - AI Settings Guide.layout.json; untracked scripts/deer-review-controls.py and scripts/prepare-deer-review.py. Leave untouched and unstaged.

## Current checkpoint (11 Nov 2026)

- Branch work/npc-ancestry-20261111. Runtime36fdb231; completed evidence/report2fe84c53. Slot B ScriptsOnly36fd over FULL e6d439ff, Mono NONDEV, built 2026-11-11T06:16:18Z. OWNER now requested normal shortcut delivery: root WorldGen.exe - Shortcut.lnk selects B. Slot A8388 remains intact as previous build; do not overwrite the now-selected B.
- NPC appearance: 24 inherited channels, 12 rendered anatomical axes, family resemblance and persistent peoples separate from politics; cultural grooming. Six source rigs retained; limb proportions stored only, short/full beards share available meshes. No creator UI.
- Core5810; actual world24 residents/8peoples/5species; native716/30profiles/120poses including3 cosmetic-hair health controls; embodiment/inventory/save19, clothing35, first-person86, combat/items184 PASS. These are existing evidence, not a requirement to rerun on every edit.
- 100-NPC close crowd:118.58/117.82FPS, p99~11.6ms, max22.31/29.06ms. Medium1080p/6km; Chrome/video shared PC. Strict100FPSfloor UNMET; no isolated improvement claim. See assets/verification/npc-appearance-20261111.json and docs/reports/npc-appearance-20261111/.
- Required external assets6576/6576 verified, release game-assets-20261111-v1. Final3-page PDF: docs/reports/WorldGen - Inherited NPC Appearance.pdf. Rivers source and evidence retained in branch ancestry; visual owner approval is not implied by passing tests. No own game/Editor/tests running at this checkpoint.
- Owner AI preferences hash unchanged: B9CAD371276C552AE499D818272577ACF90EFEA8DE2678EACF47126AA61DBED0. Unity Library/DataStore is a disposable cache junction to D:/WorldGenCaches/UnityDataStore.

## Explicitly blocked operations

Automatic approval review previously rejected these operations with reason "blocked by policy". Do not retry, bypass, broadly purge or relocate them as a deletion workaround:

- Deleting C:/WorldGenBuildArtifacts/recovery-check.
- Deleting old E:/WorldGen-builds camp-water generated debug targets.
- Relocating/deleting D:/WorldGenBuildArtifacts/world-feedback-active-cpp to C.
- Deleting artifacts/tools/trellis2-venv or its pieces.
- Deleting historical ~1.4GB profiler raw captures or the newer ~410MiB pair: artifacts/npc-20261103/crowd100-allocation-diagnostic-v1-morning/crowd-allocations.raw and artifacts/npc-20261103/crowd100-allocation-diagnostic-v2-morning/crowd-allocations.raw.

These files remain. Temporary dependency-testing renames must restore all original contents. Keep the required separate brief blocked-deletion disclosure in handoffs; do not describe blocked cleanup as completed. Historical cricket decode and wind/scent failures are not current passing evidence; do not alter unrelated behavior to hide them.

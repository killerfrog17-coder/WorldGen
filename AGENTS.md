# WorldGen repository instructions

## Owner requirement: compatibility

OWNER3Nov: backward compatibility has ZERO priority. Updates may break old
saves, generated worlds, schemas and earlier build data. Do not spend time on
legacy readers, migration paths, version-gated historical rules or old-version
replay checks unless the owner explicitly requests them. Prioritise current
behaviour and development speed; Git provides rollback. Saving/loading within
the current version still needs to work. This supersedes older compatibility
requirements and historical verification notes below.

## Owner requirements: filesystem and builds

The entire `artifacts/` folder must be disposable without affecting gameplay,
source assets, authoring tools, builds, approved presets, reference contracts,
or personal saves. Never place unique or required input data there. Scratch
outputs may be recreated by their producing command. No compatibility junction
inside scratch may be needed for permanent input.

Canonical locations are defined in `assets/project-layout.json`:
- `assets-source/`: editable art, raw motions and necessary exporter inputs.
- `assets/`: manifests, recipes, provenance and verification contracts.
- `unity/WorldGen.Unity/Assets/`: Unity runtime/imported game content and code.
- `sounds/` and `music/`: current externally loaded runtime audio.
- `tools/`: authoring applications and local tool installations.
- `builds/slot-a` and `builds/slot-b`: ONLY two reusable player locations.
- `docs/reports/`: finished reports and their source references.
- `artifacts/`: logs, captures, previews, tests and derived temporary files only.

NEVER create per-feature/per-update full player folders. Invoke
`scripts/unity-check.ps1 -BuildPlayer` to reuse the inactive fixed slot; it must
reject other names and overwriting the selected slot. OWNER1Nov BUILD POLICY
SUPERSEDES all older IL2CPP-only/never-Mono notes: use fast compiler/Editor
iteration and development players as appropriate. Only make IL2CPP players for
major playable updates when the owner explicitly requests one; the first planned
milestone is Alpha1.0.0 when the Paleolithic feature set is first complete. Do not
repeat native packaging after each small edit. The build script/editor builder
now default to managed NON-DEVELOPMENT players (no watermark). Use explicit
-DevelopmentPlayer only for requested diagnostics. Pass -Il2Cpp for an
owner-requested major native release.
Do not make isolated full player
copies. Retire superseded binaries after checks/selection. Git and immutable
GitHub Release asset packs provide history; do not keep daily binary archives.
Run `scripts/audit-project-layout.py` before builds. Source/project settings use
Git; large required inputs use `assets/game-assets.lock.json` and free immutable
release packs. No paid services, LFS charges, signup or subagents. Installed AI
weights and user worlds are not cleanup targets. Preserve Unity `.meta` GUIDs.

## Current owner work to preserve

ACTIVE3Nov03:53Z REGIONAL-2 PERFORMANCE WORK CONTINUES; no goal registered.
Owner requires local ordinary events, randomized regional dates, >=10kyr/s
ACTUAL interactive200k playback; layer switches<200ms and publication gaps<250ms.
304870c checkpoints independent local scheduler/COW/lazy hash/GPU region maps.
Current additional UNCOMMITTED CoreCLR executor uses shipped CLI worker +typed
binary regional deltas; code under RegionalPublication/RegionalWire/RegionalHistory.
No new model/second simulation; source numerical trajectory unchanged. Full200k/
190k20PASS at14,996yr/s batch, same8e9875...hash, exact initial/delta/future257yr
transfer and real current save/reload. events-scale/result.json; wire-compact-full.log.
Old Mono direct1700-2000yr/s /interactive1000 failed speed, maps6-10ms responsive.
First worker unbuffered tiny socket reads failed322yr/s; framed packet4823yr/s
and463msgap also FAILED. Do NOT claim either passes. Latest compact ecology
delta avoids resending immutable taxon arrays, new timing counters. Slot-a latest
managed nondev scripts-only built successfully; actual compact-player RUNNING
session89439, artifacts/regional-sim-20261103/coreclr-compact-player. Source/core
current exact full trajectory; new frame test result/visual review stillpending.
Root shortcut still rejected regional-1 slot-b. Never select until actual gate
passes. Further needed: actual performance fix if fails, source checkpoint/push,
current manifest/docs/shortcut and final actual verification. Recorder updated
to require>=10k actual and current20-check full history, not oldgameplay tests.
No unrelated gathering/crafting/corpse tests; no compatibility work. Owner3
untouched, no subagents/paid/cleanup/owner-app closures. All3Unityautosrestored.

SELECTED3Nov02:02Z REGIONAL PERFORMANCE REPAIR. Root shortcut NOW slot-b,
managed NON-DEVELOPMENT BuiltUtc2026-11-03T01:46:47.7910093Z. Runtime source
774854ba012131eeabff9d5e31cc9a2237edc23b PUSHED. foragers-regional-1 restores
BROAD regional history: at most ONE population aggregate/connected ecoregion,
100yr reviews, sparse migration merging totals, bounded cultural/family/discovery
profiles; local camps/people/seasonal positions are deterministic queries.
Fine clocks share frozen arrays and hash only the clock, no double world copies.
Species shares are separate from ancestry. OLD detailed-ledger saves UNSUPPORTED;
new world required, no backward-compatibility work. Owner rejects global tribes.
CURRENT200k/190k14PASS:2312regions/565aggregates548active,62.0869s batch;
1000 fine clocks11.68ms initially/16.97ms late. 200k10k15PASS includes split
publication equality and actual save/reload; Earth5000/10k15PASS. SAME packaged
200k playback8PASS/exit0:12.0005days/12.0021s; fast415.996years/s over12.52s,
P95frames10.34ms/max46.93ms. Batch speed is NOT interactive speed. Actual five
map images INSPECTED. Same player Earth nature32PASS/exit0/clean exceptions:
enter terrain/gather/craft/corpse loot/eat/held tools/save. Initial200k fixed-point
nature fixture failed deer spawn AFTER terrain entry; preserved/excluded, not
claimed passing. No unrelated fauna/gameplay edits made to hide that failure.
Manifest assets/verification/regional-simulation-20261103.json; exact packaged
assembly/worker hashes checked against stamp. New record script only current
tests; old compatibility suites NOT acceptance. PDF/report updated. Physical
NPC camp integration remains next stage; regional rates still game priors.
All owned heavy test/build/player jobs ended; owner3 files and apps untouched.
No subagents/paid/cleanup/extra player folders. Await owner playtest feedback.
Older21 and ACTIVE notes below are REJECTED/historical, not work to resume.

SELECTED3Nov00:56Z PALEOLITHIC REBUILD REVIEW: existing root shortcut NOW
builds/slot-a/WorldGen.exe, managed NON-DEVELOPMENT candidate21, BuiltUtc
2026-11-02T23:33:44.4930977Z. Production d89993dbd07fe947158b7b8a6d6262c5a07c2364
PUSHED/FROZEN. New worlds foragers-5; old2/3/4 retain their numerical rules.
Three genuine current-source histories COMPLETE: Continents190k hash453a89db59b0
bed61cd60c0e8c7d53ba0a2186d66a5a1775e8868e0618567260, Earth30k045b6b25...,
Archipelago30keda41990... . Nine coupled-world gates each; final253013684-byte
save33 lossless checks PASS. SAME actual player six map cases222 checks incl
40k/190k live playback and100m camp queries; Earth20k gameplay32 PASS/exit0.
Actual40k/Arch30k/100k/180k/190k maps and full-history plot INSPECTED. Final
packaged37yr continuation EXACT CoreCLR6bf25d5ba8ae49166b5c0ded1c9ae88372026a2
aabefecc6ae714fa4babc5c38. Old3+37 exact21b46...; old4+250 exact25d836... .
50mechanism/13temporal/485observer/69save/5large-transaction; weather9/habitat1679;
local samples1926Earth/2102Arch/2163Continents100k PASS. Proof1008frozen/
3254original/177fauna+audio EXACT. Final manifest assets/verification/
paleolithic-rebuild-20261102.json; report docs/PALEOLITHIC_REBUILD.md and2pagePDF.
IMPORTANT remaining calibration limits disclosed: this seed retains13.8%sapiens
ancestry, no established fitted/insulated clothing;39 techniques have makers.
Full190k fixture took73.5min/5000cells with concurrent checks, NOT60sec claim.
No physicalNPC camps/dialogue/newhomininart or moving terrain/vegetation yet.
Owner permits future regional Mesolithic/Neolithic overlap; never require global
Paleolithic mastery. No new eras implemented. Owner visual approval NOT inferred.
All owned histories/watchers/player jobs ended; no owner apps/owner3 files touched.
No paid/subagents/cleanup/new fullplayer slots. Await owner review; old ACTIVE
notes below are historical and do NOT mandate resuming completed overnight work.

ACTIVE2Nov23:55Z model5 d89993dbd07fe947158b7b8a6d6262c5a07c2364 PUSHED/FROZEN. Candidate21 slot-a built23:33:44.4930977Z NONDEVmanaged ScriptsOnly; proof-practice-21 PASS1008/3254original/177fauna+audioexact;workerSHAe1b28461e5691ae753fae8ebc87ca3a7bc4a4025e17586cd6e05aaa3fcc83a9f. 50mechanism/13temporal hash73947d70558cdb6cb80f037384f91c9d624d7b44ba24c28928567b134aa36833 PASS;485actualobserver; new3/4reads exact (4+250 reference RUNNING session65920; +1onlyreadprobe e80... notfullarithmeticproof). Main5history practice-continents PID19704/session38120 RUNNING~85k/190k; frozen practice-runner DO NOToverwrite. Earth30k045b6b2575db5cf724a77c452a56c340edd6b43674b251902b71f73d3efce7df COMPLETE; Arch30keda4199003b107c5b8c70087469bfa21c2664aa68c6e4698ba74d89fbce07898 COMPLETE.9broadgates each+1926/2102samples PASS. NewUse sharedproducts, makersvarybydemand; middlehistorymeanmakerShares12vsrejected4's41. Native21early session1400 ALLdone: fresh036/40k39+realplayback/Arch30k36/Earth20kgameplay32PASS, actual21mapsinspected. Native21late session63771 waits101k ->behavior/weather9/hab1679/sampling/native100k,181k/native180k,190kFULL+packed33/native190kplayback/37yractualCoreparity. Scratchscript run-native21-late.ps1 hardcodescurrentmain19704. No newfeatures/tweaksPLANNED; onlyfixsubstantivedefects. Owner23:48 givenETA60-90min, lastreplyvalidationnow. NEWOWNERERA guidance: allowregionalPaleo/Meso/Neooverlap; don'trequireall43/globalPaleo completion beforelatereras. Optionalglobalera ceilingfuture. No codefeaturechangeforthisclarification. 4runRETIRED113kmetrics/110ksafefile; NEVERcallcompleted190k. Source20UI/legacy3fullcontrolnot5acceptance. Source5ALLcodeFROZEN;pending recordscript/PDF/docs editsmodel5 notyetcommitted. Needmainfulloutcomes+finalnative/visuals/saveproof+manifest+shortcut+Git. MainrootshortcutstillREJECTEDslot-b. Owner3untouched,nopaid/subagents/cleanup/ownerapps. FirstSkilledYear/LastLossYear remainlegacy majority-share milestones, notfirst/lastindividualmaker; don'twronglydemandSkill>.5foradvancedmethodexistence; newhistorytechniques hasmakerCommunities/expectedMakers/meanMakerShare.

ACTIVE2Nov23:20Z NEW SUBSTANTIVE FAILURE FOUND AT4/100k: almost all groups near-perfect makers of all43 crafts (meanSkill41), despite diverse end use. DO NOT SELECT candidate20 or claim4finalacceptance. Owner informed earlierETAwithdrawn. Causes: knowledge+constant.12 autonomousprogress createsmakerswithoutteachers; unconstrainedtargetprevalence; localexpert feedbackpreservesunusedcrafts. Implement version5 preserving2/3/4: demand/material-ledtrainedprevalencetarget, NO autonomous proficiency from mere familiarity, expectedmakerstocks belowhalfperson retire, separate shared PRODUCT accessfrommakers. Use nowconsumer/technologyaccesscanexceedmakerprevalence; ONLY5relaxesUse<=Skillguard, stays0..1. ProductionCoverage=clamp(4*makerprevalence) explicitcoarseprior; appliedexpectedUse+WinterPractice/preparedpassages/frontier so fewmakerscanprovidegoods to others. Localteacheractivityweightedbypractice. Sampling5: goods canexistwithoutresidentlocalmaker, capabilitystillactualperson skills; older4 unchanged. Neednewmeaningfultests rareexpertteach/noexpertnoautolearn/unusedcraftloss/cold-warmmakercontrasts/sharedwintergear +old3/4hashparity. Stopowned4main21072 andlatewatcher1736 afterverifiedcommands;110kfileexists/metrics112k so110ksafesnapshot retained, annotate4requested190kINCOMPLETE/RETIRED (notpass). Newpractice-* full190k/30k30khistoriesaftercorrection +newcandidate21? Source20e8effd2 PUSHED numerical4f16; candidate20proofPASS1008/3254original/fauna+audioexact, early20all36/39/36 and32gameplayPASS,100k36PASS+weather9/hab1679/sampling2478. Native20 religionmap nowreviewedcorrect; maintain3Presentationfixes. No5editsyetatthisnote. Earlier current4no-newfeatures noteSUPERSEDED bydiagnosedfailure. Mainshortcutstilloldslot-b. No paid/subagents/cleanup/ownerapps; owner3untouched.

ACTIVE2Nov23:05Z production e8effd2 PUSHED; numerical4 f16c75f unchanged. Current slot-a candidate20 built23:03ish (readstamp), managed NONDEV ScriptsOnly; proof-specialist-20 RUNNING session76104, restored3Unityautos. Real belief screenshots17/18 exposed ancestral plurality hiding local branches +minority labels placed on wrong colours. Fixed PresentationONLY ForagerMaps/ForagerView/HistoryView: largest-community tradition, complete district shares, anchorlabels+retainedpop/area.485observer PASS inclcontrolled40%ancestral-plurality regression. Frozen17->20 exactly3Presentationfiles; Corehistorynumerics unchanged. Native20 earlysession86976: fresh0/40kplayback/Arch30k thenEarth20kgameplay32. Latepipeline session69620 waits101k forbehavior/weather9/habitat1679/sampling/native100k, then181k/native180k, thenfull190kcompletion ->behaviorfinal/packed33/native190kplayback/current37yractualruntimeparity20. Scriptsartifacts/.../run-native20-{early,late}.ps1 assertresultfiles notjustexit0; latewatcherhardcodesownedmainPID21072. Mainfrozen specialist-runner f16 historysession61577 stillRUNNING ~94k/190k; NEVERoverwrite DLLs. Earth30k +Arch30k COMPLETE, reviewed9gates each; samples2223/2395 PASS. Main60k9gates: cold fitted4.1xwarm, coldshelter24xwarm; 0->60k3ecologygates23708arrivals2511loss1595districts. Old3190k+37 exact21b46... bothnewCoreandactual17runtime PASS. Source20 oldstatements17->18 displayfixinsufficient arehistorical;20stillneedsactualvisualinspection. Stopown19latewatcher28924 and18late29808 verified/done; nootherownerappsclosed. Sourceproof20/finalpipeline/screenshots/full190koutcomes/recordscript/docs/shortcutselection/push remain. MainshortcutstillREJECTED slot-b. No paid/subagents/cleanup; owner3untouched. OwnerETA~1.5-2h at22:33Z, currentlymain~40mincompute, fullrunNOTcomplete. Goaltoolpausedfromrestartbutownerexplicitlyresumed; keepworkinguntilfinished, noindefinitemandate. Remainingno newfeatures unlessactualdefect.

ACTIVE2Nov22:30Z rules4 f16c75f PUSHED. Genuine rare-maker teaching fix +speaker-weighted registers; old2/3 numerical replay preserved.43mechanism/12temporal hash83dc65c0 PASS, old3190k+37 hash21b46a1b EXACT. Frozen specialist-runner DLLs now IN USE, do not overwrite. Fullnew4190k Continents RUNNING session61577 (~40k), Earth30k COMPLETE, Archipelago30k finishing session89485. All usef16c75f/detail5000; specialist-* paths/logs. Earth30k has5095fitted-skilled communities/9490needle-skilled vs old3zero,1819actual-use toolkits/1055varieties532families; cold/warm spread stillneeds lateroutcome review. Candidate17 slot-a BuiltUtc22:24:11.6798392Z NONDEVELOPMENT ScriptsOnly, proof-specialist-17 PASS1008/3254original/177fauna/audioexact; workerSHA2aa5961f... . Native17 Continents20k39PASS/liveplayback/imageinspection; fresh036PASS; Earth20k gameplay32PASS/exit0; 2043samplechecks20k; 69named-save +weather9/habitat1679 PASS.40k native queued session25059 (waits41000metric); freshsession28079 shouldcomplete. Source frozen; no shortcut selection. recordscript being revisedfor4/17; PDFupdated4, uncommitted docs/report edits. Remaining full190k outcomes/native late maps/actualparity/packed33/finalmanifest/shortcut/push. No paid/subagents/cleanup/ownerapps; owner3 untouched. Older3full190k is controlONLY, notcurrent4acceptance. ETA~2h fullhistory already communicated, no rigiddeadline.

ACTIVE2Nov22:17Z RESUMED. Version3 full Continents190k COMPLETE, hash0ed1be60a1e7f099e1456d1ce6fcdd46d68c790cf1b7e045ed382a6c8537c824, packed33PASS. However substantive outcome review found fitted/insulated clothing never established and rare makers' prevalence is incorrectly treated as their competence in teaching. Correct this with version-gated rules4; NO correction implemented yet at this note. Preserve old2/3 replay. Earlier ETA withdrawn; owner told30-60min focused correction plus~2h new full-history validation. Current16 old3 is control ONLY, not new-model acceptance. Native190k exited0 without result/playback proof (INCOMPLETE, not PASS);180k36PASS. Runtime parity16 PID30188/session46219 running, do not overwrite default testDLL before reference done. No selectedshortcut change. Production b326d2f pushed, docs8014a45 pushed; recordscript tiny edits uncommitted. All prior source/art preserved; owner3 untouched. No subagents/paid/cleanup/ownerapps.

ACTIVE2Nov21:31Z RESUMED verification: fullcurrent3 history PID7900/session18754
~157k, loglinear-continents-resumed.log; numericalsource41f4bd7. Remaining watcher
session92433 waits for completion, then behaviorfinal +packed33 +native190k.
Do notoverwritefrozenlinear-runner or startduplicatehistory. Candidate16 b326d2f
slot-a remainsfrozen/proved; rootshortcutstilloldslot-b. ALLlatest16early/middle/
Archipelago images nowreviewed;21rawmapimagesexact15->16. Fresh actualgeneration
36PASS/EXIT0/cleanlog, year0hash38762966..., capturesinspected; new3defaultcorrect.
PDF opened forowner; text now clearlyrequiresGenerateNewWorld (no oldsaveupgrade).
Finalrecordscript now alsoREQUIRES actual180kglacial map case36 +37yearactual
runtime/CoreCLR parity, in addition to existing190k/fullsave33. Runthese AFTER main
history/native190k tolimitmemory. Existing --worldgen-history-resume <190k.world>
--worldgen-history-years37 --worldgen-output <actual-hash.txt> selfquits; compare
Core test --forager-cost <190k.world>37 firstJSON Hash. Foldernative-runtime-parity-16
needsreference.log/player.log/exit-code.txt/actual-hash.txt. Native180k viewcase
view-resource-sharing-16-continents180k (no playback needed), visibleNormal.
Latestestimate60-90min from21:05Z; qualify iflateissues. No furthercodefeatures
beingadded; productionunchanged. Owner3 preserved, no subagents/paid/cleanup.

RESUMED2Nov21:05Z OWNER EXPLICITLY SAID CONTINUE. Pause revoked; finish genuine
remaining work andhandoff whenfinished, no old mandatory8h/untilwrap requirement.
130kcheckpointSHAexactf562f1... and actualread/validation9PASS, statehash308f68...
Now full190k current3 history RESUMED PID7900/session18754, loglinear-continents-
resumed.log, frozenlinear-runner41f4bd7 DO NOT overwrite. Sourcechain/earliermetrics
preserved by --forager-continue. Latestproductionb326d2f, checkpoint60cca95 pushed.
Candidate16 slot-a unchanged managed nondev18:33Z/proofPASS; selectedslot-b/root
shortcutstillrejectedbaseline. Latest16 100k language/tech/lineage/toolkit images
now actually inspected. Needrest of16images +full190k/audit/packed33/nativeview/
playback/lateoutcomes/finalmanifest/docs/shortcut/push. No owned Unity/nativejobs
onresume. Owner3filesunchanged,no paid/subagents/cleanup/ownerapps. Earlierpause
andactivejobPIDs below HISTORICAL. Finalrun stillINCOMPLETE, donotclaimfinished.

PAUSED2Nov18:38Z OWNER EXPLICITLY REQUESTED PAUSE FOR PC RESTART. Do NOT
resume until owner returns/resumes. Stopped only own watcherPID14616, history2332
andparent27028 after verifying commands; NO owned Unity/native/dotnet jobs remain.
New3full190k history INCOMPLETE, paused after130k checkpoint (203870579 bytes),
SHAf562f1a894a425f51598971292136f2b921aaa2384d7f8d99295d270b8f1dc8f.
First validate file on resume;120k fallback retained. Continue --forager-continue
<year-130000.world> <linear-continents>190000; preserve source/rules provenance.
Frozen linear-runner41f4bd7 or current numerical-equivalent b326d2f runner; NEVER
claim130k a completed190k run. Oldversion2teaching190k RETIRED comparison only.
Latest candidate16 slot-a built18:33:14Z b326d2f PUSHED, proof-camp-query-16 PASS.
Actual16 maps40k39/Archipelago30k36/Continents100k36 +Earth20k gameplay32 ALL
EXIT0/cleanlogs; actual100m nearby-camp check included. Current16 images still
need final actual inspection (15 reviewed). All original art/fauna/audio exact.
Remaining: full190k finish/audit, final huge save33checks +native190k/playback,
review late outcomes/maps, record-paleolithic-rebuild.py --production-source b326d2f
(defaultcandidate16), final docs/manifest/shortcut/push. Currentmainshortcutstill
REJECTED slot-b unchanged. Do not select prematurely.2pagePDF current/inspected,
not yet opened for owner. Recordscript/docs checkpoint pendingcommit at note time.
Owner3 files untouched; no cleanup/subagents/paid/ownerapps. All furtherworkPAUSED.

ACTIVE2Nov18:37Z OWNER BACK; no mandatory indefinite continuation. Finish actual
remaining verification then handoff. Production b326d2f PUSHED. Defaultforagers-3
numeric rules41f4bd7; legacy2 retained. New verified query defect: rounded dot/
acos distance has~200m floor; nearest-home pre-cap also unsafe. Query-only raw
atan2, actual conservative bounds, lightweight layout/current-position sorting.
FullEarth30band/656person export EXACT before/after; historical40k+50yr hash
133e644... EXACT vs frozen oldrunner. Earth1867/Continents100k2369/Archipelago1994
sample checks PASS, complete unpruned actual-band-position references. Initial
100m test and first partial fix FAILED; retained, not acceptance. Numerics unchanged.
Candidate16 slot-a managed NON-DEVELOPMENT ScriptsOnly built18:33:14Z; proof-
camp-query-16 PASS1008frozen/3254original/177fauna+audioexact, workerSHA430d709... .
Frozen15->16 exactly2 changedfiles (ForagerSampling +nativecheckfixture).
Native16 batchRUNNING session86693:40k39PASS/EXIT0 +realplayback, Archipelago30k
36PASS/EXIT0; current100k thenEarth20k gameplay. Earlier15 allcurrentmodelmaps/
32gameplayPASS, notlatestpackage. Existingrootshortcut/selectedslot-b UNCHANGED
rejectedbaseline. Need final190k/native/proof/manifest/selection/push beforehandoff.
Full new3 Continents190k RUNNING PID2332/session79442, frozenlinear-runnerDO NOT
overwrite,~129k18:37Z. Earth30k/Archipelago30k complete; 9broadoutcomegates each,
100k9 +actualweather9/habitat1679 PASS. Oldteaching190k RETIREDlegacycomparison;
oldEarth125kmetrics/120ksaved explicitly incomplete. No scriptedSapiensvictory.
Finalrecordscript scripts/record-paleolithic-rebuild.py refuses incomplete/stale
histories; candidate16/default sourceargument needed. Some docs/recordscript edits
uncommitted; production frozen. PhysicalNPC camps/dialogue/newhomininart, moving
vegetation/shorelineterrain NOTimplemented; highdetailhistory stillcostly, no60sclaim.
Owner3files untouched, no paid/subagents/cleanup/ownerapps. Oldtime notes superseded.

ACTIVE2Nov17:35Z OWNER BACK: finish genuine remaining work thenhandoff, no
mandatory indefinite continuation. New key diagnosis: concave(pop*diet)^.75
subsidises record splitting, artificially persistent smallgroups. Newworlds now
ForagerStateVersionforagers-3 linear/additiveclaims; legacy2retainsoldrules.
Currently UNCOMMITTED.38mechanism checksPASS (4partition/diet regression);
temporalnewrules result see session90970. Neednewlonghistory/sourcecommit/build15.
OldContinents190k COMPLETE but RETIRED LEGACY COMPARISON, notnew-modelacceptance.
OldEarthPID13412STOPPED at125kmetrics/120ksnapshot;run.jsonannotated INCOMPLETE.
No longhistoryjobs remain. optimized-runner nowunlockedfrozen2e33oldmodel;
useNEWsmalllinear-runner fornewmodel (notfullplayer). Needrunnewhistory promptly.
Candidate14 slot-a built17:31Z9a85faf: proof-planetary-14 PASS1008/3254original/
177fauna+audio exact +workerSHAd4c129...; newplanet-only transport33x2tests,
186077TCPexactgeometry,13datedmapcache,69save49legacyPASS. ItpredatesVersion3.
Selectedslot-b/rootshortcutstillunchangedrejected. CurrentCorefirstnewdefault3
sourceuncommitted; sampler06eb0da (multilingual+genuine specialists2176PASS) and
terrainworkerseparation9a85faf pushed. Neednewrules outcomes, latestnative
maps/playback +realGameplayNature onForagerworld, finalproof/shortcut/Git.
No paid/subagents/cleanup/ownerapps; owner3 untouched. Do notcalllegacy190k
acurrentmodelpass. Most historical notesbelow are superseded diagnostics.

ACTIVE2Nov17:30Z OWNER RETURNED. Latest owner explicitly revokes mandatory
until-wrap work: continue until genuinely finished, then handoff; no padding.
Goal stillactive. Latestpushed06eb0da sampled bilingual residents/actual trained
makers;2176sample checks120k pass,1.45MBactualexport(inspected),37proficientbow
makers/8bands vsoldweakaverage. CurrentUNCOMMITTED planet-only worker input +
datedlocalmapcache:33tests each10k/60k;69save49legacyPASS;186077exactTCPworker
geometry/scatter/indices/bounds/cancelsEarth40k;13actualmapcachechecksPASS. First
mapfixturefailedfixedadaptivepartitionassumption:0pixelchanges8->10tiles; old
failedlogretained. Current source compiled. Needcommit/build14/proof (new worker
SHA stamp+CLIfreeze), native finalmaps/playback and gameplayNature onforagerworld.
Slot-a stillcandidate13 87e1902 built16:49, native150k38PASS/EXIT0 proof1008/3254
original/177fauna/audioexact. Selectedslot-b/rootshortcutstillrejectedbaseline.
Two190k historiesINCOMPLETE: ContinentsPID17604/session50349 ~184k, EarthPID13412/
session85012 ~122k; frozen2e33 optimized-runner DO NOT overwrite. Logs*-optimized.
One full190k+final outcomes/native mapreview stillrequired; Earthpartialmustnot
be claimedcomplete. Currentquery/workerchanges don't alterhistoricaltrajectories.
Currentlanguagefamily/speciesbalanceoutcomesnotfullyvalidated; meaningfulregional
tech/ecology/populationchanges proven. Do not add scripted extinction/counts to
forceacceptance. Owner3 untouched,no paid/subagents/cleanup/owner apps.

ACTIVE2Nov17:10Z NO WRAP; KEEP WORKING. Latestpushed87e1902 journal/routes.
Internal slot-a candidate13 built16:49Z, proof-journal-13 PASS1008/3254/177+audio
exact. Native150k03=38PASS/EXIT0, UIto150050 exactd0187a35..., currentclimate6002,
imagesinspected. Selectedslot-b/shortcutUNCHANGED. New individual multilingual
knowledge + true craft specialists currentlyUNCOMMITTED:2176samplingPASS120k,
30bands687people190multilingual;37proficientbowmakersin8bands. ExplicitXYZ JSON
exportfixed priorrecursiveV.Unitfailure; failedlogretained. Sourceglobalhistory
numerics unchanged (queryproducts only). Needcommit/build14/proof/native.
Two190k runsstillINCOMPLETE, frozen2e33 optimized-runner locked: EarthPID13412/
session85012 ~114k; ContinentsPID17604/session50349 ~169k. Logs*-optimized,
NOToldcurrent. Keepgoinguntilownerwrap. Existingdocscontainmodel/runtime
limitsandunaccepted outcomes. Do not claim complete physicalNPCs/vegetation
reconstruction or full190k pass. Owner3 untouched; no paid/subagents/cleanup/apps.
Considering next worthwhile integrationcheck: terrainworker currently reloads
fullhistoricalarchives just forgeometry. Need investigate smallplanet-only read
withoutlosing datedlocalmap colours; not implementedyet.

ACTIVE2Nov16:47Z NO OWNER WRAP; continue. Source2298e25 query/equipment PUSHED.
Slot-a internal candidate12 built16:37Z managed nondev, proof-resident-queries-12
PASS1007/3254original/177fauna/audio exact. Native12 seed99173 original01 at30k
38PASS inclplayback/directreference, explicitEXIT0/cleanlog. Current source adds
UNCOMMITTED private journal/migration-route batching: exact10k250yrcc3dbfcb/
70k25yrad373830 hashes and12 temporalPASS. Large transaction test sessionpending
see artifacts/.../large-transactions-journal.log. Need currentcommit/build/proof.
Two190k histories stillINCOMPLETE, frozen2e33 optimized-runner locked: Earth
PID13412/session85012 ~105k, ContinentsPID17604/session50349 ~149k; logs*-optimized
not old*-current. Do not overwrite optimized-runner. Both preserve exact prefix
metrics/sourcechain, newest Earth100k03 ~322MB. Selected slot-b/rootshortcut
UNCHANGED. Current120k language audit82.6%singleton-family pop and low cross-
variety corecognacy are explicit review concerns, NOT validated realworldtargets.
43techniques regionaluse/climatevariety genuine; no forced racial extinction
or modernfamilycounts added. See new primary references in rebuilddoc. Full
190k outcomes/native late maps/final source/proof/shortcut still outstanding.
Owner3 untouched; no paid/subagents/cleanup/apps. Keep working after minimum.

ACTIVE2Nov16:35Z, NO OWNER WRAP. Eight-hour minimum passed; KEEP WORKING until
explicit owner wrap. Pushed2e33aa2 record transport03; internal slot-a built16:15
managed nondev/scripts-only, proof-record-transport-11 PASS1007frozen/3254original
assets/177fauna/audio exact. Native11 60k03=38PASS includingUIplayback;120k02=35PASS
clean logs/actual regional images inspected. Selected slot-b/shortcut UNCHANGED.
New district/contact query indexes + routine/winter equipment sample arrays are
currently UNCOMMITTED.1968sampling checks at120kPASS, exact exhaustive query
results; six queries69.47->30.07ms.34mechanism/12temporal/exact250yrcc3dbfcbPASS.
Two190k histories still activeINCOMPLETE, frozen2e33aa2 optimized-runner DO NOT
overwrite: EarthPID13412/session85012 resumed100k, ContinentsPID17604/session50349
resumed130k. Old304logs preserved, all prefix metric records exact; run.json
source chain retained. Earth100k now03~322MB vs old02~352MB. Original snapshots
remain forotherdates. Snapshot zero-year COST probe FAILED invalidseconds after
validread; not a pass.30k99173/Archipelago previous passed histories retained.
Need commit currentquery source/build/proof/native, full190k outcomes and family/
technology/climate reviews. No NPC spawn implementation yet; sampling only.
No subagents/paid/cleanup/owner apps. Owner3 unrelated files untouched.

ACTIVE2Nov16:25Z, OWNER HAS NOT REQUESTED WRAP. Continue meaningful work after
16:19:52Z minimum; NEVER stop automatically. Pushedf52344d exact allocation fixes.
Current uncommitted WGWRLD03/WGHIST03 lossless hot-record transport passes28checks
each10k/60k, full raw-bit state/future25yr/named-save equality; 69save/49legacyPASS.
Native03/package proof PENDING. Source generator/schema guard retained;01/02
readers preserved. Slot-a remains7ac internal managed candidate/native10 38PASS;
slot-b rejected selected and root shortcut unchanged. Two current190k runs304
STILL ACTIVE: EarthPID6320/session14800 ~90k; ContinentsPID28692/session61494
~122k. DO NOT overwrite current-runner while these run. Original logs+metrics
complete prefix retained. Current30k99173/Archipelago completed/audited/native35
each; all incomplete/old failed histories clearly recorded. Art/fauna/audio
unchanged. No subagents/paid/cleanup/owner apps. Owner3 unrelated files untouched.
Need current transport commit/build/proof/native, full late190k outcomes+map
review, then proper release source/proof/shortcut only when ready.

ACTIVE2Nov16:00Z, NO WRAP REQUEST. Owner says never stop until explicit wrap;
minimum16:19:52Z is not a stop. Latest pushed7ac7ab8; new stack-demography/retired
teaching filter/event lookup source verified but currently UNCOMMITTED. Large
70k125,383record25yr exactad373830... matches304; allocation1.962->1.480GB.
10k250yr cc3dbfcb exact,34mechanism/12temporalPASS. Need current commit/proof.
Slot-a internal7ac candidate built15:42Z managed nondev scripts-only, proof-
dated-camps-10 PASS1005frozen/3254originals/177fauna/audio exact. Native view-
dated-camps-10-continents60k38PASS including actual60000->60050 playback, direct
reference and current palettes, clean log/live image inspected. Native8/9 FAILED
strict clock/hash assertion:9 diagnostic shows sameDomain true, exactepoch2402,
~50dayclock-only map reuse, not stale demography. Keep failures, corrected test
matches existing domain contract. Prior304 native35 each Earth40k packed,
99173/Archipelago30k old format passed/images reviewed. Slot-b andshortcut FROZEN.
Full histories STILL RUNNING source3041073 (mechanically same teacher aeabd3):
current-runner DLLs locked by EarthPID6320/session14800, logteaching-earth-current
~80k; ContinentsPID28692/session61494, logteaching-continents-current~110k. Paths
teaching-earth/teaching-continents preserve0..prefix metrics, source chains inrun.
DO NOT overwrite current-runner. Third99173 +Archipelago30k finished/pass; native
105 scoped static checks previous package, not latest-model full-history proof.
Old674 pre-teaching baseline stopped~75k, no final pass. Earlier50bcc/2999 runs
FAILED256MB cap140k/120k. Source304 introduces WGWRLD02/WGHIST02 lossless vector
packing+1GB compressed/4GB decoded guards;13 exact numeric/fullworld/future25yr/
actualnamed-save tests and69 legacy slot checksPASS. Normal named history packing
wired, old01formats stillreadable/nonforagerwrites unchanged. Earth30k77->62MB.
Current largestEarth80k~231MiB, actor histories costly: copying/hashing/serialization
remain a serious performance limitation. No fast-gen claim. Potential next work:
pack hot cohort/culture transport to reduce DataContract object/field overhead;
NOT IMPLEMENTED at this note. Preserve all logical records/oldsave compatibility,
require raw-bit roundtrip +native proof if doing it. All3Unityautosrestored.
No paid/subagents/cleanup/ownerappsclosed. Owner3files untouched. Full190k outcomes,
late tech/language/refugia/ancestry review +final package/selection/Git stillneeded;
continue substantive work after minimum until EXPLICIT wrap, no final handoff.


ACTIVE2Nov14:45Z: Owner NO-STOP mandate remains; no wrap request. Minimum16:19:52Z
is NOT a stopping point. Latest e045fdd PUSHED; rare-teacher correction aeabd3e
separates expertise/access,34 mechanism/12 temporal PASS. Regional speech churn
fixed674d7eb (real community/cooldown); no contact-created native dialect crumbs.
Dead cohorts retain genealogy/final toolkit but clear incoming caches. Older
50bcc0a/2999fa3 long runs FAILED256MB save at140k/120k; do NOT call complete.
Fresh teacher-runner aeabd3e histories teaching-continents/Earth190k and99173
30k RUNNING (sessions72210/87448/51029); latest~30k/25k/20k, NOT final acceptance.
Optimized-runner e045fdd teaching-archipelago30k/seed61819/detail5000 just started
session64183. First invalid2500-detail invocation rejected, corrected5000.
674d7eb verified-continents190k remains PRE-TEACHING comparison session82794,
~60k; other verified-* stopped. No current long-history final-save proof yet.
Measured250yr10k fixture exactcc3dbfcb... hash before/after optimizations:
contact allocations519MB->194MB; demography161MB->33MB. Concurrent timings NOT
uncontended total-FPS evidence. New outcome audit exports real population/range
retreats, toolkit/habitat contrasts, near/far speech, ancestry and biota.
Slot-a NEW managed nondev e045 candidate built~14:44Z; proof-teaching-6 RUNNING
session87138, frozen1003/3254originals. Native view-teaching-6-20k PID14292 running
real global/atlas/three regional map captures, now also checks loaded default
Cultures. Prior674 player20nativePASS/clean/images inspected but UNSELECTED.
Selected slot-b/shortcut unchanged. Need current full histories/save/reload,
statistical and actual map review, final package/selection/Git; continue after
minimum until EXPLICIT owner wrap. All3known Unity autos restored. No paid,
subagents/cleanup/owner apps closed. Owner deleted prompt+2deer scripts untouched.


ACTIVE2Nov13:00Z (no wrap-up request): New current source adds dated habitat/map
projection, current ecology roster/colonists and native palette refresh;1679
controlled habitat checks PASS. Real internal slot-a observer2 (50bcc0a, managed
nondev12:12Z)13checks/clean logs, languages/technology HUD +atlas images inspected;
1000frozen/3254original/177fauna/audio proof exact. This candidate is UNSELECTED
and now behind latest source. Slot-b unchanged. New source ancestry exchange is
population-weighted/conservative;14affiliation/ancestry PASS. Complex teaching
uses speech/bilingual access; apprenticeships no longer span many generations;
boat/winter preparation uses skill rather than annual-use frequency.30mechanism
PASS. Full final temporal/long-run/native verification STILL REQUIRED. Three
older diagnostic histories running (check-runner50bcc0a190k/Earth50k, thermal-
runner2999fa3190k); these are NOT final-model acceptance. New frozen latest runner/
histories/build next. No cleanup, paid, subagents, owner app closures. Owner3files
remain untouched. Continue substantive work until EXPLICIT owner wrap-up; minimum
16:19:52Z is not a stop. Goal registered active for continuation.


ACTIVE2Nov12:10Z: Owner NO-STOP mandate still active (minimum16:19:52Z is NOT a
stop).631c9d4 pushed full resident speech/tradition shares and gradual regional
shift, explicit fictional sapient/learning traits, compatible mating contacts.
New household lifetime/parent/foster records and current observer/weather bridge
now pass1,152 sampling/9 temporal/9 weather/26 mechanism checks; NOT final proof.
250-year allocation optimizations preserve exact9e1f5096... hash. Latest20k audit
found nascent inventions losing skill before teachers establish; prototype maker/
recent-learning correction now in source, FRESH long histories required. Previous
50k/20k runs are diagnostic baselines, not current-model acceptance. Slot-b stays
frozen; slot-a early candidate still requires NEW build/clean native maps. No
selection, cleanup, paid calls, subagents or owner-app closures. Owner3files stay
untouched. See docs/PALEOLITHIC_REBUILD.md for source/evidence and caveats.


ACTIVE2Nov~10:50Z REBUILD CONTINUES, NO WRAP-UP REQUEST. Latest source checkpoint
adds43-technique alternative-path graph, five-year learning/saved teacher context,
demographic mass-balance/deep validation, connected lineage genealogies, sparse
biota/refugia, geography-dependent orbital wet corridors, coexisting cultural
communities, gradual beliefs/speech, stable spatial samples and new observer.
Slot-b selected stays FROZEN. Slot-a only EARLY internal managed candidate exists;
its first native view produced UI exceptions/obscured screenshots and is NOT
accepted. Fixture/atlas guard corrected in source, NEW build/view pending. Old
190k runs diagnosed several defects subsequently corrected; do not call those
final current-model proof. Short5k coexistence runs validate accounting; current
broader schema/interaction/performance checks pending. Owner deleted prompt and
2untracked deer scripts untouched. No paid/subagents/cleanup/owner app closures.
Minimum16:19:52Z is NOT a stop; continue substantive work until EXPLICIT wrap-up.

ACTIVE2Nov OVERNIGHT PALEOLITHIC REBUILD AUTHORIZED. Owner REJECTED fb4b6ac
simulation as homogeneous/unrealistic and requests substantive ground-up revision
of ALL relevant systems with primary research, behavioural/statistical tests and
actual multi-date/multi-world maps. Started08:19:52Z2Nov; at least8h work, minimum
review16:19:52Z, AND do not stop until owner EXPLICITLY asks to wrap. No idle
padding or cosmetic-only work. Preserve selected slot-b; only slot-a candidate.
No subagents, paid/signup, new player slots, owner-app closures or blocked cleanup.
Scope: ecology/resource/cold/ice limits, demographic shocks/retreat/recolonisation,
regional tech discovery/teaching/use/loss, recognisable divergent languages and
family histories, local belief traditions, diverse ancestral hominin ranges,
identity/kin/social networks; cheap coherent bands/camps/seasonal routes ready for
next NPC pass. Do not claim NPC actors/AI integrated unless actually implemented.
Requirements/clock: assets/verification/paleolithic-rebuild-session.json.
Owner3 files remain untouched. Keep Git checkpoints; report behaviour/evidence,
not a large consistency-check count as realism acceptance.


SELECTED2Nov PALEOLITHIC MAP/CALENDAR FEEDBACK COMPLETE: root shortcut NOW
builds/slot-b/WorldGen.exe, Mono NON-DEVELOPMENT BuiltUtc2026-11-02T07:52:23.5068390Z,
production2ef9911 pushed. Original Year0/calendar restored (no BC/BP),
Prehistory era now Paleolithic; default managed builds no development watermark.
Independent culture/language/technology/species colours, legends, selection cards
+existing atlas connected to actual data. Political layers EMPTY before real
institutions; unsupported religion/ethnicity layers are not culture aliases.
24-tech adoption bars +requirements visible. New worlds paleolithic2: bounded
continuing sound/lexical/rare grammar changes prevent old finite-merger convergence.
V1 worlds retain original language rules/hash. Optional saved HumanRanges:
separate procedural cold/warm archaic lineages seeded via real ecology/routes,
climate demography/shared food; coexistence without replacing sapiens records.
No new bodies/NPC tribes/dialogue/physical ice/vegetation replacement. Old saves
are not silently populated with unrecorded species. Core49 +interface334 +legacy
language267 PASS. SAME final visible player actual globe pick,50k/200k playback,
6 overlay captures, real technology HUD/atlas, no watermark, save PASS/exit0/clean
errors.152 cultures/152 speech varieties/3 separate species ranges in visual
fixture; mixed species verified via controlled demographic update, not claimed
in isolated-continent capture. Final packaged save/reload/37yr replay matches
CoreCLR e7aeee80eba33aaa... exactly. Older v1 save still81e85952...
978 frozen inputs/3254 original assets/30legacy/6x70 bank/fauna/audio EXACT.
Evidence artifacts/paleolithic-maps-20261102/visible-final,replay,final-proof;
manifest assets/verification/paleolithic-maps-20261102.json. Hidden-window HUD
captures black; excluded from visual acceptance. Earlier955c0dd sound convergence
found/fixed, not accepted. Both fixed slots only. All owned jobs ended,
owner apps/3files untouched, no paid/subagents/cleanup. Selected source/package
FROZEN. Await feedback; do not resume old overnight tasks automatically.

HISTORICAL PREVIOUS SELECTED FOUNDATION:
SELECTED2Nov PALEOLITHIC FOUNDATION: root shortcut NOW builds/slot-a/WorldGen.exe,
Mono DEVELOPMENT BuiltUtc2026-11-02T07:03:54.9195930Z, production1b219bd pushed.
New Unity worlds prehistory7/paleolithic1,~200ka established sapiens,24-tech DAG,
gradual local adoption/practice/speech, deterministic coherent band API/dialects,
glacial capacity/shelf migration and automatic gameplay weather. Original region
IDs/terrain/art/audio unchanged. Later-age society engines bypassed; existing
historical saves retain old rules.3-page PDF opened BEFORE implementation;
docs/reports/WorldGen - Paleolithic Simulation Plan.pdf, script tracked.
Rollback tag pre-paleolithic-overhaul-20261102 PUSHED. Core31PASS;267existing
language checksPASS;188300yr~4.49s on5000cell/496patch fixture,NOT fullworld/FPS.
Exact200000yr LEGACY continuation vs untouched old Core matches on Earth38130d20
and Continentsce6bbbbc. Broad old city-emergence assertion FAILS, predates task;
not claimed passing. Native UI/map3ebaa4d passes actual globe pick/50k+200k/
inspection/save; corrected colour images inspected. Since then ONLY2Corefiles
changed: per-band speech cohorts and smooth future climate join. Final SAME
selected package native read/save/reload/37yr replay PASS +CoreCLR parity exact
81e85952...; initial12vs37comparison wasinvalidfixture, retained.975inputs/
3254original/30legacy/6x70 bank/fauna/audio EXACT. Manifest assets/verification/
paleolithic-simulation-20261102.json; artifacts/paleolithic-20261102 evidence.
Current selected source/package FROZEN. No physical moving ice/ocean/vegetation,
real NPC tribes, finished dialogue or player-knowledge assignment yet. One
sapient species registered; no new models/speciation. Owner visual acceptance
not inferred. All owned build/test jobs ended; no owner app closures, paid,
subagents or extra player folders. Owner3files preserved. Await feedback.

HISTORICAL PRIOR SELECTED SHELTER BUILD:
SELECTED2Nov OWNER SHELTER REVISION: root shortcut builds/slot-b/WorldGen.exe,
Mono DEVELOPMENT BuiltUtc2026-11-02T05:18:51.2766531Z, production07075cd pushed.
12 minimal menu entries: material store/bone lodge/stone hut hidden, legacy IDs
retained. B opens; windbreak scroll adjusts height. No covering menu; supplied
material determines cover, creative copies held material without consuming it.
Crossed trestles/one hanging rail; cone band gaps fixed; new version2 A-frame
front folds share side panels, old v1 save layout retained. Sized sound bones
substitute for rigid posts/beams; bent ribs wood. Ornamental binding coils
removed, material requirements retained. Ordinary stone icons input alpha fixed.
Crouch upright torso/relaxed arms, anatomical crouched camera. Core13102 PASS;
preceding candidate 261 shelter/icon/crouch checks PASS/exit0; final player
focused upload PASS. Final delta only restores legacy v1 rack geometry; new
plan and human/runtime code unchanged. Do NOT claim261 on final package.
971 source inputs/3254 original assets/30 legacy/6x70 bank/fauna/audio exact.
Evidence artifacts/shelters-20261102/revision-check +compatible-proof. Manifest
assets/verification/paleolithic-shelters-20261102.json. Owner visual review pending;
no original animation assets changed. No NPC work, paid/subagents/extra players.
Selected package FROZEN. Preserve owner3 files; no dump cleanup retries.

HISTORICAL PRIOR REVIEW:
SELECTED2Nov PALEOLITHIC SHELTER REVIEW: existing root shortcut NOW
builds/slot-a/WorldGen.exe, Mono DEVELOPMENT BuiltUtc2026-11-02T04:25:23.4377564Z,
productionfa67c7d pushed. Owner's newer fast-iteration policy applies. Shared
15-design catalog /4covers, bounded dimensions/costs, actual donor-coat roof
patches and fitted working hides, stone/bone footings, elevated beds/cache,
storage/drying and original five save keys retained. Pebble/small/large naming
and separate wide glyphs; taller chipping glyph ONLY actual eligible stones.
Instant drink no longer stages/drops held items: no-bag spear+stone blade tested
through real freshwater/sea input and animation, exact inventory/hand identity.
Final SAME PACKAGE 257shelter/39water checks PASS/exit0/clean errors;
quick upload/main-thread cancellation and10terrain boundary checks PASS. All15
plan captures and mixed donor roof reviewed; final working-frame screenshot
reviewed after correcting normalized hide scale. Core13429blueprint/45stone/
82inventory/69camp/69save/24physiology PASS. Proof971frozen/3254original/30legacy/
6x70bank/fauna+originalaudio EXACT. Manifest assets/verification/paleolithic-
shelters-20261102.json. Source/selected package FROZEN; no owner visual approval
inferred. Surface-built architecture only: caves terrain; excavation/storage pits
and snow refuges deferred pending terrain/snow editing. NPC work awaits approval.
Mono thread guards exposed original MeshData allocate/release off-thread use:
main-thread preallocation + SynchronizationContext deferred release corrected.
Earlier failed checks1..8 excluded; later final results only above. No whole-world
FPS claim or all historical hunting suites passing. Two temporary terrain dump
deletions rejected by automatic approval review; left intact, do NOT retry.
Owner slot-b process5296 untouched; while running it protects old build slot.
No owned build/test jobs remain. Preserve owner prompt deletion and2untracked
deer scripts; no subagents/paid/extra full player folders. Await feedback.

CURRENT1Nov EXIT / THROW / NEEDS / FIRE SELECTED: normal root shortcut NOW
builds/slot-b/WorldGen.exe, IL2CPP Release BuiltUtc2026-11-02T01:21:11.3166520Z,
production1abf493 pushed. AltF4 explicit discard exits self immediately, no save
or Unity/native resource teardown; focused input only. Final native111ms/exit0,
single Alt/F4 do not exit, existing save sentinel unchanged. Own terrain worker
already has parent-death watchdog. Combat aim/collision-floor reads resident
height before analytic fallback, saved ground-offset datum retained. Original
item sounds prepared asynchronously during loading with explicit sample decode;
24 importer background flags changed, original WAV bytes/GUIDs unchanged.
Loading-time inactive disposable collider/body initialization removes first-use
cost from first release; creates no saved inventory/world item. Final instrumented
first wooden throw21.477ms, next stone/bone1.813/1.580ms, actual original swish
plays.50ms gate kept. Stamina ready/hold/release much lighter and mass-dependent;
normal full spear throw~1point,10s aimed throw<3.5. Bow/melee unchanged.
HUD food half after1comfortable solar day, thirst half at1L deficit(~10h baseline);
saved FoodNeedDays, proportional meal relief, old-save migration, low fill reserved
for severe deprivation. Intermittent need pulses; existing lethal needs unchanged.
Fires roughly2x fine-fuel/3x wood tending time, longer coals. Feeding dry tinder or
kindling to live coals automatically revives fire; shared placement/relight predicate
includes grass/leaves/reeds/bark/raw+prepared fibre/tinder. Wet/cold ash blocked.
136 SAME FINAL PLAYER native PASS/exit0/clean runtime errors:131 throw/HUD/fire,
5exit. Throw test normal hidden windowed D3D11(real audio), exit batch. HUD half/
critical images inspected. Native fuel fixture burns8h then grass->kindling->wood
and kindling-only coals revive via actual survival material transfer.581 scoped
Core PASS:82item/337HUD/24physiology/69save/69camp.957frozen/3254protected/30Legacy/
6x70banks exact; original fauna/audio data exact. Layout5655/5048locked PASS.
Manifest assets/verification/throw-exit-20261101.json; evidence artifacts/throw-exit-
20261101/native-throw-warm,native-quit-warm,package-proof-warm. Earlier fixture
factory mistake, readiness failure and824ms/89ms intermediate timings excluded
from final hitch acceptance. Old broad items suite failed uninjured deer cue at
0.6s: AI unchanged, NOT claimed passing. Prior cricket decode/Earth golden remain
outside this fix. No universal FPS/perception/audio listening quality claim.
Owner criticized90+min turnaround: avoidable test mistakes and repeated native
packaging cycles. Future fixes: use cheaper compiler/Editor checks, group edits,
package once when reviewable; keep IL2CPP ONLY. No further polish/testing this
task after final checks. Two slots only, artifacts disposable, no cleanup/player
copies. Owner apps and prompt deletion/2deer scripts untouched. All owned jobs
finished. Await feedback; no old overnight work resumption.

HISTORICAL1Nov CAMP BLUEPRINTS SELECTED: normal root shortcut THEN builds/slot-a/
WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T23:29:49.5781554Z, production
6c457f9 pushed. V opens five plans: lean-to, windbreak, drying rack, material
store, brush hut. First corner/end then size+confirm; hut centre/radius; R15deg,
ShiftR reverse, RMB back/cancel. E supplies actual suitable material and timed
component labour; ShiftE details. Partial work, materials, terrain footings and
stock save. Creative supplies components instantly. Finished weapons excluded
from construction wood; shortening needs cutting edge and returns real offcuts.
Shelter weather protection, solid two-sided collision, physical low-roof crouch,
usable hut doorway; dimensional stores feed Nearby crafting without carry mass.
Weather-driven rack drying/smoking preserves calories/source animal identity.
Dynamic fire/bedding retained, extra covering/layers and recoverable stone ring.
Normal free-form frame placement entry replaced; existing saved frames retained.
Longer branch resources (mostly short, max4.5m) preserve positions/counts/IDs/tree
proximity. First-pass procedural structure art, no new construction animations,
NPC camps, general spoilage or wildfire. Offscreen weather catch-up approximate.
160 SAME FINAL PLAYER native checks PASS/exit0/clean runtime error logs:62 plan
and98 camp. Actual mouse/keyboard sizing/rotation, survival labour/occupied hands,
partial/full save, hut entry, crouch/headroom, protection, stock/drying/dismantling,
fire/cooking/sleep/hammering. All5 completed structure renders inspected; diagnostic
avatar head hidden by retained FPS mask, not an art edit. Focused Core1788 plan/
3658 branch/33forage/39camp/82inventory/69save PASS. Branch fixture95.1% tree-derived.
Proof955frozen/3254protected/30Legacy/6x70banks exact; original fauna/audio exact.
Layout5655PASS/5048locked inputs. Manifest assets/verification/camp-blueprints-
20261101.json; docs/CAMP_SYSTEM.md. Final evidence artifacts/camp-blueprints-
20261101/native-final-verified,native-camp-verified,package-proof-verified.
Earlier flat-ground fixture/obstructing test rack/legacy feet-based aim failures
excluded. Last rebuild changes only calibrated-camera QA aiming, assertions kept.
Old cricket decode and standalone Earth golden discrepancy remain NOT passing;
no universal visual/FPS/audio regression claim. Two fixed build slots reused,
no player copies or cleanup, all artifacts disposable. Owner apps untouched;
owner prompt deletion and2 deer scripts preserved. All owned jobs finished.
No remaining task development; await feedback, no old overnight task resume.

HISTORICAL1Nov FRAME / PLAYER COMFORT SELECTED: normal root shortcut THEN
builds/slot-b/WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T21:29:46.1664161Z,
production337cc40 pushed. Wooden frames use two real contacts: driven forked
posts / post tops, ground feet, resting beams and timed lashed side joints.
Cordage/sinew/rawhide consumed by actual mass and saved in recoverable joints;
support/load graph evaluated after edits, unsupported wood falls using existing
item physics. No coverings, construction animations or engineering simulation.
Right-click wood > Position as frame wood; click two contacts; wheel rolls;
E frame menu lashes/unties. Raw fork branches retain Y; trimmed poles do not.
Round fork/twig geometry fixed for anisotropic wood scaling. Geographic frame
axes survive chart shifts and convert correctly when a piece falls.
Breathing now quiet at fresh exertion and smoothly rises with reserve depletion;
gain/filter smoothing replaces early heavy threshold, stamina itself unchanged.
Player idle/idle carry hold steady; FPS eye anchored to stable body origin rather
than animated gait/head, crouch/jump/terrain motion retained. NPC idle unchanged.
Separate single white flakeable-stone PNG (ordinary Git asset) replaces composite
marker; generated image/prompt in assets/item-art/flakeable-stone-pickup-v1.json.
56 SAME FINAL PLAYER native PASS/exit0/clean error logs:41 frame+comfort,15 icons.
293 focused Core checks PASS:25frame/41effort/39camp/37wood/69save/82inventory.
Frame/lashing and native GPU34px icon images inspected. Head is hidden in the
diagnostic frame capture because it retains the FPS mask. Final icon fixture
uses shared runtime texture/UV selection with offscreen GPU drawing: earlier
hidden-window GUI repaint attempts FAILED and are excluded. Intermediate builds
and proofs are diagnostic, final evidence artifacts/frame-building-20261101/
native-release,native-icons-release,package-proof-release. Proof952frozen/
3254protected/30Legacy/all6x70banks exact; original audio/fauna exact. Layout
audit5655PASS including5048 locked inputs. Manifest assets/verification/frame-
building-20261101.json. Earlier standalone Validate stage-hash discrepancy and
known cricket audio decode failure remain NOT passing/unresolved; no full
audio/world-hash regression claim. Owner apps untouched; all own jobs finished.
Only two permanent build slots; no full player copies or cleanup this task.
Owner prompt deletion/deer scripts preserved. Await feedback; no old task resume.

HISTORICAL1Nov ITEM TOUCHUPS SELECTED: normal root shortcut THEN builds/slot-a/
WorldGen.exe, IL2CPP Release BuiltUtc2026-11-01T06:41:04.5110214Z, source9f309e9
pushed. Compact3kg stones use one hand; two-hand support from8kg or middle
span>.24m / longest>.55m, independent of saved small/large label. Flaking marker
shows core + detached flake using the original white category atlas and actual
WorkpieceReason. Bee/fly buzz requires an active enabled actually drawn/faded-in
model; range1.5m/.9m, quieter inverse-distance level. No new source art/audio.
1050 scoped Core checks PASS (82inventory/38stone/69sound/77use/784craft), native
11PASS/exit0/clean error log. Actual icon screenshot inspected after GUI repaint;
earlier10-check image was captured early and is NOT visual proof. Package proof
950 frozen/3254 protected/30 Legacy files/all6x70 banks exact. Prior cricket
native decode failure remains known; do not claim full audio regression passed.
Manifest assets/verification/item-touchups-20261101.json. Owner slot-b process
was left running; do not overwrite any selected OR running slot. Only two fixed
slots, no player copies. All owned Unity/native jobs finished. Final manifest/
shortcut note needs closing push; owner prompt deletion remains untouched.


HISTORICAL31Oct: owner explicitly requested updating the actual launched game after the
previous candidate-only delivery. The normal root shortcut NOW selects
builds/slot-b/WorldGen.exe, native IL2CPP BuiltUtc2026-10-31T22:58:21.8587435Z,
GameAssembly SHA3d50a5c7b4398cc0935bf561cd5884e0093184c475faef122b33e0f80e0306e4.
The owner was informed of the existing cricket audio failure and then requested
actual activation; it remains a known issue, NOT a passing combat/audio suite.
Six appearance skins/textures exported under
assets-source/humans/appearance-runtime and rebound onto unchanged production
rigs in new Resources/HumanAppearance. Stable original character IDs/legacy
clip references retained; new library preferred with original library fallback.
No original art/rig/motion overwritten. Slot-b was selected for that release. At activation the owner still had slot-a process7748 open: leave
it untouched and do not rebuild slot-a until it exits. unity-check now protects
running slots as well as the selected slot. Editor images exist, but a fixture constructor
warning means its55-check run is diagnostic, NOT clean native release proof.
Native IL2CPP candidate built22:58:21Z in slot-b: appearance55, Kimodo86,
Legacy81 and shader39 checks PASS. Actual gameplay movement/item captures
inspected. Current Items156 checks PASS (prior deer cue failure did not recur).
Combat/audio STILL FAILS the native cricket decode check; do not claim it passed.
Both revised and original character libraries passed144 Editor carry-mask
checks: idle/walk/run arms stay unchanged, fingers grip, weapon actions apply.
Exact four carry runtime hashes match the native selected build. Their reviewed
carry-only hunks were adopted for the owner's explicit small-item fix request,
committed as24be330; no runtime source bytes needed changing or rebaking.
5048/5048 asset hashes verified; all prior4709 paths/hashes preserved. Five new
asset packs uploaded/download-verified in game-assets-human-appearance-20261031-v1.
Source checkpointdc290988 and lock1776c72 pushed. Current selection and known
limitations are in assets/verification/human-appearance-verification.json:
417 passing native checks including Items, plus144 Editor carry checks.

Owner deleted `WorldGen_OneShot_Prompt.md`; two untracked old deer scripts
`scripts/deer-review-controls.py` and `scripts/prepare-deer-review.py` must stay
untouched/unstaged. The previously prepared small-carry patches in
LocalTerrainFirstPersonArms, LocalTerrainNaturalHandling,
LocalTerrainSharedHumanActions and WorldGenSharedHumanMotion were reviewed and
committed as described above for the current request; preserve their behavior.
Original human art and animation sources remain preserved. The owner has now
authorized the revised appearance library, using unchanged production rigs.
Original approved human source SHA is
0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229.

Do not close owner applications. No automatic resumption of old Homo/history,
animal-generation or overnight tasks. The old broad hunting wind/scent suite
has a known historical failure; do not change AI or hide it to force a pass.

## Completed layout migration; historical gameplay candidate

Owner explicitly authorized the complete filing redesign31Oct, including moving
required inputs out of artifacts. Both existing player aliases moved to builds;
selected root shortcut THEN `builds/slot-a/WorldGen.exe`, physical D package
unchanged, GameAssembly SHA5bf6b4bf748bd526193a82e93ffe740b6b42f6b06ddb0d91ae306f178c692fc6.
The same previously tested native human release remains selected. Source fixes
e004624/runner6dba20c (night lighting, audio attenuation/dawn, melee and bleeding)
have an IL2CPP candidate in slot-b, BuiltUtc2026-10-31T21:35:47.1776397Z.
39 native shader/skin-scale/audio-settings checks PASS. Broader Combat/audio
and Items checks FAILED: cricket decode timeout and deer cue-before-flight
assertion. That older candidate was not selected. Do not claim those suites passed. No AI
changes were made to force the old perception assertion green. Pending gameplay
validation is separate from the completed filing redesign.

29686 authoring/metadata files24.888GiB relocated with byte preservation; 26
supporting mesh/material files followed. 331 Git-tracked inputs moved out of
scratch. References rebased; metadata hash references refreshed; approved model,
texture, motion-array and audio bytes unchanged. Ten active tool directories
and report dependencies moved to tools. Details in artifacts/layout-migration
are disposable evidence; canonical paths and immutable hashes must be stored
outside scratch. Additional11 model/material/world reference files and3590
audio/source-media files preserved, SHA checked. An old shared sound junction
was exposed by the final verification; all1088 runtime clips restored to a real
sounds/fauna/ai directory with their original locked hashes. No clips lost.

Asset delta game-assets-layout-20261031-v1 published:4 new remote packs uploaded
and downloaded/hash-verified; existing immutable packs reused. Final current
lock4709/4709 SHA matches. Source checkpoint86233d9 and final lock/proof85bd5e6
PUSHED and remote main verified. No tracked artifacts; owner four runtime diffs
remain separate/uncommitted, so do not claim the whole workspace remotely clean.
OLD artifacts was renamed/unavailable for the full IL2CPP build, original deer
and human scene opens, exact fresh deer mesh/motion export (12skin checks), tool
CPU imports and final lock checks. Both dependency checks passed;13 offline
restore checks passed. All original scratch restored; no temporary root remains.
Permanent proof assets/verification/project-layout-verification.json records
scope and known gameplay failures. Source art/motion/audio bytes unchanged.
Local optional studies/auditions are retained, not all are cloud-lock inputs.

TripoSG's old scratch package fallback removed; its compatible torchvision
package copied/read-verified into the installed tools environment (old blocked
environment preserved). CPU preflight passes without scratch or GPU inference.
The build preflight now rejects lock/source/build/tool dependencies in scratch,
including shared physical output-junction targets and Python .pth fallbacks.
All migration-owned Unity/Blender/native jobs finished. Slot-a remained unchanged
at migration completion; current owner-requested selection is recorded above.

Old artifacts cleanup removed17 obsolete player packages and10267 disposable
captures/geometry dumps. Earlier45GB figure was across drives, not an assertion
that artifacts was empty or a guarantee of Steam's selected-drive free space.
Keep space measurements tied to actual OS readings; unrelated changes occurred.

## Blocked operations ? do not retry or work around

Automatic approval review previously rejected these operations before execution,
with only the reason "blocked by policy":
- Deleting C:/WorldGenBuildArtifacts/recovery-check.
- Deleting old E:/WorldGen-builds camp-water generated debug targets.
- Relocating/deleting D:/WorldGenBuildArtifacts/world-feedback-active-cpp to C.
- Deleting artifacts/tools/trellis2-venv or its pieces.

These targets remain untouched. The obsolete TRELLIS environment is not a
required project input and stays as disposable historical tool cache in scratch.
Do not delete it as part of a broad purge or move it as a deletion workaround.
A temporary artifact-directory rename for dependency testing must preserve and
restore all original contents; it is not permission to delete blocked data.

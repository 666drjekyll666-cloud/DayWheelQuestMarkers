# Test Build Log

Every handed DLL is immutable and tied to exact committed source plus build artifact identity.

## 1.0.17 — accepted legacy stable baseline

- Frozen source: `CalendarQuestsPins-legacy-private`, `frozen/1.0.17`, commit `507fc6dd192993bf6290f7b6735718e3b98430a4`.
- CI run: `34499501221`; artifact `DayWheelQuestMarkers-1.0.17` (`10161273309`).
- Raw DLL: 55,296 bytes.
- SHA-256: `5dbfd4d4542978ebb14d0284f6ab82bad5be1593df9f097243e63af20d6b76d5`.
- Player result: accepted stable.

## 1.0.21 — accepted public stable

- Date accepted: 2026-09-11.
- Development branch: `dev/1.0.21`.
- Exact executable/build source: `7638343438dad6cdf522e37595f3fb21b442193a`.
- Candidate ref: `candidate/1.0.21` at the same commit.
- Accepted baseline ref: `baseline/1.0.21-accepted` at the same commit.
- Runtime logic base: accepted 1.0.17 reminder/actionability logic, with the public marker-resource implementation used by 1.0.21.
- Source audit: `QuestRuleCache.cs`, `CrossOwnerRuleCache.cs`, `SessionCacheRebinder.cs`, `LoadingCachePrewarmGate.cs`, `VerifiedBridgeReminderRules.cs`, and `ReflectionUtil.cs` are byte-identical to the accepted 1.0.17 blobs.
- CI: run `34639351706`, job `103394957694`, success, 0 warnings / 0 errors.
- Artifact: `DayWheelQuestMarkers-1.0.21` (`10278859840`), archive digest `sha256:046c957736a4d028fbe7cb12841797fe8f72673cf901d03e6fb611c6d3d80ac`.
- Raw DLL: 47,616 bytes.
- Raw DLL SHA-256: `b609da9c35cd40ce09259a4c580e371dad15c3889f4e5cf9bdb0190a00e23c9a`.
- Requested regression test: marker rendering/category correctness, menu/HUD lifecycle, multiple markers/categories if convenient, and no noticeable post-load hitch.
- Player result: **accepted**. User reported that everything works correctly.
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.21` loaded, a developed-save cache prewarm completed in 517.73 ms with `supported=75` and `cross-owner tasks=8`, and the mod reached its normal `Ready` state. One final-runtime revalidation fell back to the safe post-load rebuild path; the player reported no visible problem or noticeable regression.
- GitHub Release publication: workflow run `34640713142`, success.
- Release: `v1.0.21`, target commit `7638343438dad6cdf522e37595f3fb21b442193a`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.21.dll`, 47,616 bytes.
- Published asset digest: `sha256:b609da9c35cd40ce09259a4c580e371dad15c3889f4e5cf9bdb0190a00e23c9a`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.22 — tested, not accepted

- Date built: 2026-09-12.
- Development branch: `dev/1.0.22`.
- Exact executable/build source: `f8254f2af5112359c332f66848ad081cd754de91`.
- Candidate ref: `candidate/1.0.22` at the exact executable source above.
- Goal: cover statically verified required intermediate weekday-NPC progression stages that are not represented by direct task-completion anchors, and use live authoritative zone quality for graph-derived cached `GameRes` quality mirrors.
- Runtime changes: six verified intermediate manifest families; graph-derived unambiguous `Flow_SetPlayerParam <- Flow_GetQualityOfZone` requirement mirrors; all non-mirror requirements remain on authored `SmartRes` plus `Player.IsEnough`; no universal runtime provenance parser or recurring graph scan.
- Engineering evidence: `docs/INTERMEDIATE_PROGRESS_AUDIT.md`.
- CI: run `34679367015`, job `103514969551`, success.
- Artifact: `DayWheelQuestMarkers-1.0.22` (`10294066070`), archive digest `sha256:dff1f2e431f6f9431dd22eb45ed642e6b31f4f67c5a574e329739a0cd89ee58d`.
- Raw DLL: 53,760 bytes.
- Raw DLL SHA-256: `0821133c925898b9d52c83fec49c557d7199925a5da15e424fe20ed96e741834`.
- Requested test included the known Snake `snake_stars` / `@snake_help_done` live-quality case and naturally reachable intermediate chains.
- Player result: **not accepted**. The old Snake-quality state was no longer conveniently reproducible, but the current save exposed a stronger product-level false negative: after Snake opened the three portal-item conversations, the Inquisitor visibly offered `@inquisitor_magic_item` (Eternal Ember) and the player could select it, while Day Wheel Quest Markers emitted no Inquisitor reminder.
- Runtime log confirms 1.0.22 loaded normally, reached `Ready`, and prewarmed in 525.35 ms on one load and 507.82 ms on the later current save with `supported=75`, `cross-owner tasks=8`; no load-performance regression was reported from this test.
- Diagnosis: static universe evidence had already identified `@inquisitor_magic_item` as an authored supported self-consuming one-shot topic, but the previous progression-only policy rejected it because its Snake source path was taskless/relation-gated. That policy was too restrictive for the actual reminder product goal.
- Superseded direction: 1.0.23 broadens reminders to currently actionable authored one-shot weekday dialogue while preserving the existing task-linked rules and gate checks.
- Status: **tested / superseded / not accepted**. Do not merge to `main`, create `baseline/1.0.22-accepted`, or publish `v1.0.22`.

## 1.0.23 — accepted stable

- Date built: 2026-09-12.
- Date accepted: 2026-09-12.
- Development branch: `dev/1.0.23`.
- Exact executable/build source: `3da21541a38753387cf9c4d343559e5fb1181f34`.
- Candidate ref: `candidate/1.0.23` at the exact executable source above.
- Accepted baseline ref: `baseline/1.0.23-accepted` at the same exact executable source.
- Goal: broaden the reminder model from only task/progression-proven interactions to currently actionable authored one-shot weekday-NPC conversations, while still excluding repeatable utilities and submenu/container topics structurally.
- Runtime changes: new loading-time `OneShotDialogueRuleCache` over the six weekday-NPC graphs. A generic candidate must be an authored persisted `@` topic whose own route blacklists that exact topic, must currently be unlocked/not blacklisted, and must pass supported authored `Flow_Answer` price/lock gates through `Player.IsEnough`. Direct task-completion answers and the retained 1.0.22 bridge/intermediate answer IDs are excluded from the generic layer to avoid duplicate markers. No recurring graph traversal or universal provenance parser was added.
- Canonical product contract and verified blacklist semantics were updated in `AGENTS.md` and `docs/VERIFIED_RUNTIME_DATA.md`.
- CI: run `34680719489`, job `103518743292`, success; Release build and artifact upload both succeeded.
- Artifact: `DayWheelQuestMarkers-1.0.23` (`10293188076`), archive digest `sha256:4bad1ca06c4626df1b25dffbeb7c8326e7fd6328072d4b90d9efb0ab54d120c4`.
- Raw DLL: 65,536 bytes.
- Raw DLL SHA-256: `dc9b1f3494f46a903ec416679c08b9d6a3704f2132e82d8c9ac2cf21c06458b7`.
- Primary requested test: before consuming the already-visible Inquisitor `@inquisitor_magic_item` / Eternal Ember conversation, the Inquisitor weekday must show a base marker. The same Snake `@snake_items_give` transition also authored `@bishop_magic_item` and `@merchant_magic_item`, so Bishop and Merchant should likewise show reminders while those one-shot conversations are open/actionable. After consuming one of these topics, its contribution should disappear on the next refresh unless another independent actionable interaction still requires a marker on that weekday.
- False-positive controls: ordinary Trade / Leave / Back must not produce markers; a non-consuming submenu/header such as Snake's `@snake_about_nacklase` must not create a marker merely by being open. Existing item/relation/quality prerequisites must continue to suppress task-linked interactions until satisfied.
- Player result: **accepted**. Exactly three expected markers appeared for the three Snake-opened portal-item conversations: Inquisitor, Bishop and Merchant. After the player selected the Inquisitor `@inquisitor_magic_item` / Eternal Ember conversation, the Inquisitor marker disappeared as expected while the other two remained independently pending.
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.23` loaded normally and reached `Ready`. Loading-screen prewarm completed in **782.89 ms** with `supported=75`, `cross-owner tasks=8`, `one-shot topics=41`; steady-state summary reports `one-shot supported rules=43` and `one-shot unsupported rules=1` (fail closed). No Day Wheel Quest Markers error/warning appears in the supplied log.
- The same runtime log provides an additional false-positive control: before selection, Inquisitor offered `@inquisitor_magic_item` plus `Leave`; after consuming that one-shot topic, the game opened follow-up `@inquisitor_magic_100`, but that reply was explicitly unclickable (`_can_be_picked = False`). The mod's marker disappeared anyway, confirming that a newly visible but non-actionable follow-up does not keep the one-shot reminder alive.
- Performance comparison: this current 1.0.23 load is about 258-275 ms slower than the recorded 1.0.22 507.82-525.35 ms loads. The extra work remains confined to the loading-screen prewarm; user did not report a visible post-load hitch.
- Acceptance: user explicitly said `фиксируем` after the appearance and disappearance lifecycle test passed.
- GitHub Release publication: workflow run `34681625040`, success.
- Release: `v1.0.23`, target commit `3da21541a38753387cf9c4d343559e5fb1181f34`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.23.dll`, 65,536 bytes.
- Published asset digest: `sha256:dc9b1f3494f46a903ec416679c08b9d6a3704f2132e82d8c9ac2cf21c06458b7`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.24 — accepted unified-cache stable

- Date built: 2026-09-12.
- Date accepted: 2026-09-12.
- Development branch: `dev/1.0.24`.
- Exact executable/build source: `99d961abef528e14378c3dc8fd074a550b1138e9`.
- Runtime/source consolidation was complete by `513613f539b19b89d6ee03f3ac3331bbc0ca1d8d`; later commits before the build changed only engineering docs/workflow control, not production C# or the project file.
- Candidate ref: `candidate/1.0.24` at exact build source `99d961abef528e14378c3dc8fd074a550b1138e9`.
- Accepted baseline ref: `baseline/1.0.24-accepted` at the same exact build source.
- Goal: consolidate the accepted 1.0.23 owner-local, cross-owner and one-shot classifiers into one loading-time graph parser, and retire transitional hard-coded bridge/intermediate manifests without changing the canonical interaction-reminder rule.
- Runtime changes: `WeekdayInteractionRuleCache` parses each of the six weekday-NPC graphs once and derives owner-local completion rules, cross-owner completion rules and self-consuming one-shot topics from the same node/connection index. Direct completion answers remain excluded from the generic one-shot set. Owner-local live zone-quality mirrors are preserved; cross-owner and one-shot SmartRes semantics are not broadened. The unified cache directly owns session rebind/state validation.
- Removed production source: `QuestRuleCache`, `CrossOwnerRuleCache`, `OneShotDialogueRuleCache`, `SessionCacheRebinder`, `VerifiedBridgeReminderRules`, and `VerifiedIntermediateReminderRules`. Previously verified bridge/intermediate topic IDs are handled by their accepted exact self-consuming authored structure.
- Engineering evidence/design: `docs/UNIFIED_CACHE_REFACTOR_1.0.24.md`.
- CI: run `34682455605`, job `103523471693`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.24` (`10294740350`), archive digest `sha256:c05a205daaff183062522ed29e2b8aad7a295bf8a1fbc0945ba6d27c7c55c6cf`.
- Raw DLL: 43,008 bytes.
- Raw DLL SHA-256: `05aecb65054ba4890a7ffb043ead2fb4996512d911d63bb24a338e99c039971c`.
- Build workflow was returned to manual-only immediately after producing the candidate; that bookkeeping does not alter the frozen candidate bytes/source.
- Player regression result: **accepted**. The user loaded the pre-conversation save where all three portal-item reminders were still pending; all three markers appeared. After selecting Inquisitor `@inquisitor_magic_item` / Eternal Ember, the Inquisitor marker disappeared as expected.
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.24` loaded normally and reached `Ready`. Prewarm completed in **308.07 ms** with `owner supported=75`, `cross-owner tasks=8`, `one-shot topics=55`; steady-state summary reports owner supported=75, owner unsupported=6, cross-owner supported=6, cross-owner unsupported=0, one-shot supported=54, one-shot unsupported=1. No Day Wheel Quest Markers error/warning appears in the supplied log.
- Performance result: **308.07 ms** versus accepted 1.0.23's **782.89 ms**, a reduction of **474.82 ms / about 60.6%** for the loading-prewarm work on the developed regression save. The unified implementation is also substantially faster than the recorded 1.0.22 507.82-525.35 ms loads while covering the broader accepted one-shot behavior.
- Acceptance: user explicitly said `Фиксируем.` after the parity/lifecycle test and performance log review.
- GitHub Release publication: workflow run `34683059338`, job `103525126377`, success.
- Release: `v1.0.24`, target commit `99d961abef528e14378c3dc8fd074a550b1138e9`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.24.dll`, 43,008 bytes.
- Published asset digest: `sha256:05aecb65054ba4890a7ffb043ead2fb4996512d911d63bb24a338e99c039971c`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.26 — persistent-manifest performance candidate

- Date built: 2026-09-12.
- Development branch: `dev/1.0.26`.
- Exact executable/build source: `b9e698fc6eeeea46cf20e3b51eb1e737cb35b01a`.
- Candidate ref: `candidate/1.0.26` at the exact build source above.
- Goal: remove synchronous FlowCanvas structural parsing from gameplay, especially the measured fresh-game first-weekday-NPC hitch, while preserving the accepted 1.0.24 reminder/actionability semantics.
- Preceding evidence: fresh-game 1.0.25 testing measured a `302.22 ms` runtime structural rebuild at the first Bishop introduction. Later `known_npc` additions used cheap rebinding and did not require graph parsing, confirming that live membership can be separated from static structure. Source review also found the intended 1.0.25 prewarm gate had the wrong `game_starting` polarity.
- Runtime architecture: `PersistentRuleManifest` persists only pure structural rule data to `DayWheelQuestMarkers.rules.1.407.bin`, versioned for GK 1.407. The first cache miss bootstraps the accepted parser only in the verified loading-screen window (`game_started=false`, `game_starting=false`) and persists the result. Future launches deserialize the compact manifest and re-create only live SmartRes/WGO/player/KnownNpc bindings. Gameplay is not permitted to invoke the graph parser; if the manifest cannot be restored there, reminders fail closed for the affected session rather than paying a synchronous parse hitch.
- Integrity guard: bootstrap/persist is accepted only when structural counts match the accepted 1.0.24 universe exactly: owner 75 supported / 6 unsupported; 8 cross-owner tasks / 6 supported / 0 unsupported; 55 one-shot topics / 54 supported / 1 unsupported.
- Native marker sprites remain game-owned. Existing bounded loaded-Sprite lookup/caching is retained; no copied Graveyard Keeper pixel payloads were embedded.
- Engineering design/evidence: `docs/PERSISTENT_MANIFEST_1.0.26.md`.
- CI: run `34710799403`, job `103599117362`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.26` (`10303102216`), archive digest `sha256:6ab6e21a4e1da5bd76970a260fa5ff9e6ced984af8aaf7095efd404d4b612501`.
- Raw DLL: 56,320 bytes.
- Raw DLL SHA-256: `cc2a7deb7451318e2f6ad6f4751d27f05b2a631cc523a64c336cef594340605c`.
- Build workflow was returned to manual-only after the candidate build. Later workflow/test-history bookkeeping does not alter the frozen candidate runtime source or bytes.
- Requested test: two launches. First launch should bootstrap the persistent manifest behind the loading screen and create `DayWheelQuestMarkers.rules.1.407.bin`; the first Bishop/weekday-NPC introduction must not perform a runtime structural rebuild. After closing and relaunching, the log should report that the persistent manifest was loaded behind the loading screen and FlowCanvas graph parsing was skipped. Subsequent newly known NPCs should only trigger cheap manifest rebinding. Player should compare the formerly noticeable dialogue/start-game hitches and send the resulting log.
- Separate known issue: the user's report of excess Charmel markers is intentionally not addressed in this performance candidate and remains a later functional fix.
- Status: **candidate / awaiting player runtime test**. Do not merge to `main`, create an accepted baseline, or publish `v1.0.26` before acceptance.

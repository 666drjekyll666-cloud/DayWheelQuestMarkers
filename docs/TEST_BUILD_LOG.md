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
- Artifact: `DayWheelQuestMarkers-1.0.21` (`10278859840`), archive digest `sha256:046c957736a4d028fbe7cb12841797fe8f72673cf901d03e6fbf611c6d3d80ac`.
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
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.23` loaded normally and reached `Ready`. Loading-screen prewarm completed in **782.89 ms** with `supported=75`, `cross-owner tasks=8`, `one-shot topics=41`; steady-state cache summary reports `one-shot supported rules=43` and `one-shot unsupported rules=1` (fail closed). No Day Wheel Quest Markers error/warning appears in the supplied log.
- The same runtime log provides an additional false-positive control: before selection, Inquisitor offered `@inquisitor_magic_item` plus `Leave`; after consuming that one-shot topic, the game opened follow-up `@inquisitor_magic_100`, but that reply was explicitly unclickable (`_can_be_picked = False`). The mod's marker disappeared anyway, confirming that a newly visible but non-actionable follow-up does not keep the one-shot reminder alive.
- Performance comparison: this current 1.0.23 load is about 258-275 ms slower than the recorded 1.0.22 507.82-525.35 ms loads. The extra work remains confined to the loading-screen prewarm; user did not report a visible post-load hitch.
- Acceptance: user explicitly said `фиксируем` after the appearance and disappearance lifecycle test passed.
- GitHub Release publication: workflow run `34681625040`, success.
- Release: `v1.0.23`, target commit `3da21541a38753387cf9c4d343559e5fb1181f34`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.23.dll`, 65,536 bytes.
- Published asset digest: `sha256:dc9b1f3494f46a903ec416679c08b9d6a3704f2132e82d8c9ac2cf21c06458b7`, exactly matching the accepted DLL.
- Status: **stable / released**.

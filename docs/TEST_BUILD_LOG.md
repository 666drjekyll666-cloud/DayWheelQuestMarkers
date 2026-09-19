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
- Goal: cover statically verified required intermediate weekday-NPC progression stages that are not represented by direct task-completion anchors, and use live authoritative zone quality for graph-derived cached `GameRes` quality requirements.
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
- Removed production source: `QuestRuleCache`, `CrossOwnerRuleCache`, `OneShotDialogueRuleCache`, `SessionCacheRebinder`, `LoadingCachePrewarmGate`, `VerifiedBridgeReminderRules`, and `VerifiedIntermediateReminderRules`. Previously verified bridge/intermediate topic IDs are handled by their accepted exact self-consuming authored structure.
- Engineering evidence/design: `docs/UNIFIED_CACHE_REFACTOR_1.0.24.md`.
- CI: run `34682455605`, job `103523471693`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.24` (`10294740350`), archive digest `sha256:c05a205daaff183062522ed29e2b8aad7a295bf8a1fbc0945ba6d27c7c55c6cf`.
- Raw DLL: 43,008 bytes.
- Raw DLL SHA-256: `05aecb65054ba4890a7ffb043ead2fb4996512d911d63bb24a338e99c039971c`.
- Build workflow was returned to manual-only after candidate production; later workflow/docs bookkeeping does not alter the frozen candidate bytes/source.
- Player regression result: **accepted**. The user loaded the pre-conversation save where all three portal-item reminders were still pending; all three markers appeared. After selecting Inquisitor `@inquisitor_magic_item` / Eternal Ember, the Inquisitor marker disappeared as expected.
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.24` loaded normally and reached `Ready`. Prewarm completed in **308.07 ms** with `owner supported=75`, `cross-owner tasks=8`, `one-shot topics=55`; steady-state summary reports owner supported=75, owner unsupported=6, cross-owner supported=6, cross-owner unsupported=0, one-shot supported=54, one-shot unsupported=1. No Day Wheel Quest Markers error/warning appears in the supplied log.
- Performance result: **308.07 ms** versus accepted 1.0.23's **782.89 ms**, a reduction of **474.82 ms / about 60.6%** for the loading-prewarm work on the developed regression save. The unified implementation is also substantially faster than the recorded 1.0.22 507.82-525.35 ms loads while covering the broader accepted one-shot behavior.
- Acceptance: user explicitly said `Фиксируем.` after the parity/lifecycle test and performance log review.
- GitHub Release publication: workflow run `34683059338`, job `103525126377`, success.
- Release: `v1.0.24`, target commit `99d961abef528e14378c3dc8fd074a550b1138e9`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.24.dll`, 43,008 bytes.
- Published asset digest: `sha256:05aecb65054ba4890a7ffb043ead2fb4996512d911d63bb24a338e99c039971c`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.25 — first-NPC rebind candidate

- Date built: 2026-09-12.
- Development branch: `dev/1.0.25`.
- Exact executable/build source: `736b09179dc81b0587690cfda584a0fdf11a8cfd`.
- Candidate ref: `candidate/1.0.25` at the exact executable source above.
- Goal: decouple static graph-rule discovery from the current save's initially known NPC set so later NPC discoveries can use cheap runtime rebinding instead of reparsing all weekday graphs.
- CI: run `34708821462`, job `103593698227`, success.
- Artifact: `DayWheelQuestMarkers-1.0.25` (`10301969224`), archive digest `sha256:c63d837a0810dff1bfb1819437b528c860852311b542ab4e054d989473c086e9`.
- Raw DLL: 43,520 bytes.
- Raw DLL SHA-256: `c234d13ba00b0ce7a116b2ca62a4a976aaf390da25f6a05f4bf807f922d557f2`.
- Player result: first Bishop discovery still triggered a `302.22 ms` runtime structural rebuild. Later NPC discoveries only refreshed known-NPC bindings without graph parsing, confirming the rebind model itself worked.
- Diagnosis: the intended loading prewarm gate had the wrong `game_starting` polarity, so the graph-ready loading window was skipped on fresh saves.
- Status: **tested / superseded by 1.0.26 / not accepted**. Stable remains 1.0.24.

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
- Player result: **performance improvement confirmed, not accepted as final**. The first Bishop/weekday-NPC introduction no longer produced the previous ~302 ms runtime structural rebuild, and the user reported that the number of noticeable freezes dropped substantially (roughly from several per session segment to around one). A subsequent launch loaded the manifest behind the loading screen in 5.95 ms with the exact canonical counts; later new-NPC discoveries used cheap manifest rebinding only.
- Remaining issue: intermittent noticeable hitches still occurred. The adjacent `.bin` file was also judged poor user-facing placement, so the line was superseded rather than promoted.
- Separate known issue: excess Charmel markers remains intentionally outside this performance line.
- Status: **tested / superseded / not accepted**. Stable remains 1.0.24.

## 1.0.27 — BepInEx cache-location candidate

- Date built: 2026-09-12.
- Development branch: `dev/1.0.27`.
- Exact executable/build source: `f420ac2db01f75c55a0f71232768b8cfb083c107`.
- Candidate ref: `candidate/1.0.27` at the same exact source.
- Goal: retain the 1.0.26 persistent-manifest architecture while moving the generated cache out of the plugin directory into the standard BepInEx cache area.
- Runtime change: manifest path is `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`. No migration/import code exists by explicit user decision; an old adjacent 1.0.26 `.bin` is simply ignored and may be deleted manually.
- Quest/actionability semantics, manifest schema, parser bootstrap, marker rendering and gameplay refresh cadence are unchanged from 1.0.26.
- CI: run `34712258786`, job `103603023084`, success; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.27` (`10304050276`), archive digest `sha256:35197e85823d6cec338a1d324ea1c417af380bec3a0ba74fe9c97c34dec5c5e9`.
- Raw DLL: 56,320 bytes.
- Raw DLL SHA-256: `26646982e39a308e0e56b5e8638cc01c3257dbba5afad344544968b419d3019b`.
- Player result: cache placement **confirmed correct**. The manifest appears under `BepInEx/cache/DayWheelQuestMarkers/` and no new `.bin` appears beside the plugin DLL.
- Supplied short-run log confirms 1.0.27 loads the persisted manifest behind loading in 5.93 ms with canonical counts and reaches `Ready`; no runtime graph rebuild is present in the captured interval.
- Remaining performance issue: user still observes an approximately 0.5 s hitch roughly every 30–60 s. Source audit found recurring allocation pressure in our steady-state checks: the once-per-second known-NPC count check built a new dictionary; the 30-second signature check allocated/sorted a list and joined a new string; runtime validity also used reflective `MethodInfo.Invoke` with a new argument array every second. These are defects worth removing even though the supplied logs do not by themselves prove that all remaining stalls originate in Day Wheel.
- Status: **tested / superseded by 1.0.28 / not accepted**. Stable remains 1.0.24.

## 1.0.28 — allocation-free steady-state candidate

- Date built: 2026-09-12.
- Development branch: `dev/1.0.28`.
- Exact executable/build source: `8c6864f8fb93e16cc722fd22923f0d92337405a4`.
- Candidate ref: `candidate/1.0.28` at the same exact source.
- Goal: eliminate recurring allocation pressure in Day Wheel's normal one-second and 30-second validation paths without changing reminder semantics or the persistent-manifest schema.
- Runtime changes: the once-per-second known-NPC count check now reads `ICollection.Count` (or performs a direct non-allocating enumeration fallback) instead of constructing a dictionary; runtime validity compares the current live player to the already cached player without reflective `TryBindPlayer` invocation/argument-array allocation; the 30-second known-NPC set check now computes an allocation-free `ulong` fingerprint over existing NPC IDs instead of `List<string> -> Sort -> ToArray -> string.Join`. Actual bind/rebind still builds the small lookup dictionary only when membership really changes or a save is loaded.
- Existing `rules-1.407.bin` from 1.0.27 remains valid; no cache deletion/regeneration is required.
- Quest/actionability rules, FlowCanvas bootstrap policy, marker sprites/UI and cache location are unchanged.
- CI: run `34712967327`, job `103604965842`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.28` (`10303806667`), archive digest `sha256:0eece5c6e6ef06c817340f222d68459e7ce1c9d6042ff46c7ff0141e84bffb5a`.
- Raw DLL: 57,344 bytes.
- Raw DLL SHA-256: `fa3e64a5835cc30c730d1c083ae1531a2765f7531680340bbeda9a7ee0b14210`.
- Build workflow was restored to manual-only after candidate production; later workflow/docs bookkeeping does not alter the frozen candidate runtime source or bytes.
- Player result: **performance objective confirmed**. The previous roughly 30-second rhythmic freezes disappeared. The user still observed about two or three random short hitches over several minutes, but a control run with Day Wheel removed produced a comparable two or three random hitches over a similar interval.
- Supplied 1.0.28 log confirms the persistent manifest loaded behind the loading screen in **6.16 ms**, FlowCanvas graph parsing was skipped, no runtime structural rebuild occurred, and later NPC discoveries used cheap manifest rebinding only.
- Conclusion: the recurring Day Wheel allocation/GC-pressure defect is closed. Remaining sporadic hitches are at the measured game/modpack baseline and are not attributed to Day Wheel without new evidence.
- Separate known issue: excess Charmel markers remains intentionally untouched by 1.0.28.
- Status: **tested / performance line closed / superseded by 1.0.29 functional fix / not accepted separately**. Stable remains 1.0.24.

## 1.0.29 — Charmel nested-dialogue reachability candidate

- Date built: 2026-09-12.
- Development branch: `dev/1.0.29`.
- Exact executable/build source: `b5b09311bf60552ea411028c8d5068fc9609eeb4`.
- Candidate ref: `candidate/1.0.29` at the exact build source above.
- Goal: remove the two false Charmel/Lust-day markers observed while both visible top-level dialogue choices are locked.
- Evidence: runtime log shows `actress_2a_2` and `actress_2b` are both rendered but unpickable in the reported state. Static persisted-topic evidence identifies exactly two self-consuming nested topics, `@actress_2b_1a` and `@actress_2b_1b`, behind parent answer `actress_2b`; both have no own `AnswerData` gate and both blacklist `actress_2b` when consumed. The existing generic one-shot classifier therefore evaluated the children in isolation and missed parent-menu reachability.
- Runtime change: add a narrow verified supplemental reachability gate for those two exact Charmel child topics. They contribute reminders only while parent `actress_2b` is not blacklisted and the game's own `Player.IsEnough(SmartRes)` accepts the authored relation prerequisite `_rel >= 10` linked to `npc_actress`. All other topic/task logic is unchanged and unknown state fails closed.
- Persistent manifest schema/data are unchanged; the existing `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin` remains valid and must not be deleted/regenerated for this test.
- CI: run `34715252218`, job `103611226672`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.29` (`10304453035`), archive digest `sha256:2e9c4602373f9c3ddcb61c10ad17eef92e99d89f384c17f264110b75421c733c`.
- Raw DLL: 58,880 bytes.
- Raw DLL SHA-256: `0d447eaf442b7838584960ad6b0e2e80e54b81381dbc9fc67f36c716ea631a88`.
- Build workflow was restored to manual-only after candidate production; later workflow/docs bookkeeping does not alter the frozen candidate runtime source or bytes.
- Requested test: at the early Charmel state with 0/5 Faith and relation below 10, neither nested `actress_2b` child may produce a Lust-day marker. If convenient, after relation reaches 10 while the parent remains unconsumed, the two child one-shots may both legitimately produce reminders; after consuming either child, the parent is blacklisted and the sibling must no longer remain as a reachable reminder. Independent Charmel interactions may still contribute their own markers.
- Status: **superseded architecturally by 1.0.30 before player acceptance**. The tactical Charmel-only gate remains useful evidence, but the general nested-dialogue audit proved the same missing parent-reachability condition exists in task-linked Merchant routes. Stable remains 1.0.24.

## 1.0.30 — accepted generic nested-dialogue reachability stable

- Date built: 2026-09-12.
- Date accepted: 2026-09-13.
- Development branch: `dev/1.0.30`.
- Exact executable/build source: `a67355b2cca84954d8b0da06e91516212466969b`.
- Candidate ref: `candidate/1.0.30` at the exact build source above.
- Accepted baseline ref: `baseline/1.0.30-accepted` at the same exact build source.
- Goal: replace the 1.0.29 Charmel-specific guard with a general, loading-derived root-to-answer reachability contract shared by owner-local tasks, cross-owner tasks, and one-shot dialogue reminders.
- Evidence: `docs/NESTED_DIALOGUE_REACHABILITY_AUDIT.md` proves the model defect for Charmel `actress_2b -> @actress_2b_1a/@actress_2b_1b` and task-linked Merchant families `@merchant_business -> @merchant_marketing_done/@merchant_sales_done` plus `@merchant_2e -> @merchant_2e_1f`; Bishop `about_cathedral` is the verified unconditional-parent control.
- Runtime architecture: new `NavigationReachabilityCache` derives root-to-final-answer navigation paths only during loading/bootstrap. Each cached path stores only required ancestor phrase predicates and supported ancestor `AnswerData` price/lock gates; conditions within one path are AND, alternative authored paths are OR. Unconditional plain ancestors compile away. Unknown/unsupported ancestry fails closed. Normal gameplay evaluates only compact cached phrase/SmartRes predicates; there is no FlowCanvas/navigation graph traversal in gameplay.
- `VerifiedNestedDialogueGate` is removed from production. `WeekdayInteractionRuleCache` remains the established source of final reminder-bearing owner/cross/topic rules; 1.0.30 adds navigation reachability on top rather than replacing the accepted final-answer classifiers.
- Persistent manifest schema is 2 and persists the navigation contract under `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`. The existing format-version check simply rejects incompatible cache bytes and uses the normal loading-screen bootstrap; there is no field-by-field migration layer.
- Bootstrap includes fail-closed verified-contract validation for the known Charmel and Merchant parent chains and control cases before the manifest is accepted.
- Existing canonical final-rule integrity counts remain required: owner 75/6; cross-owner 8/6/0; one-shot 55/54/1.
- CI: run `34717995096`, job `103618570571`, success on `windows-latest`; Release build succeeded with **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.30` (`10305172405`), archive digest `sha256:cf7ecd4b5b6841ac85acb2de0daa34f81ce638845555cee8bf8560adb7ab616d`.
- Raw DLL: 76,288 bytes.
- Raw DLL SHA-256: `c08d84a601f923ac2b7a3b5a83ee07d4f56c4a2ef7ba54dffc6d1632fb59cef9`.
- First player launch: schema-2 bootstrap completed behind loading in **557.88 ms**. The mod reached `Loading manifest ready`/`Ready` with final-rule counts 75/6, 8/6/0, 55/54/1 and navigation counts **210 answers / 270 paths / 151 predicates / 0 unsupported paths**; no Day Wheel warning/error was present.
- Second full restart: the manifest loaded behind loading in **10.29 ms** and logged `FlowCanvas graph parse skipped`; the same canonical rule/navigation counts were restored and no runtime structural rebuild occurred.
- Functional result: **accepted**. In the originally reported Charmel state with relation below 10, the two false Lust-day markers are gone, confirming the generic parent-chain reachability behavior on the real game state.
- Performance result: the old roughly 30-second rhythmic Day Wheel hitch remains gone. The user observed about three sparse ~0.5 s hitches over roughly 15 minutes; the runtime log separately contains Unity `UnloadUnusedAssets` operations around 0.7 s with roughly 934k loaded objects, so these remaining stalls are not attributed to Day Wheel.
- Acceptance: user explicitly approved promotion to `main` on 2026-09-13.
- GitHub Release publication: workflow run `34720669798`, job `103625802463`, success.
- Release: `v1.0.30`, release ID `387712272`, target commit `a67355b2cca84954d8b0da06e91516212466969b`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.30.dll`, asset ID `560005580`, 76,288 bytes.
- Published asset digest: `sha256:c08d84a601f923ac2b7a3b5a83ee07d4f56c4a2ef7ba54dffc6d1632fb59cef9`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.32 — accepted verified completion-route stable

- Date built: 2026-09-13.
- Date accepted: 2026-09-13.
- Development branch: `dev/1.0.32`, from current stable `main` (`b113c5e34158747c2d174a1a30f993e3e12abd4a`; accepted executable ancestor 1.0.30 is `a67355b2cca84954d8b0da06e91516212466969b`).
- Exact executable/build source: `1c64cff7d9e15c97007b70fd5e4ed9b27d82c93f`.
- Candidate ref: `candidate/1.0.32` at that exact source. The numbered DLL is frozen to those bytes/source; later workflow/docs commits do not change it.
- Accepted baseline ref: `baseline/1.0.32-accepted` at the exact executable source above.
- Goal: cover the audited owner-local completion routes missed by accepted 1.0.30 when progression crosses `WaitForFlow`, exact CustomFunction boundaries, `Flow_FireEvent -> CustomEvent`, or a verified mandatory later interaction event.
- Implementation: `VerifiedCompletionReminderRules` reuses the accepted schema-2 one-shot `TopicRule` and `NavigationReachabilityCache` predicates for `@souls_s_s30_ask`, `@snake_give_key`, `@souls_s_s33_ask`, `@souls_s_s31_ask`, and `@bishop_get_citezen`; unsupported `@souls_s_s33_ask` remains fail-closed. Promoted topics are suppressed from the generic base-marker layer while their visible owner task is evaluated through the verified task mapping.
- `npc_cultist/snake_trap` uses verified answer `snake_stone_ready`, existing navigation reachability, and authored `GameRes:_rel >= 10` evaluated with game-owned `Player.IsEnough(SmartRes)`.
- `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back` are exact verified mandatory interaction-event task mappings only. There is no broad `CustomEvent` or `Visible task` classifier.
- Persistent manifest schema/parser/path/counts are unchanged from 1.0.30; existing `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin` remains valid. No gameplay graph parsing or cache regeneration is added.
- 1.0.31 remains a separate unaccepted allocation-pressure A/B candidate and is not part of this functional line.
- CI: run `34757773978`, job `103725042100`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.32` (`10317779076`), archive digest `sha256:4bfa1be47e4cb07542b82133401482d5625339818c7345c36d6fb91a9b560e21`.
- Raw DLL: 79,360 bytes; SHA-256 `0aecfa5178fcbbaef25bd195a9174a16074e0a73f0ca45cae872b781d50ef5c4`.
- Requested test: remove research probe 0.1.2, install 1.0.32, load the current save where `npc_inquisitor/inquisitor_talk` is Visible, verify a Wrath/Inquisitor marker before interaction, then interact and verify that this task contribution disappears after the mandatory scene unless another independent actionable Inquisitor interaction legitimately remains. Provide the resulting log.
- Regression controls: schema-2 manifest counts remain 75/6 owner, 8/6/0 cross-owner, 55/54/1 one-shot, navigation 210/270/151/0; no gameplay FlowCanvas parse; existing one-shot and nested-dialogue behavior unchanged.
- Player result: **accepted**. The Inquisitor/Wrath marker was present before the mandatory interaction. After talking to the Inquisitor, the automatic scene completed `npc_inquisitor/inquisitor_talk`; that marker contribution disappeared as intended. The scene then exposed the next Inquisitor work, and after the player satisfied one of those resource prerequisites by producing firewood, an Inquisitor marker appeared again for the newly actionable follow-up.
- Supplied runtime log confirms `Day Wheel Quest Markers 1.0.32` loaded normally, deserialized the existing schema-2 manifest in **10.80 ms**, logged `FlowCanvas graph parse skipped`, restored the exact canonical counts 75/6 owner, 8/6/0 cross-owner, 55/54/1 one-shot and 210/270/151/0 navigation, and reached `Ready`. The game log directly records `inquisitor_talk -> Complete` followed by `inquisitor_burn -> Visible`; no Day Wheel warning/error is present.
- Follow-on gate control: the completed mandatory-stage marker did not remain stuck on. A later marker appeared only after the player made the next Inquisitor interaction actionable by satisfying its resource condition, demonstrating that the verified 1.0.32 supplement hands subsequent stages back to the existing prerequisite-aware task system rather than broadly treating visible Inquisitor tasks as actionable.
- Acceptance: user explicitly said `фиксируем` on 2026-09-13 after the Inquisitor lifecycle and successor-prerequisite test passed.
- GitHub Release publication: workflow run `34759088450`, job `103728562417`, success.
- Release: `v1.0.32`, release ID `387904193`, target commit `1c64cff7d9e15c97007b70fd5e4ed9b27d82c93f`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.32.dll`, asset ID `561239167`, 79,360 bytes.
- Published asset digest: `sha256:0aecfa5178fcbbaef25bd195a9174a16074e0a73f0ca45cae872b781d50ef5c4`, exactly matching the accepted DLL.
- Status: **stable / released**.

## 1.0.35 — accepted generalized exact-self-consuming stable

- Date built: 2026-09-14 local / 2026-09-13 UTC.
- Date accepted: 2026-09-14 local.
- Development branch: `dev/1.0.35`, started directly from accepted 1.0.32 `main`; tactical 1.0.33/1.0.34 runtime lines are superseded rather than merged into this source.
- Exact executable/build source: `5e8305ea6c2515dd0694343a07eb70e403ad0528`.
- Candidate ref: `candidate/1.0.35` at the exact executable source above.
- Accepted baseline ref: `baseline/1.0.35-accepted` at the exact executable source above.
- Goal: replace separate hard-coded Astrologer/Snake non-`@` fixes with the structural rule proven by the complete six-weekday-NPC non-`@` answer audit.
- Research evidence: all six graphs contain 77 unique non-`@` answer IDs and exactly 19 exact-self-blacklisting candidates; among those 19 there are 0 reversible candidates and 0 utility-like `Leave`/`Back`/`Trade` candidates. Both known false negatives, Astrologer `astrologer_2a_1b_6c` and Snake `snake_1a`, are in this class.
- Runtime implementation: new loading-derived/persisted `NonAtSelfConsumingRuleCache`; candidates require selected answer X -> blacklist exact X, supported final AnswerData gates, and existing root-to-answer navigation reachability. Reverse tracing supports numbered WaitForFlow inputs and exact CustomFunction Call/Event UID jumps. Answers already owned by the accepted task-completion census are excluded from the generic supplement.
- Integrity guard: supplement bootstrap must reproduce the verified GK 1.407 universe 6 graphs / 77 unique non-`@` / 19 exact-self / 0 reversible / 0 utility; mismatch fails closed.
- Cache: `BepInEx/cache/DayWheelQuestMarkers/non-at-self-consuming-1.407.bin`. Existing schema-2 `rules-1.407.bin` remains valid and does not need deletion. No gameplay FlowCanvas traversal was introduced.
- CI: run `34784684310`, job `103797890060`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.35` (`10325673885`), archive digest `sha256:508b338e2b3f6296f0ee5aef89ce62c4cd4562fb873eb0a1ae2ed8eb979618bf`.
- Raw DLL: 98,816 bytes; SHA-256 `a7752d2058d4728db054adc049f650cad037cede41166b6c4c7329f4ea169879`.
- Requested test: remove the non-`@` research probe, install 1.0.35, keep existing `rules-1.407.bin`, load the preserved Snake state, verify a Snake marker while `snake_1a` / “Попытаться убедить” is available with 5 Faith, consume the interaction, and verify that its marker contribution disappears. Also watch for implausible extra weekday markers.
- Player result: **accepted**. Snake had the expected marker before the 5-Faith persuasion interaction and that contribution disappeared after the interaction completed. Ms. Charm simultaneously had a marker for her own 5-Faith-gated interaction; after the five Faith were spent on Snake, Charmel's marker disappeared because her authored gate was no longer satisfied. This confirms both exact self-consumption lifecycle and live SmartRes gate reevaluation.
- Acceptance: user explicitly said `Можно в мейн. Можно фиксировать релизить.` on 2026-09-14.
- GitHub Release publication: workflow run `34788469188`, success.
- Release: `v1.0.35`, release ID `388061761`, target exact runtime source `5e8305ea6c2515dd0694343a07eb70e403ad0528`.
- Published asset: `Day.Wheel.Quest.Markers.1.0.35.dll`, asset ID `562084252`, 98,816 bytes.
- Published asset digest: `sha256:a7752d2058d4728db054adc049f650cad037cede41166b6c4c7329f4ea169879`, exactly matching the accepted DLL.
- Follow-up architecture audit: stable behavior is retained; `research/unified-interaction-architecture` documents the evidence-backed recommendation to consolidate overlapping graph parsers/caches in a future candidate without reintroducing the rejected universal provenance parser.
- Status: **stable / released**.

## 1.1.0 — unified interaction candidate, runtime bootstrap failed

- Date built: 2026-09-14 local / 2026-09-13 UTC.
- Development branch: `dev/1.1.0`.
- Exact executable/build source: `67e693a63089d473920e5181eec7af75e6f1be75`.
- Candidate ref: `candidate/1.1.0` at the exact executable source above.
- Goal: consolidate the accepted separate non-`@` exact-self cache into the same schema-3 manifest/runtime TopicRule path as persisted `@` one-shots, without broadening task/event semantics.
- CI: run `34789944561`, job `103812189255`, success; Release build 0 warnings / 0 errors.
- Artifact: `DayWheelQuestMarkers-1.1.0` (`10328230822`).
- Raw DLL: 92,672 bytes; SHA-256 `29d6118fdd1c981c947836181c486fbac52caa39c72b6cce9d3694af85401ab9`.
- Player result: **not accepted / bootstrap regression**. On both supplied launches the schema-2 -> schema-3 rebuild failed before a usable manifest was persisted: `navigation bootstrap failed: no interaction-root navigation path for required answer npc_cultist / snake_back_12a`. Gameplay correctly failed closed, so all Day Wheel markers were absent, including the currently available Astrologer diary hand-in.
- Root cause: the unified compiler injected every admitted non-`@` exact-self candidate into production `Topics` before navigation required-answer validation. `snake_back_12a` is exact-self-blacklisting but is not independently reachable from the interaction root; accepted 1.0.35 would simply reject such a candidate at runtime navigation evaluation. 1.1.0 incorrectly elevated that candidate-level fail-closed result into a global manifest-bootstrap failure.
- Additional player report carried forward: on stable 1.0.35 the current pre-diary Astrologer state displayed three markers even though the player observed only the diary hand-in as a meaningful interaction. The supplied 1.1.0 log confirms the Astrologer menu contains `@astrologer_diary` plus a rendered `@refugees_s_ev_7_1_1`, trade and leave; the exact source of the three 1.0.35 marker contributions remains unproven and must not be guessed.
- Superseded by 1.1.1. Stable remains 1.0.35.

## 1.1.1 — root-aware unified interaction candidate

- Date built: 2026-09-19 local / 2026-09-18 UTC.
- Development branch: `dev/1.1.1`, started from current stable `main` 1.0.35 and reapplied the reviewed unified architecture with the 1.1.0 runtime finding corrected.
- Exact executable/build source: `6ec1077560311e608fde8667a99df82fbca6112d`.
- Candidate ref: `candidate/1.1.1` at the exact executable/build source above.
- Goal: preserve the 1.1 unified exact-self representation while restoring the accepted product contract that root-unreachable exact-self answers fail closed individually rather than invalidating the whole manifest.
- Runtime change: build and validate the accepted interaction-root navigation index first, then compile non-`@` exact-self candidates. A candidate is admitted to production `Topics` only if `NavigationReachabilityCache.HasInteractionRootPath(npcId, answerId)` is true. Completion-owned candidates remain separately excluded. The 19-candidate integrity census now partitions into admitted + completion-excluded + navigation-excluded; it still requires 6 graphs / 77 unique non-`@` IDs / 19 exact-self / 0 reversible / 0 utility.
- Manifest remains schema 3 and keeps the same binary layout; navigation-excluded count is reconstructible as `ExactSelf - CompletionExcluded - AdmittedTopics`. No gameplay graph traversal is added.
- CI: run `35401247903`, job `105781395380`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.1.1` (`10570606342`), archive digest `sha256:0afb46fa81d72de81b659a3c037a208d717c31fe8e8f8cdc21d9ae12fd79f541`.
- Raw DLL: **92,672 bytes**; SHA-256 `f6b55b2b172675af07a890034ccf160350e1a90966a96ff5948d64f71d9e94f6`. Local extraction/hash matches CI.
- Workflow was returned to manual-only immediately after the candidate build; later docs/workflow bookkeeping does not change the frozen candidate source or bytes.
- Requested test: install 1.1.1 over 1.1.0 without deleting either cache file; load the preserved pre-diary Astrologer save. First prove schema-3 bootstrap succeeds without the `snake_back_12a` failure, then report the Astrologer marker count before consuming `@astrologer_diary`. If the count is not exactly one, preserve the state and return the log for a narrow contributor diagnostic. If the first launch is sane, fully restart once and verify schema-3 loads with FlowCanvas graph parsing skipped and the same marker state.
- Player result: initial schema-3 bootstrap fix passed, but the preserved Astrologer pre-diary state still showed three markers; **not accepted**. See the runtime-result update below.
- Status: **superseded by the diagnostic/fix line / not accepted**.


### 1.1.1 runtime result update — Astrologer contributor defect remains

- Player first-launch result: schema-3 bootstrap **succeeded**; the previous 1.1.0 fatal `npc_cultist / snake_back_12a` bootstrap regression is closed.
- Runtime counts from the supplied log: owner 75/6; cross 8/6/0; unified self-consuming 63/62/1; non-`@` universe 77; exact-self 19; admitted 8; completion-excluded 9; navigation 210/270/151/0. Therefore the remaining two exact-self candidates are root-unreachable and were excluded rather than aborting bootstrap.
- Astrologer still displayed **three markers** in the preserved pre-diary state, so 1.1.1 is **not accepted**.
- Live dialogue evidence in the same run: `@astrologer_diary` (“Отдать Дневник”) is rendered and pickable (`fh=True`); `@refugees_s_ev_7_1_1` (“О вампирах”) is rendered but not pickable (`fh=False`); trade and leave are also rendered. The production log does not identify which task/topic/cross rules contributed the three markers, so root cause remains open pending the read-only contributor probe below.

### Research diagnostic — Astrologer marker contributors probe 0.1.1

- Purpose: identify every production rule that contributes a marker to `npc_astrologer` in the preserved three-marker state without modifying save, phrase, quest, resource, navigation, or UI state.
- Research branch: `research/astrologer-marker-contributors`.
- Exact executable/build source: `6e882dfd74d77532e7df14490ef08ab39b01014d`.
- Frozen ref: `frozen/astrologer-marker-contributors-probe-0.1.1`.
- Probe GUID: `nikich.gyk.daywheel.astrologer-marker-contributors`; coexists with production 1.1.1.
- Runtime timing: waits for Day Wheel cache readiness, then for `MainGame.game_started == true`, then another 1.25 seconds so the production gameplay marker tick has occurred before comparing contributor count with the actual Astrologer marker list.
- Output markers: `ASTRO_MARKER_BEGIN`, `ASTRO_OWNER`, `ASTRO_TOPIC`, `ASTRO_CROSS`, `ASTRO_CONTRIB`, `ASTRO_MARKER_SUMMARY`, `ASTRO_MARKER_END`.
- CI: run `35402618539`, job `105785587399`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelAstrologerMarkerContributorsProbe-0.1.1` (`10570998561`), archive digest `sha256:127db8de1d37d1e2d4e2c8c067677004ac46313f58bbe7593a1c9b5c6ebed228`.
- Raw DLL: **14,848 bytes**; SHA-256 `bcd2cf5b5a4d3414a59eb97a739a123d130933ff671b32cacc14c6601262daab`; local extraction matches CI.
- Requested test: keep production 1.1.1 installed and the pre-diary save unchanged; add this probe DLL beside it, launch that save once, do not hand over the diary, wait until normal gameplay appears, then exit after the log contains `ASTRO_MARKER_END` and return `LogOutput.log`.
- Result: **abandoned before contributor output**. The save-load hang described below occurred before any `ASTRO_MARKER_*` diagnostic line was emitted; causality was not attributed to the sidecar, but the method was retired as unnecessary risk.


### Research diagnostic follow-up — sidecar 0.1.1 abandoned after load hang

- Player result: after adding the standalone `Day Wheel Quest Markers - Astrologer marker contributors 0.1.1` beside production 1.1.1, the selected save did not finish loading.
- Log evidence: the sidecar itself loaded, but no `ASTRO_MARKER_BEGIN`, `ASTRO_CONTRIB`, or `ASTRO_MARKER_END` line was ever emitted. The log ended during the ordinary save-restore path after `PrepareScene` / `RescanGDPoints` / other mods' restore work, before Day Wheel's schema-3 load/ready stage.
- Interpretation: the log does **not** prove the sidecar caused the hang, because its contributor dump never executed; however the new hang coincided with installing it and the method is unnecessary risk. The sidecar diagnostic is therefore abandoned and must be removed before further testing.
- No production conclusion is drawn from this failed diagnostic run.

### 1.1.2 — integrated Astrologer contributor diagnostic

- Status: **research-only diagnostic / not a release candidate / do not merge or release**.
- Basis: exact frozen 1.1.1 candidate source `6ec1077560311e608fde8667a99df82fbca6112d`.
- Research branch: `research/astrologer-marker-contributors-integrated`.
- Exact executable/build source: `aa32f1ccb8a02a916d4f66129c80491824c23d59`.
- Frozen ref: `frozen/astrologer-marker-contributors-integrated-1.1.2`.
- Runtime behavior: production decision logic is unchanged. The first normal gameplay marker pass additionally logs `ASTRO_MARKER_BEGIN`, one `ASTRO_CONTRIB` line at each existing owner/topic/cross `AddMarker` call for `npc_astrologer`, then `ASTRO_MARKER_SUMMARY visualCount=... contributorCount=...` and `ASTRO_MARKER_END`. No separate plugin, polling loop, Harmony hook, save mutation, graph parse, or extra gameplay traversal is added.
- Source delta from candidate 1.1.1: version metadata plus 43 diagnostic lines around existing marker contribution sites; no rule classifier/gate/navigation behavior change.
- CI: run `35403153882`, job `105787227643`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.1.2` (`10570424599`), archive digest `sha256:550384c7e52eaaedf8c9cf77b34e0159004b9de49310839a7bcafa9b631ac9e8`.
- Raw DLL: **93,696 bytes**; SHA-256 `a874810dd119e2011a5087e9d1773845b2f600eaab548a537cc089b7596b6c81`; local extraction/hash matches CI.
- Workflow restored to manual-only after the frozen build.
- Requested test: remove the abandoned sidecar diagnostic DLL; replace production 1.1.1 with this single 1.1.2 research DLL; do not delete caches and do not hand over the diary. Load the same preserved save once, wait until normal gameplay appears and the log contains `ASTRO_MARKER_END`, then return `LogOutput.log`. The contributor lines should identify the exact sources of the three Astrologer markers.
- Result: **successful diagnostic**. The player run completed and the integrated production-path logging identified exactly three Astrologer contributors in the preserved state: owner task `astrologer_diary`, generic topic `astrologer_diary_9a`, and generic topic `astrologer_diary_9b`; visualCount=3 / contributorCount=3. This proved the duplicate-marker root cause and directly informed 1.1.3.

## 1.1.3 — task-bound downstream dedup candidate

- Date built: 2026-09-19 local / 2026-09-18 UTC.
- Development branch: `dev/1.1.3`, started from current accepted `main` 1.0.35 and reapplied the reviewed unified 1.1 architecture with both 1.1.0/1.1.1 regressions corrected.
- Exact executable/build source: `438558980ae5fbf62cac361b14f9aaaf8d292099`.
- Candidate ref: `candidate/1.1.3` at the exact executable/build source above.
- Triggering runtime evidence from integrated diagnostic 1.1.2: the preserved pre-diary Astrologer state produced exactly three contributors: owner task `astrologer_diary`, generic topic `astrologer_diary_9a`, generic topic `astrologer_diary_9b`. The live root menu exposed only `@astrologer_diary` as the relevant pickable interaction.
- Existing static evidence already proves `@astrologer_diary` is a `TASK+MENU_BOUNDARY`: it directly completes `npc_astrologer/astrologer_diary` and its direct next menu contains `astrologer_diary_9a` / `astrologer_diary_9b`. Therefore the three markers represented one visit, not three independent visits.
- Root cause: the generalized non-`@` exact-self layer treated a descendant exact-self answer as independent whenever root navigation could eventually reach it. That was too broad when every path first consumes/executes an already task-owned answer. The old completion-overlap pass only excluded a candidate when the candidate itself was task-owned; it did not suppress downstream same-visit continuation answers.
- Fix: build the existing task-completion answer set, union it with the actual accepted production owner/cross rule answer IDs, and admit a generic non-`@` exact-self candidate only if `NavigationReachabilityCache` has at least one interaction-root path whose recorded ancestors contain no task-owned answer. Ordinary submenu ancestors remain valid. Unsupported/unknown cases still fail closed.
- Persistent cache schema: **4**. Existing schema-3 `rules-1.407.bin` is intentionally rejected and rebuilt behind loading; the user does not delete cache files manually. Schema 4 is required because schema 3 may already persist the erroneous downstream generic topics.
- No gameplay FlowCanvas traversal and no new recurring scan/allocation path are added; the independence check is bootstrap-only.
- CI: run `35403868241`, job `105789438856`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.1.3` (`10571433560`), archive digest `sha256:8231ec2ac752d314a635095dc0a335b67d4fd7397148ec7b5960976872233375`.
- Raw DLL: **93,696 bytes**; SHA-256 `4cd4c67063deb672de32d1d88bb578fdcf5800dc4012ce397b5ceac13735030a`; local extraction/hash matches CI.
- Build workflow restored to manual-only after freezing the candidate; later docs/workflow commits do not change candidate source or bytes.
- Requested test: remove any prior research sidecar DLL, install only 1.1.3, leave all `.bin` caches in place, and load the preserved save before handing the diary to the Astrologer. First launch must reject schema 3 and bootstrap schema 4 successfully. The Astrologer must show exactly **one** marker while `@astrologer_diary` is pickable. Return the log before handing over the diary. If this passes, fully restart once on the same state to prove schema-4 direct load / FlowCanvas graph parse skipped; then the diary can be handed in to verify the single reminder closes naturally.
- Player result: **first-launch regression test passed**. Player reports exactly one Astrologer marker in the preserved pre-diary state. Supplied log proves 1.1.3 loaded, schema 4 rebuilt successfully behind loading in 900.79 ms, owner/cross/navigation parity remained 75/6, 8/6/0, 210/270/151/0, and non-`@` admitted exact-self topics fell from the defective 1.1.1 count 8 to **6**, matching removal of the two downstream same-visit diary choices. Live Astrologer menu still exposes `@astrologer_diary` as pickable while `@refugees_s_ev_7_1_1` is rendered but not pickable.
- Final runtime result: **passed**. After a full game restart with cache files untouched, schema 4 loaded directly from disk in **10.42 ms** and explicitly skipped FlowCanvas graph parsing. The same rule census loaded unchanged (owner 75/6, cross 8/6/0, self-consuming 61/60/1, non-`@` admitted 6, navigation 210/270/151/0).
- Lifecycle evidence: before hand-in, `@astrologer_diary` remained the single player-observed actionable Astrologer interaction and the unrelated refugee option remained blocked. Choosing `@astrologer_diary` completed `npc_astrologer/astrologer_diary`; its immediate same-visit `astrologer_diary_9a/9b` continuation then opened the next tasks. After the hand-in the player observed exactly one Astrologer marker and exactly one actually executable hand-in: `@astrologer_acid`, because Acid was present in inventory. `@astrologer_tools` was visible in the authored menu/log but could not actually be completed because Restoration Tools were not present. Therefore the retained single marker matches one genuinely actionable interaction.
- Important correction: the runtime log's `fh=True` line for a rendered answer is **not sufficient evidence by itself** that the interaction is truly executable under all live resource/item gates. Player-observed execution state remains authoritative for this acceptance point; do not use `fh=True` alone as a future actionability proof.
- Acceptance evidence now proves both required properties: (1) task-bound downstream `9a/9b` no longer create duplicate pre-diary markers; (2) schema-4 cache reload is fast and parse-free while subsequent live resource gating keeps the Astrologer reminder count aligned with the actually executable interaction count in this tested state.
- Acceptance: **explicitly accepted by the player on 2026-09-19**: `Подтверждаю. фиксируем, сливаем в main, обновляем всю документацию корректно.`
- Accepted baseline ref: `baseline/1.1.3-accepted` -> exact tested runtime source `438558980ae5fbf62cac361b14f9aaaf8d292099`.
- Stable-promotion authorization covers `main` plus GitHub Release publication of the exact tested DLL; no rebuild is permitted.
- GitHub Release publication: workflow run `35405244486`, job `105793551724`, success.
- Release: `v1.1.3`, release ID `391838646`, target exact runtime source `438558980ae5fbf62cac361b14f9aaaf8d292099`.
- Published asset: `Day.Wheel.Quest.Markers.1.1.3.dll`, asset ID `573652802`, **93,696 bytes**.
- Published asset digest: `sha256:4cd4c67063deb672de32d1d88bb578fdcf5800dc4012ce397b5ceac13735030a`, exactly matching the accepted/tested DLL and CI artifact.
- Status: **stable / released**.


## 1.1.4 — integrated Snake marker contributor diagnostic

- Status: **research-only diagnostic / not a release candidate / do not merge runtime or release**.
- Triggering player report: after Ms. Charm activated the counterfeit-coins return conversation with Snake, the player still saw only one Snake weekday marker even though a pre-existing Restoration Tools conversation remained available.
- Supplied runtime log proves Snake's menu simultaneously renders `@snake_1с` ("Шармэль попросила вернуть это тебе.") and `@snake_instrument` ("Я ищу Реставрационные инструменты."). The player independently confirms the counterfeit-coins conversation is currently executable.
- Existing accepted static evidence from `docs/NESTED_DIALOGUE_REACHABILITY_AUDIT.md` already proves Ms. Charm's `@actress_2b_1a` branch makes `actress_money` Visible and activates exact Snake topic `@snake_1с`.
- Basis: accepted stable `main` 1.1.3, runtime source `438558980ae5fbf62cac361b14f9aaaf8d292099`; no production behavior change is intended.
- Research branch: `research/snake-marker-contributors-integrated-1.1.4`.
- Exact executable/build source: `415780413add8621b4f510d4dcbda2572b3e528b`.
- Frozen ref: `frozen/snake-marker-contributors-integrated-1.1.4`.
- Diagnostic behavior: first normal marker pass logs `SNAKE_MARKER_BEGIN` / `SNAKE_MARKER_END`, exact phrase state for `@snake_1с` and `@snake_instrument`, `npc_actress/actress_money` task state, every visible Snake owner-task decision, every persisted Snake topic with unlocked/blacklisted/navigation/actionable/promoted state, every Snake cross-owner task with visible/actionable state and answer IDs, and the final visual/contributor count.
- The diagnostic is integrated into the existing production marker pass. It adds no sidecar plugin, Harmony hook, background worker, graph traversal, save mutation, or recurring diagnostic loop; the dump occurs once per loaded save.
- CI: run `35443230227`, job `105897612483`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.1.4` (`10583934763`), archive digest `sha256:11b41335a94c5e4c3ee611c797918cd70dfc754fb9d64aeb7ee3706a8a2f1e65`.
- Raw DLL: **96,256 bytes**; SHA-256 `207e82e0c8be1f641e975539bd2a0b60ded1b0dd791269c598ac2dfb0cde6565`. Local extraction/hash matches CI.
- Build workflow was restored to manual-only immediately after this frozen diagnostic build.
- Requested test: replace stable 1.1.3 with this single 1.1.4 DLL, keep the existing schema-4 cache, load the save/state where Snake currently offers both the counterfeit-coins return and Restoration Tools conversations, wait for normal gameplay, then return `LogOutput.log` after it contains `SNAKE_MARKER_END`. Do not consume either Snake conversation before collecting the log.
- Player result: **diagnostic passed / root cause established**. The returned log shows both `@snake_1с` and `@snake_instrument` unlocked and not blacklisted, with `npc_actress/actress_money` Visible. `@snake_instrument` is present as an actionable topic contributor, while `@snake_1с` is absent from owner/topic/cross rule contributors; the final Snake count is exactly 1. The live dialogue then renders both conversations simultaneously. This proves the missing second marker is a classifier omission, not UI layout/deduplication or manifest load failure.


## 1.1.5 — Snake counterfeit-coins intermediate fix

- Status: **candidate / player validation passed / architecture audit open / do not merge or release yet**.
- Trigger: 1.1.4 proved a false negative when `npc_actress/actress_money` is Visible and both Snake conversations `@snake_1с` (counterfeit coins) and `@snake_instrument` (Restoration Tools chain) are independently actionable, but production emits only the latter marker.
- Root cause: `@snake_1с` is a top-level persisted submenu parent with authored price `Item:quest_fake_coins = 1`. The parent itself does not self-blacklist; either child `snake_1с_4a` / `snake_1с_4b` blacklists the parent and unlocks `@actress_snake_back`. Therefore the accepted exact-self-consuming topic compiler correctly excludes the parent structurally, but the product model still requires a reminder because this exact visit is an authored intermediate step of visible task `npc_actress/actress_money`.
- Fix: add one exact GK 1.407 verified cross-owner intermediate rule for `npc_actress/actress_money -> npc_cultist/@snake_1с`. It requires the owner task to be Visible, the exact phrase to be unlocked and not blacklisted, and game-owned `Player.IsEnough(SmartRes)` for `Item:quest_fake_coins x1`.
- Scope guardrail: no generic submenu-parent classifier, no new provenance parser, no schema change, no graph traversal in gameplay, no diagnostic logging. Existing schema-4 cache remains valid.
- Branch: `dev/1.1.5`.
- Exact candidate/build source: `923fa06bcbce43c2498f39d8eaa27c417aa2c7c6`.
- Frozen candidate ref: `candidate/1.1.5` -> exact build source above.
- CI: run `35443963061`, job `105899596604`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.1.5` (`10584327309`), archive digest `sha256:9dddf2a433932b7b3eb569be761b6da4cabd53144deafede971ddee0fb335a2f`.
- Raw DLL: **94,720 bytes**; SHA-256 `1dcc49cc2f96bc5fe54a922b9b3ce71148c8c9156b974586b3c98d248226f9fa`; local extraction/hash matches CI.
- Build workflow restored to manual-only on `dev/1.1.5` immediately after freezing `candidate/1.1.5`.
- Requested test: install only 1.1.5 over the diagnostic build, keep the existing schema-4 cache, load the preserved state where Snake offers both `@snake_1с` and `@snake_instrument`. Before speaking to Snake there must be exactly **two** Snake weekday markers. Then consume the counterfeit-coins Snake interaction (either authored child choice) and exit the dialogue; with the Restoration Tools interaction still available, the Snake marker count must fall naturally from **2 -> 1**.
- Player result: **passed** on 2026-09-19. In the preserved Snake state, the weekday wheel showed exactly **2** Snake markers. After completing the counterfeit-coins interaction, the count dropped **2 -> 1** while the Restoration Tools interaction remained. After completing the Restoration Tools conversation, it dropped **1 -> 0**. The returned runtime log independently confirms that `@snake_1с` is present before selection, its child choice removes that parent entry, and `@snake_instrument` remains available afterward. The narrow 1.1.5 behavior is therefore validated; promotion remains intentionally deferred while the requested generalized interaction-lifecycle audit is open.

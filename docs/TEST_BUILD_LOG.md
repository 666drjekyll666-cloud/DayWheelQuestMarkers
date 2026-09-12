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

## 1.0.22 — candidate, awaiting player test

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
- Requested test: verify the known Snake `snake_stars` / `@snake_help_done` case with live Dark Church quality 20 before speaking to Snake; verify any naturally reachable intermediate-chain reminders, especially Snake help/necklace, Merchant support/Ms. Charm, Astrologer daughter/Ms. Charm, Bishop invitation/Merchant, and Inquisitor guards; confirm no marker appears while the authored relation/item prerequisite is unmet; report any noticeable load/prewarm regression.
- Player result: **pending**.
- Status: candidate only; do not merge to `main`, create `baseline/1.0.22-accepted`, or publish `v1.0.22` before explicit player acceptance.

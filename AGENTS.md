# Day Wheel Quest Markers — Working Rules

Read the global engineering contract in `666drjekyll666-cloud/DevRules` before substantive work. `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` apply here; this file adds project-specific constraints.

## Project identity

- Public mod: **Day Wheel Quest Markers**
- Repository: `666drjekyll666-cloud/DayWheelQuestMarkers`
- Project / assembly / DLL: `DayWheelQuestMarkers`
- Game: Graveyard Keeper 1.407
- Stable BepInEx GUID: `nikich.gyk.calendarquestspins`
- Legacy source namespace `CalendarQuestsPins` is intentionally retained; do not change the GUID or namespace merely for cosmetic normalization.
- Current accepted stable public baseline: **1.0.21**.
- Exact accepted runtime source: `7638343438dad6cdf522e37595f3fb21b442193a`.
- Accepted baseline ref: `baseline/1.0.21-accepted`.
- Accepted DLL SHA-256: `b609da9c35cd40ce09259a4c580e371dad15c3889f4e5cf9bdb0190a00e23c9a`.

## Product rule

The core rule is:

`active objective -> required NPC interaction -> NPC weekday -> marker`

Do not mark a weekday merely because an NPC has an unfinished quest. If the current step still requires crafting, finding, collecting, exploring, raising quality/relation, or another non-NPC prerequisite, no marker should appear yet.

False negatives are preferable to false positives. Unknown or unsupported quest structures fail closed.

## Accepted runtime architecture

Preserve the accepted 1.0.21 architecture unless a tested change explicitly replaces it:

- owner-local actionability comes from verified task completion routes in the owning weekday NPC graph;
- cross-owner actionability comes only from an explicit authored weekday-NPC completion route for the foreign-owned task;
- phrase state and blacklist state are honored;
- supported price/lock gates are reconstructed as game SmartRes and evaluated through `Player.IsEnough`;
- the two audited rare objective-bridge families are handled by the narrow `VerifiedBridgeReminderRules` mapping, not a broad dialogue heuristic;
- heavy owner/cross-owner graph parsing is prewarmed under the loading screen when all required runtime objects are verified present;
- gameplay refresh uses cached structure and a one-second cadence; broad graph/hierarchy scans do not belong in the steady-state path;
- HUD work is deferred until a real marker exists;
- multiple same-day objectives remain separate native-style markers distributed symmetrically within the weekday sector;
- current semantic `HUDSinIcon._sin_type`, not a fixed physical slot index, determines the weekday target.

## Marker sprite contract

Production uses the marker Sprite objects owned by the installed game at runtime. Do not embed or commit copied pixel payloads from Graveyard Keeper.

Verified sprite identifiers:

- `icon_quest_mark_small`
- `dlc_quest_mrk`
- `quest_marker_violet`
- `Icon_quest_mark_small_blue`

Resolve/cache these with bounded loaded-Sprite lookup. Normal lookup belongs in the verified loading/prewarm path; at most one runtime fallback lookup is allowed for a requested style that was not resident earlier. Game-owned Sprite objects must never be destroyed by the mod.

Do not introduce repeated broad `Resources.FindObjectsOfTypeAll` work into normal gameplay. Do not substitute custom art without explicit user approval.

## Performance

Steady-state runtime should be effectively negligible relative to the game:

- per-frame path: timer comparison only until the one-second tick is due;
- no continuous broad resource/hierarchy scans;
- no background worker;
- no save mutation;
- no repeated FlowCanvas parsing;
- reuse marker GameObjects and cached references;
- use the existing slow structural-staleness cadence rather than broad recurring validation.

The rejected universal provenance parser that pushed prewarm toward ~1.8 s must not return.

## Repository workflow

- `main` is accepted stable public state only.
- Runtime work belongs in `dev/X.Y.Z` until explicit player acceptance.
- A numbered DLL is immutable and tied to exact source SHA.
- Candidate artifacts may live in Actions; accepted stable DLLs belong in GitHub Releases and must be the exact tested bytes, not a rebuild under the same version.
- Documentation-only changes do not justify hosted CI.
- Public README and release notes are user-facing. Keep migration/provenance/private-development details out of them; such evidence belongs in engineering docs.

## Required evidence and records

Use these as long-lived sources of truth:

- `AGENTS.md`
- `docs/VERIFIED_RUNTIME_DATA.md`
- `docs/TEST_BUILD_LOG.md`
- `docs/MIGRATION_PROVENANCE.md`
- current production source and project file

Before adding a new quest/NPC/gate rule, establish it from repository evidence, targeted runtime evidence, or assembly inspection. Do not infer internal IDs from display text.

## Handoff / acceptance

Before handing a candidate DLL to the user:

- clean Release build succeeds;
- artifact is from committed canonical source;
- version metadata is consistent;
- no diagnostic code ships;
- exact source SHA, CI run/artifact ID, file size and SHA-256 are recorded;
- test request is narrow and actually proves the changed behavior.

After explicit player acceptance, freeze the exact tested source, promote the accepted state to `main`, and publish the exact tested DLL through GitHub Releases without rebuilding it.

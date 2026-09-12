# Day Wheel Quest Markers — Working Rules

Read the global engineering contract in `666drjekyll666-cloud/DevRules` before substantive work. `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` apply here; this file adds project-specific constraints.

## Project identity

- Public mod: **Day Wheel Quest Markers**
- Repository: `666drjekyll666-cloud/DayWheelQuestMarkers`
- Project / assembly / DLL: `DayWheelQuestMarkers`
- Game: Graveyard Keeper 1.407
- Stable BepInEx GUID: `nikich.gyk.calendarquestspins`
- Legacy source namespace `CalendarQuestsPins` is intentionally retained; do not change the GUID or namespace merely for cosmetic normalization.
- Current accepted stable public baseline: **1.0.23**.
- Exact accepted runtime source: `3da21541a38753387cf9c4d343559e5fb1181f34`.
- Accepted baseline ref: `baseline/1.0.23-accepted`.
- Accepted DLL SHA-256: `dc9b1f3494f46a903ec416679c08b9d6a3704f2132e82d8c9ac2cf21c06458b7`.

## Product rule

The core rule is:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`

A weekday-NPC interaction qualifies by either of these evidence-backed routes:

1. **Task-linked interaction:** a visible objective has an authored route through that weekday NPC, and every currently required phrase/resource gate is satisfied.
2. **One-shot dialogue interaction:** an authored persisted `@` dialogue topic is currently open/pickable and its own authored branch persistently consumes that exact topic by adding it to the game's phrase blacklist. It does not need to be a direct task-completion anchor; a unique conversation itself is worth reminding the player about.

Do not mark a weekday merely because an NPC has an unfinished quest. If the relevant task-linked step still requires crafting, finding, collecting, exploring, raising quality/relation, or another non-NPC prerequisite, that task route does not create a marker yet.

Do not treat arbitrary visible menu options as reminders. Repeatable utility/menu/container choices such as Trade, Leave, Back, or a non-consuming "about ..." submenu header are excluded structurally because they do not consume their own persisted topic. Do not implement this by translated/display-text matching.

Unknown or unsupported structures fail closed. False positives remain undesirable, but the old rule that required every reminder to prove downstream quest progression is retired: it incorrectly hid real one-time conversations such as the portal-item dialogue opened by Snake at the Inquisitor.

## Accepted 1.0.23 runtime architecture

The accepted stable 1.0.23 implementation used three loading-time structural caches plus two narrow supplemental manifests:

- `QuestRuleCache` for owner-local direct task-completion routes and live zone-quality mirrors;
- `CrossOwnerRuleCache` for direct task completion in a different weekday NPC graph;
- `OneShotDialogueRuleCache` for exact self-consuming authored topics;
- `VerifiedBridgeReminderRules` and `VerifiedIntermediateReminderRules` as temporary controls for known non-direct progression stages;
- `SessionCacheRebinder` to reconnect those separate caches across compatible save/session reloads.

This implementation is accepted and remains the stable fallback until 1.0.24 is tested.

## 1.0.24 development architecture

`dev/1.0.24` is a behavior-preserving/consolidating refactor of the accepted model, with one deliberate product-aligned simplification: previously verified bridge/intermediate topics are now handled by the already-accepted generic self-consuming one-shot rule instead of explicit manifests.

The candidate architecture is:

- one `WeekdayInteractionRuleCache` owns the structural index for all six weekday NPC graphs;
- each NPC serialized graph is converted to node/connection/incoming-flow/incoming-value indexes **once per cache build**;
- the same parsed index emits owner-local task rules, cross-owner task rules, and one-shot dialogue rules;
- direct task-completion answers are excluded from the generic one-shot set so one interaction cannot be double-counted;
- owner-local rules retain the verified authoritative `WorldZone.GetTotalQuality()` fallback for unambiguous authored quality mirrors;
- cross-owner and generic one-shot gates preserve the accepted 1.0.23 SmartRes/`Player.IsEnough` behavior rather than silently broadening zone-mirror semantics;
- exact self-consuming bridge/intermediate topics no longer require a parallel hard-coded manifest. Their authored phrase/gate state is the reminder source, consistent with the accepted product rule;
- the unified cache directly rebinds player/save/KnownNPC references, so the reflection-based `SessionCacheRebinder` is removed;
- graph parsing remains loading-time only. Normal gameplay still evaluates cached rules on the one-second cadence;
- structural staleness remains low-frequency; the 1.0.24 candidate additionally compares the known-NPC signature at that cadence so same-count NPC-set changes cannot be missed;
- `QuestRuleCache`, `CrossOwnerRuleCache`, `OneShotDialogueRuleCache`, `SessionCacheRebinder`, `VerifiedBridgeReminderRules`, and `VerifiedIntermediateReminderRules` are removed from the 1.0.24 production compile.

This consolidation must not be treated as accepted until a 1.0.24 DLL is built from frozen source and the player verifies marker parity/expected one-shot behavior plus the loading-prewarm timing.

The rejected universal provenance parser is still rejected. The unified cache only combines already-accepted local structural classifiers; it does not walk arbitrary external dependency/provenance chains.

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

The rejected universal provenance parser that pushed prewarm toward ~1.8 s must not return. Accepted 1.0.23 prewarm on the developed test save was 782.89 ms because three structural caches independently reparsed the same six NPC graphs. The explicit 1.0.24 performance goal is to remove that duplicate parsing; the actual candidate timing must be measured from the player runtime log rather than assumed.

## Repository workflow

- `main` is accepted stable public state only.
- Runtime work belongs in `dev/X.Y.Z` until explicit player acceptance.
- A numbered DLL is immutable and tied to exact source SHA.
- Candidate artifacts may live in Actions; accepted stable DLLs belong in GitHub Releases and must be the exact tested bytes, not a rebuild under the same version.
- Candidate build workflow is manual-only; documentation-only changes do not justify hosted CI.
- Public README and release notes are user-facing. Keep migration/provenance/private-development details out of them; such evidence belongs in engineering docs.

## Required evidence and records

Use these as long-lived sources of truth:

- `AGENTS.md`
- `docs/VERIFIED_RUNTIME_DATA.md`
- `docs/TEST_BUILD_LOG.md`
- `docs/MIGRATION_PROVENANCE.md`
- current production source and project file

Before adding a new quest/NPC/gate rule, establish it from repository evidence, targeted runtime evidence, or assembly inspection. Do not infer internal IDs from display text.

For generic one-shot dialogue classification, the accepted structural evidence is the authored exact-phrase self-blacklist operation plus the authored phrase/resource gates. Do not broaden it to arbitrary dialogue visibility or translated-text heuristics.

## Handoff / acceptance

Before handing a candidate DLL to the user:

- clean Release build succeeds;
- artifact is from committed canonical source;
- version metadata is consistent;
- no diagnostic code ships;
- exact source SHA, CI run/artifact ID, file size and SHA-256 are recorded;
- test request is narrow and actually proves the changed behavior.

After explicit player acceptance, freeze the exact tested source, promote the accepted state to `main`, and publish the exact tested DLL through GitHub Releases without rebuilding it.

# Day Wheel Quest Markers — Working Rules

Read the global engineering contract in `666drjekyll666-cloud/DevRules` before substantive work. `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` apply here; this file adds project-specific constraints.

## Project identity

- Public mod: **Day Wheel Quest Markers**
- Repository: `666drjekyll666-cloud/DayWheelQuestMarkers`
- Project / assembly / DLL: `DayWheelQuestMarkers`
- Game: Graveyard Keeper 1.407
- Stable BepInEx GUID: `nikich.gyk.calendarquestspins`
- Legacy source namespace `CalendarQuestsPins` is intentionally retained; do not change the GUID or namespace merely for cosmetic normalization.
- Current accepted stable public baseline: **1.0.24**.
- Exact accepted runtime source: `99d961abef528e14378c3dc8fd074a550b1138e9`.
- Accepted baseline ref: `baseline/1.0.24-accepted`.
- Accepted DLL SHA-256: `05aecb65054ba4890a7ffb043ead2fb4996512d911d63bb24a338e99c039971c`.

## Product rule

The core rule is:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`

A weekday-NPC interaction qualifies by either of these evidence-backed routes:

1. **Task-linked interaction:** a visible objective has an authored route through that weekday NPC, and every currently required phrase/resource gate is satisfied.
2. **One-shot dialogue interaction:** an authored persisted `@` dialogue topic is currently open/pickable and its own authored branch persistently consumes that exact topic by adding it to the game's phrase blacklist. It does not need to be a direct task-completion anchor; a unique conversation itself is worth reminding the player about.

Do not mark a weekday merely because an NPC has an unfinished quest. If the relevant task-linked step still requires crafting, finding, collecting, exploring, raising quality/relation, or another non-NPC prerequisite, that task route does not create a marker yet.

Do not treat arbitrary visible menu options as reminders. Repeatable utility/menu/container choices such as Trade, Leave, Back, or a non-consuming "about ..." submenu header are excluded structurally because they do not consume their own persisted topic. Do not implement this by translated/display-text matching.

Unknown or unsupported structures fail closed. False positives remain undesirable, but the old rule that required every reminder to prove downstream quest progression is retired: it incorrectly hid real one-time conversations such as the portal-item dialogue opened by Snake at the Inquisitor.

## Accepted 1.0.24 runtime architecture

The accepted production architecture is one unified loading-time structural cache:

- `WeekdayInteractionRuleCache` owns the structural index for all six weekday NPC graphs;
- each NPC serialized graph is converted to node/connection/incoming-flow/incoming-value indexes **once per cache build**;
- the same parsed index emits owner-local task rules, cross-owner task rules, and one-shot dialogue rules;
- direct task-completion answers are excluded from the generic one-shot set so one interaction cannot be double-counted;
- owner-local rules retain the verified authoritative `WorldZone.GetTotalQuality()` fallback for unambiguous authored quality mirrors;
- cross-owner and generic one-shot gates preserve the accepted SmartRes/`Player.IsEnough` behavior rather than silently broadening zone-mirror semantics;
- previously verified bridge/intermediate topics are handled by the same exact self-consuming authored one-shot structure instead of parallel hard-coded manifests;
- the unified cache directly rebinds player/save/KnownNPC references;
- graph parsing remains loading-time only. Normal gameplay evaluates cached rules on the one-second cadence;
- structural staleness remains low-frequency and compares the known-NPC signature so same-count NPC-set changes cannot be missed.

Superseded production classes removed by 1.0.24:

- `QuestRuleCache`
- `CrossOwnerRuleCache`
- `OneShotDialogueRuleCache`
- `SessionCacheRebinder`
- `VerifiedBridgeReminderRules`
- `VerifiedIntermediateReminderRules`

The rejected universal provenance parser remains rejected. The unified cache only combines accepted local structural classifiers; it does not walk arbitrary external dependency/provenance chains.

Accepted runtime evidence on the developed regression save:

- the same three portal-item reminders appeared as in accepted 1.0.23;
- selecting Inquisitor `@inquisitor_magic_item` removed only that reminder as expected;
- unified cache prewarm completed in **308.07 ms**, versus **782.89 ms** for accepted 1.0.23 on the corresponding developed-save test;
- the mod reached `Ready` with owner supported=75, owner unsupported=6, cross-owner tasks=8, cross-owner supported=6, cross-owner unsupported=0, one-shot topics=55, one-shot supported=54, one-shot unsupported=1;
- no Day Wheel Quest Markers error/warning was present in the supplied runtime log.

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

The rejected universal provenance parser that pushed prewarm toward ~1.8 s must not return. Accepted 1.0.24 unified-cache prewarm on the developed regression save was 308.07 ms, substantially below accepted 1.0.23's 782.89 ms because the six NPC graphs are no longer reparsed by multiple independent caches.

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

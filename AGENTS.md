# Day Wheel Quest Markers — Working Rules

Read the global engineering contract in `666drjekyll666-cloud/DevRules` before substantive work. `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` apply here; this file adds project-specific constraints.

## Project identity

- Public mod: **Day Wheel Quest Markers**
- Repository: `666drjekyll666-cloud/DayWheelQuestMarkers`
- Project / assembly / DLL: `DayWheelQuestMarkers`
- Game: Graveyard Keeper 1.407
- Stable BepInEx GUID: `nikich.gyk.calendarquestspins`
- Legacy source namespace `CalendarQuestsPins` is intentionally retained; do not change the GUID or namespace merely for cosmetic normalization.
- Current accepted stable public baseline: **1.0.35**.
- Exact accepted runtime source: `5e8305ea6c2515dd0694343a07eb70e403ad0528`.
- Accepted baseline ref: `baseline/1.0.35-accepted`.
- Accepted DLL SHA-256: `a7752d2058d4728db054adc049f650cad037cede41166b6c4c7329f4ea169879`.

## Product rule

The core rule is:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`

A weekday-NPC interaction qualifies by either of these evidence-backed routes:

1. **Task-linked interaction:** a visible objective has an authored route through that weekday NPC, and every currently required phrase/resource/navigation gate is satisfied.
2. **One-shot dialogue interaction:** an authored selectable answer is currently reachable/pickable and its own authored branch persistently consumes that exact answer ID by adding it to the game's phrase blacklist. The identifier may be either a persisted `@...` topic or a verified non-`@` answer. It does not need to be a direct task-completion anchor; a unique conversation itself is worth reminding the player about.

Do not mark a weekday merely because an NPC has an unfinished quest. If the relevant task-linked step still requires crafting, finding, collecting, exploring, raising quality/relation, or another non-NPC prerequisite, that task route does not create a marker yet.

Do not treat arbitrary visible menu options as reminders. Repeatable utility/menu/container choices such as Trade, Leave, Back, or a non-consuming "about ..." submenu header are excluded structurally because they do not consume their own exact authored answer ID. Do not implement this by translated/display-text matching.

Unknown or unsupported structures fail closed. False positives remain undesirable, but the old rule that required every reminder to prove downstream quest progression is retired: it incorrectly hid real one-time conversations such as the portal-item dialogue opened by Snake at the Inquisitor.

## Accepted runtime architecture

Production uses persistent structural caches plus cheap live bindings:

- `WeekdayInteractionRuleCache` remains the source of final reminder-bearing owner-local task, cross-owner task, and persisted `@` one-shot dialogue rules;
- `NavigationReachabilityCache` adds the verified root-to-final-answer reachability contract required for nested dialogue;
- `NonAtSelfConsumingRuleCache` covers the separately audited exact-self-consuming non-`@` answer class introduced in accepted 1.0.35;
- each authored path stores only required ancestor phrase-state predicates and supported ancestor `AnswerData` gates; predicates within one path are AND, alternative authored paths are OR;
- unconditional plain ancestors compile away; unknown or unsupported ancestry fails closed;
- the exact six weekday-NPC graphs are parsed only during loading/bootstrap when the relevant persistent cache is missing or incompatible;
- the schema-2 primary manifest lives at `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`;
- the accepted 1.0.35 non-`@` supplement lives at `BepInEx/cache/DayWheelQuestMarkers/non-at-self-consuming-1.407.bin`;
- normal later loads deserialize compact cached data and recreate only live SmartRes/WGO/player/KnownNpc bindings; no FlowCanvas graph parse is allowed during gameplay;
- owner-local rules retain the verified authoritative `WorldZone.GetTotalQuality()` fallback for unambiguous authored quality mirrors;
- cross-owner and generic one-shot gates preserve the accepted SmartRes/`Player.IsEnough` behavior rather than silently broadening zone-mirror semantics;
- native marker sprites remain game-owned and are cached through bounded lookup;
- known-NPC changes use cheap rebinding; the steady one-second and 30-second validation paths must remain allocation-light and must not rebuild dictionaries/signatures without a real state change.

Accepted 1.0.32 retains the narrow `VerifiedCompletionReminderRules` supplement for completion routes that the schema-2 owner classifier cannot represent directly:

- verified promoted task/topic pairs reuse existing persisted one-shot and navigation predicates rather than duplicating their authored gates;
- `npc_cultist/snake_trap` uses the exact `snake_stone_ready` route and game-owned `_rel >= 10` SmartRes check;
- `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back` are exact mandatory interaction-event mappings only;
- promoted topics are suppressed from the generic one-shot loop while their visible owner task is evaluated, preventing duplicate generic/task markers;
- unsupported `@souls_s_s33_ask` remains fail-closed;
- there is no broad `CustomEvent` or generic `Visible task` classifier.

Accepted 1.0.35 adds a generalized, evidence-bounded non-`@` self-consuming layer:

- the full six-NPC GK 1.407 census contains 77 unique non-`@` answer IDs and exactly 19 exact-self-blacklisting candidates;
- the audited class contains 0 reversible candidates and 0 utility-like `Leave`/`Back`/`Trade` candidates;
- reverse tracing for this class supports numbered `Flow_WaitForFlow` inputs and exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` jumps;
- answers already owned by the existing task-completion census are excluded from the supplemental generic layer to prevent duplicate markers;
- final-answer SmartRes gates and the accepted navigation reachability contract still apply;
- the audited universe counts are bootstrap integrity guards; mismatch fails the supplement closed rather than broadening classification.

Verified runtime evidence:

- first schema-2 bootstrap completed behind the loading screen in **557.88 ms** under 1.0.30;
- later launches load the same schema-2 persistent manifest with FlowCanvas graph parsing skipped; the accepted 1.0.32 test loaded it in **10.80 ms**;
- canonical primary-manifest counts remain owner 75/6, cross-owner 8/6/0, persisted `@` one-shot 55/54/1;
- navigation counts remain 210 answers, 270 paths, 151 predicates, 0 unsupported paths;
- the reported Charmel false-positive state (`actress_2b` blocked by relation) produces no two false Lust-day markers;
- 1.0.32 runtime testing proved the Inquisitor mandatory stage marker appears before `inquisitor_talk`, disappears when the automatic scene completes it, and subsequent ordinary prerequisite-aware reminders continue to work;
- 1.0.35 player testing proved `snake_1a` produced the expected Snake marker while the 5-Faith gate was satisfied, then disappeared after the interaction consumed the answer; the simultaneous Charmel marker also disappeared when spending the same 5 Faith made her own authored gate unsatisfied;
- the earlier roughly 30-second rhythmic Day Wheel hitch was removed by the accepted allocation-free steady-state path; remaining sparse hitches occur at the control game/modpack baseline and are not attributed to Day Wheel without new evidence.

The rejected universal provenance parser remains rejected. Production derives only verified local reminder structure/navigation from the six weekday-NPC graphs; it does not walk arbitrary external dependency/provenance chains.

Superseded production classes/approaches include:

- `QuestRuleCache`
- `CrossOwnerRuleCache`
- `OneShotDialogueRuleCache`
- `SessionCacheRebinder`
- `VerifiedBridgeReminderRules`
- `VerifiedIntermediateReminderRules`
- the 1.0.29 Charmel-only `VerifiedNestedDialogueGate`
- tactical 1.0.33/1.0.34 hard-coded Astrologer/Snake non-`@` fixes; accepted 1.0.35 replaces them with the audited structural class.

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
- no FlowCanvas/navigation graph traversal during gameplay;
- reuse marker GameObjects and cached references;
- known-NPC count/fingerprint checks remain allocation-light;
- use the existing slow structural-staleness cadence rather than broad recurring validation.

Do not optimize speculative problems. The accepted performance line is evidence-driven: 1.0.25 exposed a 302.22 ms first-NPC runtime rebuild; 1.0.26 moved graph work behind loading and persisted it; 1.0.27 moved the cache to BepInEx; 1.0.28 removed recurring allocation pressure that correlated with the old ~30-second rhythmic hitch; 1.0.30 added persisted navigation reachability; 1.0.32 added narrow verified completion predicates; 1.0.35 adds only loading-derived/persisted structural data plus cheap live blacklist/navigation/SmartRes evaluation.

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

For generic one-shot dialogue classification, the accepted structural evidence is the authored exact-answer self-blacklist operation plus authored phrase/resource/navigation gates. The `@` prefix is not itself a semantic requirement. Do not broaden this to arbitrary dialogue visibility or translated-text heuristics.

## Handoff / acceptance

Before handing a candidate DLL to the user:

- clean Release build succeeds;
- artifact is from committed canonical source;
- version metadata is consistent;
- no diagnostic code ships;
- exact source SHA, CI run/artifact ID, file size and SHA-256 are recorded;
- test request is narrow and actually proves the changed behavior.

After explicit player acceptance, freeze the exact tested source, promote the accepted state to `main`, and publish the exact tested DLL through GitHub Releases without rebuilding it.
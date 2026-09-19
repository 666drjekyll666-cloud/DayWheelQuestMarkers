# Day Wheel Quest Markers — Working Rules

Read the global engineering contract in `666drjekyll666-cloud/DevRules` before substantive work. `ENGINEERING_RULES.md`, `CI_POLICY.md`, `GIT_WORKFLOW.md`, and `PROJECT_BOOTSTRAP.md` apply here; this file adds project-specific constraints.

## Project identity

- Public mod: **Day Wheel Quest Markers**
- Repository: `666drjekyll666-cloud/DayWheelQuestMarkers`
- Project / assembly / DLL: `DayWheelQuestMarkers`
- Game: Graveyard Keeper 1.407
- Stable BepInEx GUID: `nikich.gyk.calendarquestspins`
- Legacy source namespace `CalendarQuestsPins` is intentionally retained; do not change the GUID or namespace merely for cosmetic normalization.
- Current accepted stable public baseline: **1.1.6**.
- Exact accepted tested runtime source: `e2ad9c7ef2bbf312ae79f2a2fb3d1bfb7f5cc1c7`.
- Accepted baseline ref: `baseline/1.1.6-accepted`.
- Accepted DLL: 98,304 bytes.
- Accepted DLL SHA-256: `d17cf1629acb92c9c06a8f983a826a75c40fa8f0b38fff48c45d48314e19a339`.

## Product rule

The core rule is:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`

The accepted GK 1.407 model has three evidence classes:

1. **Task-owned selectable interaction:** a current Visible task has an authored route through the weekday NPC, and every supported phrase/resource/navigation gate required for that route is currently satisfied.
2. **Dialogue-lifecycle interaction:** a reachable authored branch persistently consumes the selected answer itself or the nearest selectable ancestor on that same root-to-answer path. The nearest persistent lifecycle owner represents one visit/reminder interaction. Exact self-consumption wins over ancestor consumption; task-owned owners and same-visit descendants are deduplicated.
3. **Verified mandatory event stage:** no selectable answer represents the interaction. This remains limited to the exact runtime-proven event-only stages `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back`.

Do not mark a weekday merely because an NPC has an unfinished quest. If the relevant step still requires crafting, finding, collecting, exploring, raising quality/relation, or another non-NPC prerequisite, it does not create a reminder yet.

Do not treat arbitrary visible menu options as reminders. Repeatable utility/navigation entries such as Trade, Leave, Back, or ordinary non-consuming submenu headers are excluded structurally. Do not implement this through translated/display-text matching.

Unknown, reversible, ambiguous, or unsupported structures fail closed. False positives remain more harmful than an unsupported false negative.

## Accepted runtime architecture

Accepted production **1.1.6** uses one persistent schema-5 interaction manifest plus cheap live bindings:

- `WeekdayInteractionRuleCache` derives owner-local and cross-owner task rules plus the established persisted-topic base rules;
- `NavigationReachabilityCache` derives root-to-answer paths, ordered selectable ancestors, and compact phrase/AnswerData predicates;
- `UnifiedDialogueLifecycleCompiler` is bootstrap-only. It preserves the audited non-`@` exact-self class and additionally resolves **nearest persistent lifecycle owners** when a progressing child consumes a selectable ancestor on the same concrete root path;
- lifecycle precedence is: self consumption first, otherwise nearest consumed selectable ancestor; task-owned owners and descendants reachable only through an already task-owned visit are suppressed from the generic dialogue layer;
- the complete six-NPC lifecycle census found exactly six unique ancestor-owner candidates: four admitted independent owners and two task-owned suppressions, with zero reversible owners admitted;
- `PersistentRuleManifest` schema 5 persists task rules, dialogue-lifecycle topics, navigation predicates, and lifecycle integrity counts to `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`;
- the legacy 1.0.35 `non-at-self-consuming-1.407.bin` file is ignored and may remain harmlessly on disk;
- the exact six weekday-NPC graphs are parsed only during loading/bootstrap when schema 5 is missing or incompatible;
- normal later loads deserialize compact cached data and recreate live SmartRes/WGO/player/KnownNpc bindings; gameplay never reparses FlowCanvas graphs;
- owner-local rules retain the verified authoritative `WorldZone.GetTotalQuality()` fallback for unambiguous authored quality mirrors;
- generic resource/relation gates continue to delegate sufficiency to game-owned `Player.IsEnough(SmartRes)`;
- `VerifiedCompletionReminderRules` remains a narrow supplement for task-completion topologies not yet represented by the generic task compiler. Five promoted routes reuse normal persisted topic/navigation predicates, `snake_trap` keeps its exact verified relation-gated route, and the two mandatory event-only stages remain exact mappings;
- there is no broad `Visible task`, arbitrary `CustomEvent`, translated-text, or universal external-provenance classifier;
- native marker sprites remain game-owned and cached through bounded lookup;
- known-NPC changes use cheap rebinding; steady one-second evaluation and approximately 30-second structural validation remain allocation-light.

Accepted schema-5 integrity/runtime counts:

- owner: 75 supported / 6 unsupported;
- cross-owner: 8 tasks / 6 supported / 0 unsupported;
- dialogue-lifecycle: 65 topics / 64 supported / 1 unsupported;
- non-`@` universe: 77 unique / 19 exact-self / 6 admitted / 9 task/completion exclusions;
- ancestor lifecycle owners: 6 candidates / 2 task-owned exclusions / 4 admitted / 4 supported / 0 unsupported;
- navigation: 210 answers / 270 paths / 151 predicates / 0 unsupported.

Accepted 1.1.6 runtime evidence:

- schema 5 bootstrap completed behind loading in **1005.16 ms**;
- a subsequent same-process save reload deserialized the persisted schema-5 file in **3.89 ms** with `FlowCanvas graph parse skipped`;
- final live-object rebinding on that reload required one guarded **3.21 ms** manifest re-read, again without graph parsing; this is bounded load-transition recovery, not recurring gameplay work;
- the Snake counterfeit-coins + Restoration Tools regression passed **2 -> 1 -> 0**, proving that `@snake_1с` is now generated by the common lifecycle compiler rather than the removed 1.1.5 hard-coded rule.

The rejected universal provenance parser remains rejected. Production derives only verified local interaction/task/navigation semantics from the six weekday-NPC graphs and does not walk arbitrary external dependency chains.

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

- per frame: timer comparison only until the one-second tick is due;
- no continuous broad resource/hierarchy scans;
- no background worker;
- no save mutation;
- no FlowCanvas/navigation graph traversal during gameplay;
- reuse marker GameObjects and cached references;
- known-NPC count/fingerprint checks remain allocation-light;
- use the existing slow structural-staleness cadence rather than broad recurring validation.

Do not optimize speculative problems. The evidence-driven lineage is:

- 1.0.25 exposed a 302.22 ms first-NPC runtime rebuild;
- 1.0.26 moved graph work behind loading and persisted it;
- 1.0.28 removed recurring allocation pressure associated with the old roughly 30-second rhythmic hitch;
- 1.0.30 added persisted navigation reachability;
- 1.0.35 generalized non-`@` exact-self interactions;
- 1.1.3 unified exact-self persistence and fixed same-visit duplication;
- **1.1.6 generalizes dialogue ownership from exact-self-only to nearest persistent lifecycle owner without adding gameplay graph work**.

The one-time schema-5 bootstrap is about 1 second on the tested developed save. Cached schema-5 reads are a few milliseconds. Do not add complexity merely to optimize that bounded loading cost without measured user-visible need.

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
- `docs/UNIFIED_INTERACTION_1.1.6.md`
- `docs/EXHAUSTIVE_VALIDATION_HARNESS.md`
- `docs/MIGRATION_PROVENANCE.md`
- current production source and project file

Before adding a new quest/NPC/gate rule, establish it from repository evidence, targeted runtime evidence, or assembly/serialized-graph inspection. Do not infer internal IDs from display text.

For generic dialogue classification, the accepted structural evidence is **persistent lifecycle consumption on a concrete authored root path**:

- selected answer self-consumption wins;
- otherwise the nearest persistently consumed selectable ancestor owns the interaction;
- the owner must be independently reachable/actionable under current phrase, navigation, and supported SmartRes/AnswerData gates;
- task-owned owners and same-visit descendants are deduplicated;
- reversible, ambiguous, unsupported, utility-like, and root-unreachable structures fail closed.

Do not broaden this to arbitrary dialogue visibility, `fh=True` alone, or translated-text heuristics.

For any future structural change to task/dialogue/navigation classification, run the interaction-universe validator before handing a player DLL. An unexplained baseline delta is a failed regression, even if aggregate counts still look plausible. Baseline updates require interaction-level evidence; do not merely change expected numbers to make CI green.

The remaining narrow completion/event supplement is not permission to accumulate ad-hoc fixes. Before adding an exact rule, first test whether the state can be represented by the task compiler or dialogue-lifecycle compiler. Exact mappings are allowed only when runtime evidence proves a genuinely different host mechanism.

## Handoff / acceptance

Before handing a candidate DLL to the user:

- clean Release build succeeds;
- artifact is from committed canonical source;
- version metadata is consistent;
- no diagnostic code ships;
- exact source SHA, CI run/artifact ID, file size and SHA-256 are recorded;
- test request is narrow and actually proves the changed behavior.

After explicit player acceptance, freeze the exact tested source, promote the accepted state to `main`, and publish the exact tested DLL through GitHub Releases without rebuilding it.
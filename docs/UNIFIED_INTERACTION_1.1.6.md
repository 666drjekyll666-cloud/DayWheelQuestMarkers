# Unified Interaction Architecture — accepted 1.1.6

Status: **accepted stable architecture for Graveyard Keeper 1.407**.

## Product meaning

A marker means:

> there is an independently actionable reason to visit this weekday NPC now.

The mod does not mark unfinished NPC questlines in general. It marks current interactions whose authored prerequisites are satisfied.

## Accepted semantic model

The runtime model has three evidence classes.

### 1. Task-owned selectable interaction

A current Visible task has an authored route through the weekday NPC and the route is currently executable under supported phrase, resource, relation, quality, and navigation gates.

Owner-local and cross-owner tasks are provenance variants of the same semantic class.

### 2. Dialogue-lifecycle interaction

A progressing authored dialogue branch persistently consumes a selectable entry.

Ownership rule:

1. exact self-consumption wins;
2. otherwise the nearest persistently consumed selectable ancestor on the same concrete root-to-answer path owns the interaction;
3. if no self/ancestor is persistently consumed, the generic dialogue layer emits nothing;
4. task-owned lifecycle owners and descendants reachable only inside an already task-owned visit are deduplicated;
5. phrase state, supported AnswerData/SmartRes gates, and navigation reachability remain authoritative;
6. reversible, utility-like, ambiguous, unsupported, or root-unreachable structures fail closed.

This subsumes the older split between persisted `@` topics, non-`@` exact-self answers, and the 1.1.5 Snake counterfeit-coins special case.

### 3. Verified mandatory event stage

Two proven stages require visiting a weekday NPC but have no selectable answer representing the interaction:

- `npc_inquisitor/inquisitor_talk`
- `npc_cultist/snake_back`

They remain exact event mappings because forcing them into a dialogue-only abstraction would discard the actual host semantics.

## Complete lifecycle census

The bounded six-NPC GK 1.407 census found:

- 243 authored answer occurrences;
- 161 branches with persistent blacklist effects;
- 204 path-local self-owned paths;
- 12 path-local ancestor-owned paths;
- 0 reversible owners admitted.

The 12 ancestor paths collapse to six unique owners:

- four admitted independent dialogue interactions:
  - Astrologer `@tr_quest_13_research_1`
  - Snake `@snake_1с`
  - Merchant `@merchant_2e_1e`
  - Bishop `bishop_2_1a`
- two task-owned suppressions:
  - Merchant `@merchant_2b`
  - Merchant `@merchant_favore_done`

This is the finite evidence-backed ancestor-consumption universe for the six weekday NPC graphs in GK 1.407.

## Production architecture

- `WeekdayInteractionRuleCache`: task-owned rules and established persisted-topic base derivation.
- `NavigationReachabilityCache`: concrete root paths, ordered selectable ancestors, and compact runtime predicates.
- `UnifiedDialogueLifecycleCompiler`: bootstrap-only lifecycle derivation, including non-`@` exact-self and nearest consumed ancestor ownership.
- `PersistentRuleManifest`: schema 5 persistence.
- `VerifiedCompletionReminderRules`: narrow residual completion/event supplement.
- `CalendarMarkers` / `NativeMarkerSprites`: game-native HUD rendering.

Normal gameplay performs no FlowCanvas graph parsing.

## Schema-5 integrity counts

- owner: 75 supported / 6 unsupported
- cross-owner: 8 tasks / 6 supported / 0 unsupported
- dialogue-lifecycle: 65 topics / 64 supported / 1 unsupported
- non-`@`: 77 unique / 19 exact-self / 6 admitted / 9 task-completion exclusions
- ancestor owners: 6 candidates / 2 task-owned exclusions / 4 admitted / 4 supported / 0 unsupported
- navigation: 210 answers / 270 paths / 151 predicates / 0 unsupported

## Accepted runtime evidence

- schema-5 bootstrap: **1005.16 ms**, behind loading;
- persisted schema-5 reload: **3.89 ms**, `FlowCanvas graph parse skipped`;
- guarded final-binding reload: **3.21 ms**, no graph parse;
- Snake counterfeit-coins + Restoration Tools regression: **2 -> 1 -> 0** markers.

## What is still not one algorithm

The implementation is semantically unified much further than 1.1.3, but Graveyard Keeper does not encode every required NPC visit through one host primitive.

A literal single predicate would currently be dishonest. The smallest evidence-backed model is:

`task-owned interaction OR dialogue-lifecycle interaction OR verified event-only stage`

The first two can share more bootstrap infrastructure in the future. The third is genuinely different unless new evidence proves a broader native event contract.

## Remaining engineering debt

Two optional consolidation targets remain:

1. **Shared WeekdayGraphIndex.** The task, navigation, and lifecycle passes still perform overlapping bootstrap parsing/indexing. A single immutable parsed graph index could reduce code duplication while preserving the same runtime model.
2. **Completion supplement audit.** The five promoted task routes and `snake_trap` may be candidates for a bounded task-effect derivation pass. This should be attempted only if the complete audit proves parity and keeps the two event-only stages separate.

Neither is required for correctness or performance today. The rejected universal external-provenance parser remains out of scope.

## Final design principle

The target is not “one condition at any cost.”

The target is:

> **one compact interaction engine, fed by the smallest set of distinct host-semantic evidence classes that Graveyard Keeper actually uses.**

1.1.6 is the first accepted version where the dialogue side reaches that level: the former Snake exception is now a derived instance of the general lifecycle rule.

# Unified interaction architecture audit — after accepted 1.0.35

Status: **research conclusion / no runtime change**.

Baseline reviewed: accepted stable **1.0.35**, `main` at promotion commit `c110082ed3610ef378a36be9a0bcd2c829919946`; exact accepted runtime source remains `5e8305ea6c2515dd0694343a07eb70e403ad0528`.

## Question

The accepted 1.0.35 research established a stronger semantic rule than the old `@` topic convention:

`currently reachable authored answer + exact self-consumption of that answer ID + satisfied authored gates -> reminder`

The implementation deliberately shipped as a narrow supplement so that new runtime behavior could be tested without destabilizing the accepted schema-2 architecture. Now that the complete six-weekday-NPC non-`@` universe has been audited and the player test passed, this document asks whether the full production architecture should be simplified around the stronger rule.

## Conclusion

**Yes.** The player-facing semantics are now more unified than the implementation.

The accepted 1.0.35 runtime is correct and should remain the stable baseline, but its structural/actionability layer contains historical duplication that is now justified to remove in a later candidate. The recommended target is **not** a universal quest/provenance parser. It is a single bounded parser/index for the same six weekday-NPC graphs, followed by several small evidence-specific derivation passes that compile one common runtime interaction model.

The UI/HUD layer does not need redesign. The architectural debt is concentrated in graph parsing, requirement binding, manifest ownership, and the number of parallel actionability representations.

## Current production shape

The main structural/runtime classes are approximately:

- `WeekdayInteractionRuleCache.cs` — 54,023 bytes;
- `NavigationReachabilityCache.cs` — 52,842 bytes;
- `NonAtSelfConsumingRuleCache.cs` — 51,239 bytes;
- `PersistentRuleManifest.cs` — 34,237 bytes;
- `VerifiedCompletionReminderRules.cs` — 10,211 bytes;
- `CalendarQuestsPinsPlugin.cs` — 22,426 bytes.

The first three parser/derivation classes alone account for 158,104 bytes of C# source and independently reconstruct overlapping views of the same six serialized graphs.

This does **not** mean the executable performs all of that work every gameplay tick. Accepted persistent-cache architecture keeps graph traversal out of gameplay. The concern is duplicated bootstrap logic, duplicated runtime binding/evaluation code, multiple persisted rule representations, and correctness drift between rule families.

## What is duplicated today

### 1. Raw graph indexing is implemented repeatedly

`WeekdayInteractionRuleCache`, `NavigationReachabilityCache`, and `NonAtSelfConsumingRuleCache` each maintain their own private graph types and parsing helpers around the same serialized FlowCanvas data, including substantial overlap in:

- node indexing (`BuildNodeIndex`);
- connection parsing (`ParseConnections`);
- `Flow_MultiAnswer` answer extraction;
- answer-slot port parsing;
- node content / JSON-string parsing;
- SmartRes requirement extraction;
- blacklist-add parsing;
- flow/value connection classification.

The non-`@` supplement additionally reconstructs a production-style task-completion census only to exclude answers already owned by the old task layer.

That is a strong sign that the stable domain concept is broader than the current class boundaries.

### 2. Exact self-consumption is split by identifier convention

The accepted product rule no longer considers `@` semantically special. Nevertheless:

- `WeekdayInteractionRuleCache` only admits generic self-consuming topics when the blacklisted ID begins with `@`;
- `NonAtSelfConsumingRuleCache` separately derives the same basic concept for non-`@` IDs;
- the two classes use different `Rule`/`TopicRule`, `Variant`, and `Requirement` representations;
- the non-`@` layer persists to a second cache file and separately binds Player/SmartRes runtime state.

After the 1.0.35 universe audit, this boundary is implementation history rather than a product/domain boundary.

### 3. Requirement evaluation has multiple owners

The accepted runtime delegates authored price/lock sufficiency to game-owned `Player.IsEnough(SmartRes)`, with a narrow authoritative zone-quality exception for verified owner-local mirrors.

However SmartRes creation/player binding/evaluation is reproduced in several places:

- `WeekdayInteractionRuleCache` owns the primary implementation;
- `NavigationReachabilityCache` reaches into `WeekdayInteractionRuleCache` private `CreateSmartRes` / `IsEnough` methods through reflection;
- `NonAtSelfConsumingRuleCache` owns another SmartRes factory/player binding/requirement representation;
- `VerifiedCompletionReminderRules` owns yet another tiny SmartRes implementation for `snake_trap` relation >= 10.

There should be one explicit runtime predicate/requirement service instead.

### 4. PersistentRuleManifest depends on private internals through reflection

`PersistentRuleManifest` reflects private fields and methods from `WeekdayInteractionRuleCache`, including target storage, save/player fields, parser entrypoints, world-object/SmartRes factories, and binding methods.

This has worked, but it is a fragile internal API: a private rename or representation cleanup can silently break manifest bootstrap/load wiring. A future architecture should serialize/bind an explicit compiled rule-set object rather than reflect into another class's implementation details.

### 5. Tick has several actionability branches

Accepted 1.0.35 `Tick()` effectively evaluates these rule families independently:

1. normal owner-task rules;
2. `VerifiedCompletionReminderRules` owner-task supplements;
3. persisted `@` exact-self-consuming one-shots;
4. non-`@` exact-self-consuming supplement;
5. cross-owner task rules.

The distinction between owner-task and cross-owner is useful provenance, but not a fundamentally different runtime rule shape: both are a task-visible activation condition attached to an interaction with the target weekday NPC.

Likewise `@` and non-`@` exact self-consumption are now proven to be the same semantic class with one phrase-state difference: `@` answers additionally require the authored unlocked-phrase state.

## What should remain separate

### UI / calendar rendering

`CalendarMarkers`, `NativeMarkerSprites`, and the weekday/sin mapping are a separate concern and are already appropriately isolated. They should consume marker sets and know nothing about quest graph provenance.

### Loading readiness

`LoadingCachePrewarmGate` is small and conceptually separate. Its duplicated six-NPC ID list/world-object lookup can later be supplied by a shared runtime context, but it should remain a readiness gate rather than become part of the graph compiler.

### Root-to-answer navigation derivation

Navigation is a distinct derivation problem. It should **not** be collapsed into task semantics. What should be shared is the parsed graph index. Navigation should remain a separate pass that compiles root-to-answer path predicates from the common index.

### True mandatory interaction-event stages

`npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back` remain qualitatively different from a selectable final answer. Current evidence says the visible task itself is the verified stage boundary for a later automatic interaction event.

Do not force these into a fake AnswerId merely to make the type hierarchy uniform. Unless a broader event-stage contract is separately proved, retain a very small explicit verified-event rule family.

## Recommended target architecture

### A. One bootstrap-only `WeekdayGraphIndex`

Parse each of the six weekday-NPC serialized graphs once into a shared ephemeral index containing only directly observed graph facts, for example:

- nodes: ID, type, serialized position, function UID/source-output UID where applicable;
- connections with flow/value classification;
- numbered `Flow_WaitForFlow` flow inputs;
- MultiAnswer menus/answer slots;
- AnswerData gate inputs;
- exact blacklist add/remove effects;
- task-state effects;
- CustomFunction Call <-> Event UID edges;
- exact FireEvent/CustomEvent edges only where their contract is verified;
- authored zone-quality mirror edges.

This is **not** a generic interpreter. It is a compact index over the same local six graphs production already parses.

`WeekdayInteractionRuleCache`, navigation derivation, and non-`@` discovery should stop reparsing raw JSON independently.

### B. One serializable `RequirementSpec` + one runtime evaluator

Use one requirement representation everywhere:

- `ResType`;
- `Id`;
- `Value`;
- optional verified authoritative `ZoneId`.

A single runtime service should:

- bind the live Player and weekday-NPC WGO references;
- create/recreate SmartRes objects;
- evaluate `Player.IsEnough`;
- evaluate the narrow authoritative zone-quality rule;
- fail closed when binding is unsupported.

Navigation predicates, final-answer gates, non-`@` gates, and any verified supplemental route should all use this service.

### C. Compile one common interaction model

A useful conceptual shape is:

`CompiledInteraction`

- target weekday NPC ID;
- optional `TaskSource` (`ownerNpcId`, `taskId`);
- optional final `AnswerId`;
- consumption/phrase-state contract for the final answer;
- final gate variants;
- one or more compiled navigation paths/predicates;
- marker style/category provenance;
- evidence/source kind for diagnostics and integrity counts.

The exact data layout can be optimized later. The important semantic contract is that runtime actionability becomes one operation over compiled interactions instead of several unrelated class-specific checks.

### D. Derivation passes over the common index

Keep evidence discovery modular:

1. **Task-completion pass**
   - owner-local and cross-owner are emitted by the same pass;
   - owner identity is data, not a separate rule type;
   - use exact verified flow topology, including numbered WaitForFlow and exact CustomFunction UID jumps;
   - if separately verified, exact FireEvent -> CustomEvent matching can be included to absorb `snake_trap`-style routes.

2. **Exact-self-consumption pass**
   - no `@`/non-`@` split;
   - candidate requires selected answer X -> persistent blacklist add X;
   - blacklist removal/reversible cases fail closed unless explicitly supported later;
   - answers already owned by task completion are not emitted as duplicate generic one-shots;
   - `@` only affects the runtime phrase predicate (`RequireUnlocked=true`), not candidate class membership.

3. **Navigation pass**
   - derives root-to-final-answer paths from the same index;
   - compiles only predicates actually required by candidate interactions;
   - unconditional ancestors compile away as today.

4. **Verified mandatory-event pass/table**
   - initially only exact evidence-backed event-only stages;
   - no broad `Visible task`, `CustomEvent`, or `AddInteractionEvent` inference without new evidence.

### E. One manifest, likely schema 3

A later refactor candidate should prefer one file again:

`BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`

Possible schema-3 payload:

- compiled interactions grouped by target NPC;
- shared requirement specs;
- compiled navigation predicates/path data referenced by interaction;
- integrity counts/contracts;
- no second non-`@` cache.

The old schema-2 and 1.0.35 supplemental cache can simply be rejected as incompatible at loading and rebuilt behind the loading screen. No gameplay migration parser is needed.

### F. One runtime evaluation path

Conceptually:

1. verify interaction source is active (for example, referenced task is Visible; generic one-shot has no task source);
2. if final answer exists, verify required unlocked/not-blacklisted phrase state;
3. evaluate final AnswerData gates;
4. evaluate one compiled navigation path;
5. emit the interaction's marker style.

A true mandatory-event rule can use the same outer `CompiledInteraction` model with no `AnswerId` and a verified source predicate; it should not fabricate dialogue semantics.

The plugin loop then iterates compiled interactions grouped by weekday NPC rather than separately walking owner tasks, topics, the non-`@` cache, verified supplements, and cross-owner tasks.

## Expected benefits

### Correctness / maintainability — high confidence

- the actual accepted semantic boundary (exact self-consumption) exists in one place;
- no future `@`/non-`@` drift;
- task and generic dialogue duplicate ownership is decided once during compilation;
- one requirement implementation prevents gate semantics from diverging;
- explicit manifest/runtime APIs remove private reflection coupling;
- integrity checks can describe one compiled universe instead of several overlapping counts.

### Source / binary size — likely meaningful, must be measured

Three current structural classes each contain large overlapping raw-graph parsers. A common index should delete substantial duplicated C# and likely reduce IL/DLL size. Do not set a numerical target before implementing and building a parity candidate.

### Cold-cache loading cost — likely lower, must be measured

Today a missing primary manifest and missing non-`@` supplement can cause overlapping parses of the same six graphs. A common index would parse each serialized graph once, then run cheap derivation passes over in-memory nodes/connections.

The accepted steady-state path is already cheap, so the expected loading win is more credible than a large gameplay-FPS win.

### Steady-state gameplay performance — probably small improvement

There may be small savings from:

- one candidate loop instead of multiple rule-family loops;
- one Player/SmartRes runtime binder;
- one persisted cache lifecycle;
- fewer redundant reflection calls/bind checks.

However 1.0.28 already removed the proven recurring performance problem. Do not claim a meaningful FPS/hitch improvement without measurement.

## Risks

A consolidation refactor touches accepted behavior across all rule families at once. The main risk is not speed; it is silently losing an interaction class or changing duplicate/marker-style semantics.

Therefore the refactor should not be committed directly to stable `main` and should not rely on aesthetics as evidence.

## Recommended implementation method

If implemented, use a future build-bearing development line (for example `dev/1.0.36`) with these gates:

1. Preserve 1.0.35 as frozen stable baseline.
2. Build the common graph index and compiler on the dev branch only.
3. Add a **static parity audit** comparing the new compiled set against the accepted 1.0.35 primary + non-`@` caches for all currently supported answer-backed rules.
4. Explicitly account for the two mandatory event-only stages rather than expecting set equality to explain them accidentally.
5. Keep verified fail-closed cases fail-closed (`@souls_s_s33_ask` remains the known control unless new evidence resolves it).
6. Measure cold-cache bootstrap time and subsequent manifest-load time.
7. Verify normal gameplay still performs no FlowCanvas traversal and does not reintroduce one-second/30-second allocation pressure.
8. Hand out a numbered DLL only after clean Release build and parity evidence.
9. Player regression test should cover at minimum:
   - a normal item/resource-gated task interaction;
   - a nested dialogue parent gate (Charmel control);
   - one generic exact-self-consuming `@` topic;
   - one generic exact-self-consuming non-`@` topic;
   - one indirect completion route promoted in 1.0.32;
   - one mandatory interaction-event stage when naturally reachable, or retain exact accepted mapping if not reproducible.
10. Promote only after explicit player acceptance.

## What not to do

- Do not reintroduce the rejected universal provenance parser across arbitrary quest/NPC/dependency graphs.
- Do not infer interaction semantics from translated/display text.
- Do not generalize all visible tasks, CustomEvents, FireEvents, or AddInteractionEvents.
- Do not make the HUD/UI depend on quest/compiler internals.
- Do not optimize the accepted one-second steady-state path speculatively; preserve its allocation-light behavior and measure any change.

## Final recommendation

Keep **1.0.35** as the stable release. Treat its separate non-`@` cache as a deliberately safe transitional implementation that was appropriate before the universe rule was proven and runtime-tested.

The next architectural improvement should be a **bounded unified interaction compiler**: one six-NPC graph index, one requirement/runtime binding service, modular evidence derivation passes, one compiled interaction representation, and one persisted manifest. This is materially simpler and less fragile than the current layered implementation while preserving the evidence-driven boundary that made the mod reliable.
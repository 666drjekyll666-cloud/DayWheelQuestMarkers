# Day Wheel Quest Markers 1.1.0 — unified interaction parity candidate

Status: **candidate / awaiting player validation / do not merge or release**.

Stable baseline remains **1.0.35**. Exact accepted runtime source: `5e8305ea6c2515dd0694343a07eb70e403ad0528`.

Candidate exact executable/build source: `67e693a63089d473920e5181eec7af75e6f1be75`.

## Final audit conclusion

The post-1.0.35 architecture/parity audit found no fourth reminder semantic class. Accepted behavior reduces to:

1. task-linked actionable interaction with a weekday NPC;
2. authored exact-self-consuming dialogue interaction;
3. the two exact verified mandatory interaction-event stages retained in `VerifiedCompletionReminderRules`.

Navigation reachability, phrase/blacklist state, SmartRes requirements, live authoritative zone-quality mirrors, and marker style are predicates/metadata on those interactions rather than additional reminder classes.

The `@` prefix is not a semantic one-shot boundary. The accepted 1.0.35 universe proved that non-`@` answers can use the same persistent exact-self-blacklist semantics. The separate `NonAtSelfConsumingRuleCache` was therefore a safe transitional implementation, not a durable product boundary.

## Deliberately narrow 1.1.0 scope

1.1.0 does **not** attempt the full long-term graph-index rewrite documented in `docs/UNIFIED_INTERACTION_ARCHITECTURE_AUDIT.md`.

This candidate performs only the highest-confidence consolidation:

- `@` and non-`@` exact-self-consuming interactions compile into the same `WeekdayInteractionRuleCache.TopicRule` representation;
- both categories use the same runtime `target.Topics -> NavigationReachabilityCache.IsTopicActionable` path;
- the separate non-`@` runtime cache, runtime binder, gameplay loop, and binary cache file are removed from production;
- one schema-3 `rules-1.407.bin` persists primary rules, unified self-consuming topics, navigation data, and non-`@` integrity census;
- accepted owner/cross task semantics, navigation compiler semantics, authoritative-zone behavior, marker rendering, refresh cadence, and exact verified completion/event supplements are intentionally not broadened.

This limits the regression surface while still removing the semantic split proven obsolete by 1.0.35.

## Bootstrap parity contract

The schema-3 bootstrap fails closed unless the accepted primary universe is preserved:

- owner rules: **75 supported / 6 unsupported**;
- cross-owner tasks: **8 / 6 supported / 0 unsupported**;
- legacy `@` exact-self-consuming topics, after subtracting the newly compiled non-`@` layer: **55 topics / 54 supported variants / 1 unsupported variant**.

The non-`@` compiler additionally requires the accepted complete universe:

- six weekday-NPC graphs;
- **77** unique non-`@` answer IDs;
- **19** exact-self-blacklisting candidates;
- **0** reversible candidates;
- **0** utility-like candidates;
- admitted non-task topics plus task-completion-excluded exact-self answers must total **19**.

The stronger reverse traversal required by the accepted 1.0.35 evidence — numbered `Flow_WaitForFlow` inputs and exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` jumps — is used only by non-`@` exact-self discovery. The established owner/cross task classifier is not silently broadened.

## Cache lifecycle

The cache path remains:

`BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`

Schema changes from 2 to 3. A 1.0.35 schema-2 file is rejected during the verified loading window and rebuilt as schema 3. No gameplay graph parser is introduced and the player does not need to delete the old file manually.

The old 1.0.35 `non-at-self-consuming-1.407.bin` file, if present, is ignored by 1.1.0. It may remain on disk without affecting runtime behavior.

## Static source comparison

Compared with stable `main` 1.0.35, exact candidate source `67e693a...`:

- removes `src/NonAtSelfConsumingRuleCache.cs` (**1,073 deleted lines**);
- adds `src/UnifiedSelfConsumingCompiler.cs` (**734 lines**);
- simplifies `CalendarQuestsPinsPlugin.cs` by removing the second non-`@` runtime/cache lifecycle;
- updates `PersistentRuleManifest.cs` to schema 3 and integrated integrity metadata;
- leaves `NavigationReachabilityCache.cs`, `VerifiedCompletionReminderRules.cs`, UI classes, and accepted primary task parser unchanged.

The candidate raw DLL is **92,672 bytes**, down from accepted 1.0.35's **98,816 bytes** (6,144 bytes / about 6.2%). This is evidence of reduced compiled duplication, not a claimed gameplay-performance improvement.

## Build identity

- Branch: `dev/1.1.0`
- Candidate ref: `candidate/1.1.0`
- Exact executable/build source: `67e693a63089d473920e5181eec7af75e6f1be75`
- CI run: `34789944561`
- CI job: `103812189255`
- Result: success; Release build **0 warnings / 0 errors**
- Artifact: `DayWheelQuestMarkers-1.1.0` (`10328230822`)
- Artifact ZIP digest: `sha256:7d7854d837cd44aef2f4704bda135ced7f682b8b3050d77fd7b15e607a53e488`
- Raw DLL: **92,672 bytes**
- Raw DLL SHA-256: `29d6118fdd1c981c947836181c486fbac52caa39c72b6cce9d3694af85401ab9`
- Build workflow restored to manual-only afterward at dev commit `66d2d4f84b1ffd95a01f63910d48bc4847fd3588`; no second build was required.

## Required player validation

### Launch 1 — schema-2 -> schema-3 bootstrap

Install 1.1.0 over 1.0.35 and **do not delete either existing cache file**.

Expected:

- `Day Wheel Quest Markers 1.1.0 loaded`;
- the existing schema-2 primary manifest is rejected as incompatible during loading and schema 3 is bootstrapped there, not in gameplay;
- no Day Wheel error/warning after successful bootstrap;
- owner/cross accepted counts remain unchanged;
- unified self-consuming and non-`@` census satisfy the parity guards above;
- navigation remains at its accepted universe unless runtime evidence proves otherwise;
- existing markers behave normally, with no implausible extra weekday markers.

If naturally available, exercise any one ordinary task-gated interaction, one `@` exact-self-consuming conversation, and one non-`@` exact-self-consuming conversation. Do not alter or roll back a save solely to manufacture these cases.

### Launch 2 — persisted schema-3 restore

Restart the game without touching the cache.

Expected:

- schema-3 interaction manifest loads directly;
- log says FlowCanvas graph parse was skipped;
- the same rule/census/navigation counts are restored;
- marker behavior remains unchanged.

The two-load lifecycle is the primary acceptance gate for this architecture candidate.

## Known watchpoint

Schema-3 bootstrap injects supported non-`@` topics before `NavigationReachabilityCache.BuildTarget`, so navigation's required-answer validation now sees the unified topic set. This is intentionally fail-closed and should succeed because navigation already indexes all authored answers. If runtime bootstrap reports `no interaction-root navigation path for required answer ...`, do not broaden the classifier; diagnose that exact answer against accepted 1.0.35 behavior.

## Promotion rule

Do not merge to `main`, create an accepted baseline, or publish `v1.1.0` until the player explicitly accepts this candidate after runtime validation.

# Unified Interaction Architecture — accepted 1.1.3

Status: **accepted stable architecture for Graveyard Keeper 1.407**.

## Why 1.1.3 exists

The accepted 1.0.35 semantics were broader than its implementation: persisted `@` one-shots and verified non-`@` exact-self-consuming answers represented the same kind of reminder, but used separate production caches.

The 1.1 line consolidated those semantics. Runtime testing exposed two defects before acceptance:

1. 1.1.0 treated an internal root-unreachable exact-self answer (`snake_back_12a`) as a fatal required navigation target, invalidating the whole manifest.
2. 1.1.1 admitted Astrologer `astrologer_diary_9a` and `astrologer_diary_9b` as independent generic reminders even though both are reachable only after the already-reminded diary hand-in. This produced three markers for one NPC visit.

1.1.3 is the accepted correction.

## Accepted semantic boundary

A generic exact-self-consuming dialogue answer is independently reminder-worthy only when:

- its selected authored route persistently blacklists that exact answer ID;
- its supported final AnswerData gates pass;
- it has at least one authored interaction-root path that is currently reachable;
- that independent path does **not** first pass through an already task-owned answer.

This preserves ordinary nested menus while preventing a chain of follow-up choices inside one visit from inflating the weekday marker count.

Task-linked rules remain the authoritative source for answers already owned by owner/cross task progression. The two verified mandatory event-only stages remain narrow explicit mappings.

## Production architecture

- `WeekdayInteractionRuleCache`: accepted owner-local, cross-owner, and persisted `@` rule derivation.
- `NavigationReachabilityCache`: compact root-path predicates plus independent-root-path evidence.
- `UnifiedSelfConsumingCompiler`: bootstrap-only derivation of the audited non-`@` exact-self universe into normal `TopicRule` objects.
- `PersistentRuleManifest`: schema 4 unified persistence.
- `VerifiedCompletionReminderRules`: retained narrow completion/event supplement.
- `CalendarMarkers` / `NativeMarkerSprites`: unchanged game-native HUD layer.

The old `NonAtSelfConsumingRuleCache` runtime layer is retired. Its historical `non-at-self-consuming-1.407.bin` file is ignored by 1.1.3.

## Accepted integrity/runtime counts

- owner rules: 75 supported / 6 unsupported
- cross-owner tasks: 8 total / 6 supported / 0 unsupported
- unified self-consuming topics: 61 total / 60 supported / 1 unsupported
- non-`@` universe: 77 unique IDs
- non-`@` exact-self: 19
- non-`@` admitted independent topics: 6
- task/completion-owned exclusions: 9
- remaining non-independent/root-unreachable exclusions: 4
- navigation: 210 answers / 270 paths / 151 predicates / 0 unsupported paths

## Cache and performance

Canonical cache:

`BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`

Schema 4 intentionally invalidates schema 3. No manual cache deletion is required.

Accepted player evidence:

- schema-4 bootstrap after upgrade: 900.79 ms, behind loading;
- full restart: schema-4 load 10.42 ms;
- cached restart explicitly logged `FlowCanvas graph parse skipped`;
- no gameplay FlowCanvas traversal was added.

## Acceptance case

Before the Astrologer diary hand-in, 1.0.35/1.1.1 could display three markers from:

- owner task `astrologer_diary`;
- generic `astrologer_diary_9a`;
- generic `astrologer_diary_9b`.

Accepted 1.1.3 displays exactly one marker. After the diary chain advances, the player observed exactly one marker for the executable Acid hand-in while Restoration Tools were absent.

This validates the intended product meaning:

> A marker represents an independently actionable reason to visit that weekday NPC now, not every one-time dialogue choice that may occur later inside the same visit.

Do not use dialogue-log `fh=True` alone as proof that every live resource/item gate is satisfied.

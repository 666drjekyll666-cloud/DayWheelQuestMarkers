# 1.0.24 Unified Cache Refactor

## Goal

Replace the accepted 1.0.23 loading architecture's duplicated graph parsing with one bounded structural cache while preserving the product rule and steady-state behavior.

Accepted 1.0.23 independently parsed the same weekday-NPC graphs in:

- `QuestRuleCache`;
- `CrossOwnerRuleCache`;
- `OneShotDialogueRuleCache`.

It also retained `VerifiedBridgeReminderRules`, `VerifiedIntermediateReminderRules`, and reflection-based `SessionCacheRebinder` as transitional controls.

On the accepted developed-save runtime sample, 1.0.23 loading prewarm took **782.89 ms**. This is the comparison baseline for 1.0.24; no performance improvement is considered proven until runtime measurement.

## 1.0.24 structure

`WeekdayInteractionRuleCache` replaces the three structural caches and the rebinder.

For each of the six weekday NPC serialized graphs, one build pass creates:

- node index;
- connection list;
- incoming flow map;
- incoming value map.

The same parsed graph then derives three rule families:

1. **Owner-local task completion**
   - same visible-task requirement as 1.0.23;
   - same authored completion anchors;
   - same phrase/blacklist filtering;
   - same supported SmartRes price/lock reconstruction;
   - preserves the accepted authoritative live-zone-quality mirror resolver only for this rule family.

2. **Cross-owner task completion**
   - same explicit foreign `NPC id` + task requirement as 1.0.23;
   - same visible owner-task requirement;
   - same phrase/blacklist and SmartRes gate evaluation;
   - no new zone-quality semantics are introduced.

3. **One-shot dialogue**
   - exact persisted `@topic`;
   - its own authored route must add that exact topic to the phrase blacklist;
   - `remove=true` blacklist operations are not consumption evidence;
   - direct task-completion answers are excluded to prevent duplicate markers;
   - supported answer price/lock gates remain evaluated through `Player.IsEnough`;
   - no translated/display-text matching.

## Removal of transitional manifests

1.0.23 deliberately excluded the previously verified bridge/intermediate topic IDs from generic one-shot classification so the new one-shot rule could be tested without simultaneously deleting accepted controls.

That rollout is now accepted. 1.0.24 removes:

- `VerifiedBridgeReminderRules`;
- `VerifiedIntermediateReminderRules`;
- their exclusion list from generic one-shot classification.

The affected authored topics were already established by static project evidence as exact self-consuming topics. Under the accepted product rule, their currently actionable authored conversation is sufficient reminder evidence even without separately proving a downstream journal-task dependency.

This is intentionally **not** a return to the rejected universal provenance parser. 1.0.24 does not search arbitrary quest dependency chains; it only consolidates the same local graph structures already accepted in production.

## Session/cache lifecycle

The unified cache owns:

- player binding;
- save binding;
- periodic target WGO references;
- owner KnownNPC references;
- cross-owner KnownNPC references;
- known-NPC count for structural staleness.

`SessionCacheRebinder` is removed. Compatible reloads call the unified cache's direct `TryRebind` method.

The plugin also compares the complete known-NPC signature on the existing slow structural cadence, avoiding the old same-count set-change blind spot without introducing broad recurring graph work.

## Source simplification

Removed from the 1.0.24 compile:

- `QuestRuleCache.cs` — 689 lines;
- `CrossOwnerRuleCache.cs` — 666 lines;
- `OneShotDialogueRuleCache.cs` — superseded by unified cache;
- `SessionCacheRebinder.cs` — 107 lines;
- `VerifiedBridgeReminderRules.cs` — 224 lines;
- `VerifiedIntermediateReminderRules.cs` — 320 lines.

The new unified cache centralizes the shared parser/helpers instead of maintaining three copies.

## Required candidate validation

Before acceptance, 1.0.24 must prove:

1. Release build succeeds from frozen exact source.
2. Current known portal-item state remains correct (Bishop/Merchant reminders; consumed Inquisitor one-shot remains absent unless another independent action exists).
3. At least one ordinary task-linked marker still behaves correctly.
4. No obvious Trade/Leave/Back or submenu-header false positive appears.
5. A consumed one-shot still disappears on refresh.
6. Runtime log reports the unified-cache counts and loading prewarm duration.
7. Compare prewarm against the accepted 1.0.23 **782.89 ms** sample; improvement is expected from eliminating repeated graph parsing but must be measured, not assumed.

`main` remains 1.0.23 until explicit player acceptance.

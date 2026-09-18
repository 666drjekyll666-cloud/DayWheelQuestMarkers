# Changelog

## 1.1.3

- Consolidates the previously separate non-`@` exact-self-consuming dialogue supplement into the same persistent interaction manifest used by the rest of the reminder system.
- Fixes duplicate weekday markers when a one-time follow-up choice is reachable only after completing an already-reminded task interaction; those choices are now treated as part of the same NPC visit.
- Fixes the 1.1.0 schema-3 bootstrap regression where an internal exact-self answer with no independent interaction-root path could invalidate the entire manifest.
- Upgrades the persistent cache to schema 4. Existing cache files are rebuilt automatically during loading; no manual cache deletion is required.
- Keeps gameplay-time FlowCanvas parsing disabled. Verified schema-4 reload completed in 10.42 ms with graph parsing skipped.
- Verified in-game on the Astrologer diary sequence: one marker appears for the diary hand-in instead of three; after completing it, the marker count continues to follow the actually executable next interaction and its live item gate.

## 1.0.35

- Generalizes authored one-time dialogue reminders beyond the `@` identifier convention: a non-`@` answer may now produce a reminder when its own authored branch persistently blacklists that exact answer ID.
- Replaces the tactical 1.0.33 Astrologer and 1.0.34 Snake fixes with one evidence-backed structural rule derived from the complete six-weekday-NPC Graveyard Keeper 1.407 census.
- The audited non-`@` universe contains 77 unique answer IDs and exactly 19 exact-self-consuming candidates, with 0 reversible and 0 utility-like `Leave`/`Back`/`Trade` candidates.
- Preserves authored price/lock requirements, nested-dialogue navigation reachability, and fail-closed behavior; exact CustomFunction jumps and numbered WaitForFlow inputs are handled only during loading-time derivation.
- Persists the new structural supplement under `BepInEx/cache/DayWheelQuestMarkers/non-at-self-consuming-1.407.bin`; the existing schema-2 `rules-1.407.bin` remains valid and normal gameplay still performs no FlowCanvas graph traversal.
- Verified in-game: Snake `snake_1a` produced a marker while its 5-Faith requirement was satisfied and disappeared after the interaction consumed it; a simultaneous Ms. Charm marker disappeared when spending those 5 Faith made her own authored gate unsatisfied.

## 1.0.32

- Adds verified reminder coverage for owner-local completion routes that cross WaitForFlow, exact CustomFunction boundaries, FireEvent -> CustomEvent, or mandatory interaction-event stages.
- Fixes the Inquisitor `inquisitor_talk` stage: the weekday marker now appears before the mandatory interaction and disappears after the scene completes the task.
- Adds narrow verified coverage for Snake `snake_key`, `snake_trap`, and the later mandatory `snake_back` interaction stage, while preserving their authored item/relation/navigation prerequisites.
- Reuses the existing schema-2 manifest, one-shot topic rules, and navigation predicates; no new gameplay-time FlowCanvas parsing or cache regeneration is introduced.
- Keeps unsupported `@souls_s_s33_ask` fail-closed rather than guessing its AnswerData semantics.
- Verified on the current save: after `inquisitor_talk` completed, its marker disappeared; after the next Inquisitor resource prerequisite was satisfied, the normal prerequisite-aware marker appeared again.
- Verified existing manifest load in 10.80 ms with graph parsing skipped and canonical rule/navigation counts unchanged.

## 1.0.30

- Fixes false weekday markers caused by nested dialogue children being evaluated without checking whether their parent menu path is actually reachable.
- Adds a general root-to-answer reachability layer shared by owner-task, cross-owner-task, and one-shot dialogue reminders instead of keeping a Charmel-specific exception.
- Persists compact navigation predicates together with the structural rule manifest so normal gameplay never traverses FlowCanvas dialogue graphs.
- Keeps the established final-rule counts and game-owned gate checks while failing closed on unsupported navigation ancestry.
- Preserves the allocation-light steady-state path that removed the previous roughly 30-second rhythmic hitch pattern.
- Verified first schema-2 bootstrap behind loading at 557.88 ms; subsequent full restart loaded the persistent manifest in 10.29 ms with `FlowCanvas graph parse skipped`.
- Verified the reported early Charmel state no longer shows the two false Lust-day markers.

## 1.0.24

- Consolidates weekday-NPC quest and dialogue discovery into one unified loading-time cache.
- Parses each of the six weekday-NPC dialogue graphs once instead of reparsing them through separate owner, cross-owner, and one-shot caches.
- Removes the transitional hard-coded bridge/intermediate reminder manifests; those verified one-time interactions now use the same authored self-consuming dialogue rule as other one-shot reminders.
- Preserves owner-local and cross-owner task handling, supported resource/relation gates, native marker categories, and authoritative live zone-quality handling where previously verified.
- Reduces measured developed-save cache prewarm from 782.89 ms in 1.0.23 to 308.07 ms in the accepted regression test, with no recurring graph parsing during normal gameplay.

## 1.0.23

- Broadens reminders from only task/progression-proven interactions to any currently actionable authored one-time dialogue with a weekday NPC.
- Detects one-shot dialogue structurally through the game's exact self-blacklist behavior rather than translated/display text.
- Keeps repeatable utility/menu choices such as Trade, Leave, Back, and non-consuming submenu headers out of the reminder set.
- Preserves existing owner-local, cross-owner, bridge, and verified intermediate quest reminder handling.
- Uses the game's own gate checks for supported item/resource/relation requirements and fails closed on unsupported structures.
- Keeps graph discovery in the loading-screen prewarm; normal gameplay continues to use cached rules and low-frequency refresh work.

## 1.0.21

- Initial public release.
- Adds actionable quest markers to the weekday wheel for the six vanilla weekday NPCs.
- Supports verified owner-local and cross-owner objectives plus the Miller → Astrologer and Astrologer → Snake bridge chains.
- Uses the game's quest-marker categories and colors.
- Supports multiple simultaneous actionable objectives on the same weekday.
- Preserves correct marker placement as the weekday wheel rotates and across normal HUD/menu transitions.
- Keeps normal gameplay overhead low through cached quest structure and bounded refresh work.
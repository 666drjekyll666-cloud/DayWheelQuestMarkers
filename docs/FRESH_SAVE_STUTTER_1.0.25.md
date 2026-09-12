# Fresh-save stutter investigation — 1.0.25

## Player report

On a newly created Graveyard Keeper 1.407 save with the normal mod set, the player reports intermittent gameplay hitches, especially around dialogue/progression events but also outside dialogue.

## Current-log evidence

The supplied 1.0.24 runtime log starts correctly on the fresh-save empty path (`owner=0`, `cross-owner=0`, `one-shot=0`). Once Bishop becomes the first known weekday NPC, the unified cache becomes non-empty. After that point, newly learned NPCs repeatedly coincide with another `Ready. Unified cache` cycle.

Observed additions after Bishop include:

- `npc_guard_2` -> same rule counts as before;
- `npc_tavern owner` -> cross-owner count increases, so this addition is structurally relevant to the current 1.0.24 build model;
- `npc_guard_torch` -> same rule counts;
- `npc_mrs chain` -> same rule counts;
- `npc_blacksmith` -> same rule counts;
- `mf_stones_1` -> same rule counts.

Thus at least five post-Bishop cache rebuilds in the supplied session were provably unnecessary: the resulting structural rule counts did not change.

## Root cause in 1.0.24

`WeekdayInteractionRuleCache.Build` stores the total count of every `known_npcs` entry. `NeedsRebuild` then treats any total-count change as structural invalidation. `CalendarQuestsPinsPlugin.Tick` responds by synchronously calling the full graph `Build` on a gameplay tick.

This couples two different kinds of state:

- immutable/slow structural state: the six weekday-NPC FlowCanvas graphs and their authored routes;
- dynamic save state: which NPCs the player currently knows.

A new unrelated NPC therefore causes all six weekday graphs to be reparsed even though their structure has not changed.

The build also conditionally omits owner-local/one-shot rules when a weekday target is not yet known and omits cross-owner rules when their foreign owner is not yet known. That forces structural reparsing as the known-NPC set grows.

## Prior accepted performance evidence

Historical Runtime Probe 0.1.17 measured the old synchronous structural cache path at about 500 ms per developed-save build (`512.73 ms` first load, `497.54 ms` repeat). First actionability evaluation was only `2.53 ms` and repeat HUD work `0.05 ms`. The canonical conclusion was that graph parsing, not steady-state actionability checks, caused the visible stop.

Historical 1.0.7–1.0.12 work also established that fresh-save repeated graph work can cause player-visible hitches and that moving structural parsing under the loading screen makes both new-game and developed-save startup smooth.

## Second fresh-save regression

1.0.24 resolves native game marker sprites through `Resources.FindObjectsOfTypeAll<Sprite>()`. Normal developed-save prewarm performs this under the loading screen. A fresh save currently skips prewarm until a weekday NPC is known, so the first real marker can instead pay this broad sprite lookup during gameplay.

This is a separate one-time cost from graph parsing. It should also be moved into the verified loading window, without embedding copied game assets.

## 1.0.25 design boundary

1.0.25 is a behavior-preserving performance change only:

1. Parse the six weekday-NPC graphs independently of the current `known_npcs` membership.
2. Keep `KnownNpc` references as dynamic nullable bindings; owner/cross-owner/topic visibility still requires the same live save state as 1.0.24.
3. On known-NPC changes, cheaply rebind references instead of reparsing graph structure.
4. Reserve full graph rebuilds for actual structural invalidation (dead/replaced runtime graph objects or a failed safe rebind).
5. Allow loading-screen structural and native-sprite prewarm on a fresh save before the first weekday NPC is known, once the existing readiness gate proves player plus all six serialized graphs are present.
6. Keep the one-second actionability cadence, 30-second low-frequency structural/signature validation, marker styles, gates and fail-closed policy unchanged.
7. Do not change HUD attachment/discovery in this iteration. Historical first-HUD cost is a separate factor and should be measured only if a residual one-time hitch remains after removing graph/sprite regressions.

## Acceptance test

Use an early/new save and play naturally through introductions after Bishop (guards, tavern owner, Mrs Chain, blacksmith if reached). Expected result:

- no repeated heavy graph parse when unrelated NPCs become known;
- no obvious dialogue-linked hitch attributable to Day Wheel;
- weekday reminders still appear/disappear correctly;
- runtime log shows one loading structural prewarm, cheap known-NPC rebinding, and no gameplay structural rebuild unless a real structural invalidation occurs.

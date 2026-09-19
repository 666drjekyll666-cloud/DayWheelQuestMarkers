# Interaction Universe Coverage Audit — accepted 1.1.6 baseline

Status: **research/tooling evidence only; production runtime unchanged**.

## Scope

This audit covers the six weekday-NPC authored dialogue graphs used by Day Wheel Quest Markers on Graveyard Keeper 1.407:

- Astrologer;
- Inquisitor;
- Snake/Cultist;
- Merchant;
- Ms Charm/Actress;
- Bishop.

The accepted raw snapshot records all authored `Flow_MultiAnswer` answer occurrences plus all observed task-state, `CustomEvent`, `Flow_AddInteractionEvent`, and `Flow_RemoveInteractionEvent` nodes in those six graphs. It is deliberately broader than the production reminder compiler.

It does **not** claim that every world interaction mechanism for every NPC in Graveyard Keeper is a dialogue `MultiAnswer`. The coverage contract is intentionally bounded to the weekday-NPC graph universe relevant to this mod.

## Accepted raw universe

Read-only probe 0.1.2 established:

- 243 authored answer occurrences;
- 224 unique `NPC + answer ID` pairs;
- 150 task-state transitions;
- 66 `CustomEvent` nodes;
- 19 `AddInteractionEvent` nodes;
- 4 `RemoveInteractionEvent` nodes;
- 72 owner-local task completion routes;
- 270 production-derived navigation paths.

Of the 224 unique answer IDs:

- 210 have a normal interaction-root navigation path;
- 14 do not.

## 0.1.3 no-root topology audit

Read-only probe 0.1.3 traced upstream and downstream topology for exactly those 14 no-root answers.

All 14 are entered from a `CustomEvent` / scripted event path, not from a fresh player-initiated weekday-NPC interaction:

- Inquisitor root node 95 / `first_meet_under_mountains`: 7 answers;
- Inquisitor root node 343 / `on_came_to_mountain_for_witch_burning`: 2 answers;
- Inquisitor root node 1711 / `inquisitor_after_dark_event` (the `player_brings_three_dark_org` branch): 3 answers;
- Snake root node 318 / `player_back_to_cultist`: 2 answers.

Accepted classification for all 14:

`EVENT_INVOKED_NON_REMINDER`

This is not a heuristic based on answer names. The classification is based on the verified event-root topology.

Notable controls:

- `inquisitor_2_9a` creates `inquisitor_talk = Visible` *after* the event choice; the choice itself is therefore not an independent reminder source.
- `snake_back_12a` / `snake_back_12b` are choices inside `player_back_to_cultist`; their paths blacklist the choice phrases and then open `snake_blood = Visible` in the same scripted visit.

## Coverage watchdog

For the accepted 1.1.6 / GK 1.407 baseline:

`224 unique answer IDs = 210 navigation-backed + 14 explicitly event-invoked non-reminders`

Therefore:

`UNKNOWN = 0`

The validator must fail if:

1. the raw accepted answer universe changes;
2. the navigation-backed set changes;
3. the exact no-root set changes;
4. any no-root answer loses its accepted event-root contract;
5. any no-root answer is not explicitly classified;
6. any unexpected classification appears.

This converts the previous implicit blind spot into an explicit reviewed boundary.

## What this does and does not prove

This coverage closes the known structural-answer blind spot that allowed an authored branch to exist outside the production navigation set without explanation.

It does **not** replace runtime behavioral testing of marker evaluation, save/load lifecycle, UI recreation, or exact gate semantics. Those remain separate regression dimensions.

Production Day Wheel Quest Markers 1.1.6 is unchanged by this research.

# Exhaustive Interaction Validation Harness

Status: **research/tooling implementation, validated against accepted 1.1.6**.

This harness exists to reduce dependence on full manual Graveyard Keeper playthroughs after structural reminder changes.

## Goal

For Graveyard Keeper 1.407, keep a compact accepted evidence baseline for the six weekday NPCs and automatically reject unexplained semantic drift in Day Wheel Quest Markers.

The harness is deliberately separate from the public runtime DLL. It does not mutate saves, render markers, or run during normal gameplay.

## Current automatic validator

Entry point:

`python validator/validate_interaction_universe.py`

CI:

`.github/workflows/validate-interaction-universe.yml`

The workflow runs automatically on pull requests that change interaction-semantic production files or validator files, and is also available through `workflow_dispatch`.

### Independent lifecycle oracle

The accepted 1.1.6 read-only lifecycle census produced **216 path records**:

- 204 self-owned path instances;
- 12 ancestor-owned path instances;
- 117 task-owned suppressions;
- 6 same-visit suppressions;
- 93 admitted paths;
- 60 unique admitted dialogue owners.

The committed fixture stores lower-level path facts:

- NPC;
- selected branch;
- concrete selectable root path;
- persistent blacklist effects;
- task-owned flag;
- independent-root flag;
- reversible flag.

The Python validator does **not** trust the recorded owner/disposition. It independently derives:

1. exact self owner if the branch consumes itself;
2. otherwise nearest consumed selectable ancestor on the same path;
3. suppression for reversible/task-owned/non-independent cases;
4. admission otherwise.

It then compares its result with the accepted census. This catches accidental changes to the semantic model without executing production C#.

Control cases include:

- Snake `@snake_1с` -> admitted ancestor owner;
- Merchant `@merchant_2b` -> task-owned suppression;
- Merchant `@merchant_2e_1e` -> admitted ancestor owner;
- Merchant `@merchant_favore_done` -> task-owned suppression;
- Astrologer diary `9a/9b` -> same-visit suppression.

### Owner-task census

The accepted independent task audit is also stored compactly.

It proves the complete owner-local completion universe:

- **72** owner-local `Complete` nodes;
- probe 0.1.1 resolved **68** to selectable answers;
- **4** required additional topology evidence;
- event-provenance research recovered two more selectable routes:
  - `snake_key -> @snake_give_key`;
  - `snake_trap -> snake_stone_ready`;
- the remaining two are genuine mandatory event-only stages:
  - `npc_inquisitor/inquisitor_talk`;
  - `npc_cultist/snake_back`.

Final partition:

`72 = 70 selectable task completions + 2 event-only stages`

The validator recomputes the census arithmetic and checks that production's exact event-only set and verified completion supplement still match the accepted evidence.

### Production contract checks

The validator also checks current source for:

- schema version;
- owner/cross/base-topic canonical counts;
- lifecycle-universe guard constants;
- self-first and nearest-ancestor semantic guards;
- task-owned/navigation/reversible suppressions;
- exact promoted completion routes;
- exact event-only whitelist;
- exact `snake_trap` relation-gated route;
- absence of the retired 1.1.5 Snake fake-coins hard-code;
- manifest use of `UnifiedDialogueLifecycleCompiler.Validate`.

These checks are intentionally bounded. They are not a substitute for executing the game.

## First CI result

Workflow run `35449940018`:

- result: **PASS**;
- **52 checks / 0 failed**;
- lifecycle: **216 path records / 60 unique admitted owners**;
- tasks: **72 completion nodes -> 70 selectable + 2 event-only**;
- report artifact: `interaction-universe-report`.

## Explicit remaining coverage gaps

The harness reports these as coverage notes rather than pretending they are solved.

### 1. Per-route owner-task fixture

The historical 0.1.1 probe proved the complete 72-node census but logged individual route rows only for selected controls plus the four unresolved roots. It did not emit a compact row for every one of the 70 final selectable task routes.

A one-time read-only snapshot probe can close this without replaying quests: the six NPC graphs are structural assets and can be inspected from any loaded developed save.

### 2. Standalone navigation fixture

Accepted production evidence is:

- 210 answers;
- 270 authored root paths;
- 151 persisted ancestor predicates;
- 0 unsupported paths.

The 216 lifecycle fixture exercises a large part of that navigation structure, and production has verified control contracts, but a complete independent 270-path fixture is not yet committed.

A one-time read-only snapshot can close this layer as well.

## What the validator proves

Once a structural change enters a PR, the automatic validator can catch unexplained changes to the accepted interaction universe before a player build is accepted.

It is strongest for dialogue lifecycle classification because that layer already has complete path-level independent evidence.

It also guards the complete task census partition and exact residual special/event set.

## What it cannot prove by itself

Static CI cannot execute Graveyard Keeper's live runtime:

- `Player.IsEnough(SmartRes)`;
- actual save phrase/task transitions;
- Unity HUD object lifetime;
- rendered marker count;
- host event timing.

Those remain runtime concerns.

The planned second layer is therefore a separate **read-only runtime sentinel**. During ordinary play it should remain invisible while contracts agree and show an unmistakable FAIL indicator only when a detectable invariant is violated. It must not ship in the public production DLL.

## Acceptance policy for future structural changes

A structural interaction change should not be accepted merely because the aggregate counts still match.

Required process:

1. automatic validator passes against the accepted baseline;
2. any intentional baseline delta is explained at the individual interaction level;
3. runtime smoke checks cover only host behavior that static evidence cannot prove;
4. normal playthroughs become exploratory/end-to-end evidence, not the primary regression mechanism.

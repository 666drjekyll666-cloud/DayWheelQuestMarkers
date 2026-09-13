# Auto-interaction task-route audit — GK 1.407

Status: **research in progress; no production behavior change**.

## Question

Audit all six weekday-NPC FlowCanvas graphs for owner-local task stages whose completion is not driven by an ordinary selectable `Flow_MultiAnswer` route, especially the runtime-proven class:

`Visible task -> NPC interaction -> mandatory automatic branch -> Flow_SetTaskState(... Complete)`.

The product rule remains:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`.

A visible task alone is never sufficient. Item/craft/relation/resource/quality prerequisites must be satisfied before a reminder is emitted.

The player clarified an additional product boundary during this audit: a stage can still be reminder-worthy when approaching a weekday NPC or the NPC's relevant meeting point automatically starts the required progression, even if no selectable dialogue answer is involved. Therefore an event/cutscene root is **not** excluded merely because it is a `CustomEvent`; its actual trigger provenance must be established. Unrelated timers, global scripts, or events that do not require the player to come to the weekday NPC/meeting point remain non-reminder routes.

## Accepted starting evidence

Runtime testing of `npc_inquisitor / inquisitor_talk` proved:

- task is `Visible` before interaction;
- interacting with the Inquisitor on Witch Hill starts mandatory scripted dialogue `inquisitor_burn_1` before the ordinary answer menu;
- that automatic branch sets `inquisitor_talk = Complete`;
- the successor `inquisitor_burn` becomes `Visible`;
- no intermediate item/craft/relation prerequisite is required for this stage.

Therefore `inquisitor_talk` is a real false negative in accepted 1.0.30 and is product-eligible for a weekday reminder.

## Probe 0.1.0 — broad non-selectable completion census

Frozen source: `frozen/auto-interaction-audit-probe-0.1.0` at `59119817acff6a92b3c97cd9b0a28c44bba912dc`.
CI run `34754526415`; artifact `10315994217`.
Raw DLL: 22,016 bytes; SHA-256 `03161fd1f06c47b3c00f6cfac53d9fa9b70ffb49abd1b2a57b0eca78bdf83588`.

Player runtime snapshot on 2026-09-13 completed successfully. Across the six graphs:

- owner-local `Complete` nodes: 72;
- resolved selectable routes: 64;
- initially unresolved/non-selectable candidates: 8.

Per NPC:

- Astrologer: 14 complete / 13 selectable / 1 candidate;
- Inquisitor: 9 / 8 / 1;
- Snake: 12 / 8 / 4;
- Merchant: 14 / 14 / 0;
- Ms. Charm: 10 / 9 / 1;
- Bishop: 13 / 12 / 1.

Initial eight candidates:

1. `npc_astrologer / dlc_souls_s29_1` — reverse walk stopped at `Flow_WaitForFlow`; numbered WaitForFlow ports were misclassified as value inputs by probe 0.1.0.
2. `npc_inquisitor / inquisitor_talk` — `CustomFunctionEvent "Next day after witch burning" -> Flow_PlayerEnable -> Flow_SetWGOParam(burning_done) -> Flow_Talk(inquisitor_burn_1) -> Complete`.
3. `npc_cultist / snake_key` — rooted at `CustomEvent morgue_quest`, then player enable/camera/teleport/scripted talk -> Complete. Event/cutscene progression; trigger provenance was not yet proven.
4. `npc_cultist / snake_back` — rooted at `CustomEvent on_back_to_snake_after_ritual`, then scripted return/teleport/camera/talk -> Complete. Event-driven continuation; trigger provenance was not yet proven.
5. `npc_cultist / snake_trap` — rooted at `CustomEvent snake_stone_ready`, followed by a long multi-day scripted sequence including waits, spawned WGOs, movement, camera work and `Flow_CheckKeyQuest` before completion. Trigger provenance was not yet proven.
6. `npc_cultist / dlc_souls_s29_3` — reverse walk stopped at `Flow_WaitForFlow`; required refined flow-port traversal.
7. `npc_actress / dlc_souls_s29_2` — reverse walk stopped at `Flow_WaitForFlow`; required refined flow-port traversal.
8. `npc_bishop / bishop_rcitezen` — completion branch starts at `CustomFunctionEvent bishop_get_citizen`, immediately blacklists `@bishop_get_citezen`, then talks and completes. The old identifier-name resolver could not safely equate the differently spelled names; exact CustomFunction UID linkage was required.

Canonical conclusions after 0.1.0:

- `inquisitor_talk` remained the verified positive automatic-interaction case;
- three Snake event routes remained semantically unresolved rather than rejected: their downstream cutscenes were known, but whether the player had to come to Snake/a Snake-related meeting point was not yet established;
- Merchant had no candidate in this structural class;
- no production classifier may be based on `Visible + missing existing owner rule`;
- four apparent candidates were parser-topology uncertainties to be resolved by 0.1.1.

## Probe 0.1.1 — refined topology pass

Goal: resolve only the two proven blind spots in 0.1.0:

- treat numbered `Flow_WaitForFlow` inputs as authored flow edges;
- traverse exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` links before deciding that a function-root completion is non-selectable.

This was read-only and one-shot. It did not mutate save/UI state and disabled itself after one graph snapshot.

Frozen source: `frozen/auto-interaction-audit-probe-0.1.1` at `bd48d3b744013a4137ea3c01dd5a9fd32282ccc4`.
CI run: `34755385493`, success.
Artifact: `AutoInteractionAuditProbe-0.1.1` (`10317215799`), archive digest `sha256:260d781d0ec26a6d79487ed4d4270d7267216fa7f66429e75131f75d3c1861c8`.
Raw DLL: 20,480 bytes.
Raw DLL SHA-256: `087f019ac874db9ac41481f3dba76344832783f07a31bbcdf392f37caf5d9e90`.

Player runtime snapshot on 2026-09-13 completed successfully. Refined census:

- owner-local `Complete` nodes: **72**;
- resolved selectable routes: **68**;
- genuine non-selectable/event-root candidates remaining: **4**.

The four topology false candidates from 0.1.0 resolved as ordinary selectable task routes:

- Astrologer `dlc_souls_s29_1` -> `@souls_s_s30_ask`;
- Snake `dlc_souls_s29_3` -> `@souls_s_s33_ask`;
- Ms. Charm `dlc_souls_s29_2` -> `@souls_s_s31_ask`;
- Bishop `bishop_rcitezen` -> `@bishop_get_citezen` through exact CustomFunction UID linkage.

Per-NPC final topology counts after 0.1.1:

- Astrologer: 14 / 14 selectable / 0 candidates;
- Inquisitor: 9 / 8 / 1;
- Snake: 12 / 9 / 3;
- Merchant: 14 / 14 / 0;
- Ms. Charm: 10 / 10 / 0;
- Bishop: 13 / 13 / 0.

The four remaining routes are:

1. **`npc_inquisitor / inquisitor_talk`** — still non-selectable. Refined UID traversal traces the completion path farther upstream to `CustomEvent` data containing `witch_burning_enable` / `inquisitor_after_dark_event`, then a `CustomFunctionCall` into exact `CustomFunctionEvent` UID `60f07555-17a4-4dd4-9046-83ec64abfc49` (`Next day after witch burning`), followed by the already proven mandatory `inquisitor_burn_1` dialogue and task completion. Existing live interaction evidence remains authoritative: this route is product-eligible.
2. **`npc_cultist / snake_key`** — root receiver is a `CustomEvent` associated with `morgue_quest`; downstream route teleports/positions the player, talks `snake_give_key_1`, then completes the task. The sender/trigger is not established yet.
3. **`npc_cultist / snake_back`** — root receiver is a `CustomEvent` associated with `on_back_to_snake_after_ritual`; downstream route teleports the player, moves the camera, plays `snake_sword_13/14/15`, then completes the task. The sender/trigger is not established yet.
4. **`npc_cultist / snake_trap`** — root receiver is a `CustomEvent` associated with `snake_stone_ready`; downstream route is a long scripted sequence with waits/cutscene operations and a later `Flow_CheckKeyQuest`. The sender/trigger is not established yet.

Therefore the refined structural classifier is complete for ordinary selectable routes, but **`CustomEvent` by itself is not a safe production classifier**. The three Snake cases require exact event provenance before they can be included or excluded under the product rule.

## Probe 0.1.2 — targeted event-provenance pass

Goal: close only the remaining semantic uncertainty for the three Snake `CustomEvent` routes by locating exact references/senders for:

- `morgue_quest`;
- `on_back_to_snake_after_ritual`;
- `snake_stone_ready`.

Controls also include `witch_burning_enable` and `inquisitor_after_dark_event`.

The probe performs one bounded read-only scan of already loaded `FlowScriptController` serialized graphs and loaded `TextAsset`s for those exact internal literals, reports every matching graph/resource context, then disables itself. It does not alter save state, task state, UI, NPC state, or production reminder logic. Its purpose is to determine whether each remaining event is initiated by player arrival/interaction at the weekday NPC/meeting point or by an unrelated automatic/global script.

Exact executable/build source: `dd4d26167eb05512e5fa4760d95d4f82607fd080`.
Frozen ref: `frozen/auto-interaction-event-provenance-probe-0.1.2` at the exact source above.
CI run `34756182698`, job `103720820782`, success.
Artifact: `AutoInteractionEventProvenanceProbe-0.1.2` (`10317616910`), archive digest `sha256:d82de71ea01fd00e51555aac4a77625c586fd305f082e43c315a88cb0b03d8bc`.
Raw DLL: 12,800 bytes.
Raw DLL SHA-256: `a53d69db2208c43e3744297db61252662545cd99e6941362c6c3e1a67b3022cd`.
Requested runtime test: remove/replace 0.1.1, install 0.1.2 beside accepted production 1.0.30, load any normal gameplay save until `AUTO3_END`, then provide the log. No NPC interaction or special quest/day state is required for this first provenance scan.
Player result: pending.

## Production boundary

Do not implement production behavior until event provenance for the three Snake routes is established. The safe conclusions already available are:

- the 68 selectable owner-local completion routes belong to the existing task-linked model;
- `inquisitor_talk` is a verified positive non-selectable interaction route;
- three Snake event routes remain open questions and must not be admitted or rejected by event-name/type inference;
- a broad rule such as `Visible task + CustomEvent completion route` is unsafe;
- if only isolated verified non-selectable routes remain, a narrow explicit GK 1.407 mapping is preferable to an unproven universal classifier.

Any production representation must still be loading-derived/persisted and gameplay-cheap: cached task/predicate checks only, no FlowCanvas traversal during normal gameplay and no localized/display-text matching.

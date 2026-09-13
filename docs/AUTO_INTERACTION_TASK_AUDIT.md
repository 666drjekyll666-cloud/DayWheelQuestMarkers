# Auto-interaction task-route audit — GK 1.407

Status: **research in progress; no production behavior change**.

## Question

Audit all six weekday-NPC FlowCanvas graphs for owner-local task stages whose completion is not driven by an ordinary selectable `Flow_MultiAnswer` route, especially the runtime-proven class:

`Visible task -> NPC interaction -> mandatory automatic branch -> Flow_SetTaskState(... Complete)`.

The product rule remains:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`.

A visible task alone is never sufficient. Event/cutscene routes and routes with unmet item/relation/resource/quality prerequisites must not create a reminder.

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
3. `npc_cultist / snake_key` — rooted at `CustomEvent morgue_quest`, then player enable/camera/teleport/scripted talk -> Complete. Event/cutscene progression, not a proven NPC-interaction reminder.
4. `npc_cultist / snake_back` — rooted at `CustomEvent on_back_to_snake_after_ritual`, then scripted return/teleport/camera/talk -> Complete. Event-driven continuation, not a proven NPC-interaction reminder.
5. `npc_cultist / snake_trap` — rooted at `CustomEvent snake_stone_ready`, followed by a long multi-day scripted sequence including waits, spawned WGOs, movement, camera work and `Flow_CheckKeyQuest` before completion. Not the simple interaction class.
6. `npc_cultist / dlc_souls_s29_3` — reverse walk stopped at `Flow_WaitForFlow`; requires refined flow-port traversal.
7. `npc_actress / dlc_souls_s29_2` — reverse walk stopped at `Flow_WaitForFlow`; requires refined flow-port traversal.
8. `npc_bishop / bishop_rcitezen` — completion branch starts at `CustomFunctionEvent bishop_get_citizen`, immediately blacklists `@bishop_get_citezen`, then talks and completes. The graph excerpt also contains authored `Flow_Answer`; the old identifier-name resolver cannot safely equate `bishop_get_citizen` with typoed phrase `@bishop_get_citezen`. Requires exact CustomFunction UID linkage rather than name inference.

Canonical conclusions after 0.1.0:

- `inquisitor_talk` remains the verified positive auto-interaction case.
- `snake_key`, `snake_back`, and `snake_trap` are excluded from the simple auto-interaction class because their completion is event/cutscene driven.
- Merchant has no candidate in this structural class.
- no production classifier may be based on `Visible + missing existing owner rule`.
- the remaining uncertainty is structural traversal, not current save state.

## Probe 0.1.1 — refined topology pass

Goal: resolve only the two proven blind spots in 0.1.0:

- treat numbered `Flow_WaitForFlow` inputs as authored flow edges;
- traverse exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` links before deciding that a function-root completion is non-selectable.

This is still read-only and one-shot. It does not mutate save/UI state and disables itself after one graph snapshot.

Frozen source: `frozen/auto-interaction-audit-probe-0.1.1` at `bd48d3b744013a4137ea3c01dd5a9fd32282ccc4`.
CI run: `34755385493`, success.
Artifact: `AutoInteractionAuditProbe-0.1.1` (`10317215799`), archive digest `sha256:260d781d0ec26a6d79487ed4d4270d7267216fa7f66429e75131f75d3c1861c8`.
Raw DLL: 20,480 bytes.
Raw DLL SHA-256: `087f019ac874db9ac41481f3dba76344832783f07a31bbcdf392f37caf5d9e90`.

Requested test: replace probe 0.1.0 with 0.1.1, load any developed save until normal gameplay, then provide `LogOutput.log`. No NPC interaction or special quest state is required because the probe inspects static graph topology.

## Production boundary

Do not implement a production version until the refined pass closes the three Souls WaitForFlow routes and the Bishop CustomFunction route. If a general class is established, the intended architecture remains loading-derived compact rules persisted in the manifest, with gameplay evaluating only cached task/predicate state and no graph traversal.

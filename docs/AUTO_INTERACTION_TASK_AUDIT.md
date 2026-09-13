# Auto-interaction task-route audit — GK 1.407

Status: **research complete for the current six weekday-NPC owner-local completion universe; no production behavior change**.

## Question

Audit all six weekday-NPC FlowCanvas graphs for owner-local task stages whose completion is not driven by the ordinary selectable route model, especially the runtime-proven class:

`Visible task -> NPC interaction -> mandatory automatic branch -> Flow_SetTaskState(... Complete)`.

The product rule remains:

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`.

A visible task alone is never sufficient. Item/craft/relation/resource/quality prerequisites must be satisfied before a reminder is emitted.

The player clarified an additional product boundary during this audit: a stage is still reminder-worthy when approaching/interacting with a weekday NPC or the NPC's relevant meeting point automatically starts the required progression, even if no selectable dialogue answer is shown. Therefore an event/cutscene root is not excluded merely because it is a `CustomEvent`; its trigger provenance matters. Unrelated timers/global scripts remain non-reminder routes.

## Accepted starting evidence — Inquisitor `inquisitor_talk`

Runtime testing of `npc_inquisitor / inquisitor_talk` proved:

- task is `Visible` before interaction;
- interacting with the Inquisitor on Witch Hill starts mandatory scripted dialogue `inquisitor_burn_1` before the ordinary answer menu;
- that automatic branch sets `inquisitor_talk = Complete`;
- successor `inquisitor_burn` becomes `Visible`;
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

1. `npc_astrologer / dlc_souls_s29_1` — reverse walk stopped at `Flow_WaitForFlow`.
2. `npc_inquisitor / inquisitor_talk` — mandatory automatic completion branch.
3. `npc_cultist / snake_key` — receiver `CustomEvent morgue_quest`.
4. `npc_cultist / snake_back` — receiver `CustomEvent on_back_to_snake_after_ritual`.
5. `npc_cultist / snake_trap` — receiver `CustomEvent snake_stone_ready`.
6. `npc_cultist / dlc_souls_s29_3` — reverse walk stopped at `Flow_WaitForFlow`.
7. `npc_actress / dlc_souls_s29_2` — reverse walk stopped at `Flow_WaitForFlow`.
8. `npc_bishop / bishop_rcitezen` — function route required exact UID linkage rather than identifier spelling.

The census established that `Visible + missing existing owner rule` cannot be a production classifier.

## Probe 0.1.1 — refined topology pass

Goal: resolve the two proven topology blind spots in 0.1.0:

- treat numbered `Flow_WaitForFlow` inputs as authored flow edges;
- traverse exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` links.

Frozen source: `frozen/auto-interaction-audit-probe-0.1.1` at `bd48d3b744013a4137ea3c01dd5a9fd32282ccc4`.
CI run `34755385493`, success.
Artifact `AutoInteractionAuditProbe-0.1.1` (`10317215799`), archive digest `sha256:260d781d0ec26a6d79487ed4d4270d7267216fa7f66429e75131f75d3c1861c8`.
Raw DLL: 20,480 bytes; SHA-256 `087f019ac874db9ac41481f3dba76344832783f07a31bbcdf392f37caf5d9e90`.

Player snapshot on 2026-09-13 produced:

- owner-local `Complete` nodes: **72**;
- mapped selectable routes: **68**;
- remaining event/non-selectable roots: **4**.

The four topology false candidates resolved to ordinary selectable task routes:

- Astrologer `dlc_souls_s29_1 -> @souls_s_s30_ask`;
- Snake `dlc_souls_s29_3 -> @souls_s_s33_ask`;
- Ms. Charm `dlc_souls_s29_2 -> @souls_s_s31_ask`;
- Bishop `bishop_rcitezen -> @bishop_get_citezen` through exact CustomFunction UID linkage.

The four unresolved roots were `inquisitor_talk`, `snake_key`, `snake_back`, and `snake_trap`.

## Probe 0.1.2 — targeted event-provenance pass

Goal: close only the remaining semantic uncertainty for the three Snake event roots by locating exact references/senders for `morgue_quest`, `on_back_to_snake_after_ritual`, and `snake_stone_ready`. Controls included `witch_burning_enable` and `inquisitor_after_dark_event`.

Exact executable/build source: `dd4d26167eb05512e5fa4760d95d4f82607fd080`.
Frozen ref: `frozen/auto-interaction-event-provenance-probe-0.1.2`.
CI run `34756182698`, job `103720820782`, success.
Artifact `AutoInteractionEventProvenanceProbe-0.1.2` (`10317616910`), archive digest `sha256:d82de71ea01fd00e51555aac4a77625c586fd305f082e43c315a88cb0b03d8bc`.
Raw DLL: 12,800 bytes; SHA-256 `a53d69db2208c43e3744297db61252662545cd99e6941362c6c3e1a67b3022cd`.

Player runtime snapshot on 2026-09-13 completed successfully (`AUTO3_END`). It scanned 92 loaded controllers / 90 unique serialized graphs and 3,397 loaded `TextAsset`s. The target literals were found only in the relevant weekday-NPC graph; no TextAsset source was found.

### `snake_key` — selectable hand-in hidden behind an event hop

0.1.2 found, in the Snake graph:

- receiver `CustomEvent` node 42 for `morgue_quest`;
- sender `Flow_FireEvent` node 649 with `event=morgue_quest`;
- immediately upstream, exact `CustomFunctionEvent` node 645 with UID `ce662331-82b6-480c-a451-f1d08754bb62`.

Existing accepted/static audit evidence independently identifies top-level `@snake_give_key` as `Flow_MultiAnswer` 106 index 3, with authored price `Item:ques_key_cultist = 1`, exact self-consumption, and exactly one CustomFunction jump. The raw Snake graph places the matching `CustomFunctionCall` UID `ce662331-82b6-480c-a451-f1d08754bb62` on that answer branch.

Therefore the actual authored route is:

`@snake_give_key [requires ques_key_cultist x1] -> exact CustomFunction -> Flow_FireEvent(morgue_quest) -> CustomEvent(morgue_quest) -> scripted dialogue -> snake_key Complete`.

Classification: **ordinary selectable task interaction with an event boundary**. It must remain non-actionable until the key requirement passes.

### `snake_trap` — selectable answer hidden behind an event hop

0.1.2 found:

- `Flow_MultiAnswer` node 1520 with answers `snake_stone_ready` and `Leave`;
- the selected `snake_stone_ready` branch reaches `Flow_FireEvent` node 1527 with `event=snake_stone_ready`;
- receiver `CustomEvent` node 1541 starts the long scripted completion scene.

Earlier graph evidence around this answer includes authored `Flow_Answer` node 1532 and `SmartRes GameRes:_rel = 10` node 1538. Production must preserve that authored gate rather than treating the task as unconditionally actionable.

Classification: **ordinary selectable task interaction with an event boundary**. The old reverse walker missed only the `Flow_FireEvent -> CustomEvent` topology edge.

### `snake_back` — required later interaction event

0.1.2 found:

- `Flow_AddInteractionEvent` node 1280 with `Event=on_back_to_snake_after_ritual` on the Snake graph;
- receiver `CustomEvent` node 1346 starts the scripted return/camera/dialogue branch that completes `snake_back`.

Existing authored evidence establishes the predecessor hand-in:

- top-level `@snake_sword` is gated by `Item:sword_damask_gem = 1`;
- consuming that route completes `snake_sword` and makes `snake_back = Visible`;
- the same authored continuation installs `on_back_to_snake_after_ritual` as an interaction event.

Therefore `snake_back` must **not** be reminded before the sword hand-in. Once `snake_back` is Visible and the authored interaction-event stage has been installed, the required next interaction with Snake is product-eligible even though it bypasses the ordinary menu.

Classification: **verified later NPC-interaction stage**, distinct from ordinary selectable answers.

### Inquisitor controls

0.1.2 also found `Flow_AddInteractionEvent` references for `inquisitor_after_dark_event` plus its matching receiver, consistent with the already accepted live `inquisitor_talk` evidence. That control supports the semantic distinction between an interaction-installed event and an arbitrary/global CustomEvent.

## Final census and production design boundary

The original 72 owner-local Complete nodes are now fully classified for the current six weekday-NPC graphs:

- **70** resolve to ordinary selectable task interactions when loading-time topology includes the proven edges for numbered `Flow_WaitForFlow`, exact CustomFunction UID jumps, and exact same-graph `Flow_FireEvent(event X) -> CustomEvent(eventName X)` transitions;
- **2** are genuine mandatory interaction-event stages requiring no selectable answer at the completion interaction: `npc_inquisitor / inquisitor_talk` and `npc_cultist / snake_back`.

Production conclusions:

1. Add the exact same-graph `Flow_FireEvent(event X) -> CustomEvent(eventName X)` relation to loading-time structural traversal. This is sufficient to recover `snake_key` and `snake_trap` as ordinary task-linked routes while preserving their authored answer gates.
2. Do **not** classify arbitrary `CustomEvent` roots, and do not use `Visible + no owner rule`.
3. `Flow_AddInteractionEvent` is a different semantic boundary: it schedules behavior for a later interaction rather than forming an immediate flow edge. The audit proves two product-positive completion stages in this class (`inquisitor_talk`, `snake_back`), but does not justify admitting every interaction event universally without an exact task/stage provenance relation.
4. A narrow, reviewable GK 1.407 representation for the two verified interaction-event task states is acceptable if a general loading-derived provenance classifier cannot be made equally strict and cheap.
5. All structural work remains loading/bootstrap-only and persists in the compact manifest. Gameplay evaluates cached task/phrase/gate predicates only; no FlowCanvas traversal, localized-text matching, or broad scanning is allowed during normal play.

Research classification is complete. Production 1.0.30 and `main` were not modified by probes 0.1.0-0.1.2. A production implementation should start from current accepted state on the next available development version only after the normal development branch/version check.

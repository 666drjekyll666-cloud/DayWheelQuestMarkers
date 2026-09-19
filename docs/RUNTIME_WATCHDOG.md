# Runtime Watchdog 0.2.0

Status: **research-only / read-only / companion diagnostic for accepted Day Wheel Quest Markers 1.1.6**.

## Purpose

Runtime Watchdog 0.2.0 combines two independent live checks:

1. **production/runtime integrity** — accepted manifest counts, live bindings, known weekday-NPC bindings, and desired-marker vs active-HUD parity;
2. **live dialogue coverage** — every authored weekday-NPC multi-answer menu that actually executes, and every option the game actually renders, must belong to the accepted GK 1.407 interaction universe.

The watchdog never mutates the save, tasks, phrases, NPC state, production plugin fields, or production marker UI.

## Live dialogue seam

Accepted GK 1.407 assembly evidence establishes this native chain:

`Flow_MultiAnswer -> WorldGameObject.ShowMultianswer -> MultiAnswerGUI.ShowAnswers -> MultiAnswerOptionGUI.Show`

0.2.0 uses Harmony only at those narrow presentation/execution seams:

- the exact `Flow_MultiAnswer` runtime callback identifies the owning weekday NPC, exact multi-answer node ID, and authored answer list;
- `MultiAnswerOptionGUI.Show(AnswerVisualData, ...)` observes only options the game actually renders and records the game-owned `id` and `can_be_picked` state;
- reconciliation completes at the end of the native rendered-menu `MultiAnswerGUI.ShowAnswers` call.

No FlowCanvas graph scan is performed during gameplay.

## Accepted live-answer universe

The embedded baseline contains exactly **243 authored answer occurrences / 224 unique NPC+answer IDs** across the six weekday-NPC graphs.

Every exact `npc + multi node + answer index + answer ID` occurrence has an evidence-derived disposition:

- `REMINDER_TASK_OWNED`: 88;
- `REMINDER_DIALOGUE_OWNER`: 58;
- `NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER`: 67;
- `SAME_VISIT_NON_REMINDER`: 4;
- `SAME_VISIT_DESCENDANT_OF_DIALOGUE_OWNER`: 6;
- `SAME_VISIT_DESCENDANT_OF_TASK_OWNER`: 6;
- `EVENT_INVOKED_NON_REMINDER`: 14;
- **UNKNOWN: 0**.

This table is independently re-derived by `validator/validate_interaction_universe.py` from the accepted raw, lifecycle, task-route, and event-frontier fixtures. It is not a hand-written display-text whitelist.

## Failure behavior

Normal state: **no watchdog UI**.

A live coverage contradiction is sticky for the current session and immediately shows a large red **!** in the upper-right corner plus:

`DAY WHEEL WATCHDOG FAIL: <code>`

Key live failure codes include:

- `LIVE_AUTHORED_MENU_UNKNOWN` — a weekday-NPC Flow_MultiAnswer node executed but is absent from the accepted universe;
- `LIVE_AUTHORED_MENU_DRIFT` — the executing node's authored answer list differs from the accepted exact node/index/ID contract;
- `LIVE_DIALOGUE_UNKNOWN` — an actually rendered option cannot be mapped to its exact accepted occurrence/disposition;
- `LIVE_DIALOGUE_CONTEXT` — exact NPC/menu identity could not be read at the verified seam;
- `LIVE_DIALOGUE_HOOK_EXCEPTION` — the observed menu execution failed inside the hooked native path;
- `LIVE_WATCHDOG_INIT` — the fixture or verified GK 1.407 hook seam cannot be installed.

Existing 0.1.0 runtime-integrity failure codes remain active.

For successful weekday-NPC menus, the log emits one compact `LIVE_DIALOGUE_ACCOUNTING` record containing authored/visible/pickable counts and the evidence disposition of each rendered option.

## Marker comparison boundary

0.2.0 deliberately does **not** assert that the number of options visible in the current submenu must equal the total marker count. That would be false for nested navigation, task-owned deduplication, same-visit descendants, and mandatory event-only stages.

Instead:

- live dialogue coverage proves that every actual menu surface is known and classified;
- the static validator proves the accepted classification table has no unexplained authored answer occurrence;
- the existing runtime-integrity layer proves production's desired marker set is what the HUD actually renders.

A stricter live option-to-marker equality should only be added if an independent timing/dedup oracle is proven; 0.2.0 avoids a false-positive heuristic.

## Performance

Steady state is intentionally cheap:

- per frame: only the BaseUnityPlugin timer comparison;
- ordinary integrity checks: once every **5 seconds**;
- transient integrity mismatch must repeat on **2 consecutive checks** before a red failure;
- live dialogue coverage: **event-driven only**, doing work only while a weekday-NPC multi-answer menu is actually constructed;
- embedded 243-row disposition table is parsed once at plugin startup;
- live occurrence lookup is dictionary-based;
- no FlowCanvas graph traversal;
- no broad hierarchy/resource scans after the required runtime references are cached;
- no background worker;
- no save mutation.

The key live unknown-dialogue failures are immediate and do not wait for the five-second integrity cadence.

## Candidate 0.2.0

- exact build source: `0956ccb26af5e72b4d834e3cd42e183f985bdf20`;
- frozen ref: `frozen/runtime-watchdog-0.2.0`;
- build workflow run: `35456895718`;
- build job: `105933680313`;
- build: **success, 0 warnings / 0 errors**;
- artifact: `DayWheelQuestMarkers-RuntimeWatchdog-0.2.0`, ID `10588138858`;
- raw DLL size: **57,344 bytes**;
- raw DLL SHA-256: `04649c1444275a3e9e2361f912a2326081e231b748ad760c22893aa489e6f067`.

Supporting exhaustive validator:

- run `35456618512`;
- job `105932928974`;
- **PASS, 88 checks / 0 failed**;
- live dispositions: **243 occurrences classified / UNKNOWN=0**.

Production Day Wheel Quest Markers 1.1.6 is unchanged.

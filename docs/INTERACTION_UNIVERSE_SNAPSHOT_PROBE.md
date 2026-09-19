# Interaction Universe Snapshot Probe

Status: **research-only / read-only / not production**.

Purpose: close the two explicit fixture gaps reported by the automatic interaction-universe validator without replaying quests or mutating a save.

## Probe 0.1.1

Assembly project:

`InteractionUniverseTaskSnapshot.csproj`

Plugin:

`src/InteractionUniverseTaskSnapshotProbe.cs`

The probe waits until the loaded GK 1.407 runtime exposes all six weekday-NPC graphs, runs once, writes structured lines to the normal BepInEx log, disables itself, and performs no save/UI mutation.

### Task snapshot

The task pass is derived independently from the historical 0.1.1 owner-task audit and adds the separately verified same-graph:

`Flow_FireEvent -> CustomEvent`

edge.

It emits exactly one `TASKSNAP_ROUTE` record for every owner-local `Flow_SetTaskState(State=Complete)` node in the six weekday-NPC graphs.

Expected accepted 1.1.6 universe:

- 72 owner-local completion nodes;
- 70 selectable routes;
- 2 remaining event-only/unresolved routes:
  - `npc_inquisitor/inquisitor_talk`;
  - `npc_cultist/snake_back`.

The route output includes NPC, task ID, resolved answer ID, completion node, and reverse trace.

This closes the historical evidence problem where probe 0.1.1 proved the aggregate 72/68/4 census but did not print all ordinary mapped rows.

### Navigation snapshot

The same DLL also produces a complete `NAVSNAP_PATH` snapshot using the accepted production navigation derivation:

- NPC;
- final answer ID;
- path index;
- unsupported flag;
- ordered ancestor predicates;
- phrase-state requirements;
- supported gate variants.

Expected accepted 1.1.6 totals:

- 210 answers;
- 270 paths;
- 151 predicates;
- 0 unsupported paths;
- verified navigation control contracts pass.

This part is intentionally labeled **snapshot evidence**, not an independent oracle: it serializes the accepted navigation compiler's output so that all 270 paths can be retained and reviewed. The automatic Python validator remains responsible for clearly distinguishing independent lifecycle evidence from production-derived snapshot evidence.

## Player burden

One normal load of a developed save is sufficient. No quest interaction, teleport, inventory edit, relation edit, dialogue selection, or replay is required.

After the snapshot is captured, the DLL should be removed. Its output is converted into compact repository fixtures; the raw game graphs are not committed.

## Safety

- no Harmony patches;
- no save writes;
- no task/phrase/resource mutation;
- no UI mutation;
- no background worker;
- executes once after runtime readiness;
- disables itself after the snapshot.

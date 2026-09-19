# Interaction Universe Snapshot Probe

Status: **research-only / read-only / not production**.

Purpose: close the two explicit fixture gaps reported by the automatic interaction-universe validator without replaying quests or mutating a save.

## Probe 0.1.3

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

### Raw universe and navigation snapshot

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

## Frozen 0.1.1 build

- exact build source: `f6fef176b210f7c0e625ebb47a9b5f58d9c8f70d`;
- frozen ref: `frozen/interaction-universe-snapshot-0.1.1`;
- workflow run: `35450399543`;
- job: `105916438810`;
- build result: **success, 0 warnings / 0 errors**;
- artifact: `InteractionUniverseTaskSnapshot-0.1.1`, artifact ID `10585578817`;
- raw DLL: `DayWheelQuestMarkers-interaction-universe-snapshot-0.1.1.dll`;
- raw DLL size: **64,000 bytes**;
- raw DLL SHA-256: `4f0c62218ceb5474d2e59a63ff333aee6e149fa3119bd02a9a0ac6f848269999`.

Requested runtime capture:

1. keep normal stable Day Wheel Quest Markers 1.1.6 installed;
2. install this snapshot DLL temporarily alongside it;
3. load any developed save where the six weekday NPC graphs are available;
4. no dialogue interaction or gameplay action is required; wait only until the normal loaded game HUD is visible and the probe has emitted `TASKSNAP_END`;
5. exit normally and provide `BepInEx/LogOutput.log`;
6. remove the snapshot DLL afterward.

The log is accepted only if the importer verifies **72 task routes**, the exact two event-only stages, and **270 navigation paths** with canonical navigation totals.


## 0.1.2 accepted runtime snapshot

Player runtime log `LogOutput(20260919-152112).log` produced a complete snapshot:

- task routes: **72 = 70 selectable + 2 event-only/unresolved**;
- raw answers: **243 occurrences / 224 unique NPC+answer IDs**;
- raw task-state transitions: **150 = 70 Visible + 80 Complete**;
- raw CustomEvent entries: **66**;
- AddInteractionEvent declarations: **19**;
- RemoveInteractionEvent declarations: **4**;
- navigation: **210 answers / 270 paths / 151 predicates / 0 unsupported**;
- verified navigation contracts: **True**.

The strict importer initially exposed a tooling bug: IDs containing spaces (for example `@actress_ song_done`) cannot be parsed with whitespace tokenization. The importer now uses field-boundary regular expressions and preserves these IDs exactly.

The raw-vs-navigation comparison leaves **14 unique answer IDs** with no normal interaction-root path:

- `npc_cultist`: `snake_back_12a`, `snake_back_12b`;
- `npc_inquisitor`: `inquisitor_1_7a`, `inquisitor_1_7b`, `inquisitor_1_9a`, `inquisitor_1_9b`, `inquisitor_1_11a`, `inquisitor_1_11b`, `inquisitor_1_11c`, `inquisitor_2_9a`, `inquisitor_2_9b`, `inquisitor_gerry`, `inquisitor_cultists`, `inquisitor_nothing`.

These are not silently classified. Probe 0.1.3 adds bounded upstream/downstream topology evidence for exactly this frontier so each candidate can be reviewed as event/cutscene/non-reminder or promoted if it represents an independently actionable weekday-NPC interaction.


## 0.1.3 accepted frontier result

Player runtime log `LogOutput(20260919-153202).log` produced the complete no-root topology pass:

- **14/14** expected candidate occurrences emitted;
- every candidate has exactly one upstream event-root family;
- all 14 are event/cutscene-invoked menus rather than fresh weekday-NPC interaction roots;
- accepted classification: `EVENT_INVOKED_NON_REMINDER`;
- coverage watchdog: **UNKNOWN = 0** for the accepted six weekday-NPC graph universe.

Evidence groups:

- Inquisitor node 95 / `first_meet_under_mountains`: 7 answers;
- Inquisitor node 343 / `on_came_to_mountain_for_witch_burning`: 2 answers;
- Inquisitor node 1711 / `inquisitor_after_dark_event`: 3 answers;
- Snake node 318 / `player_back_to_cultist`: 2 answers.

Probe 0.1.3 frozen build:

- exact source: `1b4cd32db7574cf4a0a27118bec4aa557a61b627`;
- frozen ref: `frozen/interaction-universe-snapshot-0.1.3`;
- workflow run: `35451885905`;
- build: **success, 0 warnings / 0 errors**;
- artifact: `InteractionUniverseTaskSnapshot-0.1.3`, ID `10587301518`;
- raw DLL SHA-256: `6b1850370798ac293059b25c24c3c71d36840d86bd29de5638b0145a7fe78eb0`.

No further player runtime capture is required for this research question. The temporary snapshot DLL should now be removed.

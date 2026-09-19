# Runtime Watchdog 0.1.0

Status: **research-only / read-only / companion diagnostic for accepted Day Wheel Quest Markers 1.1.6**.

## Purpose

The runtime watchdog is the live companion to the static interaction-universe validator.

The static validator answers:

> Does the accepted GK 1.407 six-weekday-NPC interaction universe remain structurally accounted for?

The runtime watchdog answers:

> While the accepted 1.1.6 production DLL is running, is a proven runtime contract currently being violated?

It never mutates the save, tasks, phrases, NPC state, production plugin fields, or marker UI.

## Visible behavior

Normal state: **no watchdog UI at all**.

After a proven contradiction persists for three one-second samples, the watchdog displays a large red **!** in the upper-right corner and the text:

`DAY WHEEL WATCHDOG FAIL: <code>`

The same failure is logged once as:

`WATCHDOG_FAIL code=<code> detail=<details>`

When the contradiction disappears, the indicator disappears and the log records `WATCHDOG_RECOVERED`.

## Checks

After a 12-second post-start grace period:

1. the production plugin exists and is exactly version 1.1.6;
2. accepted schema-5 structural counts match the frozen 1.1.6 contract;
3. the production manifest reports valid live runtime bindings;
4. the set of known weekday NPCs independently read from the save matches the set bound by the production rule cache;
5. desired marker counts/styles in the production evaluator match the active marker visuals actually held by `CalendarMarkers`.

The three-sample persistence gate is intentional so HUD recreation / save-load ownership transitions do not create one-frame false alarms.

## Scope and limitation

A missing red indicator means **no monitored contract violation was detected**. It is not a mathematical proof that every gameplay semantic is correct.

Completeness of authored interaction discovery is covered separately by the static interaction-universe validator, whose accepted six-NPC baseline currently has `UNKNOWN = 0`.

The two layers are complementary:

- static coverage prevents unexplained authored interaction surfaces from being silently omitted;
- runtime watchdog detects manifest/binding/rendering contradictions while production runs.

## Performance

- one timer comparison per frame;
- one bounded reflection-based check per second;
- no FlowCanvas graph parsing;
- no hierarchy/resource scan after the production/MainGame references have been found;
- no background worker;
- `OnGUI` returns immediately unless a failure is active.

## Frozen build

- version: 0.1.0;
- exact source: `170cb075ef9618a7b9eb8373de3ebb8c2f6205ff`;
- frozen ref: `frozen/runtime-watchdog-0.1.0`;
- CI run: `35453049579`;
- job: `105923433242`;
- build: **success, 0 warnings / 0 errors**;
- artifact: `DayWheelQuestMarkers-RuntimeWatchdog-0.1.0`, ID `10587138300`;
- raw DLL size: **17,920 bytes**;
- raw DLL SHA-256: `c4a901ef1fa8bcd5b8682e46f2952a1f738302c34214ef54ad8070d2acf4c703`.

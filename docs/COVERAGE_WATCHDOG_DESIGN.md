# Coverage Watchdog Design

Status: research design; no production runtime behavior.

## Problem

A regression validator over only already-known reminder classes is closed-world. It can prove that accepted mappings did not drift, but it cannot prove that the accepted classifier discovered every reminder-worthy interaction in Graveyard Keeper 1.407.

Failure mode:

1. an authored weekday-NPC interaction exists;
2. its topology does not match task-owned selectable, persistent dialogue lifecycle, or the two currently verified event-only shapes;
3. production emits no marker;
4. a validator that only compares known counts also emits no failure.

That is an unacceptable blind spot for the long-term verification goal.

## Required separation

The verification system therefore needs two logically separate layers.

### Layer A: known-universe regression validator

Purpose: detect drift in interactions already understood and accepted.

Inputs:
- accepted lifecycle-path evidence;
- task census;
- navigation snapshot;
- exact residual event rules;
- production structural contracts.

This is the existing Python validator.

### Layer B: coverage watchdog

Purpose: detect authored interaction surfaces that production does not explain at all.

Its discovery universe must be broader than production classification and must not start from the set of rules production already generated.

For the six weekday NPC graphs, the baseline discovery inventory should include at minimum:

1. every raw `Flow_MultiAnswer` answer occurrence, identified by NPC + menu node + answer index + answer ID;
2. every `Flow_SetTaskState` transition, not only the ones production currently maps;
3. every non-function `CustomEvent` entry;
4. every `Flow_AddInteractionEvent` / `Flow_RemoveInteractionEvent` declaration;
5. the independent owner-local completion census;
6. the authored interaction-root/navigation topology.

The first snapshot target is the already measured **243 raw answer occurrences** in the six NPC graphs.

## Total-accounting invariant

Every raw interaction surface must end in exactly one reviewed disposition:

- `REMINDER_TASK`
- `REMINDER_DIALOGUE_LIFECYCLE`
- `REMINDER_EVENT_ONLY`
- `SAME_VISIT_CONTINUATION`
- `NAVIGATION_OR_UTILITY`
- `REPEATABLE_NON_REMINDER`
- `UNREACHABLE_AUTHORED_DATA`
- `VERIFIED_NON_REMINDER`
- `UNKNOWN`

`UNKNOWN` is never a silent exclusion. It is a validation failure requiring research.

The important invariant is not a magic expected count. It is:

`raw authored interaction universe = explained reminder surfaces + explained non-reminder surfaces + UNKNOWN`

and acceptance requires:

`UNKNOWN = 0`

An expected aggregate count may be recorded for corruption detection, but it cannot substitute for per-entry accounting.

## Why this addresses the closed-world problem

Production may fail to recognize a new topology, but a raw answer occurrence or interaction event still exists in the graph inventory. Because the coverage layer starts from raw authored surfaces rather than production output, such an entry remains unclassified and becomes `UNKNOWN`.

This is intentionally asymmetric:
- production stays conservative and fails closed to avoid false markers;
- verification fails loud when production cannot explain a potentially relevant authored surface.

## Remaining theoretical limit

No finite classifier can prove completeness against a host mechanism it never observes at all.

For example, if a weekday-NPC progression interaction were triggered by a completely different engine subsystem with no dialogue answer, task transition, interaction event, or other audited NPC-graph entry, a graph-only watchdog would not see it.

Therefore coverage closure also requires a one-time audit of **interaction entry mechanisms**, not only answer IDs. If another host mechanism is found, it becomes a new explicit universe source rather than an ad-hoc production exception.

The goal is not to claim mathematical completeness prematurely. The goal is to make every current observation source explicit and make unexplained surfaces loud.

## Runtime sentinel

After the static universe is closed, a separate optional development sentinel can compare live state with the accepted compact fixture.

It should remain invisible while invariants hold and show a prominent red FAIL only for conditions such as:

- raw graph fingerprint/universe differs from accepted GK 1.407;
- an accepted reminder-bearing route cannot bind/evaluate;
- an `UNKNOWN` surface becomes live/actionable;
- production marker accounting disagrees with an independently evaluated accepted route;
- manifest/navigation contracts cannot be reconstructed.

The sentinel is development/research tooling, not part of the public production mod unless explicitly approved later.

## Performance rule

Raw graph traversal belongs only at loading/bootstrap or in the temporary research probe.

Normal gameplay must not rescan FlowCanvas graphs. A future sentinel should run on compact persisted data and verified state-change seams, with only a cheap low-frequency fallback if no event seam exists.

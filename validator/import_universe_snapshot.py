#!/usr/bin/env python3
"""Import a read-only interaction-universe snapshot from a BepInEx LogOutput.log.

The importer refuses incomplete or non-canonical snapshots. It writes only compact
semantic fixtures; raw Graveyard Keeper graph serialization is never copied.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


def fields_after(line: str, prefix: str) -> dict[str, str] | None:
    pos = line.find(prefix)
    if pos < 0:
        return None
    result: dict[str, str] = {}
    for token in line[pos + len(prefix):].strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        result[key] = value
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("log", type=Path)
    parser.add_argument("--out-dir", type=Path, default=Path("validator/fixtures"))
    args = parser.parse_args()

    text = args.log.read_text(encoding="utf-8", errors="replace")
    task_rows = []
    nav_rows = []
    raw_answers = []
    raw_task_states = []
    raw_custom_events = []
    raw_add_interactions = []
    raw_remove_interactions = []
    raw_summary = None
    task_summary = None
    nav_summary = None

    for line in text.splitlines():
        d = fields_after(line, "TASKSNAP_ROUTE ")
        if d is not None:
            task_rows.append(d)
            continue
        d = fields_after(line, "TASKSNAP_SUMMARY ")
        if d is not None:
            task_summary = d
            continue
        d = fields_after(line, "UNIVERSE_ANSWER ")
        if d is not None:
            raw_answers.append(d)
            continue
        d = fields_after(line, "UNIVERSE_TASK_STATE ")
        if d is not None:
            raw_task_states.append(d)
            continue
        d = fields_after(line, "UNIVERSE_CUSTOM_EVENT ")
        if d is not None:
            raw_custom_events.append(d)
            continue
        d = fields_after(line, "UNIVERSE_ADD_INTERACTION ")
        if d is not None:
            raw_add_interactions.append(d)
            continue
        d = fields_after(line, "UNIVERSE_REMOVE_INTERACTION ")
        if d is not None:
            raw_remove_interactions.append(d)
            continue
        d = fields_after(line, "UNIVERSE_RAW_SUMMARY ")
        if d is not None:
            raw_summary = d
            continue
        d = fields_after(line, "NAVSNAP_PATH ")
        if d is not None:
            nav_rows.append(d)
            continue
        d = fields_after(line, "NAVSNAP_SUMMARY ")
        if d is not None:
            nav_summary = d

    if task_summary is None:
        raise SystemExit("TASKSNAP_SUMMARY not found")
    if raw_summary is None:
        raise SystemExit("UNIVERSE_RAW_SUMMARY not found")
    if nav_summary is None:
        raise SystemExit("NAVSNAP_SUMMARY not found")

    complete = int(task_summary["ownerComplete"])
    selectable = int(task_summary["mappedSelectable"])
    unresolved = int(task_summary["candidateNonSelectable"])
    if (complete, selectable, unresolved) != (72, 70, 2):
        raise SystemExit(
            f"unexpected task summary: {(complete, selectable, unresolved)} expected (72, 70, 2)"
        )
    if len(task_rows) != 72:
        raise SystemExit(f"expected 72 TASKSNAP_ROUTE rows, got {len(task_rows)}")

    route_keys = {(x.get("npc"), x.get("task"), x.get("completeNode")) for x in task_rows}
    if len(route_keys) != 72:
        raise SystemExit("TASKSNAP_ROUTE rows are not unique by npc/task/completeNode")

    unresolved_rows = [x for x in task_rows if x.get("kind") == "EVENT_OR_UNRESOLVED"]
    unresolved_set = {(x.get("npc"), x.get("task")) for x in unresolved_rows}
    expected_unresolved = {
        ("npc_inquisitor", "inquisitor_talk"),
        ("npc_cultist", "snake_back"),
    }
    if unresolved_set != expected_unresolved:
        raise SystemExit(
            f"unexpected event-only/unresolved set: {sorted(unresolved_set)} "
            f"expected {sorted(expected_unresolved)}"
        )

    if raw_summary.get("answerOccurrences") != "243":
        raise SystemExit(
            f"unexpected raw answer occurrence count: {raw_summary.get('answerOccurrences')} expected 243"
        )
    if len(raw_answers) != 243:
        raise SystemExit(f"expected 243 UNIVERSE_ANSWER rows, got {len(raw_answers)}")

    raw_answer_keys = {(x.get("npc"), x.get("multi"), x.get("index")) for x in raw_answers}
    if len(raw_answer_keys) != 243:
        raise SystemExit("UNIVERSE_ANSWER rows are not unique by npc/multi/index")

    raw_path = args.out_dir / "raw-interaction-universe-1.1.6.tsv"
    raw_lines = [
        "# Raw accepted GK 1.407 interaction universe from read-only snapshot probe 0.1.2.",
        "# This fixture is deliberately broader than production reminder classification.",
        "kind\tnpc\tnode\tindex\tid\tvalue1\tvalue2",
    ]
    for row in sorted(raw_answers, key=lambda x: (x["npc"], int(x["multi"]), int(x["index"]))):
        raw_lines.append("\t".join([
            "answer", row.get("npc", ""), row.get("multi", ""), row.get("index", ""),
            row.get("answer", ""), "", ""
        ]))
    for row in sorted(raw_task_states, key=lambda x: (x["npc"], int(x["node"]))):
        raw_lines.append("\t".join([
            "task_state", row.get("npc", ""), row.get("node", ""), "",
            row.get("task", ""), row.get("owner", ""), row.get("state", "")
        ]))
    for row in sorted(raw_custom_events, key=lambda x: (x["npc"], int(x["node"]))):
        raw_lines.append("\t".join([
            "custom_event", row.get("npc", ""), row.get("node", ""), "",
            row.get("event", ""), "", ""
        ]))
    for row in sorted(raw_add_interactions, key=lambda x: (x["npc"], int(x["node"]))):
        raw_lines.append("\t".join([
            "add_interaction", row.get("npc", ""), row.get("node", ""), "",
            row.get("event", ""), "", ""
        ]))
    for row in sorted(raw_remove_interactions, key=lambda x: (x["npc"], int(x["node"]))):
        raw_lines.append("\t".join([
            "remove_interaction", row.get("npc", ""), row.get("node", ""), "",
            row.get("event", ""), "", ""
        ]))
    raw_path.write_text("\n".join(raw_lines) + "\n", encoding="utf-8")

    nav_expected = {
        "answers": "210",
        "paths": "270",
        "predicates": "151",
        "unsupportedPaths": "0",
        "verifiedContracts": "True",
    }
    for key, expected in nav_expected.items():
        actual = nav_summary.get(key)
        if actual != expected:
            raise SystemExit(f"unexpected NAVSNAP_SUMMARY {key}={actual}, expected {expected}")
    if len(nav_rows) != 270:
        raise SystemExit(f"expected 270 NAVSNAP_PATH rows, got {len(nav_rows)}")

    nav_keys = {(x.get("npc"), x.get("answer"), x.get("pathIndex")) for x in nav_rows}
    if len(nav_keys) != 270:
        raise SystemExit("NAVSNAP_PATH rows are not unique by npc/answer/pathIndex")

    args.out_dir.mkdir(parents=True, exist_ok=True)

    task_path = args.out_dir / "task-routes-1.1.6.tsv"
    task_lines = [
        "# Complete accepted GK 1.407 owner-task route snapshot from read-only probe 0.1.1.",
        "npc\ttask\tkind\tanswer\tcompleteNode\ttrace",
    ]
    for row in sorted(task_rows, key=lambda x: (x["npc"], x["task"], x["completeNode"])):
        task_lines.append("\t".join([
            row.get("npc", ""),
            row.get("task", ""),
            row.get("kind", ""),
            row.get("answer", ""),
            row.get("completeNode", ""),
            row.get("trace", ""),
        ]))
    task_path.write_text("\n".join(task_lines) + "\n", encoding="utf-8")

    nav_path = args.out_dir / "navigation-paths-1.1.6.tsv"
    nav_lines = [
        "# Complete accepted GK 1.407 navigation snapshot from read-only probe 0.1.1.",
        "# Snapshot evidence is production-derived; it is not an independent navigation oracle.",
        "npc\tanswer\tpathIndex\tunsupported\tancestors",
    ]
    for row in sorted(nav_rows, key=lambda x: (x["npc"], x["answer"], int(x["pathIndex"]))):
        nav_lines.append("\t".join([
            row.get("npc", ""),
            row.get("answer", ""),
            row.get("pathIndex", ""),
            row.get("unsupported", ""),
            row.get("ancestors", ""),
        ]))
    nav_path.write_text("\n".join(nav_lines) + "\n", encoding="utf-8")

    print("Interaction universe snapshot import: PASS")
    print(f"Raw answers: {len(raw_answers)}; task states: {len(raw_task_states)}; custom events: {len(raw_custom_events)}")
    print(f"Raw universe -> {raw_path}")
    print(f"Task routes: {len(task_rows)} -> {task_path}")
    print(f"Navigation paths: {len(nav_rows)} -> {nav_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

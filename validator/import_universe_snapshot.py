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
        d = fields_after(line, "NAVSNAP_PATH ")
        if d is not None:
            nav_rows.append(d)
            continue
        d = fields_after(line, "NAVSNAP_SUMMARY ")
        if d is not None:
            nav_summary = d

    if task_summary is None:
        raise SystemExit("TASKSNAP_SUMMARY not found")
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
    print(f"Task routes: {len(task_rows)} -> {task_path}")
    print(f"Navigation paths: {len(nav_rows)} -> {nav_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

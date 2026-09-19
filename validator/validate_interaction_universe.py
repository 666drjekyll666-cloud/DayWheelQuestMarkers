#!/usr/bin/env python3
"""Independent regression validator for the accepted GK 1.407 interaction universe.

This tool intentionally does not execute Graveyard Keeper or call production C# code.
It validates compact evidence captured by accepted read-only runtime censuses and checks
that production source still declares the same bounded contracts. Structural changes
must either preserve this baseline or update it with reviewed evidence.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BASELINE = ROOT / "validator" / "baseline-1.1.6.json"
DEFAULT_LIFECYCLE = ROOT / "validator" / "fixtures" / "lifecycle-paths-1.1.6.tsv"
DEFAULT_TASKS = ROOT / "validator" / "fixtures" / "task-census-1.1.6.tsv"
DEFAULT_TASK_ROUTES = ROOT / "validator" / "fixtures" / "task-routes-1.1.6.tsv"
DEFAULT_NAVIGATION = ROOT / "validator" / "fixtures" / "navigation-paths-1.1.6.tsv"
DEFAULT_RAW_UNIVERSE = ROOT / "validator" / "fixtures" / "raw-interaction-universe-1.1.6.tsv"
DEFAULT_FRONTIER = ROOT / "validator" / "fixtures" / "no-root-frontier-1.1.6.tsv"
DEFAULT_LIVE_DISPOSITIONS = ROOT / "validator" / "fixtures" / "live-answer-dispositions-1.1.6.tsv"
DEFAULT_REPORT = ROOT / "validator" / "out" / "validation-report.json"


class CheckLog:
    def __init__(self) -> None:
        self.checks: list[dict] = []
        self.failures: list[str] = []
        self.warnings: list[str] = []

    def check(self, name: str, condition: bool, detail: str) -> None:
        self.checks.append({"name": name, "ok": bool(condition), "detail": detail})
        if not condition:
            self.failures.append(f"{name}: {detail}")

    def warn(self, message: str) -> None:
        self.warnings.append(message)


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def parse_bool(value: str) -> bool:
    if value == "True":
        return True
    if value == "False":
        return False
    raise ValueError(f"expected True/False, got {value!r}")


def load_lifecycle(path: Path) -> list[dict[str, str]]:
    lines = [
        line for line in path.read_text(encoding="utf-8").splitlines()
        if line and not line.startswith("#")
    ]
    if not lines:
        return []
    reader = csv.DictReader(lines, delimiter="\t")
    return list(reader)

def load_tsv(path: Path) -> list[dict[str, str]]:
    lines = [
        line for line in path.read_text(encoding="utf-8").splitlines()
        if line and not line.startswith("#")
    ]
    if not lines:
        return []
    return list(csv.DictReader(lines, delimiter="\t"))



def derive_lifecycle_owner(row: dict[str, str]) -> tuple[str | None, str | None]:
    branch = row["branch"]
    path = row["path"].split(">") if row["path"] else []
    blacklists = {x for x in row["blacklists"].split(",") if x}

    if not path or path[-1] != branch:
        return None, None
    if branch in blacklists:
        return branch, "self"

    for ancestor in reversed(path[:-1]):
        if ancestor in blacklists:
            return ancestor, "ancestor"
    return None, None


def derive_disposition(row: dict[str, str], owner: str | None) -> str:
    if owner is None:
        return "NO_OWNER"
    if parse_bool(row["reversible"]):
        return "SUPPRESS_REVERSIBLE"
    if parse_bool(row["taskOwned"]):
        return "SUPPRESS_TASK_OWNED"
    if not parse_bool(row["independentOwnerRoot"]):
        return "SUPPRESS_SAME_VISIT"
    return "ADMIT"


def validate_lifecycle(rows: list[dict[str, str]], baseline: dict, log: CheckLog) -> dict:
    expected = baseline["lifecycle"]
    log.check("lifecycle.path_count", len(rows) == expected["path_records"],
              f"observed={len(rows)} expected={expected['path_records']}")

    mismatch: list[str] = []
    derived = []
    for row in rows:
        owner, kind = derive_lifecycle_owner(row)
        disposition = derive_disposition(row, owner)
        derived.append((row, owner, kind, disposition))
        if owner != row["acceptedOwner"] or kind != row["acceptedOwnerKind"] or disposition != row["acceptedDisposition"]:
            mismatch.append(
                f"{row['npc']}:{row['branch']} path={row['path']} "
                f"derived={owner}/{kind}/{disposition} "
                f"accepted={row['acceptedOwner']}/{row['acceptedOwnerKind']}/{row['acceptedDisposition']}"
            )

    log.check("lifecycle.independent_rederivation", not mismatch,
              "all path owners/dispositions re-derived" if not mismatch else "; ".join(mismatch[:8]))

    kind_counts = Counter(kind for _, _, kind, _ in derived)
    disposition_counts = Counter(disposition for _, _, _, disposition in derived)
    admitted_self = sum(1 for _, _, kind, disp in derived if kind == "self" and disp == "ADMIT")
    admitted_ancestor = sum(1 for _, _, kind, disp in derived if kind == "ancestor" and disp == "ADMIT")
    unique_admitted = {(row["npc"], owner) for row, owner, _, disp in derived if owner and disp == "ADMIT"}

    observed = {
        "self_path_owners": kind_counts["self"],
        "ancestor_path_owners": kind_counts["ancestor"],
        "admitted_self_paths": admitted_self,
        "admitted_ancestor_paths": admitted_ancestor,
        "task_owned_suppressed_paths": disposition_counts["SUPPRESS_TASK_OWNED"],
        "same_visit_suppressed_paths": disposition_counts["SUPPRESS_SAME_VISIT"],
        "reversible_suppressed_paths": disposition_counts["SUPPRESS_REVERSIBLE"],
        "unique_admitted_owners": len(unique_admitted),
    }
    for key, value in observed.items():
        log.check(f"lifecycle.{key}", value == expected[key],
                  f"observed={value} expected={expected[key]}")

    ancestor_by_owner: dict[tuple[str, str], set[str]] = defaultdict(set)
    for row, owner, kind, disposition in derived:
        if owner and kind == "ancestor":
            ancestor_by_owner[(row["npc"], owner)].add(disposition)

    expected_ancestors = {
        (item["npc"], item["owner"]): item["disposition"]
        for item in expected["ancestor_owners"]
    }
    actual_ancestors = {}
    ambiguous = []
    for key, dispositions in ancestor_by_owner.items():
        if len(dispositions) != 1:
            ambiguous.append(f"{key} -> {sorted(dispositions)}")
        else:
            actual_ancestors[key] = next(iter(dispositions))

    log.check("lifecycle.ancestor_owner_disposition_consistency", not ambiguous,
              "one disposition per ancestor owner" if not ambiguous else "; ".join(ambiguous))
    log.check("lifecycle.ancestor_owner_set", actual_ancestors == expected_ancestors,
              f"observed={sorted((a,b,c) for (a,b),c in actual_ancestors.items())} "
              f"expected={sorted((a,b,c) for (a,b),c in expected_ancestors.items())}")

    controls = {
        ("npc_cultist", "@snake_1с"): "ADMIT",
        ("npc_merchant", "@merchant_2b"): "SUPPRESS_TASK_OWNED",
        ("npc_merchant", "@merchant_2e_1e"): "ADMIT",
        ("npc_merchant", "@merchant_favore_done"): "SUPPRESS_TASK_OWNED",
    }
    for key, expected_disp in controls.items():
        log.check(f"lifecycle.control.{key[0]}.{key[1]}",
                  actual_ancestors.get(key) == expected_disp,
                  f"observed={actual_ancestors.get(key)} expected={expected_disp}")

    diary = [
        (row, disp) for row, _, _, disp in derived
        if row["npc"] == "npc_astrologer" and row["branch"] in {"astrologer_diary_9a", "astrologer_diary_9b"}
    ]
    log.check("lifecycle.control.astrologer_diary_same_visit",
              len(diary) == 2 and all(disp == "SUPPRESS_SAME_VISIT" for _, disp in diary),
              f"rows={[(r['branch'], d) for r,d in diary]}")

    return {
        "records": len(rows),
        "derived_summary": observed,
        "ancestor_owners": [
            {"npc": npc, "owner": owner, "disposition": disp}
            for (npc, owner), disp in sorted(actual_ancestors.items())
        ],
    }


def parse_task_fixture(path: Path) -> dict:
    sections: dict[str, list[list[str]]] = defaultdict(list)
    section = None
    header = None
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1]
            header = None
            continue
        if section is None:
            raise ValueError(f"data outside section: {raw}")
        cols = raw.split("\t")
        if header is None:
            header = cols
        else:
            sections[section].append(dict(zip(header, cols)))
    return sections


def validate_task_census(sections: dict, baseline: dict, log: CheckLog) -> dict:
    expected = baseline["task_census"]
    npc_rows = sections.get("npc", [])
    candidate_rows = sections.get("candidate", [])
    mapped_controls = sections.get("selected-mapped-controls", [])

    observed_complete = sum(int(x["ownerComplete"]) for x in npc_rows)
    observed_selectable = sum(int(x["mappedSelectable"]) for x in npc_rows)
    observed_unresolved = sum(int(x["candidateNonSelectable"]) for x in npc_rows)

    log.check("tasks.npc_count", len(npc_rows) == 6, f"observed={len(npc_rows)} expected=6")
    log.check("tasks.owner_complete_nodes", observed_complete == expected["owner_complete_nodes"],
              f"observed={observed_complete} expected={expected['owner_complete_nodes']}")
    log.check("tasks.probe_selectable", observed_selectable == expected["probe_0_1_1_selectable"],
              f"observed={observed_selectable} expected={expected['probe_0_1_1_selectable']}")
    log.check("tasks.probe_unresolved", observed_unresolved == expected["probe_0_1_1_unresolved"],
              f"observed={observed_unresolved} expected={expected['probe_0_1_1_unresolved']}")

    expected_per_npc = {
        x["npc"]: (x["complete"], x["selectable"], x["unresolved"])
        for x in expected["per_npc"]
    }
    actual_per_npc = {
        x["npc"]: (int(x["ownerComplete"]), int(x["mappedSelectable"]), int(x["candidateNonSelectable"]))
        for x in npc_rows
    }
    log.check("tasks.per_npc_census", actual_per_npc == expected_per_npc,
              f"observed={actual_per_npc} expected={expected_per_npc}")

    expected_candidates = {(x["npc"], x["task"]) for x in expected["unresolved_resolution"]}
    actual_candidates = {(x["npc"], x["task"]) for x in candidate_rows}
    log.check("tasks.unresolved_candidate_set", actual_candidates == expected_candidates,
              f"observed={sorted(actual_candidates)} expected={sorted(expected_candidates)}")

    resolution = expected["unresolved_resolution"]
    recovered = sum(1 for x in resolution if x["kind"] == "selectable_event_hop")
    event_only = sum(1 for x in resolution if x["kind"] == "event_only")
    final_selectable = observed_selectable + recovered

    log.check("tasks.final_selectable", final_selectable == expected["final_selectable_task_completions"],
              f"observed={final_selectable} expected={expected['final_selectable_task_completions']}")
    log.check("tasks.final_event_only", event_only == expected["final_event_only_task_stages"],
              f"observed={event_only} expected={expected['final_event_only_task_stages']}")
    log.check("tasks.final_partition",
              final_selectable + event_only == observed_complete,
              f"selectable={final_selectable} eventOnly={event_only} total={observed_complete}")

    expected_controls = {
        ("npc_astrologer", "dlc_souls_s29_1", "@souls_s_s30_ask"),
        ("npc_cultist", "dlc_souls_s29_3", "@souls_s_s33_ask"),
        ("npc_actress", "dlc_souls_s29_2", "@souls_s_s31_ask"),
        ("npc_bishop", "bishop_rcitezen", "@bishop_get_citezen"),
    }
    actual_controls = {(x["npc"], x["task"], x["answer"]) for x in mapped_controls}
    log.check("tasks.selected_mapped_controls", actual_controls == expected_controls,
              f"observed={sorted(actual_controls)} expected={sorted(expected_controls)}")

    return {
        "owner_complete_nodes": observed_complete,
        "probe_selectable": observed_selectable,
        "probe_unresolved": observed_unresolved,
        "final_selectable": final_selectable,
        "final_event_only": event_only,
    }


def validate_complete_snapshots(
    task_routes: list[dict[str, str]],
    navigation_rows: list[dict[str, str]],
    raw_rows: list[dict[str, str]],
    baseline: dict,
    log: CheckLog,
) -> dict:
    raw_expected = baseline["raw_interaction_universe"]

    log.check("snapshot.task_routes.rows", len(task_routes) == 72,
              f"observed={len(task_routes)} expected=72")
    selectable = [x for x in task_routes if x["kind"] == "SELECTABLE"]
    unresolved = [x for x in task_routes if x["kind"] == "EVENT_OR_UNRESOLVED"]
    log.check("snapshot.task_routes.selectable", len(selectable) == 70,
              f"observed={len(selectable)} expected=70")
    unresolved_set = {(x["npc"], x["task"]) for x in unresolved}
    expected_unresolved = {
        ("npc_inquisitor", "inquisitor_talk"),
        ("npc_cultist", "snake_back"),
    }
    log.check("snapshot.task_routes.event_only_set", unresolved_set == expected_unresolved,
              f"observed={sorted(unresolved_set)} expected={sorted(expected_unresolved)}")

    log.check("snapshot.navigation.rows", len(navigation_rows) == baseline["navigation"]["snapshot_fixture_paths"],
              f"observed={len(navigation_rows)} expected={baseline['navigation']['snapshot_fixture_paths']}")
    nav_keys = {(x["npc"], x["answer"], x["pathIndex"]) for x in navigation_rows}
    log.check("snapshot.navigation.identity_unique", len(nav_keys) == len(navigation_rows),
              f"unique={len(nav_keys)} rows={len(navigation_rows)}")
    unsupported = [x for x in navigation_rows if x["unsupported"] != "False"]
    log.check("snapshot.navigation.unsupported_zero", not unsupported,
              "all paths supported" if not unsupported else f"unsupported rows={unsupported[:8]}")

    by_kind = Counter(x["kind"] for x in raw_rows)
    expected_kind_counts = {
        "answer": raw_expected["answer_occurrences"],
        "task_state": raw_expected["task_state_transitions"],
        "custom_event": raw_expected["custom_events"],
        "add_interaction": raw_expected["add_interaction_events"],
        "remove_interaction": raw_expected["remove_interaction_events"],
    }
    for kind, expected in expected_kind_counts.items():
        observed = by_kind[kind]
        log.check(f"snapshot.raw.{kind}", observed == expected,
                  f"observed={observed} expected={expected}")

    answers = [x for x in raw_rows if x["kind"] == "answer"]
    raw_unique = {(x["npc"], x["id"]) for x in answers}
    nav_unique = {(x["npc"], x["answer"]) for x in navigation_rows}
    no_root = sorted(raw_unique - nav_unique)

    log.check("snapshot.raw.unique_answer_ids",
              len(raw_unique) == raw_expected["unique_answer_ids"],
              f"observed={len(raw_unique)} expected={raw_expected['unique_answer_ids']}")
    log.check("snapshot.raw.navigation_backed_unique_answers",
              len(raw_unique & nav_unique) == raw_expected["navigation_backed_unique_answers"],
              f"observed={len(raw_unique & nav_unique)} expected={raw_expected['navigation_backed_unique_answers']}")
    log.check("snapshot.raw.no_interaction_root_unique_answers",
              len(no_root) == raw_expected["no_interaction_root_unique_answers"],
              f"observed={len(no_root)} expected={raw_expected['no_interaction_root_unique_answers']}")
    expected_no_root = {
        (x["npc"], x["answer"]) for x in raw_expected["no_interaction_root_answers"]
    }
    log.check("snapshot.raw.no_interaction_root_exact_set",
              set(no_root) == expected_no_root,
              f"observed={no_root} expected={sorted(expected_no_root)}")

    task_states = [x for x in raw_rows if x["kind"] == "task_state"]
    visible = [x for x in task_states if x["value2"] == "Visible"]
    complete = [x for x in task_states if x["value2"] == "Complete"]
    log.check("snapshot.raw.task_visible",
              len(visible) == raw_expected["task_visible_transitions"],
              f"observed={len(visible)} expected={raw_expected['task_visible_transitions']}")
    log.check("snapshot.raw.task_complete",
              len(complete) == raw_expected["task_complete_transitions"],
              f"observed={len(complete)} expected={raw_expected['task_complete_transitions']}")

    return {
        "task_route_rows": len(task_routes),
        "navigation_rows": len(navigation_rows),
        "raw_kind_counts": dict(by_kind),
        "raw_unique_answer_ids": len(raw_unique),
        "navigation_backed_unique_answers": len(raw_unique & nav_unique),
        "no_interaction_root_unique_answers": [
            {"npc": npc, "answer": answer} for npc, answer in no_root
        ],
    }


def validate_no_root_frontier(
    rows: list[dict[str, str]],
    snapshot_report: dict,
    baseline: dict,
    log: CheckLog,
) -> dict:
    expected = baseline["raw_interaction_universe"]
    expected_classification = expected["no_root_accepted_classification"]

    observed_keys = {(x["npc"], x["answer"]) for x in rows}
    expected_keys = {
        (x["npc"], x["answer"]) for x in snapshot_report["no_interaction_root_unique_answers"]
    }

    log.check("frontier.rows", len(rows) == expected["no_root_classified"],
              f"observed={len(rows)} expected={expected['no_root_classified']}")
    log.check("frontier.identity_unique", len(observed_keys) == len(rows),
              f"unique={len(observed_keys)} rows={len(rows)}")
    log.check("frontier.exact_raw_no_root_set", observed_keys == expected_keys,
              f"observed={sorted(observed_keys)} expected={sorted(expected_keys)}")

    wrong_classification = [
        (x["npc"], x["answer"], x["classification"])
        for x in rows if x["classification"] != expected_classification
    ]
    log.check("frontier.classification",
              not wrong_classification,
              f"all={expected_classification}" if not wrong_classification else
              f"unexpected={wrong_classification}")

    root_counts = Counter(
        (x["npc"], x["rootNode"], x["rootEvent"]) for x in rows
    )
    expected_root_counts = {
        (x["npc"], x["root_node"], x["root_event"]): x["answers"]
        for x in expected["no_root_event_roots"]
    }
    log.check("frontier.event_root_contracts", dict(root_counts) == expected_root_counts,
              f"observed={dict(root_counts)} expected={expected_root_counts}")

    unknown = sorted(expected_keys - observed_keys)
    log.check("frontier.unknown_zero", len(unknown) == expected["no_root_unknown"],
              f"observed={len(unknown)} expected={expected['no_root_unknown']} unknown={unknown}")

    return {
        "classified": len(rows),
        "classification": expected_classification,
        "unknown": len(unknown),
        "event_roots": [
            {
                "npc": npc,
                "root_node": node,
                "root_event": event,
                "answers": count,
            }
            for (npc, node, event), count in sorted(root_counts.items())
        ],
    }


def validate_live_answer_dispositions(
    accepted_rows: list[dict[str, str]],
    raw_rows: list[dict[str, str]],
    lifecycle_rows: list[dict[str, str]],
    task_routes: list[dict[str, str]],
    frontier_rows: list[dict[str, str]],
    baseline: dict,
    log: CheckLog,
) -> dict:
    expected = baseline["raw_interaction_universe"]["live_answer_dispositions"]

    raw_answers = [x for x in raw_rows if x["kind"] == "answer"]
    lifecycle_by_occ: dict[tuple[str, str, str, str], list[dict[str, str]]] = defaultdict(list)
    for row in lifecycle_rows:
        lifecycle_by_occ[(row["npc"], row["multi"], row["index"], row["branch"])].append(row)

    frontier_occ = {
        (row["npc"], row["multi"], row["answer"]) for row in frontier_rows
    }

    derived: dict[tuple[str, str, str, str], tuple[str, str, str]] = {}
    conflicts: list[str] = []

    for row in raw_answers:
        key = (row["npc"], row["node"], row["index"], row["id"])
        lifecycle = lifecycle_by_occ.get(key, [])
        disposition = ""
        owner = ""
        evidence = ""

        if (row["npc"], row["node"], row["id"]) in frontier_occ:
            disposition = "EVENT_INVOKED_NON_REMINDER"
            evidence = "frontier-0.1.3"
        elif lifecycle:
            semantics = {
                (x["acceptedDisposition"], x["acceptedOwnerKind"], x["acceptedOwner"])
                for x in lifecycle
            }
            if len(semantics) != 1:
                conflicts.append(f"{key}: {sorted(semantics)}")
                continue
            accepted_disposition, owner_kind, owner = next(iter(semantics))
            if accepted_disposition == "ADMIT" and owner_kind == "self":
                disposition = "REMINDER_DIALOGUE_OWNER"
            elif accepted_disposition == "ADMIT" and owner_kind == "ancestor":
                disposition = "SAME_VISIT_DESCENDANT_OF_DIALOGUE_OWNER"
            elif accepted_disposition == "SUPPRESS_TASK_OWNED" and owner_kind == "self":
                disposition = "REMINDER_TASK_OWNED"
            elif accepted_disposition == "SUPPRESS_TASK_OWNED" and owner_kind == "ancestor":
                disposition = "SAME_VISIT_DESCENDANT_OF_TASK_OWNER"
            elif accepted_disposition == "SUPPRESS_SAME_VISIT":
                disposition = "SAME_VISIT_NON_REMINDER"
            elif accepted_disposition == "SUPPRESS_REVERSIBLE":
                disposition = "REVERSIBLE_NON_REMINDER"
            else:
                conflicts.append(f"{key}: unsupported lifecycle semantic {next(iter(semantics))}")
                continue
            evidence = "lifecycle-census"
        else:
            direct_tasks = [
                x for x in task_routes
                if x["kind"] == "SELECTABLE"
                and x["npc"] == row["npc"]
                and x["answer"] == row["id"]
                and x["trace"] == row["node"]
            ]
            if direct_tasks:
                disposition = "REMINDER_TASK_OWNED"
                owner = ",".join(sorted({x["task"] for x in direct_tasks}))
                evidence = "task-route-snapshot"
            else:
                disposition = "NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER"
                evidence = "no-task-completion-no-persistent-lifecycle"

        task_ids = ",".join(sorted({
            x["task"] for x in task_routes
            if x["kind"] == "SELECTABLE"
            and x["npc"] == row["npc"]
            and x["answer"] == row["id"]
            and x["trace"] == row["node"]
        }))
        derived[key] = (disposition, owner, task_ids, evidence)

    log.check("live_dispositions.derivation_conflicts", not conflicts,
              "none" if not conflicts else "; ".join(conflicts[:8]))

    accepted = {
        (x["npc"], x["multi"], x["index"], x["answer"]):
            (x["disposition"], x["owner"], x["tasks"], x["evidence"])
        for x in accepted_rows
    }
    log.check("live_dispositions.rows",
              len(accepted_rows) == expected["answer_occurrences"],
              f"observed={len(accepted_rows)} expected={expected['answer_occurrences']}")
    log.check("live_dispositions.identity_unique",
              len(accepted) == len(accepted_rows),
              f"unique={len(accepted)} rows={len(accepted_rows)}")
    log.check("live_dispositions.raw_exact_set",
              set(accepted) == set(derived),
              f"accepted={len(accepted)} derived={len(derived)}")
    mismatches = [
        (key, derived.get(key), value)
        for key, value in accepted.items()
        if derived.get(key) != value
    ]
    log.check("live_dispositions.exact_rederivation", not mismatches,
              "all dispositions re-derived" if not mismatches else str(mismatches[:8]))

    counts = Counter(value[0] for value in accepted.values())
    for disposition, count in expected["counts"].items():
        observed = counts[disposition]
        log.check(f"live_dispositions.count.{disposition}", observed == count,
                  f"observed={observed} expected={count}")

    unknown = sum(1 for value in accepted.values() if "UNKNOWN" in value[0])
    log.check("live_dispositions.unknown_zero", unknown == expected["unknown"],
              f"observed={unknown} expected={expected['unknown']}")

    return {
        "answer_occurrences": len(accepted_rows),
        "counts": dict(counts),
        "unknown": unknown,
    }


def extract_int_constant(text: str, name: str) -> int | None:
    match = re.search(rf"\b{name}\s*=\s*(\d+)\s*;", text)
    return int(match.group(1)) if match else None


def validate_production_source(baseline: dict, log: CheckLog) -> dict:
    manifest_path = ROOT / "src" / "PersistentRuleManifest.cs"
    lifecycle_path = ROOT / "src" / "UnifiedDialogueLifecycleCompiler.cs"
    completion_path = ROOT / "src" / "VerifiedCompletionReminderRules.cs"
    plugin_path = ROOT / "src" / "CalendarQuestsPinsPlugin.cs"

    manifest = manifest_path.read_text(encoding="utf-8")
    lifecycle = lifecycle_path.read_text(encoding="utf-8")
    completion = completion_path.read_text(encoding="utf-8")
    plugin = plugin_path.read_text(encoding="utf-8")

    expected_manifest = baseline["manifest"]
    constant_expectations = {
        "SchemaVersion": expected_manifest["schema"],
        "ExpectedOwnerSupported": expected_manifest["owner_supported"],
        "ExpectedOwnerUnsupported": expected_manifest["owner_unsupported"],
        "ExpectedCrossTasks": expected_manifest["cross_tasks"],
        "ExpectedCrossSupported": expected_manifest["cross_supported"],
        "ExpectedCrossUnsupported": expected_manifest["cross_unsupported"],
        "ExpectedAtTopics": expected_manifest["base_at_topics"],
        "ExpectedAtTopicSupported": expected_manifest["base_at_supported"],
        "ExpectedAtTopicUnsupported": expected_manifest["base_at_unsupported"],
    }
    observed_constants = {}
    for name, expected in constant_expectations.items():
        observed = extract_int_constant(manifest, name)
        observed_constants[name] = observed
        log.check(f"source.manifest.{name}", observed == expected,
                  f"observed={observed} expected={expected}")

    lifecycle_expected = {
        "ExpectedGraphCount": 6,
        "ExpectedNonAtUnique": baseline["lifecycle"]["non_at_unique"],
        "ExpectedExactSelf": baseline["lifecycle"]["exact_self"],
        "ExpectedAncestorOwnerCandidates": baseline["lifecycle"]["ancestor_owner_candidates"],
        "ExpectedAncestorTaskExcluded": baseline["lifecycle"]["ancestor_task_excluded"],
        "ExpectedAncestorAdmittedTopics": baseline["lifecycle"]["ancestor_admitted_topics"],
    }
    observed_lifecycle_constants = {}
    for name, expected in lifecycle_expected.items():
        observed = extract_int_constant(lifecycle, name)
        observed_lifecycle_constants[name] = observed
        log.check(f"source.lifecycle.{name}", observed == expected,
                  f"observed={observed} expected={expected}")

    semantic_needles = [
        "if (branch.Blacklists.Contains(branch.Key.AnswerId)) continue;",
        "for (var a = path.Ancestors.Count - 1; a >= 0; a--)",
        "if (completionAnswerIds.Contains(ownerId))",
        "if (!navigation.HasInteractionRootPathWithoutAncestors",
        "if (removals.Contains(ownerId))",
    ]
    for needle in semantic_needles:
        log.check("source.lifecycle.guard." + str(abs(hash(needle))),
                  needle in lifecycle, f"required semantic guard missing: {needle}")

    promoted_pattern = re.compile(
        r'new\s+PromotedRoute\s*\{\s*NpcId\s*=\s*"([^"]+)"\s*,\s*'
        r'TaskId\s*=\s*"([^"]+)"\s*,\s*AnswerId\s*=\s*"([^"]+)"\s*\}'
    )
    promoted_actual = set(promoted_pattern.findall(completion))
    promoted_expected = {
        (x["npc"], x["task"], x["answer"])
        for x in baseline["verified_completion_supplement"]["promoted_routes"]
    }
    log.check("source.completion.promoted_routes", promoted_actual == promoted_expected,
              f"observed={sorted(promoted_actual)} expected={sorted(promoted_expected)}")

    event_pattern = re.compile(
        r'if\s*\(string\.Equals\(target\.NpcId,\s*"([^"]+)"[^)]*\)\s*&&\s*'
        r'string\.Equals\(taskId,\s*"([^"]+)"[^)]*\)\)\s*return\s+true\s*;',
        re.S,
    )
    event_actual = set(event_pattern.findall(completion))
    event_expected = {(x["npc"], x["task"]) for x in baseline["task_census"]["event_only"]}
    log.check("source.completion.event_only_exact_set", event_actual == event_expected,
              f"observed={sorted(event_actual)} expected={sorted(event_expected)}")

    snake_trap_ok = (
        '"snake_trap"' in completion
        and 'const string answerId = "snake_stone_ready";' in completion
        and 'CreateSmartRes("GameRes", "_rel", 10f' in completion
    )
    log.check("source.completion.snake_trap_exact_route", snake_trap_ok,
              "expected snake_trap -> snake_stone_ready with GameRes:_rel 10")

    retired_snake_special = ("@snake_1с" in completion or "quest_fake_coins" in completion or
                             "@snake_1с" in plugin or "quest_fake_coins" in plugin)
    log.check("source.no_retired_snake_fake_coins_special", not retired_snake_special,
              "old 1.1.5 Snake fake-coins hard-code must not return")

    manifest_uses_lifecycle_validation = "UnifiedDialogueLifecycleCompiler.Validate" in manifest
    log.check("source.manifest.uses_lifecycle_validation", manifest_uses_lifecycle_validation,
              "manifest must reject lifecycle-universe drift")

    return {
        "manifest_constants": observed_constants,
        "lifecycle_constants": observed_lifecycle_constants,
        "promoted_routes": sorted(promoted_actual),
        "event_only": sorted(event_actual),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--lifecycle", type=Path, default=DEFAULT_LIFECYCLE)
    parser.add_argument("--tasks", type=Path, default=DEFAULT_TASKS)
    parser.add_argument("--task-routes", type=Path, default=DEFAULT_TASK_ROUTES)
    parser.add_argument("--navigation", type=Path, default=DEFAULT_NAVIGATION)
    parser.add_argument("--raw-universe", type=Path, default=DEFAULT_RAW_UNIVERSE)
    parser.add_argument("--frontier", type=Path, default=DEFAULT_FRONTIER)
    parser.add_argument("--live-dispositions", type=Path, default=DEFAULT_LIVE_DISPOSITIONS)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    args = parser.parse_args()

    baseline = load_json(args.baseline)
    log = CheckLog()

    lifecycle_rows = load_lifecycle(args.lifecycle)
    lifecycle_report = validate_lifecycle(lifecycle_rows, baseline, log)
    task_sections = parse_task_fixture(args.tasks)
    task_report = validate_task_census(task_sections, baseline, log)
    snapshot_report = validate_complete_snapshots(
        load_tsv(args.task_routes),
        load_tsv(args.navigation),
        load_tsv(args.raw_universe),
        baseline,
        log,
    )
    frontier_rows = load_tsv(args.frontier)
    frontier_report = validate_no_root_frontier(
        frontier_rows,
        snapshot_report,
        baseline,
        log,
    )
    live_disposition_report = validate_live_answer_dispositions(
        load_tsv(args.live_dispositions),
        load_tsv(args.raw_universe),
        lifecycle_rows,
        load_tsv(args.task_routes),
        frontier_rows,
        baseline,
        log,
    )
    source_report = validate_production_source(baseline, log)

    report = {
        "validator_format": 1,
        "baseline_version": baseline["baseline_version"],
        "game": baseline["game"],
        "status": "PASS" if not log.failures else "FAIL",
        "checks_total": len(log.checks),
        "checks_failed": len(log.failures),
        "failures": log.failures,
        "warnings": log.warnings,
        "coverage": {
            "dialogue_lifecycle": "path-level exhaustive for accepted census",
            "owner_task_completion": "complete 72-route snapshot plus accepted census/classification",
            "navigation": "complete 270-path production-derived snapshot; independent lifecycle oracle remains separate",
            "raw_interaction_universe": "complete accepted six-NPC graph snapshot; all 243 authored answer occurrences have exact live dispositions; UNKNOWN=0",
            "event_only": "exact accepted set",
            "production_contract": "static bounded contract checks",
        },
        "lifecycle": lifecycle_report,
        "tasks": task_report,
        "snapshot": snapshot_report,
        "frontier": frontier_report,
        "live_answer_dispositions": live_disposition_report,
        "production_source": source_report,
        "checks": log.checks,
    }

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"Day Wheel interaction validator: {report['status']}")
    print(f"Checks: {report['checks_total']} total, {report['checks_failed']} failed")
    print(
        "Lifecycle: "
        f"{lifecycle_report['records']} path records, "
        f"{lifecycle_report['derived_summary']['unique_admitted_owners']} unique admitted owners"
    )
    print(
        "Tasks: "
        f"{task_report['owner_complete_nodes']} completion nodes -> "
        f"{task_report['final_selectable']} selectable + {task_report['final_event_only']} event-only"
    )
    print(
        "Raw universe: "
        f"{snapshot_report['raw_unique_answer_ids']} unique answer IDs -> "
        f"{snapshot_report['navigation_backed_unique_answers']} navigation-backed + "
        f"{frontier_report['classified']} event-invoked non-reminders; "
        f"UNKNOWN={frontier_report['unknown']}"
    )
    print(
        "Live dispositions: "
        f"{live_disposition_report['answer_occurrences']} occurrences classified; "
        f"UNKNOWN={live_disposition_report['unknown']}"
    )
    for warning in log.warnings:
        print("COVERAGE NOTE:", warning)
    for failure in log.failures:
        print("FAIL:", failure)
    print(f"Report: {args.report.relative_to(ROOT)}")
    return 1 if log.failures else 0


if __name__ == "__main__":
    sys.exit(main())

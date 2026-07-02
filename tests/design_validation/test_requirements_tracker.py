"""Consistency checks for REQUIREMENTS.yml, the orchestration work tracker.

These tests are the machine half of the resume protocol documented in the
header of REQUIREMENTS.yml: if they fail, the tracker is in a state no
orchestrator session should trust.
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path

import pytest
import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
REQUIREMENTS_PATH = REPO_ROOT / "REQUIREMENTS.yml"

ALLOWED_STATUSES = {
    "todo",
    "in_progress",
    "in_review",
    "changes_requested",
    "approved",
    "merged",
    "blocked",
    "done",
}

# A dependency in one of these states means the dependent task cannot be finished.
UNFINISHED_STATUSES = {"todo", "in_progress"}
FINISHED_STATUSES = {"merged", "done"}

# Statuses that imply somebody is (or was) actively carrying the task.
ACTIVE_STATUSES = {"in_progress", "in_review", "approved", "merged"}


@pytest.fixture(scope="module")
def tracker() -> dict:
    assert REQUIREMENTS_PATH.is_file(), f"missing {REQUIREMENTS_PATH}"
    with REQUIREMENTS_PATH.open(encoding="utf-8") as fh:
        data = yaml.safe_load(fh)
    assert isinstance(data, dict), "REQUIREMENTS.yml must parse to a mapping"
    return data


@pytest.fixture(scope="module")
def tasks(tracker: dict) -> list[dict]:
    tasks = tracker.get("tasks")
    assert isinstance(tasks, list) and tasks, "tracker must contain a non-empty tasks list"
    for task in tasks:
        assert isinstance(task, dict), f"task entries must be mappings, got: {task!r}"
        assert task.get("id"), f"task without id: {task!r}"
    return tasks


@pytest.fixture(scope="module")
def gates(tracker: dict) -> list[dict]:
    gates = tracker.get("gates") or []
    assert isinstance(gates, list), "gates must be a list when present"
    return gates


def test_parses_and_has_core_keys(tracker: dict) -> None:
    for key in ("version", "project", "integration_branch", "tasks"):
        assert key in tracker, f"missing top-level key: {key}"


def test_task_ids_unique(tasks: list[dict], gates: list[dict]) -> None:
    ids = [t["id"] for t in tasks] + [g.get("id") for g in gates]
    duplicates = [i for i, n in Counter(ids).items() if n > 1]
    assert not duplicates, f"duplicate task/gate ids: {duplicates}"


def test_depends_on_reference_existing_ids(tasks: list[dict], gates: list[dict]) -> None:
    known = {t["id"] for t in tasks} | {g.get("id") for g in gates}
    problems = []
    for task in tasks:
        for dep in task.get("depends_on") or []:
            if dep not in known:
                problems.append(f"{task['id']} depends on unknown id {dep!r}")
    assert not problems, "\n".join(problems)


def test_statuses_in_allowed_set(tasks: list[dict]) -> None:
    problems = [
        f"{task['id']}: status {task.get('status')!r}"
        for task in tasks
        if task.get("status") not in ALLOWED_STATUSES
    ]
    assert not problems, "invalid statuses:\n" + "\n".join(problems)


def test_no_finished_task_with_unfinished_dependency(
    tasks: list[dict], gates: list[dict]
) -> None:
    status_by_id = {t["id"]: t.get("status") for t in tasks}
    status_by_id.update({g.get("id"): g.get("status") for g in gates})
    problems = []
    for task in tasks:
        if task.get("status") not in FINISHED_STATUSES:
            continue
        for dep in task.get("depends_on") or []:
            if status_by_id.get(dep) in UNFINISHED_STATUSES:
                problems.append(
                    f"{task['id']} is {task['status']} but dependency {dep} "
                    f"is {status_by_id[dep]}"
                )
    assert not problems, "\n".join(problems)


def test_active_tasks_have_owner_and_branch(tasks: list[dict]) -> None:
    """Every in_progress/in_review/approved/merged task must name its agent and
    branch. (Orchestrator-run docs/config tasks may instead go straight to
    'done' with branch: null — those are not in the active set checked here.)"""
    problems = []
    for task in tasks:
        if task.get("status") not in ACTIVE_STATUSES:
            continue
        if not task.get("assigned_to"):
            problems.append(f"{task['id']} ({task['status']}) has no assigned_to")
        if not task.get("branch"):
            problems.append(f"{task['id']} ({task['status']}) has no branch")
    assert not problems, "\n".join(problems)


def test_gates_are_human_review_with_status(gates: list[dict]) -> None:
    for gate in gates:
        assert gate.get("id"), f"gate without id: {gate!r}"
        assert gate.get("kind") == "human_review", f"{gate.get('id')}: unexpected kind"
        assert gate.get("status") in {"pending", "approved", "rejected"}, (
            f"{gate.get('id')}: unexpected gate status {gate.get('status')!r}"
        )

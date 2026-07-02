# Orchestration protocol

How work in [REQUIREMENTS.yml](../REQUIREMENTS.yml) gets built by multiple agents without
losing state when a session dies.

## Roles

- **Orchestrator** — the top-level session. Reads REQUIREMENTS.yml, selects unblocked
  tasks, fires builder subagents (each in its own git worktree on branch `task/<id>`),
  fires reviewer subagents on finished branches, merges approved branches into the
  integration branch, updates REQUIREMENTS.yml on every transition, commits and pushes at
  each stable point.
- **Builder** — works only inside its worktree, commits only to its `task/<id>` branch,
  never edits REQUIREMENTS.yml, never merges. Final message reports: branch name, commits,
  what passed/failed, anything a reviewer must scrutinise.
- **Reviewer** — read-only. Reviews `git diff integration..task/<id>` for correctness and
  conformance to AGENTS.md/ART_BIBLE.md contracts. Verdict: `approve` or
  `changes_requested` with a concrete list.

## Task lifecycle

```
todo → in_progress → in_review → approved → merged
              ↑            |
              └── changes_requested
```

Orchestrator updates the task's `status`, `assigned_to`, `branch`, `reviewer`, `review`,
`merged_commit` fields in REQUIREMENTS.yml **immediately** on each transition and includes
the file in the merge commit, so the tracker and the code land atomically.

## Merge rules

1. Merge only `review: approved` branches.
2. Fast-forward or `--no-ff` merge into the integration branch; run
   `./tools/check_all.sh` after each merge; a red gate reverts the merge and sends the
   task back to `in_progress` with notes.
3. Push the integration branch (`git push -u origin <integration_branch>`) after every
   successful merge batch. The user renames this branch to `main` in GitHub Settings
   (decision 2026-07-02) — after the rename, update `integration_branch` in
   REQUIREMENTS.yml to `main` and push there; all work lands on `main` directly.
4. Delete merged worktrees; keep `task/<id>` branches until the phase gate passes.

## Resume protocol (orchestrator session died)

1. `git checkout` the integration branch; `git pull`.
2. Read REQUIREMENTS.yml.
3. Any `in_progress` task without a `merged_commit`: inspect `task/<id>` branch if it
   exists — salvage by sending a builder to continue it, or reset to `todo`.
4. Any `approved` but unmerged task: merge it now.
5. Continue selecting unblocked `todo` tasks (all `depends_on` in `merged|done`).
6. Never start work on a task whose phase gate (`gates:` section) is `pending` if the
   task lists that gate in `depends_on`.

## Batching

Fire independent tasks in parallel batches; serialize merges. Suggested batches are
implied by `depends_on`. Keep per-agent scope to one coherent subsystem; give agents the
exact file contracts (paths, event names, class names) in the prompt so parallel branches
merge cleanly.

## Context hygiene

The orchestrator keeps its own context small: builders and reviewers absorb file-level
detail; the orchestrator holds only REQUIREMENTS.yml state, branch names, and verdicts.
State lives in the YAML + git, never only in conversation memory.

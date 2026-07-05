# LLM environment gate report

This report is for future LLM agents working in this repo. The recent F4 false alarm appears to come from confusing three different kinds of gates:

- Repo verification gates in `abbey/`.
- Local macOS Unity/MCP gates.
- A host-global Claude Code Stop hook outside this repo.

Do not treat those as the same system.

## Repo location

The active project is:

```sh
/Users/mak/Code/bettiolo/abbey
```

This is a macOS workspace. The Linux path from the F4 handoff, `/root/.claude/stop-hook-git-check.sh`, does not exist in this environment.

## Host-global Stop hook

The F4 issue concerns a host-global Claude Code Stop hook, not an `abbey/` project file.

Expected path in the affected cloud/container environment:

```sh
/root/.claude/stop-hook-git-check.sh
```

Observed in this macOS workspace:

```sh
/root/.claude/stop-hook-git-check.sh  # not present
/Users/mak/.claude/stop-hook-git-check.sh  # not present
```

Because the hook file is absent here, there is nothing safe to patch from this workspace. Do not create a repo-local replacement hook and do not edit unrelated Claude settings to simulate the cloud hook.

## F4 root cause summary

The false alarm is caused by the Stop hook checking commits with:

```sh
git log "$upstream..HEAD"
```

That range is only safe when the local branch is a linear descendant of its upstream. It is unsafe on a diverged branch where both sides have commits:

```sh
behind=$(git rev-list --count "HEAD..$upstream")
ahead=$(git rev-list --count "$upstream..HEAD")
```

If both `ahead > 0` and `behind > 0`, the local branch likely contains stale pre-rewrite history. In that case, commits in `$upstream..HEAD` are not necessarily this session's work, and the hook must not recommend `git commit --amend`, `git rebase --exec`, `--reset-author`, or a forced history rewrite.

Correct targeted fix for the affected host-global hook:

```bash
  # A local branch that has DIVERGED from its upstream (commits on BOTH sides)
  # is a stale or rewritten clone: the commits in $upstream..HEAD are then
  # pre-existing remote lineage, not this session's work, so flagging them as
  # Unverified and proposing an amend/rebase would rewrite unrelated history.
  behind=$(git rev-list --count "HEAD..$upstream" 2>/dev/null) || behind=0
  ahead=$(git rev-list --count "$upstream..HEAD" 2>/dev/null) || ahead=0
  if [[ "$behind" -gt 0 && "$ahead" -gt 0 ]]; then
    exit 0
  fi
```

Insert that after `upstream` is assigned and before the `commit.gpgsign` check. Back up the hook first in the environment where it actually exists.

## Repo verification gates

For `abbey/`, use the repo's documented gates instead of relying on the Stop hook:

```sh
./tools/check_all.sh
```

Important caveat: `check_all.sh` can skip Unity steps depending on the environment. See `AGENTS.md` and `docs/VERIFICATION_STATUS.md` before claiming Unity is verified.

## Unity/MCP gates

On local macOS with the Unity editor and MCP bridge available, use:

```sh
tools/run_unity_mcp_gate.sh
```

If MCP is already connected, use:

```sh
UNITY_MCP_UV_OFFLINE=1 tools/run_unity_mcp_gate.sh --no-restart
```

CI currently does not prove Unity compiles unless the GameCI license secrets are configured. A green GitHub workflow may still mean Unity tests were skipped.

## Guidance for future agents

- Do not run destructive git rewrites to satisfy the F4 alarm.
- Do not rewrite pre-existing commits that this session did not author.
- Do not confuse repo verification failures with the host-global Stop hook alarm.
- If `/root/.claude/stop-hook-git-check.sh` is absent, report that the F4 fix cannot be applied in the current environment.
- If the hook exists and is writable, back it up first and apply only the minimal divergence guard above.

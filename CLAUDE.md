# CosmicBlog — Claude Code Configuration

@AGENTS.md

## Git Policy

- **Do not run git write commands without explicit per-action approval.** Read-only commands (`git status`, `git diff`, `git log`, `git show`) need no approval.
- Write commands requiring per-action approval: `git add`, `git commit`, `git push`, `git reset`, `git rebase`, `git checkout -- <file>`, `git branch -D`, `git stash drop`, force-push.
- For each commit step in an execution plan: state the proposed commit message and the staged files, then wait for explicit "yes commit" (or equivalent) before running `git commit`. Do not batch approvals across tasks.
- Never use `--no-verify`, `--no-gpg-sign`, or other hook/signing bypasses unless the user explicitly asks for them.

## Planning Workflow

- Plans and specs live in a separate repo at `D:\Projects\CosmicBlog-planning\docs\superpowers\` (not in this code repo, by design — keeps planning churn out of the engine's git history).
- Current spec: `2026-04-27-jeffwidmer-blog-design.md` (rev 2).
- Current active plan: `2026-05-12-phase-1c-engine-hardening.md`.
- When executing a plan, follow the plan's task order. Do not silently expand scope; surface anything you find that's out-of-scope as an Open Question rather than rolling it into the plan.

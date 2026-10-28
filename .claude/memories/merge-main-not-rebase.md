# Merge main into a pushed branch, don't rebase

Force-pushing is refused by Claude Code's auto mode (as destructive), so a pushed PR branch can't be rebased. Bring it up to date by merging `origin/main` into it; the squash merge flattens it anyway. A branch stacked on another PR that was squash-merged since, and not pushed yet, is easiest replayed: take its own diff (`git diff <its first commit>~1 HEAD`), recreate the branch from `origin/main` and `git apply --3way` the diff.

**Why:** a rebase plus `--force-with-lease` was refused in the Milestone 1 loop (2026-10-28, #88); merging main into a stacked branch after its base was squash-merged conflicted on every file of the base PR (#92).

**How to apply:** for a pushed branch, `git merge origin/main`; for an unpushed stacked branch, replay its diff onto `origin/main`. When versions conflict, keep the branch's (higher) numbers.

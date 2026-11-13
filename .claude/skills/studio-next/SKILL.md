---
name: studio-next
description: Recommend one next development task for this Unity roguelite. Use when the user asks what to do next, how to continue, or which milestone comes first. Read-only; do not start the suggested work.
user-invocable: true
---

# One next step

Adapted from upstream next-step; see `tools/studio/UPSTREAM.md`.

Read root `AGENTS.md`, `design/game-brief.md`, `production/backlog.md` and the
current checkpoint. Use Git status and relevant evidence to check whether notes
are stale. Do not imply that a status label proves the game works.

Prioritize, in order:

1. A reproducible blocker to the current approved milestone.
2. Finishing or verifying the task already in progress.
3. The smallest playable improvement in the brief's build order.

Return:

- **Next task:** one concrete action.
- **Why now:** its connection to a fun, playable first-person loop.
- **Done means:** a short set of observable checks and any needed human playtest.
- **To start:** one `/studio-feature ...` or `/studio-bug ...` command as appropriate.

Keep missing inputs as unknowns; ask one focused question only when it changes
the next action. Do not propose paperwork, new packages or a full agent team as
the default next step. Do not edit files, commit, push or launch the proposed task.

---
name: studio-feature
description: Implement one explicitly requested Unity gameplay feature with bounded scope, tests and actual scene verification. Use only when the user invokes this workflow, not for planning or setup.
user-invocable: true
disable-model-invocation: true
---

# Build one feature

Adapted from upstream dev-story, without its mandatory story/ADR infrastructure.
See `tools/studio/UPSTREAM.md` and read root `AGENTS.md` first.

1. Read the brief, relevant backlog/checkpoint, Git status and exact affected
   source. Treat the invocation's arguments as the requested feature. If absent,
   ask which feature; do not silently start an entire milestone.
2. State scope, affected systems, explicit exclusions and observable acceptance
   criteria. Ask only for unresolved decisions that materially affect the work.
   A direct implementation request authorizes routine edits inside that scope;
   a planning request does not.
3. Prefer a focused extension of the existing controller/system. Do not replace
   the game, migrate input packages, add ECS, or touch hair/cosmetics unless asked.
4. If specialist delegation is useful, name the role, bounded task and ownership
   and obtain approval before spawning it. Default to one implementer, not a team.
   The specialist inherits the session model; do not choose another provider.
5. For a reported bug, first follow `studio-bug`. For a new mechanic, make a
   small testable slice, then exercise it at its real call site. Fetch current
   version-matched API docs when necessary.
6. Run relevant compilation, automated and scene checks. Do not launch a second
   editor on the user's open project. Use an approved isolated copy if needed.
   For movement, verify start/stop, diagonal input, jump/landing, slopes, low
   ceilings, slide exits, and mantling only where the feature touches them.
7. Inspect visual changes in the actual first-person camera and scene. Full-body
   animation fixes also need full-body evidence. Preserve screenshots/recordings
   if the scope includes durable QA evidence.
8. Report each acceptance criterion as PASS, FAIL or NOT ASSESSED, citing actual
   runs and evidence. A missing check means awaiting verification, not complete.
   Say when human playtesting is still needed.

Do not auto-commit, push, close bugs, declare user approval, or expand into the
next feature. Keep documentation/checkpoint updates proportional to the task.

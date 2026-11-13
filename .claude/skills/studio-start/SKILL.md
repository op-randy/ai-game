---
name: studio-start
description: Assess this existing Unity game and orient a development session. Use when the user starts, resumes, onboards, or asks where the game stands. Read-only; not a new-game generator.
user-invocable: true
---

# Studio start — existing project assessment

This is a lean adaptation of the upstream project-stage-detect workflow, not
the upstream browser-first onboarding process. See `tools/studio/UPSTREAM.md`.

1. Read root `AGENTS.md`, `project.yaml`, `design/game-brief.md`,
   `production/backlog.md` and `production/session-state/active.md`.
2. Check Git status, the Unity version and installed packages. Inspect only the
   source and scene information relevant to the user's request. Use the available
   source index if applicable; verify exact files before proposing changes.
3. Distinguish what exists, what has been run, what the user has approved, and
   what remains a proposal. Check the configured stage against observed behavior;
   code count alone does not demonstrate a playable game or a ready milestone.
4. For the first movement assessment, begin with
   `Assets/Scripts/FPS/FirstPersonController.cs`, `RifleController.cs`, and the
   shooting-range builder. Keep the separate third-person demo distinct.
5. Identify blockers and known user-reported faults without claiming to have
   reproduced them merely from reading the code.
6. Report in chat: current playable scope, relevant risks, checks actually run,
   and exactly one suggested next task with observable acceptance criteria.

Do not write reports, change settings, scaffold a browser game, rebuild scenes,
run a second editor on an open project, or start implementation from this skill.
Missing GDDs, epics and an art bible are not blockers in this minimal workflow.
If asked to save findings, get the destination/scope clear and save only those findings.

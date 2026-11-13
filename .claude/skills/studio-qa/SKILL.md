---
name: studio-qa
description: Check a Unity feature or milestone using real compilation, tests and visual gameplay evidence. Use for smoke checks, acceptance checks, regressions or verifying whether a fix actually works. Report limitations; do not auto-fix game code.
user-invocable: true
---

# Honest Unity QA

Adapted from upstream smoke-check and its separation of observations from
verdicts. See `tools/studio/UPSTREAM.md` and root `AGENTS.md`.

1. Establish the requested scene, feature, expected behavior, relevant changes
   and acceptance criteria. If unclear, ask for the one input needed to test.
2. Read the Unity version, package manifest and existing test/check entry points.
   Do not assume NUnit, Unity Test Framework, a build profile or a playable
   executable exists. `CharacterMotionChecks`/`PonytailChecks` are limited editor
   smoke checks and do not prove foot quality, player input or real mesh clearance.
3. Choose safe checks. Check editor ownership before any batch command. Never
   kill the user's editor, regenerate their scene, or run an unreviewed builder.
   Ask before creating an isolated copy when permission/scope is unclear.
4. Run the actual checks available for this task and retain relevant results.
   Validate return codes and logs; a zero exit code does not override logged
   exceptions, NaNs, missing tests, or a failed acceptance criterion.
5. For player-visible work, launch and inspect the actual scene/view. Record the
   exact scene, state, camera and evidence. A headless numerical fixture is not
   a visual inspection; unavailable gameplay is NOT ASSESSED.
6. Keep human confirmation separate from automated observations. Do not fabricate
   a played session or use prior screenshots as evidence for untested new code.
7. Report a compact matrix: criterion, PASS/FAIL/NOT ASSESSED, evidence, limitation.
   List the smallest required next action. Do not auto-fix source, close reports,
   or commit/push. Save a report only when the user requests one or approves its path.

Compilation, numerical checks, rendered frames, interactive input, performance
and subjective movement feel are separate categories. Never collapse them into
one green "game tested" verdict.

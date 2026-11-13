# AI Game — shared development instructions

## Project and direction

- This is an existing Unity **6000.6.8f1**, C#, URP **17.6.0** project. Confirm versions against `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` before version-sensitive changes.
- The goal is a first-person movement-focused roguelite: responsive sprinting, sliding, jumping and mantling, followed by encounters, XP, run upgrades, stages and persistent skill-tree choices.
- Read `design/game-brief.md`, `project.yaml`, `production/backlog.md` and the relevant checkpoint in `production/session-state/active.md`. Proposed systems are not implemented features.
- `Assets/Scripts/FPS/FirstPersonController.cs` and the shooting range are the existing first-person starting point. Do not build a second game or switch engines.
- The combat mix, signature movement ability, target release platforms and multiplayer scope still need the user's decisions. Do not quietly invent them.

## How to work

- Work on one bounded task at a time. Explain the intended changes, then proceed within the user's approved scope; do not require a new permission question for every routine edit.
- Planning, assessment and QA requests do not authorize gameplay changes. Ask before expanding scope, adding dependencies, changing engine versions, replacing assets, or rebuilding generated scenes.
- Use the existing code style and simplest suitable Unity architecture. Do not impose ECS, Addressables, assembly definitions, a new input system, or a large documentation pipeline just because a template recommends them.
- Prefer the available code index for source exploration, verifying exact source before edits. Fetch version-matched API documentation with Context7, or official documentation if unavailable.
- The project studio skills are `studio-start`, `studio-next`, `studio-feature`, `studio-qa`, `studio-bug`, and `studio-art`. Their workflows work in Claude Code and OpenCode without shell preprocessing or third-party hooks.
- Available specialists are `studio-unity`, `studio-gameplay`, `studio-tech-art`, and `studio-qa`. Delegate only when the user explicitly requests it or approves a plan naming the delegation. Give a specialist a bounded task, relevant paths and acceptance criteria. Do not automatically launch a whole team.
- Keep your current provider/model; these tools do not require a paid model switch. An agent role is guidance, not proof of expertise or a security sandbox.

## Protect existing work

- Preserve `.meta` files, asset GUIDs, scene references, original FBX/Blender sources and Git LFS settings.
- Unity-generated caches, logs, IDE projects, credentials and the local code index do not belong in Git. Never print or store credentials in prompts or reports.
- Do not hand-edit Unity scene/prefab/asset YAML while the editor holds the scene. Prefer editor tooling and reviewed builders.
- `Assets/Scripts/Editor/AutoRebuild.cs` regenerates scenes when its content version changes. Do not bump it or run builders casually: generated scene changes can replace manual edits.
- Never open the same project in a second batch Unity instance while its editor is open. Use an approved isolated test copy, or ask for a safe opportunity to run it. Do not terminate the user's editor.
- Do not commit, push, publish, or create external issues unless requested for the current task. Do not revert work you did not make.
- Start each task with Git status. Do not change game code while configuring assistant tooling.

## Evidence and visual quality

- For bugs, reproduce the user's actual symptom before changing code. Do not substitute a passing toy fixture for a failing animation, camera or movement sequence.
- A compile pass is not a gameplay pass. A numerical physics pass is not proof that hair looks natural or that feet animate correctly.
- `CharacterMotionChecks` and `PonytailChecks` are limited editor smoke checks, not a complete Unity Test Framework suite. The installed package manifest is authoritative about which test runners exist.
- Check visual changes on the actual character, scene and camera. Keep screenshots or recordings under `production/qa/evidence/` when sharing durable evidence; scratch captures can stay in ignored `Logs/`.
- For character animation, inspect full-body idle, walk, run, jump, crouch and transitions as relevant. Check retargeting, foot placement and collider alignment, not only whether clip/state names exist.
- Record each acceptance criterion as **PASS**, **FAIL**, or **NOT ASSESSED**, with the command, scene/view, evidence and limitations. Never turn a missing run or unavailable editor into a pass.
- Keep human playtesting separate: do not claim that a person has played or approved something unless they have said so.
- Do not mark a feature complete if a required visual or gameplay check is unassessed. Report it as implemented but awaiting verification.

## Lightweight handoff

Summarize the outcome, changed paths, tests actually run, remaining issues, and one next step. Update project notes only when the task calls for it; do not manufacture design documents or completed milestones.

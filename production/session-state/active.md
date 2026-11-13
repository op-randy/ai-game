# Session checkpoint

## Current task

Configured a project-local, Unity-focused adaptation of Claude Code Game Studios
for OpenCode and Claude Code. No gameplay was implemented during installation.

## Verification

- Dependency-free setup validator and all 15 regression tests passed.
- Project-scoped OpenCode discovery lists all 6 skills, 6 commands and 4 specialists.
- `claude doctor` reports no installation issues. Claude project definitions were
  statically validated; model-driven workflow execution was not tested.
- No upstream hooks, plugins, global settings changes or model/provider overrides
  were installed. No game source/assets/settings were changed.
- Setup changes have not been committed or pushed.

## Confirmed context

- Unity 6000.6.8f1, C#, URP 17.6.0.
- Git repository: `op-randy/ai-game`, branch `main`; models and Blender sources use Git LFS.
- User goal: first-person movement-focused roguelite with stages, XP and skill trees.
- Existing first-person range and rifle are the intended starting point.
- No signature ability, combat mix or multiplayer scope has been selected.

## Next suggested action

Run `/studio-start` for a read-only assessment, then `/studio-next` for one bounded
recommendation. Do not treat this suggestion as approval to change the controller.

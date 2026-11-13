# Upstream and adaptation record

- **Source:** https://github.com/Donchitos/Claude-Code-Game-Studios
- **Pinned commit:** `abc0de79d6ceb0b5d7aa693945849b3937afbe6a`
- **Upstream framework version:** 1.3.0
- **Reviewed:** 2026-11-12
- **License:** MIT, Copyright (c) 2026 Donchitos; see `LICENSE.upstream`.

## Adapted material

| Local workflow / role | Upstream source at the pinned commit |
| --- | --- |
| studio-start | `.claude/skills/project-stage-detect/SKILL.md` |
| studio-next | `.claude/skills/next-step/SKILL.md` |
| studio-feature | `.claude/skills/dev-story/SKILL.md` |
| studio-qa | `.claude/skills/smoke-check/SKILL.md` |
| studio-bug | `.claude/skills/bug-report/SKILL.md` |
| studio-art | `.claude/skills/asset-audit/SKILL.md`, `.claude/agents/technical-artist.md` |
| studio-unity | `.claude/agents/unity-specialist.md` |
| studio-gameplay | `.claude/agents/gameplay-programmer.md` |
| studio-tech-art | `.claude/agents/technical-artist.md` |
| studio-qa agent | `.claude/agents/qa-tester.md` |

This is a **rewritten, self-contained lean adaptation**, not an unmodified
installation or a promise that all upstream commands exist. The local role and
workflow text is the operative version. Original files can be compared through
the pinned GitHub commit; their relative helper paths are not local dependencies.

## Deliberate compatibility changes

- Six namespaced workflows avoid collisions with existing global skills.
- One shared `.claude/skills/` source is discovered by both clients. OpenCode
  additionally gets six native command launchers; Claude gets skill slash commands.
- Four specialist role bodies are shared by native client-specific agent wrappers.
  Claude uses `model: inherit`; OpenCode omits a model override.
- Shell preprocessing, `${CLAUDE_SKILL_DIR}`, upstream helper dependencies,
  Godot/web defaults and mandatory story/ADR infrastructure were removed from
  active workflows. Ordinary tool use replaces host-specific tool names.
- No upstream `settings.json`, hooks, plugins, MCP servers, auto-update scripts,
  model aliases, dependency installers or permission grants were installed.
- The shared project instructions favor the existing Unity project and preserve
  user-level provider, authentication, MCP and permission settings.
- Numerical, visual, interactive and human checks are explicitly separate.
  Missing evidence is not promoted to a pass.

## Updating safely

Compare this pinned source with a new upstream revision in a separate checkout.
Review changes to the selected roles/workflows, update the local adaptation and
this record, and re-run `python3 tools/studio/check_setup.py` and its tests.
Do not `git pull` an unrelated template into the game or copy its whole config
over this integration. Do not auto-update active instructions from the network.

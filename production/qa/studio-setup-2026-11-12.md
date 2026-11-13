# Dual-client studio setup verification

## Installation scope

Project-local, lean adaptation of Claude Code Game Studios at
`abc0de79d6ceb0b5d7aa693945849b3937afbe6a`. Six shared workflows and four shared
specialist roles with native Claude Code and OpenCode wrappers. See
`tools/studio/UPSTREAM.md` for attribution and compatibility differences.

## Checks actually run

| Check | Result | Evidence / limitation |
| --- | --- | --- |
| Static setup validation | PASS | `python3 tools/studio/check_setup.py`: 6 shared skills, 6 command launchers, 4 role pairs; engine pin and references validated |
| Validator regression tests | PASS | `python3 -m unittest discover -s tools/studio/tests -v`: 15 tests passed, including deliberate broken configurations |
| OpenCode runtime skill discovery | PASS | Project-scoped `/api/skill` lists all six `studio-*` skills, including explicit-invocation-only `studio-feature` |
| OpenCode runtime command discovery | PASS | Project-scoped `/api/command` lists all six `/studio-*` launchers |
| OpenCode runtime agent discovery | PASS | Project-scoped `/api/agent` lists all four specialist IDs as subagents without model overrides |
| Claude Code installation health | PASS | `claude doctor`, version 2.1.322: no installation issues found |
| Claude project definitions | PASS — static only | Frontmatter, matching names, inherited model, shared role references and `CLAUDE.md` import checked; no model-driven invocation performed |
| Actual skill behavior in Claude/OpenCode | NOT ASSESSED | Discovery/structure checks are not a behavioral benchmark; no paid LLM evaluation or game work was started |
| Game source/assets/settings unchanged | PASS | `git diff --stat -- Assets ArtSource Packages ProjectSettings` returned no changes during setup |
| Unity/gameplay/human playtest | NOT ASSESSED — outside setup | No Unity session, game test or playtest was run for this tooling installation |

## Discovery detail

The CLI API defaults to the server's home-directory location, not necessarily
the shell's current project. Verification explicitly used the documented
`location[directory]` query parameter pointing to this game, and read the returned
`data` envelope. An empty list from the wrong location is not a failed install.
No global reload/restart was needed to confirm project discovery.

## Handoff

From the game root, run `/studio-start`, then `/studio-next`. OpenCode reloads
project definitions automatically; `/reload` is available if a client view is
stale. In Claude Code, start a fresh project-root session to load the new project
instructions and inspect the `/studio-` commands and `/agents` list.

No hooks, dependency installers, external services, automatic commits/pushes or
game changes were enabled. The current setup changes remain uncommitted.

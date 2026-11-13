# Game studio tooling — Claude Code and OpenCode

Project-local, lean adaptation of [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios).
The engine and game are unchanged. This is a useful workflow layer, not an
autonomous studio, a new model, or a guarantee of better art.

## Start here

Open either client **from the `ai-game` project root**, not `Assets/`:

```sh
opencode
# or
claude
```

Then run:

```text
/studio-start
/studio-next
```

The first assesses this existing project; the second proposes one next task.
Neither changes the game. After reviewing the scope, you can invoke:

```text
/studio-feature Polish first-person sliding and slide-jumping in the existing range; preserve the rifle and leave cosmetics/progression alone
```

That is an example request, not work automatically started by installation.

## Workflows

| Command in either client | Purpose |
| --- | --- |
| `/studio-start [focus]` | Read-only project assessment and session orientation |
| `/studio-next [context]` | One recommended next task, without starting it |
| `/studio-feature <feature>` | Explicitly implement one scoped feature with real verification |
| `/studio-qa <scope>` | Check acceptance, report evidence, do not auto-fix |
| `/studio-bug <issue>` | Reproduce a real symptom, diagnose, then fix only when requested |
| `/studio-art <scope>` | Technical-art/rig/animation/hair assessment or an approved visual change |

OpenCode can also load the shared skills directly, for example `@studio-start`.
`studio-feature` is intentionally explicit-invocation-only, not an automatically
triggered planning skill. The native OpenCode launcher loads it when requested.

## Specialists

Ask the primary assistant to use a named specialist when you want delegation:

```text
Use the studio-tech-art specialist to audit the character's leg retargeting.
Read-only findings first; do not replace the rig or clips.
```

- `studio-unity`: engine integration and Unity-specific behavior.
- `studio-gameplay`: movement, combat and progression implementation.
- `studio-tech-art`: rigging, animation, materials and hair integration.
- `studio-qa`: independent checks and evidence.

Agents inherit the current session model. No specialist runs automatically on
startup. Roles constrain intent, not the operating system; ordinary client
permissions still apply. Multiple clients should not edit the same task/files
simultaneously. Agree ownership or use separate branches/worktrees when needed.

## What is installed where

- `AGENTS.md`: common project policy and context for OpenCode.
- `CLAUDE.md`: imports that same policy for Claude Code.
- `project.yaml`, `design/game-brief.md`, `production/backlog.md`: lightweight game context.
- `.claude/skills/studio-*/SKILL.md`: six shared workflow definitions.
- `.claude/agents/`: Claude-native wrappers.
- `.opencode/agents/`: OpenCode V2-native wrappers.
- `.opencode/commands/`: native slash-command launchers.
- `.opencode/opencode.json`: cache watcher exclusions and ask-before-push rule;
  no model, provider, authentication or MCP overrides.
- `tools/studio/roles/`: shared specialist instructions.
- `tools/studio/UPSTREAM.md`, `LICENSE.upstream`: pinned origin and attribution.

This does **not** install the full upstream `/prototype`, `/start`, `/dev-story`,
49-agent hierarchy, hooks or automatic commit/rebuild machinery. It does not
create a Unity Test Framework suite or replace missing gameplay evidence.
User-level client settings and other projects are not modified.

## Reload and verification

OpenCode watches project files; `/reload` forces a refresh if needed. In Claude
Code use a fresh project-root session to ensure the new `CLAUDE.md`, agents and
skill inventory are loaded; inspect `/agents` and the `/studio-` commands.

Run the dependency-free local setup checks:

```sh
python3 tools/studio/check_setup.py
python3 -m unittest discover -s tools/studio/tests -v
```

These check structure, parity, references and configuration boundaries. They
do not call an LLM, run Unity, or prove the workflow improves a game. An actual
client session and user playtesting remain separate checks. Do not report
Claude skill execution as tested if only its files were validated.

## Source and updates

See `UPSTREAM.md`. Updates are reviewed, pinned and manual. No network script
runs on session start. No new external service, key or subscription is required
by this installation; each client still needs its own existing authentication.

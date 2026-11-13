#!/usr/bin/env python3
"""Validate this lean dual-client installation without running hooks or an LLM."""

from __future__ import annotations

import json
from pathlib import Path
import re
import sys


SKILLS = ("studio-start", "studio-next", "studio-feature", "studio-qa", "studio-bug", "studio-art")
ROLES = {
    "studio-unity": "unity-specialist.md",
    "studio-gameplay": "gameplay-programmer.md",
    "studio-tech-art": "technical-artist.md",
    "studio-qa": "qa-tester.md",
}


def frontmatter(text: str) -> tuple[dict[str, str], str]:
    """Parse only the flat scalar frontmatter used by this installation.

    This is intentionally not a general YAML parser or a client config schema.
    Reject unsupported structure rather than silently misreading it.
    """
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        raise ValueError("missing frontmatter")
    try:
        end = lines.index("---", 1)
    except ValueError as exc:
        raise ValueError("unterminated frontmatter") from exc
    fields: dict[str, str] = {}
    for line in lines[1:end]:
        if not line.strip() or line.startswith("#"):
            continue
        match = re.fullmatch(r"([A-Za-z][A-Za-z0-9_-]*):\s*(.+)", line)
        if not match:
            raise ValueError(f"unsupported frontmatter line: {line!r}")
        key, value = match.groups()
        if key in fields:
            raise ValueError(f"duplicate frontmatter field: {key}")
        fields[key] = value.strip().strip('"').strip("'")
    return fields, "\n".join(lines[end + 1:]).strip()


def validate(root: Path) -> list[str]:
    root = root.resolve()
    errors: list[str] = []

    def read(relative: str) -> str:
        path = root / relative
        if not path.is_file():
            errors.append(f"missing file: {relative}")
            return ""
        return path.read_text(encoding="utf-8")

    def document(relative: str) -> tuple[dict[str, str], str]:
        text = read(relative)
        if not text:
            return {}, ""
        try:
            return frontmatter(text)
        except ValueError as exc:
            errors.append(f"{relative}: {exc}")
            return {}, ""

    required = (
        "AGENTS.md", "CLAUDE.md", "project.yaml", "design/game-brief.md",
        "production/backlog.md", "production/session-state/active.md",
        "tools/studio/README.md", "tools/studio/UPSTREAM.md", "tools/studio/LICENSE.upstream",
    )
    content = {path: read(path) for path in required}
    if "@AGENTS.md" not in content["CLAUDE.md"].splitlines():
        errors.append("CLAUDE.md must import @AGENTS.md")
    if "Copyright (c) 2026 Donchitos" not in content["tools/studio/LICENSE.upstream"]:
        errors.append("upstream license attribution is missing")
    pin = re.search(r"upstream_commit:\s*([a-f0-9]{40})\b", content["project.yaml"])
    if not pin or pin.group(1) not in content["tools/studio/UPSTREAM.md"]:
        errors.append("upstream commit pin is missing or inconsistent")

    version_text = read("ProjectSettings/ProjectVersion.txt")
    version = re.search(r"^m_EditorVersion:\s*(\S+)", version_text, re.MULTILINE)
    engine = re.search(r"^engine:\n((?:[ \t].*\n|\n)+)", content["project.yaml"], re.MULTILINE)
    if not engine or "name: Unity" not in engine.group(1):
        errors.append("project.yaml must configure the existing Unity engine")
    elif not version or f"version: {version.group(1)}" not in engine.group(1):
        errors.append("project.yaml engine version differs from ProjectVersion.txt")

    for skill in SKILLS:
        path = f".claude/skills/{skill}/SKILL.md"
        fields, body = document(path)
        if fields.get("name") != skill:
            errors.append(f"{path}: name must match directory")
        if not fields.get("description") or fields.get("user-invocable") != "true":
            errors.append(f"{path}: missing description or user invocation")
        if "model" in fields or "allowed-tools" in fields or "hooks" in fields:
            errors.append(f"{path}: host-specific model/tool/hook override is not part of the shared skill")
        if "!`" in body or "${CLAUDE_SKILL_DIR}" in body:
            errors.append(f"{path}: shell preprocessing is not portable")
        if "AGENTS.md" not in body:
            errors.append(f"{path}: shared project instructions are not referenced")
        if skill == "studio-feature" and fields.get("disable-model-invocation") != "true":
            errors.append(f"{path}: implementation must require explicit invocation")
        if len(body.splitlines()) > 500:
            errors.append(f"{path}: skill exceeds the lean body limit")

        command_path = f".opencode/commands/{skill}.md"
        command, template = document(command_path)
        if not command.get("description") or f"`{skill}`" not in template or "$ARGUMENTS" not in template:
            errors.append(f"{command_path}: missing description, matching skill or argument placeholder")
        if "!`" in template or "model" in command or "agent" in command or "subagent" in command:
            errors.append(f"{command_path}: unexpected execution/model override")

    for name, role_file in ROLES.items():
        role = f"tools/studio/roles/{role_file}"
        role_text = read(role)
        if "AGENTS.md" not in role_text or "UPSTREAM.md" not in role_text:
            errors.append(f"{role}: missing shared policy or attribution reference")
        claude_path = f".claude/agents/{name}.md"
        claude, claude_body = document(claude_path)
        opencode_path = f".opencode/agents/{name}.md"
        opencode, opencode_body = document(opencode_path)
        if claude.get("name") != name or claude.get("model") != "inherit":
            errors.append(f"{claude_path}: expected matching name and inherited model")
        if "permissionMode" in claude:
            errors.append(f"{claude_path}: permission mode override is not installed")
        if opencode.get("mode") != "subagent" or "model" in opencode:
            errors.append(f"{opencode_path}: expected subagent mode with no model override")
        for path, fields, body in ((claude_path, claude, claude_body), (opencode_path, opencode, opencode_body)):
            if not fields.get("description") or role not in body or "AGENTS.md" not in body:
                errors.append(f"{path}: missing description or shared role references")
        for path, fields, limit in ((claude_path, claude, "maxTurns"), (opencode_path, opencode, "steps")):
            if not fields.get(limit, "").isdigit() or int(fields[limit]) <= 0:
                errors.append(f"{path}: invalid turn/step bound")

    try:
        config = json.loads(read(".opencode/opencode.json"))
        if config.get("$schema") != "https://opencode.ai/config.json":
            errors.append("OpenCode schema reference is missing")
        if set(config) - {"$schema", "watcher", "permissions"}:
            errors.append("unexpected OpenCode config overrides; review before extending the lean setup")
        if config.get("permissions") != [{"action": "shell", "resource": "git push *", "effect": "ask"}]:
            errors.append("OpenCode must retain the narrow ask-before-push rule")
    except (json.JSONDecodeError, AttributeError, TypeError) as exc:
        errors.append(f"invalid OpenCode configuration: {exc}")

    for directory in (".claude/hooks", ".opencode/plugins"):
        if (root / directory).exists():
            errors.append(f"automatic hook/plugin directory is outside this installation: {directory}")
    return errors


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
    errors = validate(root)
    if errors:
        for error in errors:
            print(f"FAIL: {error}")
        return 1
    print(f"PASS: {len(SKILLS)} shared skills, {len(SKILLS)} OpenCode commands, {len(ROLES)} role pairs")
    print("PASS: references, engine pin, inherited models, provenance and configuration boundaries")
    print("Scope: static setup validation only; no LLM, Unity or gameplay checks were run")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Regression tests for installation validation, using isolated small fixtures."""

from pathlib import Path
import shutil
import sys
import tempfile
import unittest

STUDIO = Path(__file__).resolve().parents[1]
ROOT = STUDIO.parents[1]
sys.path.insert(0, str(STUDIO))
from check_setup import frontmatter, validate


class SetupTests(unittest.TestCase):
    def setUp(self):
        temporary_root = Path("/tmp/opencode")
        self.temporary = tempfile.TemporaryDirectory(
            prefix="studio-setup-test-", dir=temporary_root if temporary_root.is_dir() else None
        )
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for name in (".claude", ".opencode", "design", "production", "tools/studio/roles"):
            shutil.copytree(ROOT / name, self.root / name)
        files = (
            "AGENTS.md", "CLAUDE.md", "project.yaml", "tools/studio/README.md",
            "tools/studio/UPSTREAM.md", "tools/studio/LICENSE.upstream",
            "ProjectSettings/ProjectVersion.txt",
        )
        for name in files:
            destination = self.root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / name, destination)

    def replace(self, path, old, new):
        file = self.root / path
        text = file.read_text()
        self.assertIn(old, text)
        file.write_text(text.replace(old, new))

    def assert_error(self, fragment):
        self.assertTrue(any(fragment in error for error in validate(self.root)), validate(self.root))

    def test_current_installation_validates(self):
        self.assertEqual([], validate(self.root))

    def test_missing_role_is_detected(self):
        (self.root / "tools/studio/roles/technical-artist.md").unlink()
        self.assert_error("missing file: tools/studio/roles/technical-artist.md")

    def test_missing_native_launcher_is_detected(self):
        (self.root / ".opencode/commands/studio-feature.md").unlink()
        self.assert_error("missing file: .opencode/commands/studio-feature.md")

    def test_wrong_engine_pin_is_detected(self):
        self.replace("project.yaml", "version: 6000.6.8f1", "version: 6000.0.0f1")
        self.assert_error("engine version differs")

    def test_pinned_claude_model_is_detected(self):
        self.replace(".claude/agents/studio-unity.md", "model: inherit", "model: opus")
        self.assert_error("inherited model")

    def test_opencode_model_override_is_detected(self):
        self.replace(".opencode/agents/studio-unity.md", "mode: subagent", "mode: subagent\nmodel: vendor/model")
        self.assert_error("no model override")

    def test_shell_preprocessing_in_shared_skill_is_detected(self):
        with (self.root / ".claude/skills/studio-start/SKILL.md").open("a") as file:
            file.write("\n!`echo unexpected`\n")
        self.assert_error("shell preprocessing is not portable")

    def test_automatic_feature_invocation_is_detected(self):
        self.replace(".claude/skills/studio-feature/SKILL.md", "disable-model-invocation: true", "disable-model-invocation: false")
        self.assert_error("implementation must require explicit invocation")

    def test_missing_shared_policy_import_is_detected(self):
        self.replace("CLAUDE.md", "@AGENTS.md", "AGENTS.md")
        self.assert_error("must import @AGENTS.md")

    def test_missing_attribution_is_detected(self):
        self.replace("tools/studio/LICENSE.upstream", "Copyright (c) 2026 Donchitos", "Missing")
        self.assert_error("license attribution")

    def test_permission_mode_override_is_detected(self):
        self.replace(".claude/agents/studio-qa.md", "model: inherit", "model: inherit\npermissionMode: bypassPermissions")
        self.assert_error("permission mode override")

    def test_missing_command_arguments_is_detected(self):
        self.replace(".opencode/commands/studio-start.md", "$ARGUMENTS", "none")
        self.assert_error("argument placeholder")

    def test_unreviewed_opencode_overrides_are_detected(self):
        import json
        path = self.root / ".opencode/opencode.json"
        config = json.loads(path.read_text())
        config["model"] = "vendor/model"
        path.write_text(json.dumps(config))
        self.assert_error("unexpected OpenCode config overrides")

    def test_frontmatter_rejects_duplicate_fields(self):
        with self.assertRaisesRegex(ValueError, "duplicate"):
            frontmatter("---\nname: one\nname: two\n---\nBody")

    def test_frontmatter_rejects_nested_structure_instead_of_guessing(self):
        with self.assertRaisesRegex(ValueError, "unsupported"):
            frontmatter("---\nmetadata:\n  unsupported: value\n---\nBody")


if __name__ == "__main__":
    unittest.main()

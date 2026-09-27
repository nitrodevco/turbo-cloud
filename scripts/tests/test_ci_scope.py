"""Guard the CI shortcut against accidentally bypassing code checks."""

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ci_scope


class ScopeTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.previous_directory = Path.cwd()
        os.chdir(self.directory.name)
        self.environment = patch.dict(os.environ, {"GITHUB_EVENT_NAME": "pull_request"})
        self.environment.start()
        for name in ci_scope.REQUIRED_FILES:
            self.write(name)

    def tearDown(self):
        self.environment.stop()
        os.chdir(self.previous_directory)
        self.directory.cleanup()

    def write(self, name, content="Documentation\n"):
        path = Path(name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def classify(self, diff):
        with patch.object(subprocess, "check_output", return_value=diff) as command:
            result = ci_scope.documentation_only()
            command.assert_called_once_with([
                "git", "diff", "--name-status", "--no-renames", "-z", "HEAD^1", "HEAD",
            ])
            return result

    def test_markdown_additions_and_modifications(self):
        self.write("docs/guide.md")
        self.write("README.md")
        self.assertTrue(self.classify(b"A\0docs/guide.md\0M\0README.md\0"))

    def test_root_instruction_edit(self):
        self.assertTrue(self.classify(b"M\0AGENTS.md\0"))

    def test_paths_with_spaces(self):
        self.write("docs/a guide.md")
        self.assertTrue(self.classify(b"A\0docs/a guide.md\0"))

    def test_non_document_changes(self):
        for name in ("Turbo.Main/App.cs", "docs/patterns/Example.cs", "global.json",
                     ".github/workflows/quality.yml", "scripts/ci_scope.py", "other/file.md"):
            with self.subTest(name=name):
                self.assertFalse(self.classify(f"M\0{name}\0".encode()))

    def test_mixed_changes(self):
        self.assertFalse(self.classify(b"M\0README.md\0M\0Turbo.Main/App.cs\0"))

    def test_deletion(self):
        self.assertFalse(self.classify(b"D\0docs/guide.md\0"))

    def test_rename_is_delete_plus_add(self):
        self.assertFalse(self.classify(b"D\0docs/old.md\0A\0docs/new.md\0"))

    def test_type_change(self):
        self.assertFalse(self.classify(b"T\0README.md\0"))

    def test_empty_or_malformed_diff(self):
        for diff in (b"", b"M\0", b"M\0README.md\0A\0"):
            with self.subTest(diff=diff):
                self.assertFalse(self.classify(diff))

    def test_git_failure_runs_full_checks(self):
        with patch.object(subprocess, "check_output", side_effect=subprocess.CalledProcessError(1, "git")):
            self.assertFalse(ci_scope.documentation_only())

    def test_invalid_filename_encoding_runs_full_checks(self):
        self.assertFalse(self.classify(b"M\0docs/\xff.md\0"))

    def test_main_and_manual_runs_are_full(self):
        for event in ("push", "workflow_dispatch", "pull_request_target"):
            with self.subTest(event=event), patch.dict(os.environ, {"GITHUB_EVENT_NAME": event}):
                with patch.object(subprocess, "check_output") as command:
                    self.assertFalse(ci_scope.documentation_only())
                    command.assert_not_called()

    def test_missing_governance_file_fails(self):
        self.write("README.md")
        Path("AGENTS.md").unlink()
        with self.assertRaises(SystemExit):
            self.classify(b"M\0README.md\0")

    def test_empty_document_fails(self):
        self.write("README.md", " \n")
        with self.assertRaises(SystemExit):
            self.classify(b"M\0README.md\0")


if __name__ == "__main__":
    unittest.main()

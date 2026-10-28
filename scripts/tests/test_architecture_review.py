"""Boundary ratchet and semantic review failure cases, without external requests."""

import json
import contextlib
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import architecture
import jev_review


class BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.policy = json.loads(architecture.POLICY.read_text())
        self.legacy_policy = {**self.policy, "baseline": [
            {"source": "Turbo.PacketHandlers", "target": "Turbo.Database",
             "rule": "handler-persistence", "reason": "Legacy exception fixture."}
        ]}

    def test_baseline_does_not_hide_new_contract_dependency(self):
        graph = {"Turbo.PacketHandlers": ["Turbo.Database"],
                 "Turbo.Contracts": ["Turbo.Primitives", "Turbo.Database"]}
        result = architecture.check(graph, self.legacy_policy)
        self.assertEqual(result["new_violations"],
                         [("Turbo.Contracts", "Turbo.Database", "contract-implementation")])
        self.assertEqual(len(result["existing_violations"]), 1)

    def test_removed_violation_requires_removing_exception(self):
        result = architecture.check({"Turbo.PacketHandlers": []}, self.legacy_policy)
        self.assertEqual(len(result["stale_baseline"]), 1)

    def test_handler_database_reference_is_rejected_without_an_exception(self):
        result = architecture.check({"Turbo.PacketHandlers": ["Turbo.Database"]}, self.policy)
        self.assertEqual(result["new_violations"],
                         [("Turbo.PacketHandlers", "Turbo.Database", "handler-persistence")])
        self.assertEqual(result["existing_violations"], [])

    def test_windows_reference_paths_and_conditions_are_in_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, content in (
                ("Turbo.A", '<Project><ItemGroup><ProjectReference Include="..\\Turbo.B\\Turbo.B.csproj" Condition="false" /></ItemGroup></Project>'),
                ("Turbo.B", '<Project />'),
            ):
                (root / name).mkdir()
                (root / name / f"{name}.csproj").write_text(content)
            self.assertEqual(architecture.project_graph(root), {"Turbo.A": ["Turbo.B"], "Turbo.B": []})


class ReviewTests(unittest.TestCase):
    def setUp(self):
        self.evidence = {"h0_rule": {"path": "Turbo.Example/Test.cs", "line": 4, "rule": "rule"}}

    def test_advisory_threshold_and_evidence(self):
        result = jev_review.parse_answers(
            {"answers": {"h0_rule": {"type": "noul", "noul": 0.9}}}, self.evidence, 0.8)
        self.assertTrue(result[0]["investigate"])
        self.assertEqual(result[0]["line"], 4)

    def test_partial_response_is_not_success(self):
        for answers in ({}, {"other": {"type": "noul", "noul": 0.1}}):
            with self.subTest(answers=answers), self.assertRaises(ValueError):
                jev_review.parse_answers({"answers": answers}, self.evidence, 0.8)

    def test_invalid_probabilities_are_rejected(self):
        for probability in (True, None, "0.9", -0.1, 1.1, float("nan"), float("inf")):
            with self.subTest(value=probability), self.assertRaises(ValueError):
                jev_review.parse_answers({"answers": {"h0_rule": {"type": "noul", "noul": probability}}},
                                         self.evidence, 0.8)

    def test_http_errors_do_not_echo_server_body_or_credentials(self):
        error = urllib.error.HTTPError(jev_review.ENDPOINT, 401, "secret-response", {}, None)
        with patch("urllib.request.OpenerDirector.open", side_effect=error):
            with self.assertRaisesRegex(ValueError, "^Jev HTTP 401; no review completed$"):
                jev_review.submit(b"{}", "test-key")

    def test_redirects_refused(self):
        self.assertIsNone(jev_review.NoRedirect().redirect_request(None, None, 302, "", {}, "https://example.com"))

    def test_changed_hunks_include_staged_unstaged_untracked_and_deletion(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.PIPE)
            git("init")
            (root / "Turbo.Example").mkdir()
            source = root / "Turbo.Example/Test.cs"
            deleted = root / "Turbo.Example/Deleted.cs"
            source.write_text("class Original {}\n")
            deleted.write_text("class Deleted {}\n")
            git("add", ".")
            git("-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-m", "fixture")
            source.write_text("class Staged {}\n")
            git("add", ".")
            source.write_text("class WorkingTree {}\n")
            deleted.unlink()
            (root / "Turbo.Example/New.cs").write_text("class Added {}\n")
            (root / ".env").write_text("not-source")
            _, hunks, skipped = jev_review.collect_hunks(root, "HEAD", [])
            self.assertEqual({h["path"] for h in hunks},
                             {"Turbo.Example/Test.cs", "Turbo.Example/New.cs", "Turbo.Example/Deleted.cs"})
            self.assertIn(".env", skipped)
            self.assertTrue(any("+class WorkingTree" in h["diff"] for h in hunks))
            self.assertTrue(any("-class Deleted" in h["diff"] for h in hunks))
            with self.assertRaises(subprocess.CalledProcessError):
                jev_review.collect_hunks(root, "nonexistent-base", [])
            source.write_text('const string ApiKey = "obvious-test-credential";\n')
            with self.assertRaisesRegex(ValueError, "Possible credential"):
                jev_review.collect_hunks(root, "HEAD", [])

    def test_nested_sources_match_handler_rules(self):
        rules = json.loads(architecture.POLICY.read_text())["semantic_rules"]
        _, evidence = jev_review.build_request(
            [{"path": "Turbo.PacketHandlers/Users/Handler.cs", "line": 10, "diff": "+new"}], rules, "jev-latest")
        self.assertIn("h0_handler-persistence", evidence)

    def test_preview_never_submits_and_live_requires_key(self):
        hunks = [{"path": "Turbo.PacketHandlers/Test.cs", "line": 1, "diff": "+new"}]
        with patch.object(jev_review, "collect_hunks", return_value=("abc", hunks, [])), \
             patch.object(jev_review, "submit") as submit:
            with patch.object(sys, "argv", ["jev_review.py"]), contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(jev_review.main(), 0)
            self.assertEqual(json.loads(output.getvalue())["status"], "preview-only-nothing-sent")
            submit.assert_not_called()
            with patch.object(sys, "argv", ["jev_review.py", "--live"]), \
                 patch.dict(os.environ, {}, clear=True), self.assertRaisesRegex(ValueError, "Set TYPESAFE_API_KEY"):
                jev_review.main()
            submit.assert_not_called()

    def test_oversized_payload_is_rejected_before_network_call(self):
        hunks = [{"path": "Turbo.PacketHandlers/Test.cs", "line": 1, "diff": "+" + "x" * 50_000}]
        with patch.object(jev_review, "collect_hunks", return_value=("abc", hunks, [])), \
             patch.object(sys, "argv", ["jev_review.py", "--live"]), \
             patch.object(jev_review, "submit") as submit, self.assertRaisesRegex(ValueError, "exceeds 48 KB"):
            jev_review.main()
        submit.assert_not_called()

    def test_actual_key_is_not_echoed_even_in_preview(self):
        hunks = [{"path": "Turbo.PacketHandlers/Test.cs", "line": 1, "diff": '+var value = "fixture-key";'}]
        with patch.object(jev_review, "collect_hunks", return_value=("abc", hunks, [])), \
             patch.object(sys, "argv", ["jev_review.py"]), \
             patch.dict(os.environ, {"TYPESAFE_API_KEY": "fixture-key"}), \
             contextlib.redirect_stdout(io.StringIO()) as output, \
             self.assertRaisesRegex(ValueError, "Credential found"):
            jev_review.main()
        self.assertEqual(output.getvalue(), "")


if __name__ == "__main__":
    unittest.main()

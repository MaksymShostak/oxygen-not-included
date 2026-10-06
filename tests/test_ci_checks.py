"""Exercise CI policy against real Git histories and deliberately broken completions."""

import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / "tools/ci/checks.py"
spec = importlib.util.spec_from_file_location("oni_checks", SOURCE)
checks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checks)


class CiChecksTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.run_git("init", "-b", "main")
        self.run_git("config", "user.name", "CI fixture")
        self.run_git("config", "user.email", "fixture@example.invalid")
        self.run_git("config", "commit.gpgsign", "false")
        self.write("tools/ci/checks.py", SOURCE.read_text(encoding="utf-8"))
        self.write("docs/plans/old.md", "# Plan\n")
        self.write("docs/image.png", "image")
        self.write("clean.py", "print('old')\n")
        self.write("tools/oni-mod-pipeline/src/OniModPipeline/Readme/File.cs", "old\n")
        self.base = self.commit()

    def run_git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.root, stderr=subprocess.PIPE).decode().strip()

    def write(self, path, text):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")

    def commit(self):
        self.run_git("add", "--all")
        self.run_git("commit", "-m", "fixture")
        return self.run_git("rev-parse", "HEAD")

    def push_plan(self, before=None):
        revision = self.commit()
        return checks.select(self.root, "push", {"before": before or self.base, "after": revision,
                             "ref": "refs/heads/main"}, revision, "owner/oni")

    def test_document_add_edit_move_delete_and_unusual_names_are_docs_only(self):
        (self.root / "docs/plans/old.md").rename(self.root / "docs/plans/new.md")
        (self.root / "docs/image.png").unlink()
        self.write("docs/reviews/Unicode-雪 spaces.md", "# Review\n")
        plan = self.push_plan()
        self.assertEqual({key for key, value in plan["checks"].items() if value}, {"documentation"})
        self.assertEqual({record["status"] for record in plan["changes"]}, {"A", "D"})
        self.assertIn("docs/reviews/Unicode-雪 spaces.md", [record["path"] for record in plan["changes"]])

    @unittest.skipIf(os.name == "nt", "Windows cannot create newline filenames; Linux CI exercises this fixture")
    def test_newline_filename_remains_one_native_git_record(self):
        self.write("docs/reviews/new\nline.md", "# Review\n")
        plan = self.push_plan()
        self.assertEqual([item["path"] for item in plan["changes"]], ["docs/reviews/new\nline.md"])
        self.assertTrue(plan["checks"]["documentation"])

    def test_python_only_does_not_acquire_application_or_node_graphs(self):
        self.write("clean.py", "print('new')\n")
        plan = self.push_plan()
        self.assertEqual({key for key, value in plan["checks"].items() if value}, {"python", "codeql"})
        self.assertEqual(plan["languages"], ["python"])

    def test_pipeline_producer_and_embedded_backend_inputs(self):
        self.write("tools/oni-mod-pipeline/src/OniModPipeline/Readme/File.cs", "new\n")
        plan = self.push_plan()
        self.assertTrue(plan["checks"]["pipeline"])
        self.assertTrue(plan["checks"]["producer"])
        self.assertFalse(plan["checks"]["python"])
        self.assertEqual(plan["languages"], ["csharp"])
        self.write("tools/oni-mod-pipeline/src/OniModPipeline/Catalogs/Python/catalogs.py", "print('catalog')\n")
        plan = self.push_plan()
        self.assertTrue(plan["checks"]["python"])
        self.assertEqual(plan["languages"], ["csharp", "python"])

    def test_producer_source_cone_and_real_preview_content_edits(self):
        paths = [
            "tools/oni-mod-pipeline/src/OniModPipeline/Program.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/WorkshopListing/ListingTextRenderer.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/ModProfiles/ContainedPathResolver.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/Processes/ExternalProcessRunner.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/SourceControl/GitRepositoryInspector.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/Diagnostics/Diagnostic.cs",
            "tools/oni-mod-pipeline/src/OniModPipeline/EnvironmentDiscovery/GameInstallationCandidateSource.cs",
            "tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Readme/ReadmeSynchronizerTests.cs",
            "tools/oni-mod-pipeline/tests/OniModPipeline.Tests/Cli/SyncReadmeCommandTests.cs",
            "mods/delivery-temperature-limit-supercooled/Preview.png",
        ]
        for path in paths:
            self.write(path, "original\n")
        before = self.commit()
        for path in paths:
            with self.subTest(path=path):
                self.write(path, "changed\n")
                plan = self.push_plan(before)
                before = plan["revision"]
                self.assertEqual([record["status"] for record in plan["changes"]], ["M"])
                self.assertTrue(plan["checks"]["pipeline"])
                self.assertTrue(plan["checks"]["producer"])

    def test_other_authored_mod_markdown_addition_then_content_edit(self):
        path = "mods/delivery-temperature-limit-supercooled/TRANSLATION.md"
        self.write(path, "# Translation\n")
        added = self.push_plan()
        self.assertTrue(added["checks"]["documentation"])
        self.write(path, "# Translation\n\nUpdated guidance.\n")
        edited = self.push_plan(added["revision"])
        self.assertEqual([record["status"] for record in edited["changes"]], ["M"])
        self.assertTrue(edited["checks"]["documentation"])
        self.assertTrue(edited["checks"]["pipeline"])
        self.assertTrue(edited["checks"]["python"])

    def test_dependency_mixed_and_control_inputs(self):
        self.write("mods/example/Source/packages.lock.json", "{}")
        self.write("docs/review.md", "# Review\n")
        plan = self.push_plan()
        self.assertTrue(plan["checks"]["dependency"])
        self.assertTrue(plan["checks"]["documentation"])
        self.assertEqual(plan["languages"], ["csharp"])
        self.write(".github/workflows/oni-checks.yml", "control")
        plan = self.push_plan()
        self.assertTrue(all(plan["checks"].values()))
        self.assertEqual(plan["languages"], list(checks.LANGUAGES))

    def test_actions_source_selects_only_its_analysis_and_link_existence(self):
        self.write(".github/actions/helper/action.yml", "name: Helper\n")
        plan = self.push_plan()
        self.assertEqual(plan["languages"], ["actions"])
        self.assertEqual({key for key, value in plan["checks"].items() if value}, {"documentation", "codeql"})

    def test_unknown_input_and_missing_base_select_full(self):
        self.write("new-engine/source.unknown", "data")
        plan = self.push_plan()
        self.assertEqual(plan["fallback"], "unknown-input")
        self.assertTrue(all(plan["checks"].values()))
        revision = plan["revision"]
        plan = checks.select(self.root, "push", {"before": "1" * 40, "after": revision,
                             "ref": "refs/heads/main"}, revision, "owner/oni")
        self.assertEqual(plan["fallback"], "comparison-unavailable")
        self.assertTrue(all(plan["checks"].values()))

    def test_large_document_diff_exceeds_hosted_changed_file_cap(self):
        for number in range(350):
            self.write(f"docs/reviews/review-{number}.md", "# Review\n")
        plan = self.push_plan()
        self.assertEqual(len(plan["changes"]), 350)
        self.assertEqual({key for key, value in plan["checks"].items() if value}, {"documentation"})

    def test_move_across_docs_boundary_selects_the_product_consumer(self):
        target = self.root / "mods/delivery-temperature-limit-supercooled/README.md"
        target.parent.mkdir(parents=True)
        (self.root / "docs/plans/old.md").rename(target)
        plan = self.push_plan()
        self.assertEqual({record["status"] for record in plan["changes"]}, {"A", "D"})
        self.assertTrue(plan["checks"]["documentation"])
        self.assertTrue(plan["checks"]["pipeline"])
        self.assertTrue(plan["checks"]["producer"])

    def test_new_main_history_selects_full_and_unsupported_event_fails(self):
        plan = checks.select(self.root, "push", {"before": "0" * 40, "after": self.base,
                             "ref": "refs/heads/main"}, self.base, "owner/oni")
        self.assertEqual(plan["fallback"], "new-main-history")
        self.assertTrue(all(plan["checks"].values()))
        with self.assertRaises(checks.CheckError):
            checks.select(self.root, "pull_request_target", {}, self.base, "owner/oni")

    def test_docs_symlink_tree_entry_is_rejected_without_following_its_target(self):
        self.write("link-target.txt", "plans/old.md")
        blob = self.run_git("hash-object", "-w", "--", "link-target.txt")
        self.run_git("update-index", "--add", "--cacheinfo", f"120000,{blob},docs/link.md")
        self.run_git("commit", "-m", "symlink tree fixture")
        revision = self.run_git("rev-parse", "HEAD")
        with self.assertRaises(checks.CheckError):
            checks.select(self.root, "push", {"before": self.base, "after": revision,
                          "ref": "refs/heads/main"}, revision, "owner/oni")

    def test_native_cli_refuses_malformed_and_oversized_events_before_output(self):
        event_path = self.root / "event.json"
        output_path = self.root / "output.txt"
        environment = {**os.environ, "GITHUB_EVENT_PATH": str(event_path), "GITHUB_OUTPUT": str(output_path),
                       "GITHUB_SHA": self.base, "GITHUB_EVENT_NAME": "push", "GITHUB_REPOSITORY": "owner/oni"}
        for payload in ("{", "[]", " " * (1024 * 1024 + 1)):
            with self.subTest(payload_size=len(payload)):
                event_path.write_text(payload, encoding="utf-8")
                result = subprocess.run([sys.executable, str(SOURCE), "select"], cwd=self.root, env=environment,
                                        capture_output=True, timeout=30)
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse(output_path.exists())

    def test_native_worker_completion_refuses_missing_or_unsuccessful_selected_steps(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        event_path = self.root / "event.json"
        event_path.write_text("{}", encoding="utf-8")
        output_path = self.root / "output.txt"
        environment = {**os.environ, "GITHUB_EVENT_PATH": str(event_path), "GITHUB_OUTPUT": str(output_path),
                       "GITHUB_SHA": self.base, "GITHUB_REF": "refs/heads/main", "GITHUB_EVENT_NAME": "workflow_dispatch",
                       "GITHUB_REPOSITORY": "owner/oni", "ONI_PLAN": json.dumps(plan), "ONI_CONSUMER": "worker"}
        for step in ("pipeline", "python", "producer"):
            for status in ("skipped", "failure", "cancelled", None):
                statuses = {key: "success" for key in ("pipeline", "python", "producer")}
                if status is None:
                    del statuses[step]
                else:
                    statuses[step] = status
                with self.subTest(step=step, status=status):
                    result = subprocess.run([sys.executable, str(SOURCE), "complete"], cwd=self.root,
                                            env={**environment, "ONI_STEPS": json.dumps(statuses)},
                                            capture_output=True, timeout=30)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse(output_path.exists())
    def test_output_limit_never_returns_partial_green_selection(self):
        self.write("docs/new.md", "# New\n")
        revision = self.commit()
        real_git = checks.git
        def bounded(root, *args):
            if args[0] == "diff":
                raise checks.CheckError("Git observation exceeded 4 MiB")
            return real_git(root, *args)
        with patch.object(checks, "git", side_effect=bounded):
            plan = checks.select(self.root, "push", {"before": self.base, "after": revision,
                                 "ref": "refs/heads/main"}, revision, "owner/oni")
        self.assertTrue(all(plan["checks"].values()))
        expected = checks.select(self.root, "push", {"before": self.base, "after": revision,
                                 "ref": "refs/heads/main"}, revision, "owner/oni")
        checks.validate(plan, expected)
        plan["checks"]["dependency"] = False
        with self.assertRaises(checks.CheckError):
            checks.validate(plan, expected)

    def test_unsupported_docs_executable_is_rejected_as_data_boundary(self):
        self.write("docs/run.py", "print('never execute')")
        with self.assertRaises(checks.CheckError):
            self.push_plan()

    def test_mode_change_inside_docs_is_rejected(self):
        self.run_git("update-index", "--chmod=+x", "docs/plans/old.md")
        self.run_git("commit", "-m", "mode")
        revision = self.run_git("rev-parse", "HEAD")
        with self.assertRaises(checks.CheckError):
            checks.select(self.root, "push", {"before": self.base, "after": revision,
                          "ref": "refs/heads/main"}, revision, "owner/oni")

    def test_checkout_event_and_merge_identity_are_required(self):
        for event, payload, revision in [
            ("push", {"ref": "refs/heads/other", "before": self.base, "after": self.base}, self.base),
            ("push", {"ref": "refs/heads/main", "before": self.base, "after": self.base}, "2" * 40),
            ("pull_request", {"pull_request": {"base": {"sha": self.base}, "head": {"sha": self.base}}}, self.base),
        ]:
            with self.subTest(event=event, revision=revision), self.assertRaises(checks.CheckError):
                checks.select(self.root, event, payload, revision, "owner/oni")

    def test_actual_merge_revision_and_fork_eligibility(self):
        self.run_git("switch", "-c", "fixture-feature")
        self.write("clean.py", "print('feature')\n")
        head = self.commit()
        self.run_git("switch", "main")
        self.run_git("merge", "--no-ff", "fixture-feature", "-m", "merge fixture")
        revision = self.run_git("rev-parse", "HEAD")
        payload = {"pull_request": {"base": {"sha": self.base, "ref": "main"},
                   "head": {"sha": head, "repo": {"full_name": "fork/oni"}}}}
        plan = checks.select(self.root, "pull_request", payload, revision, "owner/oni")
        self.assertTrue(plan["checks"]["python"])
        self.assertFalse(plan["checks"]["codeql"])
        self.assertFalse(plan["checks"]["dependency"])
        payload["pull_request"]["head"]["repo"]["full_name"] = "owner/oni"
        self.assertEqual(checks.select(self.root, "pull_request", payload, revision, "owner/oni")["languages"], ["python"])

    def test_schedule_and_manual_full_main_only(self):
        for event in ("schedule", "workflow_dispatch"):
            with patch.dict(os.environ, {"GITHUB_REF": "refs/heads/main"}):
                self.assertTrue(all(checks.select(self.root, event, {}, self.base, "owner/oni")["checks"].values()))
            with patch.dict(os.environ, {"GITHUB_REF": "refs/heads/other"}), self.assertRaises(checks.CheckError):
                checks.select(self.root, event, {}, self.base, "owner/oni")

    def test_gate_rejects_every_missing_failed_skipped_or_stale_selected_consumer(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        results = {job: "success" for job in ("worker", "documentation", "codeql", "dependency")}
        completions = {job: self.base for job in results}
        checks.evaluate(plan, results, completions)
        for job in results:
            for status in ("skipped", "failure", "cancelled", "", None):
                with self.subTest(job=job, status=status), self.assertRaises(checks.CheckError):
                    checks.evaluate(plan, {**results, job: status}, completions)
            for identity in ("", None, "f" * 40):
                with self.subTest(job=job, identity=identity), self.assertRaises(checks.CheckError):
                    checks.evaluate(plan, results, {**completions, job: identity})
        with self.assertRaises(checks.CheckError):
            checks.evaluate(plan, {}, completions)

    def test_docs_only_gate_allows_explained_unselected_skips(self):
        self.write("docs/plans/old.md", "# Edited\n")
        plan = self.push_plan()
        checks.evaluate(plan, {"worker": "skipped", "documentation": "success", "codeql": "skipped", "dependency": "skipped"},
                        {"worker": "", "documentation": plan["revision"], "codeql": "", "dependency": ""})
        altered = copy.deepcopy(plan)
        altered["checks"]["documentation"] = False
        with self.assertRaises(checks.CheckError):
            checks.validate(altered, plan)
        altered = copy.deepcopy(plan)
        altered["checks"]["documentation"] = 1
        with self.assertRaises(checks.CheckError):
            checks.validate(altered, plan)

    def test_selected_matrix_language_requires_same_attempt_completion(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        receipts = self.root / "receipts"
        receipts.mkdir()
        for language in plan["languages"]:
            (receipts / f"{language}.json").write_text(json.dumps({
                "language": language, "revision": self.base, "policy_sha256": plan["policy_sha256"],
                "run_id": "123", "attempt": "2",
            }), encoding="utf-8")
        checks.codeql_receipts(plan, receipts, "123", "2")
        with self.assertRaises(checks.CheckError):
            checks.codeql_receipts(plan, receipts, "123", "3")
        (receipts / "python.json").unlink()
        with self.assertRaises(checks.CheckError):
            checks.codeql_receipts(plan, receipts, "123", "2")

    def test_dependency_old_sha_survives_docs_but_not_new_dependency_state(self):
        remote = self.root.parent / (self.root.name + "-remote.git")
        self.addCleanup(lambda: __import__("shutil").rmtree(remote, ignore_errors=True))
        subprocess.check_call(["git", "init", "--bare", str(remote)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.run_git("remote", "add", "origin", str(remote))
        self.write("mods/example/Source/packages.lock.json", "{}")
        plan = self.push_plan()
        self.write("docs/later.md", "# Later\n")
        docs_tip = self.commit()
        self.run_git("push", "origin", "main")
        self.assertEqual(checks.dependency_ready(self.root, plan), docs_tip)
        self.write("mods/example/Source/packages.lock.json", '{"updated": true}')
        self.commit()
        self.run_git("push", "origin", "main")
        with self.assertRaises(checks.CheckError):
            checks.dependency_ready(self.root, plan)


if __name__ == "__main__":
    unittest.main()

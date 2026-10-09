"""Exercise CI policy against real Git histories and deliberately broken completions."""

import copy
import contextlib
import html
import importlib.util
import io
import itertools
import json
import os
from pathlib import Path
import subprocess
import re
import shlex
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

    def test_shared_markdown_controls_select_qualification_and_readme_consumers(self):
        for path in (".markdown-quality-execution.json", ".node-version", ".python-version"):
            with self.subTest(path=path):
                self.write(path, "updated\n")
                plan = self.push_plan()
                self.assertTrue(plan["checks"]["documentation"])
                self.assertTrue(plan["checks"]["producer"])
                self.assertTrue(plan["checks"]["pipeline"])
                self.assertTrue(plan["checks"]["codeql"])

    def test_python_runtime_also_selects_python_unit_tests(self):
        self.write(".python-version", "3.14.8\n")
        self.assertTrue(self.push_plan()["checks"]["python"])

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

    def codeql_environment(self, plan):
        """Bind real event recomputation to independently authored job evidence."""
        event_path = self.root / "event.json"
        event_path.write_text("{}", encoding="utf-8")
        environment = {**os.environ, "GITHUB_EVENT_PATH": str(event_path),
                       "GITHUB_OUTPUT": str(self.root / "output.txt"),
                       "GITHUB_STEP_SUMMARY": str(self.root / "summary.md"),
                       "GITHUB_SHA": plan["revision"], "GITHUB_REF": "refs/heads/main",
                       "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_REPOSITORY": "owner/oni",
                       "GITHUB_RUN_ID": "123", "GITHUB_RUN_ATTEMPT": "2",
                       "ONI_PLAN": json.dumps(plan), "ONI_VALIDATION_RESULT": "success"}
        for language in ("actions", "csharp", "python"):
            environment[f"ONI_{language.upper()}_RESULT"] = "success"
            environment[f"ONI_{language.upper()}_RECEIPT"] = json.dumps({
                "language": language, "revision": plan["revision"],
                "policy_sha256": plan["policy_sha256"], "run_id": "123", "attempt": "2",
            }, separators=(",", ":"))
        return environment

    def run_codeql(self, operation, environment):
        return subprocess.run([sys.executable, str(SOURCE), operation], cwd=self.root,
                              env=environment, capture_output=True, text=True, timeout=30)

    def test_native_receipt_is_a_single_line_output_after_successful_analysis(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        environment = self.codeql_environment(plan)
        environment.update(ONI_LANGUAGE="python", ONI_ANALYSIS_RESULT="success")
        result = self.run_codeql("codeql-receipt", environment)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        expected = environment["ONI_PYTHON_RECEIPT"]
        self.assertEqual((self.root / "output.txt").read_bytes(), f"receipt={expected}\n".encode())
        self.assertFalse((self.root / "receipts").exists())

    def test_native_completion_rejects_a_previous_attempt_and_preserves_siblings(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        environment = self.codeql_environment(plan)
        stale = json.loads(environment["ONI_CSHARP_RECEIPT"])
        stale["attempt"] = "1"
        environment["ONI_CSHARP_RECEIPT"] = json.dumps(stale)
        result = self.run_codeql("codeql-complete", environment)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.root / "output.txt").exists())
        for language in ("actions", "csharp", "python"):
            self.assertIn(language, result.stdout)
        self.assertIn('"attempt": "1"', result.stdout)
        self.assertIn("rerun the complete selected CodeQL set", result.stdout)

    def test_native_completion_emits_only_the_qualified_source_revision(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        result = self.run_codeql("codeql-complete", self.codeql_environment(plan))
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.root / "output.txt").read_bytes(), f"completion_sha={self.base}\n".encode())

    def test_real_python_only_selection_rejects_an_unselected_child_before_analysis(self):
        self.write("clean.py", "print('python-only change')\n")
        plan = self.push_plan()
        self.assertEqual(plan["languages"], ["python"])
        environment = self.codeql_environment(plan)
        environment["GITHUB_EVENT_NAME"] = "push"
        (self.root / "event.json").write_text(json.dumps({"before": self.base, "after": plan["revision"],
            "ref": "refs/heads/main"}), encoding="utf-8")
        child = json.loads((SOURCE.parents[2] / ".github/workflows/codeql-language.yml").read_text())
        validation_commands = [shlex.split(command) for command in child["jobs"]["analyze"]["steps"][1]["run"].splitlines()]
        for language in ("actions", "csharp", "python"):
            environment["ONI_LANGUAGE"] = language
            return_codes = []
            for command in validation_commands:
                result = subprocess.run([sys.executable, *command[1:]], cwd=self.root,
                                        env=environment, capture_output=True, timeout=30)
                return_codes.append(result.returncode)
                if result.returncode:
                    break
            self.assertEqual(return_codes[0], 0)  # Authentic plan reaches the language guard.
            self.assertEqual(return_codes[-1] == 0, language == "python")

    def test_native_receipt_rejects_failure_unknown_language_and_output_injection(self):
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        environment = self.codeql_environment(plan)
        environment.update(ONI_LANGUAGE="python", ONI_ANALYSIS_RESULT="success")
        for key, value in ([("ONI_ANALYSIS_RESULT", status) for status in ("failure", "cancelled", "skipped", "")]
                + [("ONI_LANGUAGE", "javascript"), ("GITHUB_RUN_ID", "123\nforged=success"),
                   ("GITHUB_RUN_ATTEMPT", "0"), ("GITHUB_RUN_ID", "1" * 4096)]):
            with self.subTest(key=key, value=value[:40]):
                result = self.run_codeql("codeql-receipt", {**environment, key: value})
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse((self.root / "output.txt").exists())

    def test_actual_child_validation_rejects_unselected_unknown_and_ineligible_calls(self):
        child = json.loads((SOURCE.parents[2] / ".github/workflows/codeql-language.yml").read_text())
        validation_script = child["jobs"]["analyze"]["steps"][1]["run"]
        plan = checks.select(self.root, "workflow_dispatch", {}, self.base, "owner/oni")
        environment = self.codeql_environment(plan)
        for language, modified_plan, should_succeed in [
            ("python", plan, True), ("javascript", plan, False),
            ("python", {**plan, "languages": ["actions"]}, False),
            ("python", {**plan, "eligibility": {**plan["eligibility"], "codeql": False}}, False),
            ("python", {**plan, "revision": "f" * 40}, False),
        ]:
            with self.subTest(language=language, plan=modified_plan):
                environment.update(ONI_LANGUAGE=language, ONI_PLAN=json.dumps(modified_plan))
                results = []
                # Run the real authored shell commands with the repository interpreter.
                for command in validation_script.splitlines():
                    arguments = shlex.split(command)
                    self.assertEqual(arguments[0], "python3")
                    results.append(subprocess.run([sys.executable, *arguments[1:]], cwd=self.root,
                                                  env=environment, capture_output=True, timeout=30))
                    if results[-1].returncode:
                        break
                self.assertEqual(all(result.returncode == 0 for result in results), should_succeed)

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


class CodeqlWorkflowContractTests(unittest.TestCase):
    """Mutate the actual authored workflows and exercise their completion wiring.

    JSON flow syntax is valid YAML. The native JSON parser lets these contracts
    run in the existing standard-library-only control job without a YAML shim.
    These checks cannot establish GitHub's hosted expression/output semantics.
    """

    def setUp(self):
        workflows = SOURCE.parents[2] / ".github/workflows"
        self.parent = json.loads((workflows / "codeql.yml").read_text(encoding="utf-8"))
        self.child = json.loads((workflows / "codeql-language.yml").read_text(encoding="utf-8"))

    def assert_workflow_contract(self, parent, child):
        """Assert accepted topology/trust invariants, rather than a whole-file snapshot."""
        calls = ["analyze-actions", "analyze-csharp", "analyze-python"]
        self.assertEqual(set(parent["jobs"]), {"validate", "complete", *calls})
        self.assertEqual(set(parent["on"]), {"workflow_call"})
        self.assertEqual(set(child["on"]), {"workflow_call"})
        self.assertEqual(parent["permissions"], {})
        self.assertEqual(child["permissions"], {})
        self.assertEqual(parent["on"]["workflow_call"]["outputs"]["completion_sha"]["value"],
                         "${{ jobs.complete.outputs.completion_sha }}")
        self.assertEqual(parent["on"]["workflow_call"]["inputs"], {"plan": {"required": True, "type": "string"}})
        self.assertEqual(child["on"]["workflow_call"]["inputs"],
                         {"plan": {"required": True, "type": "string"},
                          "language": {"required": True, "type": "string"}})
        validation = parent["jobs"]["validate"]
        self.assertEqual(validation["permissions"], {"contents": "read"})
        self.assertEqual(validation["outputs"]["languages"], "${{ steps.scope.outputs.languages }}")
        self.assertIn("python3 tools/ci/checks.py validate", validation["steps"][1]["run"])
        self.assertIn('p["checks"]["codeql"] and p["eligibility"]["codeql"] and p["languages"]',
                      validation["steps"][1]["run"])
        for language in ("actions", "csharp", "python"):
            call = parent["jobs"][f"analyze-{language}"]
            self.assertEqual(call["needs"], "validate")
            self.assertEqual(call["if"], f"contains(fromJSON(needs.validate.outputs.languages), '{language}')")
            self.assertEqual(call["uses"], "./.github/workflows/codeql-language.yml")
            self.assertEqual(call["with"], {"plan": "${{ inputs.plan }}", "language": language})
            self.assertEqual(call["permissions"], {"contents": "read", "security-events": "write"})
            self.assertFalse({"strategy", "secrets", "steps", "continue-on-error"} & set(call))
        completion = parent["jobs"]["complete"]
        self.assertEqual(completion["if"], "always()")
        self.assertEqual(completion["needs"], ["validate", *calls])
        self.assertEqual(completion["outputs"], {"completion_sha": "${{ steps.complete.outputs.completion_sha }}"})
        self.assertEqual(completion["permissions"], {"contents": "read"})
        expected_environment = {"ONI_PLAN": "${{ inputs.plan }}", "ONI_VALIDATION_RESULT": "${{ needs.validate.result }}"}
        for language in ("actions", "csharp", "python"):
            expected_environment[f"ONI_{language.upper()}_RESULT"] = "${{ needs.analyze-" + language + ".result }}"
            expected_environment[f"ONI_{language.upper()}_RECEIPT"] = "${{ needs.analyze-" + language + ".outputs.receipt }}"
        self.assertEqual(completion["env"], expected_environment)
        self.assertEqual(completion["steps"][1]["run"], "python3 tools/ci/checks.py codeql-complete")
        self.assertEqual(set(child["jobs"]), {"analyze"})
        analysis = child["jobs"]["analyze"]
        self.assertEqual(analysis["runs-on"], "ubuntu-24.04")
        self.assertEqual(analysis["timeout-minutes"], 20)
        self.assertEqual(analysis["permissions"], {"contents": "read", "security-events": "write"})
        self.assertEqual(analysis["env"], {"ONI_PLAN": "${{ inputs.plan }}", "ONI_LANGUAGE": "${{ inputs.language }}"})
        self.assertEqual(analysis["outputs"], {"receipt": "${{ steps.receipt.outputs.receipt }}"})
        self.assertEqual(child["on"]["workflow_call"]["outputs"]["receipt"]["value"], "${{ jobs.analyze.outputs.receipt }}")
        self.assertFalse({"strategy", "continue-on-error", "if"} & set(analysis))
        steps = analysis["steps"]
        self.assertEqual(len(steps), 5)
        self.assertEqual(steps[0]["uses"], "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1")
        self.assertIn("python3 tools/ci/checks.py validate", steps[1]["run"])
        self.assertIn('language in ("actions","csharp","python")', steps[1]["run"])
        self.assertIn('language in plan["languages"]', steps[1]["run"])
        self.assertIn('plan["checks"]["codeql"] and plan["eligibility"]["codeql"]', steps[1]["run"])
        self.assertEqual(steps[2]["uses"], "github/codeql-action/init@1c5b675653bb5c22dbe9b12b556ec555138e09fd")
        self.assertEqual(steps[2]["with"], {"languages": "${{ inputs.language }}", "build-mode": "none", "tools": "linked"})
        self.assertEqual(steps[3]["id"], "analysis")
        self.assertEqual(steps[3]["uses"], "github/codeql-action/analyze@1c5b675653bb5c22dbe9b12b556ec555138e09fd")
        self.assertEqual(steps[3]["with"], {"category": "/language:${{ inputs.language }}"})
        self.assertEqual(steps[4]["id"], "receipt")
        self.assertEqual(steps[4]["env"], {"ONI_ANALYSIS_RESULT": "${{ steps.analysis.outcome }}"})
        self.assertEqual(steps[4]["run"], "python3 tools/ci/checks.py codeql-receipt")
        for workflow in (parent, child):
            serialized = json.dumps(workflow)
            self.assertNotIn("upload-artifact", serialized)
            self.assertNotIn("download-artifact", serialized)
            self.assertNotIn("ONI_RECEIPTS", serialized)
            for job in workflow["jobs"].values():
                for step in job.get("steps", []):
                    self.assertNotIn("continue-on-error", step)
                    self.assertNotIn("${{", step.get("run", ""))
                    if step.get("uses", "").startswith("actions/checkout@"):
                        self.assertEqual(step["uses"], "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1")
                        self.assertEqual(step["with"], {"fetch-depth": 0, "persist-credentials": False})

    def test_actual_workflow_contract_and_independent_output_wiring(self):
        self.assert_workflow_contract(self.parent, self.child)
        for selected in (("python",), ("csharp", "python"), ("actions", "csharp", "python")):
            plan = {"revision": "a" * 40, "policy_sha256": "b" * 64, "languages": list(selected),
                    "checks": {"codeql": True}, "eligibility": {"codeql": True}}
            job_evidence = {"validate": {"result": "success"}}
            for language in ("actions", "csharp", "python"):
                call = self.parent["jobs"][f"analyze-{language}"]
                selected_language = re.fullmatch(r"contains\(fromJSON\(needs.validate.outputs.languages\), '([^']+)'\)",
                                                 call["if"]).group(1)
                is_selected = selected_language in selected
                receipt = json.dumps({"language": call["with"]["language"], "revision": "a" * 40,
                                      "policy_sha256": "b" * 64, "run_id": "123", "attempt": "2"}) if is_selected else ""
                job_evidence[f"analyze-{language}"] = {"result": "success" if is_selected else "skipped", "receipt": receipt}
            environment = {}
            for name, expression in self.parent["jobs"]["complete"]["env"].items():
                if name == "ONI_PLAN":
                    continue
                binding = re.fullmatch(r"\$\{\{ needs\.([\w-]+)\.(?:outputs\.)?(result|receipt) \}\}", expression)
                environment[name] = job_evidence[binding.group(1)][binding.group(2)]
            results = {language: environment[f"ONI_{language.upper()}_RESULT"] for language in ("actions", "csharp", "python")}
            receipts = {language: environment[f"ONI_{language.upper()}_RECEIPT"] for language in ("actions", "csharp", "python")}
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(checks.complete_codeql_analysis(plan, results, receipts, "123", "2",
                                                                environment["ONI_VALIDATION_RESULT"]), "a" * 40)

    def test_workflow_mutations_detect_omitted_bindings_weakened_trust_and_archives(self):
        mutations = [
            ("parent", ("jobs", "complete", "needs"), ["validate", "analyze-python"]),
            ("parent", ("jobs", "complete", "if"), "success()"),
            ("parent", ("jobs", "complete", "env", "ONI_VALIDATION_RESULT"), "success"),
            ("parent", ("jobs", "validate", "steps", 1, "run"), "true"),
            ("parent", ("jobs", "complete", "steps", 1, "run"), "python3 tools/ci/checks.py complete"),
            ("child", ("jobs", "analyze", "strategy"), {"matrix": {"language": ["actions", "csharp", "python"]}}),
            ("child", ("jobs", "analyze", "if"), "false"),
            ("child", ("jobs", "analyze", "steps", 0, "uses"), "unexpected/replacement@unknown"),
            ("child", ("jobs", "analyze", "steps", 1, "run"), "python3 tools/ci/checks.py validate"),
            ("child", ("jobs", "analyze", "steps", 2, "with", "build-mode"), "autobuild"),
            ("child", ("jobs", "analyze", "steps", 2, "with", "tools"), "latest"),
            ("child", ("jobs", "analyze", "steps", 3, "with", "category"), "shared"),
            ("child", ("jobs", "analyze", "steps", 4, "env", "ONI_ANALYSIS_RESULT"), "success"),
            ("child", ("jobs", "analyze", "steps", 4, "uses"), "actions/upload-artifact@unexpected"),
            ("child", ("jobs", "analyze", "steps", 0, "with", "persist-credentials"), True),
            ("child", ("jobs", "analyze", "permissions", "security-events"), "read"),
        ]
        for language in ("actions", "csharp", "python"):
            mutations.extend([
                ("parent", ("jobs", f"analyze-{language}", "if"), "always()"),
                ("parent", ("jobs", f"analyze-{language}", "with", "language"), "unknown"),
                ("parent", ("jobs", f"analyze-{language}", "permissions", "security-events"), "read"),
                ("parent", ("jobs", "complete", "env", f"ONI_{language.upper()}_RESULT"), "success"),
                ("parent", ("jobs", "complete", "env", f"ONI_{language.upper()}_RECEIPT"), "${{ needs.validate.outputs.receipt }}"),
            ])
        for target, path, replacement in mutations:
            workflows = {"parent": copy.deepcopy(self.parent), "child": copy.deepcopy(self.child)}
            container = workflows[target]
            for key in path[:-1]:
                container = container[key]
            container[path[-1]] = replacement
            with self.subTest(target=target, path=path), self.assertRaises(AssertionError):
                self.assert_workflow_contract(workflows["parent"], workflows["child"])


class CodeqlCompletionContractTests(unittest.TestCase):
    """Independent wire identities exercise the full fixed-slot admission boundary."""

    def setUp(self):
        self.plan = {"revision": "a" * 40, "policy_sha256": "b" * 64,
                     "languages": ["actions", "csharp", "python"],
                     "checks": {"codeql": True}, "eligibility": {"codeql": True}}
        self.results = {language: "success" for language in ("actions", "csharp", "python")}
        self.receipts = {language: json.dumps({"language": language, "revision": "a" * 40,
            "policy_sha256": "b" * 64, "run_id": "123", "attempt": "2"})
            for language in ("actions", "csharp", "python")}
        self.output = io.StringIO()

    def complete(self, plan=None, results=None, receipts=None, validation_result="success"):
        with contextlib.redirect_stdout(self.output):
            return checks.complete_codeql_analysis(plan or self.plan, self.results if results is None else results,
                self.receipts if receipts is None else receipts, "123", "2", validation_result)

    def test_all_nonempty_language_subsets_and_receipt_arrival_orders(self):
        for count in (1, 2, 3):
            for selected in itertools.combinations(("actions", "csharp", "python"), count):
                for order in itertools.permutations(selected):
                    plan = {**self.plan, "languages": list(selected)}
                    results = {language: "success" if language in selected else "skipped" for language in self.results}
                    receipts = {language: self.receipts[language] for language in order}
                    receipts.update({language: "" for language in self.receipts if language not in selected})
                    with self.subTest(selected=selected, order=order):
                        self.assertEqual(self.complete(plan, results, receipts), "a" * 40)

    def test_every_non_successful_selected_result_and_missing_record_is_reported(self):
        for language in ("actions", "csharp", "python"):
            for result in ("failure", "cancelled", "skipped", "", "unknown"):
                self.output = io.StringIO()
                with self.subTest(language=language, result=result), self.assertRaises(checks.CheckError):
                    self.complete(results={**self.results, language: result}, receipts={**self.receipts, language: ""})
                report = self.output.getvalue()
                self.assertIn("missing completion receipt", report)
                self.assertIn("selected analysis did not succeed", report)
                for sibling in ("actions", "csharp", "python"):
                    self.assertIn(f"{sibling}: selected=true", report)
        with self.assertRaises(checks.CheckError):
            self.complete(receipts={**self.receipts, "python": ""})

    def test_wrong_identity_types_keys_duplicates_and_json_values_fail(self):
        invalid_records = ["{", "[]", "null", '"receipt"', "1", "{}", " " * 4097,
                           "雪" * 1366, self.receipts["csharp"] + "\n", '{"language":"csharp","language":"csharp"}']
        original = json.loads(self.receipts["csharp"])
        for field in ("language", "revision", "policy_sha256", "run_id", "attempt"):
            for value in (None, 123, True, [], {}, "wrong", ""):
                invalid_records.append(json.dumps({**original, field: value}))
            invalid_records.append(json.dumps({key: value for key, value in original.items() if key != field}))
            invalid_records.append(self.receipts["csharp"][:-1] + f',"{field}":{json.dumps(original[field])}' + "}")
        invalid_records.extend([json.dumps({**original, "unexpected": "field"}),
                                json.dumps({**original, "language": "python"}),
                                json.dumps({**original, "attempt": "1"}),
                                json.dumps({**original, "run_id": "124"}),
                                json.dumps({**original, "revision": "c" * 40}),
                                json.dumps({**original, "policy_sha256": "d" * 64})])
        for record in invalid_records:
            with self.subTest(record=record[:100]), self.assertRaises(checks.CheckError):
                self.complete(receipts={**self.receipts, "csharp": record})

    def test_deeply_nested_receipt_reports_valid_siblings_before_rejection(self):
        nested_receipt = "[" * 1500 + "0" + "]" * 1500
        self.assertLess(len(nested_receipt.encode("utf-8")), checks.MAX_CODEQL_RECEIPT_BYTES)
        decode_json = json.loads

        def reject_deep_nesting(value, **options):
            # Hosted Python versions can hit a parser recursion limit here.
            if value == nested_receipt:
                raise RecursionError("maximum recursion depth exceeded while decoding JSON")
            return decode_json(value, **options)

        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "summary.md"
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary_path)}), \
                    patch.object(checks.json, "loads", side_effect=reject_deep_nesting):
                with self.assertRaisesRegex(checks.CheckError, "rerun the complete selected CodeQL set"):
                    self.complete(receipts={**self.receipts, "csharp": nested_receipt})
            report = self.output.getvalue()
            summary = summary_path.read_text(encoding="utf-8")
        for sibling in ("actions", "python"):
            self.assertIn(self.receipts[sibling], report)
            self.assertIn(html.escape(self.receipts[sibling]), summary)
        self.assertIn(nested_receipt, report)
        self.assertIn("invalid=", summary)
        token = report.splitlines()[0].removeprefix("::stop-commands::")
        self.assertEqual(report.splitlines()[-1], f"::{token}::")

    def test_unselected_slots_cannot_return_results_or_receipts(self):
        plan = {**self.plan, "languages": ["python"]}
        results = {"actions": "skipped", "csharp": "skipped", "python": "success"}
        receipts = {"actions": "", "csharp": "", "python": self.receipts["python"]}
        self.assertEqual(self.complete(plan, results, receipts), "a" * 40)
        for language in ("actions", "csharp"):
            for result in ("success", "failure", "cancelled", ""):
                with self.subTest(language=language, result=result), self.assertRaises(checks.CheckError):
                    self.complete(plan, {**results, language: result}, receipts)
            with self.assertRaises(checks.CheckError):
                self.complete(plan, results, {**receipts, language: self.receipts[language]})

    def test_extra_missing_slots_and_unsuccessful_validator_fail(self):
        for results, receipts in [({}, self.receipts), (self.results, {}),
                                  ({**self.results, "javascript": "success"}, self.receipts),
                                  (self.results, {**self.receipts, "javascript": "{}"})]:
            with self.subTest(results=results, receipts=receipts), self.assertRaises(checks.CheckError):
                self.complete(results=results, receipts=receipts)
        for status in ("failure", "skipped", "cancelled", ""):
            with self.subTest(status=status), self.assertRaises(checks.CheckError):
                self.complete(validation_result=status)

    def test_receipt_producer_rejects_untrusted_selection_and_invalid_identity_formats(self):
        for field, value in [("revision", "A" * 40), ("revision", "a" * 39),
                             ("policy_sha256", "B" * 64), ("policy_sha256", "b" * 63),
                             ("eligibility", {"codeql": False}), ("checks", {"codeql": False}),
                             ("languages", ["python"])]:
            with self.subTest(field=field, value=value), self.assertRaises(checks.CheckError):
                checks.create_codeql_receipt({**self.plan, field: value}, "actions", "success", "123", "2")
        for run_id, attempt in [("", "2"), ("0", "2"), ("123", "0"), ("01", "2"),
                                ("１２３", "2"), ("123", "2\nforged=success")]:
            with self.subTest(run_id=run_id, attempt=attempt), self.assertRaises(checks.CheckError):
                checks.create_codeql_receipt(self.plan, "python", "success", run_id, attempt)

    def test_hostile_middle_receipt_is_protected_and_does_not_hide_siblings(self):
        hostile = "::error::forged\n</pre><script>alert(1)</script> `$()` ' quote &"
        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "summary.md"
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary_path)}):
                with self.assertRaises(checks.CheckError):
                    self.complete(receipts={**self.receipts, "csharp": hostile})
            summary = summary_path.read_text(encoding="utf-8")
            self.assertIn("&lt;script&gt;", summary)
            self.assertNotIn("<script>", summary)
            self.assertLessEqual(len(summary.encode("utf-8")), 16384)
            report = self.output.getvalue()
            token = re.search(r"^::stop-commands::([0-9a-f]{64})$", report, re.MULTILINE).group(1)
            self.assertLess(report.index(f"::stop-commands::{token}"), report.index(hostile))
            self.assertGreater(report.index(f"::{token}::"), report.index(hostile))
            self.assertIn(self.receipts["actions"], report)
            self.assertIn(self.receipts["python"], report)

    def test_oversized_raw_evidence_and_escaped_summaries_are_explicitly_bounded(self):
        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "summary.md"
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary_path)}):
                with self.assertRaises(checks.CheckError):
                    self.complete(receipts={language: "<" * 10000 for language in self.receipts})
            self.assertLessEqual(summary_path.stat().st_size, 16384)
            self.assertLess(len(self.output.getvalue().encode()), 16384)
            self.assertIn("exceeds 4 KiB", self.output.getvalue())
            self.assertIn("diagnostic excerpt", self.output.getvalue())

    def test_summary_failure_preserves_rejection_and_resumes_workflow_commands(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": directory}):
                with self.assertRaises(checks.CheckError) as raised:
                    self.complete(receipts={**self.receipts, "csharp": "{"})
            self.assertIn("csharp:", str(raised.exception))
            self.assertIn("summary could not be written", str(raised.exception))
            token = re.search(r"::stop-commands::([0-9a-f]{64})", self.output.getvalue()).group(1)
            self.assertIn(f"::{token}::", self.output.getvalue())


if __name__ == "__main__":
    unittest.main()

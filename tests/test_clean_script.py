import io
import os
import subprocess
import sys
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import clean  # noqa: E402

REPO_ROOT = "/repo"


def completed(cmd, stdout=""):
    return subprocess.CompletedProcess(cmd, 0, stdout=stdout)


class CleanScriptTests(unittest.TestCase):
    def run_main(self, inputs, run_side_effect=None):
        calls = []

        def fake_run(cmd, **kwargs):
            calls.append(cmd)
            if run_side_effect is not None:
                outcome = run_side_effect(cmd)
                if isinstance(outcome, BaseException):
                    raise outcome
            if cmd[:2] == ["git", "rev-parse"]:
                return completed(cmd, stdout=REPO_ROOT + "\n")
            return completed(cmd)

        with mock.patch("subprocess.run", side_effect=fake_run), \
                mock.patch("builtins.input", side_effect=inputs), \
                mock.patch("os.chdir") as chdir, \
                mock.patch("os.makedirs") as makedirs, \
                mock.patch("os.system"), \
                redirect_stdout(io.StringIO()):
            exit_code = None
            try:
                clean.main()
            except SystemExit as exc:
                exit_code = exc.code
        return exit_code, calls, chdir, makedirs

    def test_keyboard_interrupt_at_first_prompt_exits_130(self):
        exit_code, calls, _, _ = self.run_main(KeyboardInterrupt)
        self.assertEqual(exit_code, clean.EXIT_INTERRUPTED)
        self.assertFalse(any(cmd[1] in ("clean", "checkout", "reset") for cmd in calls))

    def test_eof_at_first_prompt_exits_non_zero(self):
        exit_code, calls, _, _ = self.run_main(EOFError)
        self.assertEqual(exit_code, clean.EXIT_FAILURE)
        self.assertFalse(any(cmd[1] in ("clean", "checkout", "reset") for cmd in calls))

    def test_keyboard_interrupt_at_zip_prompt_exits_130(self):
        exit_code, _, _, _ = self.run_main(["", KeyboardInterrupt])
        self.assertEqual(exit_code, clean.EXIT_INTERRUPTED)

    def test_git_status_failure_aborts_before_destructive_steps(self):
        def fail_status(cmd):
            if cmd[:2] == ["git", "status"]:
                return subprocess.CalledProcessError(128, cmd)
            return None

        exit_code, calls, _, _ = self.run_main(["", "n"], fail_status)
        self.assertEqual(exit_code, clean.EXIT_FAILURE)
        self.assertEqual(calls[-1][:2], ["git", "status"])

    def test_missing_git_executable_exits_non_zero(self):
        exit_code, calls, _, _ = self.run_main(
            ["", "n"], lambda cmd: FileNotFoundError(2, "git not found"))
        self.assertEqual(exit_code, clean.EXIT_FAILURE)
        self.assertEqual(len(calls), 1)

    def test_clean_failure_aborts_before_checkout(self):
        def fail_clean(cmd):
            if cmd[:2] == ["git", "clean"]:
                return subprocess.CalledProcessError(1, cmd)
            return None

        exit_code, calls, _, _ = self.run_main(["", "n"], fail_clean)
        self.assertEqual(exit_code, clean.EXIT_FAILURE)
        self.assertNotIn(["git", "checkout", "--", "."], calls)

    def test_runs_from_repository_root(self):
        exit_code, calls, chdir, _ = self.run_main(["", "n"])
        self.assertIsNone(exit_code)
        self.assertEqual(calls[0], ["git", "rev-parse", "--show-toplevel"])
        chdir.assert_called_once_with(REPO_ROOT)

    def test_unstages_then_cleans_excluding_venv_then_checks_out(self):
        _, calls, _, _ = self.run_main(["", "n"])
        self.assertEqual(calls[2:], [
            ["git", "reset", "--quiet", "HEAD"],
            ["git", "clean", "-idx", "-e", ".venv/", "."],
            ["git", "checkout", "--", "."],
        ])

    def test_zip_export_writes_into_ignored_artifacts_directory(self):
        exit_code, calls, _, makedirs = self.run_main(["", "y"])
        self.assertIsNone(exit_code)
        self.assertEqual(calls[-1], ["git", "archive", "--format=zip", "HEAD", "-o", clean.ZIP_PATH])
        self.assertEqual(os.path.dirname(clean.ZIP_PATH), "artifacts")
        makedirs.assert_called_once_with("artifacts", exist_ok=True)

    def test_zip_export_failure_exits_non_zero(self):
        def fail_archive(cmd):
            if cmd[:2] == ["git", "archive"]:
                return subprocess.CalledProcessError(128, cmd)
            return None

        exit_code, _, _, _ = self.run_main(["", "y"], fail_archive)
        self.assertEqual(exit_code, clean.EXIT_FAILURE)


if __name__ == "__main__":
    unittest.main()

"""Exercise the standalone character check against real disposable Git indexes."""

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


CHECKER = Path(__file__).resolve().parents[1] / "tools/ci/check_doc_characters.py"


class DocCharacterTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True, capture_output=True)

    def write(self, name, content, *, tracked=True):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content.encode("utf-8"))
        if tracked:
            subprocess.run(["git", "add", "--", name], cwd=self.root, check=True, capture_output=True)
        return path

    def run_check(self, root=None):
        return subprocess.run(
            [sys.executable, str(CHECKER), str(root or self.root)],
            cwd=CHECKER.parent,
            env={**os.environ, "PYTHONIOENCODING": "utf-8"},
            capture_output=True, text=True, encoding="utf-8", timeout=30,
        )

    def test_reports_every_em_dash_with_character_columns_in_working_bytes(self):
        path = self.write("README.md", "# Test\n\nClean text.\n")
        # The index remains clean: the check must consume the edited candidate file.
        path.write_bytes("# Test\r\n\r\nA\U0001f680\u2014B\u2014\r\n".encode("utf-8"))
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("README.md:3:3: U+2014", result.stdout)
        self.assertIn("README.md:3:5: U+2014", result.stdout)
        self.assertIn("2 em dash(es) in 1 tracked Markdown file(s)", result.stdout)

    def test_covers_root_documents_templates_nested_readmes_and_literal_regions(self):
        names = [
            "README.md", "CONTRIBUTING.md", "SECURITY.md", "SUPPORT.md", "AGENTS.md",
            ".github/pull_request_template.md",
            "mods/example/README.md", "nested/space and \u96ea.md",
            "tools/oni-mod-pipeline/manual/troubleshooting.md",
            "mods/example/translator-handbook.md", "mods/example/workshop-image-production-brief.md",
            "nested/docs/guide.md",
        ]
        for name in names:
            self.write(name, "```text\n\u2014\n```\n")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        for name in names:
            self.assertIn(f"{name}:2:1: U+2014", result.stdout)
        self.assertIn("12 em dash(es) in 12 tracked Markdown file(s)", result.stdout)

    def test_allows_other_unicode_and_excludes_docs_non_markdown_and_untracked_files(self):
        self.write("README.md", "# Unicode\n\u96ea \U0001f680 20\u00b0C \u2013 \u201cquoted\u201d - allowed\n")
        self.write("notes.txt", "\u2014")
        self.write("listing.bbcode", "\u2014")
        self.write("docs/README.md", "\u2014")
        self.write("docs/nested/guide.md", "\u2014")
        self.write("untracked.md", "\u2014", tracked=False)
        self.write("node_modules/example/README.md", "\u2014", tracked=False)
        result = self.run_check()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Checked 1 tracked Markdown file(s): no em dashes.", result.stdout)

    def test_missing_or_invalid_utf8_tracked_sources_fail(self):
        path = self.write("README.md", "# Tracked\n")
        path.unlink()
        missing = self.run_check()
        self.assertEqual(missing.returncode, 1, missing.stdout + missing.stderr)
        self.assertIn("Markdown character check failed:", missing.stdout)
        path.write_bytes(b"# Invalid\n\xff")
        invalid = self.run_check()
        self.assertEqual(invalid.returncode, 1, invalid.stdout + invalid.stderr)
        self.assertIn("Markdown character check failed:", invalid.stdout)

    def test_empty_scope_non_repository_and_subdirectory_fail(self):
        empty = self.run_check()
        self.assertEqual(empty.returncode, 1, empty.stdout + empty.stderr)
        self.assertIn("No tracked Markdown files were selected", empty.stdout)
        with tempfile.TemporaryDirectory() as outside:
            result = self.run_check(Path(outside))
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn("Markdown character check failed:", result.stdout)
        self.write("docs/README.md", "# Clean\n")
        result = self.run_check(self.root / "docs")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("The candidate root must be the Git repository root", result.stdout)

    def test_git_symlink_entry_fails_without_reading_its_target(self):
        self.write("README.md", "# Clean\n")
        target = self.write("target.txt", "outside text", tracked=False)
        blob = subprocess.run(
            ["git", "hash-object", "-w", "--stdin"], cwd=self.root,
            input=b"target.txt", capture_output=True, check=True,
        ).stdout.decode("ascii").strip()
        subprocess.run(
            ["git", "update-index", "--add", "--cacheinfo", f"120000,{blob},linked.md"],
            cwd=self.root, capture_output=True, check=True,
        )
        # A native symlink index entry is portable even without Windows link rights.
        self.assertEqual(target.read_text(encoding="utf-8"), "outside text")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("linked.md: Markdown source must be a regular file without merge conflicts", result.stdout)

    @unittest.skipIf(os.name == "nt", "Windows filenames cannot contain newlines")
    def test_newline_filename_remains_one_diagnostic_record(self):
        self.write("nested/new\nline.md", "\u2014\n")
        result = self.run_check()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("nested/new\\nline.md:1:1: U+2014", result.stdout)
        self.assertEqual(len(result.stdout.splitlines()), 2)


if __name__ == "__main__":
    unittest.main()

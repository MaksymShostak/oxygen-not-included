# SPDX-License-Identifier: AGPL-3.0-only
"""Qualify native sentence formatting and the converter's file-selection boundary."""

from pathlib import Path
import subprocess
import tempfile
import unittest

from format_docs import CONFIG, EXECUTABLE, authored_document_paths, check_documents


class NativeProseFormattingTests(unittest.TestCase):
    def format(self, source: str) -> str:
        result = subprocess.run(
            [str(EXECUTABLE), "--config", str(CONFIG)],
            input=source, capture_output=True, text=True, encoding="utf8", timeout=10,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_sentence_boundaries_and_literal_content(self):
        cases = [
            ("First sentence. Second sentence.\n", "First sentence.\nSecond sentence.\n"),
            ("Use e.g. Node tooling. Check it.\n", "Use e.g. Node tooling.\nCheck it.\n"),
            ("Use version 12.0.2. Check it.\n", "Use version 12.0.2.\nCheck it.\n"),
            ("Use `npm test`. Check it.\n", "Use `npm test`.\nCheck it.\n"),
            ("Run this. `npm test` checks it.\n", "Run this.\n`npm test` checks it.\n"),
            ("Run this. [Testing](testing.md) explains it.\n", "Run this.\n[Testing](testing.md) explains it.\n"),
            ("**First sentence.** Next sentence.\n", "**First sentence.**\nNext sentence.\n"),
            ("Use **checked source**. Check output.\n", "Use **checked source**.\nCheck output.\n"),
            ("```bbcode\n[b]First.[/b] Next.\n```\n", "```bbcode\n[b]First.[/b] Next.\n```\n"),
            ("Keep\u00a0this. Next sentence.\n", "Keep\u00a0this.\nNext sentence.\n"),
            ("First line.\\\nSecond line. Third sentence.\n", "First line.\\\nSecond line.\nThird sentence.\n"),
            ('Say "Done." Then check it.\n', 'Say "Done."\nThen check it.\n'),
            ("First paragraph.\n\nSecond paragraph.\n", "First paragraph.\n\nSecond paragraph.\n"),
            ("> First sentence. Second sentence.\n", "> First sentence.\n> Second sentence.\n"),
            ("- First sentence. Second sentence.\n", "- First sentence.\n  Second sentence.\n"),
        ]
        for source, expected in cases:
            with self.subTest(source=source):
                self.assertEqual(self.format(source), expected)
                self.assertEqual(self.format(expected), expected)

    def test_tables_code_and_hard_breaks_are_preserved(self):
        source = (
            "| First sentence. Second sentence. | Value |\n"
            "| --- | --- |\n"
            "| `a \\| b` | **text** |\n\n"
            "```javascript\n// First sentence. Second sentence.\nconst x = 1;\n```\n\n"
            "First line.  \nSecond line.\n"
        )
        self.assertEqual(self.format(source), source)

    def test_native_check_rejects_without_writing_then_accepts_formatted_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            document = Path(temporary) / "guide.md"
            original = b"First sentence. Second sentence.\n"
            document.write_bytes(original)
            command = [str(EXECUTABLE), "--config", str(CONFIG)]
            failed = subprocess.run([*command, "--check", str(document)], capture_output=True, timeout=10)
            self.assertEqual(failed.returncode, 1)
            self.assertEqual(document.read_bytes(), original)
            written = subprocess.run([*command, "--in-place", str(document)], capture_output=True, timeout=10)
            self.assertEqual(written.returncode, 0, written.stderr)
            self.assertEqual(document.read_bytes(), b"First sentence.\nSecond sentence.\n")
            passed = subprocess.run([*command, "--check", str(document)], capture_output=True, timeout=10)
            self.assertEqual(passed.returncode, 0, passed.stderr)

    def test_only_authored_package_guides_are_selected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            included = ["README.md", "SECURITY.md", "docs/testing.md", "docs/decisions/design.md"]
            excluded = ["docs/reference/index.md", "docs/reference/nested/type.md",
                        "docs/conversion-semantics.md", "docs/steam-support-matrix.md",
                        "test/fixtures/README.md", "node_modules/vendor/README.md"]
            for name in [*included, *excluded]:
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("First. Second.\n", encoding="utf8")
            self.assertEqual(authored_document_paths(root), sorted(root / name for name in included))

    def test_native_render_backstop_cannot_hide_unformatted_sentences(self):
        with tempfile.TemporaryDirectory() as temporary:
            document = Path(temporary) / "guide.md"
            source = ".NET mods retain their own licence. Check the converter.\n"
            document.write_text(source, encoding="utf8")
            self.assertEqual(self.format(source), source, "exercise the native preservation backstop")
            self.assertEqual(check_documents([document]), 1)
            self.assertEqual(document.read_text(encoding="utf8"), source)

    def test_long_sentence_is_allowed_without_a_fixed_column_limit(self):
        with tempfile.TemporaryDirectory() as temporary:
            document = Path(temporary) / "guide.md"
            source = "Keep this sentence, " + "with meaningful prose " * 12 + "on one source line.\n"
            document.write_text(source, encoding="utf8")
            self.assertEqual(self.format(source), source)
            self.assertEqual(check_documents([document]), 0)


if __name__ == "__main__":
    unittest.main()

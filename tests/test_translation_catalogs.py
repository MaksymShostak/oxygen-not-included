"""Independent catalog contract fixtures; no historical artifacts are required."""

from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOLS_ROOT = REPO_ROOT / "tools/translation-catalogs"
PREFIX = "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS."


def load_tool(name):
    """Load an ordinary tool file without requiring package configuration."""
    spec = importlib.util.spec_from_file_location(name, TOOLS_ROOT / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


checker = load_tool("check_catalogs")
reader = load_tool("read_option_catalogs")


def catalog_entry(key, english, translation):
    """Encode fixture input; this builder supplies no expected parser result."""
    return (f"msgctxt {json.dumps(PREFIX + key, ensure_ascii=False)}\n"
            f"msgid {json.dumps(english, ensure_ascii=False)}\n"
            f"msgstr {json.dumps(translation, ensure_ascii=False)}\n\n")


class CatalogContractTests(unittest.TestCase):
    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory()
        self.addCleanup(self.workspace.cleanup)
        self.mod_root = Path(self.workspace.name)
        self.translations = self.mod_root / "translations"
        self.translations.mkdir()
        (self.mod_root / "Source").mkdir()
        self.write_fixture()

    def write_fixture(self, english="Save {0} {0} {1}\nNow", translation="Зберегти {1} {0} {0}\nЗараз"):
        self.english = english
        self.translation = translation
        (self.translations / "delivery_temperature_limit.pot").write_text(
            catalog_entry("BUTTON_SAVE", english, ""), encoding="utf-8")
        self.write_locale(catalog_entry("BUTTON_SAVE", english, translation))
        (self.mod_root / "Source/DeliveryTemperatureLimitStrings.cs").write_text(
            "public static class OPTIONS { public static LocString BUTTON_SAVE = "
            + json.dumps(english, ensure_ascii=False) + "; }", encoding="utf-8")

    def write_locale(self, content):
        (self.translations / "uk.po").write_text(content, encoding="utf-8")

    def assert_failure(self, reason):
        with self.assertRaises(AssertionError) as caught:
            checker.check_catalogs(self.mod_root)
        self.assertEqual(caught.exception.args, (reason,))

    def test_reordered_placeholder_multiset_and_newlines_pass(self):
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))

    def test_inconsistencies_have_independent_diagnostics(self):
        cases = [
            ("Save {0} {0} {1}\nNow", "{0} {1}\n", "placeholders"),
            ("Save {0} {0} {1}\nNow", "{0} {0} {1}", "newlines"),
            ("Wrong {0} {0} {1}\nNow", self.translation, "source text"),
            (self.english, " \t\n", "empty translation"),
        ]
        for english, translation, reason in cases:
            with self.subTest(reason=reason):
                self.write_locale(catalog_entry("BUTTON_SAVE", english, translation))
                self.assert_failure(("uk.po", PREFIX + "BUTTON_SAVE", reason))

    def test_missing_and_extra_keys_fail(self):
        for content in [catalog_entry("DIALOG_TITLE", "Title", "Назва"),
                        catalog_entry("BUTTON_SAVE", self.english, self.translation)
                        + catalog_entry("DIALOG_TITLE", "Title", "Назва")]:
            with self.subTest(content=content):
                self.write_locale(content)
                self.assert_failure("uk.po")

    def test_duplicate_context_fails_before_dictionary_collapse(self):
        entry = catalog_entry("BUTTON_SAVE", self.english, self.translation)
        self.write_locale(entry + entry)
        self.assert_failure(("uk.po", "duplicate keys"))

    def test_missing_template_and_locale_and_empty_catalog(self):
        (self.translations / "delivery_temperature_limit.pot").unlink()
        with self.assertRaises(KeyError):
            checker.check_catalogs(self.mod_root)
        self.write_fixture()
        (self.translations / "uk.po").unlink()
        self.assert_failure("no locale catalogs")
        self.write_locale("# no entries\n")
        self.assert_failure(("uk.po", "no entries"))

    def test_source_declaration_mismatch_fails(self):
        (self.mod_root / "Source/DeliveryTemperatureLimitStrings.cs").write_text(
            'public static class OPTIONS { public static LocString BUTTON_SAVE = "Wrong"; }',
            encoding="utf-8")
        self.assert_failure(("BUTTON_SAVE", "source declaration"))

    def test_additional_template_source_text_is_checked(self):
        (self.translations / "additional.pot").write_text(
            catalog_entry("BUTTON_SAVE", "Wrong", ""), encoding="utf-8")
        self.assert_failure(("additional.pot", PREFIX + "BUTTON_SAVE", "source text"))
        (self.translations / "additional.pot").write_text(
            catalog_entry("BUTTON_SAVE", self.english, ""), encoding="utf-8")
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))

    def test_bom_crlf_and_ignored_non_catalog_files(self):
        path = self.translations / "uk.po"
        text = catalog_entry("BUTTON_SAVE", self.english, self.translation)
        path.write_bytes(b"\xef\xbb\xbf" + text.replace("\n", "\r\n").encode("utf-8"))
        self.assertTrue(path.read_bytes().startswith(b"\xef\xbb\xbf"))
        (self.translations / "note.txt").write_text("not a catalog", encoding="utf-8")
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        self.assertEqual(reader.read_option_catalogs(self.translations)["uk.po"]["BUTTON_SAVE"]["en"],
                         "Save {0} {0} {1}\nNow")

    def test_escaped_quotes_backslashes_and_literal_backslash_n(self):
        self.write_fixture('Say "yes" at C:\\tmp\\n', 'Скажи "так" у C:\\tmp\\n')
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        result = reader.read_option_catalogs(self.translations)["uk.po"]["BUTTON_SAVE"]
        self.assertEqual(result["en"], 'Say "yes" at C:\\tmp\\n')
        self.assertEqual(result["translation"], 'Скажи "так" у C:\\tmp\\n')

    def test_unicode_digits_and_escaped_braces_retain_existing_meaning(self):
        self.write_fixture("{{0}} {١}", "{١} {{0}}")
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        self.write_locale(catalog_entry("BUTTON_SAVE", "{{0}} {١}", "{0}"))
        self.assert_failure(("uk.po", PREFIX + "BUTTON_SAVE", "placeholders"))

    def test_malformed_literal_still_raises_json_error(self):
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        self.write_locale('msgctxt "' + PREFIX + 'BUTTON_SAVE"\nmsgid "\\q"\nmsgstr "Value"\n')
        for operation in [lambda: checker.check_catalogs(self.mod_root),
                          lambda: reader.read_option_catalogs(self.translations)]:
            with self.subTest(operation=operation), self.assertRaises(json.JSONDecodeError):
                operation()

    def test_reader_preserves_literal_block_values_and_absent_selection(self):
        self.write_locale('msgctxt "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_SAVE"\n'
                          'msgid "Save"\nmsgstr "Зберегти"\n')
        entries = reader.read_option_catalogs(self.translations)["uk.po"]
        self.assertEqual(entries, {"BUTTON_SAVE": {
            "block": 'msgctxt "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_SAVE"\n'
                     'msgid "Save"\nmsgstr "Зберегти"',
            "en": "Save", "translation": "Зберегти"}})
        self.assertNotIn("BUTTON_CLOSE_REPORT", entries)

    def test_fixed_enumeration_controls_reader_rendering_and_first_failure(self):
        # Only filesystem enumeration is controlled; parsing and checks stay real.
        uk = self.translations / "uk.po"
        pot = self.translations / "delivery_temperature_limit.pot"
        self.write_locale(catalog_entry("BUTTON_SAVE", "Wrong", "Value"))
        (self.translations / "de.po").write_text(
            catalog_entry("BUTTON_SAVE", "Also wrong", "Wert"), encoding="utf-8")
        with mock.patch.object(Path, "iterdir", return_value=iter([uk, pot, self.translations / "de.po"])):
            self.assert_failure(("uk.po", PREFIX + "BUTTON_SAVE", "source text"))
        self.write_locale('msgctxt "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_SAVE"\n'
                          'msgid "Save"\nmsgstr "Зберегти"\n')
        with mock.patch.object(Path, "iterdir", return_value=iter([uk])):
            result = reader.read_option_catalogs(self.translations)
        self.assertEqual(json.dumps(result, ensure_ascii=True) + "\n",
                         '{"uk.po": {"BUTTON_SAVE": {"block": "msgctxt \\"STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.BUTTON_SAVE\\"\\nmsgid \\"Save\\"\\nmsgstr \\"\\u0417\\u0431\\u0435\\u0440\\u0435\\u0433\\u0442\\u0438\\"", "en": "Save", "translation": "\\u0417\\u0431\\u0435\\u0440\\u0435\\u0433\\u0442\\u0438"}}}\n')

    def test_fresh_operation_observes_changed_content(self):
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        self.write_fixture("Save", "Зберегти")
        self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
        self.assertEqual(reader.read_option_catalogs(self.translations)["uk.po"]["BUTTON_SAVE"]["en"], "Save")

    def test_checker_decodes_each_successful_literal_once_per_operation(self):
        # A spy on the real decoder diagnoses redundant work, not correctness.
        with mock.patch.object(checker.json, "loads", wraps=json.loads) as decode:
            self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
            self.assertEqual(decode.call_count, 4)
            self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
            self.assertEqual(decode.call_count, 8)

    def test_empty_decoded_strings_are_cache_hits(self):
        self.write_fixture("", "Translation")
        with mock.patch.object(checker.json, "loads", wraps=json.loads) as decode:
            self.assertEqual(checker.check_catalogs(self.mod_root), (1, 1))
            self.assertEqual(decode.call_count, 3)

    def test_import_has_no_output_or_input_reads(self):
        for name in ["check_catalogs", "read_option_catalogs", "benchmark_catalogs"]:
            with self.subTest(name=name), redirect_stdout(io.StringIO()) as output, \
                    mock.patch.object(Path, "read_text", side_effect=AssertionError("input read at import")):
                load_tool(name)
            self.assertEqual(output.getvalue(), "")

    def test_invalid_catalog_cli_exits_nonzero_without_success(self):
        self.write_locale(catalog_entry("BUTTON_SAVE", "Wrong", "Value"))
        layout = self.mod_root / "cli-fixture"
        tool = layout / "tools/translation-catalogs/check_catalogs.py"
        tool.parent.mkdir(parents=True)
        shutil.copy2(TOOLS_ROOT / "check_catalogs.py", tool)
        fixture_mod = layout / "mods/delivery-temperature-limit-supercooled"
        shutil.copytree(self.translations, fixture_mod / "translations")
        shutil.copytree(self.mod_root / "Source", fixture_mod / "Source")
        result = subprocess.run([sys.executable, "-B", str(tool)], cwd=self.workspace.name,
                                capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("PASS:", result.stdout)
        self.assertIn("source text", result.stderr)

    def test_benchmark_gate_calculation_rejects_regressions(self):
        # Synthetic numbers check the acceptance calculation, not host timing.
        benchmark = load_tool("benchmark_catalogs")
        warm = {"samples_seconds": {"baseline": [0.010] * 30, "candidate": [0.008] * 30}}
        cli = {"samples_seconds": {"baseline": [0.100] * 20, "candidate": [0.106] * 20}}
        memory = {"baseline": {"peak_bytes": 100}, "candidate": {"peak_bytes": 1048677}}
        result = benchmark.summarize(warm, cli, memory, 0.10, 0.0005)
        self.assertTrue(result["warm_gate"])
        self.assertFalse(result["cli_gate"])
        self.assertFalse(result["memory_gate"])

    def test_current_corpus_and_cli_from_another_working_directory(self):
        mod_root = REPO_ROOT / "mods/delivery-temperature-limit-supercooled"
        self.assertEqual(checker.check_catalogs(mod_root), (82, 18))
        result = subprocess.run([sys.executable, "-B", str(TOOLS_ROOT / "check_catalogs.py")],
                                cwd=self.workspace.name, capture_output=True, text=True, check=True)
        self.assertEqual(result.stdout,
                         "PASS: 82 keys in POT and all 18 locales; source text, translations, placeholders and newlines agree.\n")
        result = subprocess.run([sys.executable, "-B", str(TOOLS_ROOT / "read_option_catalogs.py")],
                                cwd=self.workspace.name, capture_output=True, text=True, check=True)
        projection = json.loads(result.stdout)
        self.assertEqual(len(projection), 19)
        self.assertEqual(set(projection["uk.po"]), {"BUTTON_SAVE", "DIALOG_TITLE", "STATUS_ISSUE_FORM_OPENED",
                                                    "STATUS_LOG_INCLUDED", "STATUS_REPORT_CREATED"})
        self.assertTrue(result.stdout.endswith("\n"))


if __name__ == "__main__":
    unittest.main()

"""Check consistency of the mod's single-line contextual catalog entries.

This preserves the existing extraction subset, not full gettext conformance.
Run without Python's -O option: assertions are the checker contract.
"""

import json
from pathlib import Path
import re


CATALOG_PATTERN = re.compile(r'msgctxt (".*")\nmsgid (".*")\nmsgstr (".*")')


def check_catalogs(mod_root: Path) -> tuple[int, int]:
    """Validate catalog/source consistency; return source-key and locale counts.

    Assertion reasons, parse errors and input enumeration retain the historical
    checker behavior. Inputs are read only; POT translations may be empty.
    """
    catalogs = {}
    for path in (mod_root / "translations").iterdir():
        if path.suffix not in {".po", ".pot"}:
            continue
        entries = [tuple(json.loads(part) for part in match.groups())
                   for match in CATALOG_PATTERN.finditer(
                       path.read_text(encoding="utf-8-sig"))]
        assert entries, (path.name, "no entries")
        assert len({entry[0] for entry in entries}) == len(entries), (
            path.name, "duplicate keys")
        catalogs[path.name] = {context: (english, translation)
                               for context, english, translation in entries}
    source = catalogs["delivery_temperature_limit.pot"]
    locales = [name for name in catalogs if name.endswith(".po")]
    assert locales, "no locale catalogs"
    for name, entries in catalogs.items():
        assert entries.keys() == source.keys(), name
        for key, (english, translation) in entries.items():
            assert english == source[key][0], (name, key, "source text")
            if name.endswith(".po"):
                assert translation.strip(), (name, key, "empty translation")
                assert sorted(re.findall(r"\{\d+\}", english)) == sorted(
                    re.findall(r"\{\d+\}", translation)), (name, key, "placeholders")
                assert english.count("\n") == translation.count("\n"), (
                    name, key, "newlines")
    code = (mod_root / "Source/DeliveryTemperatureLimitStrings.cs").read_text(
        encoding="utf-8-sig")
    options = code.split("public static class OPTIONS", 1)[1]
    for key, value in re.findall(
            r'public static LocString (\w+)\s*=\s*("(?:[^"\\]|\\.)*");', options):
        context = "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS." + key
        assert source[context][0] == json.loads(value), (key, "source declaration")
    return len(source), len(locales)


def main() -> None:
    """Run against repository inputs, independent of the caller's directory."""
    mod_root = Path(__file__).resolve().parents[2] / "mods/delivery-temperature-limit-supercooled"
    key_count, locale_count = check_catalogs(mod_root)
    print(f"PASS: {key_count} keys in POT and all {locale_count} locales; "
          "source text, translations, placeholders and newlines agree.")


if __name__ == "__main__":
    main()

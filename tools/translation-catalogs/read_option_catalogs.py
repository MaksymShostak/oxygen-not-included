"""Project selected Options entries, retaining their raw matched catalog blocks.

The extraction subset is the historical single-line form, not a PO parser.
Absent selected keys stay absent. CLI JSON follows catalog enumeration order.
"""

import json
from pathlib import Path
import re


OPTION_KEYS = {
    "BUTTON_CLOSE_REPORT", "BUTTON_EXPAND_TROUBLESHOOTING",
    "BUTTON_COLLAPSE_TROUBLESHOOTING", "STATUS_REPORT_CREATED",
    "STATUS_LOG_INCLUDED", "STATUS_ISSUE_FORM_OPENED", "BUTTON_SAVE", "DIALOG_TITLE",
}


def read_option_catalogs(translations_root: Path) -> dict:
    """Return raw blocks and decoded values for present selected Options keys."""
    result = {}
    for path in translations_root.iterdir():
        if path.suffix not in {".po", ".pot"}:
            continue
        entries = {}
        for match in re.finditer(
                r'msgctxt "STRINGS\.DELIVERY_TEMPERATURE_LIMIT\.OPTIONS\.(\w+)"\nmsgid (".*")\nmsgstr (".*")',
                path.read_text(encoding="utf-8-sig")):
            if match[1] in OPTION_KEYS:
                entries[match[1]] = {"block": match[0], "en": json.loads(match[2]),
                                     "translation": json.loads(match[3])}
        result[path.name] = entries
    return result


def main() -> None:
    """Render the selection as ASCII-escaped JSON with its trailing newline."""
    translations_root = (Path(__file__).resolve().parents[2]
                         / "mods/delivery-temperature-limit-supercooled/translations")
    print(json.dumps(read_option_catalogs(translations_root), ensure_ascii=True))


if __name__ == "__main__":
    main()

"""Owned catalog backend: a deliberately limited, fail-closed PO subset.

Contextual entries must have three single-line JSON-compatible string literals.
Metadata headers may span lines. This is not a general gettext parser.
"""

import argparse
import json
from pathlib import Path
import re
import sys


PLACEHOLDER_PATTERN = re.compile(r"\{\d+\}")
DIRECTIVE = re.compile(r'(msgctxt|msgid|msgstr) (".*")$')


class CatalogError(ValueError):
    """A rejected catalog or source declaration, also enforced under -O."""


def require(condition, reason):
    if not condition:
        raise CatalogError(reason)


def parse_catalog(path, decode):
    lines = path.read_text(encoding="utf-8-sig").split("\n")
    entries = {}
    index = 0
    header_seen = False
    while index < len(lines):
        line = lines[index]
        if line.startswith("#~") or (line.startswith("#,") and "fuzzy" in {flag.strip() for flag in line[2:].split(",")}):
            raise CatalogError((path.name, index + 1, "obsolete/fuzzy entries unsupported"))
        if not line.strip() or line.startswith("#"):
            index += 1
            continue
        # A non-contextual metadata header is the only multiline exception.
        if line == 'msgid ""' and not header_seen and not entries:
            header_seen = True
            index += 1
            require(index < len(lines) and lines[index].startswith('msgstr "'),
                    (path.name, index + 1, "invalid metadata header"))
            match = DIRECTIVE.fullmatch(lines[index])
            require(match is not None and match[1] == "msgstr",
                    (path.name, index + 1, "invalid metadata header"))
            decode(match[2])
            index += 1
            while index < len(lines) and lines[index].startswith('"'):
                decode(lines[index])
                index += 1
            continue
        tokens = []
        start = index
        for expected in ("msgctxt", "msgid", "msgstr"):
            match = DIRECTIVE.fullmatch(lines[index]) if index < len(lines) else None
            require(match is not None and match[1] == expected,
                    (path.name, index + 1, "unsupported or malformed active syntax"))
            tokens.append(decode(match[2]))
            index += 1
        context, english, translation = tokens
        require(context not in entries, (path.name, "duplicate keys"))
        entries[context] = (english, translation, "\n".join(lines[start:index]))
    require(entries, (path.name, "no entries"))
    return entries


def load_catalogs(directory):
    decoded = {}

    def decode(token):
        if token not in decoded:
            value = json.loads(token)
            require(isinstance(value, str), "catalog literals must be strings")
            decoded[token] = value
        return decoded[token]

    catalogs = {}
    for path in directory.iterdir():
        if path.suffix not in {".po", ".pot"}:
            continue
        require(path.is_file() and not path.is_symlink(), (path.name, "unsafe catalog input"))
        catalogs[path.name] = parse_catalog(path, decode)
    return catalogs, decode


def check_catalogs(directory, template, options_source, context_prefix):
    """Return source-key and locale counts after all declared checks succeed."""
    catalogs, decode = load_catalogs(directory)
    source = catalogs[template]
    locales = [name for name in catalogs if name.endswith(".po")]
    require(locales, "no locale catalogs")
    source_tokens = {key: sorted(PLACEHOLDER_PATTERN.findall(value[0]))
                     for key, value in source.items()}
    source_newlines = {key: value[0].count("\n") for key, value in source.items()}
    for name, entries in catalogs.items():
        require(entries.keys() == source.keys(), name)
        for key, (english, translation, _) in entries.items():
            require(english == source[key][0], (name, key, "source text"))
            if not name.endswith(".po"):
                continue
            require(translation.strip(), (name, key, "empty translation"))
            require(source_tokens[key] == sorted(PLACEHOLDER_PATTERN.findall(translation)),
                    (name, key, "placeholders"))
            require(source_newlines[key] == translation.count("\n"), (name, key, "newlines"))
    code = options_source.read_text(encoding="utf-8-sig")
    require("public static class OPTIONS" in code, "missing OPTIONS class")
    options = code.split("public static class OPTIONS", 1)[1]
    declarations = re.findall(r'public static LocString (\w+)\s*=\s*("(?:[^"\\]|\\.)*");', options)
    # Inventory declarations before accepting the literal-only C# subset.
    declared_names = re.findall(r'public static LocString (\w+)\s*=', options)
    require(declarations and len(declarations) == len(declared_names)
            and len(set(declared_names)) == len(declared_names), "unsupported OPTIONS declarations")
    for key, value in declarations:
        require(context_prefix + key in source and source[context_prefix + key][0] == decode(value),
                (key, "source declaration"))
    return len(source), len(locales)


def read_catalogs(directory, context_prefix, keys=None):
    """Project selected contexts with decoded values and their exact raw blocks."""
    catalogs, _ = load_catalogs(directory)
    return {name: {context[len(context_prefix):]: {"block": block, "en": english,
                                                  "translation": translation}
                   for context, (english, translation, block) in entries.items()
                   if context.startswith(context_prefix)
                   and (keys is None or context[len(context_prefix):] in keys)}
            for name, entries in catalogs.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("check", "inspect"))
    parser.add_argument("--catalog-directory", required=True, type=Path)
    parser.add_argument("--template")
    parser.add_argument("--options-source", type=Path)
    parser.add_argument("--context-prefix", required=True)
    parser.add_argument("--key", action="append")
    args = parser.parse_args()
    response = {"schemaVersion": 1, "operation": args.operation,
                "runtimeVersion": list(sys.version_info[:3]), "success": False,
                "value": None, "diagnostics": []}
    try:
        if args.operation == "check":
            require(args.template and args.options_source, "check requires template and options source")
            keys, locales = check_catalogs(args.catalog_directory, args.template,
                                          args.options_source, args.context_prefix)
            response["value"] = {"keyCount": keys, "localeCount": locales}
        else:
            response["value"] = read_catalogs(args.catalog_directory, args.context_prefix, args.key)
        response["success"] = True
    except (CatalogError, OSError, UnicodeError, json.JSONDecodeError, KeyError) as error:
        response["diagnostics"].append(str(error))
    print(json.dumps(response, ensure_ascii=True))
    return 0 if response["success"] else 1


if __name__ == "__main__":
    sys.exit(main())

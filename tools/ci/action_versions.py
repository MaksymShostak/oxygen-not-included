"""Project central action revisions into the literal uses fields GitHub requires."""

import argparse
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
REVISION = re.compile(r"[0-9a-f]{40}")
REFERENCE = re.compile(r"(?P<repository>[^/@]+/[^/@]+)(?P<subpath>/[^@\s]+)?@[^\s]+")
YAML_USES = re.compile(r"^(?P<prefix>[ \t]*(?:-[ \t]+)?uses:[ \t]*)(?P<value>[^\r\n]*)$", re.MULTILINE)
YAML_VALUE = re.compile(r"(?P<quote>['\"]?)(?P<reference>[^\s'\"]+)(?P=quote)(?P<comment>[ \t]+#.*)?[ \t]*")


def read_catalog(root):
    catalog = json.loads((root / "tools/ci/action-versions.json").read_text(encoding="utf-8"))
    if catalog.get("schemaVersion") != 1 or not isinstance(catalog.get("actions"), dict):
        raise ValueError("Invalid action version catalog")
    for name, entry in catalog["actions"].items():
        if not REPOSITORY.fullmatch(name) or not isinstance(entry, dict):
            raise ValueError(f"Invalid action catalog entry: {name}")
        if not isinstance(entry.get("revision"), str) or not REVISION.fullmatch(entry["revision"]):
            raise ValueError(f"Action revision must be a full commit SHA: {name}")
        if "tag" in entry and (not isinstance(entry["tag"], str) or not re.fullmatch(r"v\d+\.\d+\.\d+", entry["tag"])):
            raise ValueError(f"Invalid action release tag: {name}")
        if set(entry) - {"tag", "revision"}:
            raise ValueError(f"Unknown action catalog settings: {name}")
    return catalog["actions"]


def project_reference(reference, actions, used):
    if not isinstance(reference, str):
        raise ValueError("Action uses must be a literal string")
    if reference.startswith("./"):
        return reference, None
    match = REFERENCE.fullmatch(reference)
    if not match or match["repository"] not in actions:
        raise ValueError(f"Uncatalogued or unsupported action: {reference}")
    name = match["repository"]
    used.add(name)
    entry = actions[name]
    return f"{name}{match['subpath'] or ''}@{entry['revision']}", entry


def project_text(text, actions, used):
    # Two existing workflows and the composite action use JSON (valid YAML).
    if text.lstrip().startswith("{"):
        document = json.loads(text)

        def visit(value):
            if isinstance(value, dict):
                for key, item in value.items():
                    if key == "uses":
                        value[key], _ = project_reference(item, actions, used)
                    else:
                        visit(item)
            elif isinstance(value, list):
                for item in value:
                    visit(item)

        original = json.dumps(document, ensure_ascii=False)
        visit(document)
        if original == json.dumps(document, ensure_ascii=False):
            return text
        return json.dumps(document, indent=2, ensure_ascii=False) + "\n"

    # Preserve the repository's block-style YAML, including quotes and comments.
    def replace(match):
        value = YAML_VALUE.fullmatch(match["value"])
        if not value:
            raise ValueError(f"Unsupported action uses field: {match['value']}")
        reference, entry = project_reference(value["reference"], actions, used)
        comment = value["comment"] or ""
        if entry and entry.get("tag") and re.fullmatch(r"[ \t]+# v\d+\.\d+\.\d+", comment):
            comment = " # " + entry["tag"]
        return f"{match['prefix']}{value['quote']}{reference}{value['quote']}{comment}"

    return YAML_USES.sub(replace, text)


def synchronize(root=ROOT, write=False):
    root = Path(root)
    actions = read_catalog(root)
    used = set()
    projections = []
    for directory in (root / ".github/workflows", root / ".github/actions"):
        for path in sorted(directory.rglob("*")):
            if path.suffix not in {".yml", ".yaml"} or not path.is_file():
                continue
            text = path.read_text(encoding="utf-8")
            try:
                expected = project_text(text, actions, used)
            except ValueError as error:
                raise ValueError(f"{path.relative_to(root)}: {error}") from error
            if text != expected:
                projections.append((path, expected))
    unused = set(actions) - used
    if unused:
        raise ValueError(f"Unused action authorities: {', '.join(sorted(unused))}")
    changed = [path.relative_to(root).as_posix() for path, _ in projections]
    if changed and not write:
        raise ValueError("Action pin drift: " + ", ".join(changed) +
                         ". Edit tools/ci/action-versions.json, then run python tools/ci/action_versions.py --write.")
    for path, expected in projections:
        path.write_text(expected, encoding="utf-8", newline="\n")
    return changed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="Reject drift without writing (default)")
    mode.add_argument("--write", action="store_true", help="Project the approved catalog into workflows and actions")
    args = parser.parse_args()
    try:
        changed = synchronize(write=args.write)
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1
    print("Updated action pins: " + ", ".join(changed) if changed else "GitHub action pins match the central catalog.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

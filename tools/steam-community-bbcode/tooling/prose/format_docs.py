# SPDX-License-Identifier: AGPL-3.0-only
"""Select authored converter documents and invoke the installed native formatter."""

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import sysconfig
import tomllib

PACKAGE_ROOT = Path(__file__).resolve().parents[2]
CONFIG = PACKAGE_ROOT / ".snapperrc.toml"
EXECUTABLE = Path(sysconfig.get_path("scripts")) / (
    "snapper-fmt.exe" if os.name == "nt" else "snapper-fmt"
)


def authored_document_paths(package_root: Path) -> list[Path]:
    """Select package guides while leaving generated projections to their owners."""
    generated = {"conversion-semantics.md", "steam-support-matrix.md"}
    docs = package_root / "docs"
    return sorted(
        [path for path in package_root.glob("*.md") if path.is_file()]
        + [
            path
            for path in docs.rglob("*.md")
            if path.is_file()
            and not path.is_relative_to(docs / "reference")
            and path not in {docs / name for name in generated}
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    args = parser.parse_args()
    with CONFIG.open("rb") as config:
        tomllib.load(config)
    if args.check:
        return check_documents(
            authored_document_paths(PACKAGE_ROOT),
            PACKAGE_ROOT / "artifacts" / "prose" / "check.json",
        )
    result = subprocess.run(
        [
            str(EXECUTABLE),
            "--config", str(CONFIG),
            "--in-place",
            *map(str, authored_document_paths(PACKAGE_ROOT)),
        ],
        cwd=PACKAGE_ROOT,
        check=False,
    )
    return result.returncode


def check_documents(paths: list[Path], report: Path | None = None) -> int:
    """Enforce native formatting findings even when its render backstop preserves input."""
    result = subprocess.run(
        [str(EXECUTABLE), "--config", str(CONFIG), "--check", "--output-format", "json", *map(str, paths)],
        cwd=PACKAGE_ROOT, capture_output=True, text=True, encoding="utf8", check=False,
    )
    if report is not None:
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(result.stdout, encoding="utf8")
    if result.stderr:
        print(result.stderr, file=sys.stderr, end="")
    findings = json.loads(result.stdout)
    failed = result.returncode != 0
    for document in findings:
        if document["would_reformat"]:
            failed = True
            print(f"Would reformat: {document['file']}")
        for diagnostic in document["diagnostics"]:
            if diagnostic["kind"] != "long":
                failed = True
                print(f"{document['file']}:{diagnostic['line']}: {diagnostic['kind']}: {diagnostic['excerpt']}")
    if not failed:
        print(f"Semantic line formatting checked: {len(paths)} authored documents.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())

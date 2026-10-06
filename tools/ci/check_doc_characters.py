"""Check Git-tracked Markdown source characters independently of Markdown linting."""

import argparse
import json
from pathlib import Path
import subprocess
import sys


def git(root: Path, *args: str) -> bytes:
    """Observe Git's native file selection without invoking a shell."""
    result = subprocess.run(
        ["git", *args], cwd=root, check=True, capture_output=True, timeout=30,
    )
    return result.stdout


def check(root: Path) -> int:
    """Reject U+2014 in tracked .md working files outside docs/; incomplete scans fail.

    No text regions are exempt in selected sources. The docs/ tree and untracked
    files are outside this CI source contract. Sources must be regular UTF-8 files,
    not links.
    """
    root = root.resolve(strict=True)
    repository = Path(git(root, "rev-parse", "--show-toplevel").decode("utf-8").strip()).resolve()
    if root != repository:
        raise ValueError("The candidate root must be the Git repository root")

    # NUL records retain spaces, Unicode and newlines in native Git filenames.
    records = git(root, "ls-files", "--cached", "--stage", "-z", "--", "*.md").split(b"\0")
    files = 0
    violations = 0
    for record in records:
        if not record:
            continue
        metadata, name_bytes = record.split(b"\t", 1)
        mode, _, stage = metadata.split()
        name = name_bytes.decode("utf-8")
        if name.startswith("docs/"):
            continue
        # Escape filename controls so each diagnostic occupies one log line.
        display_name = json.dumps(name, ensure_ascii=False)[1:-1]
        if mode not in {b"100644", b"100755"} or stage != b"0":
            raise ValueError(f"{display_name}: Markdown source must be a regular file without merge conflicts")
        path = root / name
        # Never follow a candidate-controlled link, including a linked ancestor.
        if not path.resolve().is_relative_to(root) or any(
            parent.is_symlink() for parent in (path, *path.parents) if parent != root
        ):
            raise ValueError(f"{display_name}: Markdown source must not use links")
        content = path.read_bytes().decode("utf-8")
        files += 1
        for line_number, line in enumerate(content.split("\n"), 1):
            for column, character in enumerate(line, 1):
                if character == "\u2014":
                    violations += 1
                    print(f"{display_name}:{line_number}:{column}: U+2014 em dash is not allowed; use a hyphen (U+002D).")

    if not files:
        raise ValueError("No tracked Markdown files were selected")
    if violations:
        print(f"Found {violations} em dash(es) in {files} tracked Markdown file(s).")
        return 1
    print(f"Checked {files} tracked Markdown file(s): no em dashes.")
    return 0


def main() -> int:
    """Expose a read-only local/CI check with nonzero failure diagnostics."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", default=".")
    args = parser.parse_args()
    # The source and filenames are UTF-8 on both Windows and Linux consumers.
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        return check(Path(args.root))
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Markdown character check failed: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

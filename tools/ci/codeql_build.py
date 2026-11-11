"""Compile every authored C# project under CodeQL with exact real game references."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from oni_references import CATALOG, MANIFEST, ROOT, ReferenceError, json_bytes, read_catalog, validate_directory


def projects(root: Path) -> list[str]:
    paths = subprocess.check_output(["git", "ls-files", "-z", "--", "*.csproj"], cwd=root).split(b"\0")
    result = sorted(path.decode("utf-8") for path in paths if path)
    if not result:
        raise ReferenceError("The checkout has no authored C# projects")
    return result


def commands(root: Path, managed: Path, output: Path) -> list[list[str]]:
    selected = projects(root)
    restore = [["dotnet", "restore", str(root / project), "--locked-mode",
                "--artifacts-path", str(output / "artifacts")] for project in selected]
    build = [["dotnet", "build", str(root / project), "--no-restore", "--no-incremental",
              "--configuration", "Release", "--artifacts-path", str(output / "artifacts"),
              "-p:UseSharedCompilation=false", "-p:BuildInParallel=false",
              f"-p:OniManagedAssemblyDirectory={managed}",
              f"-p:OniMergedModOutputPath={output / 'merged' / Path(project).stem / 'DeliveryTemperatureLimit.dll'}"]
             for project in selected]
    return restore + build


def build(managed: Path, output: Path, root: Path = ROOT) -> Path:
    managed = managed.resolve(strict=True)
    output = output.resolve()
    manifest_data = (managed.parent / MANIFEST).read_bytes()
    catalog = read_catalog(CATALOG)
    if hashlib.sha256(manifest_data).hexdigest() != catalog["manifestSha256"]:
        raise ReferenceError("Build references do not have the pinned manifest identity")
    manifest = json.loads(manifest_data)
    validate_directory(managed, manifest)
    if output.exists():
        raise ReferenceError(f"A new isolated CodeQL build directory is required: {output}")
    output.mkdir(parents=True)
    invocations = commands(root, managed, output)
    for arguments in invocations:
        subprocess.run(arguments, cwd=root, check=True)
    validate_directory(managed, manifest)
    evidence = output / "build-evidence.json"
    evidence.write_bytes(json_bytes({
        "schemaVersion": 1,
        "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
        "sdk": subprocess.check_output(["dotnet", "--version"], cwd=root, text=True).strip(),
        "package": catalog["package"], "manifestSha256": catalog["manifestSha256"],
        "projects": projects(root), "commands": invocations,
        "gameReferences": manifest["files"],
    }))
    return evidence


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--managed-directory", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    args = parser.parse_args()
    print(f"Completed traced-build input evidence: {build(args.managed_directory, args.output_directory)}")


if __name__ == "__main__":
    try:
        main()
    except (ReferenceError, OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"CodeQL build: {error}") from error

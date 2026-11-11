"""Snapshot real game DLLs privately and acquire the exact pinned reference set."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "tools/ci/oni-references.json"
MANIFEST = "references.json"
PACKAGE_ID = "MaksymShostak.ONI.GameReferences"


class ReferenceError(ValueError):
    """The supplied files cannot establish the pinned game reference identity."""


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value: dict) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def declared_references(root: Path = ROOT) -> set[str]:
    names = set()
    for project in sorted((root / "mods").glob("*/Source/*.csproj")):
        for hint in ET.parse(project).findall(".//Reference/HintPath"):
            prefix = "$(OniManagedAssemblyDirectory)/"
            if (hint.text or "").startswith(prefix):
                name = hint.text[len(prefix):]
                if Path(name).name != name or not name.endswith(".dll"):
                    raise ReferenceError(f"Unsupported game reference in {project}: {name}")
                names.add(name)
    if not {"Assembly-CSharp.dll", "UnityEngine.CoreModule.dll"} <= names:
        raise ReferenceError("No complete production ONI reference declaration was found")
    return names


def read_catalog(path: Path = CATALOG) -> dict:
    catalog = json.loads(path.read_text(encoding="utf-8"))
    package = catalog["package"]
    if (catalog.get("schemaVersion") != 1
            or not re.fullmatch(r"[A-Za-z0-9-]+", package["owner"])
            or package["id"] != PACKAGE_ID
            or not re.fullmatch(r"\d+\.\d+\.\d+", package["version"])
            or type(package["byteLength"]) is not int or package["byteLength"] <= 0
            or not re.fullmatch(r"[0-9a-f]{64}", package["sha256"])
            or not re.fullmatch(r"[0-9a-f]{64}", catalog["manifestSha256"])):
        raise ReferenceError("Invalid ONI reference catalog")
    return catalog


def snapshot(managed: Path, version: str, destination: Path, catalog_path: Path) -> Path:
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ReferenceError("The private reference package needs an exact NuGet version")
    managed = managed.resolve(strict=True)
    paths = sorted(managed.glob("*.dll"), key=lambda p: p.name)
    if not declared_references() <= {p.name for p in paths}:
        raise ReferenceError("The game installation lacks declared ONI/Unity reference DLLs")
    if any(p.is_symlink() or not p.is_file() for p in paths):
        raise ReferenceError("Game references must be regular DLL files")
    contents = {p.name: p.read_bytes() for p in paths}
    manifest = {"schemaVersion": 1, "packageId": PACKAGE_ID, "version": version,
                "files": {name: {"byteLength": len(data), "sha256": digest(data)}
                          for name, data in contents.items()}}
    metadata = ET.Element("package")
    fields = ET.SubElement(metadata, "metadata")
    for name, value in {"id": PACKAGE_ID, "version": version, "authors": "MaksymShostak",
                        "description": "Private original ONI/Unity managed references for analysis builds."}.items():
        ET.SubElement(fields, name).text = value
    entries = {PACKAGE_ID + ".nuspec": ET.tostring(metadata, encoding="utf-8", xml_declaration=True),
               MANIFEST: json_bytes(manifest),
               **{"references/" + name: data for name, data in contents.items()}}
    destination.mkdir(parents=True, exist_ok=True)
    archive = destination / f"{PACKAGE_ID}.{version}.nupkg"
    if archive.exists():
        raise ReferenceError(f"Refusing to replace an existing package: {archive}")
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as package:
        for name, data in sorted(entries.items()):
            item = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            item.compress_type = zipfile.ZIP_DEFLATED
            item.external_attr = 0o100644 << 16
            package.writestr(item, data)
    if any(p.read_bytes() != contents[p.name] for p in paths):
        raise ReferenceError("The game installation changed while its references were copied")
    data = archive.read_bytes()
    catalog = {"schemaVersion": 1,
               "package": {"owner": "MaksymShostak", "id": PACKAGE_ID, "version": version,
                           "byteLength": len(data), "sha256": digest(data)},
               "manifestSha256": digest(entries[MANIFEST])}
    catalog_path.write_bytes(json_bytes(catalog))
    return archive


def package_download_project(catalog: dict) -> bytes:
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(properties, "TargetFramework").text = "net10.0"
    items = ET.SubElement(project, "ItemGroup")
    package = catalog["package"]
    ET.SubElement(items, "PackageDownload", Include=package["id"], Version=f'[{package["version"]}]')
    return ET.tostring(project, encoding="utf-8", xml_declaration=True)


def download(catalog: dict, work: Path) -> Path:
    token = os.environ.get("ONI_REFERENCES_TOKEN", "")
    if not token or any(c in token for c in "\r\n\0;"):
        raise ReferenceError("A read-only ONI_REFERENCES_TOKEN is required for the private package")
    package = catalog["package"]
    project = work / "References.csproj"
    project.write_bytes(package_download_project(catalog))
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="OniReferences",
                  value=f'https://nuget.pkg.github.com/{package["owner"]}/index.json')
    config_path = work / "NuGet.Config"
    config_path.write_bytes(ET.tostring(config, encoding="utf-8", xml_declaration=True))
    environment = {**os.environ,
                   "NuGetPackageSourceCredentials_OniReferences":
                       f'Username={package["owner"]};Password={token};ValidAuthenticationTypes=Basic'}
    cache = work / "packages"
    subprocess.run(["dotnet", "restore", str(project), "--configfile", str(config_path),
                    "--packages", str(cache), "--verbosity", "minimal"],
                   cwd=ROOT, env=environment, check=True)
    return cache / package["id"].lower() / package["version"] / (
        package["id"].lower() + "." + package["version"] + ".nupkg")


def verified_contents(archive: Path, catalog: dict) -> tuple[dict, dict[str, bytes]]:
    data = archive.read_bytes()
    pin = catalog["package"]
    if len(data) != pin["byteLength"] or digest(data) != pin["sha256"]:
        raise ReferenceError("The reference package does not match its pinned SHA-256 and length")
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(set(names)):
            raise ReferenceError("The reference package contains duplicate entries")
        manifest_data = package.read(MANIFEST)
        if digest(manifest_data) != catalog["manifestSha256"]:
            raise ReferenceError("The reference manifest does not match its pinned SHA-256")
        manifest = json.loads(manifest_data)
        if (manifest.get("schemaVersion") != 1 or manifest.get("packageId") != pin["id"]
                or manifest.get("version") != pin["version"]):
            raise ReferenceError("The reference manifest has the wrong package identity")
        files = manifest["files"]
        expected = {MANIFEST, pin["id"] + ".nuspec", *("references/" + name for name in files)}
        if set(names) != expected:
            raise ReferenceError("The package inventory is not the exact declared reference set")
        contents = {}
        for name, identity in files.items():
            if (Path(name).name != name or "/" in name or "\\" in name
                    or not name.endswith(".dll")):
                raise ReferenceError("The reference manifest contains an unsafe DLL path")
            value = package.read("references/" + name)
            if len(value) != identity["byteLength"] or digest(value) != identity["sha256"]:
                raise ReferenceError(f"The game DLL does not match its manifest: {name}")
            contents[name] = value
    if not declared_references() <= contents.keys():
        raise ReferenceError("The package lacks production ONI/Unity references")
    return manifest, contents


def validate_directory(managed: Path, manifest: dict) -> None:
    if not managed.is_dir() or managed.is_symlink():
        raise ReferenceError("A real managed assembly directory is required")
    if {p.name for p in managed.iterdir()} != set(manifest["files"]):
        raise ReferenceError("The acquired reference directory has changed inventory")
    for name, identity in manifest["files"].items():
        path = managed / name
        if path.is_symlink() or not path.is_file():
            raise ReferenceError(f"The reference is not a regular game DLL: {name}")
        data = path.read_bytes()
        if len(data) != identity["byteLength"] or digest(data) != identity["sha256"]:
            raise ReferenceError(f"The acquired game reference has changed: {name}")


def acquire(output: Path, catalog: dict, archive: Path | None = None) -> Path:
    if output.exists():
        raise ReferenceError(f"A new isolated reference directory is required: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="oni-package-", dir=output.parent) as work:
        archive = archive if archive is not None else download(catalog, Path(work))
        manifest, contents = verified_contents(archive, catalog)
        managed = output / "Managed"
        managed.mkdir(parents=True)
        for name, data in contents.items():
            (managed / name).write_bytes(data)
        (output / MANIFEST).write_bytes(json_bytes(manifest))
    validate_directory(managed, manifest)
    return managed.resolve()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="operation", required=True)
    pack = commands.add_parser("pack")
    pack.add_argument("--managed-directory", type=Path, required=True)
    pack.add_argument("--version", required=True)
    pack.add_argument("--output-directory", type=Path, required=True)
    pack.add_argument("--catalog", type=Path, default=CATALOG)
    get = commands.add_parser("acquire")
    get.add_argument("--output-directory", type=Path, required=True)
    get.add_argument("--archive", type=Path)
    get.add_argument("--catalog", type=Path, default=CATALOG)
    args = parser.parse_args()
    if args.operation == "pack":
        print(snapshot(args.managed_directory, args.version, args.output_directory, args.catalog))
    else:
        managed = acquire(args.output_directory, read_catalog(args.catalog), args.archive)
        print(f"Verified ONI reference directory: {managed}")
        if output := os.environ.get("GITHUB_OUTPUT"):
            with Path(output).open("a", encoding="utf-8", newline="\n") as stream:
                stream.write(f"managed_directory={managed}\n")


if __name__ == "__main__":
    try:
        main()
    except (ReferenceError, OSError, ValueError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        raise SystemExit(f"ONI references: {error}") from error

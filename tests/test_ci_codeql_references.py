"""Exercise private reference integrity and the build-backed analysis boundary."""

import copy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools/ci"))
import oni_references as references
import codeql_build


class GameReferenceIntegrityTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.managed = self.root / "game/Managed"
        self.managed.mkdir(parents=True)
        self.files = {"Assembly-CSharp.dll": b"fixture ONI bytes", "UnityEngine.CoreModule.dll": b"fixture Unity bytes"}
        for name, data in self.files.items():
            (self.managed / name).write_bytes(data)
        # These fixtures test byte identity, never replace production build references.
        declaration = patch.object(references, "declared_references", return_value=set(self.files))
        declaration.start()
        self.addCleanup(declaration.stop)
        self.catalog_path = self.root / "catalog.json"
        self.archive = references.snapshot(self.managed, "1.2.3", self.root / "packages", self.catalog_path)
        self.catalog = references.read_catalog(self.catalog_path)

    def repack(self, entries):
        target = self.root / "changed.nupkg"
        with zipfile.ZipFile(target, "w") as package:
            for name, data in entries.items():
                package.writestr(name, data)
        catalog = copy.deepcopy(self.catalog)
        catalog["package"].update(byteLength=target.stat().st_size, sha256=references.digest(target.read_bytes()))
        return target, catalog

    def entries(self):
        with zipfile.ZipFile(self.archive) as package:
            return {name: package.read(name) for name in package.namelist()}

    def test_snapshot_is_reproducible_and_contains_only_dlls_and_metadata(self):
        (self.managed / "player.log").write_text("private user data")
        second = references.snapshot(self.managed, "1.2.3", self.root / "second", self.root / "second.json")
        self.assertEqual(second.read_bytes(), self.archive.read_bytes())
        entries = self.entries()
        self.assertEqual(set(entries), {references.MANIFEST, references.PACKAGE_ID + ".nuspec",
                                       *("references/" + name for name in self.files)})
        metadata = ET.fromstring(entries[references.PACKAGE_ID + ".nuspec"])
        self.assertIsNone(metadata.find(".//repository"))

    def test_missing_production_dll_or_existing_package_is_rejected(self):
        with self.assertRaisesRegex(references.ReferenceError, "existing package"):
            references.snapshot(self.managed, "1.2.3", self.archive.parent, self.catalog_path)
        (self.managed / "Assembly-CSharp.dll").unlink()
        with self.assertRaisesRegex(references.ReferenceError, "lacks declared"):
            references.snapshot(self.managed, "1.2.3", self.root / "third", self.catalog_path)

    def test_acquisition_preserves_exact_original_bytes_and_refuses_overwrite(self):
        output = self.root / "acquired"
        managed = references.acquire(output, self.catalog, self.archive)
        self.assertEqual({p.name: p.read_bytes() for p in managed.iterdir()}, self.files)
        with self.assertRaisesRegex(references.ReferenceError, "new isolated"):
            references.acquire(output, self.catalog, self.archive)

    def test_corrupt_archive_fails_before_any_reference_is_written(self):
        self.archive.write_bytes(self.archive.read_bytes() + b"changed")
        output = self.root / "acquired"
        with self.assertRaisesRegex(references.ReferenceError, "pinned SHA-256"):
            references.acquire(output, self.catalog, self.archive)
        self.assertFalse(output.exists())

    def test_manifest_pin_cannot_be_bypassed_by_changing_archive_pin(self):
        entries = self.entries()
        entries[references.MANIFEST] += b" "
        archive, catalog = self.repack(entries)
        with self.assertRaisesRegex(references.ReferenceError, "manifest.*pinned"):
            references.verified_contents(archive, catalog)

    def test_unexpected_inventory_and_changed_dll_bytes_are_rejected(self):
        for extra in (False, True):
            with self.subTest(extra=extra):
                entries = self.entries()
                entries["unreviewed.targets" if extra else "references/Assembly-CSharp.dll"] = b"changed"
                archive, catalog = self.repack(entries)
                with self.assertRaisesRegex(references.ReferenceError, "inventory|DLL.*manifest"):
                    references.verified_contents(archive, catalog)

    def test_manifest_path_traversal_is_rejected_even_when_both_pins_match(self):
        entries = self.entries()
        manifest = json.loads(entries[references.MANIFEST])
        manifest["files"]["../outside.dll"] = manifest["files"].pop("Assembly-CSharp.dll")
        entries["references/../outside.dll"] = entries.pop("references/Assembly-CSharp.dll")
        entries[references.MANIFEST] = references.json_bytes(manifest)
        archive, catalog = self.repack(entries)
        catalog["manifestSha256"] = references.digest(entries[references.MANIFEST])
        with self.assertRaisesRegex(references.ReferenceError, "unsafe DLL path"):
            references.verified_contents(archive, catalog)

    def test_acquired_inventory_and_dll_changes_fail_validation(self):
        managed = references.acquire(self.root / "acquired", self.catalog, self.archive)
        manifest = json.loads((managed.parent / references.MANIFEST).read_bytes())
        (managed / "Assembly-CSharp.dll").write_bytes(b"changed")
        with self.assertRaisesRegex(references.ReferenceError, "reference has changed"):
            references.validate_directory(managed, manifest)
        (managed / "extra.dll").write_bytes(b"extra")
        with self.assertRaisesRegex(references.ReferenceError, "changed inventory"):
            references.validate_directory(managed, manifest)

    def test_native_download_uses_exact_data_only_package_and_transient_credentials(self):
        work = self.root / "download"
        work.mkdir()
        with patch.dict(os.environ, {"ONI_REFERENCES_TOKEN": "fixture-secret"}), \
                patch.object(references.subprocess, "run") as run:
            references.download(self.catalog, work)
        arguments = run.call_args.args[0]
        self.assertEqual(arguments[:2], ["dotnet", "restore"])
        self.assertNotIn("fixture-secret", " ".join(arguments))
        self.assertIn("Password=fixture-secret", run.call_args.kwargs["env"]["NuGetPackageSourceCredentials_OniReferences"])
        project = ET.parse(work / "References.csproj")
        self.assertEqual(project.find(".//PackageDownload").attrib,
                         {"Include": references.PACKAGE_ID, "Version": "[1.2.3]"})
        self.assertIsNone(project.find(".//PackageReference"))
        self.assertIsNotNone(ET.parse(work / "NuGet.Config").find(".//packageSources/clear"))
        for path in work.iterdir():
            self.assertNotIn("fixture-secret", path.read_text())

    def test_build_covers_every_authored_project_with_locked_fresh_compilation(self):
        tracked = subprocess.check_output(["git", "ls-files", "-z", "--", "*.csproj"], cwd=ROOT).decode().split("\0")
        expected = {str(ROOT / path) for path in tracked if path}
        commands = codeql_build.commands(ROOT, self.managed, self.root / "build")
        restores = [command for command in commands if command[1] == "restore"]
        builds = [command for command in commands if command[1] == "build"]
        self.assertEqual({command[2] for command in restores}, expected)
        self.assertEqual({command[2] for command in builds}, expected)
        self.assertTrue(any("/Source/" in path.replace("\\", "/") for path in expected))
        for command in restores:
            self.assertIn("--locked-mode", command)
        for command in builds:
            self.assertTrue({"--no-restore", "--no-incremental", "-p:UseSharedCompilation=false"} <= set(command))
            self.assertIn(f"-p:OniManagedAssemblyDirectory={self.managed}", command)


class BuildBackedWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.workflow = json.loads((ROOT / ".github/workflows/codeql-csharp.yml").read_text())

    def assert_contract(self, workflow):
        self.assertEqual(workflow["permissions"], {})
        job = workflow["jobs"]["analyze"]
        self.assertEqual(job["permissions"], {"contents": "read", "packages": "read", "security-events": "write"})
        self.assertNotIn("continue-on-error", job)
        steps = job["steps"]
        self.assertEqual(len(steps), 9)
        self.assertIn("checks.py validate", steps[2]["run"])
        self.assertIn('language == "csharp"', steps[2]["run"])
        self.assertIn('plan["checks"]["codeql"] and plan["eligibility"]["codeql"]', steps[2]["run"])
        self.assertFalse(steps[0]["with"]["persist-credentials"])
        self.assertEqual(steps[3]["with"], {"global-json-file": "global.json"})
        self.assertRegex(steps[3]["uses"], r"^actions/setup-dotnet@[0-9a-f]{40}$")
        self.assertIn("oni_references.py acquire", steps[4]["run"])
        self.assertEqual(steps[4]["env"], {"ONI_REFERENCES_TOKEN": "${{ github.token }}"})
        self.assertEqual(steps[5]["with"], {"languages": "csharp", "build-mode": "manual", "tools": "linked"})
        self.assertRegex(steps[5]["uses"], r"^github/codeql-action/init@[0-9a-f]{40}$")
        self.assertEqual(steps[5]["uses"].split("@")[1], steps[7]["uses"].split("@")[1])
        self.assertIn("codeql_build.py", steps[6]["run"])
        self.assertEqual(steps[6]["env"]["ONI_MANAGED_ASSEMBLY_DIRECTORY"], "${{ steps.references.outputs.managed_directory }}")
        self.assertRegex(steps[7]["uses"], r"^github/codeql-action/analyze@[0-9a-f]{40}$")
        self.assertEqual(steps[7]["with"], {"category": "/language:csharp", "upload-database": False})
        self.assertEqual(steps[8]["env"], {"ONI_ANALYSIS_RESULT": "${{ steps.analysis.outcome }}"})
        self.assertEqual(steps[8]["run"], "python3 tools/ci/checks.py codeql-receipt")
        for step in steps:
            self.assertFalse({"if", "continue-on-error"} & step.keys())

    def test_workflow_builds_with_actual_references_before_analysis(self):
        self.assert_contract(self.workflow)
        root = (ROOT / ".github/workflows/oni-checks.yml").read_text()
        self.assertIn("packages: read", root.split("\n  codeql:")[1].split("\n  dependency:")[0])

    def test_buildless_skipped_or_ignored_builds_break_contract(self):
        for mutation in ("buildless", "skipped", "ignored", "missing"):
            with self.subTest(mutation=mutation):
                changed = copy.deepcopy(self.workflow)
                steps = changed["jobs"]["analyze"]["steps"]
                if mutation == "buildless":
                    steps[5]["with"]["build-mode"] = "none"
                elif mutation == "skipped":
                    steps[6]["if"] = "false"
                elif mutation == "ignored":
                    steps[6]["continue-on-error"] = True
                else:
                    steps.pop(6)
                with self.assertRaises(AssertionError):
                    self.assert_contract(changed)


if __name__ == "__main__":
    unittest.main()

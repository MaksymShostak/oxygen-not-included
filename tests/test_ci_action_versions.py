"""Reject stale or missing action authorities before executing dependent CI jobs."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "tools/ci/action_versions.py"
spec = importlib.util.spec_from_file_location("action_versions", SOURCE)
versions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(versions)


class ActionVersionsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.catalog = {"schemaVersion": 1, "actions": {
            "owner/tool": {"tag": "v2.0.0", "revision": "a" * 40}}}
        self.put("tools/ci/action-versions.json", json.dumps(self.catalog))

    def put(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def test_check_rejects_drift_without_writing(self):
        path = self.put(".github/workflows/check.yml", "steps:\n  - uses: owner/tool@v1\n")
        before = path.read_bytes()
        with self.assertRaisesRegex(ValueError, "Action pin drift"):
            versions.synchronize(self.root)
        self.assertEqual(path.read_bytes(), before)

    def test_write_projects_yaml_json_subactions_and_preserves_local_steps(self):
        yaml = self.put(".github/workflows/check.yml",
                        "steps:\n  - uses: 'owner/tool/init@v1' # v1.0.0\n"
                        "  - uses: ./local/action\n  - run: echo uses\n")
        composite = self.put(".github/actions/tool/action.yml", json.dumps({"runs": {"steps": [
            {"uses": "owner/tool/analyze@old"}, {"uses": "./another/action"}]}}))
        self.assertEqual(set(versions.synchronize(self.root, write=True)),
                         {".github/workflows/check.yml", ".github/actions/tool/action.yml"})
        self.assertIn("'owner/tool/init@" + "a" * 40 + "' # v2.0.0", yaml.read_text())
        self.assertIn("  - uses: ./local/action\n  - run: echo uses\n", yaml.read_text())
        steps = json.loads(composite.read_text())["runs"]["steps"]
        self.assertEqual(steps, [{"uses": "owner/tool/analyze@" + "a" * 40}, {"uses": "./another/action"}])
        self.assertEqual(versions.synchronize(self.root), [])
        self.assertEqual(versions.synchronize(self.root, write=True), [])

    def test_unknown_consumer_prevents_partial_writes(self):
        first = self.put(".github/workflows/a.yml", "steps:\n  - uses: owner/tool@old\n")
        self.put(".github/workflows/z.yml", "steps:\n  - uses: another/tool@v1\n")
        before = first.read_bytes()
        with self.assertRaisesRegex(ValueError, "Uncatalogued"):
            versions.synchronize(self.root, write=True)
        self.assertEqual(first.read_bytes(), before)

    def test_unused_and_invalid_authorities_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "Unused action authorities"):
            versions.synchronize(self.root)
        self.catalog["actions"]["owner/tool"]["revision"] = "main"
        self.put("tools/ci/action-versions.json", json.dumps(self.catalog))
        with self.assertRaisesRegex(ValueError, "full commit SHA"):
            versions.synchronize(self.root, write=True)

    def test_new_workflow_consumer_is_checked(self):
        self.put(".github/actions/tool/action.yaml", "runs:\n  steps:\n    - uses: owner/tool@" + "a" * 40 + "\n")
        self.assertEqual(versions.synchronize(self.root), [])
        self.put(".github/workflows/new.yaml", 'steps:\n  - uses: "owner/tool@stale" # keep this note\n')
        with self.assertRaisesRegex(ValueError, "new.yaml"):
            versions.synchronize(self.root)
        versions.synchronize(self.root, write=True)
        self.assertIn('" # keep this note', (self.root / ".github/workflows/new.yaml").read_text())

    def test_actual_repository_projections_match(self):
        self.assertEqual(versions.synchronize(SOURCE.parents[2]), [])


if __name__ == "__main__":
    unittest.main()

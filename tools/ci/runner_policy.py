"""Project the repository's Ubuntu release into native GitHub job selectors."""

import argparse
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
UBUNTU_RELEASE = re.compile(r"\d[02468]\.04", re.ASCII)
UBUNTU_RUNNER = re.compile(r"ubuntu-(?:\d[02468]\.04|latest)", re.ASCII)
JSON_RUNNER = re.compile(r'("runs-on"\s*:\s*")' + UBUNTU_RUNNER.pattern + r'(")')
JSON_JOBS = {
    "codeql-csharp.yml": {"analyze"},
    "codeql-language.yml": {"analyze"},
    "codeql.yml": {"validate", "complete"},
}
YAML_JOBS = {
    "dependency-submission.yml": {"submit"},
    "oni-checks.yml": {"select", "documentation", "required"},
}
YAML_JOB = re.compile(r"^  (?P<job>[A-Za-z0-9_-]+):\n(?P<body>.*?)(?=^ {0,2}\S|\Z)", re.M | re.S)
YAML_RUNNER = re.compile(
    r"^(?P<prefix>    runs-on:[ \t]*)(?P<quote>['\"]?)"
    r"(?P<label>ubuntu-[\w.-]+)(?P=quote)(?=[ \t]*(?:#.*)?$)", re.M,
)
UBUNTU_LABEL = re.compile(r"\bubuntu-(?:\d{2}\.\d{2}(?:-arm)?|latest)\b", re.ASCII)


def ubuntu_runner(root=ROOT):
    policy = json.loads((root / ".github/runner-policy.json").read_text(encoding="utf-8"))
    release = policy.get("ubuntuRelease") if isinstance(policy, dict) else None
    if not isinstance(release, str) or not UBUNTU_RELEASE.fullmatch(release):
        raise ValueError("ubuntuRelease must be an even-year Ubuntu LTS release in YY.04 format")
    return f"ubuntu-{release}"


def project_workflow(name, source, runner):
    if name in JSON_JOBS:
        workflow = json.loads(source)
        jobs = {
            job_id: job for job_id, job in workflow["jobs"].items()
            if isinstance(job.get("runs-on"), str) and job["runs-on"].startswith("ubuntu-")
        }
        if (
            jobs.keys() != JSON_JOBS[name]
            or len(UBUNTU_LABEL.findall(source)) != len(jobs)
            or any(not UBUNTU_RUNNER.fullmatch(job["runs-on"]) for job in jobs.values())
        ):
            raise ValueError(f"{name}: unexpected Ubuntu jobs or runner labels")
        if all(job["runs-on"] == runner for job in jobs.values()):
            return source
        for job in jobs.values():
            job["runs-on"] = runner
        updated, count = JSON_RUNNER.subn(lambda match: match[1] + runner + match[2], source)
        if count != len(jobs) or json.loads(updated) != workflow:
            raise ValueError(f"{name}: Ubuntu selectors must be literal job fields")
        return updated

    updated = source
    matched = set()
    for job in reversed(list(YAML_JOB.finditer(source))):
        selectors = list(YAML_RUNNER.finditer(job["body"]))
        if not selectors:
            continue
        if (
            job["job"] not in YAML_JOBS[name] or job["job"] in matched
            or len(selectors) != 1 or not UBUNTU_RUNNER.fullmatch(selectors[0]["label"])
        ):
            raise ValueError(f"{name}: unexpected Ubuntu selector in {job['job']}")
        matched.add(job["job"])
        start, end = selectors[0].span("label")
        offset = job.start("body")
        updated = updated[:offset + start] + runner + updated[offset + end:]
    if matched != YAML_JOBS[name] or len(UBUNTU_LABEL.findall(source)) != len(matched):
        raise ValueError(f"{name}: expected exactly these Ubuntu jobs: {', '.join(sorted(YAML_JOBS[name]))}")
    return updated


def synchronize(root=ROOT, *, write=False):
    runner = ubuntu_runner(root)
    updates = []
    for name in sorted(JSON_JOBS.keys() | YAML_JOBS.keys()):
        path = root / ".github/workflows" / name
        source = path.read_text(encoding="utf-8")
        updated = project_workflow(name, source, runner)
        if updated != source:
            updates.append((path, updated))
    if updates and not write:
        changed = ", ".join(path.name for path, _ in updates)
        raise ValueError(f"Ubuntu runner drift in {changed}. Run python tools/ci/runner_policy.py --write.")
    for path, updated in updates:
        path.write_text(updated, encoding="utf-8", newline="\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="Reject drift without writing (default)")
    mode.add_argument("--write", action="store_true", help="Update native job selectors from the policy")
    args = parser.parse_args()
    try:
        synchronize(write=args.write)
    except (OSError, KeyError, TypeError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1
    print("GitHub Ubuntu runner selectors match the policy.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

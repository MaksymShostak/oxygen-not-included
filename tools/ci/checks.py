"""Select ONI consumers and fail closed on identity or completion uncertainty."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import threading

SCHEMA = 1
LANGUAGES = ("actions", "csharp", "python")
CHECKS = ("pipeline", "python", "producer", "documentation", "codeql", "dependency")
SHA = re.compile(r"[0-9a-f]{40}\Z")
MAX_GIT_BYTES = 4 * 1024 * 1024
MAX_PLAN_BYTES = 256 * 1024
DOC_ASSETS = {".md", ".txt", ".diff", ".patch", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".pdf"}


class CheckError(ValueError):
    """An observation cannot support a successful check."""


def git(root: Path, *args: str) -> bytes:
    """Bound both streams during observation, rather than after buffering them."""
    process = subprocess.Popen(
        ["git", "--no-lazy-fetch", *args], cwd=root,
        env={**os.environ, "GIT_NO_LAZY_FETCH": "1", "GIT_TERMINAL_PROMPT": "0"},
        stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    streams = [bytearray(), bytearray()]
    total = 0
    overflow = threading.Event()
    lock = threading.Lock()

    def read(index: int) -> None:
        nonlocal total
        stream = process.stdout if index == 0 else process.stderr
        assert stream is not None
        while block := stream.read(4096):
            with lock:
                total += len(block)
                if total > MAX_GIT_BYTES:
                    overflow.set()
                    if process.poll() is None:
                        process.kill()
                    break
                streams[index].extend(block)

    readers = [threading.Thread(target=read, args=(i,), daemon=True) for i in range(2)]
    for reader in readers:
        reader.start()
    try:
        process.wait(timeout=30)
    except subprocess.TimeoutExpired as error:
        process.kill()
        process.wait(timeout=5)
        raise CheckError("Git observation timed out") from error
    finally:
        for reader in readers:
            reader.join(timeout=1)
        for stream in (process.stdout, process.stderr):
            if stream is not None and not any(reader.is_alive() for reader in readers):
                stream.close()
    if any(reader.is_alive() for reader in readers):
        raise CheckError("Git stream completion was not observed")
    if overflow.is_set():
        raise CheckError("Git observation exceeded 4 MiB")
    if process.returncode:
        raise CheckError("Git observation failed")
    return bytes(streams[0])


def checked_sha(value: object) -> str:
    if not isinstance(value, str) or not SHA.fullmatch(value):
        raise CheckError("Expected a complete lowercase commit SHA")
    return value


def changes(root: Path, base: str, revision: str) -> tuple[list[dict], str]:
    raw = git(root, "diff", "--raw", "-z", "--no-renames", "--no-ext-diff", "--no-textconv", base, revision, "--")
    fields = raw.split(b"\0")
    if fields[-1] != b"" or (len(fields) - 1) % 2:
        raise CheckError("Incomplete Git change records")
    records = []
    for index in range(0, len(fields) - 1, 2):
        metadata = fields[index].decode("ascii").split()
        if len(metadata) != 5 or not metadata[0].startswith(":") or metadata[4] not in {"A", "M", "D", "T"}:
            raise CheckError("Unsupported Git change record")
        records.append({"path": fields[index + 1].decode("utf-8", "surrogateescape"),
                        "old_mode": metadata[0][1:], "new_mode": metadata[1], "status": metadata[4]})
    return records, hashlib.sha256(raw).hexdigest()


def classify(path: str) -> tuple[set[str], set[str]] | None:
    """Consumers own source, fixtures, dependency and execution-control inputs."""
    lower = path.lower()
    suffix = Path(lower).suffix
    if path.startswith("docs/"):
        return {"documentation"}, set()
    if path in {"AGENTS.md", ".gitignore", ".gitattributes"} or path.startswith("tools/ci/") or path == "tests/test_ci_checks.py" or path.startswith(".github/workflows/"):
        return set(CHECKS), set(LANGUAGES)
    if path.startswith(".github/actions/"):
        return {"codeql"}, {"actions"}
    if path == ".markdown-quality.json" or path.startswith("tooling/markdown/"):
        return {"documentation", "pipeline", "producer", "codeql"}, set(LANGUAGES)
    if path in {"package.json", "package-lock.json"}:
        return {"pipeline", "producer", "documentation", "codeql"}, {"csharp"}
    if lower in {"global.json", "nuget.config", "nuget.config.json"} or suffix in {".csproj", ".sln", ".slnx", ".props", ".targets"} or lower.endswith("/packages.lock.json") or lower.endswith("/nuget.config"):
        return {"pipeline", "producer", "dependency", "codeql"}, {"csharp"}
    if path == "clean.py" or path == "tests/test_clean_script.py" or path == ".python-version":
        return {"python", "codeql"}, {"python"}
    if path == "tests/test_translation_catalogs.py" or path.startswith("tools/translation-catalogs/") or path.endswith("/Catalogs/Python/catalogs.py"):
        return {"python", "pipeline", "codeql"}, {"python", "csharp"}
    if path.startswith("tools/oni-mod-pipeline/"):
        checks = {"pipeline", "codeql"}
        if path.startswith("tools/oni-mod-pipeline/src/OniModPipeline/") or "/Readme/" in path or "/Cli/" in path:
            checks.add("producer")
        if suffix == ".py":
            checks.add("python")
        if suffix == ".md":
            checks.add("documentation")
        return checks, {"python"} if suffix == ".py" else {"csharp"}
    if path.startswith("mods/"):
        if suffix == ".md" and "/Tests/" in path:
            return {"documentation", "pipeline"}, set()
        checks = {"pipeline", "python"}
        if suffix == ".md":
            checks.add("documentation")
        if path == "mods/delivery-temperature-limit-supercooled/README.md":
            checks.add("producer")
        if suffix in {".bbcode", ".toml", ".yaml", ".yml"} or path == "mods/delivery-temperature-limit-supercooled/Preview.png":
            checks.add("producer")
        return checks, {"csharp"} if suffix == ".cs" else set()
    if suffix == ".md" and ("/" not in path or path == ".github/pull_request_template.md"):
        return {"documentation"}, set()
    return None


def full_checks(codeql: bool, dependency: bool) -> tuple[dict, list[str]]:
    return {name: codeql if name == "codeql" else dependency if name == "dependency" else True for name in CHECKS}, list(LANGUAGES) if codeql else []


def select(root: Path, event: str, payload: dict, revision: str, repository: str) -> dict:
    checked_sha(revision)
    if git(root, "rev-parse", "HEAD").decode().strip() != revision:
        raise CheckError("Checkout does not match the tested revision")
    base = None
    if event == "pull_request":
        pr = payload["pull_request"]
        base = checked_sha(pr["base"]["sha"])
        head = checked_sha(pr["head"]["sha"])
        if git(root, "rev-list", "--parents", "-n", "1", revision).decode().split() != [revision, base, head]:
            raise CheckError("PR merge parents do not match the captured event")
        codeql = pr["base"]["ref"] == "main" and pr["head"]["repo"]["full_name"] == repository
        dependency = False
    elif event == "push":
        if payload["ref"] != "refs/heads/main" or checked_sha(payload["after"]) != revision:
            raise CheckError("Push identity is not the supported main event")
        before = checked_sha(payload["before"])
        base = None if before == "0" * 40 else before
        codeql = dependency = True
    elif event in {"workflow_dispatch", "schedule"}:
        if os.environ.get("GITHUB_REF", "refs/heads/main") != "refs/heads/main":
            raise CheckError("Full qualification requires main")
        codeql = dependency = True
    else:
        raise CheckError("Unsupported event")

    # Docs are data only: do not let a new executable, link or submodule acquire consumers.
    for entry in git(root, "ls-tree", "-r", "-z", revision, "--", "docs").split(b"\0"):
        if not entry:
            continue
        metadata, path_bytes = entry.split(b"\t", 1)
        path = path_bytes.decode("utf-8", "surrogateescape")
        if metadata.split()[0] != b"100644" or Path(path).suffix.lower() not in DOC_ASSETS:
            raise CheckError("Unsupported entry inside the documentation boundary")

    fallback = None
    records = None
    change_digest = None
    if base is None:
        fallback = "full-event" if event in {"schedule", "workflow_dispatch"} else "new-main-history"
    else:
        try:
            git(root, "cat-file", "-e", base + "^{commit}")
        except CheckError:
            try:
                git(root, "fetch", "--no-tags", "--depth=1", "origin", base)
            except CheckError:
                fallback = "comparison-unavailable"
        if fallback is None:
            try:
                records, change_digest = changes(root, base, revision)
            except CheckError:
                fallback = "comparison-unavailable"

    if fallback:
        checks, languages = full_checks(codeql, dependency)
    else:
        selected: set[str] = set()
        selected_languages: set[str] = set()
        assert records is not None
        for record in records:
            classification = classify(record["path"])
            if classification is None:
                fallback = "unknown-input"
                break
            check_names, language_names = classification
            selected.update(check_names)
            selected_languages.update(language_names)
            # Add/delete/type/mode changes anywhere can affect a local link target.
            # A regular non-Markdown file's content-only edit cannot change link existence.
            if record["status"] != "M" or record["old_mode"] != record["new_mode"]:
                selected.add("documentation")
        if fallback:
            checks, languages = full_checks(codeql, dependency)
        else:
            languages = sorted(selected_languages) if codeql else []
            checks = {name: name in selected for name in CHECKS}
            checks["codeql"] = bool(languages)
            checks["dependency"] = checks["dependency"] and dependency

    plan = {"schema": SCHEMA, "event": event, "base_sha": base, "revision": revision,
            "repository": repository, "eligibility": {"codeql": codeql, "dependency": dependency},
            "policy_sha256": hashlib.sha256(git(root, "show", revision + ":tools/ci/checks.py")).hexdigest(),
            "checks": checks, "languages": languages, "fallback": fallback,
            "changes": records, "change_sha256": change_digest}
    if len(json.dumps(plan).encode()) > MAX_PLAN_BYTES:
        plan.update(checks=full_checks(codeql, dependency)[0], languages=list(LANGUAGES) if codeql else [],
                    fallback="selection-record-limit", changes=None)
    return plan


def validate(plan: dict, expected: dict) -> None:
    if not isinstance(plan, dict) or not isinstance(plan.get("checks"), dict) or any(type(value) is not bool for value in plan["checks"].values()):
        raise CheckError("Malformed decision flags")
    if json.dumps(plan, sort_keys=True) == json.dumps(expected, sort_keys=True):
        return
    # A valid earlier observation may have lacked a comparison now available.
    if plan.get("fallback") == "comparison-unavailable" and set(plan) == set(expected):
        identity = ("schema", "event", "base_sha", "revision", "repository", "eligibility", "policy_sha256")
        if all(plan[key] == expected[key] for key in identity) and plan["changes"] is None and plan["change_sha256"] is None:
            full, languages = full_checks(**expected["eligibility"])
            if plan["checks"] == full and plan["languages"] == languages:
                return
    raise CheckError("Decision differs from independently recomputed scope or identity")


def codeql_receipts(plan: dict, directory: Path, run_id: str, attempt: str) -> None:
    """Same-run data proves every selected matrix member reached completion."""
    found = []
    for path in directory.iterdir():
        if not path.is_file() or path.is_symlink() or path.stat().st_size > 65536:
            raise CheckError("Unsafe or oversized analysis receipt")
        receipt = json.loads(path.read_text(encoding="utf-8"))
        language = receipt.get("language")
        if language not in plan["languages"] or path.name != f"{language}.json" or receipt != {
            "language": language, "revision": plan["revision"], "policy_sha256": plan["policy_sha256"],
            "run_id": run_id, "attempt": attempt,
        }:
            raise CheckError("Analysis receipt does not match this selected attempt")
        found.append(language)
    if sorted(found) != sorted(plan["languages"]) or not found:
        raise CheckError("A selected analysis language did not return completion")


def evaluate(plan: dict, results: dict, completions: dict) -> None:
    expected_jobs = {"worker", "documentation", "codeql", "dependency"}
    if set(results) != expected_jobs or set(completions) != expected_jobs:
        raise CheckError("Missing or unexpected consumer keys")
    checks = plan["checks"]
    required = {"worker": checks["pipeline"] or checks["python"] or checks["producer"],
                "documentation": checks["documentation"], "codeql": checks["codeql"], "dependency": checks["dependency"]}
    for job, needed in required.items():
        status = results[job]
        if status not in {"success", "skipped"} or needed and status != "success":
            raise CheckError(f"Consumer {job} did not complete successfully")
        if needed and completions[job] != plan["revision"]:
            raise CheckError(f"Consumer {job} has missing or stale completion identity")


def dependency_ready(root: Path, plan: dict) -> str:
    if not plan["checks"]["dependency"] or plan["event"] == "pull_request":
        raise CheckError("Dependency publication is not selected or trusted")
    tip = checked_sha(git(root, "ls-remote", "--exit-code", "origin", "refs/heads/main").decode().split()[0])
    try:
        git(root, "cat-file", "-e", tip + "^{commit}")
    except CheckError:
        git(root, "fetch", "--no-tags", "--depth=1", "origin", tip)
    for record in changes(root, plan["revision"], tip)[0]:
        classification = classify(record["path"])
        if classification is None or "dependency" in classification[0]:
            raise CheckError("Snapshot dependency inputs are obsolete or uncertain")
    return tip


def emit(values: dict) -> None:
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8", newline="\n") as stream:
            for name, value in values.items():
                stream.write(f"{name}={json.dumps(value, separators=(',', ':')) if not isinstance(value, str) else value}\n")
    print(json.dumps(values, separators=(",", ":")))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("operation", choices=("select", "validate", "gate", "dependency-ready", "complete", "codeql-receipt", "codeql-complete"))
    args = parser.parse_args()
    root = Path.cwd()
    payload_path = Path(os.environ["GITHUB_EVENT_PATH"])
    if payload_path.stat().st_size > 1024 * 1024:
        raise CheckError("Event payload is too large")
    expected = select(root, os.environ["GITHUB_EVENT_NAME"], json.loads(payload_path.read_text(encoding="utf-8")),
                      os.environ["GITHUB_SHA"], os.environ["GITHUB_REPOSITORY"])
    if args.operation == "select":
        emit({"plan": json.dumps(expected, separators=(",", ":")), "languages": expected["languages"],
              **expected["checks"], "worker": any(expected["checks"][key] for key in ("pipeline", "python", "producer"))})
        return 0
    plan = json.loads(os.environ["ONI_PLAN"])
    validate(plan, expected)
    if args.operation == "validate":
        emit({"languages": plan["languages"]})
    elif args.operation == "gate":
        evaluate(plan, json.loads(os.environ["ONI_RESULTS"]), json.loads(os.environ["ONI_COMPLETIONS"]))
    elif args.operation == "dependency-ready":
        emit({"observed_main": dependency_ready(root, plan), "snapshot_sha": plan["revision"]})
    elif args.operation == "codeql-receipt":
        language = os.environ["ONI_LANGUAGE"]
        if language not in plan["languages"] or os.environ["ONI_ANALYSIS_RESULT"] != "success":
            raise CheckError("Selected analysis did not succeed")
        directory = Path(os.environ["ONI_RECEIPTS"])
        directory.mkdir(parents=True, exist_ok=True)
        (directory / f"{language}.json").write_text(json.dumps({"language": language, "revision": plan["revision"],
            "policy_sha256": plan["policy_sha256"], "run_id": os.environ["GITHUB_RUN_ID"],
            "attempt": os.environ["GITHUB_RUN_ATTEMPT"]}), encoding="utf-8")
    elif args.operation == "codeql-complete":
        if os.environ["ONI_ANALYSIS_RESULT"] != "success":
            raise CheckError("Analysis matrix did not succeed")
        codeql_receipts(plan, Path(os.environ["ONI_RECEIPTS"]), os.environ["GITHUB_RUN_ID"], os.environ["GITHUB_RUN_ATTEMPT"])
        emit({"completion_sha": plan["revision"]})
    elif args.operation == "complete":
        consumer = os.environ["ONI_CONSUMER"]
        if consumer == "worker":
            statuses = json.loads(os.environ["ONI_STEPS"])
            if set(statuses) != {"pipeline", "python", "producer"}:
                raise CheckError("Incomplete selected-step results")
            for key, status in statuses.items():
                if status not in {"success", "skipped"} or plan["checks"][key] and status != "success":
                    raise CheckError("A selected worker step did not succeed")
        elif consumer not in CHECKS or not plan["checks"][consumer]:
            raise CheckError("Completion was not selected")
        emit({"completion_sha": plan["revision"]})
    if summary := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(summary, "a", encoding="utf-8") as stream:
            stream.write(f"ONI {args.operation}: `{plan['revision']}`; selected `{json.dumps(plan['checks'], sort_keys=True)}`; fallback `{plan['fallback']}`.\n")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (CheckError, KeyError, TypeError, ValueError, OSError) as error:
        print(f"ONI checks failed: {error}")
        raise SystemExit(1) from error

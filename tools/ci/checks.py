"""Select ONI consumers and fail closed on identity or completion uncertainty."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import threading

SCHEMA = 1
LANGUAGES = ("actions", "csharp", "python")
CHECKS = ("pipeline", "python", "producer", "documentation", "codeql", "dependency")
SHA = re.compile(r"[0-9a-f]{40}\Z")
MAX_GIT_BYTES = 4 * 1024 * 1024
MAX_PLAN_BYTES = 256 * 1024
MAX_CODEQL_RECEIPT_BYTES = 4 * 1024
MAX_CODEQL_COMPLETION_SUMMARY_BYTES = 16 * 1024
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
    if path in {".markdown-quality.json", ".markdown-quality-execution.json", ".node-version", ".python-version"} or path.startswith("tooling/markdown/"):
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


def codeql_receipt_identity(plan: dict, language: str, run_id: str, attempt: str) -> dict:
    """Return the exact five-field identity for selected, trusted CodeQL analysis."""
    if (not plan["checks"]["codeql"] or not plan["eligibility"]["codeql"]
            or language not in LANGUAGES or language not in plan["languages"]):
        raise CheckError("CodeQL language is unknown, unselected or ineligible")
    checked_sha(plan["revision"])
    if not isinstance(plan["policy_sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", plan["policy_sha256"]):
        raise CheckError("CodeQL policy digest must be a complete lowercase SHA-256")
    for value in (run_id, attempt):
        if not isinstance(value, str) or not re.fullmatch(r"[1-9][0-9]*", value):
            raise CheckError("CodeQL run and attempt must be positive decimal strings")
    return {"language": language, "revision": plan["revision"], "policy_sha256": plan["policy_sha256"],
            "run_id": run_id, "attempt": attempt}


def create_codeql_receipt(plan: dict, language: str, analysis_result: str, run_id: str, attempt: str) -> str:
    """Create one bounded output only after successful selected-language analysis."""
    if analysis_result != "success":
        raise CheckError("Selected CodeQL analysis did not succeed")
    receipt = json.dumps(codeql_receipt_identity(plan, language, run_id, attempt), separators=(",", ":"))
    if len(receipt.encode("utf-8")) > MAX_CODEQL_RECEIPT_BYTES:
        raise CheckError("CodeQL receipt exceeds 4 KiB UTF-8")
    return receipt


def _unique_json_members(pairs: list[tuple[str, object]]) -> dict:
    """Reject ambiguous JSON identities rather than accepting the last duplicate."""
    members = {}
    for name, value in pairs:
        if name in members:
            raise CheckError("CodeQL receipt has duplicate JSON member names")
        members[name] = value
    return members


def _diagnostic_excerpt(text: str, maximum_bytes: int) -> str:
    """Bound diagnostic display explicitly; this never makes oversized evidence valid."""
    encoded = text.encode("utf-8", errors="replace")
    if len(encoded) <= maximum_bytes:
        return text
    return (encoded[:maximum_bytes].decode("utf-8", errors="replace")
            + f" [diagnostic excerpt: first {maximum_bytes} of {len(encoded)} UTF-8 bytes;"
            + f" SHA-256 {hashlib.sha256(encoded).hexdigest()}]")


def complete_codeql_analysis(plan: dict, results: dict, receipts: dict, run_id: str,
                             attempt: str, validation_result: str) -> str:
    """Diagnose every fixed slot, then admit only complete current-attempt evidence.

    Missing/redacted outputs and mixed-attempt partial reruns fail closed. All
    readable siblings are reported before rejection; there is no archive reader.
    """
    errors = []
    observations = []
    if validation_result != "success":
        errors.append("CodeQL selection validation did not succeed")
    if set(results) != set(LANGUAGES) or set(receipts) != set(LANGUAGES):
        errors.append("Missing or unexpected CodeQL result/receipt slots")
    if not plan["languages"] or not set(plan["languages"]) <= set(LANGUAGES):
        errors.append("CodeQL selected language set is empty or unknown")
    for language in LANGUAGES:
        is_selected = language in plan["languages"]
        result = results.get(language, "")
        raw_receipt = receipts.get(language, "")
        slot_errors = []
        if is_selected:
            if result != "success":
                slot_errors.append("selected analysis did not succeed")
            if not raw_receipt:
                slot_errors.append("missing completion receipt (possibly suppressed)")
            else:
                try:
                    if not isinstance(raw_receipt, str):
                        raise CheckError("CodeQL receipt must be a string")
                    if len(raw_receipt.encode("utf-8")) > MAX_CODEQL_RECEIPT_BYTES:
                        raise CheckError("CodeQL receipt exceeds 4 KiB UTF-8")
                    if "\n" in raw_receipt or "\r" in raw_receipt:
                        raise CheckError("CodeQL receipt must be single-line JSON")
                    receipt = json.loads(raw_receipt, object_pairs_hook=_unique_json_members)
                    expected = codeql_receipt_identity(plan, language, run_id, attempt)
                    if not isinstance(receipt, dict) or set(receipt) != set(expected):
                        raise CheckError("CodeQL receipt must contain exactly the five identity fields")
                    if any(not isinstance(value, str) for value in receipt.values()):
                        raise CheckError("CodeQL receipt identity fields must be strings")
                    if receipt != expected:
                        raise CheckError("CodeQL receipt does not match selected language/source/policy/run/attempt")
                except (CheckError, ValueError, TypeError, RecursionError) as error:
                    slot_errors.append(str(error))
        elif result != "skipped" or raw_receipt != "":
            slot_errors.append("unselected analysis must be skipped without a receipt")
        errors.extend(f"{language}: {error}" for error in slot_errors)
        observations.append((language, is_selected, str(result), str(raw_receipt), slot_errors))

    # Raw candidate text must not become runner commands or summary markup.
    command_token = secrets.token_hex(32)
    print(f"::stop-commands::{command_token}", flush=True)
    try:
        summary_lines = ["### ONI CodeQL completion", "", "<pre>"]
        for language, is_selected, result, raw_receipt, slot_errors in observations:
            status = f"{language}: selected={str(is_selected).lower()}; result={_diagnostic_excerpt(result, 128)}"
            print(status)
            print("receipt=" + (_diagnostic_excerpt(raw_receipt, MAX_CODEQL_RECEIPT_BYTES)
                                if raw_receipt else "MISSING"))
            for error in slot_errors:
                print("invalid=" + error)
            summary_lines.extend([html.escape(status), "receipt=" + _diagnostic_excerpt(
                html.escape(raw_receipt), 2048) if raw_receipt else "receipt=MISSING"])
            summary_lines.extend(html.escape("invalid=" + error) for error in slot_errors)
        print("CodeQL completion: " + ("REJECTED" if errors else "accepted"))
        summary_lines.extend(["</pre>", "", "Rejected." if errors else "Accepted.", ""])
        summary_text = "\n".join(summary_lines)
        if len(summary_text.encode("utf-8")) > MAX_CODEQL_COMPLETION_SUMMARY_BYTES:
            errors.append("CodeQL completion summary exceeds 16 KiB UTF-8")
        elif summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
            try:
                with open(summary_path, "a", encoding="utf-8", newline="\n") as stream:
                    stream.write(summary_text)
            except OSError as error:
                # A reporting error remains visible and cannot replace prior failure.
                print(f"CodeQL completion summary write failed: {error}")
                errors.append("CodeQL completion summary could not be written")
    finally:
        print(f"::{command_token}::", flush=True)
    if errors:
        raise CheckError("; ".join(errors) + "; rerun the complete selected CodeQL set plus its completer")
    return plan["revision"]


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
        emit({"receipt": create_codeql_receipt(plan, os.environ["ONI_LANGUAGE"], os.environ["ONI_ANALYSIS_RESULT"],
              os.environ["GITHUB_RUN_ID"], os.environ["GITHUB_RUN_ATTEMPT"])})
        return 0
    elif args.operation == "codeql-complete":
        results = {language: os.environ.get(f"ONI_{language.upper()}_RESULT", "") for language in LANGUAGES}
        receipts = {language: os.environ.get(f"ONI_{language.upper()}_RECEIPT", "") for language in LANGUAGES}
        revision = complete_codeql_analysis(plan, results, receipts, os.environ["GITHUB_RUN_ID"],
                                           os.environ["GITHUB_RUN_ATTEMPT"], os.environ.get("ONI_VALIDATION_RESULT", ""))
        emit({"completion_sha": revision})
        return 0
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

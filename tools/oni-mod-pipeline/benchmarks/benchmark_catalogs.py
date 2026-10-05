"""Measure frozen baseline/candidate catalog operations without CI timing tests.

Each invocation is one independent session. Baseline root must have the tracked
tool layout and identical catalog/source inputs. Results are descriptive, not a
statistical significance claim. Memory tracing never runs during timing samples.
"""

import argparse
import gc
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import platform
import statistics
import subprocess
import sys
import tempfile
import time
import timeit
import tracemalloc


MOD_PATH = Path("mods/delivery-temperature-limit-supercooled")
TOOL_PATH = Path("tools/oni-mod-pipeline/src/OniModPipeline/Catalogs/Python/catalogs.py")
PREFIX = "STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS."


def load_tool(path):
    """Load frozen tool bytes without invoking their CLI."""
    spec = importlib.util.spec_from_file_location(path.stem, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def file_identity(path):
    """Bind a result to actual bytes rather than a branch or filename alone."""
    return {"path": str(path.resolve()), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def input_manifest(root):
    """Capture all consumed catalog and declaration files in a fixed manifest."""
    paths = [path for path in (root / "translations").iterdir()
             if path.suffix in {".po", ".pot"}]
    paths.append(root / "Source/DeliveryTemperatureLimitStrings.cs")
    return {str(path.relative_to(root)): file_identity(path)["sha256"]
            for path in sorted(paths)}


def warm_measurements(operations):
    """Balance variant order, enable GC, and create fresh caches per operation."""
    timers = {name: timeit.Timer(operation, setup=gc.enable)
              for name, operation in operations.items()}
    for operation in operations.values():
        for _ in range(5):
            operation()
    # Choose enough loops for even the fastest variant to take about 100 ms.
    durations = [timer.timeit(number=1) for timer in timers.values()]
    loops = max(1, math.ceil(0.1 / min(durations)))
    samples = {name: [] for name in operations}
    names = list(operations)
    for pair in range(30):
        for name in names if pair % 2 == 0 else reversed(names):
            samples[name].append(timers[name].timeit(number=loops) / loops)
    return {"loops_per_value": loops, "gc_enabled": True, "samples_seconds": samples}


def cli_measurements(paths, cwd):
    """Include process/import/input/rendering cost; reject nonzero CLI results."""
    samples = {name: [] for name in paths}
    captures = {}
    names = list(paths)
    for pair in range(20):
        for name in names if pair % 2 == 0 else reversed(names):
            start = time.perf_counter_ns()
            result = subprocess.run([sys.executable, "-I", "-B", *paths[name]], cwd=cwd,
                                    capture_output=True, check=True, timeout=30)
            samples[name].append((time.perf_counter_ns() - start) / 1e9)
            output = {"stdout_sha256": hashlib.sha256(result.stdout).hexdigest(),
                      "stderr": result.stderr.decode("utf-8", errors="replace"),
                      "exit_code": result.returncode}
            if name in captures and captures[name] != output:
                raise RuntimeError(f"CLI output changed during measurement: {name}")
            captures[name] = output
    # Reader bytes can differ with filesystem enumeration; mapping equivalence
    # is established separately, without canonicalizing the actual CLI output.
    return {"samples_seconds": samples, "captures": captures}


def peak_allocations(operation):
    """Record traced Python allocation, not RSS; release results before readback."""
    gc.collect()
    tracemalloc.start()
    try:
        operation()
        first_current, first_peak = tracemalloc.get_traced_memory()
        tracemalloc.reset_peak()
        operation()
        gc.collect()
        second_current, second_peak = tracemalloc.get_traced_memory()
        return {"peak_bytes": max(first_peak, second_peak),
                "first_return_bytes": first_current, "second_return_bytes": second_current}
    finally:
        tracemalloc.stop()


def summarize(warm, cli, memory, minimum_fraction, minimum_seconds):
    """Apply the preset per-session engineering gates without a hypothesis test."""
    before = statistics.median(warm["samples_seconds"]["baseline"])
    after = statistics.median(warm["samples_seconds"]["candidate"])
    cold_before = statistics.median(cli["samples_seconds"]["baseline"])
    cold_after = statistics.median(cli["samples_seconds"]["candidate"])
    delta = memory["candidate"]["peak_bytes"] - memory["baseline"]["peak_bytes"]
    return {"warm_baseline_ms": before * 1000, "warm_candidate_ms": after * 1000,
            "warm_reduction_fraction": 1 - after / before,
            "warm_saved_ms": (before - after) * 1000,
            "cli_baseline_ms": cold_before * 1000, "cli_candidate_ms": cold_after * 1000,
            "peak_delta_bytes": delta,
            "warm_gate": before - after >= minimum_seconds and 1 - after / before >= minimum_fraction,
            "cli_gate": cold_after - cold_before <= max(cold_before * 0.05, 0.002),
            "memory_gate": delta <= 1024 * 1024,
            "ranges_ms": {name: [min(values) * 1000, max(values) * 1000]
                          for name, values in warm["samples_seconds"].items()}}


def stress_memory(operations, source_root, fixture_root):
    """Exercise larger repeats and distinct translations while keeping valid keys.

    Fully unique tokens cannot satisfy checker source/locale equality. Distinct
    translation tokens exercise the maximum relevant variation instead.
    """
    import shutil

    # Only consumed inputs belong in the stress fixture, not binaries, images,
    # game fixtures or other mod artifacts unrelated to catalog consistency.
    shutil.copytree(source_root / "translations", fixture_root / "translations")
    (fixture_root / "Source").mkdir()
    shutil.copy2(source_root / "Source/DeliveryTemperatureLimitStrings.cs",
                 fixture_root / "Source/DeliveryTemperatureLimitStrings.cs")
    translations = fixture_root / "translations"
    originals = [path for path in translations.iterdir() if path.suffix == ".po"]
    for index in range(2):
        for path in originals:
            (translations / f"repeat_{index}_{path.name}").write_bytes(path.read_bytes())
    repeated = {name: peak_allocations(lambda operation=operation: operation(fixture_root))
                for name, operation in operations.items()}
    for path in translations.iterdir():
        if path.suffix == ".po":
            # Appending plain text changes no numbered tokens or newline counts.
            lines = path.read_text(encoding="utf-8-sig").splitlines(keepends=True)
            for index, line in enumerate(lines):
                if line.startswith('msgstr "') and line.strip() != 'msgstr ""':
                    value = json.loads(line[len("msgstr "):].strip())
                    lines[index] = "msgstr " + json.dumps(value + f" unique {path.name} {index}") + "\n"
            path.write_text("".join(lines), encoding="utf-8")
    distinct = {name: peak_allocations(lambda operation=operation: operation(fixture_root))
                for name, operation in operations.items()}
    return {"repeated_locales": repeated, "distinct_translation_tokens": distinct}


def main():
    """Run one bounded session and persist its raw results to operator evidence."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    candidate_root = Path(__file__).resolve().parents[3]
    source_root = candidate_root / MOD_PATH
    baseline_inputs = input_manifest(args.baseline_root / MOD_PATH)
    candidate_inputs = input_manifest(source_root)
    if baseline_inputs != candidate_inputs:
        raise RuntimeError("Baseline and candidate inputs differ")
    results = {"interpreter": file_identity(Path(sys.executable)), "python": sys.version,
               "platform": platform.platform(), "started_ns": time.time_ns(),
               "adapter": file_identity(Path(__file__)), "arguments": sys.argv,
               "cwd": str(Path.cwd()),
               "input_manifest": candidate_inputs, "operations": {}}
    with tempfile.TemporaryDirectory(prefix="oni-catalog-benchmark-") as temporary:
        for function, operation, minimum_fraction, minimum_seconds in [
                ("check_catalogs", "check", 0.10, 0.0005),
                ("read_catalogs", "inspect", 0.05, 0.0001)]:
            paths = {"baseline": (args.baseline_root / TOOL_PATH).resolve(),
                     "candidate": (candidate_root / TOOL_PATH).resolve()}
            modules = {name: load_tool(path) for name, path in paths.items()}
            def invoke(module, root):
                if operation == "check":
                    return module.check_catalogs(root / "translations", "delivery_temperature_limit.pot",
                                                 root / "Source/DeliveryTemperatureLimitStrings.cs", PREFIX)
                return module.read_catalogs(root / "translations", PREFIX)
            operations = {name: lambda module=module: invoke(module, source_root)
                          for name, module in modules.items()}
            observed = {name: operation() for name, operation in operations.items()}
            if any(value != observed["baseline"] for value in observed.values()):
                raise RuntimeError(f"Operation results differ: {function}")
            warm = warm_measurements(operations)
            cli_args = {name: [str(paths[name]), operation, "--catalog-directory", str(source_root / "translations"),
                               "--template", "delivery_temperature_limit.pot", "--options-source",
                               str(source_root / "Source/DeliveryTemperatureLimitStrings.cs"), "--context-prefix", PREFIX]
                        for name in paths}
            cli = cli_measurements(cli_args, temporary)
            memory = {name: peak_allocations(action) for name, action in operations.items()}
            stress_operations = {name: lambda root, module=module: invoke(module, root)
                                 for name, module in modules.items()}
            stress = stress_memory(stress_operations, source_root, Path(temporary) / function)
            results["operations"][function] = {
                "tool_identities": {name: file_identity(path) for name, path in paths.items()},
                "warm": warm, "cli": cli, "memory": memory, "stress_memory": stress,
                "summary": summarize(warm, cli, memory, minimum_fraction, minimum_seconds)}
    if candidate_inputs != input_manifest(source_root):
        raise RuntimeError("Inputs changed during measurement")
    args.output.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({name: result["summary"] for name, result in results["operations"].items()}, indent=2))


if __name__ == "__main__":
    main()

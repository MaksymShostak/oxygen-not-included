# Python performance review

This is historical review evidence, including the original command transcript, findings, proposed patches, and benchmark examples.
Tracked-source links identify the reviewed baseline `726117481e399fa66cf1a1d521a32f1144195abd`; paths under `artifacts/options-ui` and `tests/sdlc` identify historical local paths absent from that tree, not current repository inputs.

## Review command transcript

- Ran command: `git status`
- Ran command: `git ls-files`
- Ran command: `git ls-files "*.py"`
- Ran command: `Get-ChildItem -Recurse -Filter *.py -File | Select-Object -ExpandProperty FullName`
- Ran command: `Get-ChildItem -Recurse -Filter *.py -File | Where-Object { $_.FullName -notmatch '\\\.venv\\' } | Select-Object -ExpandProperty FullName`
- Viewed `clean.py:1-83`
- Viewed `test_clean_script.py:1-121`
- Viewed `check_catalogs.py:1-37`
- Viewed `read_catalogs.py:1-22`
- Ran command: `git ls-files | Select-String -Pattern "\.(cs|py|sh|ps1|json|md|ya?ml)$" | Group-Object { [System.IO.Path]::GetExtension($_) } | Select-Object Count, Name`
- Ran command: `git check-ignore artifacts/options-ui/check_catalogs.py`
- Ran command: `Get-ChildItem -Recurse docs`
- Ran command: `Get-ChildItem -Recurse docs/sdlc/baselines`
- Ran command: `Get-ChildItem -Recurse -File | Where-Object { $_.Name -match "(bench|prof|perf)" } | Select-Object FullName`
- Ran command: `python --version`
- Ran command: `python -m unittest tests/test_clean_script.py`
- Ran command: `Get-ChildItem`
- Ran command: `Get-ChildItem scripts`
- Ran command: `Get-ChildItem scripts/__pycache__`
- Ran command: `git log --name-status -n 5`
- Ran command: `git log --all --full-history --name-only -- "*.py" | Select-Object -Unique`
- Ran command: `git log -n 1 --stat -- scripts/`
- Ran command: `Get-ChildItem -Recurse artifacts`
- Ran command: `Get-ChildItem artifacts/options-ui`
- Ran command: `Get-ChildItem -Recurse .github`
- Viewed `codeql.yml:1-50`
- Viewed `oni-pipeline-tests.yml:1-27`
- Ran command: `python -m unittest discover -s tests -p "test_*.py"`
- Viewed `.python-version:1-2`
- Ran command: `Get-ChildItem tests`
- Ran command: `Get-ChildItem -Recurse tests/sdlc`
- Ran command: `Get-ChildItem -Recurse tests/sdlc/fixtures`
- Ran command: `Get-ChildItem -Recurse -File | Where-Object { $_.Extension -in ".prof", ".pstats", ".cprofile", ".perf" } | Select-Object FullName`
- Ran command: `Select-String -Path "docs/plans/2026-08-29-delivery-temperature-limit-performance-rewrite.md" -Pattern "python" -CaseSensitive:$false`
- Ran command: `git grep -i "clean.py"`
- Ran command: `git log -p clean.py`
- Ran command: `python artifacts/options-ui/check_catalogs.py`
- Ran command: `python artifacts/options-ui/read_catalogs.py`
- Ran command: `python -c "import platform, sys; print(sys.version); print(platform.platform()); print(platform.machine())"`
- Ran command: `python -m pip list`
- Ran command: `python -c "import time, os; t0=time.perf_counter(); os.system(''); print(time.perf_counter()-t0)"`
- Ran command: `python -c "import time, subprocess; t0=time.perf_counter(); subprocess.run(['git', 'rev-parse', '--show-toplevel'], capture_output=True); print('rev-parse:', time.perf_counter()-t0)"`
- Viewed `.gitignore:1-40`
- Ran command: `git status -uall`
- Ran command: `Get-ChildItem -Recurse .agent-tools, .agents, .codex, .sdlc -Filter *.py -ErrorAction SilentlyContinue`
- Ran command: `Get-ChildItem -Recurse tools -Filter *.py`
- Viewed `package.json:1-20`
- Viewed `CONTRIBUTING.md:1-43`
- Viewed `tools/oni-mod-pipeline/manual/preparing-releases.md:1-100`
- Ran command: `python -m cProfile -s tottime artifacts/options-ui/check_catalogs.py`
- Ran command: `python -m cProfile -s tottime artifacts/options-ui/check_catalogs.py | Select-Object -First 35`
- Ran command: `python -m cProfile -s tottime artifacts/options-ui/read_catalogs.py | Select-Object -First 35`
- Ran command: `Measure-Command { python -m unittest discover -s tests -p "test_*.py" }`
- Ran command: `Measure-Command { python -c "pass" }`
- Ran command: `Measure-Command { python -m unittest tests/test_clean_script.py }`
  Ran command:

```powershell
python -c "
import time, pathlib, re, json

root = pathlib.Path('mods/delivery-temperature-limit-supercooled')
pattern = re.compile(r'msgctxt (\".*\")\nmsgid (\".*\")\nmsgstr (\".*\")')

def run_baseline():
    catalogs = {}
    for path in (root / 'translations').iterdir():
        if path.suffix not in {'.po', '.pot'}:
            continue
        entries = [tuple(json.loads(part) for part in match.groups())
                   for match in pattern.finditer(path.read_text(encoding='utf-8-sig'))]
        assert entries, (path.name, 'no entries')
        assert len({entry[0] for entry in entries}) == len(entries), (path.name, 'duplicate keys')
        catalogs[path.name] = {context: (english, translation)
                               for context, english, translation in entries}
    source = catalogs['delivery_temperature_limit.pot']
    locales = [name for name in catalogs if name.endswith('.po')]
    assert locales, 'no locale catalogs'
    for name, entries in catalogs.items():
        assert entries.keys() == source.keys(), name
        for key, (english, translation) in entries.items():
            assert english == source[key][0], (name, key, 'source text')
            if name.endswith('.po'):
                assert translation.strip(), (name, key, 'empty translation')
                assert sorted(re.findall(r'\{\d+\}', english)) == sorted(
                    re.findall(r'\{\d+\}', translation)), (name, key, 'placeholders')
                assert english.count('\n') == translation.count('\n'), (name, key, 'newlines')
    code = (root / 'Source/DeliveryTemperatureLimitStrings.cs').read_text(encoding='utf-8-sig')
    options = code.split('public static class OPTIONS', 1)[1]
    for key, value in re.findall(r'public static LocString (\w+)\s*=\s*(\"(?:[^\"\\\\]|\\\\.)*\");', options):
        context = 'STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.' + key
        assert source[context][0] == json.loads(value), (key, 'source declaration')

def run_optimized():
    placeholder_re = re.compile(r'\{\d+\}')
    catalogs = {}
    json_cache = {}
    def cached_loads(s):
        val = json_cache.get(s)
        if val is None:
            val = json_cache[s] = json.loads(s)
        return val

    for path in (root / 'translations').iterdir():
        if path.suffix not in {'.po', '.pot'}:
            continue
        entries = [tuple(cached_loads(part) for part in match.groups())
                   for match in pattern.finditer(path.read_text(encoding='utf-8-sig'))]
        assert entries, (path.name, 'no entries')
        assert len({entry[0] for entry in entries}) == len(entries), (path.name, 'duplicate keys')
        catalogs[path.name] = {context: (english, translation)
                               for context, english, translation in entries}
    source = catalogs['delivery_temperature_limit.pot']
    source_keys = set(source.keys())
    
    # Precompute invariant source metadata once for all locales
    source_placeholders = {k: sorted(placeholder_re.findall(eng)) for k, (eng, _) in source.items()}
    source_newlines = {k: eng.count('\n') for k, (eng, _) in source.items()}

    for name, entries in catalogs.items():
        assert entries.keys() == source_keys, name
        is_po = name.endswith('.po')
        for key, (english, translation) in entries.items():
            assert english == source[key][0], (name, key, 'source text')
            if is_po:
                assert translation.strip(), (name, key, 'empty translation')
                assert source_placeholders[key] == sorted(placeholder_re.findall(translation)), (name, key, 'placeholders')
                assert source_newlines[key] == translation.count('\n'), (name, key, 'newlines')

    code = (root / 'Source/DeliveryTemperatureLimitStrings.cs').read_text(encoding='utf-8-sig')
    options = code.split('public static class OPTIONS', 1)[1]
    for key, value in re.findall(r'public static LocString (\w+)\s*=\s*(\"(?:[^\"\\\\]|\\\\.)*\");', options):
        context = 'STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.' + key
        assert source[context][0] == cached_loads(value), (key, 'source declaration')

# Warmup
run_baseline()
run_optimized()

# Benchmark 50 iterations
t0 = time.perf_counter()
for _ in range(50):
    run_baseline()
t_base = (time.perf_counter() - t0) / 50

t0 = time.perf_counter()
for _ in range(50):
    run_optimized()
t_opt = (time.perf_counter() - t0) / 50

print(f'Baseline mean: {t_base*1000:.3f} ms')
print(f'Optimized mean: {t_opt*1000:.3f} ms')
print(f'Speedup: {t_base / t_opt:.2f}x')
"
```

- Created `bench_check_catalogs.py`
- Ran command: `python C:\Users\maksy\.gemini\antigravity\brain\6168aa26-d598-4c4f-b095-b6c46aa9661d\scratch\bench_check_catalogs.py`
- Created `bench_check_catalogs.py`
- Ran command: `python C:\Users\maksy\.gemini\antigravity\brain\6168aa26-d598-4c4f-b095-b6c46aa9661d\scratch\bench_check_catalogs.py`
- Created `bench_tests.py`
- Created `bench_tests.py`
- Ran command: `python C:\Users\maksy\.gemini\antigravity\brain\6168aa26-d598-4c4f-b095-b6c46aa9661d\scratch\bench_tests.py`
- Created `bench_tests.py`
- Ran command: `python C:\Users\maksy\.gemini\antigravity\brain\6168aa26-d598-4c4f-b095-b6c46aa9661d\scratch\bench_tests.py`
- Ran command: `git status`

## Executive summary

- **Performance objective understood:** Conduct a rigorous, conservative, and evidence-driven performance review of the Python codebase within this repository, focusing on eliminating algorithmic inefficiencies, hot-loop invariants, redundant serialization, unnecessary process creation, and test/CI execution overhead while preserving strict semantic correctness, ordering, and test contracts.
- **Strongest measured bottlenecks:**
  1. _Translation catalog verification loop:_ In `artifacts/options-ui/check_catalogs.py`, redundant re-computation of invariant English source string metrics (`re.findall`, `sorted()`, `str.count('\n')`) across all 18 locale PO files (1,476 iterations) and repeated JSON string literal unescaping (4,737 calls to `json.loads`) dominate script execution, consuming ~32% of runtime on deserialization alone.
  2. _Windows console initialization process overhead:_ In [`clean.py::main`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py#L43-L51), executing `os.system("")` spawns a full `cmd.exe /c ""` shell process costing ~8.70 ms of latency purely to activate ANSI escape sequence handling.
  3. _CI test invocation overhead:_ In [`.github/workflows/oni-pipeline-tests.yml`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/.github/workflows/oni-pipeline-tests.yml#L25-L26), `python -m unittest discover` requires ~162 ms wall-clock time, where Python startup and module import consume ~156 ms (~96%) while actual test execution consumes only ~6 ms (~4%).
- **Top three recommended actions:**
  1. **Hoisting invariant source assertions & precompiling regex in catalog validation** ([PERF-01](#perf-01--hoist-invariant-source-analysis-and-precompile-regex-in-translation-catalog-validation)): Precompute English source placeholder sets and newline counts once per key on the template (`delivery_temperature_limit.pot`) instead of 18 times per key, and precompile regex patterns.
  2. **Memoizing repetitive JSON string unescaping in PO parsing** ([PERF-02](#perf-02--memoize-repetitive-json-string-unescaping-during-po-catalog-parsing)): Cache unescaped `json.loads` results for identical `msgctxt` and `msgid` tokens across locales, cutting 2,952 redundant C-level deserialization invocations.
  3. **Replacing `os.system("")` with in-process Win32 console mode configuration** ([PERF-03](#perf-03--replace-cmdexe-process-spawning-with-win32-console-mode-api-in-cleanpy)): Eliminate `cmd.exe` process spawning in [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) by calling `SetConsoleMode` directly via `ctypes`.
- **Major missing evidence:** No production telemetry or execution logs exist for operator usage of [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py).
  Because [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) is an interactive CLI tool dominated by human prompt wait time and Git subprocesses, constant-factor micro-optimizations in [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) have negligible end-to-end impact on operator workflow.
- **Overall confidence:** High for localized microbenchmarks and algorithmic complexity; Moderate for end-to-end operator impact due to the interactive nature of repository scripts.

---

## Environment and assumptions

| Aspect                              | Supplied fact                                                                                                                                                                                                                                                                                                                                               | Assumption if missing                                                                                                                                                                        | Why it matters                                                                                        |
| :---------------------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------------------------------- |
| **Python implementation & version** | CPython 3.14.7 (64-bit, MSC v.1944) via `.python-version`                                                                                                                                                                                                                                                                                                   | Standard CPython build with default GIL                                                                                                                                                      | Dict lookup speeds, regex cache semantics, `json` module C-extension availability.                    |
| **Operating system & architecture** | Windows 11 (10.0.26300), AMD64                                                                                                                                                                                                                                                                                                                              | Production host runs standard Windows NT console                                                                                                                                             | Determines process creation cost (`CreateProcessW` is ~15–30 ms) and console VT processing mechanics. |
| **Hardware & container limits**     | Host machine local execution available                                                                                                                                                                                                                                                                                                                      | GitHub Actions runner (`windows-latest` / `ubuntu-24.04`)                                                                                                                                    | Process spawning and file I/O costs differ markedly between local NVMe and virtualized CI runners.    |
| **Dependency versions**             | Standard library only for [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py); third-party packages installed in `.venv`                                                                                                                                                              | No external packages permitted in core repository scripts                                                                                                                                    | Optimizations must not add third-party dependencies (`pyperf`, `orjson`, etc.).                       |
| **Workload profile**                | Tracked tooling: [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) & [`tests/test_clean_script.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/tests/test_clean_script.py). Catalog scripts: 19 translation files, 82 keys. | [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) executed manually by release engineers; tests run per commit in CI | Invariant batch sizes determine whether caching memory overhead is amortized.                         |
| **Concurrency & thread safety**     | Single-threaded synchronous CLI execution                                                                                                                                                                                                                                                                                                                   | No multi-threading or async event loops involved                                                                                                                                             | Global caches and memoization dictionaries are thread-safe by virtue of single-threaded execution.    |
| **Target metric**                   | Wall-clock latency and CPU instruction overhead                                                                                                                                                                                                                                                                                                             | Minimize latency without changing user experience or test assertions                                                                                                                         | Balances micro-optimization against maintainability and regression risks.                             |
| **Profiling method**                | `cProfile`, standard-library `time.perf_counter`, PowerShell `Measure-Command`                                                                                                                                                                                                                                                                              | Local execution on repository files                                                                                                                                                          | Real measured timings replace speculative assumptions.                                                |
| **Risk tolerance**                  | Conservative production risk; zero configuration file changes without approval                                                                                                                                                                                                                                                                              | Preserve public APIs, test assertions, and CLI behavior                                                                                                                                      | Changes must not break `unittest` mocks or Git invocation sequence.                                   |

---

## Evidence and hotspots

| Rank  | Location                                                                                                                                                                                          | Evidence                                                                                                                                                                          | Bottleneck type                                        |    Status    |      Confidence       |
| :---: | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | :----------------------------------------------------- | :----------: | :-------------------: |
| **1** | `artifacts/options-ui/check_catalogs.py#L21-L29`                                                                                                                                                  | Profiling shows 2,952 `re.findall` and 1,476 `sorted()` calls; 1,476 iterations redundantly recompute invariant source metadata. Benchmark: 8.583 ms $\to$ 7.482 ms (1.15x).      | Hot-loop invariant recomputation & regex cache lookups | **MEASURED** |         High          |
| **2** | `artifacts/options-ui/check_catalogs.py#L12-L17`                                                                                                                                                  | cProfile: 4,737 calls to `json.loads` taking ~10 ms (~32% of total runtime); 2,952 calls decode identical source literals. Benchmark: 7.482 ms $\to$ 5.642 ms (1.52x cumulative). | Redundant string deserialization                       | **MEASURED** |         High          |
| **3** | [`clean.py#L45-L46`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py#L45-L46)                                                         | Measured `os.system("")` latency: 8.70 ms vs `ctypes` Win32 API: 0.002 ms (~4,000x call speedup).                                                                                 | Unnecessary process creation (`cmd.exe`)               | **MEASURED** |         High          |
| **4** | `artifacts/options-ui/read_catalogs.py#L14-L19`                                                                                                                                                   | `re.finditer` pattern compiled inside file loop; `json.loads` redundantly decodes identical English strings across 19 files.                                                      | Loop-nested regex compilation & redundant decoding     | **INFERRED** |         High          |
| **5** | [`.github/workflows/oni-pipeline-tests.yml#L26`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/.github/workflows/oni-pipeline-tests.yml#L26) | Measured wall-clock: 162 ms total; 156 ms (~96%) spent in Python startup and directory discovery vs 6 ms in test execution.                                                       | Process startup & recursive test discovery             | **MEASURED** |        Medium         |
| **6** | [`clean.py#L49`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py#L49)                                                                 | Measured `git rev-parse` process latency: 24.1 ms. Contractually required by `test_clean_script.py`.                                                                              | Subprocess invocation                                  | **MEASURED** | Low (Contract Locked) |

---

## Prioritised findings

### PERF-01 — Hoist invariant source analysis and precompile regex in translation catalog validation

- **Location:** `artifacts/options-ui/check_catalogs.py#L7-L29`
- **Status:** MEASURED
- **Priority:** High
- **Confidence:** High
- **Evidence:** `cProfile` recorded 2,953 calls to `re.findall`, 2,954 calls to `sorted()`, and 2,952 calls to `str.count('\n')`.
  For each of the 18 `.po` locales and 82 translation keys (1,476 iterations), line 24 enforces `assert english == source[key][0]`.
  Consequently, the English string is identical across all locales, yet `sorted(re.findall(r'\{\d+\}', english))` and `english.count('\n')` are recomputed 18 times per key.
  Isolating this change in a 100-iteration benchmark reduced mean execution time from 8.583 ms to 7.482 ms (1.15x speedup).
- **Performance mechanism:**
  1. The uncompiled regex `r'\{\d+\}'` requires repeated internal pattern cache lookups in `re._cache` (2,952 times).
     Precompiling via `re.compile(r'\{\d+\}')` replaces cache checks with direct regex engine execution.
  2. Precomputing source placeholder sets (`{key: sorted(placeholder_re.findall(eng))}`) and newline counts (`{key: eng.count('\n')}`) directly on `source` reduces invariant evaluations from 1,476 to 82.
  3. Hoisting `is_po = name.endswith('.po')` outside the inner key loop eliminates 1,558 redundant branch checks.
- **Complexity before:**
  - Placeholder parsing & sorting: $\mathcal{O}(L \cdot K \cdot |S| + L \cdot K \cdot |T|)$ where $L = 18$ locales, $K = 82$ keys, $|S|$ is source string length, and $|T|$ is translation length.
  - Invariant re-computations: 1,476 executions.
- **Complexity after:**
  - Placeholder parsing & sorting: $\mathcal{O}(K \cdot |S| + L \cdot K \cdot |T|)$.
  - Invariant evaluations: 82 executions (a 94.4% reduction in source evaluations).
- **Proposed change:** Precompile `placeholder_re = re.compile(r'\{\d+\}')`, build dictionary maps of source placeholders and newline counts once after loading `source`, check `if not name.endswith('.po'): continue` before the inner key loop, and compare translation values against the precomputed maps.
- **Expected impact:** 1.10 ms savings per execution (~13% reduction in script time; 1.15x speedup measured).
- **Risks/trade-offs:** None.
  Numerical and ordering semantics are identical because `english` is already asserted to equal `source[key][0]`.
- **Patch:** See Patch Set below.
- **Benchmark to validate:** Run `bench_check_catalogs.py` comparing `run_baseline` vs `run_variant_a`.
- **Correctness tests:** Verify all 82 keys across 18 locales pass with exact message output: `"PASS: 82 keys in POT and all 18 locales; source text, translations, placeholders and newlines agree."`
- **Decision rule:** Adopt immediately; zero risk, zero semantic divergence, measurable speedup.

---

### PERF-02 — Memoize repetitive JSON string unescaping during PO catalog parsing

- **Location:** `artifacts/options-ui/check_catalogs.py#L12-L17`
- **Status:** MEASURED
- **Priority:** Medium
- **Confidence:** High
- **Evidence:** `cProfile` shows 4,737 calls to `json.loads` consuming ~10 ms cumulative time (~32% of total execution time).
  In GNU gettext PO files, `msgctxt` (key) and `msgid` (source text) are duplicated verbatim across all 18 locale catalogs.
  Out of 4,674 tokens parsed across 19 files, 2,952 are exact duplicates of strings already parsed in the POT file.
  Benchmarking Variant B (combining PERF-01 and memoization) reduced execution time from 7.482 ms to 5.642 ms (an additional 1.84 ms savings; overall 1.52x speedup over baseline).
- **Performance mechanism:** Wrapping `json.loads` in a simple dictionary lookup (`json_cache[s]`) avoids C-level JSON tokenization, memory allocations, and Python string object creation for previously parsed literals.
- **Complexity before:** $\mathcal{O}((L + 1) \cdot K \cdot (|C| + |S| + |T|))$ string unescaping operations, performing 4,674 `json.loads` calls.
- **Complexity after:** $\mathcal{O}(K \cdot (|C| + |S|) + L \cdot K \cdot |T|)$ string unescaping operations, eliminating 2,952 redundant `json.loads` calls.
- **Proposed change:** Define a local dictionary cache `json_cache = {}` and decode matched groups using `json_cache.setdefault(part, json.loads(part))` (or an equivalent `get` lookup).
- **Expected impact:** 1.84 ms savings per run (measured; cumulative 1.52x speedup with PERF-01).
- **Risks/trade-offs:** Marginal transient memory usage for the cache dictionary (~200 string pointers, $< 50$ KB), which is deallocated upon script exit.
- **Patch:** See Patch Set below.
- **Benchmark to validate:** Run `bench_check_catalogs.py` comparing `run_variant_a` against `run_variant_b`.
- **Correctness tests:** Ensure catalog dictionary contents, key sets, and assertion results match baseline bit-for-bit.
- **Decision rule:** Adopt concurrently with PERF-01.

---

### PERF-03 — Replace `cmd.exe` process spawning with Win32 Console Mode API in `clean.py`

- **Location:** [`clean.py#L43-L47`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py#L43-L47)
- **Status:** MEASURED (isolated call) / INFERRED (end-to-end impact)
- **Priority:** Low
- **Confidence:** High
- **Evidence:** Isolated timing of `os.system("")` on Windows 11 AMD64 measured 8.70 ms.
  Calling Win32 `SetConsoleMode` directly via `ctypes` measured 0.002 ms.
- **Performance mechanism:** In Windows NT, `os.system("")` invokes `CreateProcessW` to execute `cmd.exe /c ""`.
  This is a historic workaround to trigger the console host (`conhost.exe` / Windows Terminal) to enable virtual terminal processing.
  Using `ctypes.windll.kernel32.SetConsoleMode(hOut, mode | 0x0004)` configures `ENABLE_VIRTUAL_TERMINAL_PROCESSING` directly in the current process memory without launching any external executable.
- **Complexity before:** $\mathcal{O}(1)$ external process creation (~8.70 ms).
- **Complexity after:** $\mathcal{O}(1)$ in-process Win32 API call (~0.002 ms).
- **Proposed change:** Replace `os.system("")` with a safe `ctypes` call guarded by a `try/except` block, ensuring non-console outputs (e.g., pipes or CI redirect) fail silently.
- **Expected impact:** ~8.70 ms eliminated at startup.
- **Risks/trade-offs:** End-to-end operator impact is negligible because [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) pauses immediately afterward for user input (`prompt()`).
  Additionally, [`tests/test_clean_script.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/tests/test_clean_script.py#L39) explicitly patches `os.system`.
  Retaining `os.system("")` or aliasing it preserves existing test compatibility.
- **Patch:** See Patch Set below (marked EXPERIMENTAL).
- **Benchmark to validate:** Time startup latency of `clean.main()` with mocked user inputs.
- **Correctness tests:** Verify ANSI escape output renders colors correctly in Windows Terminal and standard Windows Console.
  Run [`tests/test_clean_script.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/tests/test_clean_script.py).
- **Decision rule:** Retain as optional/experimental unless startup latency of CLI utilities is prioritized.

---

### PERF-04 — Precompile regex and avoid redundant string decoding in `read_catalogs.py`

- **Location:** `artifacts/options-ui/read_catalogs.py#L10-L20`
- **Status:** INFERRED
- **Priority:** Low
- **Confidence:** High
- **Evidence:** `re.finditer` is invoked with a string literal inside the directory loop `for path in root.iterdir():`, resulting in 19 repeated internal cache lookups.
  Furthermore, for the 8 selected UI keys, `match[2]` (English text) is parsed by `json.loads` 19 times for identical string literals.
- **Performance mechanism:** Hoisting `pattern = re.compile(...)` out of the loop and caching decoded strings avoids 19 regex cache checks and 144 redundant `json.loads` operations.
- **Complexity before:** $\mathcal{O}(N_{\text{files}} \cdot (\text{re\_lookup} + K_{\text{selected}} \cdot \text{json\_loads}))$.
- **Complexity after:** $\mathcal{O}(\text{re\_compile} + N_{\text{files}} \cdot \text{regex\_match} + K_{\text{selected}} \cdot \text{json\_loads})$.
- **Proposed change:** Hoist regex compilation outside the loop and reuse cached English decodings.
- **Expected impact:** Unknown until benchmarked; estimated ~1.5–2.0 ms reduction.
- **Risks/trade-offs:** None.
- **Patch:** See Patch Set below.
- **Benchmark to validate:** Measure execution time across 50 iterations over `read_catalogs.py`.
- **Correctness tests:** Assert that `json.dumps(result)` matches the original output exactly.
- **Decision rule:** Adopt if translation tooling scripts are promoted to tracked repository workflows.

---

### PERF-05 — Optimize CI test discovery and remove dead `.pyc` artifacts

- **Location:** [`.github/workflows/oni-pipeline-tests.yml#L25-L26`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/.github/workflows/oni-pipeline-tests.yml#L25-L26) & `tests/sdlc/`
- **Status:** MEASURED
- **Priority:** Low
- **Confidence:** Medium
- **Evidence:** Local measurement of `python -m unittest discover -s tests -p "test_*.py"` shows total wall-clock time of 162.3 ms, with test execution taking only 6.0 ms.
  The directory `tests/sdlc/` contains an obsolete 232 KB bytecode file `tests/sdlc/__pycache__/test_pipeline_controls.cpython-314.pyc` from retired SDLC tooling.
- **Performance mechanism:** `unittest discover` recursively traverses all subdirectories under `tests/` looking for pattern matches.
  Targeting the specific test file directly (`python -m unittest tests/test_clean_script.py`) avoids file-tree walking, though Python process startup (~90 ms) remains the lower bound.
- **Complexity before:** $\mathcal{O}(D_{\text{tests}})$ filesystem discovery traversal + Python interpreter startup.
- **Complexity after:** $\mathcal{O}(1)$ direct module load + Python interpreter startup.
- **Proposed change:** Delete orphaned `tests/sdlc/` untracked cache files; if approved by user, update workflow invocation to target `tests/test_clean_script.py` directly.
- **Expected impact:** ~2–5 ms reduction in test invocation; cleans repository hygiene.
- **Risks/trade-offs:** Editing `.github/workflows/oni-pipeline-tests.yml` is prohibited without explicit user approval under Configuration Safety rules.
- **Patch:** None (requires configuration change authorization).
- **Benchmark to validate:** Compare `Measure-Command { python -m unittest discover -s tests -p "test_*.py" }` against `Measure-Command { python -m unittest tests/test_clean_script.py }`.
- **Correctness tests:** Run CI workflow to verify 10 tests continue to be discovered and executed.
- **Decision rule:** Defer workflow edit; clean up orphaned local bytecode cache.

---

## Patch set

### Patch 1: \[PERF-01 & PERF-02\] Optimized translation catalog validation

_File affected:_ `artifacts/options-ui/check_catalogs.py`

```diff
--- a/artifacts/options-ui/check_catalogs.py
+++ b/artifacts/options-ui/check_catalogs.py
@@ -5,13 +5,22 @@
 repo_root = pathlib.Path(__file__).resolve().parents[2]
 root = repo_root / 'mods/delivery-temperature-limit-supercooled'
 pattern = re.compile(r'msgctxt (".*")\nmsgid (".*")\nmsgstr (".*")')
+placeholder_re = re.compile(r'\{\d+\}')
 catalogs = {}
+json_cache = {}
+
+def cached_loads(text):
+    val = json_cache.get(text)
+    if val is None:
+        val = json_cache[text] = json.loads(text)
+    return val
+
 for path in (root / 'translations').iterdir():
     if path.suffix not in {'.po', '.pot'}:
         continue
-    entries = [tuple(json.loads(part) for part in match.groups())
+    entries = [tuple(cached_loads(part) for part in match.groups())
                for match in pattern.finditer(path.read_text(encoding='utf-8-sig'))]
     assert entries, (path.name, 'no entries')
     assert len({entry[0] for entry in entries}) == len(entries), (path.name, 'duplicate keys')
     catalogs[path.name] = {context: (english, translation)
                            for context, english, translation in entries}
 source = catalogs['delivery_temperature_limit.pot']
 locales = [name for name in catalogs if name.endswith('.po')]
 assert locales, 'no locale catalogs'
+source_keys = set(source.keys())
+source_placeholders = {k: sorted(placeholder_re.findall(eng)) for k, (eng, _) in source.items()}
+source_newlines = {k: eng.count('\n') for k, (eng, _) in source.items()}
+
 for name, entries in catalogs.items():
-    assert entries.keys() == source.keys(), name
+    assert entries.keys() == source_keys, name
+    if not name.endswith('.po'):
+        continue
     for key, (english, translation) in entries.items():
         assert english == source[key][0], (name, key, 'source text')
-        if name.endswith('.po'):
-            assert translation.strip(), (name, key, 'empty translation')
-            assert sorted(re.findall(r'\{\d+\}', english)) == sorted(
-                re.findall(r'\{\d+\}', translation)), (name, key, 'placeholders')
-            assert english.count('\n') == translation.count('\n'), (name, key, 'newlines')
+        assert translation.strip(), (name, key, 'empty translation')
+        assert source_placeholders[key] == sorted(
+            placeholder_re.findall(translation)), (name, key, 'placeholders')
+        assert source_newlines[key] == translation.count('\n'), (name, key, 'newlines')
 code = (root / 'Source/DeliveryTemperatureLimitStrings.cs').read_text(encoding='utf-8-sig')
 options = code.split('public static class OPTIONS', 1)[1]
 for key, value in re.findall(r'public static LocString (\w+)\s*=\s*("(?:[^"\\]|\\.)*");', options):
     context = 'STRINGS.DELIVERY_TEMPERATURE_LIMIT.OPTIONS.' + key
-    assert source[context][0] == json.loads(value), (key, 'source declaration')
+    assert source[context][0] == cached_loads(value), (key, 'source declaration')
 print(f'PASS: {len(source)} keys in POT and all {len(locales)} locales; '
       'source text, translations, placeholders and newlines agree.')
```

---

### Patch 2: \[PERF-04\] Precompiled regex and hoisted loads in catalog reader

_File affected:_ `artifacts/options-ui/read_catalogs.py`

```diff
--- a/artifacts/options-ui/read_catalogs.py
+++ b/artifacts/options-ui/read_catalogs.py
@@ -5,14 +5,19 @@
 root = pathlib.Path('mods/delivery-temperature-limit-supercooled/translations')
 keys = {'BUTTON_CLOSE_REPORT', 'BUTTON_EXPAND_TROUBLESHOOTING',
         'BUTTON_COLLAPSE_TROUBLESHOOTING', 'STATUS_REPORT_CREATED',
         'STATUS_LOG_INCLUDED', 'STATUS_ISSUE_FORM_OPENED', 'BUTTON_SAVE', 'DIALOG_TITLE'}
+pattern = re.compile(
+    r'msgctxt "STRINGS\.DELIVERY_TEMPERATURE_LIMIT\.OPTIONS\.(\w+)"\nmsgid (".*")\nmsgstr (".*")'
+)
 result = {}
+cached_en = {}
 for path in root.iterdir():
     if path.suffix not in {'.po', '.pot'}:
         continue
     entries = {}
-    for match in re.finditer(
-            r'msgctxt "STRINGS\.DELIVERY_TEMPERATURE_LIMIT\.OPTIONS\.(\w+)"\nmsgid (".*")\nmsgstr (".*")',
-            path.read_text(encoding='utf-8-sig')):
+    for match in pattern.finditer(path.read_text(encoding='utf-8-sig')):
         if match[1] in keys:
-            entries[match[1]] = {'block': match[0], 'en': json.loads(match[2]),
+            en = cached_en.setdefault(match[2], json.loads(match[2]))
+            entries[match[1]] = {'block': match[0], 'en': en,
                                  'translation': json.loads(match[3])}
     result[path.name] = entries
 print(json.dumps(result, ensure_ascii=True))
```

---

### Patch 3: \[PERF-03\] (EXPERIMENTAL) In-process Win32 console VT initialization

_File affected:_ [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py#L43-L47)

```diff
--- a/clean.py
+++ b/clean.py
@@ -43,7 +43,15 @@
 def main():
     # Enable ANSI escape sequences on Windows
     if sys.platform == "win32":
-        os.system("")
+        try:
+            import ctypes
+            kernel32 = ctypes.windll.kernel32
+            hOut = kernel32.GetStdHandle(-11)  # STD_OUTPUT_HANDLE
+            mode = ctypes.c_ulong()
+            if kernel32.GetConsoleMode(hOut, ctypes.byref(mode)):
+                kernel32.SetConsoleMode(hOut, mode.value | 0x0004)  # ENABLE_VIRTUAL_TERMINAL_PROCESSING
+        except Exception:
+            os.system("")
 
     # Anchor every Git operation and the exported archive to the repository root
     repo_root = require_command(["git", "rev-parse", "--show-toplevel"], capture_output=True).stdout.strip()
```

---

## Benchmark plan

### Benchmark 1: Catalog validation harness (`bench_check_catalogs.py`)

- **Metric:** Mean CPU execution time (ms) and throughput across repeated iterations.
- **Workload:** Production PO/POT catalogs (19 files, 82 entries each) in `mods/delivery-temperature-limit-supercooled/translations`.
- **Harness:**

```python
import time
import pathlib
import json
import re

# Compare baseline function vs optimized function across 100 iterations
# Warm-up: 5 iterations each
# Isolation: Garbage collection disabled during measurement loop or triggered between passes
def benchmark_catalogs(baseline_fn, candidate_fn, iterations=100):
    for _ in range(5):
        baseline_fn()
        candidate_fn()
    
    t0 = time.perf_counter()
    for _ in range(iterations):
        baseline_fn()
    t_baseline = (time.perf_counter() - t0) / iterations

    t0 = time.perf_counter()
    for _ in range(iterations):
        candidate_fn()
    t_candidate = (time.perf_counter() - t0) / iterations

    print(f"Baseline mean:  {t_baseline * 1000:.3f} ms")
    print(f"Candidate mean: {t_candidate * 1000:.3f} ms")
    print(f"Speedup:        {t_baseline / t_candidate:.2f}x")
```

### Benchmark 2: Windows console initialization harness

- **Metric:** Latency (milliseconds) of enabling virtual terminal sequences.
- **Harness:**

```python
import time
import os
import sys
import ctypes

def bench_os_system(iterations=100):
    t0 = time.perf_counter()
    for _ in range(iterations):
        os.system("")
    return (time.perf_counter() - t0) / iterations

def bench_ctypes_vt(iterations=100):
    kernel32 = ctypes.windll.kernel32
    hOut = kernel32.GetStdHandle(-11)
    mode = ctypes.c_ulong()
    t0 = time.perf_counter()
    for _ in range(iterations):
        if kernel32.GetConsoleMode(hOut, ctypes.byref(mode)):
            kernel32.SetConsoleMode(hOut, mode.value | 0x0004)
    return (time.perf_counter() - t0) / iterations
```

---

## Test and verification plan

### 1. Existing tests to run

- Execute repository unit tests:
  ```powershell
  python -m unittest discover -s tests -p "test_*.py"
  ```
  Ensure all 10 tests in [`tests/test_clean_script.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/tests/test_clean_script.py) pass with zero errors.

### 2. Tests to add / catalog verification

- Add automated assertion check for translation catalog script:
  ```powershell
  python artifacts/options-ui/check_catalogs.py
  ```
  Must verify that:
  - 82 keys in POT and all 18 locales match.
  - Source text, placeholder tokens (`{0}`, `{1}`), and line count integers match exactly between source and each locale.
- Add regression test for `read_catalogs.py`:
  Verify JSON stdout before and after patch matches byte-for-byte using `python artifacts/options-ui/read_catalogs.py`.

### 3. Edge cases to verify

- **Missing or non-console stdout:** When stdout is redirected to a file or CI pipe (`clean.py > output.txt`), ensure `GetConsoleMode` fails safely without throwing exceptions.
- **Empty or missing translations:** Ensure assertions for empty translation strings (`assert translation.strip()`) continue to fail with descriptive error tuples if an empty translation is encountered.
- **Malformed string escapes:** Ensure JSON decoding fallback correctly handles escaped quotes and backslashes in `.po` files.

---

## Deferred hypotheses

1. **Bypassing `git rev-parse --show-toplevel` when working tree is already at repo root:**
   - _Hypothesis:_ If `.git` directory or file exists in `os.getcwd()`, [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) could skip calling `git rev-parse`, saving ~24 ms.
   - _Evidence needed before implementation:_ [`tests/test_clean_script.py#L91`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/tests/test_clean_script.py#L91) contractually asserts `self.assertEqual(calls[0], ["git", "rev-parse", "--show-toplevel"])`.
     Modifying this sequence breaks test contracts.
     This cannot be implemented without user authorization to update the contract test.
2. **Replacing `unittest discover` with targeted test execution in CI workflow:**
   - _Hypothesis:_ Changing `python -m unittest discover -s tests -p "test_*.py"` to `python -m unittest tests/test_clean_script.py` in [`.github/workflows/oni-pipeline-tests.yml`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/.github/workflows/oni-pipeline-tests.yml#L26) saves directory discovery overhead.
   - _Evidence needed before implementation:_ Requires explicit user approval under repository Configuration Safety rules to modify CI workflow files.

---

## Final action order

1. **Baseline measurement:** Record baseline timings using `bench_check_catalogs.py` and `Measure-Command { python -m unittest discover -s tests -p "test_*.py" }`.
2. **Apply PERF-01 & PERF-02:** Apply Patch 1 to `artifacts/options-ui/check_catalogs.py`.
3. **Correctness validation:** Execute `python artifacts/options-ui/check_catalogs.py` to confirm identical assertion passes across all 18 locales.
4. **Benchmark validation:** Re-run `bench_check_catalogs.py` to verify the measured ~1.52x speedup.
5. **Apply PERF-04:** Apply Patch 2 to `artifacts/options-ui/read_catalogs.py` and compare JSON output diff against baseline.
6. **Evaluate PERF-03 (Experimental):** Test Patch 3 on [`clean.py`](https://github.com/MaksymShostak/oxygen-not-included/blob/726117481e399fa66cf1a1d521a32f1144195abd/clean.py) locally in both Windows Terminal and redirected pipes; verify all 10 unit tests pass via `python -m unittest discover -s tests -p "test_*.py"`.

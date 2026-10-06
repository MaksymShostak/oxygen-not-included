# Python tooling performance implementation plan

Date: 2026-10-05.
Status: **draft for review; implementation is not authorized by this document**.

Execution authorization recorded 2026-10-05: the user subsequently requested implementation of this plan through HISEW in the existing `main` checkout, preauthorized detailed commit messages and direct pushes, and selected bounded Antigravity `gemini-3.8-flash-high` independent reviews with Codex fallback.
That later request accepts the main scope and its preset gates; the planning-time authority statements below remain historical.
The implementation preserves unrelated files and the explicitly deferred console, CI, and bytecode work.

Originating task: create a detailed HISEW plan from [the Antigravity Python performance review](../reviews/python-performance-review-antigravity.md), resolving engineering choices through first principles, modern best practice, authoritative specifications/guidelines, and adopted community practice, in that order.
Consult the user through the grilling skill only when that sequence cannot resolve a material decision.

## 1. Purpose, baseline, and authority

The beneficiary is the maintainer validating and inspecting Delivery Temperature Limit translations.
The intended result is reproducible, measurably faster catalog tooling with the same validation meaning and useful failure diagnostics.
Faster local tooling must not reduce translation assurance, omit future Python tests, change repository cleanup behavior, or increase maintenance disproportionately to the small absolute saving.

This is a plan for repository tooling, not ONI simulation performance.
Milliseconds saved in these scripts do not imply a higher game frame rate, faster mod startup, or a faster complete release pipeline.

### 1.1 Planning basis and prerequisite status

The supplied review is evidence to investigate, not an accepted design or a correctness oracle.
No accepted Python-specific requirement baseline, risk route, QA scenarios, or implementation design was found in the inspected plan set.
Sections 3–6 supply a **draft dossier, proposed route, scenarios, and selected design recommendations** together so the missing prerequisites are visible and reviewable.
None is represented as previously approved.
Before implementation, confirm the accepted brief and actual authority in the existing task; a material revision invalidates downstream selections.

The plan-writing request authorizes this documentation.
It does not authorize implementation, configuration edits, deletion, software installation, commits, pushes, publication, or workflow-state mutation.
No configuration change is necessary to create this plan.
No engine execution, requirement snapshot, configuration proposal, or lifecycle approval has been created.

HISEW inspection found personal applicability active, installed engine `0.1.0.dev17`, and no current execution.
Configuration and evidence roots are respectively `C:\Users\maksy\.hi\w\c` and `C:\Users\maksy\.hi\w\e`.
The registered profile command lists and their coverage limitations are recorded in section 9; these inspections are not verification runs.

### 1.2 Inspected candidate identity

Repository: `C:\Users\maksy\GitHub\oxygen-not-included`; branch: `main`; HEAD: `726117481e399fa66cf1a1d521a32f1144195abd`.

At inspection, the user-owned untracked inputs were `docs/reviews/` and `docs/steam-community-bbcode-repository-extraction-plan.md`.
Both remain untouched.
Refresh this inventory before future work; it is not an exhaustive permanent authorization boundary.

| Input                                                   | SHA-256 at planning inspection                                     |
| ------------------------------------------------------- | ------------------------------------------------------------------ |
| `docs/reviews/python-performance-review-antigravity.md` | `E2DA44854AC01B1A04AF947F8FE9F5DA13DF8CD1603F97AA10908F9E8BA329F4` |
| `artifacts/options-ui/check_catalogs.py`                | `57448C040BBDEC74467FF7D15E849848A537A411A0E889761AAA38C9EBBF07AC` |
| `artifacts/options-ui/read_catalogs.py`                 | `EE0601F5858070FD54825F243820D7E31E1CAB5FD96B709CDBEBC9FBBAD07129` |
| `clean.py`                                              | `1C3844F834A0F5520B4C7D958CB1F395F28A3CDC8E03B2EB3B3FF7E3E5EFF6AB` |
| `tests/test_clean_script.py`                            | `5F3A268749269582B253E63D18E2C799F1C0DA6A2A39D7632FF6C4233A39738A` |

The ignored scripts are authoritative local inputs for planning, even though HEAD cannot reproduce them.
Preserve their exact bytes in the future implementation evidence bundle before extraction.
Do not overwrite, remove, or force-add them.

### 1.3 Current observations and review corrections

| Observation                                                                                                                                                                                                                                                                                      | Consequence for the plan                                                                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `git ls-files '*.py'` lists only `clean.py` and `tests/test_clean_script.py`. Both catalog scripts are excluded by the existing `artifacts/` ignore rule.                                                                                                                                        | Optimizing only ignored files would not deliver reproducible repository tooling. Recommend ordinary tracked destinations without an ignore-rule exception.                                                                                                 |
| The current corpus contains one POT, 18 PO files, and 82 contextual entries per catalog.                                                                                                                                                                                                         | Use this as a fingerprinted representative workload, not a permanently hardcoded production limit.                                                                                                                                                         |
| Every current contextual entry has a corresponding match under the existing extraction pattern. The reader's eight-name selection set currently returns five entries per catalog: `BUTTON_SAVE`, `DIALOG_TITLE`, `STATUS_ISSUE_FORM_OPENED`, `STATUS_LOG_INCLUDED`, and `STATUS_REPORT_CREATED`. | No unmatched contextual entries were found in this corpus. Preserve the three absent selections as absent; do not synthesize entries or treat an eight-name selection set as eight returned entries. This inventory is not general PO conformance.         |
| `python -B artifacts/options-ui/check_catalogs.py` passed with `PASS: 82 keys in POT and all 18 locales; source text, translations, placeholders and newlines agree.`                                                                                                                            | This is one current correctness observation, not independent conformance or performance acceptance.                                                                                                                                                        |
| A read-only count of the existing three-field regex matches found 4,674 token occurrences, 1,547 unique literal tokens, and 3,127 repeat occurrences. Distinct literal tokens contain 90,178 characters in total.                                                                                | The review's 2,952 count describes repeated context/source work, not all possible cache hits. Its roughly 200-pointer / less-than-50-KB cache estimate is unsupported. Measure actual peak memory. These counts exclude the later C# declaration decoding. |
| A two-call `setdefault` demonstration invoked the decoder twice for the same token.                                                                                                                                                                                                              | Reject the review's `setdefault(token, json.loads(token))` form: the default argument is eagerly evaluated.                                                                                                                                                |
| The existing placeholder expression is `\{\d+\}` and comparisons sort every occurrence.                                                                                                                                                                                                          | Preserve a multiset, including duplicate occurrences and the current Unicode-digit matching behavior. A set would weaken validation.                                                                                                                       |
| Python regex module functions cache recent compiled patterns.                                                                                                                                                                                                                                    | Describe PERF-04 as avoiding repeated module dispatch/cache lookup, not repeatedly compiling the pattern from scratch.                                                                                                                                     |
| `Path.iterdir()` does not prescribe order.                                                                                                                                                                                                                                                       | Exact output/failure ordering needs identical explicit enumeration in comparison tests; sorting is a separate behavior change.                                                                                                                             |
| `tests/sdlc/__pycache__/test_pipeline_controls.cpython-314.pyc` exists and is 232,376 bytes, with no source file found under that directory.                                                                                                                                                     | Existence does not establish discovery overhead or deletion authority. Retain it; it is not task-owned.                                                                                                                                                    |
| The current workflow retains broad `unittest discover`. Registered HISEW profiles contain `.NET` commands only.                                                                                                                                                                                  | Do not narrow CI discovery. Add Python tests under its existing pattern; label direct Python results supplemental unless their execution is explicitly governed.                                                                                           |
| Local Python and `.python-version` are `3.14.7`. Official Python `3.14.8` was released on 2026-09-30.                                                                                                                                                                                            | Do not call 3.14.7 latest. Preserve the existing pin; qualify newly promoted functionality against the current stable target separately, without silently changing configuration.                                                                          |

The review's `8.583 → 7.482 → 5.642 ms`, `1.52x`, `8.70 ms`, and test-startup measurements remain **review-reported historical measurements**.
This planning session did not reproduce them.
Profiling cumulative time, warm function latency, fresh-process latency, and operator wait time are different metrics.

## 2. Scope and finding dispositions

The proposed main scope is a small tracked home for the existing catalog checker and Options catalog reader, behavioral regression evidence, invariant hoisting, and real invocation-local decoding memoization.
Retain the existing single-line catalog extraction contract during optimization; this plan does not introduce a general gettext implementation.

| Finding                                                         | Disposition                                               | Delivery condition                                                                                                                                                         |
| --------------------------------------------------------------- | --------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| PERF-01: source invariant hoisting and placeholder regex reuse  | Main implementation candidate                             | Preserve key equality, English equality, duplicate placeholder multiplicity, newline counts, source declarations, and diagnostics; pass correctness and performance gates. |
| PERF-02: repeated literal decoding                              | Main implementation candidate with corrected cache design | Decode on actual misses only; preserve empty strings and exceptions; demonstrate bounded lifetime, latency improvement, and measured memory impact.                        |
| PERF-03: Win32 VT initialization                                | Deferred, with a bounded re-entry design                  | Reopen only if measured unattended startup or a separately accepted console-correctness need justifies native API maintenance. No `clean.py` change in the main scope.     |
| PERF-04: Options reader regex reuse and English literal caching | Conditional independent slice                             | First establish reproducible ownership/output. Retain caching only if it passes the reader's own benefit gate; PERF-01/02 success is not evidence for it.                  |
| PERF-05: narrower CI test invocation and old bytecode removal   | Reject invocation narrowing; defer local cleanup          | Preserve discovery for current and future tests. Old bytecode removal requires a separate exact ownership/removal decision and evidence of relevance.                      |
| Skip `git rev-parse`                                            | Rejected for this program                                 | Preserve Git's repository discovery across nested directories and worktrees; do not substitute a `.git` presence heuristic.                                                |

Non-goals: C# gameplay changes; edits to translations or source `LocString` values; a gettext parser migration; new runtime dependencies; CI timing gates; parallel workers; persistent caches; generalized benchmark infrastructure; changing ANSI output policy; cleanup of existing files; configuration changes; release packaging or game installation.

## 3. Proposed HISEW risk route

### Risk class:

**R1, proposed for the main catalog-tooling program.**
Planning itself remains a documentation activity.
This classification concerns the behavior eventually implemented, not the Markdown extension.

### Decision owner:

The repository owner/requesting user accepts the brief, scope, and any material changes.
The implementing agent supplies evidence and recommendations; it cannot accept its own exceptions.

### Reasoning:

Observed scope is offline, single-threaded maintainer tooling.
Failures could miss translation inconsistencies or disrupt local diagnostics, but affect neither save data nor live gameplay.
Optimization changes are detectable through independent negative fixtures and straightforward to reverse.
Millisecond improvements are not a material service-availability/performance change.
R1 is an inference from that bounded scope, not a claim that parsing is risk-free.

### Potential blast radius:

Catalog checks, selected Options inspection output, their documentation, and repository Python test discovery.
No changed game assembly, data schema, network surface, package graph, or cleanup command sequence.

### Reversibility:

Before activation, discard only task-owned experiments through targeted reversal of the agent's edits.
After authorized delivery, reverse the named optimization or restore the previous tracked implementation through an authorized corrective change.
Do not restore or overwrite unrelated working-tree content.
Keeping exact original script bytes makes comparison possible; HEAD alone is insufficient for ignored inputs.

### Principal unknowns:

Current end-to-end saving, cache peak memory, reader benefit, any consumers of the ignored paths, and availability of the selected current runtime for qualification.
Each has a discriminating experiment in section 11.
Widening syntax support, packaging tools for external consumers, native console changes, or configuration changes requires re-routing the affected scope.

### Required artifacts:

This combined draft dossier/plan/research record; an accepted task reference before implementation; a source/corpus/command identity manifest; ordinary review findings; raw correctness/performance results and their disposition.
No new issue, separate dossier hierarchy, or lifecycle record is needed solely to inflate the plan.

### Required specialist lenses:

Ordinary code review covering semantic equivalence, independent test oracles, parser limitations, names, output order, memory lifetime, and practical benefit.
Use the configured Codex ordinary reviewer under the applicable HISEW review procedure at the future frozen implementation target.
This plan does not invoke a reviewer, delegate, or waive provider discovery.
A native console extension needs Windows ABI expertise.
No security scan has been authorized or run.

### Required verification:

Focused Python correctness evidence during development; complete Python discovery and real-corpus checks at integration; the registered **affected** HISEW profile at final R1 assurance, with its actual limitations.
Timing evidence is separate.
Full is required if a changed scope is reclassified R2; it is not a substitute for missing Python coverage.

### Required human approvals:

Implementation authority and acceptance of material draft scope; exact configuration approval if later necessary; separate commit and push authority; case-specific deletion authority.
A plan's approval does not implicitly approve configuration or Git publication.
Reuse actual session decisions rather than asking for them again.

### Maximum sensible autonomy:

Now: inspect, research, and write/review this plan. After implementation is authorized: edit only the accepted ordinary-source/test/documentation scope, run safe checks, resolve routine engineering choices, and stop at material contract or permission changes.
Run inline with one agent unless separately authorized otherwise.

### Next lifecycle step:

Review the draft brief and selected recommendations.
At authorized implementation entry, refresh applicability and source identity, confirm the accepted R1 basis, and use the applicable HISEW implementation procedure.
Do not create execution state or authenticate approval by referring to this draft alone.

## 4. Draft requirements, acceptance criteria, and domain invariants

These IDs are stable within this program.
Approval must refer to the actual accepted revision.
Proposed numerical thresholds are engineering recommendations, not pre-existing user requirements or specification constants.

| Requirement                                                                    | Acceptance criterion                                                                                                                                                                                                                                                    | Proof obligation                                                                              |
| ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| REQ-001: preserve checker meaning on its supported input subset                | AC-001: same valid-corpus success text/counts, nonzero failures, duplicate-context checks, source keys, locale keys, English values, nonempty translations, placeholder multiplicity, newline counts, and C# Options declaration comparisons                            | Reviewed literals and negative fixtures plus baseline/candidate differential execution        |
| REQ-002: remove repeated source analysis without weakening validation          | AC-002: derive source placeholder/newline metadata once per source key per invocation; still compare each locale's English text to that key's source before relying on the metadata                                                                                     | Structural review and untimed mechanism diagnostic; AC-001 remains mandatory                  |
| REQ-003: memoize decoding correctly and locally                                | AC-003: repeated literal tokens do not re-enter `json.loads`; empty decoded strings hit the cache; malformed literals still fail; a new invocation observes changed input and retains no prior cache                                                                    | Miss/hit/invalid-input/fresh-invocation fixtures; separate peak-memory measurements           |
| REQ-004: demonstrate useful improvement rather than reproduce a headline ratio | AC-004: proposed checker gate is at least 10% and 0.5 ms median warm-workload reduction in each of three independent sessions; no median fresh-process regression exceeding max(5%, 2 ms); measured peak-memory increase no greater than 1 MiB on the fixed real corpus | Section 8 protocol; failures or unstable data cause simplify/defer/replan, never a false pass |
| REQ-005: preserve reader content and rendering contract                        | AC-005: identical selected keys, `block`, `en`, `translation`, `ensure_ascii=True`, and trailing newline for identical ordered inputs; no unexpected stdout at import; same failures on supported malformed literals                                                    | Literal synthetic golden output plus current-corpus differential comparison                   |
| REQ-006: make accepted tooling reproducible from the repository                | AC-006: tracked entrypoints/tests work in a clean checkout with the existing standard library; historical artifacts are not required; imports do not execute CLI actions; paths are documented                                                                          | Clean-checkout execution without `artifacts/options-ui`; no ignore/config edits               |
| REQ-007: retain broad verification and separate coverage claims                | AC-007: existing CI discovery command unchanged; new top-level `test_*.py` cases discovered; Python, `.NET`, performance, and external-consumer evidence distinctly labeled                                                                                             | Test discovery inventory, configuration diff, final evidence index                            |
| REQ-008: preserve cleanup safety and user assets                               | AC-008: `clean.py`, Git discovery/command order, prompts, exit statuses, existing ignored scripts, and old bytecode unchanged in the main program                                                                                                                       | Exact diffs/digests and existing clean-script regression tests using mocks                    |
| REQ-009: optimize the reader only on its own evidence                          | AC-009: proposed reader cache gate is at least 5% and 0.1 ms median warm reduction in each session, with the same fresh-process regression and memory ceilings as AC-004; otherwise retain the simpler behavior                                                         | Baseline/compiled-only/cached comparisons under section 8                                     |

Threshold rationale: the checker review suggests about 34% warm savings, so 10%/0.5 ms demands a repeatable useful fraction without enshrining 1.52x. The smaller reader has no reliable measured benefit, so 5%/0.1 ms is a screening threshold.
The fresh-process tolerance prevents warm improvements from masking startup regressions.
The 1 MiB budget is a generous draft guardrail for a small CLI dataset, not a claim about actual usage.
These bounds must be accepted or adjusted before measurements; do not tune them after seeing the candidate.

Invariants:

1. POT defines the source key universe; locales must have exactly that universe.
   Missing/extra keys are errors, not silently ignored entries.
2. Duplicate contexts must be detected before dictionary construction collapses them.
   Do not infer uniqueness from dictionary size alone.
3. Every locale source string equals the corresponding POT source string.
   Hoisted metadata never replaces that comparison.
4. Placeholder order may change in translations; multiplicity may not.
   Preserve the existing regex, including its escape/Unicode behavior, rather than interpreting a new format grammar.
5. Decoded newline count is the contract; physical PO line breaks and escaped literal `\\n` are different values.
6. Empty POT translations are allowed.
   Whitespace-only locale translations are not.
   Empty decoded strings are legitimate cache values.
7. UTF-8 BOM handling, universal newline reading, raw reader `block` content, and JSON output settings retain their existing meanings.
8. Input enumeration, validation phases, and error categories remain stable for an identical explicit sequence.
   Full traceback line numbers are not a public contract; first reported catalog/key/reason is.
9. A cache lives within one operation and is keyed by the original literal token.
   It caches successful immutable strings only, not parse errors, paths, whole files, or values across invocations.
10. The extraction regex is not a complete PO parser.
    General multiline entries, plural forms, fuzzy/obsolete entries, and full C#/gettext format-string semantics are outside its established coverage.
    A green result must not be described as full gettext or ONI runtime conformance.
11. Standard execution without `-O` is the supported assertion-based check.
    This optimization does not redesign assertion disabling under optimized Python.

## 5. Ordered research and decision record

Research checked on 2026-10-05.
Binding repository authority and language/format contracts constrain every stage.
The following decisions are selected **draft recommendations**; research resolves the technical recommendation without pretending to supply user approval.

### DEC-001 — Reproducible destinations, not ignored-file delivery

1. **First principles:** a performance change that disappears in a clean checkout has no reproducible delivery boundary.
2. **Modern best practice:** separate durable executable tooling and tests from generated artifacts; preserve user-owned originals and bind experiments to exact inputs.
3. **Authoritative evidence:** repository `.gitignore` excludes `artifacts/`; existing CI discovers top-level Python tests.
   No ignore exception is needed to create ordinary files under `tools/`.
4. **Adopted practice:** the repository already keeps maintained executable tooling under `tools/oni-mod-pipeline`, with generated material in ignored build/artifact directories.

**Selection:** introduce `tools/translation-catalogs/check_catalogs.py` and `read_option_catalogs.py`, with tests in `tests/test_translation_catalogs.py`.
Preserve the ignored originals as historical inputs.
Do not add wrappers, aliases, or run both copies as maintained implementations.
Redirect documented use once the tracked tools qualify.
Investigate any actual consumer before switching it; an undiscoverable external consumer remains an explicit limit.

### DEC-002 — Preserve the extraction contract; separate a general-parser migration

1. **First principles:** optimize only work whose meaning is established.
   Changing parsing rules and timing simultaneously obscures the oracle and can alter accepted/rejected input.
2. **Modern best practice:** prefer maintained native parsers when broadening a format boundary.
   Do not grow the existing regex into a shadow gettext grammar.
3. **Authoritative evidence:** GNU's [PO format manual](https://www.gnu.org/software/gettext/manual/gettext.html#PO-Files) supports constructs beyond this regex.
   [Python `gettext`](https://docs.python.org/3.14/library/gettext.html) reads compiled MO catalogs; it is not a general PO reader.
   `DeliveryTemperatureLimitMod.OnLoad` registers Klei localization and PLib, but the inspected repository has no safe standalone consumer validation entrypoint for these Python checks.
4. **Adopted practice:** [polib](https://polib.readthedocs.io/en/latest/api.html) and [Babel's PO API](https://babel.pocoo.org/en/latest/api/messages/pofile.html) supply maintained catalog models; the translator guide already uses CAT tools.
   Neither replaces repository-specific source-declaration, newline, or placeholder obligations by itself.

**Selection:** reuse the current extraction behavior and standard-library decoder in this optimization.
Document its limited assurance and preserve literal raw blocks.
No new PO grammar is implemented.
If unsupported syntax is a current accepted requirement, stop this slice and select a consumer/native parser migration first; polib is the smaller first candidate, with explicit raw-block preservation and duplicate-diagnostic qualification.
Broader support cannot be silently deferred while claiming complete validation.
The reason for deferral is semantic migration and output fidelity, not mere package-install inconvenience.

### DEC-003 — Hoist source metadata and preserve multisets

1. **First principles:** after exact English equality is checked, source placeholders and newline counts are invariant across locales.
2. **Modern best practice:** compute invariant values once, leave translation-specific checks in the locale loop, and keep independently specified expected results.
3. **Authoritative evidence:** [Python regex documentation](https://docs.python.org/3.14/library/re.html#re.compile) supports reusable compiled objects and documents module caching.
   Existing code sorts occurrences; set conversion would change its semantics.
4. **Adopted practice:** the existing catalog script already compiles its extraction regex once.
   Apply that local idiom to placeholders without introducing a framework.

**Selection:** one source metadata pass and one reusable placeholder expression.
Preserve the English comparison and all failure categories.
The POT self-comparisons are redundant only after its duplicate/nonempty-entry/key/source-declaration obligations remain proven.
Use clear names such as `source_placeholders` and `source_newline_counts`; no ambiguous generic metrics object is necessary.

### DEC-004 — Decode on misses, with operation-owned lifetime

1. **First principles:** avoid repeated pure decoding only if the cached result is valid for the current operation.
   Account for the additional retained tokens and dictionary entries.
2. **Modern best practice:** explicit cache hit/miss behavior; fresh state per operation; measure memory separately; avoid mutable or persistent shared caches for short scripts.
3. **Authoritative evidence:** [Python call semantics](https://docs.python.org/3.14/reference/expressions.html#calls) evaluate argument expressions before entering `setdefault`.
   [JSON decoding](https://docs.python.org/3.14/library/json.html#json.loads) supplies the actual escaping/error behavior.
4. **Adopted practice:** standard-library dictionary lookup suffices; no faster JSON package or global memoization service is required for repeated strings in this corpus.

**Selection:** an explicit membership/miss path (or a unique sentinel) wrapping `json.loads`; never `setdefault(..., json.loads(...))` or truthiness as a miss test. Checker scope may include all matched literals and declaration literals within the same operation; reader scope starts with repeated English literals. Measure before selecting a wider reader cache. Do not add persistent caching, concurrency, or an eviction policy without evidence of a larger problem. If memory fails its gate, compare source/context-only caching before accepting a broader design.

### DEC-005 — Separate diagnostic attribution from performance proof

1. **First principles:** users pay for complete work, while mechanism attribution needs smaller boundaries.
   A cached loop run is not process startup; profiler overhead distorts timing.
2. **Modern best practice:** fixed inputs, distinct warm and fresh-process measurements, repeated independent processes, balanced baseline/candidate ordering, raw results, and separate memory runs.
3. **Authoritative evidence:** [Python profiling guidance](https://docs.python.org/3.14/library/profile.html) distinguishes profiling from benchmarking.
   [timeit](https://docs.python.org/3.14/library/timeit.html) disables GC by default, so enable it deliberately for the representative allocation workload.
   [pyperf](https://pyperf.readthedocs.io/en/latest/run_benchmark.html) documents worker isolation, warmups, calibration, and instability.
4. **Adopted practice:** [the Python performance suite](https://github.com/python/pyperformance) uses maintained benchmark tooling.
   Use pyperf rather than inventing a statistical framework if formal statistical claims become necessary.

**Selection:** bounded standard-library composition for this small program: `timeit`, `perf_counter_ns`, `subprocess`, `statistics`, and separate `tracemalloc`.
It records descriptive measurements and preset engineering gates, not a significance test.
No custom p-value/confidence-interval engine. Optional pyperf adoption requires a separate accepted tool setup and version/license qualification; none occurs during planning.

### DEC-006 — Preserve enumeration-dependent output and errors

1. **First principles:** an optimization must not silently change observable bytes or the first diagnostic when several failures exist.
2. **Modern best practice:** use an explicit order in differential tests and capture full outputs before adopting deterministic ordering as a separate improvement.
3. **Authoritative evidence:** [Path.iterdir](https://docs.python.org/3.14/library/pathlib.html#pathlib.Path.iterdir) yields children in arbitrary order.
   [JSON serialization](https://docs.python.org/3.14/library/json.html) preserves container order unless ordering behavior is explicitly changed.
4. **Adopted practice:** existing scripts preserve enumeration/insertion order and emit one compact JSON object.
   No current sort convention is imposed on that output.

**Selection:** do not sort catalogs/keys or canonicalize CLI JSON in this program.
Compare exact bytes with identical enumeration; compare decoded mappings independently across differing enumeration.
A future deterministic output contract needs its own explicit behavior decision.
Anchor promoted entrypoints to repository paths derived from their own file location; this deliberately adds invocation-from-other-directory support while preserving results for the original repository-root invocation.

### DEC-007 — Retain discovery and avoid unjustified cleanup

1. **First principles:** saving a few milliseconds cannot justify failing to discover future tests.
   A bytecode file's presence does not prove it is visited or imported.
2. **Modern best practice:** use filtered tests for local iteration and comprehensive discovery for integration; delete only material with established ownership and retention criteria.
3. **Authoritative evidence:** [Python 3.14 test discovery](https://docs.python.org/3.14/library/unittest.html#test-discovery) does not simply recurse into every unrelated directory; subdirectories without `__init__.py` are not searched for tests.
   Repository configuration and working-tree rules prohibit unapproved workflow edits or discarding existing files.
4. **Adopted practice:** the current GitHub workflow already uses the maintainable `test_*.py` discovery convention, which can include new top-level regression tests unchanged.

**Selection:** retain the CI command and old bytecode.
No PERF-05 speed claim.
Direct-file tests are a developer convenience only.
Any cleanup follows exact path approval and recoverable retention; it is not a prerequisite for PERF-01/02.

### DEC-008 — Defer native console work; keep a correct re-entry route

1. **First principles:** replacing process creation can help startup, but the interactive cleanup utility is dominated by user wait and Git operations.
   Adding FFI can cause handle/ABI problems without improving that outcome materially.
2. **Modern best practice:** guard native calls, use correct signatures, check return values, test redirected output, and isolate side effects from destructive workflow logic.
3. **Authoritative evidence:** Microsoft's [GetStdHandle](https://learn.microsoft.com/en-us/windows/console/getstdhandle), [GetConsoleMode](https://learn.microsoft.com/en-us/windows/console/getconsolemode), and [SetConsoleMode](https://learn.microsoft.com/en-us/windows/console/setconsolemode) define HANDLE/DWORD/BOOL contracts.
   [ctypes](https://docs.python.org/3.14/library/ctypes.html) requires explicit prototypes for reliable native interfaces.
   GetStdHandle can return null/invalid or redirected handles; SetConsoleMode failure need not raise a Python exception.
4. **Adopted practice:** Microsoft's supported console APIs provide the platform capability; a shell side-effect workaround is not an API contract.
   But the existing code's behavior is established, and no measured operator need currently overrides its maintenance advantage.

**Selection:** defer PERF-03.
If reopened, use explicit pointer-width HANDLE and BOOL/DWORD signatures, check both GetConsoleMode and SetConsoleMode, retain existing bits and enable the documented processed-output/VT flags as required.
Missing console is a safe no-op; do not fall back to launching `cmd.exe` merely because an API call fails.
Preserve prompts, escape bytes, Git sequence, statuses, and non-Windows behavior.
Changing whether redirected output contains colors is a separate decision.
Do not preserve a fake `os.system` call for mock compatibility.

### DEC-009 — Current runtime selection without configuration drift

1. **First principles:** a result needs the exact interpreter identity; a patch-version change can affect both correctness and timing.
2. **Modern best practice:** compare baseline and candidate under the same runtime, and identify current target validation separately from historical measurements.
3. **Authoritative evidence:** [Python's 3.14.8 release page](https://www.python.org/downloads/release/python-3148/) identifies the current maintenance target; local inspection identifies 3.14.7.
   Existing `.python-version` is configuration and cannot be edited without exact approval.
4. **Adopted practice:** this repository already pins its toolchain.
   Keep that pin's identity truthful rather than silently running a different interpreter and calling it the same environment.

**Selection:** retain 3.14.7 as the current-repository reference and select 3.14.8 for newly promoted-tool qualification.
Run any baseline/candidate pair on one exact interpreter; do not compare across versions.
Target qualification is currently unperformed, and availability is an integration question.
Use a separately authorized, explicitly addressed interpreter when available; no installer or pin edit is implied.
Refresh the latest stable target at implementation entry and rebaseline if it changes.

### 5.1 Reuse/version/rights assessment

The consuming repository license is MIT, inspected in `LICENSE`.
No external component is being installed, copied, bundled, or redistributed by this plan.
Technical fit, published license text, and actual adoption clearance are distinct.

| Candidate and checked identity                                                               | Fit and material limitations                                                                                                                                                                                               | Rights/integration disposition                                                                                                                                                                                                                                           |
| -------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Existing Python standard library; local 3.14.7, selected current qualification target 3.14.8 | `json`, `re`, `pathlib`, `unittest`, timing and memory APIs meet the optimization need. `gettext` does not parse PO input.                                                                                                 | Existing-runtime reuse; [PSF license](https://docs.python.org/3/license.html) inspected. No vendored runtime/code or new package. 3.14.8 execution remains unverified.                                                                                                   |
| [pyperf 2.10.0](https://pypi.org/project/pyperf/)                                            | Maintained calibration, workers, metadata, analysis, and comparison; preferable if significance/large campaign requirements arise. Larger setup than needed for descriptive screening.                                     | [Exact-tag MIT license](https://raw.githubusercontent.com/psf/pyperf/2.10.0/COPYING) inspected. Not adopted; dependency/environment/Windows behavior and installation authority not qualified. No system tuning or realtime-priority change authorized.                  |
| [polib 1.2.0](https://pypi.org/project/polib/)                                               | Compact general PO/POT parser with context/duplicate handling; does not by itself preserve reader raw block slices or repository checks. Documentation banner still shows 1.1.1; registry resolves the release identity.   | [Exact-tag MIT license](https://raw.githubusercontent.com/izimobil/polib/1.2.0/LICENSE) inspected. Candidate for a separately accepted parser migration, not embedded/copied here.                                                                                       |
| [Babel 2.18.0](https://pypi.org/project/babel/)                                              | `read_po` and Catalog API; broader locale functionality unnecessary for this optimization. Raw-block fidelity and repository invariants still need proof. Documentation banner showing 2.17.0 is not the selected release. | [2.18.0 license](https://raw.githubusercontent.com/python-babel/babel/v2.18.0/LICENSE) inspected: redistribution conditions and notice/nonendorsement obligations. No adoption or compatibility approval claimed.                                                        |
| GNU gettext `msgfmt`; consulted manual identifies version 1.0                                | Authoritative PO/format validation can supplement a parser migration. Its declared format checks do not automatically enforce this mod's current numbered-placeholder/newline/C# declaration rules.                        | [Manual](https://www.gnu.org/software/gettext/manual/gettext.html#msgfmt-Invocation) reviewed; selected binary version, exact distribution license, Windows provisioning, and notices are not qualified. No adoption recommendation requiring execution in this program. |
| Existing Klei localization / PLib registration                                               | Actual game consumer; decisive for runtime localization acceptance, but not a safe standalone Python validator in the inspected entrypoints.                                                                               | Retain existing integration. Do not invoke or install the game as a pretend parser dry run; runtime conformance remains outside this optimization's claim.                                                                                                               |

The residual custom work is arranging existing decoding, source comparisons, and raw-block extraction into importable operation boundaries, plus workload adapters and independent fixtures.
Do not build a new serializer, PO grammar, statistical library, native-wrapper framework, or dependency resolver.
If the native-consumer gap becomes an accepted requirement, reopen DEC-002 before extending custom parsing.

## 6. Selected architecture and predicted file map

The following paths and seams are predictions, not authorization to edit them now.

| Predicted file                                                       | Responsibility                                                                        | Constraint                                                                                                                       |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `tools/translation-catalogs/check_catalogs.py`                       | One import-safe catalog consistency operation and CLI entrypoint                      | Preserve limited extraction and all existing validation obligations; cache per operation; file-based root anchoring              |
| `tools/translation-catalogs/read_option_catalogs.py`                 | One import-safe projection of the existing eight Options keys and JSON CLI entrypoint | Preserve raw block/content/rendering; reader-specific optimization evidence                                                      |
| `tools/translation-catalogs/benchmark_catalogs.py`                   | Narrow adapter for frozen baseline/candidate workloads and raw timing records         | Compose standard APIs; no production import, generic benchmark service, or timing assertions in unit tests                       |
| `tests/test_translation_catalogs.py`                                 | Independently specified fixtures and real-corpus regression coverage                  | Top-level discovery; import tool files through standard `importlib` path loading if needed; no package/test configuration change |
| `mods/delivery-temperature-limit-supercooled/translator-handbook.md` | Document qualified commands and their assurance limits                                | No claim of full gettext/game validation or measured speed without evidence                                                      |

Expose small domain operations accepting catalog/source paths so tests can exercise real temporary fixtures without monkeypatching all internal logic. CLI code supplies repository defaults, renders one result, and runs only under the usual main guard. Prefer plain returned mappings/counts and existing exceptions to a framework, plugin registry, abstract parser hierarchy, or new public SDK.

Do not create a shared cache module just to remove a few repeated lines from two independently usable scripts.
If genuinely shared parsing responsibilities emerge, review that semantic coupling before introducing a common module; both callers and their oracles change together.
Names must describe catalog consistency, selected Options entries, decoded literals, source placeholders, and units precisely.
NAM-01 is not waived by existing abbreviated names.

No compatibility alias or no-shim override has been accepted.
Existing ignored paths are preserved user material, not maintained fallback implementations.
If a real consumer cannot migrate, identify its exact contract and request the specific exception instead of quietly adding wrappers.

## 7. Vertical implementation slices and dependencies

Each slice completes an observable path with tests/evidence.
The checklist remains unexecuted.
Main integration chain is `SLICE-001 → SLICE-002 → SLICE-003 → SLICE-005`; the reader branch is `SLICE-001 → SLICE-004 → SLICE-005`.
If SLICE-004 does not pass its own benefit gate, SLICE-005 records it as deferred and can deliver the checker independently.

### SLICE-001 — Reproduce the current catalog workflow from tracked tools

- [ ] Refresh ignored inputs, corpus, source declarations, active instructions, route, and current runtime target; preserve exact comparison bytes outside generated product output.
- [ ] Characterize valid output and negative behavior before changing the algorithm.
      Supply hand-reviewed fixtures for keys, text, multiplicity, newline counts, escapes, and duplicates.
- [ ] Introduce tracked, import-safe checker and Options reader boundaries under the predicted tool paths.
      Preserve current algorithms and output for repository-root use; prove the deliberately added file-root anchoring from another working directory.
- [ ] Add top-level discoverable tests and document the new command destinations.
      Inspect repository references to ignored paths; migrate only actual accepted consumers.
      Preserve originals untouched.
- [ ] Demonstrate a clean checkout can execute the tools without historical artifacts or extra packages.
      Record parser/output limitations.

**Demonstration:** a maintainer invokes the tracked checker on current catalogs and gets the current success message; a deliberately wrong placeholder fails.
The reader emits the reviewed fixture output.
Importing either tool emits nothing and performs no file/CLI work.

**Proof:** AC-001, AC-005–008; the tracked baseline must match frozen originals before optimization.
Separate extraction/refactoring impact from later performance impact.

**Release/recovery:** useful independently as reproducible tooling after accepted qualification.
Reverse only this introduction and its new documentation if equivalence fails; do not delete original artifacts.
A newly exposed root-anchoring failure is fixed here, not hidden in an optimization result.

### SLICE-002 — Reduce repeated source analysis in a complete checker run

- [ ] Establish an untouched tracked baseline from SLICE-001 and record its workload identity.
- [ ] Precompute source placeholder multisets and newline counts once per key; reuse the placeholder expression.
- [ ] Retain source-key and locale-key equality, each locale's English equality, nonempty translation, declaration comparisons, and existing diagnostic order.
      Remove only proven-redundant POT self-comparisons.
- [ ] Run negative and real-corpus checks, then baseline/hoisted timing comparison without profiling.
- [ ] Keep the change only if correctness holds and its contribution is repeatable; record if its individual timing is inconclusive even when the later combined candidate qualifies.

**Demonstration:** a valid corpus still passes; repeated source analysis decreases while a duplicated/missing placeholder still fails for the right key.

**Proof:** AC-001–002 and contribution to AC-004; test source metadata's values independently.
An untimed profile/count may confirm the mechanism, but counts are diagnostic evidence rather than brittle CI requirements.

**Release/recovery:** independently reviewable checker-only change.
Reverse the metadata optimization if it complicates diagnostics or provides no benefit; later caching does not justify a correctness defect here.

### SLICE-003 — Avoid repeated decoding across one checker operation

- [ ] Add miss-only literal decoding with invocation-owned state, including explicit coverage for empty strings.
- [ ] Preserve `json.loads` escaping/errors and source-declaration comparisons; clear ownership naturally on operation return/error without a persistent module cache.
- [ ] Exercise two operations with changed content in one process, proving the second result is fresh.
      Exercise malformed literals after a prior successful decode.
- [ ] Compare baseline, hoisted-only, and combined candidates; measure memory in separate runs.
      Record actual distinct-token counts instead of the review's estimate.
- [ ] If all-literal caching misses the memory gate, measure context/source-only caching.
      Accept a simpler qualifying candidate or defer; do not hide a loss behind combined speedup.

**Demonstration:** repeated literal values decode once per operation while new input is decoded on the next operation; a invalid escape still fails; the complete checker passes its preset benefit and memory gates.

**Proof:** AC-001–004, AC-006–008.
Independent fixture values are the correctness oracle; decoder call counts only establish memoization's mechanism.

**Release/recovery:** operation-local cache requires no invalidation or migration.
On abort, memory becomes reclaimable with the operation; process shutdown is not the only intended release boundary.
Reverse caching independently of invariant hoisting if it does not qualify.

### SLICE-004 — Qualify and optionally accelerate the Options catalog reader

- [ ] Start from the reader baseline established in SLICE-001.
      Compare compiled-pattern-only and miss-only English-decoding variants independently.
- [ ] Preserve the eight-name selection set and actual matching entries (currently five per catalog), raw matched blocks, escaped/non-ASCII serialization, missing-selected-key behavior, output order for identical enumeration, and malformed-literal behavior.
- [ ] Use invocation-local English caching with the same empty-string/miss rules.
      Do not adopt the review's eager `setdefault` patch.
- [ ] Run current-corpus and literal golden-output tests; report semantic equality separately when filesystem order differs.
- [ ] Retain the smallest reader change that passes AC-009.
      If caching does not qualify, retain the straightforward reader and document PERF-04 as deferred.
      Do not invent an expected 1.5–2 ms saving.

**Demonstration:** the output's `block`, `en`, and `translation` bytes match for fixed ordered inputs and the selected implementation passes its own measurements.

**Proof:** AC-003, AC-005–009; checker benchmark results cannot satisfy reader acceptance.

**Release/recovery:** independent of checker optimization once extraction is settled.
Reverse the reader-specific cache without changing checker state.
No deterministic-output migration is bundled into this slice.

### SLICE-005 — Freeze evidence and hand off qualified tooling

- [ ] Finish source/documentation changes and ordinary review corrections before final expensive assurance; bind all inputs, tests, commands, corpus, and baseline/candidate bytes.
- [ ] Run complete Python discovery, real-corpus CLI checks, import/root-anchoring regressions, and the registered R1 affected profile with its coverage warning intact.
- [ ] Repeat affected evidence only after a relevant change.
      Run the final bounded performance and memory protocol on the same frozen tooling/input identity.
- [ ] Publish an evidence index in the existing external evidence store, clearly classifying engine receipts versus supplemental Python/performance/operator artifacts.
      Record failed/inconclusive attempts too.
- [ ] Accept or defer each optimization by its own gate, record surviving historical inputs, and remove only task-owned spent fixtures/experiment files after consumers finish.
- [ ] Report implementation acceptance and measured benefits separately from authorized Git delivery.
      Commit/push occur only with their own explicit authority and required skills.

**Demonstration:** a clean checkout reproduces the qualified checker/reader behavior with no historical artifact dependency; an evidence index explains what passed and what remains outside assurance.

**Release/recovery:** maintainer-tool adoption only.
There is no game install, Workshop publication, CI reconfiguration, or mod release.
Revert one failing optimization or forward-fix its defect through a reviewed corrective change; retain counterevidence and unaffected work.

### 7.1 Integration and work coupling

The current implementation task owns integration across tool paths, documentation, and tests.
That is a technical ownership boundary, not a staffing plan.
Use one agent inline; neither this document nor skill discovery authorizes delegation.

Untimed correctness checks for the checker and reader are semantically independent after SLICE-001, but shared fixtures/import boundaries must settle first. Do not run benchmarks concurrently with builds/tests or each other.
Decoder and source-metadata changes couple in the same checker loop; integrate them sequentially and compare individual variants.
Separate Git commits are optional delivery checkpoints only after explicit commit authority, not a prerequisite invented by this plan.

## 8. Falsifiable quality scenarios, oracles, and measurements

### 8.1 QA scenarios

| Scenario                            | Stimulus/environment                                                                                                                                       | Required response and measure                                                                                                                                         |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| QA-001: valid multilingual corpus   | Fixed one-POT/18-PO corpus, normal Python, identical explicit catalog order                                                                                | Same counts/text and success status; reader values identical; byte comparison when order is controlled                                                                |
| QA-002: rejected inconsistency      | One fixture mutation: duplicate context, missing/extra key, wrong English, empty/whitespace translation, missing/repeated placeholder, or newline mismatch | Nonzero checker result identifying the expected catalog/key/reason; no success message                                                                                |
| QA-003: escaping and representation | BOM/no-BOM, LF/CRLF, non-ASCII, escaped quotes/backslashes/newlines, literal backslash-n, empty English/POT translation, malformed JSON escape             | Reviewed decoded values and existing exception category; preserve exact reader raw block and JSON escaping for fixed enumeration                                      |
| QA-004: cache isolation             | Repeated tokens, all-unique tokens, then a second operation with modified input in the same process                                                        | Repeated successful literals decoded once per operation; unique literals still decoded; later data visible; no persistent cache growth                                |
| QA-005: observable checker benefit  | Frozen real corpus and baseline/hoisted/cached variants under one interpreter                                                                              | AC-004 gates, separate fresh-process/warm/memory records, no timing assertion in CI                                                                                   |
| QA-006: reader benefit              | Existing eight-name selection, currently five matching entries per catalog; baseline/compiled-only/cached variants                                         | AC-009 gates; absent keys stay absent; inconclusive results cause defer/simplify rather than fabricated saving                                                        |
| QA-007: reproducibility             | Clean checkout without artifacts; imports and CLI from a different cwd                                                                                     | No historical artifact dependency, no import side effects, same expected results                                                                                      |
| QA-008: verification preservation   | Existing CI command and all top-level Python tests                                                                                                         | All expected tests discovered; no workflow change; recorded Python/`.NET` coverage distinction                                                                        |
| QA-009: cleanup invariants          | Existing `clean` tests with Git/input/directory effects mocked                                                                                             | Existing prompt/status/order/failure behavior unchanged; no real destructive command during tests or benchmarks                                                       |
| QA-010: optional console re-entry   | Only after separate scope acceptance: Windows Terminal/conhost, redirection, invalid/null handles, API failure, non-Windows                                | Safe no-op or documented successful VT enablement, no shell launch, ABI-correct calls, unchanged Git/input behavior; native console evidence distinct from unit mocks |

### 8.2 Correctness oracle and boundary ownership

The tests own hand-written expected strings, occurrence lists, integer newline counts, key universes, and exact JSON fixture bytes.
Do not calculate expected placeholders with the candidate's regex/helper or construct expected decoded values through its cache.
Include `{0} {0} {1}` versus `{0} {1}` so a set-based regression cannot pass.
Include reordered equal multiplicity, escaped braces under the existing semantics, Unicode-digit tokens, and source mismatches that must fail before translation metadata is trusted.

Keep the frozen pre-optimization script as a **differential baseline**, not the sole oracle.
It can share undiscovered bugs and omit multiline entries.
Tests explicitly record that limitation; unsupported syntax triggers design reconsideration when it becomes required, not an expanded correctness claim.
Preserve C# declaration checks independently with a reviewed source fixture; do not rewrite declaration extraction into a new C# parser during a performance change.

Use genuine temporary catalog/source files for parser, encoding, and filesystem behavior. A controlled path iterable is appropriate only for ordering-specific comparisons. Mock decoder calls only in a narrow cache-mechanism test. Mock `clean.py` subprocess/input/directory effects because its actual workflow is destructive; never run `clean.py` end to end as a timing harness. Optional native-console unit tests mock the Win32 boundary and verify call signatures/return handling, with separate actual console evidence if that branch is authorized.

Negative cases must test process exit/output as well as function-level exceptions. No success/error oracle is derived from the optimized implementation.
No disabled tests, relaxed placeholders, replaced source fixture expectations, or removed cleanup contracts count as remediation.

### 8.3 Benchmark protocol

1. **Freeze inputs before timing.**
   Record HEAD, dirty-path digests, baseline/candidate script hashes, corpus relative paths and per-file SHA-256, source-declaration hash, exact interpreter executable/version/build, OS/architecture, command arguments, working directory, GC mode, and variant boundaries.
   Preserve the baseline's bytes; do not reconstruct it later from prose patches.
   Counts are descriptive metadata, not substitute digests.
2. **Characterize the invocation.**
   Measure (a) warmed complete catalog operation including file reads and decoding, with cache newly created for every call; (b) fresh Python CLI including imports, filesystem access, validation, rendering, and captured output.
   An optional preloaded-text measurement attributes CPU work but cannot satisfy the whole-operation gate alone.
   Fresh process does not mean cold filesystem cache; do not flush OS caches.
3. **Keep diagnostics out of timings.**
   Profile once outside timed samples if attribution is useful.
   Use `tracemalloc` separately; do not compare profiled or traced timings against ordinary execution.
   Preserve failed results and raw stdout/stderr/exit codes.
4. **Use three fresh measurement sessions.**
   For warm runs, five untimed operations per variant followed by 30 balanced pairs, alternating AB/BA order.
   Use `timeit` with GC explicitly enabled; a timed value aggregates enough complete operations to last at least roughly 100 ms, recording loop count and normalized per-operation duration.
   Do not share a populated cache between operations.
   Run variants sequentially, with identical scope and stdout treatment.
5. **Measure process latency independently.**
   Capture at least 20 balanced baseline/candidate fresh-process pairs per session, normalizing neither imports nor process creation away. Use the same exact interpreter and bytecode policy for both. Benchmark CLI success on copies/unchanged read-only inputs; no console cleanup routine or game side effects are involved.
6. **Measure memory independently.**
   Record peak traced allocation plus cache-entry/token counts on the real corpus, a repeated-token corpus with more locales, and an all-unique-token corpus with similar total input size.
   Peak delta is candidate minus baseline under the same method.
   Traced allocation is not RSS; do not claim a whole-process memory ceiling.
   Confirm a second operation does not accumulate retained state.
   Input-linear storage is acceptable; unexplained retention across calls is not.
7. **Inspect stability and decide.**
   Retain samples and summarize session medians/ranges with `statistics`. Apply AC-004/009 to each session, not a selectively pooled best session.
   If variation exceeds the proposed effect or signs reverse, classify the result inconclusive and stop the bounded campaign.
   Do not delete outliers selectively, keep measuring until success, infer statistical significance, or add a home-grown hypothesis test.
   Formal statistical needs reopen maintained pyperf adoption.
8. **Interpret benefit honestly.**
   Report absolute milliseconds and relative ratio for the exact operation.
   Optional aggregate estimates need actual invocation frequency; no usage-frequency telemetry is invented.
   A negligible whole-process benefit can justify declining a more complex candidate even when its inner mechanism is faster.

No benchmark was executed during planning.
The 1.52x figure is a review hypothesis, not a target that must be achieved or a result promised by this plan.
No timing gate runs inside GitHub unit tests.

## 9. Verification commands and HISEW assurance boundary

At planning inspection, `inspect-verification-profiles` returned these registered commands:

| Profile    | Actual declared commands                                                                                                                                                                   |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `focused`  | `npm run test:pipeline`                                                                                                                                                                    |
| `affected` | focused command, then `npm run pipeline -- validate --mod mods/delivery-temperature-limit-supercooled`, then `npm run pipeline -- build --mod mods/delivery-temperature-limit-supercooled` |
| `full`     | affected commands, then `npm run pipeline -- test --mod mods/delivery-temperature-limit-supercooled`                                                                                       |

`package.json` resolves `test:pipeline` to the `.NET` pipeline test project, and `pipeline` to the repository-local `.NET` pipeline executable.
No declared command runs `unittest` or the catalog scripts.
Coverage and input ordering are currently unknown/undeclared, not proven correct.
The plan does not change those profiles or treat their names as Python coverage.

Future supplemental Python commands, from the repository root, after the relevant files exist:

```powershell
python -B -m unittest discover -s tests -p "test_translation_catalogs.py"
python -B -m unittest discover -s tests -p "test_*.py"
python -B tools/translation-catalogs/check_catalogs.py
python -B tools/translation-catalogs/read_option_catalogs.py
```

Use an explicitly addressed current-target interpreter for its separate qualification; record its absolute executable path rather than assuming the `python` alias selected it.
Keep import/root-anchoring and baseline/candidate CLI comparisons in the test module or the narrow benchmark adapter.
Redirect the reader output to task-owned evidence when capturing; never modify the catalogs as a benchmark setup.

For final R1 implementation evidence, run the existing affected profile through HISEW under the then-current session identity after applicability, source state, and accepted authority are refreshed.
Do not replay the configured commands manually first merely to repeat them for receipts.
Supplement with the Python commands and raw performance evidence, explicitly marked as operator evidence when not captured by the engine.
Refresh profile inspection at implementation entry and after any authorized configuration change.

If an acceptance decision requires **engine-observed Python proof**, the present profiles cannot supply it.
Stop that assurance claim and obtain exact approval for the smallest profile command addition using the native proposal mechanism; identify its resolved configuration file, command, timeout, coverage and pipeline impact before asking.
Do not edit numbered store files directly or manufacture a receipt.
This gate does not block creating a plan or collecting truthful supplemental evidence.

Ordinary review happens once on the frozen implementation scope through the applicable selected provider.
Resolve findings, rerun only affected evidence, and preserve the provider's actual target/limitations. This plan's source/link/traceability checks do not constitute that future implementation review.

## 10. Traceability and integration gates

| Slice     | REQ / AC / QA / DEC links                                                       | Falsifiable proof                                                                                                             | Release and cleanup implication                                                   |
| --------- | ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| SLICE-001 | REQ-001,005–008 / AC-001,005–008 / QA-001–003,007–009 / DEC-001,002,006,007,009 | Frozen-original equivalence, independent negative/golden fixtures, import/cwd tests, clean-checkout run                       | Tracked tooling can stand alone; keep historical ignored inputs and no shims      |
| SLICE-002 | REQ-001,002,004 / AC-001,002,004 / QA-001–003,005 / DEC-003,005,006             | Full checker correctness plus isolated hoisting comparison                                                                    | Reversible checker-only optimization; no corpus migration                         |
| SLICE-003 | REQ-001–004,008 / AC-001–004,008 / QA-001–005,009 / DEC-004,005,009             | Independent cache cases, fresh invocation, latency/peak-memory gates                                                          | Cache disappears with operation; reverse independently if not useful              |
| SLICE-004 | REQ-003,005–009 / AC-003,005–009 / QA-001,003,004,006–009 / DEC-002,004–007,009 | Fixed-order exact JSON and reader-specific measurements                                                                       | Deliver/defer independently; do not duplicate maintained consumers                |
| SLICE-005 | REQ-001–009 / AC-001–009 / QA-001–009 / DEC-001–009                             | Frozen candidate, ordinary review, supplemental Python proof, registered affected receipts and honest performance disposition | Tool adoption only; task-owned cleanup, retained evidence, separate Git authority |

AC-009 may be explicitly deferred with SLICE-004; the checker does not claim reader acceptance in that case.
QA-010/DEC-008 concern only a separately accepted console extension, not main-program completion.

Gates:

1. **GATE-001 — accepted intent:** implementation authority, draft brief/route disposition, exact input identities, parser subset and output obligations settled.
   No self-approval or inferred configuration permission.
2. **GATE-002 — reproducible oracle:** SLICE-001 passes before timing optimized variants.
   Missing ignored inputs, wrong counts, or inconsistent independent fixtures block equivalence claims.
3. **GATE-003 — optimization qualification:** each candidate passes correctness and its stated performance/memory obligations; simplify/defer when benefit is inconclusive.
   Thresholds cannot move post hoc.
4. **GATE-004 — review and assurance:** frozen implementation, ordinary review disposition, complete Python evidence and required HISEW affected receipts.
   Unknown profile coverage and consumer limitations remain visible.
5. **GATE-005 — adoption/delivery:** documented qualified entrypoints work in a clean checkout.
   Any commit/push uses its separately authorized scope and maintained skills; local acceptance is not remote-main publication.

## 11. Unknowns and cheapest discriminating experiments

| Unknown                                                        | Cheapest evidence                                                                                                                                   | Decision/stop rule                                                                                                                                                             |
| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Do ignored-script consumers need their old paths?              | Search current docs/scripts/tasks for exact references; inspect known entrypoints before switching                                                  | Migrate discovered accepted consumers directly. A required unmigratable external contract blocks path retirement; no automatic alias.                                          |
| Does the limited parser omit required current catalog content? | Compare contextual-entry shape inventory with matched entries; inspect multiline/plural/fuzzy/obsolete constructs and declared current requirements | If currently required entries are omitted, DEC-002 is inadequate: rebaseline around maintained parsing before optimizing. Do not celebrate a fast incomplete check.            |
| Are baseline diagnostics/order reproducible?                   | Fixed enumeration plus single-fault and multi-fault temporary corpora                                                                               | Preserve expected first failure categories. A disagreement requires semantic review, not changing expected output to match candidate.                                          |
| Does hoisting/caching help actual CLI use?                     | Baseline/isolated/combined warm and fresh-process pairs on the same frozen corpus                                                                   | Apply AC-004; poor benefit favors simpler code or deferral.                                                                                                                    |
| How much memory does all-literal caching retain?               | Separate traced allocation and token cardinality on repeated versus unique fixtures                                                                 | Apply preset budget; try source/context-only cache if justified; persistent growth blocks acceptance.                                                                          |
| Is reader caching worthwhile?                                  | Compiled-only versus cached reader, with exact-output proof                                                                                         | Apply AC-009 independently; do not infer benefit from checker.                                                                                                                 |
| Can 3.14.8 qualification run safely?                           | Inspect approved installed interpreter inventory and explicitly identify executable/version                                                         | Missing current-target runtime is an integration gap, not permission to label 3.14.7 current or install/change pins. Retain target qualification as unverified until resolved. |
| Does bytecode contribute to discovery time?                    | Read discovery behavior and, only if cleanup is later accepted, compare identical test trees with/without the exact file in disposable copies       | No main-program deletion or overhead claim.                                                                                                                                    |
| Is console startup a meaningful outcome?                       | Optional safe helper-only/mocked-main timing under separately accepted scope, excluding human input                                                 | Without meaningful benefit or a concrete compatibility need, keep PERF-03 deferred.                                                                                            |

No remaining user-preference decision is required to finish this **draft plan**.
Technical recommendations are supplied with their evidence and acceptance gates.
If later evidence leaves a material choice unresolved after the ordered research, use [the grilling skill](C:/Users/maksy/.agents/skills/grilling/SKILL.md): ask the whole prerequisite-ready frontier with numbered questions and recommendations, wait for answers, and do not implement a disputed branch before shared understanding is confirmed.
Facts are investigated by the agent; they are not passed to the user as guesswork.
The skill does not expand configuration or delegation authority.

## 12. Compatibility, security, observability, rollout, and recovery

### Compatibility and migration

No persisted data/schema, backfill, saved-game migration, or reconciliation job is needed. Catalog/source files remain read-only inputs.
The only migration is documented entrypoint ownership from ignored historical material to tracked tooling.
Default reader path anchoring changes deliberately; current repository-root content/output remains the reference.
No interpreter pin, package graph, workflow, localization format, or game loader change accompanies it.

Interruption/resumption: resume from the last complete slice's frozen evidence, refresh dirty/ignored inputs and accepted decisions, and discard stale measurements when hashes/runtime differ. Rebuild the baseline/candidate comparison rather than substituting whichever files are currently present.
Do not silently adopt another HISEW execution or use a prior session's identity.

### Security and trust boundaries

Main tooling reads repository-owned local catalogs and C# declarations, decodes literal strings, and writes results only to stdout or an explicitly chosen task-owned evidence destination.
It does not use `eval`, unpickle data, fetch network resources, execute translation content, change access controls, or invoke cleanup operations.
No new native FFI is added in the main scope.

Ordinary review must inspect unexpected file writes/subprocesses, malformed-input errors, cache ownership, and any path-boundary changes. A future arbitrary-input service, external parser/dependency, privileged CI edit, native console branch, or security-sensitive input expansion triggers re-routing and the applicable native security assessment under separate scan authority.
A scanner was neither necessary to write this plan nor invoked.
Provider setup/scan permission cannot be inferred from plugin availability.

### Observability and outcome acceptance

Retain current success counts and failure identifiers; keep routine stdout compatible.
Put benchmark metadata and diagnostic profiles in evidence, not the maintainer's ordinary CLI output.
Record which variant actually qualified, absolute/relative timings by boundary, memory measure type, test inventory, reviewer target, and all limitations.
No production telemetry service or usage tracking is added.

For these local tools, production acceptance means maintainer use of the tracked command on the actual checked-in corpus, with the qualified outcome and no historical-artifact dependency.
The implementing task observes that run; the repository owner decides adoption when requested.
This is distinct from game localization/runtime acceptance, external distribution, and remote Git delivery.

### Rollout, abort, and recovery

Roll out the reproducible baseline first, then qualifying checker changes, then any independently qualifying reader optimization.
Do not ship a slower or weaker checker just because all planned code is written.
Stop on changed input contracts, missing negative-oracle evidence, stale candidate identity, configuration requirements, worse diagnostics, unresolved review findings, inconclusive measurements, or a violated memory guardrail.

Before delivery, reverse only the task's specific edits or keep the proven baseline and defer optimization.
After an authorized commit, a rollback/corrective commit itself requires appropriate authority; do not rewrite history or overwrite working-tree changes.
The previous tracked baseline can be restored through that corrective delivery route without a data restore because no input is migrated.
Forward-fix is appropriate for a narrow proven defect; re-run the affected checks and measurements.
Console-side effects, if that branch is ever accepted, need their own recovery analysis and are not covered by this source-only rollback claim.

### Cleanup and retention

Preserve the original review, ignored catalog scripts, pre-existing untracked documents, and old bytecode.
Keep raw successful, failed, and inconclusive benchmark evidence while it has review/adoption consumers.
Remove only task-owned temporary copies and scratch outputs after durable evidence is retained and no consumer remains, using the applicable recoverable cleanup rules.
Do not remove a file merely because it lives under `artifacts/` or `__pycache__/`.

### Replanning triggers

Rebaseline if catalog syntax, source declarations, number/shape of files, runtime version, output consumers, invocation paths, dependencies, or configuration materially change. Re-route if a local optimization becomes external/public tooling, a game-load acceptance check, a trust-boundary change, or destructive/native-platform work.
Reassess the purpose at extraction, optimization selection, pre-review freeze, and adoption: added complexity must still improve maintainer throughput without weakening assurance.
If it only improves a microbenchmark proxy, simplify or decline it.

## 13. Planning validation and handoff status

Completed during planning: repository/instruction/input inspection; HISEW applicability/status/profile inspection; ordered primary-source research and license-text inspection; current catalog checker success observation; token-cardinality and eager-default demonstrations; draft route/dossier/design/scenarios/slices/traceability and approval boundaries.

Not completed: implementation, candidate tests/benchmarks, current-target runtime qualification, ordinary implementation review, HISEW verification receipts, configuration approval, deletion, commits, pushes, or runtime/release acceptance.
This document is a reviewable proposal; its checklists and predicted paths are not completed work.

## 14. Implementation outcome — 2026-10-05

This section supersedes the planning-time status above.
The subsequent user request accepted implementation on the existing `main` checkout, signed commits and direct publication, including the named review and this plan at the first checkpoint.
No configuration change was necessary.
HISEW execution `9bc96713-6b5e-42ad-a45a-e3ec65c194d8` uses the accepted R1 route.

- **SLICE-001 completed:** tracked import-safe checker/reader, independent fixtures and invocation guidance were signed in baseline commit `3db08d4cd552865882ca7e10cc6f7a24f51364af`. The original review, ignored scripts, `clean.py`, old bytecode, catalog/source inputs and unrelated untracked documents were preserved.
- **SLICE-002/003 qualified:** the checker precompiles the numbered-token pattern, computes source metadata once, and caches successfully decoded literals for one operation.
  Explicit membership preserves empty-string hits.
  Source equality, duplicate rejection, occurrence multiplicity, newline checks, exception categories and the C# comparison remain intact.
  A newly introduced additional-POT source-check omission was reproduced with a failing fixture and corrected before final qualification; the fixture also checks acceptance of a valid additional POT with empty translations.
- **SLICE-004 deferred:** an exploratory cached reader saved about 5.2%/0.14 ms with substantial sample variation.
  The preset repeatable benefit was not established; the tracked reader retains its baseline implementation.
  PERF-03 and bytecode cleanup remain outside the main scope.
- **SLICE-005 implementation evidence completed:** all 30 discovered Python tests passed on CPython 3.14.7 and the isolated task-owned CPython 3.14.8, and in an artifact-free checkout overlay on 3.14.8.
  Both promoted CLIs produced byte-identical stdout to the preserved historical scripts on this corpus.
  The actual checker returned 82 source strings and 18 locales.
  Existing HISEW affected-profile coverage remains separate from these supplemental Python observations.

Three fresh final-code sessions, with GC enabled, five warmups, 30 balanced warm pairs and 20 balanced CLI pairs per session, yielded:

| Session | Warm baseline → candidate | Reduction / saved time | Fresh-process baseline → candidate | Peak traced allocation delta |
| ------- | ------------------------- | ---------------------- | ---------------------------------- | ---------------------------- |
| 1       | 8.675 → 6.096 ms          | 29.7% / 2.579 ms       | 98.450 → 96.173 ms                 | −20,851 bytes                |
| 2       | 8.220 → 5.697 ms          | 30.7% / 2.524 ms       | 102.721 → 102.614 ms               | −20,499 bytes                |
| 3       | 8.903 → 6.230 ms          | 30.0% / 2.673 ms       | 104.794 → 103.292 ms               | −20,405 bytes                |

Every final checker session passed AC-004 without changing thresholds or removing samples.
These are descriptive local measurements, not statistical significance, a guaranteed CLI speedup, RSS limits, or game-runtime acceptance.
Earlier exploratory and pre-correction sessions remain retained but do not establish final-code acceptance.
The exploratory baseline/hoisted/combined comparison provided attribution; final qualification compares the retained combined checker against the frozen baseline.

Separate diagnostic counts were 4,737 literal occurrences/1,551 distinct tokens on the real corpus, 13,593/1,551 with repeated locales, and 13,593/4,597 with distinct translations. Fully unique English/context tokens would violate the required source/key equality; the stress fixture therefore varies translation tokens while retaining valid catalog contracts. Raw memory observations include first/second operation readbacks and both stress shapes; operation-local ownership and fresh-operation fixtures provide the retention proof.

Independent static review used Antigravity CLI 1.2.17, `gemini-3.8-flash-high` with high effort: one consolidated broad review and one bounded follow-up restricted to the additional-POT correction.
The broad report had no actionable findings; its general acceptance language is limited to static inspection and does not establish execution or performance.
The follow-up's positive-fixture suggestion was applied.
Its optional self-comparison optimization was declined because correctness and preset performance gates already passed.
Reviewer file references and contracts were checked against the frozen input manifests; native process receipts confirmed producer quiescence.
No additional review iteration was required.

Durable task evidence is under `C:\Users\maksy\.hi\w\e\python-performance-2026-10-05`: `qualification.json`, `qualification-identities.json`, `qualified-session-{1,2,3}.json`, Python logs, frozen baseline inputs, review assignments/manifests/reports and native-process receipts.
The task-owned interpreter and checkout copies remain retained for reproduction; temporary test/stress fixtures are automatically removed.
The evidence index records final commit identity, registered HISEW results and remote publication separately once observed.
No pre-commit statement here claims those later effects already occurred.

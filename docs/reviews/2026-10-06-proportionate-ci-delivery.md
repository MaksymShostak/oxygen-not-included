# Proportionate CI implementation delivery

The implementation uses one input selector and stable completion gate, with separate application, Markdown, CodeQL and NuGet consumers.
Documentation-only changes select authored Markdown quality without the application worker, CodeQL or controlled NuGet restoration.
Missing, failed or cancelled selected consumers fail the gate; uncertain classification selects conservative coverage.

The accepted implementation and qualification limits are in the [plan](../plans/2026-10-06-proportionate-github-actions.md).
The [core workflow](../../.github/workflows/oni-checks.yml) owns selection; [CodeQL](../../.github/workflows/codeql.yml) and [NuGet publication](../../.github/workflows/dependency-submission.yml) consume its validated plan.

## Tool and README adoption

The isolated Markdown graph pins coordinated `@hadden-industries/markdown-quality@1.0.2`, using Node `>=24.21.0 <25`.
This corrects the fenced-code preservation refusal and the inline-code comparison false positive; it adds no third-party dependency or consumer repair.
The public full-scope live check passes all 40 documents present before this delivery record, without exclusions for either historical review.
Code-block bodies remain intact, and repeated formatting is stable.

The root README is a repository hub.
The complete synchronized description is in the [mod README](../../mods/delivery-temperature-limit-supercooled/README.md), retaining all six compatibility badges with explicit Markdown alternatives.
The real producer check has no drift or writes; its public converter, bounded badge adapter and native formatter preserve authored bytes outside the managed block.

## Local and hosted evidence

Signed implementation HEAD `bbb177c7d94fdc0065f19106f0ec0a10f59bcc22` passed canonical full HISEW receipt `c0b01c19-4f21-4128-b889-36448d137b73`: 406 pipeline tests, mod validation, production build and 926 executed mod tests.
The mod suite discovered 991 tests, with 65 not executed and no failures.
The supplemental Python suite passed 58 discovered tests with one Windows newline-name skip; the Linux control job exercises that fixture.
Native actual-workflow mutation checks rejected ten weakened orchestration specimens.

[Control push 37429604833, attempt 1](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37429604833/attempts/1) passed selection, Linux contracts, Windows pipeline/Python/producer, complete authored Markdown, Actions/Python/C# CodeQL, controlled NuGet publication and the stable gate.
Attempt 2 reran only the dependency publisher and its downstream gate with debug capture; prior successful exact-source consumers were retained.

The native controlled publisher's four complete manifests exactly match [automatic submission 37429604023, attempt 2](https://github.com/MaksymShostak/oxygen-not-included/actions/runs/37429604023/attempts/2), including direct/transitive identity, scope and child relationships.
GitHub SBOM readback contains all 21 distinct NuGet identities and 17 native dependency edges.
Detailed receipts, complete native observations and independent-review dispositions are retained externally under `C:/Users/maksy/.hi/w/e/oni-proportionate-ci-2026-10-06`.

## Activation state and remaining limits

Automatic dependency submission remains enabled pending its separate exact setting approval.
The genuine documentation-only qualification follows publication of this delivery record.
Hosted PR/fork, isolated language/product, project-removal and reordered-publication scenarios remain pending until genuine changes exercise them; local fixtures are not hosted evidence.
The native latest-correlator rule and freshness checks do not provide an atomic check-and-submit guarantee.
The earlier input-specific `ANALYSIS_TIMEOUT` has no general resolution claim.

The configured full HISEW profile captures its four repository commands; supplemental Markdown/CI/hosted observations retain their separate provenance.
This delivery does not claim release-candidate, Steam publication, live-game or proprietary fixture acceptance.

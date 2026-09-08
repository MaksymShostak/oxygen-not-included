# Steamify regression lineage

`historical-results.json` is the exact supplied Steamify 2.0.1 probe result from
2026-09-07, task `01a07cec-e183-70f3-abcc-d3df666d335e`. It contains all 33 checks:
16 current-description passes and 6 passes among 17 additional probes. The
historical source SHA-256 is recorded there. Those results are historical evidence,
not the new converter's oracle or an assertion about later Steamify versions.

The historical description differs from the current repository description. The
new `description.bbcode` is independently authored AGPL-3.0-only fixture material
with the same 16 structural obligations: determinism, LF/CRLF equivalence, six
level-one headings, 29 strong nodes, five emphasis nodes, four unordered lists,
18 items, both links, nested italic link text, five literal strings and prose order.
`description.txt` supplies the independently written expected visible prose.
The original check names remain the stable crosswalk, including placeholder link
identifiers; their labels do not attest current mod content.

`original-description.bbcode` was recovered on 2026-09-08 from the retained raw
output of the original task's `Get-Content` command (2026-09-07T17:34:10Z). It is
5,780 UTF-8 bytes, LF, without a final newline. Its SHA-256 exactly matches the
original probe: `d2d80f2e374a2eb7360d480cd139c458c1404f6ae4592e4131c009e452eb8a3c`.
None of the nine retained Git versions, including their CRLF checkouts, matched;
the probe had read an uncommitted description. A test checks the recovered bytes
before executing all 16 original description obligations against the new converter.
The copied mod description retains the repository's [MIT/Klei licence](../../../../../LICENSE);
it is fixture input, not newly authored AGPL implementation source.

All 17 minimal probe inputs are retained verbatim in the historical evidence.
New semantic expectations must come from the accepted plan and source registry,
not the old Markdown or HTML output. Underline/spoiler loss needs diagnostics when
target presentation cannot be established; preserving visible source text alone
does not establish faithful presentation. Registry and runtime conformance remain
separate from tests checking that this corpus has been retained correctly.

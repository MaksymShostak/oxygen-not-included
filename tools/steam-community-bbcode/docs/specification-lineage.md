# Specification lineage

`spec/sources.json`, `spec/profiles.json` and `spec/constructs.json` are the
versioned sources of construct and profile facts. They distinguish Valve's own
documentation, community observations, historical syntax and renderer behavior.
An omitted profile entry means evidence is unclassified, not that Steam rejects
the construct. A recorded observation is not a live rendering test.

Source coverage, profile support, target representability and tested conversion
fidelity are separate facts. Registry presence is not an implemented handler or a
conformance pass. Executed coverage and support documentation must be generated
from the same registry once conversion tests exist. The initial registry has no
passing-conformance flags and makes no complete-coverage claim.

Steam's plain-URL widgets, emoticons, spacing transformations, content filtering
and clan-image placeholders are renderer behavior. Their registry entries must
not be represented as fictional paired BBCode tags. External resources are never
fetched to resolve media or reproduce renderer behavior.

Initial facts reuse the accepted plan and were refreshed against the three source
pages on 2026-09-08. The community guide includes historical/disputed behavior;
these classifications must remain visible instead of implying current Valve
support. Registry edits require source evidence and semantic fixtures, not only
schema validation.

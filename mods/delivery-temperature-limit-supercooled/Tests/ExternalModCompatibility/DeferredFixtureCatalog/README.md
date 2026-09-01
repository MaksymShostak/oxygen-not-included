# Deferred external-mod fixture catalog stubs

These source files preserve proposed serialized document shapes for a future
generalized external-mod evidence catalog. They are deliberately
non-operational: current code does not discover, construct, deserialize, or
validate them, and no current compatibility decision relies on them.

Activate this design only when the repository has a concrete need to retain an
evaluated incompatible build or to apply the same evidence workflow to another
external integration. Activation starts with failing acceptance tests and must
keep format knowledge with authoritative tools. Repository validation should
cover only cross-artifact invariants those tools cannot know, including:

- exact content-addressed path, file-version, and digest agreement;
- one explicit support or incompatibility decision per retained build;
- retained-file origin and unavailable-fact completeness;
- safe, unambiguous relative artifact paths; and
- one-to-one closure between supported production identities and compatible
  fixtures.

If JSON remains the chosen format, configure `System.Text.Json` to reject
duplicate properties and unknown properties before adding repository-specific
invariant checks. If package metadata is retained, execute its authoritative
parser or consumer instead of reimplementing that format in repository code.

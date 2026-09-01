# FastTrack 0.18.5.0 preserved compatibility fixture

This directory preserves inert, repository-owned test evidence for one exact
FastTrack assembly build. Preservation identifies the bytes under test; it does
not imply support until the production catalog admits the same file-version
and SHA-256 pair.

## Assembly build identity

- file: `FastTrack.dll`
- file version: `0.18.5.0`
- assembly name: `FastTrack`
- assembly version: `0.18.0.0`
- module version identifier: `bb4e7a11-4985-4d8f-b1c9-f497c6bb3d1e`
- DLL SHA-256: `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B`

## Provenance boundary

The candidate archive was observed at the mutable `FastTrackBeta` release
endpoint at `2026-09-01T05:00:41.3223477Z` and hashed before extraction:

- archive SHA-256: `3ED47A89966B3780DD4C8855DA20B6335B642AA15A92143DA749FBC3621F5211`
- archive member `FastTrack/FastTrack.dll`: SHA-256
  `CDF0150546952FDA3A31A612D61FBEF3808E05DB89B9B6E8CCEEA1F3C752AA3B`,
  retained as `FastTrack.dll`
- archive member `FastTrack/mod.yaml`: SHA-256
  `32576251B1A57027DF93F47748679650E4916AD8F4E7F872C39C5D12E98EC20E`
- archive member `FastTrack/mod_info.yaml`: SHA-256
  `7CAAF5D05ECC1AD5B362E52616A179519B935BF4204F9E2677A6CF02AADEAB5D`

The archive contained exactly the `FastTrack/` directory and those three
regular files. The member paths were checked for rooting, parent traversal,
case-colliding duplicates, and link-like entries before the three files were
accepted. The ZIP and package metadata bytes are not retained because the
compatibility gate consumes only the exact assembly build; their observed
identities and semantic values remain recorded here. Keeping those additional
formats as active fixtures would add no compatibility evidence to the current
test objective.

`UPSTREAM-LICENSE.txt` is the exact `LICENSE` file from immutable upstream
source revision `e24e8f3082a52785e971943a8f1fff8de0ca8dff`. It is recorded as
source-revision evidence and is not represented as a release-archive member.

## Observed packaged identity

The package metadata observed in that archive declared:

- static ID: `PeterHan.FastTrack`
- package version: `0.18.5.0`
- minimum supported ONI build: `736649`
- API version: `2`

## Compatibility classification

The permanent static matrix verifies this build's world-inventory and
pickup-grouping replacements and verifies that its direct-delivery replacement
is absent. The production catalog admits this build only after those tests and
the fixture closed-world test pass.

Future FastTrack bytes, including same-version republishings, require a new
content-addressed fixture and an explicit compatibility decision.

## Handling constraints

Tests inspect `FastTrack.dll` only as PE data through
`System.Reflection.Metadata` and `PEReader`. They must never load or execute the
fixture, resolve its dependencies, add it as a compile reference, copy it into
production output, or include it in a published mod package.

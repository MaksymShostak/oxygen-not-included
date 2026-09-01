# FastTrack 0.18.4.0 preserved compatibility fixture

This directory preserves inert, repository-owned test evidence for one exact
FastTrack assembly build. Preservation identifies the bytes under test; it does
not imply support for any other file published with the same version.

## Assembly build identity

- file: `FastTrack.dll`
- file version: `0.18.4.0`
- assembly name: `FastTrack`
- assembly version: `0.18.0.0`
- module version identifier: `b1e31127-5b91-4607-b5b5-8ea255bd5288`
- DLL SHA-256: `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`

## Provenance boundary

The DLL was previously extracted without modification from the mutable
`FastTrackBeta` GitHub release asset. The recorded archive SHA-256 is
`8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`,
but the original archive is no longer available from that endpoint and its
observation timestamp was not retained. The DLL is therefore documented only
as a previously extracted release-archive member; this note does not claim a
new download or reconstructed archive.

The closest independently reviewed upstream source revision is
`e24e8f3082a52785e971943a8f1fff8de0ca8dff`. That revision contains neither
`FastTrack/mod.yaml` nor `FastTrack/mod_info.yaml`, so the exact packaged files,
member paths, and hashes cannot be recovered from source and are not
synthesized here.

`UPSTREAM-LICENSE.txt` is the exact `LICENSE` file from that immutable source
revision. It is source-revision evidence and is not represented as a member of
the unavailable release archive.

The actual Steam Workshop-distributed FastTrack DLL could not be located or
proven byte-identical to this artifact. Compatibility claims are limited to
this exact file-version and SHA-256 pair.

## Compatibility classification

Static metadata inspection confirms that this build contains FastTrack's
world-inventory and pickup-grouping replacements. It does not contain
`PeterHan.FastTrack.GamePatches.ChoreComparator.CheckFetchChore` or the former
`ChorePatches.GlobalChoreProvider_CollectChores_Patch` that activated it.
Delivery Temperature Limit therefore retains its Klei direct-delivery
implementation for this build.

The production supported-build catalog admits this exact assembly identity.
Future FastTrack bytes, including same-version republishings, require a new
content-addressed fixture and an explicit compatibility decision.

## Handling constraints

Tests inspect `FastTrack.dll` only as PE data through
`System.Reflection.Metadata` and `PEReader`. They must never load or execute the
fixture, resolve its dependencies, add it as a compile reference, copy it into
production output, or include it in a published mod package.

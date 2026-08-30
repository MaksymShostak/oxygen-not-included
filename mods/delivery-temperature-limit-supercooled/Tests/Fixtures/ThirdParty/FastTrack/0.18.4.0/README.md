# FastTrack 0.18.4.0 static contract fixture

`FastTrack.dll` in this directory is test-only evidence extracted without modification from the official `FastTrack.zip` asset published by `peterhaneve` on the ONIMods GitHub release page:

- release page: <https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta>
- release asset: <https://github.com/peterhaneve/ONIMods/releases/download/FastTrackBeta/FastTrack.zip>
- release name observed during acquisition: `Fast Track Beta - 0.18.4.0`
- closest reviewed source revision: `e24e8f3082a52785e971943a8f1fff8de0ca8dff`
- DLL file version: `0.18.4.0`
- DLL assembly version: `0.18.0.0`
- `FastTrack.dll` SHA-256: `D291C0D58379B77B4A60FB6D386B3783E4061E5C620DEF93502AE984CD657ADD`
- downloaded `FastTrack.zip` SHA-256: `8EA0263FBD64F3D94C4127A03EC15A8ED88A1DA6BBDEDDA7E8EE85C9E2B3FC1D`

The actual Steam Workshop-distributed FastTrack DLL could not be located or proven byte-identical to this GitHub release artifact. Compatibility claims are therefore limited to this available `0.18.4.0` artifact and are made on a best-efforts basis. They are not a promise for another FastTrack release or proof about the Workshop package.

Static metadata inspection confirms that this artifact contains FastTrack's world-inventory and pickup-grouping replacements. It does **not** contain `PeterHan.FastTrack.GamePatches.ChoreComparator.CheckFetchChore` or the former `ChorePatches.GlobalChoreProvider_CollectChores_Patch` that activated it. Upstream removed those chore replacements in commit [`201d2457162544504fbbf185ba076da1e9e9d41a`](https://github.com/peterhaneve/ONIMods/commit/201d2457162544504fbbf185ba076da1e9e9d41a). Delivery Temperature Limit must therefore classify FastTrack direct-delivery eligibility as `ReplacementInactive` for this artifact and retain the Klei direct-delivery implementation path; absence must not be mislabeled as an incompatible active replacement.

The word `Beta` above is part of the upstream GitHub release tag and URL. Delivery Temperature Limit itself has no beta release stage.

Tests inspect this DLL statically with `System.Reflection.Metadata` and `PEReader`. They must not resolve or execute the fixture's dependencies, add it as a compile/reference dependency, copy it into production output, or include it in a published mod package.

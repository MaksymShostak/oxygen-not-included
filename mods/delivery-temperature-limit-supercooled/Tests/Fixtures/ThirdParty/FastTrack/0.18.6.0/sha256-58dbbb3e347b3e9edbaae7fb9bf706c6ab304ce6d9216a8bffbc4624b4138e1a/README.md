# FastTrack 0.18.6.0 compatibility fixture

This directory preserves the released DLL as inert PE metadata for offline tests.
The package contains no FastTrack runtime.
Runtime admission depends on a paired in-game acceptance run with the same bytes and settings.

GitHub release asset `677623555` contained `FastTrack.zip` (562244 bytes), SHA-256 `882eac3c39571e921645b892005dc3fb4c8f34b43e532705bec3d95da1ff682a`.
The archive member `FastTrack/FastTrack.dll` was 1211904 bytes and matched the directory digest.
Its assembly identity was `FastTrack, Version=0.18.0.0`, its file version was `0.18.6.0`, and its MVID was `b82e018d-40cf-4df1-b9be-10e86ca3876d`.
The archive declared static ID `PeterHan.FastTrack` and minimum ONI build `736649`.
The tag pointed to `38dfc8c9aa2e3263c5bb033a8eafa64c20785485` at intake on 25 November 2026.
[Upstream release](https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta) · [Tagged source and license](https://github.com/peterhaneve/ONIMods/tree/38dfc8c9aa2e3263c5bb033a8eafa64c20785485)

The inventory and pickup replacements are present in these bytes.
The direct-delivery replacement is absent.
Default update paths reference the removed `SortedClearable.comparer` field and the former array-returning `Workable.GetOffsets(int)` signature.
U60 provides a read-only list.
The distributable catalog withholds admission pending live qualification.
The Workshop copy's byte identity is unverified.

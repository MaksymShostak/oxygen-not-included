# Changelog

## Unreleased

- Add Steam BBCode parsing with source spans, opaque code/noparse, resource limits
  and explicit malformed/unknown-source preservation.
- Add Steam-aware MDAST and native GFM serialization, with executed policies for
  37 observable constructs and two context-only renderer rules across six profiles.
- Preserve standalone bracket labels literally and normalize ordinary layout
  whitespace at converted block/list boundaries while protecting opaque content.
- Add an explicitly partial GFM-to-Steam API with unsupported-node accounting,
  safe literal encoding and bounded target output.
- Expose bounded UTF-8 conversion, fidelity exit policy and conformance reporting
  through the installed CLI.
- Add checked JavaScript contracts, generated declarations, installed consumers,
  structural regressions, seeded properties, runtime/mutation qualification and
  reproducible comparison evidence.

No npm release or Steam publication has occurred. See [release qualification](docs/releasing.md)
for the remaining acceptance and publication boundaries.

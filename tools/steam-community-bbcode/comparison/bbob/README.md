# BBob 4.4.1 qualification

Status: **rejected for adoption under the accepted strict checker contract**.

On Node 24.20.0, its 17 syntax/span characterization cases passed. This includes
recoverable evidence for malformed input; BBob's recovery tree is not necessarily
a valid Steam semantic tree. Missing opaque closers and mismatched pairs require
rejection/preservation by the semantic boundary.

TypeScript 7.0.2 with `exactOptionalPropertyTypes: true` and `skipLibCheck: false`
then reported TS2416: `@bbob/plugin-helper`'s `TagNode.toJSON()` explicitly returns
`start`/`end` possibly undefined, incompatible with the optional properties of
`TagNodeObject`. The full parser cannot satisfy the required tooling gate as shipped.

Reproduce in this directory with `npm ci --ignore-scripts` followed by
`npm run qualify`. The qualification is expected to exit nonzero at the checker;
that is retained rejection evidence, not a converter test pass. No declaration
patch, ambient replacement, checker relaxation, old dependency selection, or shim
is used. This isolated comparison environment is outside the distributed package.
Reassess only when a new upstream release or a specifically authorized decision
changes the failed contract. The companion packages retain their MIT notices.

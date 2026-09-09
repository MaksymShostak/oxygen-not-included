# JavaScript with checked JSDoc

Status: adopted from the accepted v2 plan, 2026-09-08.

Use ESM JavaScript with strict JSDoc checking and generated declarations.
This retains direct source execution while supplying checked public contracts.
A parallel TypeScript implementation, hand-maintained declaration model, dual CJS/ESM distribution, consumer compiler or runtime loader would add ownership and packaging obligations outside the accepted design.

The unified/MDAST ecosystem provides the relevant checked-JavaScript precedent.
Reuse its node types and serializers.
Static typing complements independent semantic, rendering, property and security checks; it cannot show that the registry includes every construct or that conversion preserves meaning.

The TypeScript 6.0.3 coverage-tool exception is isolated and explicitly approved; see [software selection](../software-selection.md).
The primary checker, declaration emitter and consumer compiler remain TypeScript 7.0.2.

Change this language decision only through a later accepted decision supported by maintenance evidence.
Generated declarations remain outputs of the source contract.

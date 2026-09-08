// SPDX-License-Identifier: AGPL-3.0-only
// Consumer-only TypeScript: the implementation remains checked JavaScript.
import type {SteamParseResourceLimits, SourceSpan} from 'steam-community-bbcode';

const limits: SteamParseResourceLimits = {
  maxInputBytes: 4096,
  maxNestingDepth: 32,
  maxNodeCount: 1000,
  maxAttributeBytes: 512,
};
const span: SourceSpan = {start: {line: 1, column: 1}, end: {line: 1, column: 3}};
// @ts-expect-error Byte limits are numeric; strings are not a public contract.
limits.maxInputBytes = '4096';
// @ts-expect-error A misspelled resource dimension must not be accepted.
limits.maxDepth = 32;
// @ts-expect-error All four resource dimensions are required.
const incomplete: SteamParseResourceLimits = {maxInputBytes: 10};
// @ts-expect-error Source coordinates reuse unist's numeric coordinate contract.
span.start.line = 'first';
void [limits, span, incomplete];

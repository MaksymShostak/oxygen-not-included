// SPDX-License-Identifier: AGPL-3.0-only
import {constructCases} from '../../test/conformance/construct-cases.js';
import {reverseCases} from '../../test/conformance/reverse-cases.js';

/** @typedef {{id: string, direction: 'forward' | 'reverse', source: string, expectedTree: unknown, expectedOutput?: string, requiresDiagnosis: boolean, policy: string}} ComparisonCase */
/** @type {readonly ComparisonCase[]} */
export const comparisonCases = [
  {id: 'plain-b', direction: 'forward', source: '[b]B[/b]', expectedOutput: '**B**', requiresDiagnosis: false,
    expectedTree: {type: 'root', children: [{type: 'paragraph', children: [{type: 'strong', children: [{type: 'text', value: 'B'}]}]}]},
    policy: 'Ordinary bold control, separately from literal-text escaping probes.'},
  {id: 'plain-h1', direction: 'forward', source: '[h1]H[/h1]', expectedOutput: '# H', requiresDiagnosis: false,
    expectedTree: {type: 'root', children: [{type: 'heading', depth: 1, children: [{type: 'text', value: 'H'}]}]},
    policy: 'Ordinary heading control.'},
  {id: 'plain-list', direction: 'forward', source: '[list]\n[*]A\n[/list]', requiresDiagnosis: false,
    expectedTree: {type: 'root', children: [{type: 'list', ordered: false, children: [{type: 'listItem', children: [{type: 'paragraph', children: [{type: 'text', value: 'A'}]}]}]}]},
    policy: 'Ordinary multiline list control, separately from compact list syntax.'},
  {id: 'plain-quote', direction: 'forward', source: '[quote]\nQ\n[/quote]', requiresDiagnosis: false,
    expectedTree: {type: 'root', children: [{type: 'blockquote', children: [{type: 'paragraph', children: [{type: 'text', value: 'Q'}]}]}]},
    policy: 'Ordinary multiline blockquote control, separately from compact quote syntax.'},
  ...constructCases.map(fixture => ({id: fixture.id, direction: /** @type {const} */ ('forward'), source: fixture.source,
    expectedTree: fixture.expectedGfm, requiresDiagnosis: !['exact', 'equivalent'].includes(fixture.fidelity),
    policy: `Authored ${fixture.fidelity} GFM mapping; diagnostics required for target loss or fallback.`})),
  ...reverseCases.filter(fixture => fixture.unsupported.length === 0).map(fixture => ({id: fixture.id,
    direction: /** @type {const} */ ('reverse'), source: fixture.source, expectedTree: fixture.expectedGfm,
    ...(fixture.expectedSteam === undefined ? {} : {expectedOutput: fixture.expectedSteam}),
    requiresDiagnosis: false, policy: fixture.policy})),
  {id: 'literal-bbcode', direction: 'reverse', source: '[b]Literal[/b]', requiresDiagnosis: false,
    expectedTree: {type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: '[b]Literal[/b]'}]}]},
    policy: 'Preserve GFM text. BUTR deliberately interprets literal BBCode: a documented policy difference, not an undocumented defect.'},
];

/** @typedef {'candidate' | 'steamify' | 'bbcode-to-markdown' | 'steam-editor-tools' | 'butr'} ProviderId */
/** @type {readonly ProviderId[]} */
export const providerIds = ['candidate', 'steamify', 'bbcode-to-markdown', 'steam-editor-tools', 'butr'];
const steamifyForward = new Set(['b', 'i', 'strike', 'h1', 'h2', 'h3', 'list', 'olist', 'code', 'hr', 'url', 'quote', 'img']);
const genericForward = new Set(['b', 'i', 'strike', 'h1', 'h2', 'h3', 'list', 'code', 'hr', 'url', 'quote', 'img', 'table', 'u', 'color', 'noparse']);

/** @param {ProviderId} provider @param {ComparisonCase} fixture */
export function applicability(provider, fixture) {
  const construct = fixture.id.startsWith('plain-') ? fixture.id.slice(6) : fixture.id;
  if (provider === 'candidate') return {applicable: true, reason: fixture.policy};
  if (provider === 'bbcode-to-markdown') return fixture.direction === 'forward' && genericForward.has(construct)
    ? {applicable: true, reason: 'Installed bbcodejs BUILTIN/newBBCodeTags registration; no claim of a complete Steam dialect. Content-only tags deliberately lose formatting.'}
    : {applicable: false, reason: 'No documented conversion for this direction or Steam-specific syntax; generic list=1 is not Steam olist.'};
  if (provider === 'steamify') return fixture.direction === 'forward'
    ? {applicable: steamifyForward.has(construct), reason: 'README supported-tag table. Underline, spoiler, noparse and table are explicitly passthrough; Steam media widgets are not claimed.'}
    : {applicable: fixture.id !== 'table', reason: 'Documented Markdown-to-Steam API; tables are absent from its supported Markdown feature list.'};
  return {applicable: fixture.direction === 'reverse', reason: fixture.direction === 'reverse'
    ? `${provider === 'butr' ? 'Documented Markdown CLI; literal BBCode is intentionally interpreted.' : 'Documented DocumentParser.parse_markdown and BBCodeRenderer path.'} ${fixture.policy}`
    : 'This project does not provide a Markdown output API.'};
}

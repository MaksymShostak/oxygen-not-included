// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Independently authored reverse cases. Exact Steam and semantic trees are
 * expectations, never snapshots produced by the converter.
 * @typedef {Readonly<{id: string, policy: string, source: string, unsupported: readonly import('mdast').Nodes['type'][], expectedSteam?: string, expectedGfm?: import('mdast').Root}>} ReverseCase
 */
/** @param {string} value @returns {import('mdast').Text} */
const text = value => ({type: 'text', value});
/** @param {import('mdast').PhrasingContent[]} children @returns {import('mdast').Paragraph} */
const paragraph = children => ({type: 'paragraph', children});
/** @param {import('mdast').RootContent[]} children @returns {import('mdast').Root} */
const root = children => ({type: 'root', children});

/** @type {readonly ReverseCase[]} */
export const reverseCases = [
  {id: 'empty', policy: 'Empty document', source: '', unsupported: [], expectedSteam: '', expectedGfm: root([])},
  {id: 'text', policy: 'Literal paragraph text', source: 'A &amp; B', unsupported: [],
    expectedSteam: '[noparse]A & B[/noparse]', expectedGfm: root([paragraph([text('A & B')])])},
  {id: 'headings', policy: 'Headings 1 through 3', source: '# A\n\n## B\n\n### C', unsupported: [],
    expectedSteam: '[h1][noparse]A[/noparse][/h1]\n\n[h2][noparse]B[/noparse][/h2]\n\n[h3][noparse]C[/noparse][/h3]',
    expectedGfm: root([{type: 'heading', depth: 1, children: [text('A')]}, {type: 'heading', depth: 2, children: [text('B')]}, {type: 'heading', depth: 3, children: [text('C')]}])},
  {id: 'inline', policy: 'Strong, emphasis and deletion', source: '**B** *I* ~~D~~', unsupported: [],
    expectedSteam: '[b][noparse]B[/noparse][/b][noparse] [/noparse][i][noparse]I[/noparse][/i][noparse] [/noparse][strike][noparse]D[/noparse][/strike]',
    expectedGfm: root([paragraph([{type: 'strong', children: [text('B')]}, text(' '), {type: 'emphasis', children: [text('I')]}, text(' '), {type: 'delete', children: [text('D')]}])])},
  {id: 'quote', policy: 'Blockquote', source: '> Q', unsupported: [], expectedSteam: '[quote][noparse]Q[/noparse][/quote]',
    expectedGfm: root([{type: 'blockquote', children: [paragraph([text('Q')])]}])},
  {id: 'rule', policy: 'Thematic break', source: '---', unsupported: [], expectedSteam: '[hr][/hr]', expectedGfm: root([{type: 'thematicBreak'}])},
  {id: 'unordered-list', policy: 'Unordered list', source: '- A\n- B', unsupported: [], expectedSteam: '[list][*][noparse]A[/noparse][*][noparse]B[/noparse][/list]',
    expectedGfm: root([{type: 'list', ordered: false, children: [{type: 'listItem', children: [paragraph([text('A')])]}, {type: 'listItem', children: [paragraph([text('B')])]}]}])},
  {id: 'ordered-list', policy: 'Ordered list starting at 1', source: '1. A\n2. B', unsupported: [], expectedSteam: '[olist][*][noparse]A[/noparse][*][noparse]B[/noparse][/olist]',
    expectedGfm: root([{type: 'list', ordered: true, children: [{type: 'listItem', children: [paragraph([text('A')])]}, {type: 'listItem', children: [paragraph([text('B')])]}]}])},
  {id: 'code', policy: 'Code block without metadata or closing delimiter', source: '```\n[b]X[/b]\n```', unsupported: [], expectedSteam: '[code][b]X[/b][/code]',
    expectedGfm: root([{type: 'code', value: '[b]X[/b]'}])},
  {id: 'link', policy: 'Safe absolute link without title', source: '[L](https://example.org)', unsupported: [], expectedSteam: '[url="https://example.org"][noparse]L[/noparse][/url]',
    expectedGfm: root([paragraph([{type: 'link', url: 'https://example.org', children: [text('L')]}])])},
  {id: 'image', policy: 'Safe absolute image without alt text or title', source: '![](https://example.org/i.png)', unsupported: [], expectedSteam: '[img]https://example.org/i.png[/img]',
    expectedGfm: root([paragraph([{type: 'image', url: 'https://example.org/i.png', alt: ''}])])},
  {id: 'table', policy: 'Rectangular table without alignment', source: '| H |\n| - |\n| V |', unsupported: [],
    expectedSteam: '[table][tr][th][noparse]H[/noparse][/th][/tr][tr][td][noparse]V[/noparse][/td][/tr][/table]',
    expectedGfm: root([{type: 'table', children: [{type: 'tableRow', children: [{type: 'tableCell', children: [text('H')]}]}, {type: 'tableRow', children: [{type: 'tableCell', children: [text('V')]}]}]}])},
  {id: 'heading-4', policy: 'Heading 4 preserved', source: '#### H', unsupported: ['heading']},
  {id: 'heading-5', policy: 'Heading 5 preserved', source: '##### H', unsupported: ['heading']},
  {id: 'heading-6', policy: 'Heading 6 preserved', source: '###### H', unsupported: ['heading']},
  {id: 'inline-code', policy: 'Inline code preserved', source: '`[b]X[/b]`', unsupported: ['inlineCode']},
  {id: 'html', policy: 'Raw HTML preserved', source: '<script>[b]X[/b]</script>', unsupported: ['html']},
  {id: 'task-list', policy: 'Task-list state preserved', source: '- [x] Done', unsupported: ['list']},
  {id: 'list-start', policy: 'Non-default list start preserved', source: '7. Seven', unsupported: ['list']},
  {id: 'code-language', policy: 'Code metadata preserved', source: '```js\nx\n```', unsupported: ['code']},
  {id: 'code-delimiter', policy: 'Code with Steam closing delimiter preserved', source: '```\n[/CoDe ]\n```', unsupported: ['code']},
  {id: 'image-alt', policy: 'Image alternative text preserved', source: '![Alt](https://example.org/i.png)', unsupported: ['image']},
  {id: 'image-title', policy: 'Image title preserved', source: '![](https://example.org/i.png "Title")', unsupported: ['image']},
  {id: 'link-title', policy: 'Link title preserved', source: '[L](https://example.org "Title")', unsupported: ['link']},
  {id: 'unsafe-link', policy: 'Unsafe link preserved', source: '[L](javascript:alert%281%29)', unsupported: ['link']},
  {id: 'relative-link', policy: 'Relative link preserved without a document base', source: '[L](./guide.md)', unsupported: ['link']},
  {id: 'relative-image', policy: 'Relative image preserved without a document base', source: '![](../i.png)', unsupported: ['image']},
  {id: 'aligned-table', policy: 'Table alignment preserved', source: '| H |\n| :- |\n| V |', unsupported: ['table']},
  {id: 'uneven-table', policy: 'Uneven table rows preserved', source: '| A | B |\n| - | - |\n| C |', unsupported: ['table']},
  {id: 'hard-break', policy: 'Hard-break source preserved', source: 'A  \nB', unsupported: ['break']},
  {id: 'definition', policy: 'Reference definition preserved', source: '[id]: https://example.org', unsupported: ['definition']},
  {id: 'link-reference', policy: 'Link reference and definition preserved', source: '[L][id]\n\n[id]: https://example.org', unsupported: ['linkReference', 'definition']},
  {id: 'image-reference', policy: 'Image reference and definition preserved', source: '![L][id]\n\n[id]: https://example.org/i.png', unsupported: ['imageReference', 'definition']},
];

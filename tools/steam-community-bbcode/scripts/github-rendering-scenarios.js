// SPDX-License-Identifier: AGPL-3.0-only
import {steamCommunityBbcodeToGfm} from '../src/index.js';

/**
 * Reviewed target presentation, never regenerated from a live response.
 * @typedef {object} GithubRenderingScenario
 * @property {'presentation' | 'literal-autolinks' | 'flow-whitespace'} name
 * @property {string} markdown
 * @property {string} expectedHtml
 * @property {string} [source]
 * @property {string} [expectedMarkdown]
 */
const flowSource = '[h1]Title[/h1]\n\nBody\n\n[hr][/hr]\n[list]\n[*] One\n[*] Two\n[/list]\n[noparse]  [sd]  [Fixed]  [/noparse]';

/** @type {GithubRenderingScenario[]} */
export const githubRenderingScenarios = [
  {
    name: 'presentation',
    markdown: [
      'Candidate U: <u>Underlined</u>',
      'Candidate INS: <ins>Inserted</ins>',
      steamCommunityBbcodeToGfm('[spoiler][b]Hidden[/b] *literal* <script>alert(1)</script>[/spoiler]').value,
      steamCommunityBbcodeToGfm('[table][tr][th]H[/th][/tr][tr][td][strike]Old[/strike][/td][/tr][/table]').value,
    ].join('\n\n'),
    expectedHtml: [
      '<p>Candidate U: Underlined</p>',
      '<p>Candidate INS: <ins>Inserted</ins></p>',
      '<details>', '<summary>Spoiler</summary>',
      '<p><strong>Hidden</strong> *literal* &lt;script&gt;alert(1)&lt;/script&gt;</p>',
      '</details>', '<markdown-accessiblity-table><table role="table">',
      '<thead>', '<tr>', '<th>H</th>', '</tr>', '</thead>',
      '<tbody>', '<tr>', '<td><del>Old</del></td>', '</tr>', '</tbody>',
      '</table></markdown-accessiblity-table>',
    ].join('\n'),
  },
  {
    name: 'literal-autolinks',
    markdown: [
      'Escaped: https\\://example.org/path',
      'Entity: https&#x3A;//example.org/path',
      steamCommunityBbcodeToGfm('[noparse]https://example.org/path[/noparse]').value,
      steamCommunityBbcodeToGfm('[img]https://example.org/image.png').value,
    ].join('\n\n'),
    expectedHtml: [
      '<p>Escaped: https://example.org/path</p>',
      '<p>Entity: https://example.org/path</p>',
      '<p>https://example.org/path</p>',
      '<p>[img]https://example.org/image.png</p>',
    ].join('\n'),
  },
  {
    name: 'flow-whitespace',
    source: flowSource,
    markdown: steamCommunityBbcodeToGfm(flowSource).value,
    expectedMarkdown: '# Title\n\nBody\n\n***\n\n* One\n* Two\n\n&#32;&#32;[sd]  [Fixed]&#32;&#32;\n',
    expectedHtml: '<h1>Title</h1>\n<p>Body</p>\n<hr>\n<ul>\n<li>One</li>\n<li>Two</li>\n</ul>\n<p>  [sd]  [Fixed]  </p>',
  },
];

/** Normalize transport whitespace without erasing text padding. @param {string} html @returns {string} */
export function normalizeGithubRenderedHtml(html) {
  return html.replaceAll('\r\n', '\n').replace(/\n+$/u, '');
}

// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';
import {semanticProjection} from './conformance/execute-cases.js';

/** @param {string} source */
function target(source) {
  return semanticProjection(fromMarkdown(source, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]}));
}

/** @type {[string, string, string][]} */
const formattingCases = [
  ['empty bold', '[b][/b]', ''],
  ['empty italic', 'a[i][/i]b', 'ab'],
  ['empty deletion', '[strike][b][/b][/strike]', ''],
  ['nested bold', '[b]a[b]b[/b]c[/b]', '**abc**'],
  ['nested italic', '[i]a[i]b[/i]c[/i]', '*abc*'],
  ['nested deletion', '[strike]a[strike]b[/strike]c[/strike]', '~~abc~~'],
  ['adjacent bold', '[b]a[/b][b]b[/b]', '**ab**'],
  ['redundant bold through italic', '[b]a[i]b[b]c[/b]d[/i]e[/b]', '**a*bcd*e**'],
  ['padded deletion', '[strike] D [/strike]', '~~&#x20;D&#x20;~~'],
  ['tab padded deletion', '[strike]\tD\t[/strike]', '~~&#x9;D&#x9;~~'],
  ['nonbreaking padded deletion', '[strike]\u00a0D\u00a0[/strike]', '~~&#xA0;D&#xA0;~~'],
  ['bold across paragraphs', '[b]a\n\nb[/b]', '**a**\n\n**b**'],
  ['italic across CRLF paragraphs', '[i]a\r\n \t\r\nb[/i]', '*a*\n\n*b*'],
  ['deletion across paragraphs', '[strike]a\n\nb[/strike]', '~~a~~\n\n~~b~~'],
  ['paragraph boundary across styles', '[b]a\n[/b][i]\nb[/i]', '**a**\n\n*b*'],
  ['nested paragraph styles', '[b]a[i]b\n\nc[/i]d[/b]', '**a*b***\n\n***c*d**'],
  ['links within retained style', '[b]a[url=https://example.org]b[b]c[/b][/url]d[/b]', '**a[bc](https://example.org)d**'],
  ['adjacent distinct link destinations', '[b][url=https://one.example]a[/url][url=https://two.example]b[/url][/b]', '**[a](https://one.example)[b](https://two.example)**'],
  ['adjacent distinct presentation', '[b]a[/b][i]b[/i][strike]c[/strike]', '**a***b*~~c~~'],
  ['link followed by formatted text', '[url=https://one.example][b]a[/b][/url][b]b[/b]', '[**a**](https://one.example)**b**'],
  ['opaque punctuation within style', '[b][noparse]*[x]*[/noparse][b]z[/b][/b]', '**\\*\\[x]\\*z**'],
  ['heading style', '[h2][b]a[b]b[/b][/b][/h2]', '## **ab**'],
  ['table cell style', '[table][tr][th][b]a[/b][b]b[/b][/th][/tr][tr][td][strike]c[strike]d[/strike][/strike][/td][/tr][/table]', '| **ab** |\n| --- |\n| ~~cd~~ |'],
];
for (const [name, source, expected] of formattingCases) {
  test(`GFM formatting preserves ${name}`, () => {
    const result = steamCommunityBbcodeToGfm(source);
    assert.deepEqual(target(result.value), target(expected));
    assert.deepEqual(result.diagnostics, []);
    assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'exact'));
  });
}

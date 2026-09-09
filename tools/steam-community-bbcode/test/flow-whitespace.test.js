// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {parseSteamCommunityBbcode, steamCommunityBbcodeToGfm, steamCommunityBbcodeToMdast} from '../src/index.js';

test('flow-whitespace between explicit blocks does not become duplicate paragraph padding', () => {
  const source = '[h1]Title[/h1]\n\nBody\n\n[hr][/hr]';
  const parsed = parseSteamCommunityBbcode(source);
  const originalSyntax = JSON.stringify(parsed);
  const mdast = steamCommunityBbcodeToMdast(parsed);
  const paragraph = mdast.value.children[1];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, 'Body');
  const result = steamCommunityBbcodeToGfm(source);
  assert.equal(result.value, '# Title\n\nBody\n\n***\n');
  assert.deepEqual(fromMarkdown(result.value).children.map(child => child.type), ['heading', 'paragraph', 'thematicBreak']);
  assert.deepEqual(result.diagnostics, []);
  assert.equal(JSON.stringify(parsed), originalSyntax);
});

test('flow-whitespace does not trim ordinary document text or inline separation', () => {
  const source = '  before  [b]bold[/b] [i]italic[/i]\nnext  ';
  const result = steamCommunityBbcodeToMdast(source);
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.deepEqual(paragraph.children.map(child => child.type === 'text' ? child.value : child.type),
    ['  before  ', 'strong', ' ', 'emphasis', '\nnext  ']);
});

for (const ending of ['\n', '\r\n', '\r']) {
  test(`flow-whitespace list delimiters preserve a tight list: ${JSON.stringify(ending)}`, () => {
    const source = ['[list]', '[*] One', '[*]\tTwo', '[/list]'].join(ending);
    const result = steamCommunityBbcodeToGfm(source);
    assert.equal(result.value, '* One\n* Two\n');
    const list = fromMarkdown(result.value).children[0];
    assert.ok(list?.type === 'list');
    assert.equal(list.spread, false);
    assert.equal(list.children.length, 2);
    assert.deepEqual(result.diagnostics, []);
  });
}

test('flow-whitespace retains nested list ownership and paragraphs within an item', () => {
  const source = '[list]\n[*] Parent\n[olist]\n[*] Child\n[/olist]\n[*] First paragraph.\n\nSecond paragraph.\n[/list]';
  const list = fromMarkdown(steamCommunityBbcodeToGfm(source).value).children[0];
  assert.ok(list?.type === 'list');
  assert.equal(list.children.length, 2);
  const nested = list.children[0]?.children[1];
  assert.ok(nested?.type === 'list');
  assert.equal(nested.ordered, true);
  assert.equal(nested.children.length, 1);
  assert.deepEqual(list.children[1]?.children.map(child => child.type), ['paragraph', 'paragraph']);
});

test('flow-whitespace keeps opaque padding, code content and malformed fallback', () => {
  const protectedText = '  [sd]  [Fixed]  ';
  const source = `[h1]Title[/h1]\n[noparse]${protectedText}[/noparse]\n[hr][/hr]`;
  const paragraph = fromMarkdown(steamCommunityBbcodeToGfm(source).value).children[1];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, protectedText);

  const codePayload = '\n  [b]raw[/b]  \n\n';
  const code = fromMarkdown(steamCommunityBbcodeToGfm(`[list]\n[*] [code]${codePayload}[/code]\n[/list]`).value).children[0];
  assert.ok(code?.type === 'list');
  const codeBlock = code.children[0]?.children[0];
  assert.ok(codeBlock?.type === 'code');
  assert.equal(codeBlock.value, codePayload);

  const malformed = '[code]  literal\n\n';
  const preserved = steamCommunityBbcodeToMdast('[h1]Title[/h1]\n' + malformed).value.children[1];
  assert.ok(preserved?.type === 'paragraph');
  assert.deepEqual(preserved.children.map(child => child.type === 'text' ? child.value : child.type), [malformed]);
});

test('flow-whitespace does not discard nonbreaking-space content', () => {
  const result = steamCommunityBbcodeToGfm('[h1]Title[/h1]\n\u00a0\n[hr][/hr]');
  const paragraph = fromMarkdown(result.value).children[1];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, '\u00a0');
});

test('flow-whitespace preserves nonbreaking prose before the first list marker', () => {
  const source = '[list]\u00a0[*]One[/list]';
  const result = steamCommunityBbcodeToGfm(source);
  assert.deepEqual(result.diagnostics.map(diagnostic => diagnostic.code), ['STEAM_LIST_CONTENT_PRESERVED']);
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, source);
});

test('flow-whitespace keeps literal fallback and source positions around a link', () => {
  const source = '[h1]Title[/h1]\r\n  [future]  raw\n[/future] [url=https://example.org] Label [/url]\r\n[hr][/hr]';
  const paragraph = steamCommunityBbcodeToMdast(source).value.children[1];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, '[future]  raw\n[/future]');
  const link = paragraph.children[2];
  assert.ok(link?.type === 'link' && link.position);
  assert.equal(source.slice(link.position.start.offset, link.position.end.offset), '[url=https://example.org] Label [/url]');
  const label = link.children[0];
  assert.ok(label?.type === 'text');
  assert.equal(label.value, ' Label ');
});

for (const separator of ['\u00a0', '\u2003']) {
  for (const boundary of ['table', 'row']) {
    test(`table layout preserves ${JSON.stringify(separator)} at its ${boundary} boundary`, () => {
      const header = '[tr][th]H[/th][/tr]';
      const row = '[tr][td]D[/td][/tr]';
      const positive = steamCommunityBbcodeToMdast(`[table] \t\r\n${header}\n${row}[/table]`);
      assert.equal(positive.value.children[0]?.type, 'table', 'the fixture must otherwise be a representable table');
      const source = boundary === 'table'
        ? `[table]${header}${separator}${row}[/table]`
        : `[table]${header}[tr]${separator}[td]D[/td][/tr][/table]`;
      const result = steamCommunityBbcodeToGfm(source);
      const paragraph = fromMarkdown(result.value).children[0];
      assert.ok(paragraph?.type === 'paragraph');
      assert.deepEqual(paragraph.children.map(child => child.type === 'text' ? child.value : child.type), [source]);
      assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_TABLE_STRUCTURE_PRESERVED'));
      assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'unsupported'));
    });
  }
}

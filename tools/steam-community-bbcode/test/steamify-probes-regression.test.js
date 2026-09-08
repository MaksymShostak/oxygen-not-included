// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

/** @type {{checks: {name: string, detail: string | {input: string}}[]}} */
const history = JSON.parse(await readFile(new URL('./fixtures/steamify/historical-results.json', import.meta.url), 'utf8'));
const probes = history.checks.filter(check => check.name.startsWith('probe: '));
assert.equal(probes.length, 17);

for (const probe of probes) {
  test(`Steamify ${probe.name}`, () => {
    assert.ok(typeof probe.detail === 'object');
    const result = steamCommunityBbcodeToGfm(probe.detail.input);
    const tree = fromMarkdown(result.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
    const all = [...nodes(tree)];
    const text = all.filter(node => node.type === 'text').map(node => node.value).join('');
    switch (probe.name) {
      case 'probe: nested unordered list': case 'probe: nested ordered list': {
        const list = tree.children[0];
        assert.ok(list?.type === 'list');
        assert.equal(list.ordered, probe.name === 'probe: nested ordered list');
        assert.equal(list.children.length, 1);
        const nested = list.children[0]?.children[1];
        assert.ok(nested?.type === 'list');
        assert.equal(nested.ordered, list.ordered);
        break;
      }
      case 'probe: image':
        assert.ok(all.some(node => node.type === 'image' && node.url === 'https://example.com/badge.png'));
        break;
      case 'probe: linked image': {
        const link = all.find(node => node.type === 'link');
        assert.ok(link?.type === 'link');
        assert.equal(link.url, 'https://example.com');
        assert.equal(link.children[0]?.type, 'image');
        break;
      }
      case 'probe: bare URL tag':
        assert.ok(all.some(node => node.type === 'link' && node.url === 'https://example.com'));
        assert.equal(text, 'https://example.com');
        break;
      case 'probe: inline list':
        assert.equal(all.filter(node => node.type === 'listItem').length, 2);
        break;
      case 'probe: multiline bold': {
        const strong = all.find(node => node.type === 'strong');
        assert.ok(strong?.type === 'strong');
        assert.equal(strong.children[0]?.type === 'text' ? strong.children[0].value : '', 'First\nsecond');
        break;
      }
      case 'probe: underline':
        assert.equal(text, 'Underlined');
        assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_UNDERLINE_LOWERED_TO_TEXT' && diagnostic.fidelity === 'lossy'));
        assert.ok(result.coverage.constructs.some(outcome => outcome.constructId === 'steam.bbcode.u' && outcome.fidelity === 'lossy'));
        break;
      case 'probe: spoiler':
        assert.ok(tree.children.some(node => node.type === 'html' && node.value.includes('<details>')));
        assert.equal(text, 'Secret');
        assert.ok(result.diagnostics.some(diagnostic => diagnostic.fidelity === 'approximate'));
        break;
      case 'probe: noparse':
        assert.equal(text, '[b]Literal[/b]');
        assert.equal(all.some(node => node.type === 'strong'), false);
        break;
      case 'probe: table':
        assert.equal(tree.children[0]?.type, 'table');
        assert.equal(all.filter(node => node.type === 'tableCell').length, 2);
        break;
      case 'probe: literal Markdown emphasis':
        assert.equal(text, 'Use *literal* asterisks.');
        assert.equal(all.some(node => node.type === 'emphasis'), false);
        break;
      case 'probe: inline code containing backticks': case 'probe: block code containing fences': {
        const code = all.find(node => node.type === 'code');
        assert.ok(code?.type === 'code');
        assert.equal(code.value, probe.name === 'probe: inline code containing backticks' ? 'a`b' : '\n```\ntext\n');
        break;
      }
      case 'probe: nested blockquotes':
        assert.equal(all.filter(node => node.type === 'blockquote').length, 2);
        break;
      case 'probe: horizontal rule': assert.equal(tree.children[0]?.type, 'thematicBreak'); break;
      case 'probe: strikethrough': assert.ok(all.some(node => node.type === 'delete')); assert.equal(text, 'Old'); break;
      default: assert.fail(`The frozen historical probe has no independent oracle: ${probe.name}`);
    }
  });
}

/** @param {import('mdast').Nodes} node @returns {Generator<import('mdast').Nodes>} */
function* nodes(node) {
  yield node;
  if ('children' in node) for (const child of node.children) yield* nodes(child);
}

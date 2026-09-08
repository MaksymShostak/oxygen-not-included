// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {LinkifyIt} from 'linkify-it';

test('the maintained plain-text recognizer reports Unicode offsets without parsing Markdown', () => {
  const scanner = new LinkifyIt({fuzzyLink: false, fuzzyEmail: false}).add('ftp:', null).add('mailto:', null).add('//', null);
  const text = '😀 *literal* (https://www.youtube.com/watch?v=tax4e4hBBZc), then https://store.steampowered.com/app/457140/.';
  const matches = scanner.match(text);
  assert.ok(matches);
  assert.deepEqual(matches.map(m => m.raw), ['https://www.youtube.com/watch?v=tax4e4hBBZc', 'https://store.steampowered.com/app/457140/']);
  assert.equal(matches[0]?.index, 14);
  assert.ok(matches.every(m => text.slice(m.index, m.lastIndex) === m.raw));
  assert.equal(scanner.match('foo@example.org example.org ftp://example.org javascript:alert(1)'), null);
});

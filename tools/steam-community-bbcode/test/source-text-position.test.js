// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {sourceTextPositionCursor} from '../src/steam/source-text-position-cursor.js';

test('successive text slices retain UTF-16 positions across CRLF, LF and CR', () => {
  const start = {line: 4, column: 3, offset: 20};
  const end = {line: 7, column: 6, offset: 33};
  const value = '😀\r\nA\nB\rC:ok:';
  const consume = sourceTextPositionCursor({type: 'steamText', value, rawSource: value, sourceSpan: {start, end}});
  assert.deepEqual(consume(0), {start, end: start});
  const emojiEnd = {line: 4, column: 5, offset: 22};
  assert.deepEqual(consume(2), {start, end: emojiEnd});
  const crlfEnd = {line: 5, column: 1, offset: 24};
  assert.deepEqual(consume(4), {start: emojiEnd, end: crlfEnd});
  const lfEnd = {line: 6, column: 1, offset: 26};
  assert.deepEqual(consume(6), {start: crlfEnd, end: lfEnd});
  const crEnd = {line: 7, column: 1, offset: 28};
  assert.deepEqual(consume(8), {start: lfEnd, end: crEnd});
  const last = consume(13);
  assert.deepEqual(last, {start: crEnd, end});
  assert.deepEqual(consume(13), {start: end, end});
  assert.deepEqual(last, {start: crEnd, end}, 'later consumption must not mutate previously returned positions');
  assert.deepEqual(start, {line: 4, column: 3, offset: 20}, 'the source span is immutable input');
});

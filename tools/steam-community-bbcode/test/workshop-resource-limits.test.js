// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

test('a Workshop caller can convert 8000 literal bracket bytes with its existing input limit', () => {
  const result = steamCommunityBbcodeToGfm('['.repeat(8000), {resourceLimits: {maxInputBytes: 8000}});
  assert.equal(result.value, '\\['.repeat(8000) + '\n');
  assert.equal(result.diagnostics.some(diagnostic => diagnostic.severity === 'error'), false);
});

test('the Workshop caller limit rejects oversized UTF-8 input with no partial conversion', () => {
  for (const source of ['['.repeat(8001), '😀'.repeat(2001)]) {
    const result = steamCommunityBbcodeToGfm(source, {resourceLimits: {maxInputBytes: 8000}});
    assert.equal(result.value, '');
    assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_INPUT_BYTES_EXCEEDED' && diagnostic.severity === 'error'));
  }
});

test('the general converter remains able to accept descriptions above the Workshop caller limit', () => {
  const source = 'x'.repeat(8001);
  assert.equal(steamCommunityBbcodeToGfm(source).value, source + '\n');
});

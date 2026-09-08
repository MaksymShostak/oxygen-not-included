// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

test('remote renderer policies are explicit without inventing observed occurrences', () => {
  const result = steamCommunityBbcodeToGfm('[b]A[/b]B and ordinary user text', {profile: 'discussion'});
  assert.equal(Array.isArray(result.coverage.contextOnlyPolicies), true);
  for (const id of ['steam.renderer.bracket-expansion', 'steam.renderer.word-filter']) {
    /** @type {import('../src/diagnostics/conversion-result.js').ContextOnlyRendererPolicy | undefined} */
    const contextPolicy = result.coverage.contextOnlyPolicies.find(p => p.constructId === id);
    assert.ok(contextPolicy);
    assert.equal(contextPolicy.fidelity, 'unsupported');
    assert.equal(contextPolicy.policy, 'preserve-source');
    assert.ok(contextPolicy.reason.length > 0);
    assert.ok(!result.coverage.constructs.some(c => c.constructId === id));
  }
});

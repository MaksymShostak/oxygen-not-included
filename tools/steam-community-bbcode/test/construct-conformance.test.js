// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import {constructCases} from './conformance/construct-cases.js';
import {executeConstructCase} from './conformance/execute-cases.js';
import assert from 'node:assert/strict';
import {steamConstructDefinitions} from '../src/steam/construct-registry.js';
import {steamDialectProfileIds} from '../src/steam/registry-identifiers.js';

for (const profile of steamDialectProfileIds) {
  for (const fixture of constructCases) test(`construct conformance: ${profile}/${fixture.id}`, () => { executeConstructCase(fixture, profile); });
}

test('every source-observable registry entry has an authored executable case', () => {
  const sourceIds = steamConstructDefinitions.filter(d => !('observation' in d && d.observation === 'contextOnly')).map(d => d.id);
  assert.deepEqual(new Set(constructCases.flatMap(c => c.constructIds)), new Set(sourceIds));
});

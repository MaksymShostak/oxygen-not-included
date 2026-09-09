// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {classifyResult} from '../scripts/comparison/classify-result.js';

const expectedTree = {type: 'root', children: [{type: 'paragraph', children: [{type: 'strong', children: [{type: 'text', value: 'B'}]}]}]};
/** @type {import('../scripts/comparison/classify-result.js').ComparisonObservation} */
const observation = {applicable: true, source: '[b]B[/b]', output: '**B**', expectedOutput: '**B**',
  expectedTree, actualTree: expectedTree, requiresDiagnosis: false, diagnostics: []};

test('comparison distinguishes exact bytes from equivalent target structure', () => {
  assert.equal(classifyResult(observation), 'PASS_EXACT');
  assert.equal(classifyResult({...observation, output: '__B__', actualTree: structuredClone(expectedTree)}), 'PASS_EQUIVALENT');
});

test('diagnostics account for known target loss but cannot excuse wrong structure', () => {
  assert.equal(classifyResult({...observation, requiresDiagnosis: true}), 'FAIL_SILENT_LOSS');
  assert.equal(classifyResult({...observation, requiresDiagnosis: true, diagnostics: ['underline removed']}), 'PASS_DIAGNOSED_LOSS');
  assert.equal(classifyResult({...observation, actualTree: {type: 'root', children: []}, diagnostics: ['warning']}), 'FAIL_WRONG_STRUCTURE');
});

test('unchanged source is raw markup only when target semantics are wrong', () => {
  assert.equal(classifyResult({...observation, output: observation.source, actualTree: {type: 'text', value: observation.source}}), 'FAIL_RAW_SOURCE_MARKUP');
  assert.equal(classifyResult({...observation, source: '**B**'}), 'PASS_EXACT');
});

test('unclaimed capability and execution errors remain separate from conversion failures', () => {
  assert.equal(classifyResult({...observation, error: 'TypeError: legacy dependency'}), 'FAIL_EXCEPTION');
  assert.equal(classifyResult({...observation, applicable: false, error: 'not executed'}), 'NOT_SUPPORTED_BY_PROJECT');
});

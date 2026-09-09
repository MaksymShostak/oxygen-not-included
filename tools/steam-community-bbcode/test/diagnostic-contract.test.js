// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToGfm, steamCommunityBbcodeToMdast, gfmToSteamCommunityBbcode} from '../src/index.js';
import {conversionDiagnosticCodes, steamParseDiagnosticCodes} from '../src/diagnostics/diagnostic-codes.js';
import {steamConstructIds} from '../src/steam/registry-identifiers.js';
import {constructCases} from './conformance/construct-cases.js';
import {reverseCases} from './conformance/reverse-cases.js';

test('diagnostic catalogues have unique nonempty identifiers and include parser outcomes', () => {
  for (const catalogue of [conversionDiagnosticCodes, steamParseDiagnosticCodes]) {
    assert.equal(new Set(catalogue).size, catalogue.length);
    assert.ok(catalogue.every(code => code.trim().length > 0));
  }
  assert.ok(steamParseDiagnosticCodes.every(code => conversionDiagnosticCodes.includes(code)));
});

test('conformance diagnostics identify an outcome and explain it to the caller', () => {
  /** @type {ReadonlySet<string>} */
  const knownCodes = new Set(conversionDiagnosticCodes);
  const conversions = [
    ...constructCases.flatMap(fixture => [steamCommunityBbcodeToGfm(fixture.source), steamCommunityBbcodeToGfm(fixture.malformed)]),
    ...constructCases.flatMap(fixture => [steamCommunityBbcodeToMdast(fixture.source), steamCommunityBbcodeToMdast(fixture.malformed)]),
    ...reverseCases.map(fixture => gfmToSteamCommunityBbcode(fixture.source)),
    steamCommunityBbcodeToGfm('XX', {resourceLimits: {maxInputBytes: 1}}),
    gfmToSteamCommunityBbcode('XX', {resourceLimits: {maxInputBytes: 1}}),
  ];
  const diagnostics = conversions.flatMap(result => result.diagnostics);
  assert.ok(diagnostics.some(d => d.scope === 'input'));
  assert.ok(diagnostics.some(d => d.scope === 'construct'));
  assert.ok(diagnostics.some(d => d.scope === 'gfm-node'));
  for (const diagnostic of diagnostics) {
    assert.ok(knownCodes.has(diagnostic.code), `Unknown emitted diagnostic: ${diagnostic.code}`);
    assert.notEqual(diagnostic.message.trim(), '', `Missing explanation: ${diagnostic.code}`);
    assert.ok(['info', 'warning', 'error'].includes(diagnostic.severity));
    assert.ok(['exact', 'equivalent', 'approximate', 'lossy', 'unsupported'].includes(diagnostic.fidelity));
    assert.ok(['input', 'construct', 'gfm-node'].includes(diagnostic.scope));
    if (diagnostic.scope === 'construct') {
      assert.ok(diagnostic.constructId === 'steam.bbcode.unknown' || steamConstructIds.includes(diagnostic.constructId));
      assert.ok(diagnostic.sourceSpan);
    }
    if (diagnostic.scope === 'gfm-node') assert.notEqual(diagnostic.nodeType, '');
  }
  for (const result of conversions) {
    for (const outcome of result.coverage.constructs) {
      assert.ok(outcome.constructId === 'steam.bbcode.unknown' || steamConstructIds.includes(outcome.constructId));
      assert.ok(['exact', 'equivalent', 'approximate', 'lossy', 'unsupported'].includes(outcome.fidelity));
      assert.ok(outcome.sourceSpan);
    }
  }
});

test('resource exhaustion is an input error and malformed syntax is a warning', () => {
  for (const convert of [steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm]) {
    const exceeded = convert('XX', {resourceLimits: {maxInputBytes: 1}});
    assert.deepEqual(exceeded.diagnostics.map(d => [d.code, d.scope, d.severity, d.fidelity]),
      [['STEAM_MAX_INPUT_BYTES_EXCEEDED', 'input', 'error', 'unsupported']]);
    const malformed = convert('[b]unfinished');
    const inputDiagnostic = malformed.diagnostics.find(d => d.scope === 'input');
    assert.ok(inputDiagnostic);
    assert.deepEqual([inputDiagnostic.code, inputDiagnostic.severity, inputDiagnostic.fidelity],
      ['STEAM_UNCLOSED_TAG', 'warning', 'unsupported']);
  }
});

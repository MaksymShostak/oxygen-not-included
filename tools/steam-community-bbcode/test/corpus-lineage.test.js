// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import test from 'node:test';

test('all 33 historical checks remain identifiable independently of new conversion results', async () => {
  /** @type {{steamify: string, checks: Array<{name: string, pass: boolean, detail: string | {input: string}}>}} */
  const history = JSON.parse(await readFile(new URL('./fixtures/steamify/historical-results.json', import.meta.url), 'utf8'));
  assert.equal(history.steamify, '2.0.1');
  assert.equal(history.checks.length, 33);
  assert.equal(new Set(history.checks.map(check => check.name)).size, 33);
  const actual = history.checks.filter(check => check.name.startsWith('actual: '));
  const probes = history.checks.filter(check => check.name.startsWith('probe: '));
  assert.equal(actual.length, 16);
  assert.equal(actual.filter(check => check.pass).length, 16);
  assert.equal(probes.length, 17);
  assert.equal(probes.filter(check => check.pass).length, 6);
  for (const probe of probes) {
    assert.equal(typeof probe.detail, 'object');
    assert.ok(typeof probe.detail !== 'string' && probe.detail.input.length > 0);
  }
});

test('independently authored description exercises the same structural preconditions', async () => {
  const source = await readFile(new URL('./fixtures/steamify/description.bbcode', import.meta.url), 'utf8');
  for (const [token, expected] of [['[h1]', 6], ['[b]', 29], ['[i]', 5], ['[list]', 4], ['[*]', 18]]) {
    assert.equal(source.split(String(token)).length - 1, expected);
  }
  assert.ok(source.includes('[url=FAST_TRACK_LINK][i]Fast Track[/i][/url]'));
  for (const literal of ['[sd]', '[Fixed]', '[keep existing Base Game + DLC compatibility badge row]', '°C', '—']) {
    assert.ok(source.includes(literal));
  }
});

#!/usr/bin/env node
// SPDX-License-Identifier: AGPL-3.0-only
import {createReadStream, writeFileSync} from 'node:fs';
import {readFile} from 'node:fs/promises';
import {parseArgs} from 'node:util';
import {steamCommunityBbcodeToGfm, gfmToSteamCommunityBbcode} from './index.js';
import {steamDialectProfileIds, defaultSteamDialectProfile} from './steam/registry-identifiers.js';
import {defaultSteamParseResourceLimits} from './security/resource-limits.js';

const help = `Usage: steam-community-bbcode COMMAND [OPTIONS] [FILE|-]
  to-gfm     Convert Steam Community BBCode to GFM.
  to-steam   Convert the supported GFM subset to Steam (partial).
  coverage   Print the shipped conformance report as JSON.
  --format=text|json   Output text (default) or the public result model.
  --fail-on=none|approximate|lossy|unsupported   Reject this fidelity or worse.
  --profile=PROFILE   Select a forward Steam profile (default: workshop-item).
  --max-input-bytes=N   Bound UTF-8 input bytes (default: 1048576).
  --help              Show this help.

Omit FILE or use - for stdin. Conversion diagnostics are JSON lines on stderr.
to-steam is partial: unsupported GFM is preserved literally and diagnosed.
Exit: 0 accepted; 1 conversion/fidelity failure; 2 argument/I/O/UTF-8 failure.
Fidelity failures retain converted output; check status before adopting it.
`;

/** @param {string | undefined} path @param {number} maxInputBytes */
async function readSource(path, maxInputBytes) {
  const stream = path === undefined || path === '-' ? process.stdin : createReadStream(path);
  /** @type {Buffer[]} */
  const chunks = [];
  let byteLength = 0;
  /** @type {AsyncIterable<unknown, undefined, undefined>} */
  const input = stream;
  for await (const chunk of input) {
    if (!Buffer.isBuffer(chunk)) throw new TypeError('Expected raw input bytes.');
    byteLength += chunk.length;
    if (byteLength > maxInputBytes) throw new RangeError(`CLI input exceeds the ${maxInputBytes} byte limit.`);
    chunks.push(chunk);
  }
  return new TextDecoder('utf-8', {fatal: true}).decode(Buffer.concat(chunks, byteLength));
}

async function main() {
  const {values, positionals} = parseArgs({allowPositionals: true, options: {
    help: {type: 'boolean'}, format: {type: 'string'}, profile: {type: 'string'},
    'fail-on': {type: 'string'}, 'max-input-bytes': {type: 'string'},
  }});
  if (values.help) { writeFileSync(1, help); return 0; }
  const [command, path] = positionals;
  if (command === 'coverage') {
    if (positionals.length !== 1 || Object.keys(values).some(key => key !== 'format')
        || (values.format !== undefined && values.format !== 'json')) throw new TypeError('Use coverage --format=json without conversion options or an input file.');
    writeFileSync(1, await readFile(new URL('../coverage.json', import.meta.url), 'utf8'));
    return 0;
  }
  if (command !== 'to-gfm' && command !== 'to-steam') throw new TypeError('Expected to-gfm or to-steam; use --help.');
  if (positionals.length > 2) throw new TypeError('Expected at most one input file.');
  const format = values.format ?? 'text';
  if (format !== 'text' && format !== 'json') throw new TypeError('Expected --format=text or --format=json.');
  const profile = steamDialectProfileIds.find(id => id === (values.profile ?? defaultSteamDialectProfile));
  if (profile === undefined) throw new TypeError('Unknown Steam profile.');
  if (command === 'to-steam' && values.profile !== undefined) throw new TypeError('--profile applies only to to-gfm.');
  const failurePolicy = values['fail-on'] ?? 'none';
  /** @type {Record<import('./index.js').ConversionFidelity, number>} */
  const fidelityRanks = {exact: 0, equivalent: 0, approximate: 1, lossy: 2, unsupported: 3};
  let failureRank = Infinity;
  if (failurePolicy === 'approximate' || failurePolicy === 'lossy' || failurePolicy === 'unsupported') failureRank = fidelityRanks[failurePolicy];
  else if (failurePolicy !== 'none') throw new TypeError('Expected --fail-on=none, approximate, lossy or unsupported.');
  const maxInputBytes = values['max-input-bytes'] === undefined ? defaultSteamParseResourceLimits.maxInputBytes : Number(values['max-input-bytes']);
  if (!Number.isSafeInteger(maxInputBytes) || maxInputBytes < 1) throw new RangeError('--max-input-bytes must be a positive safe integer.');
  const source = await readSource(path, maxInputBytes);
  const result = command === 'to-gfm' ? steamCommunityBbcodeToGfm(source, {profile, resourceLimits: {maxInputBytes}})
    : gfmToSteamCommunityBbcode(source, {resourceLimits: {maxInputBytes}});
  // Native descriptor writes finish before exit and report pipe errors through
  // the same operational-error boundary, without unhandled stream events.
  writeFileSync(1, format === 'json' ? `${JSON.stringify(result)}\n` : result.value);
  for (const diagnostic of result.diagnostics) writeFileSync(2, `${JSON.stringify(diagnostic)}\n`);
  return result.diagnostics.some(diagnostic => diagnostic.severity === 'error' || fidelityRanks[diagnostic.fidelity] >= failureRank) ? 1 : 0;
}

try { process.exitCode = await main(); }
catch (error) {
  process.exitCode = 2;
  try {
    writeFileSync(2, `${JSON.stringify({code: 'CLI_ERROR', message: error instanceof Error ? error.message : String(error)})}\n`);
  } catch {
    // A closed diagnostic pipe cannot receive an error; retain the I/O status.
  }
}

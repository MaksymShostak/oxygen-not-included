// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {spawn, spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import test from 'node:test';

/** @param {string[]} args @param {string | Buffer} [input] */
function cli(args, input = '') {
  const result = spawnSync(process.execPath, [fileURLToPath(new URL('../src/cli.js', import.meta.url)), ...args],
    {input, encoding: 'utf8', windowsHide: true, timeout: 10000});
  assert.equal(result.error, undefined);
  return result;
}

test('CLI converts stdin without mixing diagnostic metadata into Markdown', () => {
  const result = cli(['to-gfm'], '[b]B[/b]');
  assert.equal(result.status, 0);
  assert.equal(result.stdout, '**B**\n');
  assert.equal(result.stderr, '');
});

test('CLI makes reverse limitations visible and returns unsupported source diagnostics', () => {
  const help = cli(['--help']);
  assert.equal(help.status, 0);
  assert.match(help.stdout, /to-steam.*partial/iu);
  const result = cli(['to-steam', '--format=json'], '#### H');
  assert.equal(result.status, 0);
  /** @type {{value: string, unsupportedSourceNodes: {nodeType: string}[]}} */
  const parsed = JSON.parse(result.stdout);
  assert.equal(parsed.value, '[noparse]#### H[/noparse]');
  assert.equal(parsed.unsupportedSourceNodes[0]?.nodeType, 'heading');
  assert.match(result.stderr, /GFM_NODE_UNSUPPORTED_PRESERVED/u);
});

test('CLI accepts a spaced file path after the option terminator and explicit stdin', () => {
  const path = fileURLToPath(new URL('./fixtures/cli/- source.bbcode', import.meta.url));
  const file = cli(['to-gfm', '--', path]);
  assert.equal(file.status, 0);
  assert.equal(file.stdout, '**B**\n');
  const stdin = cli(['to-gfm', '-'], '[b]B[/b]');
  assert.equal(stdin.status, 0);
  assert.equal(stdin.stdout, file.stdout);
});

test('CLI fidelity thresholds reject the selected loss or worse while retaining the result', () => {
  const lossy = cli(['to-gfm', '--fail-on=lossy'], '[u]U[/u]');
  assert.equal(lossy.status, 1);
  assert.equal(lossy.stdout, 'U\n');
  assert.match(lossy.stderr, /STEAM_UNDERLINE_LOWERED_TO_TEXT/u);
  assert.equal(cli(['to-gfm', '--fail-on=unsupported'], '[u]U[/u]').status, 0);
  assert.equal(cli(['to-gfm', '--fail-on=lossy'], '[spoiler]S[/spoiler]').status, 0);
  assert.equal(cli(['to-gfm', '--fail-on=approximate'], '[spoiler]S[/spoiler]').status, 1);
  const unsupported = cli(['to-steam', '--fail-on=lossy', '--format=json'], '#### H');
  assert.equal(unsupported.status, 1);
  assert.match(unsupported.stdout, /unsupportedSourceNodes/u);
});

test('CLI bounds UTF-8 input bytes before conversion and rejects malformed encoding', () => {
  const bounded = cli(['to-gfm', '--max-input-bytes=3'], '💙');
  assert.equal(bounded.status, 2);
  assert.equal(bounded.stdout, '');
  assert.match(bounded.stderr, /input.*byte/iu);
  const exact = cli(['to-gfm', '--max-input-bytes=4'], '💙');
  assert.equal(exact.status, 0);
  assert.equal(exact.stdout, '💙\n');
  const invalid = cli(['to-gfm'], Buffer.from([0xff]));
  assert.equal(invalid.status, 2);
  assert.equal(invalid.stdout, '');
  assert.match(invalid.stderr, /encod/iu);
});

test('CLI emits the shipped conformance report and selected forward profile', () => {
  const coverage = cli(['coverage', '--format=json']);
  assert.equal(coverage.status, 0);
  /** @type {{registryConstructCount: number, reverse: {executedCaseCount: number}}} */
  const report = JSON.parse(coverage.stdout);
  assert.equal(report.registryConstructCount, 39);
  assert.equal(report.reverse.executedCaseCount, 33);
  assert.equal(coverage.stderr, '');
  const conversion = cli(['to-gfm', '--profile=discussion', '--format=json'], '[b]B[/b]');
  assert.equal(conversion.status, 0);
  /** @type {{coverage: {profile: string}}} */
  const result = JSON.parse(conversion.stdout);
  assert.equal(result.coverage.profile, 'discussion');
});

for (const args of [[], ['unknown'], ['to-gfm', '--unknown'], ['to-gfm', 'one', 'two'],
  ['to-gfm', '--format=xml'], ['to-gfm', '--profile=typo'], ['to-steam', '--profile=workshop-item'],
  ['to-gfm', '--fail-on=typo'], ['to-gfm', '--max-input-bytes=0'], ['to-gfm', '--max-input-bytes=1.5'],
  ['coverage', 'unexpected.bbcode'], ['coverage', '--fail-on=lossy'], ['coverage', '--format=text']]) {
  test(`CLI rejects invalid invocation ${JSON.stringify(args)}`, () => {
    const result = cli(args, '[b]B[/b]');
    assert.equal(result.status, 2);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /CLI_ERROR/u);
  });
}

test('CLI reports missing files without a stack trace or partial output', () => {
  const result = cli(['to-gfm', fileURLToPath(new URL('./fixtures/cli/does-not-exist.bbcode', import.meta.url))]);
  assert.equal(result.status, 2);
  assert.equal(result.stdout, '');
  assert.match(result.stderr, /ENOENT/u);
  assert.doesNotMatch(result.stderr, /at main/u);
});

test('CLI reports a closed output pipe as an I/O failure without an unhandled stack', {timeout: 10000}, async () => {
  const child = spawn(process.execPath, [fileURLToPath(new URL('../src/cli.js', import.meta.url)), 'to-gfm'], {windowsHide: true});
  let stderr = '';
  child.stderr.setEncoding('utf8');
  child.stderr.on('data', /** @param {string} chunk */ chunk => { stderr += chunk; });
  child.stdout.destroy();
  child.stdin.end('[b]B[/b]');
  /** @type {number | null} */
  const status = await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('close', code => resolve(code));
  });
  assert.equal(status, 2);
  assert.match(stderr, /CLI_ERROR/u);
  assert.doesNotMatch(stderr, /Unhandled|at main/u);
});

test('CLI retains an I/O failure exit code when the diagnostic pipe is also closed', {timeout: 10000}, async () => {
  const child = spawn(process.execPath, [fileURLToPath(new URL('../src/cli.js', import.meta.url)), 'to-gfm'], {windowsHide: true});
  child.stdout.resume();
  child.stderr.destroy();
  child.stdin.end('[u]U[/u]');
  /** @type {number | null} */
  const status = await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('close', code => resolve(code));
  });
  assert.equal(status, 2);
});

test('CLI always fails on a conversion resource error, independently of fidelity policy', () => {
  const source = '[b]'.repeat(129) + 'X' + '[/b]'.repeat(129);
  const result = cli(['to-gfm', '--fail-on=none', '--format=json'], source);
  assert.equal(result.status, 1);
  /** @type {{value: string, diagnostics: {severity: string}[]}} */
  const parsed = JSON.parse(result.stdout);
  assert.equal(parsed.value, '');
  assert.ok(parsed.diagnostics.some(diagnostic => diagnostic.severity === 'error'));
});

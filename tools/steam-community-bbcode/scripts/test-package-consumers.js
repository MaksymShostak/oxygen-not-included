// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, readFile, readdir, writeFile, lstat, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename, dirname, join, resolve, sep} from 'node:path';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const npmCli = process.env['npm_execpath'];
assert.ok(npmCli, 'Use npm run test:package.');
const artifacts = join(root, 'artifacts');
await mkdir(artifacts, {recursive: true});

/** @param {string[]} args @param {string} cwd @param {string} [input] */
function run(args, cwd, input = '') {
  const result = spawnSync(process.execPath, args,
    {cwd, input, encoding: 'utf8', windowsHide: true});
  assert.equal(result.status, 0, result.error?.message ?? result.stdout + result.stderr);
  return result.stdout;
}

run([npmCli, 'run', 'build'], root);
/** @type {unknown} */
const packed = JSON.parse(run([npmCli, 'pack', '--ignore-scripts', '--json', '--pack-destination', artifacts], root));
assert.ok(packed && typeof packed === 'object' && !Array.isArray(packed));
assert.deepEqual(Object.keys(packed), ['steam-community-bbcode']);
assert.ok('steam-community-bbcode' in packed);
/** @type {unknown} */
const entry = packed['steam-community-bbcode'];
assert.ok(entry && typeof entry === 'object' && 'filename' in entry && typeof entry.filename === 'string');
assert.equal(basename(entry.filename), entry.filename);
const archive = join(artifacts, entry.filename);
const consumer = await mkdtemp(join(tmpdir(), 'steam-bbcode-consumer-'));
try {
  await writeFile(join(consumer, 'package.json'), JSON.stringify({private: true, type: 'module'}));
  run([npmCli, 'install', '--ignore-scripts', '--omit=dev', '--no-audit', '--no-fund', archive], consumer);
  const installed = join(consumer, 'node_modules', 'steam-community-bbcode');
  /** @type {{license: string, scripts?: Record<string, string>, bin?: Record<string, string>}} */
  const manifest = JSON.parse(await readFile(join(installed, 'package.json'), 'utf8'));
  assert.equal(manifest.license, 'AGPL-3.0-only');
  assert.ok((await readFile(join(installed, 'LICENSE'), 'utf8')).includes('GNU AFFERO GENERAL PUBLIC LICENSE'));
  for (const hook of ['preinstall', 'install', 'postinstall', 'prepare']) {
    assert.equal(manifest.scripts?.[hook], undefined, 'Consumers must not compile during installation.');
  }
  for (const excluded of ['test', 'scripts', 'comparison', 'node_modules']) {
    assert.equal(await lstat(join(installed, excluded)).catch(() => undefined), undefined);
  }
  const mapNames = (await readdir(join(installed, 'types'), {recursive: true})).filter(name => name.endsWith('.map'));
  assert.ok(mapNames.length > 0);
  for (const name of mapNames) {
    const mapPath = join(installed, 'types', name);
    /** @type {{sources: string[], sourceRoot?: string}} */
    const map = JSON.parse(await readFile(mapPath, 'utf8'));
    for (const source of map.sources) {
      const target = resolve(dirname(mapPath), map.sourceRoot ?? '', source);
      assert.ok(target.startsWith(join(installed, 'src') + sep));
      assert.ok((await lstat(target)).isFile(), 'Declaration maps must resolve inside the shipped source.');
    }
  }
  // Execute the authored consumer against the archive's actual runtime exports.
  await writeFile(join(consumer, 'consumer.js'), await readFile(new URL('../test/package-consumers/javascript/public-api.js', import.meta.url)));
  run(['consumer.js'], consumer);
  assert.equal(manifest.bin?.['steam-community-bbcode'], 'src/cli.js');
  assert.ok((await readFile(join(installed, 'src', 'cli.js'), 'utf8')).startsWith('#!/usr/bin/env node\n'));
  const installedHelp = run([npmCli, 'exec', '--offline', '--', 'steam-community-bbcode', '--help'], consumer);
  assert.match(installedHelp, /to-steam.*partial/iu);
  const installedConversion = run([npmCli, 'exec', '--offline', '--', 'steam-community-bbcode', 'to-gfm'], consumer, '[b]B[/b]');
  assert.equal(installedConversion, '**B**\n');
  /** @type {{registryConstructCount: number}} */
  const installedCoverage = JSON.parse(run([npmCli, 'exec', '--offline', '--', 'steam-community-bbcode', 'coverage', '--format=json'], consumer));
  assert.equal(installedCoverage.registryConstructCount, 39);
  const contractRoot = new URL('../test/type-contracts/', import.meta.url);
  const contracts = (await readdir(contractRoot)).filter(name => name.endsWith('.mts'));
  assert.ok(contracts.length > 0);
  for (const name of contracts) {
    await writeFile(join(consumer, name), await readFile(new URL(name, contractRoot)));
  }
  await writeFile(join(consumer, 'tsconfig.json'), JSON.stringify({compilerOptions: {
    strict: true, exactOptionalPropertyTypes: true, noEmit: true, module: 'NodeNext', moduleResolution: 'NodeNext', types: [],
  }, include: ['*.mts']}));
  // The publisher provides the compiler; type resolution occurs in the isolated
  // consumer, where only tarball dependencies exist. There is no workspace hoist.
  run([join(root, 'node_modules', 'typescript', 'bin', 'tsc'), '--project', join(consumer, 'tsconfig.json')], consumer);
  console.log(`Installed JavaScript/type consumer smoke passed: ${archive}`);
} finally {
  const target = resolve(consumer);
  assert.equal(dirname(target), resolve(tmpdir()));
  assert.ok(basename(target).startsWith('steam-bbcode-consumer-'));
  await rm(target, {recursive: true});
}

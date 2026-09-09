// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {assertPublicRegistryFacts, formatSha256Sums, sha256Buffer, verifyIntegrity} from '../scripts/release-artifacts.js';

const digest = 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad';
const integrity = 'sha512-3a81oZNherrMQXNJriBBMRLm+k6JqX6iCp7u5ktV05ohkpkqJ0/BqDa6PCOj/uu9RU1EI2Q86A4qmslPpUyknw==';
const facts = {
  expectedName: 'steam-community-bbcode', expectedVersion: '1.1.0-rc.1', tag: 'next',
  retainedSha256: digest, registryTarballSha256: digest,
  metadata: {name: 'steam-community-bbcode', version: '1.1.0-rc.1', dist: {
    integrity, tarball: 'https://registry.npmjs.org/steam-community-bbcode/-/steam-community-bbcode-1.1.0-rc.1.tgz',
  }},
  distTags: {latest: '1.0.0', next: '1.1.0-rc.1'},
};

test('registry qualification permits an existing stable channel alongside the requested prerelease', () => {
  assert.deepEqual(assertPublicRegistryFacts(facts), {
    coordinate: 'steam-community-bbcode@1.1.0-rc.1', channel: 'next', integrity, tarballSha256: digest, tarballUrl: facts.metadata.dist.tarball,
  });
  assert.equal(assertPublicRegistryFacts({...facts, tag: 'latest', distTags: {latest: facts.expectedVersion, next: '2.0.0-rc.1'}}).channel, 'latest');
});

test('registry qualification rejects a different coordinate, tag target or archive', () => {
  for (const metadata of [{...facts.metadata, name: 'different-package'}, {...facts.metadata, version: '1.1.0-rc.2'}]) {
    assert.throws(() => assertPublicRegistryFacts({...facts, metadata}), /wrong package coordinate/u);
  }
  for (const distTags of [{next: '1.0.0'}, {}]) {
    assert.throws(() => assertPublicRegistryFacts({...facts, distTags}), /distribution tag/u);
  }
  assert.throws(() => assertPublicRegistryFacts({...facts, registryTarballSha256: 'different'}), /bytes differ/u);
});

test('registry distribution locations stay inside the exact public package origin', () => {
  for (const tarball of ['https://registry.npmjs.org.evil.example/file.tgz', 'http://registry.npmjs.org/steam-community-bbcode/-/x.tgz',
    'https://registry.npmjs.org/other-package/-/x.tgz']) {
    assert.throws(() => assertPublicRegistryFacts({...facts, metadata: {...facts.metadata, dist: {...facts.metadata.dist, tarball}}}), /distribution metadata/u);
  }
});

test('archive hashes and SRI use independent standard abc vectors and reject changed bytes', () => {
  assert.equal(sha256Buffer(Buffer.from('abc')), digest);
  verifyIntegrity(Buffer.from('abc'), integrity);
  assert.throws(() => verifyIntegrity(Buffer.from('abd'), integrity), /fails.*integrity/u);
  assert.throws(() => verifyIntegrity(Buffer.from('abc'), `sha256-${digest}`), /sha512 SRI/u);
});

test('the candidate checksum file is deterministic and consumable by sha256sum', () => {
  assert.equal(formatSha256Sums([{fileName: 'z.tgz', sha256: digest}, {fileName: 'a.json', sha256: digest}]),
    `${digest}  a.json\n${digest}  z.tgz\n`);
});

// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {access, readFile, readdir} from 'node:fs/promises';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamConstructDefinitions} from '../src/steam/construct-registry.js';
import {constructCases} from './conformance/construct-cases.js';

test('each construct has an executed example or an explicit context-only explanation', async () => {
  const source = await readFile(new URL('../docs/conversion-semantics.md', import.meta.url), 'utf8');
  const tree = fromMarkdown(source);
  const headings = tree.children.filter(node => node.type === 'heading').filter(node => node.depth === 2);
  assert.deepEqual(headings.map(heading => heading.children.map(child => child.type === 'text' ? child.value : '').join('')),
    steamConstructDefinitions.map(definition => definition.id));
  for (const definition of steamConstructDefinitions) {
    const index = tree.children.findIndex(node => node.type === 'heading' && node.children.some(child => child.type === 'text' && child.value === definition.id));
    const next = tree.children.findIndex((node, candidate) => candidate > index && node.type === 'heading' && node.depth === 2);
    const section = tree.children.slice(index + 1, next < 0 ? undefined : next);
    if ('observation' in definition && definition.observation === 'contextOnly') {
      assert.ok(section.some(node => node.type === 'paragraph' && node.children.some(child => child.type === 'text' && child.value.includes(definition.contextReason))));
      assert.ok(!section.some(node => node.type === 'code'), 'remote behavior cannot acquire invented source syntax');
    } else {
      const fixture = constructCases.find(candidate => candidate.constructIds.includes(definition.id));
      assert.ok(fixture);
      assert.ok(section.some(node => node.type === 'code' && node.lang === 'bbcode' && node.value === fixture.source));
      assert.ok(section.some(node => node.type === 'code' && node.lang === 'markdown'));
    }
  }
});

test('package-local documentation links resolve to real files', async () => {
  const packageUrl = new URL('../', import.meta.url);
  const docs = await readdir(new URL('docs/', packageUrl), {recursive: true});
  const paths = ['README.md', 'CONTRIBUTING.md', 'CHANGELOG.md', 'SECURITY.md',
    ...docs.filter(path => path.endsWith('.md')).map(path => `docs/${path.replaceAll('\\', '/')}`)];
  let checked = 0;
  for (const path of paths) {
    const documentUrl = new URL(path, packageUrl);
    const tree = fromMarkdown(await readFile(documentUrl, 'utf8'), {extensions: [gfm()], mdastExtensions: gfmFromMarkdown()});
    /** @type {import('mdast').Nodes[]} */
    const pending = [tree];
    while (pending.length) {
      const node = pending.pop();
      assert.ok(node);
      if ('children' in node) pending.push(...node.children);
      if (node.type !== 'link' && node.type !== 'image' && node.type !== 'definition') continue;
      const destination = new URL(node.url, documentUrl);
      if (destination.protocol !== 'file:') continue;
      // Source-checkout plan references outside the package are not shipped.
      // This contract also runs in the package-only mutation sandbox.
      if (!destination.href.startsWith(packageUrl.href)) continue;
      await assert.doesNotReject(access(destination), `${path}: ${node.url}`);
      checked++;
    }
  }
  assert.ok(checked > 0, 'the documentation must exercise actual local links');
});

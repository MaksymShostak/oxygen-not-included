// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {mkdir, readFile, readdir, unlink, writeFile} from 'node:fs/promises';
import {dirname, join, relative} from 'node:path';
import {fileURLToPath} from 'node:url';
import {Application, ReferenceType, ReflectionType} from 'typedoc';

const packageRoot = new URL('../../', import.meta.url);
const options = fileURLToPath(new URL('typedoc.json', import.meta.url));
const update = process.argv.slice(2);
assert.ok(update.length === 0 || (update.length === 1 && update[0] === '--check'));

/** Generate with the supported native application and preserve its diagnostics.
 * @param {import('typedoc').TypeDocOptions} overrides
 */
async function generate(overrides) {
  const app = await Application.bootstrapWithPlugins({options, ...overrides});
  const project = await app.convert();
  assert.ok(project, 'TypeDoc must produce a native project.');
  app.validate(project);
  assert.ok(!app.logger.hasErrors() && !app.logger.hasWarnings(), 'TypeDoc diagnostics must be clean.');
  await app.generateOutputs(project);
  assert.ok(!app.logger.hasErrors() && !app.logger.hasWarnings(), 'Native rendering must be clean.');
  return project;
}

const fixtureRoot = new URL('artifacts/api-documentation-fixture/', packageRoot);
const fixture = await generate({
  entryPoints: ['api.js', 'model.js'].map(name => fileURLToPath(new URL(`test/documentation-fixtures/${name}`, packageRoot)).replaceAll('\\', '/')),
  out: fileURLToPath(new URL('markdown/', fixtureRoot)),
  json: fileURLToPath(new URL('model.json', fixtureRoot)),
});
const callable = fixture.getChildByName(['api', 'envelope']);
assert.ok(callable?.isDeclaration());
const signature = callable.signatures?.[0];
assert.ok(signature);
assert.ok(signature.type instanceof ReferenceType);
assert.equal(signature.type.name, 'Envelope');
const argument = signature.type.typeArguments?.[0];
assert.ok(argument instanceof ReferenceType);
assert.equal(argument.reflection, signature.typeParameters?.[0], 'The result must retain the caller generic.');
const definition = signature.type.reflection;
assert.ok(definition?.isDeclaration() && definition.type instanceof ReflectionType);
const value = definition.type.declaration.getChildByName('value');
assert.ok(value?.isDeclaration() && value.type instanceof ReferenceType);
assert.equal(value.type.reflection, definition.typeParameters?.[0], 'The imported value must retain the alias generic.');
assert.ok(signature.comment?.summary.some(part => part.text.includes('Retain the input')));
const renderedFixture = await readFile(new URL('markdown/api.md', fixtureRoot), 'utf8');
assert.match(renderedFixture, /Envelope/);
assert.match(renderedFixture, /Retain the input in a labelled envelope/);
assert.match(renderedFixture, /The same generic value and a label/);
assert.match(await readFile(new URL('markdown/model.md', fixtureRoot), 'utf8'), /\*\*value\*\*: `T`/);

const reference = await generate({});
const sourceSpan = reference.getChildByName(['index', 'SourceSpan']);
assert.ok(sourceSpan?.isDeclaration(), 'The exported native source-span contract must be documented.');
assert.deepEqual(sourceSpan.children?.map(child => child.name).sort(), ['end', 'start']);
assert.ok(sourceSpan.getChildByName('start')?.isDeclaration());
const generatedRoot = new URL('artifacts/api-reference/markdown/', packageRoot);
const referenceRoot = new URL('docs/reference/', packageRoot);

/** @param {URL} root */
async function files(root) {
  return (await readdir(root, {recursive: true, withFileTypes: true}))
    .filter(entry => entry.isFile())
    .map(entry => relative(fileURLToPath(root), join(entry.parentPath, entry.name)).replaceAll('\\', '/'))
    .sort();
}

const names = await files(generatedRoot);
assert.ok(names.length > 1 && names.every(name => name.endsWith('.md')));
if (update[0] === '--check') {
  assert.deepEqual(await files(referenceRoot), names, 'Generated API documentation file inventory is stale.');
  for (const name of names) {
    assert.equal(await readFile(new URL(name, referenceRoot), 'utf8'),
      await readFile(new URL(name, generatedRoot), 'utf8'), `Generated API documentation is stale: ${name}`);
  }
} else {
  await mkdir(referenceRoot, {recursive: true});
  for (const name of await files(referenceRoot)) {
    assert.ok(name.endsWith('.md'), 'Only generated Markdown belongs in the reference folder.');
    if (!names.includes(name)) await unlink(new URL(name, referenceRoot));
  }
  for (const name of names) {
    const destination = new URL(name, referenceRoot);
    await mkdir(dirname(fileURLToPath(destination)), {recursive: true});
    await writeFile(destination, await readFile(new URL(name, generatedRoot)));
  }
}
console.log(`Qualified imported generic JSDoc and ${names.length} native API reference files.`);

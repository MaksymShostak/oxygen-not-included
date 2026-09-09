// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {parseSteamCommunityBbcode, gfmToSteamCommunityBbcode, steamCommunityBbcodeToGfm, steamCommunityBbcodeToMdast} from '../src/index.js';

for (const convert of [parseSteamCommunityBbcode, gfmToSteamCommunityBbcode]) {
  test(`${convert.name} rejects invalid resource options with actionable caller errors`, () => {
    for (const resourceLimits of [null, [], 0, 1, true, false, '', 'limits']) {
      assert.throws(() => Reflect.apply(convert, undefined, ['', {resourceLimits}]),
        {name: 'TypeError', message: /Resource limits must be an object/});
    }
    assert.throws(() => Reflect.apply(convert, undefined, ['', {resourceLimits: {unknown: 1}}]),
      {name: 'TypeError', message: /Unknown resource limit: unknown/});
    for (const maxInputBytes of [0, -1, 1.5, Infinity, NaN, Number.MAX_SAFE_INTEGER + 1, '2']) {
      assert.throws(() => Reflect.apply(convert, undefined, ['', {resourceLimits: {maxInputBytes}}]),
        {name: 'RangeError', message: /maxInputBytes.*positive safe integer/});
    }
    assert.throws(() => convert('', {resourceLimits: {maxNestingDepth: 257}}),
      {name: 'RangeError', message: /maxNestingDepth.*256/});
    assert.deepEqual(convert('', {resourceLimits: {maxInputBytes: 1, maxNodeCount: 1, maxNestingDepth: 1}}).diagnostics, []);
  });
}

test('public conversion boundaries identify wrong source and option arguments', () => {
  for (const convert of [parseSteamCommunityBbcode, steamCommunityBbcodeToGfm, gfmToSteamCommunityBbcode]) {
    for (const source of [undefined, null, 1, true, []]) {
      assert.throws(() => Reflect.apply(convert, undefined, [source]), {name: 'TypeError', message: /source.*string/i});
    }
    for (const options of [null, [], 1, true, '']) {
      assert.throws(() => Reflect.apply(convert, undefined, ['', options]), {name: 'TypeError', message: /options.*object/i});
    }
    assert.throws(() => Reflect.apply(convert, undefined, ['', {misspelled: true}]), {name: 'TypeError', message: /Unknown.*option.*misspelled/i});
  }
  const prepared = parseSteamCommunityBbcode('X');
  for (const options of [null, [], 1, true, '']) {
    assert.throws(() => Reflect.apply(steamCommunityBbcodeToMdast, undefined, [prepared, options]),
      {name: 'TypeError', message: /Conversion options.*object/});
  }
  assert.throws(() => Reflect.apply(steamCommunityBbcodeToMdast, undefined, [{...prepared}]),
    {name: 'TypeError', message: /issued by parseSteamCommunityBbcode/});
  assert.throws(() => steamCommunityBbcodeToMdast(prepared, {profile: 'review'}),
    {name: 'RangeError', message: /profile cannot be changed/});
  assert.throws(() => steamCommunityBbcodeToMdast(prepared, {resourceLimits: {maxInputBytes: 1}}),
    {name: 'TypeError', message: /limits must be supplied when parsing/});
  assert.throws(() => Reflect.apply(steamCommunityBbcodeToMdast, undefined, [prepared, {misspelled: true}]),
    {name: 'TypeError', message: /Unknown conversion option.*misspelled/});
  assert.deepEqual(steamCommunityBbcodeToMdast(prepared, {profile: 'workshop-item'}).diagnostics, []);
});

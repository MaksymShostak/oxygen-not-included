// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {parseSteamCommunityBbcode, steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from 'steam-community-bbcode';

const source = '[b]Packed[/b]';
const parsed = parseSteamCommunityBbcode(source);
assert.equal(parsed.profile, 'workshop-item');
assert.equal(parsed.source, source);
assert.deepEqual(parsed.diagnostics, []);
const tag = parsed.children[0];
assert.ok(tag?.type === 'steamTag');
assert.equal(tag.tagName, 'b');
assert.equal(tag.children[0]?.rawSource, 'Packed');
assert.ok(Object.isFrozen(parsed));
assert.ok(!('cst' in parsed) && !('tokens' in parsed));

const semantic = steamCommunityBbcodeToMdast(parsed);
const paragraph = semantic.value.children[0];
assert.ok(paragraph?.type === 'paragraph');
assert.equal(paragraph.children[0]?.type, 'strong');
const markdown = steamCommunityBbcodeToGfm('[u]Packed[/u]');
assert.equal(markdown.value, 'Packed\n');
assert.ok(markdown.diagnostics.some(d => d.code === 'STEAM_UNDERLINE_LOWERED_TO_TEXT'));
const video = steamCommunityBbcodeToMdast('[video mp4=https://example.org/video.mp4][/video]');
assert.equal(video.value.children[0]?.type, 'steamEmbeddedMedia');

// SPDX-License-Identifier: AGPL-3.0-only
import {parseSteamCommunityBbcode} from 'steam-community-bbcode';

const parsed = parseSteamCommunityBbcode('[b]Text[/b]', {profile: 'workshop-item', resourceLimits: {maxInputBytes: 1024}});
const source: string = parsed.source;
void source;
// @ts-expect-error Input is BBCode source text.
parseSteamCommunityBbcode(12);
// @ts-expect-error Profile identifiers come from the validated registry.
parseSteamCommunityBbcode('', {profile: 'workshop'});
// @ts-expect-error Resource dimensions are numeric.
parseSteamCommunityBbcode('', {resourceLimits: {maxInputBytes: '1024'}});
// @ts-expect-error Public parse results are readonly.
parsed.source = 'replacement';
// @ts-expect-error The syntax sequence is readonly.
parsed.children.push({type: 'steamText', value: 'injected'});

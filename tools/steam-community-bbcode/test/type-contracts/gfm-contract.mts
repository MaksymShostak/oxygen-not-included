// SPDX-License-Identifier: AGPL-3.0-only
import {steamCommunityBbcodeToGfm} from 'steam-community-bbcode';
import type {ConversionResult} from 'steam-community-bbcode';

const result: ConversionResult<string> = steamCommunityBbcodeToGfm('[b]Text[/b]', {profile: 'workshop-item'});
// @ts-expect-error The serializing API accepts source strings, not arbitrary MDAST.
steamCommunityBbcodeToGfm({type: 'root', children: []});
// @ts-expect-error A result object is not the serialized string alone.
const text: string = result;
void text;

// SPDX-License-Identifier: AGPL-3.0-only
import type {SteamMdastRoot, SteamUnderline, SteamNoParse, SteamAttributedBlockquote, SteamBlockSpoiler} from 'steam-community-bbcode';

const opaque: SteamNoParse = {type: 'steamNoParse', value: '[b]*literal*[/b]'};
const nested: SteamMdastRoot = {type: 'root', children: [{type: 'paragraph', children: [
  {type: 'strong', children: [{type: 'steamUnderline', children: [
    {type: 'steamSpoiler', children: [opaque]},
  ]}]},
]}]};
const invalidChild: SteamUnderline = {type: 'steamUnderline', children: [
  // @ts-expect-error An inline underline cannot own a block paragraph.
  {type: 'paragraph', children: []},
]};
// @ts-expect-error Opaque source is a string literal value, not child elements.
const invalidOpaque: SteamNoParse = {type: 'steamNoParse', children: []};
void [nested, invalidChild, invalidOpaque];

const quote: SteamAttributedBlockquote = {type: 'steamAttributedBlockquote', author: 'Ada', steamCommentId: '42',
  children: [{type: 'paragraph', children: [{type: 'steamColor', color: '#123456', children: [opaque]}]}]};
const blockSpoiler: SteamBlockSpoiler = {type: 'steamBlockSpoiler', children: [quote, {type: 'steamPullQuote', children: []}]};
const blockRoot: SteamMdastRoot = {type: 'root', children: [blockSpoiler]};
const badBlockSpoiler: SteamBlockSpoiler = {type: 'steamBlockSpoiler', children: [
  // @ts-expect-error Flow content requires a paragraph around inline text.
  {type: 'text', value: 'X'},
]};
void [blockRoot, badBlockSpoiler];

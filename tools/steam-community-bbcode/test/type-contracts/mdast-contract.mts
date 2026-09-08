// SPDX-License-Identifier: AGPL-3.0-only
import {steamCommunityBbcodeToMdast, parseSteamCommunityBbcode} from 'steam-community-bbcode';
import type {SteamMdastRoot, ConversionResult, ConversionDiagnostic} from 'steam-community-bbcode';

const result: ConversionResult<SteamMdastRoot> = steamCommunityBbcodeToMdast('[b]Text[/b]');
const root: 'root' = result.value.type;
steamCommunityBbcodeToMdast(parseSteamCommunityBbcode('Text'));
// @ts-expect-error The source is a string or an issued parse result.
steamCommunityBbcodeToMdast(12);
// @ts-expect-error Native MDAST node discriminants are closed.
const wrongRoot: SteamMdastRoot = {type: 'document', children: []};
// @ts-expect-error Diagnostics are returned as a readonly sequence.
result.diagnostics.push({});
void [root, wrongRoot];

const invalidCode: ConversionDiagnostic = {
  // @ts-expect-error Diagnostic identifiers are closed and mechanically checked.
  code: 'STEAM_SILENTLY_GOOD', severity: 'warning', fidelity: 'unsupported', message: 'invalid', scope: 'input',
};
// @ts-expect-error Metadata coordinates are readonly; editable MDAST owns separate positions.
result.coverage.constructs[0]!.sourceSpan.start.offset = 0;
void invalidCode;

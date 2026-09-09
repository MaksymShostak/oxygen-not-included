// SPDX-License-Identifier: AGPL-3.0-only
import {gfmToSteamCommunityBbcode} from 'steam-community-bbcode';
import type {PartialConversionResult, GfmToSteamBbcodeOptions, GfmConversionResourceLimits} from 'steam-community-bbcode';

const limits: GfmConversionResourceLimits = {maxInputBytes: 4096, maxOutputBytes: 16_384, maxNodeCount: 500, maxNestingDepth: 32};
const options: GfmToSteamBbcodeOptions = {resourceLimits: limits};
const result: PartialConversionResult<string> = gfmToSteamCommunityBbcode('## Heading', options);
const direction: 'gfm-to-steam' = result.coverage.direction;
for (const diagnostic of result.unsupportedSourceNodes) {
  const scope: 'gfm-node' = diagnostic.scope;
  const nodeType: string = diagnostic.nodeType;
  void [scope, nodeType];
}

// Negative contracts are type-level assertions, with no checker suppression.
type Assert<T extends true> = T;
type RejectNonStringInput = Assert<number extends Parameters<typeof gfmToSteamCommunityBbcode>[0] ? false : true>;
type RejectStringLimit = Assert<string extends GfmConversionResourceLimits['maxInputBytes'] ? false : true>;
type RejectSteamAttributeLimit = Assert<'maxAttributeBytes' extends keyof GfmConversionResourceLimits ? false : true>;
type RejectUndocumentedProfile = Assert<'profile' extends keyof GfmToSteamBbcodeOptions ? false : true>;
type RequirePartialResult = Assert<{value: string} extends PartialConversionResult<string> ? false : true>;
const negativeContracts: [RejectNonStringInput, RejectStringLimit, RejectSteamAttributeLimit, RejectUndocumentedProfile, RequirePartialResult] = [true, true, true, true, true];
void [direction, negativeContracts];

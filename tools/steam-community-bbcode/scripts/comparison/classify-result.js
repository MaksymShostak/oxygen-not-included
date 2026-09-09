// SPDX-License-Identifier: AGPL-3.0-only
import {isDeepStrictEqual} from 'node:util';
/**
 * @typedef {'PASS_EXACT' | 'PASS_EQUIVALENT' | 'PASS_DIAGNOSED_LOSS' | 'FAIL_WRONG_STRUCTURE' | 'FAIL_SILENT_LOSS' | 'FAIL_RAW_SOURCE_MARKUP' | 'FAIL_EXCEPTION' | 'NOT_SUPPORTED_BY_PROJECT'} ComparisonStatus
 * @typedef {{applicable: boolean, source: string, output: string, error?: string, expectedOutput?: string, expectedTree: unknown, actualTree: unknown, requiresDiagnosis: boolean, diagnostics: readonly string[]}} ComparisonObservation
 * diagnostics contains per-input target-loss/fallback diagnostics, not unrelated warnings.
 */
/** @param {ComparisonObservation} observation @returns {ComparisonStatus} */
export function classifyResult(observation) {
  if (!observation.applicable) return 'NOT_SUPPORTED_BY_PROJECT';
  if (observation.error !== undefined) return 'FAIL_EXCEPTION';
  if (!isDeepStrictEqual(observation.actualTree, observation.expectedTree)) {
    return observation.output.trim() === observation.source.trim() ? 'FAIL_RAW_SOURCE_MARKUP' : 'FAIL_WRONG_STRUCTURE';
  }
  if (observation.requiresDiagnosis) return observation.diagnostics.length ? 'PASS_DIAGNOSED_LOSS' : 'FAIL_SILENT_LOSS';
  return observation.output === observation.expectedOutput ? 'PASS_EXACT' : 'PASS_EQUIVALENT';
}

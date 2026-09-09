// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import {reverseCases} from './conformance/reverse-cases.js';
import {executeReverseCase} from './conformance/execute-reverse-cases.js';

for (const fixture of reverseCases) test(`reverse conformance: ${fixture.id}`, () => { executeReverseCase(fixture); });

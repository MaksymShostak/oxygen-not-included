// SPDX-License-Identifier: AGPL-3.0-only
// Process boundary for the unmodified comparator's documented string API.
import {readFileSync} from 'node:fs';
import {createRequire} from 'node:module';

const require = createRequire(new URL('../../comparison/node/package.json', import.meta.url));
/** @type {unknown} */
const convert = require('bbcode-to-markdown');
if (typeof convert !== 'function') throw new TypeError('bbcode-to-markdown must export its documented conversion function');
/** @type {unknown} */
const output = Reflect.apply(convert, undefined, [readFileSync(0, 'utf8')]);
if (typeof output !== 'string') throw new TypeError('bbcode-to-markdown returned a non-string result');
process.stdout.write(output);

// SPDX-License-Identifier: AGPL-3.0-only
import {lstat, rm} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';

// Only this package's generated declarations are disposable. A redirect must
// never turn this cleanup into removal of user-owned source or another package.
const directory = new URL('../types/', import.meta.url);
const status = await lstat(directory).catch((/** @type {NodeJS.ErrnoException} */ error) => {
  if (error.code === 'ENOENT') return undefined;
  throw error;
});
if (status && (!status.isDirectory() || status.isSymbolicLink())) {
  throw new Error(`Generated declaration directory is redirected: ${fileURLToPath(directory)}`);
}
if (status) await rm(directory, {recursive: true});

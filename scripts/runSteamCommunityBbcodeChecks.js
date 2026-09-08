import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

// npm owns command/platform dispatch. Use the invoking npm installation, including
// the repository's documented npm-exec selection, without installing global tools.
const npmCli = process.env.npm_execpath;
if (!npmCli) {
  throw new Error('Run npm run check:converter or npm run check:affected.');
}
const root = fileURLToPath(new URL('../', import.meta.url));
const result = spawnSync(process.execPath,
  [npmCli, '--prefix', 'tools/steam-community-bbcode', 'run', 'check'],
  {cwd: root, stdio: 'inherit', windowsHide: true});
if (result.error) throw result.error;
if (result.signal) throw new Error(`Converter verification terminated by ${result.signal}.`);
process.exitCode = result.status ?? 1;

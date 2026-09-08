import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { parseArgs } from "node:util";
import { fileURLToPath, pathToFileURL } from "node:url";
import { selectWorkingTreeChecks } from "./selectPullRequestChecks.js";

const REPOSITORY_ROOT = fileURLToPath(new URL("../", import.meta.url));
const PIPELINE_PROJECT = "tools/oni-mod-pipeline/src/OniModPipeline/OniModPipeline.csproj";
const PIPELINE_SOLUTION = "tools/oni-mod-pipeline/OniModPipeline.slnx";
const MOD = "mods/delivery-temperature-limit-supercooled";

export function affectedCheckCommands(selected) {
  const commands = [];
  if (selected.sdlc) {
    commands.push(
      ["node", "scripts/runRepositoryPython.js", "-m", "unittest", "discover", "-s", "tests/sdlc", "-v"],
      ["node", "scripts/runRepositoryPython.js", "-m", "unittest", "discover", "-s", "tests", "-p", "test_set_up_*.py"],
      ["node", "--experimental-vm-modules", "node_modules/jest/bin/jest.js", "--runInBand"],
    );
  }
  if (selected.pipeline || selected.mods) commands.push(["dotnet", "restore", PIPELINE_SOLUTION, "--locked-mode"]);
  if (selected.pipeline) commands.push(["dotnet", "test", "--project", "tools/oni-mod-pipeline/tests/OniModPipeline.Tests/OniModPipeline.Tests.csproj", "--no-restore"]);
  if (selected.mods) {
    for (const command of ["validate", "build", "test"]) {
      commands.push(["dotnet", "run", "--project", PIPELINE_PROJECT, "--no-restore", "--", command, "--mod", MOD]);
    }
  }
  return commands;
}

export function runAffectedChecks({ root = REPOSITORY_ROOT, base } = {}) {
  if (!base) {
    const active = JSON.parse(readFileSync(resolve(root, ".sdlc/runtime/active.json"), "utf8"));
    base = active.startingHead;
  }
  const selected = selectWorkingTreeChecks({ root, base });
  console.log("Affected scopes from " + base + ": " + JSON.stringify(selected));
  for (const [executable, ...args] of affectedCheckCommands(selected)) {
    console.log("Running: " + JSON.stringify([executable, ...args]));
    const result = spawnSync(executable, args, { cwd: root, stdio: "inherit", windowsHide: true });
    if (result.error) throw result.error;
    if (result.signal) throw new Error("Check terminated by " + result.signal);
    if (result.status !== 0) return result.status ?? 1;
  }
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const { values } = parseArgs({ options: { base: { type: "string" } }, allowPositionals: false });
    process.exitCode = runAffectedChecks({ base: values.base });
  } catch (error) {
    console.error("Affected verification failed: " + error.message + ". Supply --base <full commit SHA> or begin an SDLC task.");
    process.exitCode = 1;
  }
}

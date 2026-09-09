import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { selectPullRequestChecks } from "../scripts/selectPullRequestChecks.js";
import * as selector from "../scripts/selectPullRequestChecks.js";
import { affectedCheckCommands } from "../scripts/runAffectedChecks.js";

let root;
let base;
function git(args) {
  const result = spawnSync("git", args, {
    cwd: root, encoding: "utf8", windowsHide: true,
    env: { ...process.env, GIT_CONFIG_NOSYSTEM: "1", GIT_CONFIG_GLOBAL: join(root, "empty-config") },
  });
  if (result.error || result.status !== 0) throw new Error(result.stderr || result.error?.message);
  return result.stdout.trim();
}
function write(path) {
  mkdirSync(dirname(join(root, path)), { recursive: true });
  writeFileSync(join(root, path), "Input owned by this fixture\n");
}
function commit(path) {
  git(["add", "--", path]);
  git(["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "-qm", "Fixture"]);
  return git(["rev-parse", "HEAD"]);
}
beforeEach(() => {
  root = mkdtempSync(join(tmpdir(), "oni-scopes-"));
  writeFileSync(join(root, "empty-config"), "");
  git(["init", "--initial-branch=main"]);
  write("initial.md");
  base = commit("initial.md");
});
test("local selection includes untracked and staged input but excludes ignored run evidence", () => {
  write("scripts/validate_sdlc_pr.py");
  expect(typeof selector.selectWorkingTreeChecks).toBe("function");
  expect(selector.selectWorkingTreeChecks({ root, base })).toEqual({ sdlc: true, pipeline: false, mods: false, converter: false });
  git(["add", "--", "scripts/validate_sdlc_pr.py"]);
  expect(selector.selectWorkingTreeChecks({ root, base }).sdlc).toBe(true);
  base = commit("scripts/validate_sdlc_pr.py");
  writeFileSync(join(root, ".gitignore"), ".sdlc/runtime/\n");
  base = commit(".gitignore");
  write(".sdlc/runtime/verification/full.json");
  expect(selector.selectWorkingTreeChecks({ root, base })).toEqual({ sdlc: false, pipeline: false, mods: false, converter: false });
});
afterEach(() => {
  const target = resolve(root);
  if (dirname(target) !== resolve(tmpdir()) || !basename(target).startsWith("oni-scopes-")) throw new Error("Unexpected fixture path");
  rmSync(target, { recursive: true, force: true });
});
test.each([
  ["scripts/validate_sdlc_pr.py", ["sdlc"]],
  [".github/pull_request_template.md", ["sdlc"]],
  ["tools/oni-mod-pipeline/src/OniModPipeline/Program.cs", ["pipeline", "mods"]],
  ["mods/example/Mod.cs", ["mods"]],
  ["global.json", ["pipeline", "mods"]],
  ["docs/plans/converter.md", []],
  ["package-lock.json", ["sdlc"]],
  ["tools/steam-community-bbcode/src/index.js", ["converter"]],
  ["tools/steam-community-bbcode/package-lock.json", ["converter"]],
  ["scripts/runSteamCommunityBbcodeChecks.js", ["converter"]],
  [".github/workflows/steam-community-bbcode.yml", ["sdlc", "converter"]],
  [".github/workflows/steam-community-bbcode-release.yml", ["sdlc", "converter"]],
])("ONI routes %s to the actual affected components", (path, expected) => {
  write(path);
  const head = commit(path);
  const selected = selectPullRequestChecks({ root, base, head });
  expect(Object.entries(selected).filter(([, needed]) => needed).map(([name]) => name)).toEqual(expected);
});

test("untracked converter input selects its actual verification without .NET checks", () => {
  write("tools/steam-community-bbcode/test/contract.test.js");
  const selected = selector.selectWorkingTreeChecks({ root, base });
  expect(selected).toEqual({sdlc: false, pipeline: false, mods: false, converter: true});
  expect(affectedCheckCommands(selected)).toEqual([["node", "scripts/runSteamCommunityBbcodeChecks.js"]]);
});

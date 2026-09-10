import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { parse } from "yaml";

const workflow = parse(readFileSync(
  new URL("../.github/workflows/steam-community-bbcode.yml", import.meta.url),
  "utf8",
));
const { jobs } = workflow;
const bash = process.platform === "win32"
  ? resolve(execFileSync("git", ["--exec-path"], { encoding: "utf8", windowsHide: true }).trim(), "../../../bin/bash.exe")
  : "bash";

test("short BBCode checks must succeed before the expensive mutation stage", () => {
  expect(jobs.converter.needs).toBe("dependency-review");
  expect(jobs.mutation.needs).toBe("converter");
  // Native needs propagation must not be bypassed by always() or tolerated failures.
  for (const name of ["dependency-review", "converter", "mutation"]) {
    expect(jobs[name].if).toBeUndefined();
    expect(jobs[name]["continue-on-error"]).toBeUndefined();
  }
  expect(jobs.mutation.strategy.matrix.profile).toEqual(["library", "cli"]);
});

test("dependency review stays PR-only without skipping the job on push or manual runs", () => {
  expect(Object.keys(workflow.on)).toEqual(["pull_request", "push", "workflow_dispatch"]);
  const review = jobs["dependency-review"];
  expect(review.if).toBeUndefined();
  expect(review.needs).toBeUndefined();
  expect(review.steps.find(step => step.name === "Review introduced dependencies").if)
    .toBe("github.event_name == 'pull_request' && steps.scope.outputs.converter == 'true'");
});

test("the required qualification check observes every stage even when a prerequisite fails", () => {
  expect(jobs.qualification.name).toBe("BBCode / ubuntu-latest / Node 24.20.0");
  expect(jobs.qualification.needs).toEqual(["dependency-review", "converter", "mutation"]);
  expect(jobs.qualification.if).toBe("always()");
  expect(jobs.qualification.steps[0].env).toEqual({
    DEPENDENCY_REVIEW_RESULT: "${{ needs.dependency-review.result }}",
    CHECK_RESULT: "${{ needs.converter.result }}",
    MUTATION_RESULT: "${{ needs.mutation.result }}",
  });
  expect(jobs.qualification.steps[0].if).toBeUndefined();
  expect(jobs.qualification.steps[0]["continue-on-error"]).toBeUndefined();
});

const successfulResults = {
  DEPENDENCY_REVIEW_RESULT: "success",
  CHECK_RESULT: "success",
  MUTATION_RESULT: "success",
};
const aggregateCases = [
  ["all stages succeed", successfulResults, true],
  ...Object.keys(successfulResults).flatMap(stage =>
    ["failure", "cancelled", "skipped", ""].map(result => [
      `${stage} is ${result || "absent"}`,
      { ...successfulResults, [stage]: result },
      false,
    ]),
  ),
];

test.each(aggregateCases)("the actual Bash qualification gate: %s", (_name, results, passes) => {
  const result = spawnSync(bash, ["--noprofile", "--norc", "-eo", "pipefail", "-c", jobs.qualification.steps[0].run], {
    encoding: "utf8",
    windowsHide: true,
    env: { ...process.env, ...results },
  });
  expect(result.error).toBeUndefined();
  expect(result.status).not.toBeNull();
  expect(result.status === 0).toBe(passes);
});

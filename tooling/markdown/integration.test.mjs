import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import {
  readFileSync,
  writeFileSync,
  mkdirSync,
  mkdtempSync,
  rmSync,
  copyFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import {
  executeQuality,
  inspectSelection,
  readExecutionProfile,
  stageCandidate,
  validateQualityResult,
} from "@hadden-industries/markdown-quality";

const root = fileURLToPath(new URL("../../", import.meta.url));
const sourceSha = "47febbe1b6f3282814e77db7ea13eac72b4928ed";
const json = (file) => JSON.parse(readFileSync(file, "utf8"));

test("ONI locks the independently qualified archives and shared workflow", () => {
  const profile = readExecutionProfile({ root });
  assert.equal(profile.runtimes.node, process.versions.node);
  const lock = json(join(root, profile.toolchain.lockFile));
  const manifest = json(new URL("package.json", import.meta.url));
  for (const [name, spec] of Object.entries({
    ...manifest.devDependencies,
    ...manifest.optionalDependencies,
  })) {
    assert.equal(lock.packages[`node_modules/${name}`].resolved, spec);
    assert.equal(
      lock.packages[`node_modules/${name}`].integrity,
      "sha512-" +
        createHash("sha512")
          .update(readFileSync(new URL(spec.slice(5), import.meta.url)))
          .digest("base64"),
    );
  }
  assert.equal(
    createHash("sha256")
      .update(readFileSync(join(root, profile.toolchain.coreArchive)))
      .digest("hex"),
    "ad51a2ccb721a2b14a05e8c1a61d9a1b7b3276d55a90beadb4c9f33d49702127",
  );
  assert.ok(
    readFileSync(
      join(root, ".github/workflows/oni-checks.yml"),
      "utf8",
    ).includes(`markdown-quality.yml@${sourceSha}`),
  );
});

test("full selection accounts for tracked ONI Markdown and authored anchors", async () => {
  const report = await inspectSelection({ root });
  validateQualityResult(report, {
    operation: "inspect",
    selectionMode: "full",
    exitCode: 0,
  });
  for (const path of [
    "AGENTS.md",
    "README.md",
    "docs/reviews/delivery-temperature-limit-steam-copy-review.md",
    "mods/delivery-temperature-limit-supercooled/README.md",
    "tools/oni-mod-pipeline/manual/profile-reference.md",
  ])
    assert.ok(report.selection.files.includes(path), path);
  assert.ok(
    report.selection.inventory.every((entry) => entry.decision === "selected"),
  );
  for (const entry of report.selection.inventory)
    assert.ok(report.selection.files.includes(entry.path), entry.path);
});

test("logical README transport preserves exclusion bytes and rejects authored missing alternatives", async () => {
  const directory = mkdtempSync(join(tmpdir(), "oni-markdown-document-"));
  try {
    const config = json(join(root, ".markdown-quality.json"));
    config.exclude.push("excluded.md");
    writeFileSync(
      join(directory, ".markdown-quality.json"),
      JSON.stringify(config),
    );
    const bytes = Buffer.from([0xff, 13, 10]);
    const excluded = await executeQuality({
      root: directory,
      mode: "format",
      document: {
        path: "excluded.md",
        requestId: "excluded",
        contentBase64: bytes.toString("base64"),
      },
    });
    validateQualityResult(excluded, { requestId: "excluded", exitCode: 0 });
    assert.equal(excluded.document.contentBase64, bytes.toString("base64"));
    const contentBase64 = Buffer.from(
      "# Owner\n\n![](https://example.org/unknown.png)\n",
    ).toString("base64");
    const failed = await executeQuality({
      root: directory,
      mode: "format",
      document: {
        path: "README.md",
        requestId: "authored",
        contentBase64,
      },
    });
    validateQualityResult(failed, { requestId: "authored", exitCode: 1 });
    assert.equal(failed.document.contentBase64, contentBase64);
    assert.ok(
      failed.diagnostics.some(
        (item) => item.rule === "markdown/require-alt-text",
      ),
    );
    assert.deepEqual(failed.written, []);
  } finally {
    rmSync(directory, { recursive: true });
  }
});

test("actual advisory findings succeed normally and strict warnings refuse admission", async () => {
  const contentBase64 = Buffer.from(
    "# Title\n\n## Repeated\n\nFirst.\n\n## Repeated\n\nSecond.\n",
  ).toString("base64");
  const document = {
    path: "README.md",
    requestId: "warning-contract",
    contentBase64,
  };
  const normal = await executeQuality({ root, mode: "format", document });
  validateQualityResult(normal, { exitCode: 0, requestId: document.requestId });
  assert.equal(normal.outcome, "findings");
  assert.ok(normal.diagnostics.some((item) => item.severity === "warning"));
  const strict = await executeQuality({
    root,
    mode: "format",
    document,
    strict: true,
  });
  validateQualityResult(strict, { exitCode: 1, requestId: document.requestId });
  assert.equal(strict.document.contentBase64, contentBase64);
});

test("C#/Node bridge formats at the final logical path without scratch Markdown", () => {
  const request = {
    path: "README.md",
    requestId: "oni-integration",
    contentBase64: Buffer.from("# Title\n\nText.\n").toString("base64"),
  };
  const report = JSON.parse(
    execFileSync(
      process.execPath,
      [fileURLToPath(new URL("readme-document.mjs", import.meta.url)), root],
      {
        input: JSON.stringify(request),
        encoding: "utf8",
        timeout: 40000,
        maxBuffer: 8388608,
      },
    ),
  );
  validateQualityResult(report, {
    operation: "format",
    requestId: request.requestId,
    exitCode: 0,
  });
  assert.equal(report.document.path, request.path);
  assert.deepEqual(report.written, []);
});

test("shared staging keeps trusted policy authoritative over candidate policy and scripts", async () => {
  const directory = mkdtempSync(join(tmpdir(), "oni-markdown-staging-"));
  const trustedRoot = join(directory, "trusted");
  const sourceRoot = join(directory, "candidate");
  const outputRoot = join(directory, "staged");
  try {
    mkdirSync(trustedRoot);
    mkdirSync(sourceRoot);
    for (const file of [
      ".markdown-quality.json",
      ".markdown-quality-execution.json",
      ".node-version",
      ".python-version",
    ])
      copyFileSync(join(root, file), join(trustedRoot, file));
    writeFileSync(
      join(sourceRoot, ".markdown-quality.json"),
      JSON.stringify({ schemaVersion: 2, include: [], exclude: ["**/*.md"] }),
    );
    writeFileSync(
      join(sourceRoot, "package.json"),
      JSON.stringify({
        scripts: { preinstall: "throw new Error('candidate executed')" },
      }),
    );
    writeFileSync(join(sourceRoot, ".gitignore"), "*.md\n");
    writeFileSync(
      join(sourceRoot, "README.md"),
      "# Broken\n\n[Missing](missing.md)\n",
    );
    const stage = stageCandidate({ sourceRoot, trustedRoot, outputRoot });
    const result = await executeQuality({
      root: outputRoot,
      mode: "check",
      config: stage.configPath,
    });
    validateQualityResult(result, { operation: "check", exitCode: 1 });
    assert.ok(result.selection.files.includes("README.md"));
    assert.ok(
      result.diagnostics.some(
        (item) => item.source === "links" && item.rule === "local-target",
      ),
    );
  } finally {
    rmSync(directory, { recursive: true });
  }
});

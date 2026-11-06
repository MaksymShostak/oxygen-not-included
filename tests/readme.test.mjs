import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { validateQualityResult } from "@hadden-industries/markdown-quality";
import { labelCompatibilityImages } from "../tools/readme/compatibility-badges.mjs";

const root = fileURLToPath(new URL("../", import.meta.url));
const bridge = fileURLToPath(
  new URL("../tools/readme/canonicalize.mjs", import.meta.url),
);
const readme = new URL("../README.md", import.meta.url);
function canonicalize(content, requestId = "oni-readme") {
  const original = readFileSync(readme);
  const document = {
    path: "README.md",
    requestId,
    contentBase64: Buffer.from(content).toString("base64"),
  };
  const response = spawnSync(process.execPath, [bridge, root], {
    input: JSON.stringify(document),
    encoding: "utf8",
    timeout: 40_000,
    maxBuffer: 8_388_608,
  });
  assert.ifError(response.error);
  const result = JSON.parse(response.stdout);
  validateQualityResult(result);
  assert.equal(result.exitCode, response.status);
  assert.equal(result.document.path, document.path);
  assert.equal(result.document.requestId, requestId);
  assert.deepEqual(result.writtenPaths, []);
  assert.deepEqual(readFileSync(readme), original);
  return result;
}

test("domain badge labels affect actual images while preserving literal code and unknown labels", () => {
  const base =
    "https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/";
  const files = [
    "VanillaYes.png",
    "Dlc1Yes.png",
    "Dlc2Yes.png",
    "Dlc3Yes.png",
    "Dlc4Yes.png",
    "Dlc5Yes.png",
  ];
  const labels = [
    "Base game supported",
    "Spaced Out! supported",
    "The Frosty Planet Pack supported",
    "The Bionic Booster Pack supported",
    "The Prehistoric Planet Pack supported",
    "The Aquatic Planet Pack supported",
  ];
  for (const [index, file] of files.entries()) {
    const url = base + file;
    const literal = `![](${url})`;
    const input = `${literal}\n\n\`\`\`\n${literal}\n\`\`\`\n\n\`${literal}\`\n`;
    const output = labelCompatibilityImages(input);
    assert.ok(output.includes(`![${labels[index]}](${url})`));
    assert.ok(output.includes(`\`\`\`\n${literal}\n\`\`\``));
    assert.ok(output.includes(`\`${literal}\``));
  }
  const unknown = `![](https://example.org/unknown.png) ![Owner label](${base}Dlc1Yes.png)\n`;
  assert.equal(labelCompatibilityImages(unknown), unknown);
});

test("README bridge formats and checks supplied bytes without replacing the physical README", () => {
  const result = canonicalize("#  Owner\n\nText.\n");
  assert.equal(result.exitCode, 0);
  assert.equal(result.operation, "format");
  assert.equal(result.operationStatus, "completed");
  assert.equal(
    Buffer.from(result.document.contentBase64, "base64").toString("utf8"),
    "# Owner\n\nText.\n",
  );
});

test("README bridge refuses missing image alternatives before replacement", () => {
  const input = "# Owner\n\n![](https://example.org/unknown.png)\n";
  const result = canonicalize(input);
  assert.equal(result.exitCode, 1);
  assert.equal(
    Buffer.from(result.document.contentBase64, "base64").toString("utf8"),
    input,
  );
});

test("README bridge does not mistake a completed format for a passing NFC check", () => {
  const input = "# Owner\n\nCafe\u0301.\n";
  const result = canonicalize(input);
  assert.equal(result.exitCode, 1);
  assert.equal(result.operation, "check");
  assert.ok(
    result.diagnostics.some((item) => item.rule === "quality/non-nfc-prose"),
  );
  assert.equal(
    Buffer.from(result.document.contentBase64, "base64").toString("utf8"),
    input,
  );
});

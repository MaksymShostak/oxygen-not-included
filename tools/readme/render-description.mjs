import { labelCompatibilityImages } from "./compatibility-badges.mjs";

try {
  const chunks = [];
  let bytes = 0;
  for await (const chunk of process.stdin) {
    bytes += chunk.length;
    if (bytes > 2097152)
      throw new Error("Generated description exceeds its limit.");
    chunks.push(chunk);
  }
  const markdown = new TextDecoder("utf-8", { fatal: true }).decode(
    Buffer.concat(chunks),
  );
  process.stdout.write(labelCompatibilityImages(markdown));
} catch {
  process.stderr.write("DESCRIPTION_RENDER_FAILED: README was not replaced.\n");
  process.exitCode = 2;
}

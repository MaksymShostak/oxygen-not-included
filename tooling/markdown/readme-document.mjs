// C#/Node transport for the domain README generator. Markdown semantics stay in the package.
import {
  executeQuality,
  readExecutionProfile,
  validateQualityResult,
} from "@hadden-industries/markdown-quality";

try {
  const root = process.argv[2];
  const profile = readExecutionProfile({ root });
  if (profile.runtimes.node !== process.versions.node)
    throw new Error("README runtime differs from its qualified declaration.");
  const chunks = [];
  let bytes = 0;
  for await (const chunk of process.stdin) {
    bytes += chunk.length;
    if (bytes > profile.requestBytes)
      throw new Error("README request exceeds the transport limit.");
    chunks.push(chunk);
  }
  const document = JSON.parse(Buffer.concat(chunks).toString("utf8"));
  const result = await executeQuality(
    { root, mode: "format", document, limits: profile.limits },
    {
      requestBytes: profile.requestBytes,
      reportBytes: profile.reportBytes,
      checkerMs: profile.checkerMs,
      nodeOldSpaceMb: profile.nodeOldSpaceMb,
    },
  );
  validateQualityResult(result, {
    operation: "format",
    requestId: document.requestId,
    exitCode: result.exitCode,
  });
  process.stdout.write(JSON.stringify(result) + "\n");
  process.exitCode = result.exitCode;
} catch (error) {
  process.stderr.write(
    (error.code ?? "README_DOCUMENT_FAILED") + ": README was not replaced.\n",
  );
  process.exitCode = 2;
}

// Transport the C# producer's virtual README through the public package API.
import {
  executeQualityRequest,
  validateQualityResult,
} from "@hadden-industries/markdown-quality";

try {
  const root = process.argv[2];
  const chunks = [];
  let bytes = 0;
  for await (const chunk of process.stdin) {
    bytes += chunk.length;
    if (bytes > 8_388_608)
      throw new Error("README request exceeds its transport limit.");
    chunks.push(chunk);
  }
  const document = JSON.parse(
    new TextDecoder("utf-8", { fatal: true }).decode(Buffer.concat(chunks)),
  );
  const formatted = await executeQualityRequest({
    root,
    mode: "format",
    document,
  });
  validateQualityResult(formatted);
  let result = formatted;
  if (formatted.exitCode === 0) {
    // A v3 repair is not a check verdict. Check the returned candidate before
    // the C# owner can admit it for its guarded filesystem replacement.
    const checked = await executeQualityRequest({
      root,
      mode: "check",
      document: {
        ...document,
        contentBase64: formatted.document.contentBase64,
      },
    });
    validateQualityResult(checked);
    if (checked.exitCode !== 0) result = checked;
  }
  process.stdout.write(JSON.stringify(result) + "\n");
  process.exitCode = result.exitCode;
} catch (error) {
  process.stderr.write(
    (error.code ?? "README_DOCUMENT_FAILED") + ": README was not replaced.\n",
  );
  process.exitCode = 2;
}

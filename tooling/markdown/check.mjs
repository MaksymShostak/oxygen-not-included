// The reviewed installation owns execution; the separately checked-out tree is data.
import { runQuality } from "@hadden-industries/markdown-quality";

const result = await runQuality({ mode: "check", root: process.argv[2] });
process.stdout.write(JSON.stringify(result) + "\n");
if (result.selection.mode !== "full" || result.selection.files.length === 0) {
  throw new Error("Documentation qualification requires the complete authored scope.");
}
process.exitCode = result.exitCode;

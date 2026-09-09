# Command line

The installed executable is `steam-community-bbcode`. In the source checkout,
use `npm run cli --` before the same arguments. It calls the public library APIs.

```text
steam-community-bbcode to-gfm description.bbcode
steam-community-bbcode to-gfm --format=json --profile=workshop-item description.bbcode
steam-community-bbcode to-gfm --fail-on=lossy description.bbcode
steam-community-bbcode to-steam README.md
steam-community-bbcode coverage --format=json
steam-community-bbcode --help
```

`to-steam` implements the [partial reverse subset](gfm-to-steam-limitations.md).
Omit a filename or supply `-` for stdin. Use `--` before a filename beginning
with `-`. Input is strict UTF-8. The default input limit is 1 MiB, enforced during
reading; `--max-input-bytes=N` sets an explicit positive safe-integer override.

`--format=text` is the default for conversion. `--format=json` prints the complete
result, including diagnostics, occurrence coverage and reverse unsupported nodes.
Stdout contains only the requested result. Stderr emits one JSON object per
diagnostic, including argument and I/O errors. `--profile` is forward-only.

| Exit | Meaning |
| --- | --- |
| 0 | Conversion accepted under the selected policy |
| 1 | Conversion error or fidelity below the selected CLI threshold |
| 2 | Invalid arguments, I/O failure or malformed UTF-8 |

`--fail-on=none` is the default; conversion errors still fail. Other values are
`approximate`, `lossy` and `unsupported`, each rejecting that fidelity and worse.
A fidelity failure retains output for inspection. Check status before using it;
shell redirection can create a file even when conversion fails. The CLI has no
output-file overwrite option or automatic publication step.

`coverage` prints the shipped **conformance** report as JSON, also its default
format. It does not execute tests or measure runtime coverage. Other conversion
options and input files do not apply to this command.

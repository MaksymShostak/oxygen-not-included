# Partial GFM-to-Steam conversion

`gfmToSteamCommunityBbcode` consumes native GFM syntax and maps an explicit subset to Steam BBCode.
Its independent [matrix](steam-support-matrix.md#partial-gfm-to-steam) records executed cases; forward registry coverage does not establish reverse coverage.
Neither direction promises a byte-identical round trip.

The qualified subset includes headings 1–3, paragraphs, strong/emphasis/deletion, blockquotes, thematic breaks, ordinary unordered lists, ordered lists starting at 1, nested list structure, eligible code blocks, safe absolute links, images with empty alternative text, and rectangular unaligned tables.

Unsupported features keep their Markdown source as literal Steam text.
Current examples include headings 4–6, inline code, raw HTML, task state, other list starts, hard breaks, reference syntax, table alignment, resource titles and meaningful image alternative text.
Relative resources have no qualified Steam document base.
Code with language/metadata or a Steam closing delimiter is also preserved.

Preservation is deliberate: dropping an image's alt text, changing a list start or executing Markdown-shaped text as BBCode would lose meaning silently.
The literal encoder protects source brackets even when the input contains `[/noparse]`.
Unsupported output can therefore look like Markdown source in Steam; it does not pretend to be a faithful rendering of that unsupported feature.

Use `unsupportedSourceNodes` for feature-level explanations and `coverage.sourceNodes` for occurrence accounting.
A preserved parent retains its complete source span and accounts for descendants without interpreting them.
Whitespace and conventional list serialization can normalize in supported output.

The library does not fetch links, resolve repository-relative resources, upload images or verify live Steam behavior.
Default limits and their allocation/timing limitations are documented in the [security model](security-model.md).
